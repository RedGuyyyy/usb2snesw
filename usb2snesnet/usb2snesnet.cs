using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using usb2snes.utils;
using usb2snesnet.Properties;
using Be.Windows.Forms;
using System.IO;
using System.Timers;
using System.Threading;

using System.Net;
using System.Net.Sockets;

namespace usb2snes
{
    public partial class usb2snesnet : Form
    {
        public class Client
        {
            public Client(CommunicationQueue<operation> q, CommunicationQueue<operation> r)
            {
                _q = q;
                _r = r;
                //_b = b;
            }

            ~Client()
            {
                //_sendThread.Abort();
                //_recvThread.Abort();
            }

            public void Run()
            {
                // FIXME: move this all to a connect function
                TcpClient c = new TcpClient();
                try
                {
                    c.Connect("localhost", 8001);
                    c.SendTimeout = 500;
                    c.ReceiveTimeout = 500;

                    var send = new SendThread(_q, c);
                    _sendThread = new Thread(send.Run);
                    _sendThread.Start();
                    while (!_sendThread.IsAlive) { }

                    var recv = new RecvThread(_r, c);
                    _recvThread = new Thread(recv.Run);
                    _recvThread.Start();
                    while (!_recvThread.IsAlive) { }

                    // TODO: is there anything to add here?
                    _event.WaitOne();
                    _event.Reset();

                    send.Stop();
                    recv.Stop();

                    _r.Stop();
                    _q.Stop();
                }
                catch (SocketException e)
                {
                    // connection refused
                }
            }

            public void Stop() { _event.Set(); }

            private class SendThread
            {
                public SendThread(CommunicationQueue<operation> q, TcpClient c)
                {
                    _q = q;
                    _c = c;
                }

                public void Run()
                {

                    Byte[] b = new Byte[16];
                    BinaryWriter w = new BinaryWriter(new MemoryStream(b));

                    while (!_stop)
                    {
                        var t = _q.Dequeue();

                        if (t.Item1)
                        {
                            var op = t.Item2;

                            // convert data to array
                            w.Seek(0, SeekOrigin.Begin);
                            w.Write(IPAddress.HostToNetworkOrder(op.type));
                            w.Write(IPAddress.HostToNetworkOrder(op.release));
                            w.Write(IPAddress.HostToNetworkOrder(op.address));
                            w.Write(op.data);

                            // transmit data
                            _c.GetStream().Write(b, 0, b.Length);
                        }
                    }
                }

                public void Stop() { _stop = true; }

                private CommunicationQueue<operation> _q;
                private TcpClient _c;
                bool _stop = false;
            }

            private class RecvThread
            {
                public RecvThread(CommunicationQueue<operation> q, TcpClient c) {
                    _q = q;
                    _c = c;
                }

                public void Run()
                {
                    Byte[] b = new Byte[16];
                    BinaryReader rdr = new BinaryReader(new MemoryStream(b));
                    BinaryWriter w = new BinaryWriter(new MemoryStream(b));

                    int curSize = 0;
                    while (!_stop)
                    {
                        // wait for message
                        try
                        {
                            curSize += _c.GetStream().Read(b, curSize, b.Length - curSize);

                            if (curSize == b.Length)
                            {
                                // parse packet
                                operation op = new operation();
                                rdr.BaseStream.Seek(0, SeekOrigin.Begin);

                                op.type = IPAddress.NetworkToHostOrder(rdr.ReadInt32());
                                op.release = IPAddress.NetworkToHostOrder(rdr.ReadInt32());
                                op.address = IPAddress.NetworkToHostOrder(rdr.ReadInt32());
                                op.data = rdr.ReadBytes(op.data.Length);

                                _q.Enqueue(op);

                                curSize = 0;
                            }
                        }
                        catch (IOException x)
                        {
                            // stop called
                        }
                        catch (InvalidOperationException x)
                        {
                            // server closed connection
                            break;
                        }
                    }
                }

                public void Stop() { _stop = true; }

                private CommunicationQueue<operation> _q;
                //private DynamicByteProvider _b;
                private TcpClient _c;
                bool _stop = false;
            }

            private Thread _sendThread, _recvThread;
            private CommunicationQueue<operation> _q;
            private CommunicationQueue<operation> _r;
            //private DynamicByteProvider _b;
            ManualResetEvent _event = new ManualResetEvent(false);
        }

        public class Server
        {

