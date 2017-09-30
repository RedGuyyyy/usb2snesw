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
using usb2snes.core;
using usb2snes.utils;
using usb2snesviewer.Properties;
using Be.Windows.Forms;
using System.IO;
using System.Timers;
using System.Threading;

using System.Net;
using System.Net.Sockets;

namespace WindowsFormsApplication1
{
    public partial class usb2snesviewer : Form
    {
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

        public usb2snesviewer()
        {
            InitializeComponent();

            comboBoxRegion.Items.Add("ALL");
            comboBoxRegion.Items.Add("WRAM");
            comboBoxRegion.Items.Add("VRAM");
            comboBoxRegion.Items.Add("APU");
            comboBoxRegion.Items.Add("CGRAM");
            comboBoxRegion.Items.Add("OAM");
            comboBoxRegion.Items.Add("PPUREG");
            comboBoxRegion.Items.Add("CPUREG");
            comboBoxRegion.Items.Add("MISC");
            comboBoxRegion.Items.Add("MSU");
            comboBoxRegion.Items.Add("ROM");
            comboBoxRegion.Items.Add("TEST");

            try
            {
                _provider = new DynamicByteProvider(new byte[0x50000]);
                // ignore event handlers for now since we shouldn't change them.
                // FIXME: insert/remove shouldn't call this event.  This is only supposed to be for user changes (writes)
                _provider.Changed += new EventHandler(UpdateSnesMemory);
                hexBox.ByteProvider = _provider;

                hexBox.Font = new Font("MonoSpace", 8);
            }
            catch (IOException x)
            {
                HandleException(x);
            }

            _timer.AutoReset = false;
            _timer.Elapsed += new ElapsedEventHandler(RefreshSnesMemory);
            //_timer.Interval = 200;
            _timer.Stop();

            comboBoxRegion.SelectedIndex = 0;
        }

        ~usb2snesviewer()
        {
            _timer.Stop();
            Monitor.Enter(_timerLock);
            //_timer.Dispose();
            core.Disconnect();
        }

        private void comboBoxPort_SelectedIndexChanged(object sender, EventArgs e)
        {
            _timer.Stop();
            Monitor.Enter(_timerLock);

            try
            {
                hexBox.Enabled = false;

                if (comboBoxPort.SelectedIndex >= 0)
                {
                    var port = (core.Port)comboBoxPort.SelectedItem;

                    core.Disconnect();
                    core.Connect(port.Name);
                    pictureConnected.Image = Resources.bullet_green;
                    pictureConnected.Refresh();
                    toolStripStatusLabel1.Text = "idle";
                    Setup();
                }
            }
            catch (Exception x)
            {
                HandleException(x);
            }
            finally
            {
                Monitor.Exit(_timerLock);
                _timer.Start();
            }
        }

        private void comboBoxRegion_SelectedIndexChanged(object sender, EventArgs e)
        {
            _timer.Stop();
            Monitor.Enter(_timerLock);

            try
            {
                int oldRegionSize = _regionSize;

                _region = comboBoxRegion.SelectedIndex;
                switch (_region)
                {
                    case 0:  _regionBase = 0xF50000; _regionSize = 0x0050000; break;
                    case 1:  _regionBase = 0xF50000; _regionSize = 0x0020000; break;
                    case 2:  _regionBase = 0xF70000; _regionSize = 0x0010000; break;
                    case 3:  _regionBase = 0xF80000; _regionSize = 0x0010000; break;
                    case 4:  _regionBase = 0xF90000; _regionSize = 0x0000200; break;
                    case 5:  _regionBase = 0xF90200; _regionSize = 0x0000220; break;
                    case 6:  _regionBase = 0xF90500; _regionSize = 0x0000200; break;
                    case 7:  _regionBase = 0xF90700; _regionSize = 0x0000200; break;
                    case 8:  _regionBase = 0xF90420; _regionSize = 0x00000E0; break;
                    case 9:  _regionBase = 0x000000; _regionSize = 0x0004000; break;
                    case 10: _regionBase = 0x000000; _regionSize = 0x1000000; break;
                    case 11: _regionBase = 0x000000; _regionSize = 0x0000100; break;
                    default: _regionBase = 0xF50000; _regionSize = 0x0050000; break;
                }

                if (oldRegionSize < _regionSize)
                {
                    _provider.InsertBytes(oldRegionSize, new byte[_regionSize - oldRegionSize]);
                }
                else if (oldRegionSize > _regionSize)
                {
                    _provider.DeleteBytes(_regionSize, oldRegionSize - _regionSize);
                }
            }
            catch (Exception x)
            {
                HandleException(x);
            }
            finally
            {
                Monitor.Exit(_timerLock);
                _timer.Start();
            }

        }

