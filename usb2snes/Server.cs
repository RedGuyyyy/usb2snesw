using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Net.WebSockets;
using System.Net;

using usb2snes;

namespace usb2snes
{
    public class Server
    {
        SocketServer _h;
        private Thread _hT;

        private class QueueElementType {
            public WebSocket Socket { get; set; }
            public RequestType Request { get; set; }
            public AutoResetEvent Event { get; set; }
            public PortAndCount Port { get; set; }
        }

        // main server
        public Server()
        {
            _h = new SocketServer("http://localhost:8080/");
            _hT = new Thread(_h.Start);
            _hT.Start();
        }

        public void Stop()
        {
            _h.Stop();
            _hT.Abort();
            _hT.Join();
        }

        /// <summary>
        /// Scheduler takes input from the socket threads via the CommunicationQueue and sends
        /// the request to USB via a serial port interface.
        /// </summary>
        private class Scheduler
        {
            public Scheduler()
            {
                _thr = new Thread(this.Run);
                _thr.Start();
            }

            /// <summary>
            /// Run is the main operation thread.
            /// </summary>
            async public void Run()
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();

                while (!_stop)
                {
                    var t = _q.Dequeue();

                    if (t.Item1)
                    {
                        var elem = t.Item2;
                        var s = elem.Socket;
                        var req = elem.Request;
                        var ev = elem.Event;
                        var p = elem.Port.Port;

                        _socketHash[s.GetHashCode()] = s;
                        var socketOpcode = (OpcodeType)Enum.Parse(typeof(OpcodeType), req.Opcode);

                        // convert flags
                        var flags = usbint_server_flags_e.NONE;
                        if (req.Flags != null) foreach (var flag in req.Flags) flags |= (usbint_server_flags_e)Enum.Parse(typeof(usbint_server_flags_e), flag);

                        try
                        {
                            // perform snes operation
                            switch (socketOpcode)
                            {
                                case OpcodeType.Boot:
                                    {
                                        usbint_server_opcode_e opcode = usbint_server_opcode_e.BOOT;
                                        p.SendCommand(opcode, usbint_server_space_e.FILE, flags, req.Operands[0]);
                                        break;
                                    }
                                case OpcodeType.Menu:
                                case OpcodeType.Reset:
                                    {
                                        usbint_server_opcode_e opcode = (socketOpcode == OpcodeType.Menu) ? usbint_server_opcode_e.MENU_RESET
                                                                      : usbint_server_opcode_e.RESET;
                                        p.SendCommand(opcode, usbint_server_space_e.FILE, flags);
                                        break;
                                    }
                                case OpcodeType.Info:
                                    {
                                        usbint_server_opcode_e opcode = usbint_server_opcode_e.INFO;
                                        var version = (string)p.SendCommand(opcode, usbint_server_space_e.SNES, flags);
                                        ResponseType rsp = new ResponseType();
                                        rsp.Results = new List<string>();
                                        rsp.Results.Add(version);

                                        try
                                        {
                                            await s.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(rsp))), WebSocketMessageType.Text, true, CancellationToken.None);
                                        }
                                        catch (Exception e)
                                        {
                                            // Allow socket failures
                                        }
                                        break;
                                    }
                                case OpcodeType.Stream:
                                    // FIXME:
                                    break;

                                case OpcodeType.GetAddress:
                                    {
                                        // loop through all addresses and perform the fewest commands necessary to get all data in the same order requested
                                        int totalSize = 0;
                                        bool sizeOperand = false;
                                        foreach (var operand in req.Operands)
                                        {
                                            if (sizeOperand) totalSize += int.Parse(operand, System.Globalization.NumberStyles.HexNumber);
                                            sizeOperand = !sizeOperand;
                                        }

                                        Byte[] data = new Byte[totalSize];
                                        for (int i = 0; i < req.Operands.Count; i += 2)
                                        {
                                            var name = req.Operands[i + 0];
                                            var sizeStr = req.Operands[i + 1];

                                            usbint_server_space_e space = (usbint_server_space_e)Enum.Parse(typeof(usbint_server_space_e), req.Space);
                                            uint address = uint.Parse(name, System.Globalization.NumberStyles.HexNumber);
                                            int size = int.Parse(sizeStr, System.Globalization.NumberStyles.HexNumber);
                                            p.SendCommand(usbint_server_opcode_e.GET, space, usbint_server_flags_e.NORESP | usbint_server_flags_e.DATA64B | flags, address, (uint)size);
                                            Byte[] tempData = new Byte[Constants.MaxMessageSize];

                                            int readSize = (size + 63) & ~63;
                                            int readByte = 0;
                                            int writeSize = size;
                                            int writeByte = 0;

                                            while (readByte < readSize)
                                            {
                                                int readOffset = readByte % Constants.MaxMessageSize;
                                                int packetOffset = readByte % 64;
                                                readByte += p.GetData(tempData, readOffset, 64 - packetOffset); // Math.Min(readSize - readByte, Math.Min(Constants.MaxMessageSize - readOffset, 64 - packetOffset)));

                                                // send data
                                                if (readByte == readSize || (readByte - writeByte >= Constants.MaxMessageSize))
                                                {
                                                    int toWriteSize = Math.Min(writeSize - writeByte, Constants.MaxMessageSize);
                                                    try
                                                    {
                                                        s.SendAsync(new ArraySegment<byte>(tempData, 0, toWriteSize), WebSocketMessageType.Binary, readByte == readSize, CancellationToken.None).Wait();
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        // Allow socket failures
                                                    }
                                                    writeByte += toWriteSize;
                                                }
                                            }
                                        }
                                        break;
                                    }
                                case OpcodeType.PutAddress:
                                    {
                                        // TODO: add vector operations
                                        for (int i = 0; i < req.Operands.Count; i += 2)
                                        {
                                            var name = req.Operands[i + 0];
                                            var sizeStr = req.Operands[i + 1];

                                            usbint_server_space_e space = (usbint_server_space_e)Enum.Parse(typeof(usbint_server_space_e), req.Space);
                                            uint address = uint.Parse(name, System.Globalization.NumberStyles.HexNumber);
                                            int size = int.Parse(sizeStr, System.Globalization.NumberStyles.HexNumber);
                                            p.SendCommand(usbint_server_opcode_e.PUT, space, usbint_server_flags_e.NORESP | usbint_server_flags_e.DATA64B | flags, address, (uint)size);

                                            // get data and write to USB
                                            WebSocketReceiveResult result;
                                            int blockSize = 64;
                                            // simplify data receipt by allocating a full size buffer
                                            Byte[] receiveBuffer = new Byte[blockSize];
                                            Byte[] fileBuffer = new Byte[size + Constants.MaxMessageSize];
                                            int getCount = 0;
                                            int putCount = 0;
                                            do
                                            {
                                                result = await s.ReceiveAsync(new ArraySegment<Byte>(fileBuffer, getCount, size + blockSize - getCount), CancellationToken.None);
                                                //Array.Copy(receiveBuffer, 0, fileBuffer, getCount, result.Count);
                                                getCount += result.Count;

                                                // if we have received all data then round up to the next 64B.  else send up to the current complete 64B
                                                if (result.MessageType == WebSocketMessageType.Binary)
                                                {
                                                    int nextCount = result.EndOfMessage ? ((getCount + blockSize - 1) & ~(blockSize - 1)) : (getCount & ~(blockSize - 1));
                                                    // send data over USB
                                                    while (putCount < nextCount)
                                                    {
                                                        // copies for now.  Would be better to work with array segments
                                                        Array.Copy(fileBuffer, putCount, receiveBuffer, 0, blockSize);
                                                        putCount += blockSize;
                                                        p.SendData(receiveBuffer, blockSize);
                                                    }
                                                }
                                                else
                                                {
                                                    // complete USB operation with garbage data
                                                    int nextCount = ((size + blockSize - 1) & ~(blockSize - 1));
                                                    // send data over USB
                                                    while (putCount < nextCount)
                                                    {
                                                        Array.Copy(fileBuffer, putCount, receiveBuffer, 0, blockSize);
                                                        putCount += blockSize;
                                                        p.SendData(receiveBuffer, blockSize);
                                                    }

                                                    string closeMessage = string.Format("Maximum message size: {0} bytes.", Constants.MaxMessageSize);
                                                    Disconnect(s, WebSocketCloseStatus.MessageTooBig, elem.Port, closeMessage);

                                                    break;
                                                }

                                            } while (!result.EndOfMessage);
                                        }
                                        break;
                                    }
                                case OpcodeType.List:
                                    foreach (var dir in req.Operands)
                                    {
                                        usbint_server_opcode_e opcode = usbint_server_opcode_e.LS;
                                        usbint_server_space_e space = usbint_server_space_e.FILE;
                                        var list = (List<Tuple<int, string>>)p.SendCommand(opcode, space, flags, dir);
                                        ResponseType rsp = new ResponseType();
                                        rsp.Results = new List<string>();
                                        foreach (var item in list)
                                        {
                                            rsp.Results.Add(item.Item1.ToString());
                                            rsp.Results.Add(item.Item2);

                                            if (Encoding.UTF8.GetBytes(serializer.Serialize(rsp)).Count() > Constants.MaxMessageSize)
                                            {
                                                rsp.Results.RemoveRange(rsp.Results.Count - 2, 2);

                                                try
                                                {
                                                    await s.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(rsp))), WebSocketMessageType.Text, false, CancellationToken.None);
                                                }
                                                catch (Exception e)
                                                {
                                                    // Allow socket failures
                                                }

                                                // add back remaining as first set
                                                rsp.Results.Clear();
                                                rsp.Results.Add(item.Item1.ToString());
                                                rsp.Results.Add(item.Item2);
                                            }
                                        }