            public void Run()
            {
                // FIXME: move this all to a connect function
                List<Thread> clients = new List<Thread>();
                List<RecvThread> recvThreads = new List<RecvThread>();
                IPAddress ip = IPAddress.Any;
                l = new TcpListener(ip, 8001);

                // start server thread
                var server = new SendThread(_sockets, _queue, _hash);
                _server = new Thread(server.Run);
                _server.Start();
                while (!_server.IsAlive) { }

                l.Start();
                while (!_stop)
                {
                    try
                    {
                        Socket s = l.AcceptSocket();
                        s.SendTimeout = 500;
                        s.ReceiveTimeout = 500;
                        _sockets.Add(s);

                        // start a thread for that connection
                        var t = new RecvThread(s, _queue, _hash);
                        recvThreads.Add(t);
                        _threads.Add(new Thread(t.Run));
                        _threads.Last().Start();
                    }
                    catch (SocketException e)
                    {
                        // interrupted
                    }
                }

                server.Stop();
                foreach (var r in recvThreads) r.Stop();
                l.Stop();
            }

            public void Stop() { _stop = true; l.Server.Close(); }

            ~Server()
            {
            }

            private class RecvThread
            {
                public RecvThread(Socket s, CommunicationQueue<operation> q, CommunicationHash<int, int> h)
                {
                    _s = s;
                    _q = q;
                    _h = h;
                }

                public void Run()
                {
                    Byte[] b = new Byte[16];
                    BinaryReader rdr = new BinaryReader(new MemoryStream(b));

                    int curSize = 0;
                    while (!_stop)
                    {
                        // wait for message
                        try
                        {
                            curSize += _s.Receive(b, curSize, b.Length - curSize, SocketFlags.None);

                            if (curSize == b.Length)
                            {
                                // parse packet
                                rdr.BaseStream.Seek(0, SeekOrigin.Begin);
                                operation op = new operation();
                                op.type = IPAddress.NetworkToHostOrder(rdr.ReadInt32());
                                op.release = IPAddress.NetworkToHostOrder(rdr.ReadInt32());
                                op.address = IPAddress.NetworkToHostOrder(rdr.ReadInt32());
                                op.data = rdr.ReadBytes(op.data.Length);

                                if (op.type == 0)
                                {
                                    // enqueue request
                                    _q.Enqueue(op);
                                }
                                else
                                {
                                    // handle response
                                    lock (_h)
                                    {
                                        _h[op.address]--;
                                        // remove after we have received all responses
                                        if (_h[op.address] == 0) _h.Remove(op.address);
                                    }
                                }

                                curSize = 0;
                            }
                        }
                        catch (SocketException x)
                        {
                            // socket exception
                        }
                    }
                }

                public void Stop() { _stop = true; _q.Stop(); _h.Stop(); }

                private Socket _s;
                private CommunicationQueue<operation> _q;
                private CommunicationHash<int, int> _h;
                bool _stop = false;
            }

            private class SendThread
            {
                public SendThread(List<Socket> s, CommunicationQueue<operation> q, CommunicationHash<int, int> h)
                {
                    _s = s;
                    _q = q;
                    _h = h;
                }

                public void Run()
                {
                    Byte[] b = new Byte[16];
                    BinaryWriter w = new BinaryWriter(new MemoryStream(b));

                    while (!_stop)
                    {
                        // monitor queue
                        var t = _q.Dequeue();

                        if (t.Item1)
                        {
                            var op = t.Item2;

                            _h.AddIfNotPresent(op.address, _s.Count);

                            // convert data to array
                            w.Seek(0, SeekOrigin.Begin);
                            w.Write(IPAddress.HostToNetworkOrder(op.type));
                            w.Write(IPAddress.HostToNetworkOrder(op.release));
                            w.Write(IPAddress.HostToNetworkOrder(op.address));
                            w.Write(op.data);

                            // broadcast operation
                            foreach (var s in _s)
                            {
                                s.Send(b);
                            }

                        }
                    }
                }

                public void Stop() { _stop = true; _q.Stop(); _h.Stop(); }

                List<Socket> _s;
                private CommunicationQueue<operation> _q;
                private CommunicationHash<int, int> _h;
                bool _stop = false;
            }

            private CommunicationQueue<operation> _queue = new CommunicationQueue<operation>();
            private CommunicationHash<int, int> _hash = new CommunicationHash<int, int>();
            private List<Socket> _sockets = new List<Socket>();
            private List<Thread> _threads = new List<Thread>();
            private Thread _server;
            TcpListener l;
            bool _stop = false;
        }

        // thread-aware queue class
        public class CommunicationQueue<T>
        {
            private readonly Queue<T> queue = new Queue<T>();

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
                    if (queue.Count == 0) Monitor.Wait(queue);
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

