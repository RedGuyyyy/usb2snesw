using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
//using System.Net.WebSockets;
using System.Net;

using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

using usb2snes;

namespace usb2snes
{
    public class Server
    {
        SocketServer _h;
        Thread _t;

        private class RequestQueueElementType {
            public RequestType Request { get; set; }

            public ClientSocket Socket { get; set; }
        }

        private class ResponseQueueElementType
        {
            public ResponseType Response { get; set; }
            public Byte[] Data { get; set; }

            public bool Done { get; set; }
        }

        // main server
        public Server()
        {
            _h = new SocketServer();
        }

        public void Start()
        {
            // create a separate thread for the socket server so UI calls don't slow it down
            _t = new Thread(_h.Start);
            _t.Start();
        }

        public void Stop()
        {
            _h.Stop();
            _t.Join();
        }

        /// <summary>
        /// SocketServer handles new socket connections and spawns SocketThreads.
        /// </summary>
        private class SocketServer
        {
            /// <summary>
            /// tracks the com port to scheduler mapping
            /// </summary>
            private SortedDictionary<string, Scheduler> _ports = new SortedDictionary<string, Scheduler>();
            AutoResetEvent _ev = new AutoResetEvent(false);

            //private HttpListener _l = new HttpListener();
            //private List<Thread> _t = new List<Thread>();

            public SocketServer()
            {
            }

            public void Start()
            {
                WebSocketServer _wssv = new WebSocketServer("ws://localhost:8080/");
                //WebSocketServer _wssv = new WebSocketServer(System.Net.IPAddress.Loopback, 8080);
                _wssv.AddWebSocketService<ClientSocket>("/", () => new ClientSocket(_ports));
                _wssv.Start();
                _ev.WaitOne();
                _wssv.Stop();
            }

            /// <summary>
            /// Stop cleans up existing state.
            /// </summary>
            public void Stop()
            {
                lock (_ports)
                {
                    foreach (var p in _ports)
                    {
                        lock (p.Value.Clients)
                        {
                            foreach (var c in p.Value.Clients)
                            {
                                c.Value.Context.WebSocket.Close();
                            }
                        }
                        p.Value.Stop();
                    }
                }
                _ev.Set();
            }

        }

        /// <summary>
        /// Socket manages the socket connection and inserts new commands into the CommunicationQueue for the Scheduler to execute.
        /// </summary>
        private class ClientSocket : WebSocketBehavior
        {
            private SortedDictionary<string, Scheduler> _ports;
            private string _portName = "";
            private JavaScriptSerializer serializer = new JavaScriptSerializer();

            public BlockingCollection<ResponseQueueElementType> Queue { get; private set; }
            //public CommunicationQueue<ResponseQueueElementType> Queue { get; private set; }
            //public CommunicationQueue<Byte[]> DataQueue { get; private set; }
            public BlockingCollection<Byte[]> DataQueue { get; private set; }
            //public AutoResetEvent DataEvent { get; private set; }

            public ClientSocket(SortedDictionary<string, Scheduler> ports)
            {
                _ports = ports;
                //Queue = new CommunicationQueue<ResponseQueueElementType>();
                Queue = new BlockingCollection<ResponseQueueElementType>();
                //DataQueue = new CommunicationQueue<Byte[]>();
                DataQueue = new BlockingCollection<Byte[]>();
                //DataEvent = new AutoResetEvent(false);
            }

            protected override void OnOpen()
            {
                base.OnOpen();
            }

            protected override void OnClose(CloseEventArgs e)
            {
                base.OnClose(e);
            }

            protected override void OnError(ErrorEventArgs e)
            {
                base.OnError(e);
            }

