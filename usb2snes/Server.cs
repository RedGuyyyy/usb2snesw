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
        CancellationTokenSource _tokenSource = new CancellationTokenSource();

        private class QueueElementType {
            public WebSocket Socket { get; set; }
            public RequestType Request { get; set; }
            public AutoResetEvent Event { get; set; }
            public PortAndCount Port { get; set; }
        }

        // main server
        public Server()
        {
            _h = new SocketServer("http://localhost:8080/", _tokenSource.Token);
            _hT = new Thread(_h.Start);
            _hT.Start();
        }

        public void Stop()
        {
            _tokenSource.Cancel();
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
            private SortedDictionary<int, Tuple<WebSocket, PortAndCount>> _sockets = null;
            private core _p = null;
            private Thread _thr;
            private CommunicationQueue<QueueElementType> _q = new CommunicationQueue<QueueElementType>();
            private bool _stop = false;
            private CancellationToken _token;

            public CommunicationQueue<QueueElementType> Queue() { return _q; }

            public Scheduler(SortedDictionary<int, Tuple<WebSocket, PortAndCount>> sockets, core p, CancellationToken t)
            {
                _token = t;
                _sockets = sockets;
                _p = p;
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
                                        _p.SendCommand(opcode, usbint_server_space_e.FILE, flags, req.Operands[0]);
                                        break;
                                    }
                                case OpcodeType.Menu:
                                case OpcodeType.Reset:
                                    {
                                        usbint_server_opcode_e opcode = (socketOpcode == OpcodeType.Menu) ? usbint_server_opcode_e.MENU_RESET
                                                                      : usbint_server_opcode_e.RESET;
                                        _p.SendCommand(opcode, usbint_server_space_e.FILE, flags);
                                        break;
                                    }
                                case OpcodeType.Info:
                                    {
                                        usbint_server_opcode_e opcode = usbint_server_opcode_e.INFO;
                                        var version = (List<string>)_p.SendCommand(opcode, usbint_server_space_e.SNES, flags);
                                        ResponseType rsp = new ResponseType();
                                        rsp.Results = version;

                                        try
                                        {
                                            await s.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(rsp))), WebSocketMessageType.Text, true, _token);
                                        }
                                        catch (Exception e)
                                        {
                                            // Allow socket failures
                                        }
                                        break;
                                    }
                                case OpcodeType.Stream:
                                    {
                                        usbint_server_opcode_e opcode = usbint_server_opcode_e.STREAM;
                                        _p.SendCommand(opcode, usbint_server_space_e.MSU, flags | usbint_server_flags_e.DATA64B);

                                        // generate data
                                        int readByte = 0;
                                        Byte[] tempData = new Byte[64];

                                        while (true)
                                        {
                                            int readSize = 64;
                                            readByte += _p.GetData(tempData, readByte, 64 - readByte); // Math.Min(readSize - readByte, Math.Min(Constants.MaxMessageSize - readOffset, 64 - packetOffset)));

                                            // send data
                                            if (readByte == readSize)
                                            {
                                                try
                                                {
                                                    _token.ThrowIfCancellationRequested();
                                                    s.SendAsync(new ArraySegment<byte>(tempData, 0, 64), WebSocketMessageType.Binary, true, _token).Wait();
                                                    readByte = 0;
                                                }
                                                catch (Exception e)
                                                {
                                                    _p.Reset();
                                                    try
                                                    {
                                                        while (true)
                                                        {
                                                            _p.GetData(tempData, 0, 64);
                                                        }
                                                    }
                                                    catch (Exception x)
                                                    {
                                                        // collect all data until timeout
                                                        break;
                                                    }
                                                }
                                            }
                                        }

                                        break;
                                    }
                                case OpcodeType.GetAddress:
                                    {
                                        // TODO: decide if we want to pack more optimally
                                        int totalSize = 0;
                                        bool sizeOperand = false;
                                        // vector operands only support up to 8 tuples
                                        Tuple<int, int>[] vOperands = (req.Operands.Count <= 16) ? new Tuple<int, int>[req.Operands.Count / 2] : null;
                                        string nameOperand = "";
                                        int operandNum = 0;
                                        foreach (var operand in req.Operands)
                                        {
                                            if (sizeOperand)
                                            {
                                                int size = int.Parse(operand, System.Globalization.NumberStyles.HexNumber);
                                                int name = int.Parse(nameOperand, System.Globalization.NumberStyles.HexNumber);
                                                totalSize += size;
                                                if (size > 255) vOperands = null;
                                                else if (vOperands != null) vOperands[operandNum++] = Tuple.Create(name, size);
                                            }
                                            else
                                            {
                                                nameOperand = operand;
                                            }
                                            sizeOperand = !sizeOperand;
                                        }

                                        Byte[] data = new Byte[totalSize];
                                        for (int i = 0; i < req.Operands.Count; i += 2)
                                        {
                                            var name = req.Operands[i + 0];
                                            var sizeStr = req.Operands[i + 1];

                                            usbint_server_space_e space = (usbint_server_space_e)Enum.Parse(typeof(usbint_server_space_e), req.Space);
                                            uint address = uint.Parse(name, System.Globalization.NumberStyles.HexNumber);
                                            int size = (vOperands != null) ? totalSize : int.Parse(sizeStr, System.Globalization.NumberStyles.HexNumber);

                                            if (vOperands == null) _p.SendCommand(usbint_server_opcode_e.GET, space, /*usbint_server_flags_e.NORESP*/usbint_server_flags_e.NORESP | usbint_server_flags_e.DATA64B | flags, address, (uint)size);
                                            else
                                            {
                                                i = req.Operands.Count - 2;
                                                _p.SendCommand(usbint_server_opcode_e.VGET, space, /*usbint_server_flags_e.NORESP*/usbint_server_flags_e.NORESP | usbint_server_flags_e.DATA64B | flags, vOperands);
                                            }
                                            Byte[] tempData = new Byte[Constants.MaxMessageSize];

                                            int readSize = (size + 63) & ~63;
                                            int readByte = 0;
                                            int writeSize = size;
                                            int writeByte = 0;

                                            while (readByte < readSize)
                                            {
                                                int readOffset = readByte % Constants.MaxMessageSize;
                                                int packetOffset = readByte % 64;
                                                readByte += _p.GetData(tempData, readOffset, 64 - packetOffset); // Math.Min(readSize - readByte, Math.Min(Constants.MaxMessageSize - readOffset, 64 - packetOffset)));

                                                // send data
                                                if (readByte == readSize || (readByte - writeByte >= Constants.MaxMessageSize))
                                                {
                                                    int toWriteSize = Math.Min(writeSize - writeByte, Constants.MaxMessageSize);
                                                    try
                                                    {
                                                        _token.ThrowIfCancellationRequested();
                                                        s.SendAsync(new ArraySegment<byte>(tempData, 0, toWriteSize), WebSocketMessageType.Binary, readByte == readSize && (i + 2 >= req.Operands.Count), _token).Wait(3000);
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
                                        // TODO: decide if we want to pack more optimally
                                        // vector operands only support up to 8 tuples
                                        int totalSize = 0;
                                        bool sizeOperand = false;
                                        Tuple<int, int>[] vOperands = (req.Operands.Count <= 16) ? new Tuple<int, int>[req.Operands.Count / 2] : null;
                                        string nameOperand = "";
                                        int operandNum = 0;
                                        foreach (var operand in req.Operands)
                                        {
                                            if (sizeOperand)
                                            {
                                                int size = int.Parse(operand, System.Globalization.NumberStyles.HexNumber);
                                                int name = int.Parse(nameOperand, System.Globalization.NumberStyles.HexNumber);
                                                totalSize += size;
                                                if (size > 255) vOperands = null;
                                                else if (vOperands != null) vOperands[operandNum++] = Tuple.Create(name, size);
                                            }
                                            else
                                            {
                                                nameOperand = operand;
                                            }
                                            sizeOperand = !sizeOperand;
                                        }

                                        for (int i = 0; i < req.Operands.Count; i += 2)
                                        {
                                            var name = req.Operands[i + 0];
                                            var sizeStr = req.Operands[i + 1];

                                            usbint_server_space_e space = (usbint_server_space_e)Enum.Parse(typeof(usbint_server_space_e), req.Space);
                                            uint address = uint.Parse(name, System.Globalization.NumberStyles.HexNumber);
                                            int size = (vOperands != null) ? totalSize : int.Parse(sizeStr, System.Globalization.NumberStyles.HexNumber);
                                            if (vOperands == null) _p.SendCommand(usbint_server_opcode_e.PUT, space, usbint_server_flags_e.NORESP | usbint_server_flags_e.DATA64B | flags, address, (uint)size);
                                            else
                                            {
                                                i = req.Operands.Count - 2;
                                                _p.SendCommand(usbint_server_opcode_e.VPUT, space, usbint_server_flags_e.NORESP | usbint_server_flags_e.DATA64B | flags, vOperands);
                                            }

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
                                                _token.ThrowIfCancellationRequested();
                                                result = await s.ReceiveAsync(new ArraySegment<Byte>(fileBuffer, getCount, size + blockSize - getCount), _token);
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
                                                        _p.SendData(receiveBuffer, blockSize);
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
                                                        _p.SendData(receiveBuffer, blockSize);
                                                    }

                                                    string closeMessage = string.Format("Maximum message size: {0} bytes.", Constants.MaxMessageSize);
                                                    Disconnect(s, WebSocketCloseStatus.MessageTooBig, _sockets, closeMessage, _token);

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
                                        var list = (List<Tuple<int, string>>)_p.SendCommand(opcode, space, flags, dir);
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
                                                    _token.ThrowIfCancellationRequested();
                                                    s.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(rsp))), WebSocketMessageType.Text, false, _token).Wait();
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
                                            s.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(rsp))), WebSocketMessageType.Text, true, _token).Wait();
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
                                            int size = (int)_p.SendCommand(opcode, space, flags, name);
                                            Byte[] tempData = new Byte[Constants.MaxMessageSize];

                                            int readSize = (size + 511) & ~511;
                                            int readByte = 0;
                                            int writeSize = size;
                                            int writeByte = 0;

                                            while (readByte < readSize)
                                            {
                                                int readOffset = readByte % Constants.MaxMessageSize;
                                                int packetOffset = readByte % 64;
                                                readByte += _p.GetData(tempData, readOffset, 64 - packetOffset); // Math.Min(readSize - readByte, Math.Min(Constants.MaxMessageSize - readOffset, 64 - packetOffset)));

                                                // send data
                                                if (readByte == readSize || (readByte - writeByte >= Constants.MaxMessageSize))
                                                {
                                                    int toWriteSize = Math.Min(writeSize - writeByte, Constants.MaxMessageSize);
                                                    try
                                                    {
                                                        if (s.State == WebSocketState.Open)
                                                            s.SendAsync(new ArraySegment<byte>(tempData, 0, toWriteSize), WebSocketMessageType.Binary, readByte == readSize, _token).Wait();
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
                                        _p.SendCommand(usbint_server_opcode_e.PUT, space, flags, name, (uint)size);

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
                                            result = await s.ReceiveAsync(new ArraySegment<Byte>(fileBuffer, getCount, size + blockSize - getCount), _token);
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
                                                    _p.SendData(receiveBuffer, blockSize);
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
                                                    _p.SendData(receiveBuffer, blockSize);
                                                }

                                                string closeMessage = string.Format("Maximum message size: {0} bytes.", Constants.MaxMessageSize);
                                                Disconnect(s, WebSocketCloseStatus.MessageTooBig, _sockets, closeMessage, _token);

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
                                            _p.SendCommand(opcode, space, flags, name, newName);
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
                                            _p.SendCommand(opcode, space, flags, name);
                                        }
                                        break;
                                    }
                            }
                        }
                        catch (Exception e)
                        {
                            List<WebSocket> sL = new List<WebSocket>();
                            lock (_sockets)
                            {
                                // assume all failures close sockets
                                foreach (var socket in _sockets)
                                {
                                    if (_p.PortName() == socket.Value.Item2.Port.PortName())
                                    {
                                        sL.Add(socket.Value.Item1);
                                    }
                                }
                            }

                            foreach (var socket in sL) Disconnect(socket, WebSocketCloseStatus.InternalServerError, _sockets, "USB failure: " + e.Message, _token);

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

        }

        /// <summary>
        /// SocketServer handles new socket connections and spawns SocketThreads.
        /// </summary>
        private class SocketServer
        {
            /// <summary>
            /// Tracks the socket to port state mapping
            /// </summary>
            private SortedDictionary<int, Tuple<WebSocket, PortAndCount>> _sockets = new SortedDictionary<int, Tuple<WebSocket, PortAndCount>>();

            private HttpListener _l = new HttpListener();
            private List<Thread> _t = new List<Thread>();

            private string _p;
            private CancellationToken _token;

            public SocketServer(string p, CancellationToken token)
            {
                _p = p;
                _token = token;
                _token.ThrowIfCancellationRequested();
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
                            var s = new SocketThread(context, _sockets, _token);
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
                List<Tuple<WebSocket, PortAndCount>> sL = new List<Tuple<WebSocket, PortAndCount>>();

                // FIXME: this is messy and has some races to cleanup
                foreach (var t in _sockets)
                {
                    WebSocket s = t.Value.Item1;
                    PortAndCount p = t.Value.Item2;
                    sL.Add(Tuple.Create(s, p));
                }

                foreach (var socket in sL)
                {
                    var s = socket.Item1;
                    var p = socket.Item2;
                    Disconnect(s, WebSocketCloseStatus.NormalClosure, _sockets, "", _token);

                    if (p != null) p.Sch.Stop();
                }

                foreach (var t in _t)
                {
                    // The socket disconnect will cause the thread to exit and we should join back up here
                    t.Join();
                }

                _l.Close();
            }

        }

        /// <summary>
        /// SocketThread manages the socket connection and inserts new commands into the CommunicationQueue for the Scheduler to execute.
        /// </summary>
        private class SocketThread {
            HttpListenerContext _c;
            private SortedDictionary<int, Tuple<WebSocket, PortAndCount>> _sockets;
            private CancellationToken _token;

            public SocketThread(HttpListenerContext c, SortedDictionary<int, Tuple<WebSocket, PortAndCount>> s, CancellationToken t)
            {
                _c = c;
                _sockets = s;
                _token = t;
                _token.ThrowIfCancellationRequested();
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

                lock (_sockets)
                {
                    _sockets[s.GetHashCode()] = Tuple.Create<WebSocket, PortAndCount>(s, null);
                }

                RequestType req = null;
                while (s.State == System.Net.WebSockets.WebSocketState.Open)
                {
                    try
                    {
                        _token.ThrowIfCancellationRequested();
                        var t = s.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), _token);
                        t.Wait();
                        WebSocketReceiveResult result = t.Result; 
                        var port = _sockets[s.GetHashCode()].Item2;

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Disconnect(s, WebSocketCloseStatus.NormalClosure, _sockets, string.Empty, _token);
                        }
                        else if (result.MessageType == WebSocketMessageType.Text)
                        {
                            int count = result.Count;

                            while (result.EndOfMessage == false)
                            {
                                if (count >= Constants.MaxMessageSize)
                                {
                                    string closeMessage = string.Format("Maximum message size: {0} bytes.", Constants.MaxMessageSize);
                                    Disconnect(s, WebSocketCloseStatus.MessageTooBig, _sockets, closeMessage, _token);
                                    return;
                                }

                                _token.ThrowIfCancellationRequested();
                                t = s.ReceiveAsync(new ArraySegment<Byte>(receiveBuffer, count, Constants.MaxMessageSize - count), _token);
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
                                    _token.ThrowIfCancellationRequested();
                                    s.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(rsp))), WebSocketMessageType.Text, true, _token).Wait();
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
                                bool found = false;
                                foreach (var c in d)
                                {
                                    if (c.Name == req.Operands[0])
                                    {
                                        lock (_sockets)
                                        {
                                            port = null;
                                            foreach (var kv in _sockets)
                                            {
                                                if (kv.Value.Item2 != null && kv.Value.Item2.Port.PortName() == c.Name)
                                                {
                                                    port = kv.Value.Item2;
                                                    break;
                                                }
                                            }

                                            if (port == null)
                                            {
                                                // add reference and connect
                                                port = new PortAndCount();
                                                port.Port = new core();
                                                port.Port.Connect(c.Name);
                                                port.Sch = new Scheduler(_sockets, port.Port, _token);
                                            }

                                            // associate the port with the socket
                                            _sockets[s.GetHashCode()] = Tuple.Create(s, port);
                                        }
                                        found = true;
                                        break;
                                    }
                                }

                                if (!found)
                                {
                                    Disconnect(s, WebSocketCloseStatus.EndpointUnavailable, _sockets, "Comport not available: " + req.Operands[0], _token);
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
                            Disconnect(s, WebSocketCloseStatus.InvalidMessageType, _sockets, "Unexpected binary data", _token);
                        }
                        else
                        {
                            Disconnect(s, WebSocketCloseStatus.InvalidMessageType, _sockets, "Cannot accept message", _token);
                        }
                    }
                    catch (Exception e)
                    {
                        // FIXME: handle exceptions
                        Disconnect(s, WebSocketCloseStatus.InternalServerError, _sockets, "Exception: " + e.Message, _token);
                    }
                }
            }

        }

        private class PortAndCount
        {
            public int Count { get; set; }
            public core Port { get; set; }
            public Scheduler Sch { get; set; }
            public CancellationToken Token { get; set; }
        }

        static void Disconnect(WebSocket s, WebSocketCloseStatus status, SortedDictionary<int, Tuple<WebSocket, PortAndCount>> _sockets, string msg, CancellationToken token)
        {
            lock (_sockets)
            {
                if (_sockets.ContainsKey(s.GetHashCode()))
                {
                    var pS = _sockets[s.GetHashCode()].Item2;
                    _sockets.Remove(s.GetHashCode());

                    if (pS != null)
                    {
                        bool found = false;
                        foreach (var kv in _sockets)
                        {
                            if (kv.Value.Item2 != null && kv.Value.Item2.Port.PortName() == pS.Port.PortName())
                            {
                                found = true;
                                break;
                            }
                        }
                        // disconnect port on last connection
                        if (!found)
                        {
                            pS.Sch.Stop();
                            pS.Port.Disconnect();
                        }
                    }

                    try
                    {
                        token.ThrowIfCancellationRequested();
                        s.CloseAsync(status, msg, token).Wait();
                    }
                    catch (Exception e)
                    {
                        // cancellation
                    }
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

    }

}