                lock (hash) {
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

        public enum e_usbnet_opcode
        {
            // For atomics/writes bit3=1 is 16b.  CAS is only 8b
            USBNET_OPCODE_WRITE          = 0x0,
            USBNET_OPCODE_ADD            = 0x1,
            USBNET_OPCODE_SUB            = 0x2,
            USBNET_OPCODE_MIN            = 0x3,
            USBNET_OPCODE_MAX            = 0x4,
            USBNET_OPCODE_AND            = 0x5,
            USBNET_OPCODE_EOR            = 0x6,
            USBNET_OPCODE_CAS            = 0x7,
            USBNET_OPCODE_FLUSH          = 0xF,
        }

        public class operation
        {
            public int type = 0;
            public int release = 0;
            public int address = 0;
            public byte[] data = new byte[4];
        }

        public usb2snesnet()
        {
            InitializeComponent();

            try
            {
                _provider = new DynamicByteProvider(new byte[0x2000]);
                // ignore event handlers for now since we shouldn't change them.
                hexBox.ByteProvider = _provider;
            }
            catch (IOException x)
            {
                HandleException(x);
            }

            Application.ApplicationExit += new EventHandler(OnApplicationExit);

            _timer.Elapsed += new ElapsedEventHandler(RefreshSnesMemory);
            _timer.Enabled = false;
            _timer.Interval = 500;
        }

        void OnApplicationExit(object sender, EventArgs e)
        {
            _timer.Enabled = false;
            // FIXME: need graceful exit from threads using flag
            if (_client != null)
            {
                _client.Stop();
                _clientThread.Join();
            }
            if (_server != null)
            {
                _server.Stop();
                _serverThread.Join();
            }
            _port.Disconnect();
        }

        private void comboBoxPort_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                _timer.Enabled = false;

                if (comboBoxPort.SelectedIndex >= 0)
                {
                    var port = (core.Port)comboBoxPort.SelectedItem;

                    _port.Disconnect();
                    _port.Connect(port.Name);
                    pictureConnected.Image = Resources.bullet_green;
                    pictureConnected.Refresh();
                    toolStripStatusLabel1.Text = "idle";
                    Setup();
                    _timer.Enabled = true;
                }
            }
            catch (Exception x)
            {
                HandleException(x);
            }
        }

        private void Setup()
        {
            GetDataAndResetHead();
        }

        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            comboBoxPort.Items.Clear();
            comboBoxPort.ResetText();
            comboBoxPort.SelectedIndex = -1;
            pictureConnected.Image = Resources.bullet_red;
            //pictureConnected.Refresh();

            try
            {
                var deviceList = Win32DeviceMgmt.GetAllCOMPorts();
                foreach (var port in core.GetDeviceList())
                {
                    comboBoxPort.Items.Add(port);
                }

                if (comboBoxPort.Items.Count != 0) comboBoxPort.SelectedIndex = 0;
            }
            catch (Exception x)
            {
                HandleException(x);
            }
        }

        private void RefreshMemoryView()
        {
            hexBox.Refresh();
        }

        private void RefreshSnesMemory(object source, ElapsedEventArgs e)
        {
            try
            {
                byte[] tBuffer = new byte[512];
                // read the snes memory
                _timer.Enabled = false;
                GetDataAndResetHead();

                // queue up any new requests
                while (headPtr != tailPtr)
                {
                    operation op = new operation();
                    op.type = 0;
                    for (uint i = 0; i < op.data.Length; i++)
                    {
                        op.data[i] = _memory[0x1000 + headPtr + i];
                    }
                    op.release = ((op.data[0] & 0xF) == 0xF) ? 1 : 0;
                    op.address = op.data[1]; op.address <<= 4; op.address |= (op.data[0] >> 4);
                    _requestQueue.Enqueue(op);

                    headPtr = (headPtr + 4) & 0x7FC;
                }

                // handle operations
                while (_receiveQueue.Count() != 0)
                {
                    // exit if we filled up the queue
                    if (((sendQTailPtr + 4) & 0x7FC) == sendQHeadPtr) break;

                    var t = _receiveQueue.Dequeue();
                    if (t.Item1)
                    {
                        var op = t.Item2;

                        // NOTE: release shouldn't even get this far
                        if (op.release == 1) throw new Exception("Address");

                        // add to the send queue
                        Array.Clear(tBuffer, 0, tBuffer.Length);
                        for (uint i = 0; i < op.data.Length; i++)
                        {
                            tBuffer[i] = op.data[i];
                        }
                        _port.SendCommand(usbint_server_opcode_e.PUT, usbint_server_space_e.SNES, usbint_server_flags_e.NONE, (uint)(0xF9F800 + sendQTailPtr), (uint)op.data.Length);
                        _port.SendData(tBuffer, op.data.Length);

                        sendQTailPtr = (sendQTailPtr + 4) & 0x7FC;

                        // send response
                        op.type = 1;
                        _requestQueue.Enqueue(op);
                    }

                }

                // advance tail pointer
                Array.Clear(tBuffer, 0, tBuffer.Length);
                // snes is little endian
                tBuffer[0] = Convert.ToByte((sendQTailPtr >> 0) & 0xFF);
                tBuffer[1] = Convert.ToByte((sendQTailPtr >> 8) & 0xFF);

                _port.SendCommand(usbint_server_opcode_e.PUT, usbint_server_space_e.SNES, usbint_server_flags_e.NONE, (uint)0xF9EFF6, (uint)0x2);
                _port.SendData(tBuffer, 2);

                _timer.Enabled = true;
            }
            catch (Exception x)
            {
                HandleException(x);
            }
        }