            protected override void OnMessage(MessageEventArgs e)
            {
                // parse message
                if (e.Type == Opcode.Text)
                {
                    var messageString = e.Data;
                    var req = new RequestType();
                    req = serializer.Deserialize<RequestType>(messageString);

                    var opcode = (OpcodeType)Enum.Parse(typeof(OpcodeType), req.Opcode);
                    if (opcode == OpcodeType.DeviceList)
                    {
                        ResponseType rsp = new ResponseType();
                        rsp.Results = new List<string>();
                        var d = core.GetDeviceList();
                        foreach (var c in d) rsp.Results.Add(c.Name);

                        Send(serializer.Serialize(rsp));
                        //SendAsync(serializer.Serialize(rsp), a => { });
                    }
                    else if (opcode == OpcodeType.Attach)
                    {
                        // detach from existing comport if it exists
                        lock (_ports)
                        {
                            if (_ports.ContainsKey(_portName))
                                lock (_ports[_portName].Clients)
                                    _ports[_portName].Clients.Remove(GetHashCode());
                        }

                        // setup comport if it doesn't already exist
                        var d = core.GetDeviceList();
                        foreach (var c in d)
                        {
                            if (c.Name == req.Operands[0])
                            {
                                // assign new name
                                _portName = req.Operands[0];

                                // test if new
                                lock (_ports)
                                {
                                    if (!_ports.ContainsKey(_portName))
                                    {
                                        try
                                        {
                                            var s = new Scheduler(_portName, _ports);
                                            _ports.Add(_portName, s);
                                        }
                                        catch (Exception x)
                                        {
                                            // close the socket if there is a failure
                                            Context.WebSocket.Close();
                                            return;
                                        }
                                    }

                                    lock (_ports[_portName].Clients) lock (_ports[_portName].Clients) _ports[_portName].Clients.Add(GetHashCode(), this);
                                }

                                break;
                            }
                        }
                    }
                    else
                    {
                        lock (_ports)
                        {
                            if (!_ports.ContainsKey(_portName))
                            {
                                // if port doesn't exist, close it.
                                Context.WebSocket.Close();
                                return;
                            }
                            var port = _ports[_portName];
                            //port.Queue.Enqueue(new RequestQueueElementType() { Request = req, Socket = this });
                            port.Queue.Add(new RequestQueueElementType() { Request = req, Socket = this });
                        }

                        // if this is a data operation then wait until all data is sent back
                        if (req.RequiresData())
                        {
                            bool done = false;
                            bool first = true;
                            do
                            {
                                //var t = Queue.Dequeue();
                                ResponseQueueElementType t = null;
                                try
                                {
                                    t = Queue.Take();
                                }
                                catch(Exception x)
                                {
                                    done = true;
                                }

                                //done = !t.Item1;
                                if (!done)
                                {
                                    //var elem = t.Item2;
                                    var elem = t;

                                    if (opcode == OpcodeType.List || opcode == OpcodeType.Info || (opcode == OpcodeType.GetFile && first))
                                    {
                                        var rsp = elem.Response;
                                        //SendAsync(serializer.Serialize(rsp), a => { });
                                        Send(serializer.Serialize(rsp));
                                        first = false;
                                    }
                                    else
                                    {
                                        Send((Byte[])elem.Data.Clone());
                                        //SendAsync((Byte[])elem.Data.Clone(), a => { });
                                    }

                                    done = elem.Done;
                                }


                            } while (!done);
                        }

                    }

                }
                else if (e.Type == Opcode.Binary)
                {
                    // FIXME back pressure so the transfer isn't instantaneous.
                    Byte[] data = (Byte[])e.RawData.Clone();
                    DataQueue.Add(data);
                }
                else if (e.Type == Opcode.Close)
                {
                    lock (_ports)
                    {
                        if (_ports.ContainsKey(_portName))
                            lock (_ports[_portName].Clients)
                                _ports[_portName].Clients.Remove(GetHashCode());
                    }
                }
            }
        }

        /// <summary>
        /// Scheduler takes input from the socket threads via the CommunicationQueue and sends
        /// the request to USB via a serial port interface.
        /// </summary>
        private class Scheduler
        {
            private core _p = new core();
            private SortedDictionary<string, Scheduler> _ports;

            private bool _stop = false;
            private Thread _thr;

            //public CommunicationQueue<RequestQueueElementType> Queue { get; private set; }
            public BlockingCollection<RequestQueueElementType> Queue { get; private set; }
            public Dictionary<int, ClientSocket> Clients { get; set; }