        private void Setup()
        {
            hexBox.Enabled = true;
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
            //hexBox.Invalidate();
            // update visual

            hexBox.Refresh();
        }

        private void RefreshSnesMemory(object source, ElapsedEventArgs e)
        {
            System.Timers.Timer timer = (System.Timers.Timer)source;
            //Monitor.Enter(_timerLock);

            if (checkBoxAutoUpdate.Checked)
            {
                if (!Monitor.TryEnter(_timerLock))
                    return;

                try
                {
                    // read the snes memory
                    GetDataAndResetHead();
                    _timer.Start();
                }
                catch (Exception x)
                {
                    this.Invoke(new Action(() => { HandleException(x); }));
                }
                finally
                {
                    Monitor.Exit(_timerLock);
                }

                try
                {
                    this.Invoke(new Action(() => { RefreshMemoryView(); }));
                }
                catch (Exception x) { }
            }
            else
            {
                _timer.Start();
            }
        }

        private void UpdateSnesMemory(object source, EventArgs e)
        {
            // send updates
            if (e is HexBoxEventArgs)
            {
                ByteChange b = new ByteChange();
                HexBoxEventArgs he = (HexBoxEventArgs)e;

                b.index = he.index;
                b.value = he.value;
                _queue.Enqueue(b);
            }
        }

        private void HandleException(Exception x)
        {
            //_timer.Enabled = false;
            toolStripStatusLabel1.Text = x.Message.ToString();
            core.Disconnect();
            pictureConnected.Image = Resources.bullet_red;
            //pictureConnected.Refresh();
        }

        private void GetDataAndResetHead()
        {
            if (_queue.Count() != 0)
            {
                byte[] tBuffer = new byte[512];
                int size = (int)core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_GET, core.usbint_server_space_e.USBINT_SERVER_SPACE_SNES, core.usbint_server_flags_e.USBINT_SERVER_FLAGS_NONE, (uint)0xF9EFF4, (uint)0x4);
                int queueSize = 0;
                while (queueSize < size)
                {
                    queueSize += core.GetData(tBuffer, queueSize, 512 - (queueSize % 512));
                }
                sendQHeadPtr = ((tBuffer[0x1] << 8) | (tBuffer[0x0])) & 0x7FC;
                sendQTailPtr = ((tBuffer[0x3] << 8) | (tBuffer[0x2])) & 0x7FC;

                // check if we need to make any updates
                while (_queue.Count() != 0)
                {
                    // exit if we filled up the queue
                    if (((sendQTailPtr + 4) & 0x7FC) == sendQHeadPtr) break;

                    var d = _queue.Dequeue();
                    var b = d.Item2;
                    Array.Clear(tBuffer, 0, tBuffer.Length);
                    long addr = 0x7E0000 + b.index;
                    tBuffer[0] = Convert.ToByte((addr >> 16) & 0xFF);
                    tBuffer[1] = b.value;
                    tBuffer[2] = Convert.ToByte((addr >> 0) & 0xFF);
                    tBuffer[3] = Convert.ToByte((addr >> 8) & 0xFF);
                    core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_PUT, core.usbint_server_space_e.USBINT_SERVER_SPACE_SNES, core.usbint_server_flags_e.USBINT_SERVER_FLAGS_NONE, (uint)(0xF9F800 + sendQTailPtr), (uint)4);
                    core.SendData(tBuffer, 4);

                    sendQTailPtr = (sendQTailPtr + 4) & 0x7FC;
                }