        private void HandleException(Exception x)
        {
            //_timer.Enabled = false;
            toolStripStatusLabel1.Text = x.Message.ToString();
            _port.Disconnect();
            pictureConnected.Image = Resources.bullet_red;
            //pictureConnected.Refresh();
        }

        private void GetDataAndResetHead()
        {
            int fileSize = (int)_port.SendCommand(usbint_server_opcode_e.GET, usbint_server_space_e.SNES, usbint_server_flags_e.NONE, (uint)0xF9E000, (uint)0x2000);
            int curSize = 0;
            while (curSize < fileSize)
            {
                curSize += _port.GetData(_memory, curSize, 512 - (curSize % 512));
            }

            // set the tail pointer equal to the head
            headPtr = ((_memory[0x0FF1] << 8) | (_memory[0x0FF0])) & 0x7FC;
            tailPtr = ((_memory[0x0FF3] << 8) | (_memory[0x0FF2])) & 0x7FC;
            sendQHeadPtr = ((_memory[0x0FF5] << 8) | (_memory[0x0FF4])) & 0x7FC;
            sendQTailPtr = ((_memory[0x0FF7] << 8) | (_memory[0x0FF6])) & 0x7FC;
            byte[] tBuffer = new byte[512];
            Array.Clear(tBuffer, 0, tBuffer.Length);

            tBuffer[0] = _memory[0x0FF2];
            tBuffer[1] = _memory[0x0FF3];

            _port.SendCommand(usbint_server_opcode_e.PUT, usbint_server_space_e.SNES, usbint_server_flags_e.NONE, (uint)0xF9EFF0, (uint)0x2);
            _port.SendData(tBuffer, 2);

            // update visual
            for (uint i = 0; i < 0x2000; i++)
            {
                _provider.WriteByte(i, _memory[i]);
            }
            this.Invoke(new Action(() => { RefreshMemoryView(); }));
            
        }

        /// <summary>
        /// Local representation of memory.
        /// </summary>
        private byte[] _memory = new byte[0x2000];
        private CommunicationQueue<operation> _requestQueue = new CommunicationQueue<operation>();
        private CommunicationQueue<operation> _receiveQueue = new CommunicationQueue<operation>();
        DynamicByteProvider _provider;

        private int headPtr = 0;
        private int tailPtr = 0;
        private int sendQHeadPtr = 0;
        private int sendQTailPtr = 0;

        // poll for reading SNES
        System.Timers.Timer _timer = new System.Timers.Timer();

        // threads for net communication
        Thread _clientThread, _serverThread;
        Server _server;
        Client _client;

        // usb port
        core _port = new core();


        private void buttonServer_Click(object sender, EventArgs e)
        {
            if (_serverThread != null && _serverThread.IsAlive)
            {
                _server.Stop();
                _serverThread.Join();
                buttonServer.Image = Resources.bullet_red;
            }
            else
            {
                _server = new Server();
                _serverThread = new Thread(new ThreadStart(_server.Run));
                _serverThread.Start();
                while (!_serverThread.IsAlive) { }
                buttonServer.Image = Resources.bullet_green;
            }
        }

        private void buttonClient_Click(object sender, EventArgs e)
        {
            if (_clientThread != null && _clientThread.IsAlive)
            {
                _client.Stop();
                _clientThread.Join();
                buttonClient.Image = Resources.bullet_red;
            }
            else
            {
                _client = new Client(_requestQueue, _receiveQueue);
                _clientThread = new Thread(new ThreadStart(_client.Run));
                _clientThread.Start();
                while (!_clientThread.IsAlive) { }
                buttonClient.Image = Resources.bullet_green;
            }
        }

    }

}