            public Scheduler(string name, SortedDictionary<string, Scheduler> ports)
            {
                //Queue = new CommunicationQueue<RequestQueueElementType>();
                Queue = new BlockingCollection<RequestQueueElementType>();
                Clients = new Dictionary<int, ClientSocket>();
                _p.Connect(name);
                _p.Reset();
                Byte[] buffer = new Byte[64];
                // read out any remaining data
                var timeout = _p.serialPort.ReadTimeout;
                try {
                    _p.serialPort.ReadTimeout = 50;
                    _p.serialPort.WriteTimeout = 50;
                    while (true) _p.GetData(buffer, 0, 64);
                } catch (Exception e) { } 
                finally
                {
                    _p.serialPort.ReadTimeout = timeout;
                    _p.serialPort.WriteTimeout = timeout;
                }
                _ports = ports;

                _thr = new Thread(this.Run);
                _thr.Start();
            }

            /// <summary>
            /// Run is the main operation thread.
            /// </summary>
            public void Run()
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();

                while (!_stop)
                {
                    //var t = Queue.Dequeue();
                    RequestQueueElementType t = null;
                    bool done = false;
                    try
                    {
                        t = Queue.Take();
                    }
                    catch (Exception x)
                    {
                        done = true;
                    }

                    if (!done)
                    {
                        //var elem = t.Item2;
                        var elem = t;
                        var req = elem.Request;
                        var socket = elem.Socket;

                        OpcodeType socketOpcode;
                        usbint_server_flags_e flags;

                        try
                        {
                            socketOpcode = (OpcodeType)Enum.Parse(typeof(OpcodeType), req.Opcode);

                            // convert flags
                            flags = usbint_server_flags_e.NONE;
                            if (req.Flags != null) foreach (var flag in req.Flags) flags |= (usbint_server_flags_e)Enum.Parse(typeof(usbint_server_flags_e), flag);
                        }
                        catch (Exception x)
                        {
                            socket.Context.WebSocket.Close(CloseStatusCode.ProtocolError, "Invalid Command Exception: " + x.Message);
                            continue;
                        }

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

                                        //socket.Queue.Enqueue(new ResponseQueueElementType() { Response = rsp, Done = true });
                                        socket.Queue.Add(new ResponseQueueElementType() { Response = rsp, Done = true });
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
                                                //socket.Queue.Enqueue(new ResponseQueueElementType() { Data = new ArraySegment<byte>(tempData, 0, readSize).ToArray(), Done = false });
                                                socket.Queue.Add(new ResponseQueueElementType() { Data = new ArraySegment<byte>(tempData, 0, readSize).ToArray(), Done = false });
                                                readByte = 0;
                                                // TODO: deal with disconnects
                                                //_p.Reset();
                                                //try { while (true) _p.GetData(tempData, 0, 64); } catch (Exception x) { break; }
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

                                            // FIXME: problem with large transfers and 64B???
                                            if (vOperands == null)
                                            {
                                                flags |= usbint_server_flags_e.NORESP | usbint_server_flags_e.DATA64B;
                                                _p.SendCommand(usbint_server_opcode_e.GET, space, flags, address, (uint)size);
                                            }
                                            else
                                            {
                                                flags |= usbint_server_flags_e.NORESP | usbint_server_flags_e.DATA64B;
                                                i = req.Operands.Count - 2;
                                                _p.SendCommand(usbint_server_opcode_e.VGET, space, flags, vOperands);
                                            }
                                            Byte[] tempData = new Byte[Constants.MaxMessageSize];

                                            int blockSize = ((flags & usbint_server_flags_e.DATA64B) == 0) ? 512 : 64;
                                            int readSize = (size + blockSize - 1) & ~(blockSize - 1);
                                            int readByte = 0;
                                            int writeSize = size;
                                            int writeByte = 0;
                                            
                                            while (readByte < readSize)
                                            {
                                                int readOffset = readByte % Constants.MaxMessageSize;
                                                int packetOffset = readByte % blockSize;
                                                int count = 0;
                                                count = _p.GetData(tempData, readOffset, blockSize - packetOffset); // Math.Min(readSize - readByte, Math.Min(Constants.MaxMessageSize - readOffset, blockSize - packetOffset)));
                                                if (count == 0) continue;

                                                readByte += count;

                                                // send data
                                                if ((readByte == readSize || (readByte - writeByte >= Constants.MaxMessageSize)) && (writeByte < writeSize))
                                                {
                                                    // NOTE: this only works when we read out an evently divisible amount into the buffer.
                                                    int toWriteSize = Math.Min(writeSize - writeByte, Constants.MaxMessageSize);
                                                    writeByte += toWriteSize;
                                                    //socket.Queue.Enqueue(new ResponseQueueElementType() { Data = new ArraySegment<byte>(tempData, 0, toWriteSize).ToArray(), Done = (writeByte >= writeSize) });
                                                    socket.Queue.Add(new ResponseQueueElementType() { Data = new ArraySegment<byte>(tempData, 0, toWriteSize).ToArray(), Done = (writeByte >= writeSize) });
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
                                            if (vOperands == null)
                                            {
                                                flags |= usbint_server_flags_e.NORESP | usbint_server_flags_e.DATA64B;
                                                _p.SendCommand(usbint_server_opcode_e.PUT, space, flags, address, (uint)size);
                                            }
                                            else
                                            {
                                                flags |= usbint_server_flags_e.NORESP | usbint_server_flags_e.DATA64B;
                                                i = req.Operands.Count - 2;
                                                _p.SendCommand(usbint_server_opcode_e.VPUT, space, flags, vOperands);
                                            }

                                            // get data and write to USB
                                            int blockSize = ((flags & usbint_server_flags_e.DATA64B) == 0) ? 512 : 64;
                                            // simplify data receipt by allocating a full size buffer
                                            Byte[] receiveBuffer = new Byte[blockSize];
                                            Byte[] fileBuffer = new Byte[size + Constants.MaxMessageSize];
                                            int getCount = 0;
                                            int putCount = 0;
                                            do
                                            {
                                                Byte[] d = null;
                                                bool dataDone = false;
                                                try
                                                {
                                                    d = socket.DataQueue.Take();
                                                }
                                                catch (Exception x)
                                                {
                                                    dataDone = true;
                                                }

                                                if (!dataDone)
                                                {
                                                    //Array.Copy(d.Item2, 0, fileBuffer, getCount, d.Item2.Length);
                                                    //getCount += d.Item2.Length;
                                                    Array.Copy(d, 0, fileBuffer, getCount, d.Length);
                                                    getCount += d.Length;

                                                    // if we have received all data then round up to the next 64B.  else send up to the current complete 64B
                                                    int nextCount = (getCount >= totalSize) ? ((totalSize + blockSize - 1) & ~(blockSize - 1)) : (getCount & ~(blockSize - 1));
                                                    // send data over USB
                                                    while (putCount < nextCount)
                                                    {
                                                        // copies for now.  Would be better to work with array segments
                                                        Array.Copy(fileBuffer, putCount, receiveBuffer, 0, blockSize);
                                                        putCount += blockSize;
                                                        _p.SendData(receiveBuffer, blockSize);
                                                    }
                                                    //socket.DataEvent.Set();
                                                }
                                            } while (putCount < totalSize);
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
                                        }

                                        // send entire list (or last)
                                        //socket.Queue.Enqueue(new ResponseQueueElementType() { Response = rsp, Done = true });
                                        socket.Queue.Add(new ResponseQueueElementType() { Response = rsp, Done = true });
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

                                            // send back response with size
                                            ResponseType rsp = new ResponseType();
                                            rsp.Results = new List<string>();
                                            rsp.Results.Add(size.ToString("X"));
                                            //socket.Queue.Enqueue(new ResponseQueueElementType() { Response = rsp, Done = false });
                                            socket.Queue.Add(new ResponseQueueElementType() { Response = rsp, Done = false });

                                            int blockSize = ((flags & usbint_server_flags_e.DATA64B) == 0) ? 512 : 64;
                                            int readSize = (size + blockSize - 1) & ~(blockSize - 1);
                                            int readByte = 0;
                                            int writeSize = size;
                                            int writeByte = 0;

                                            while (readByte < readSize)
                                            {
                                                int readOffset = readByte % Constants.MaxMessageSize;
                                                int packetOffset = readByte % blockSize;
                                                var count = _p.GetData(tempData, readOffset, blockSize - packetOffset); // Math.Min(readSize - readByte, Math.Min(Constants.MaxMessageSize - readOffset, 64 - packetOffset)));
                                                if (count == 0) continue;
                                                readByte += count;

                                                // send data
                                                if ((readByte == readSize || (readByte - writeByte >= Constants.MaxMessageSize)) && (writeByte < writeSize))
                                                {
                                                    int toWriteSize = Math.Min(writeSize - writeByte, Constants.MaxMessageSize);
                                                    writeByte += toWriteSize;
                                                    //socket.Queue.Enqueue(new ResponseQueueElementType() { Data = new ArraySegment<byte>(tempData, 0, toWriteSize).ToArray(), Done = (writeByte >= writeSize) });
                                                    socket.Queue.Add(new ResponseQueueElementType() { Data = new ArraySegment<byte>(tempData, 0, toWriteSize).ToArray(), Done = (writeByte >= writeSize) });
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
                                        //WebSocketReceiveResult result;
                                        int blockSize = ((flags & usbint_server_flags_e.DATA64B) == 0) ? 512 : 64;
                                        // simplify data receipt by allocating a full size buffer
                                        Byte[] receiveBuffer = new Byte[blockSize];
                                        Byte[] fileBuffer = new Byte[size + Constants.MaxMessageSize];
                                        int getCount = 0;
                                        int putCount = 0;
                                        do
                                        {
                                            //result = await s.ReceiveAsync(new ArraySegment<Byte>(fileBuffer, getCount, size + blockSize - getCount), _token);
                                            //getCount += result.Count;

                                            //var d = socket.DataQueue.Dequeue();
                                            Byte[] d = null;
                                            bool dataDone = false;
                                            try
                                            {
                                                d = socket.DataQueue.Take();
                                            }
                                            catch (Exception x)
                                            {
                                                dataDone = true;
                                            }

                                            if (!dataDone)
                                            {
                                                //Array.Copy(d.Item2, 0, fileBuffer, getCount, d.Item2.Length);
                                                //getCount += d.Item2.Length;
                                                Array.Copy(d, 0, fileBuffer, getCount, d.Length);
                                                getCount += d.Length;

                                                int nextCount = (getCount >= size) ? ((size + blockSize - 1) & ~(blockSize - 1)) : (getCount & ~(blockSize - 1));
                                                // send data over USB
                                                while (putCount < nextCount)
                                                {
                                                    // copies for now.  Would be better to work with array segments
                                                    Array.Copy(fileBuffer, putCount, receiveBuffer, 0, blockSize);
                                                    putCount += blockSize;
                                                    _p.SendData(receiveBuffer, blockSize);
                                                }
                                                //socket.DataEvent.Set();
                                            }
                                        } while (putCount < size);
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
                            // TODO: close all sockets and clear queues
                            lock(_ports)
                            {
                                var p = _ports[_p.PortName()];

                                lock (p.Clients)
                                {
                                    // close all clients
                                    foreach (var c in p.Clients) c.Value.Context.WebSocket.Close();

                                    // clear them out
                                    p.Clients.Clear();
                                }

                                // remove the port so a new one needs to be created
                                _ports.Remove(_p.PortName());
                            }
                            break;
                        }
                    }
                }
            }

            /// <summary>
            /// Stop communicates to the main thread to stop executing and return.
            /// </summary>
            public void Stop()
            {
                _stop = true;
                //Queue.Stop();
                Queue.CompleteAdding();
                _thr.Join();
            }

        }

        // thread-aware queue class
        public class CommunicationQueue<T>
        {
            private readonly Queue<T> queue = new Queue<T>();
            private bool stop = false;

            public void Enqueue(T item)
            {
                lock (queue)
                {
                    //if (_size != 0 && queue.Count >= _size && !stop) Monitor.Wait(queue);
                    queue.Enqueue(item);
                    if (queue.Count >= 1) Monitor.PulseAll(queue);
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
                        //if (_size != 0 && queue.Count < _size) Monitor.PulseAll(queue);
                    }
                    return Tuple.Create(f, t);
                }
            }

            public void Available(int count)
            {
                lock (queue)
                {
                    while (queue.Count >= count && !stop) { Monitor.Wait(queue); }
                }
            }

            public void Clear()
            {
                lock (queue)
                {
                    queue.Clear();
                }
            }

            public int Count_NoLock()
            {
                return queue.Count;
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
                    stop = true;
                    Monitor.PulseAll(queue);
                }
            }
        }

    }

}