                                        try
                                        {
                                            // send last
                                            await s.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(rsp))), WebSocketMessageType.Text, true, CancellationToken.None);
                                        }
                                        catch (Exception e)
                                        {
                                            // Allow socket failures
                                        }
                                    }
                                    break;
                                case OpcodeType.GetFile:
                                    {
                                        foreach (var name in req.Operands)
                                        {
                                            usbint_server_opcode_e opcode = usbint_server_opcode_e.GET;
                                            usbint_server_space_e space = usbint_server_space_e.FILE;
                                            int size = (int)p.SendCommand(opcode, space, flags, name);
                                            Byte[] tempData = new Byte[Constants.MaxMessageSize];

                                            int readSize = (size + 511) & ~511;
                                            int readByte = 0;
                                            int writeSize = size;
                                            int writeByte = 0;

                                            while (readByte < readSize)
                                            {
                                                int readOffset = readByte % Constants.MaxMessageSize;
                                                int packetOffset = readByte % 64;
                                                readByte += p.GetData(tempData, readOffset, 64 - packetOffset); // Math.Min(readSize - readByte, Math.Min(Constants.MaxMessageSize - readOffset, 64 - packetOffset)));

                                                // send data
                                                if (readByte == readSize || (readByte - writeByte >= Constants.MaxMessageSize))
                                                {
                                                    int toWriteSize = Math.Min(writeSize - writeByte, Constants.MaxMessageSize);
                                                    try
                                                    {
                                                        if (s.State == WebSocketState.Open)
                                                            s.SendAsync(new ArraySegment<byte>(tempData, 0, toWriteSize), WebSocketMessageType.Binary, readByte == readSize, CancellationToken.None).Wait();
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        // Allow socket failures
                                                    }
                                                    writeByte += toWriteSize;
                                                }
                                            }
                                        }
                                        break;
                                    }
                                case OpcodeType.PutFile:
                                    for (int i = 0; i < req.Operands.Count; i += 2)
                                    {
                                        var name = req.Operands[i + 0];
                                        var sizeStr = req.Operands[i + 1];

                                        usbint_server_space_e space = usbint_server_space_e.FILE;
                                        int size = int.Parse(sizeStr, System.Globalization.NumberStyles.HexNumber);
                                        p.SendCommand(usbint_server_opcode_e.PUT, space, flags, name, (uint)size);

                                        // get data and write to USB
                                        WebSocketReceiveResult result;
                                        int blockSize = 512;
                                        // simplify data receipt by allocating a full size buffer
                                        Byte[] receiveBuffer = new Byte[blockSize];
                                        Byte[] fileBuffer = new Byte[size + Constants.MaxMessageSize];
                                        int getCount = 0;
                                        int putCount = 0;
                                        do
                                        {
                                            result = await s.ReceiveAsync(new ArraySegment<Byte>(fileBuffer, getCount, size + blockSize - getCount), CancellationToken.None);
                                            //Array.Copy(receiveBuffer, 0, fileBuffer, getCount, result.Count);
                                            getCount += result.Count;

                                            // if we have received all data then round up to the next 64B.  else send up to the current complete 64B
                                            if (result.MessageType == WebSocketMessageType.Binary)
                                            {
                                                int nextCount = result.EndOfMessage ? ((getCount + blockSize - 1) & ~(blockSize - 1)) : (getCount & ~(blockSize - 1));
                                                // send data over USB
                                                while (putCount < nextCount)
                                                {
                                                    // copies for now.  Would be better to work with array segments
                                                    Array.Copy(fileBuffer, putCount, receiveBuffer, 0, blockSize);
                                                    putCount += blockSize;
                                                    p.SendData(receiveBuffer, blockSize);
                                                }
                                            }
                                            else
                                            {
                                                // complete USB operation with garbage data
                                                int nextCount = ((size + blockSize - 1) & ~(blockSize - 1));
                                                // send data over USB
                                                while (putCount < nextCount)
                                                {
                                                    Array.Copy(fileBuffer, putCount, receiveBuffer, 0, blockSize);
                                                    putCount += blockSize;
                                                    p.SendData(receiveBuffer, blockSize);
                                                }

                                                string closeMessage = string.Format("Maximum message size: {0} bytes.", Constants.MaxMessageSize);
                                                Disconnect(s, WebSocketCloseStatus.MessageTooBig, elem.Port, closeMessage);

                                                break;
                                            }

                                        } while (!result.EndOfMessage);
                                    }
                                    break;
                                case OpcodeType.Rename:
                                    {
                                        for (int i = 0; i < req.Operands.Count; i += 2)
                                        {
                                            string name = req.Operands[i + 0];
                                            string newName = req.Operands[i + 1];

                                            usbint_server_opcode_e opcode = usbint_server_opcode_e.MV;
                                            usbint_server_space_e space = usbint_server_space_e.FILE;
                                            p.SendCommand(opcode, space, flags, name, newName);
                                        }
                                        break;
                                    }
                                case OpcodeType.MakeDir:
                                case OpcodeType.Remove:
                                    {
                                        foreach (var name in req.Operands)
                                        {
                                            usbint_server_opcode_e opcode = (socketOpcode == OpcodeType.MakeDir) ? usbint_server_opcode_e.MKDIR : usbint_server_opcode_e.RM;
                                            usbint_server_space_e space = usbint_server_space_e.FILE;
                                            p.SendCommand(opcode, space, flags, name);
                                        }
                                        break;
                                    }
                            }
                        }
                        catch (Exception e)
                        {
                            // assume all failures close sockets
                            foreach (var socket in _socketHash)
                            {
                                try
                                {
                                    var task = socket.Value.CloseAsync(WebSocketCloseStatus.InternalServerError, "USB failure: " + e.Message, CancellationToken.None);
                                    // don't wait for the close to succeed.  causes deadlock?
                                }
                                catch (Exception x)
                                {
                                    // ignore closing exceptions
                                }
                                //Disconnect(socket.Value, WebSocketCloseStatus.InternalServerError, elem.Port, "USB failure: " + e.Message);
                            }
                            _socketHash.Clear();

                            // reset USB state by generating new usb2snes core
                            lock (elem.Port)
                            {
                                elem.Port.Count = 0;
                                elem.Port.Port = new core();
                                //elem.Port.Sch = new Scheduler();
                            }

                            // clear the queue - there are probably races here with disconnecting sockets and traffic they are generating.
                            _q.Clear();
                        }

                        // signal command completion if required
                        if (ev != null) ev.Set();
                    }
                }
            }

            /// <summary>
            /// Stop communicates to the main thread to stop executing and return.
            /// </summary>
            public void Stop()
            {
                _stop = true;
                _q.Stop();
                _thr.Abort();
                _thr.Join();
            }

            public CommunicationQueue<QueueElementType> Queue() { return _q; }

            private Thread _thr;
            private CommunicationQueue<QueueElementType> _q = new CommunicationQueue<QueueElementType>();
            private bool _stop = false;

            private SortedDictionary<int, WebSocket> _socketHash = new SortedDictionary<int, WebSocket>();
        }

        /// <summary>
        /// SocketServer handles new socket connections and spawns SocketThreads.
        /// </summary>
        private class SocketServer
        {
            public SocketServer(string p)
            {
                _p = p;
            }

            public void Start()
            {
                _l.Prefixes.Clear();
                _l.Prefixes.Add(_p);
                _l.Start();

                while (true)
                {
                    try
                    {
                        var context = _l.GetContext();
                        if (context.Request.IsWebSocketRequest)
                        {
                            // start new thread for context
                            var s = new SocketThread(context, _sockets, _h);
                            _t.Add(new Thread(s.ProcessRequest));
                            _t.Last().Start();
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                            context.Response.Close();
                        }
                    }
                    catch (Exception e)
                    {
                    }
                }
            }

            /// <summary>
            /// Stop cleans up existing state.
            /// </summary>
            public void Stop()
            {
                // FIXME: this is messy and has some races to cleanup
                foreach (var t in _sockets)
                {
                    WebSocket s = t.Value.Item1;
                    PortAndCount p = t.Value.Item2;
                    Disconnect(s, WebSocketCloseStatus.NormalClosure, p, "");
                }
                foreach (var t in _sockets)
                {
                    PortAndCount p = t.Value.Item2;
                    if (p != null) p.Sch.Stop();
                }

                foreach (var t in _t)
                {
                    // The socket disconnect will cause the thread to exit and we should join back up here
                    t.Join();
                }

                _l.Close();
            }

            /// <summary>
            /// Tracks the socket to port state mapping
            /// </summary>
            private SortedDictionary<int, Tuple<WebSocket, PortAndCount>> _sockets = new SortedDictionary<int, Tuple<WebSocket, PortAndCount>>();

            /// <summary>
            /// Tracks active com to port state mapping
            /// </summary>
            private CommunicationHash<string, PortAndCount> _h = new CommunicationHash<string, PortAndCount>();

            private HttpListener _l = new HttpListener();
            private List<Thread> _t = new List<Thread>();

            private string _p;
        }

        /// <summary>
        /// SocketThread manages the socket connection and inserts new commands into the CommunicationQueue for the Scheduler to execute.
        /// </summary>
        private class SocketThread {
            HttpListenerContext _c;
            private SortedDictionary<int, Tuple<WebSocket, PortAndCount>> _sockets;
            private CommunicationHash<string, PortAndCount> _h;

            public SocketThread(HttpListenerContext c, SortedDictionary<int, Tuple<WebSocket, PortAndCount>> s, CommunicationHash<string, PortAndCount> h)
            {
                _c = c;
                _sockets = s;
                _h = h;
            }

            public void ProcessRequest()
            {
                WebSocketContext wsc = null;

                try
                {
                    var t = _c.AcceptWebSocketAsync(subProtocol: null);
                    t.Wait();
                    wsc = t.Result;
                }
                catch (Exception e)
                {
                    _c.Response.StatusCode = 500;
                    _c.Response.Close();
                }

                byte[] receiveBuffer = new byte[Constants.MaxMessageSize];
                var s = wsc.WebSocket;
                JavaScriptSerializer serializer = new JavaScriptSerializer();

                _sockets[s.GetHashCode()] = Tuple.Create<WebSocket, PortAndCount>(s, null);

                RequestType req = null;
                while (s.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    try
                    {
                        var t = s.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
                        t.Wait();
                        WebSocketReceiveResult result = t.Result; 
                        var port = _sockets[s.GetHashCode()].Item2;

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Disconnect(s, WebSocketCloseStatus.NormalClosure, port, string.Empty);
                        }
                        else if (result.MessageType == WebSocketMessageType.Text)
                        {
                            int count = result.Count;

                            while (result.EndOfMessage == false)
                            {
                                if (count >= Constants.MaxMessageSize)
                                {
                                    string closeMessage = string.Format("Maximum message size: {0} bytes.", Constants.MaxMessageSize);
                                    Disconnect(s, WebSocketCloseStatus.MessageTooBig, port, closeMessage);
                                    return;
                                }

                                t = s.ReceiveAsync(new ArraySegment<Byte>(receiveBuffer, count, Constants.MaxMessageSize - count), CancellationToken.None);
                                t.Wait();
                                result = t.Result;
                                count += result.Count;
                            }

                            var messageString = Encoding.UTF8.GetString(receiveBuffer, 0, count);
                            req = new RequestType();
                            req = serializer.Deserialize<RequestType>(messageString);

                            // event for handling serialization
                            AutoResetEvent ev = null;

                            var opcode = (OpcodeType)Enum.Parse(typeof(OpcodeType), req.Opcode);
                            if (opcode == OpcodeType.DeviceList)
                            {
                                ResponseType rsp = new ResponseType();
                                rsp.Results = new List<string>();
                                var d = core.GetDeviceList();
                                foreach (var c in d) rsp.Results.Add(c.Name);
                                try
                                {
                                    s.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(rsp))), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
                                }
                                catch (Exception e)
                                {
                                    // Allow socket failures
                                }
                            }
                            else if (opcode == OpcodeType.Attach)
                            {

                                // set comport
                                var d = core.GetDeviceList();
                                foreach (var c in d)
                                {
                                    if (c.Name == req.Operands[0])
                                    {
                                        if (!_h.Exists(c.Name))
                                        {
                                            // add reference and connect
                                            var p = new PortAndCount();
                                            p.Count = 0;
                                            p.Port = new core();
                                            p.Sch = new Scheduler();
                                            _h.Add(c.Name, p);
                                        }

                                        port = _h[c.Name];
                                        // associate the port with the socket
                                        _sockets[s.GetHashCode()] = Tuple.Create(s, port);

                                        lock (port)
                                        {
                                            port.Count++;
                                            if (port.Count == 1) port.Port.Connect(c.Name);
                                        }

                                        break;
                                    }
                                }
                            }
                            else
                            {
                                if (req.Serialize()) ev = new AutoResetEvent(false);
                                port.Sch.Queue().Enqueue(new QueueElementType() { Request = req, Event = ev, Port = port, Socket = s });
                            }

                            // stall until signalled by scheduler
                            if (ev != null) ev.WaitOne();
                        }
                        else if (result.MessageType == WebSocketMessageType.Binary)
                        {
                            Disconnect(s, WebSocketCloseStatus.InvalidMessageType, port, "Unexpected binary data");
                        }
                        else
                        {
                            Disconnect(s, WebSocketCloseStatus.InvalidMessageType, port, "Cannot accept message");
                        }
                    }
                    catch (Exception e)
                    {
                        // FIXME: handle exceptions
                    }
                }
            }

        }

        private class PortAndCount
        {
            public int Count { get; set; }
            public usb2snes.core Port { get; set; }
            public Scheduler Sch { get; set; }
        }

        static void Disconnect(WebSocket s, WebSocketCloseStatus status, PortAndCount port, string msg)
        {
            if (port != null)
            {
                lock (port)
                {
                    port.Count--;
                    // don't disconnect because we may still have USB transactions inflight
                    //if (port.Count == 0) port.Port.Disconnect();
                }
            }
        }

        // thread-aware queue class
        private class CommunicationQueue<T>
        {
            private readonly Queue<T> queue = new Queue<T>();
            private bool stop = false;

            public void Enqueue(T item)
            {
                lock (queue)
                {
                    queue.Enqueue(item);
                    if (queue.Count == 1) Monitor.PulseAll(queue);
                }
            }

            public Tuple<bool, T> Dequeue()
            {
                lock (queue)
                {
                    if (queue.Count == 0 && !stop) Monitor.Wait(queue);
                    bool f = false;
                    T t = default(T);
                    if (queue.Count != 0)
                    {
                        f = true;
                        t = queue.Dequeue();
                    }
                    return Tuple.Create(f, t);
                }
            }

            public void Clear()
            {
                lock (queue)
                {
                    queue.Clear();
                }
            }

            public int Count()
            {
                lock (queue)
                {
                    return queue.Count;
                }
            }

            public void Stop()
            {
                lock (queue)
                {
                    Monitor.PulseAll(queue);
                    stop = true;
                }
            }
        }

        // thread-aware hash class
        public class CommunicationHash<K, V>
        {
            private readonly SortedDictionary<K, V> hash = new SortedDictionary<K, V>();

            public void Add(K k, V v)
            {
                lock (hash)
                {
                    hash.Add(k, v);
                }
            }

            public void AddIfNotPresent(K k, V v)
            {

                lock (hash)
                {
                    while (hash.ContainsKey(k)) Monitor.Wait(hash);
                    hash.Add(k, v);
                }
            }

            public void Remove(K k)
            {
                lock (hash)
                {
                    hash.Remove(k);
                    Monitor.PulseAll(hash);
                }
            }

            public bool Exists(K k)
            {
                lock (hash)
                {
                    return hash.ContainsKey(k);
                }
            }

            public int Count()
            {
                lock (hash)
                {
                    return hash.Count;
                }
            }

            public void Stop()
            {
                lock (hash)
                {
                    Monitor.PulseAll(hash);
                }
            }

            public V this[K k] { get { lock (hash) return hash[k]; } set { lock (hash) hash[k] = value; } }
        }

    }

}