                // advance tail pointer
                Array.Clear(tBuffer, 0, tBuffer.Length);
                // snes is little endian
                tBuffer[0] = Convert.ToByte((sendQTailPtr >> 0) & 0xFF);
                tBuffer[1] = Convert.ToByte((sendQTailPtr >> 8) & 0xFF);

                core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_PUT, core.usbint_server_space_e.USBINT_SERVER_SPACE_SNES, core.usbint_server_flags_e.USBINT_SERVER_FLAGS_NONE, (uint)0xF9EFF6, (uint)0x2);
                core.SendData(tBuffer, 2);
            }

            int fileSize = (int)core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_GET, (_region == 9 ? core.usbint_server_space_e.USBINT_SERVER_SPACE_MSU : core.usbint_server_space_e.USBINT_SERVER_SPACE_SNES), core.usbint_server_flags_e.USBINT_SERVER_FLAGS_NONE, (uint)_regionBase, (uint)_regionSize);
            int curSize = 0;
            while (curSize < fileSize)
            {
                curSize += core.GetData(_memory, curSize, 512 - (curSize % 512));
            }

            for (uint i = 0; i < _regionSize; i++)
            {
                _provider.WriteByteNoEvent(i, _memory[i]);
            }
        }

        /// <summary>
        /// Local representation of memory.
        /// </summary>
        private byte[] _memory = new byte[0x1000000];
        private DynamicByteProvider _provider;

        // memory regions
        private int _region = 0;
        private int _regionBase = 0;
        private int _regionSize = 0x50000;

        // poll for reading SNES
        private System.Timers.Timer _timer = new System.Timers.Timer();
        //private System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();
        private object _timerLock = new object();

        // queue
        private int sendQTailPtr = 0, sendQHeadPtr = 0;

        struct ByteChange { public long index; public byte value; }

        private void buttonExport_Click(object sender, EventArgs e)
        {
            FileStream fs = new FileStream(@"export-" + comboBoxRegion.SelectedItem.ToString() + ".bin", FileMode.Create, FileAccess.Write);
            fs.Write(_memory, 0, _regionSize);
            fs.Close();
        }

        private void textBoxBase_TextChanged(object sender, EventArgs e)
        {
            if (comboBoxRegion.SelectedItem.ToString() == "TEST") {
                try
                {
                    _regionBase = Convert.ToInt32(textBoxBase.Text, 16);
                }
                catch (Exception x) { }

            }
        }

        private void textBoxSize_TextChanged(object sender, EventArgs e)
        {
            if (comboBoxRegion.SelectedItem.ToString() == "TEST")
            {
                _timer.Stop();
                Monitor.Enter(_timerLock);

                var oldRegionSize = _regionSize;

                try
                {
                    _regionSize = Convert.ToInt32(textBoxSize.Text, 16);
                    _regionSize = _regionSize != 0 ? _regionSize : 1;

                    if (oldRegionSize < _regionSize)
                    {
                        _provider.InsertBytes(oldRegionSize, new byte[_regionSize - oldRegionSize]);
                    }
                    else if (oldRegionSize > _regionSize)
                    {
                        _provider.DeleteBytes(_regionSize, oldRegionSize - _regionSize);
                    }

                }
                catch (Exception x)
                {
                    //HandleException(x);
                }
                finally
                {
                    Monitor.Exit(_timerLock);
                    _timer.Start();
                }

            }


        }

        private void pictureConnected_Click(object sender, EventArgs e)
        {
            _timer.Stop();
            Monitor.Enter(_timerLock);
            core.Disconnect();
            pictureConnected.Image = Resources.bullet_red;
            pictureConnected.Refresh();
            Monitor.Exit(_timerLock);
        }

        CommunicationQueue<ByteChange> _queue = new CommunicationQueue<ByteChange>();
    }

}
