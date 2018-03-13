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
using usb2snesmemoryviewer.Properties;
using Be.Windows.Forms;
using System.IO;
using System.Timers;
using System.Threading;

using System.Net;
using System.Net.Sockets;

//using WebSocket4Net;
//using System.Net.WebSockets;
using WebSocketSharp;
using System.Web.Script.Serialization;

using usb2snes;

namespace usb2snes
{
    public partial class usb2snesmemoryviewer : Form
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

        public usb2snesmemoryviewer()
        {
            InitializeComponent();

            comboBoxRegion.Items.Add("SD2SNES_RANGE");
            comboBoxRegion.Items.Add("WRAM");
            comboBoxRegion.Items.Add("VRAM");
            comboBoxRegion.Items.Add("APU");
            comboBoxRegion.Items.Add("CGRAM");
            comboBoxRegion.Items.Add("OAM");
            comboBoxRegion.Items.Add("PPUREG");
            comboBoxRegion.Items.Add("CPUREG");
            comboBoxRegion.Items.Add("MISC");
            comboBoxRegion.Items.Add("MSU");
            comboBoxRegion.Items.Add("CMD");

            _waitHandles[0] = _ev;
            _waitHandles[1] = _term;

            try
            {
                _provider = new DynamicByteProvider(new byte[0x50000]);
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
            _timer.Stop();

            comboBoxRegion.SelectedIndex = 0;

#if DEBUG
            buttonGsuDebug.Visible = true;
#endif

        }

        ~usb2snesmemoryviewer()
        {
            Monitor.Enter(_timerLock);
            _timer.Stop();
            _term.Set();
            _ws.Close();
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
                    Connect();
                    RequestType req = new RequestType();
                    req.Opcode = OpcodeType.Attach.ToString();
                    req.Space = "SNES";
                    req.Operands = new List<string>(new string[] { comboBoxPort.SelectedItem.ToString() });
                    _ws.Send(serializer.Serialize(req));

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
                _offset = 0;

                _region = comboBoxRegion.SelectedIndex;
                switch (_region)
                {
                    case 0: try { _regionBase = Convert.ToInt32(textBoxBase.Text, 16); } catch (Exception x) { _regionBase = 0x0; }; try { _regionSize = Convert.ToInt32(textBoxSize.Text, 16); } catch (Exception x) { _regionSize = 0x100; } break;
                    case 1:  _regionBase = 0xF50000; _regionSize = 0x0020000; break;
                    case 2:  _regionBase = 0xF70000; _regionSize = 0x0010000; break;
                    case 3:  _regionBase = 0xF80000; _regionSize = 0x0010000; break;
                    case 4:  _regionBase = 0xF90000; _regionSize = 0x0000200; break;
                    case 5:  _regionBase = 0xF90200; _regionSize = 0x0000220; break;
                    case 6:  _regionBase = 0xF90500; _regionSize = 0x0000200; break;
                    case 7:  _regionBase = 0xF90700; _regionSize = 0x0000200; break;
                    case 8:  _regionBase = 0xF90420; _regionSize = 0x00000E0; break;
                    case 9:  _regionBase = 0x000000; _regionSize = 0x0007800; break;
                    case 10: _regionBase = 0x002A00; _regionSize = 0x0000600; break; // CMD only uses 1.5KB
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

                hexBox.ReadOnly = (_region != 0 && _region != 1 && _region != 2 && _region != 4 && _region != 5 && _region != 10);
            }
            catch (Exception x)
            {
                HandleException(x);
            }
            finally
            {
                Monitor.Exit(_timerLock);
                if (comboBoxPort.SelectedIndex >= 0)
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
            // make sure the timer stops
            Monitor.Enter(_timerLock);
            _timer.Stop();
            Monitor.Exit(_timerLock);

            comboBoxPort.Items.Clear();
            comboBoxPort.ResetText();
            comboBoxPort.SelectedIndex = -1;
            pictureConnected.Image = Resources.bullet_red;

            try
            {
                Connect();

                var req = new RequestType() { Opcode = OpcodeType.DeviceList.ToString(), Space = "SNES" };
                _ws.Send(serializer.Serialize(req));
                if (WaitHandle.WaitAny(_waitHandles, 1000) != 0) return;
                _ev.Reset();

                foreach (var port in _rsp.Results)
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
            System.Timers.Timer timer = (System.Timers.Timer)source;

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
                try
                {
                    Monitor.Enter(_timerLock);

                    HexBoxEventArgs he = (HexBoxEventArgs)e;

                    if (_region == 0)
                    {
                        // RAM
                        var address = _regionBase + he.index;

                        RequestType req = new RequestType() { Opcode = OpcodeType.PutAddress.ToString(), Space = "SNES", Operands = new List<string>(new string[] { address.ToString("X"), 1.ToString("X") }) };
                        _ws.Send(serializer.Serialize(req));
                        _ws.Send(new Byte[] { he.value });

                    }
                    else if (_region == 10)
                    {
                        // RAM
                        var address = 0x2A00 + he.index;

                        RequestType req = new RequestType() { Opcode = OpcodeType.PutAddress.ToString(), Space = "CMD", Operands = new List<string>(new string[] { address.ToString("X"), 1.ToString("X") }) };
                        _ws.Send(serializer.Serialize(req));
                        _ws.Send(new Byte[] { he.value });
                    }
                    else if (_region == 1 || _region == 2 || _region == 4 || _region == 5)
                    {
                        // setup new command
                        List<Byte> cmd = new List<Byte>();

                        // PHP
                        cmd.Add(0x08);
                        // SEP #20
                        cmd.Add(0xE2); cmd.Add(0x20);
                        // PHA
                        cmd.Add(0x48);
                        // XBA
                        cmd.Add(0xEB);
                        // PHA
                        cmd.Add(0x48);

                        if (_region == 1)
                        {
                            // WRAM

                            // LDA.l $7E0000+addr
                            var address = 0x7E0000 + he.index;
                            cmd.Add(0xAF); cmd.Add(Convert.ToByte((address >> 0) & 0xFF)); cmd.Add(Convert.ToByte((address >> 8) & 0xFF)); cmd.Add(Convert.ToByte((address >> 16) & 0xFF));
                        }
                        else if (_region == 2)
                        {
                            // VRAM
                            var address = he.index >> 1;
                            var lowByte = (he.index & 0x1) == 0;

                            // push control
                            cmd.Add(0xAF); cmd.Add(0x2A); cmd.Add(0x05); cmd.Add(0xF9);
                            cmd.Add(0x48);
                            // push address
                            cmd.Add(0xAF); cmd.Add(0x2C); cmd.Add(0x05); cmd.Add(0xF9);
                            cmd.Add(0x48);
                            cmd.Add(0xAF); cmd.Add(0x2D); cmd.Add(0x05); cmd.Add(0xF9);
                            cmd.Add(0x48);
                            // set control
                            cmd.Add(0xA9); cmd.Add(Convert.ToByte(lowByte ? 0x00 : 0x80));
                            cmd.Add(0x8F); cmd.Add(0x15); cmd.Add(0x21); cmd.Add(0x00);
                            // set address
                            cmd.Add(0xA9); cmd.Add(Convert.ToByte((address >> 0) & 0xFF));
                            cmd.Add(0x8F); cmd.Add(0x16); cmd.Add(0x21); cmd.Add(0x00);
                            cmd.Add(0xA9); cmd.Add(Convert.ToByte((address >> 8) & 0xFF));
                            cmd.Add(0x8F); cmd.Add(0x17); cmd.Add(0x21); cmd.Add(0x00);
                            // get data
                            cmd.Add(0xAF); cmd.Add(Convert.ToByte(lowByte ? 0x3A : 0x39)); cmd.Add(0x21); cmd.Add(0x00);
                            cmd.Add(0x48);
                            cmd.Add(0xAF); cmd.Add(Convert.ToByte(lowByte ? 0x39 : 0x3A)); cmd.Add(0x21); cmd.Add(0x00);
                            cmd.Add(0x48);
                            // set address
                            cmd.Add(0xA9); cmd.Add(Convert.ToByte((address >> 0) & 0xFF));
                            cmd.Add(0x8F); cmd.Add(0x16); cmd.Add(0x21); cmd.Add(0x00);
                            cmd.Add(0xA9); cmd.Add(Convert.ToByte((address >> 8) & 0xFF));
                            cmd.Add(0x8F); cmd.Add(0x17); cmd.Add(0x21); cmd.Add(0x00);
                            // set control
                            cmd.Add(0xA9); cmd.Add(Convert.ToByte(lowByte ? 0x80 : 0x00));
                            cmd.Add(0x8F); cmd.Add(0x15); cmd.Add(0x21); cmd.Add(0x00);
                            // pull modified data
                            cmd.Add(0x68);
                        }
                        else if (_region == 4)
                        {
                            // CGRAM
                            var address = he.index >> 1;
                            var lowByte = (he.index & 0x1) == 0;

                            // push address
                            cmd.Add(0xAF); cmd.Add(0x42); cmd.Add(0x05); cmd.Add(0xF9);
                            cmd.Add(0x48);
                            // set address
                            cmd.Add(0xA9); cmd.Add(Convert.ToByte((address >> 0) & 0xFF));
                            cmd.Add(0x8F); cmd.Add(0x21); cmd.Add(0x21); cmd.Add(0x00);
                            // get data
                            cmd.Add(0xAF); cmd.Add(0x3B); cmd.Add(0x21); cmd.Add(0x00);
                            cmd.Add(0xEB);
                            cmd.Add(0xAF); cmd.Add(0x3B); cmd.Add(0x21); cmd.Add(0x00);
                            if (lowByte) cmd.Add(0xEB);
                            cmd.Add(0x48);
                            // set address
                            cmd.Add(0xA9); cmd.Add(Convert.ToByte((address >> 0) & 0xFF));
                            cmd.Add(0x8F); cmd.Add(0x21); cmd.Add(0x21); cmd.Add(0x00);
                            cmd.Add(0x68);
                        }
                        else if (_region == 5)
                        {
                            // OAM
                            var address = he.index >> 1;
                            var lowByte = (he.index & 0x1) == 0;

                            // push address
                            cmd.Add(0xAF); cmd.Add(0x04); cmd.Add(0x05); cmd.Add(0xF9);
                            cmd.Add(0x48);
                            cmd.Add(0xAF); cmd.Add(0x06); cmd.Add(0x05); cmd.Add(0xF9);
                            cmd.Add(0x48);
                            // set address
                            cmd.Add(0xA9); cmd.Add(Convert.ToByte((address >> 0) & 0xFF));
                            cmd.Add(0x8F); cmd.Add(0x02); cmd.Add(0x21); cmd.Add(0x00);
                            cmd.Add(0xA9); cmd.Add(Convert.ToByte((address >> 8) & 0xFF));
                            cmd.Add(0x8F); cmd.Add(0x03); cmd.Add(0x21); cmd.Add(0x00);
                            // get data
                            cmd.Add(0xAF); cmd.Add(0x38); cmd.Add(0x21); cmd.Add(0x00);
                            cmd.Add(0xEB);
                            cmd.Add(0xAF); cmd.Add(0x38); cmd.Add(0x21); cmd.Add(0x00);
                            if (lowByte) cmd.Add(0xEB);
                            cmd.Add(0x48);
                            // set address
                            cmd.Add(0xA9); cmd.Add(Convert.ToByte((address >> 0) & 0xFF));
                            cmd.Add(0x8F); cmd.Add(0x02); cmd.Add(0x21); cmd.Add(0x00);
                            cmd.Add(0xA9); cmd.Add(Convert.ToByte((address >> 8) & 0xFF));
                            cmd.Add(0x8F); cmd.Add(0x03); cmd.Add(0x21); cmd.Add(0x00);
                            cmd.Add(0x68);
                        }

                        // AND #$F0
                        cmd.Add(0x29); cmd.Add(Convert.ToByte(he.cp == 1 ? 0xF0 : 0x0F));
                        // ORA #$DATA
                        cmd.Add(0x09); cmd.Add(Convert.ToByte((he.cp == 1 ? 0x0F : 0xF0) & he.value));

                        if (_region == 1)
                        {
                            var address = 0x7E0000 + he.index;
                            // STA.l $7E0000+addr
                            cmd.Add(0x8F); cmd.Add(Convert.ToByte((address >> 0) & 0xFF)); cmd.Add(Convert.ToByte((address >> 8) & 0xFF)); cmd.Add(Convert.ToByte((address >> 16) & 0xFF));
                        }
                        else if (_region == 2)
                        {
                            var lowByte = (he.index & 0x1) == 0;

                            // write data
                            cmd.Add(0x8F); cmd.Add(Convert.ToByte(lowByte ? 0x18 : 0x19)); cmd.Add(0x21); cmd.Add(0x00);
                            cmd.Add(0x68);
                            cmd.Add(0x8F); cmd.Add(Convert.ToByte(lowByte ? 0x19 : 0x18)); cmd.Add(0x21); cmd.Add(0x00);
                            // restore address
                            cmd.Add(0x68);
                            cmd.Add(0x8F); cmd.Add(0x17); cmd.Add(0x21); cmd.Add(0x00);
                            cmd.Add(0x68);
                            cmd.Add(0x8F); cmd.Add(0x16); cmd.Add(0x21); cmd.Add(0x00);
                            // restore control
                            cmd.Add(0x68);
                            cmd.Add(0x8F); cmd.Add(0x15); cmd.Add(0x21); cmd.Add(0x00);
                        }
                        else if (_region == 4)
                        {
                            var lowByte = (he.index & 0x1) == 0;

                            // write data
                            if (!lowByte) cmd.Add(0xEB);
                            cmd.Add(0x8F); cmd.Add(0x22); cmd.Add(0x21); cmd.Add(0x00);
                            cmd.Add(0xEB);
                            cmd.Add(0x8F); cmd.Add(0x22); cmd.Add(0x21); cmd.Add(0x00);
                            // restore address
                            cmd.Add(0x68);
                            cmd.Add(0x8F); cmd.Add(0x21); cmd.Add(0x21); cmd.Add(0x00);
                        }
                        else if (_region == 5)
                        {
                            var lowByte = (he.index & 0x1) == 0;

                            // write data
                            if (!lowByte) cmd.Add(0xEB);
                            cmd.Add(0x8F); cmd.Add(0x04); cmd.Add(0x21); cmd.Add(0x00);
                            cmd.Add(0xEB);
                            cmd.Add(0x8F); cmd.Add(0x04); cmd.Add(0x21); cmd.Add(0x00);
                            // restore address
                            cmd.Add(0x68);
                            cmd.Add(0x8F); cmd.Add(0x03); cmd.Add(0x21); cmd.Add(0x00);
                            cmd.Add(0x68);
                            cmd.Add(0x8F); cmd.Add(0x02); cmd.Add(0x21); cmd.Add(0x00);
                        }

                        // LDA #$00
                        cmd.Add(0xA9); cmd.Add(0x00);
                        // STA.l $002C00
                        cmd.Add(0x8F); cmd.Add(0x00); cmd.Add(0x2C); cmd.Add(0x00);
                        // PLA
                        cmd.Add(0x68);
                        // XBA
                        cmd.Add(0xEB);
                        // PLA
                        cmd.Add(0x68);
                        // PLP
                        cmd.Add(0x28);
                        // JMP ($FFEA)
                        cmd.Add(0x6C); cmd.Add(0xEA); cmd.Add(0xFF);

                        RequestType req = new RequestType() { Opcode = OpcodeType.PutAddress.ToString(), Space = "CMD", Operands = new List<string>(new string[] { 0x002C00.ToString("X"), cmd.Count.ToString("X"), 0x002C00.ToString("X"), 1.ToString("X") }) };
                        // Perform first byte last
                        cmd.Add(cmd[0]); cmd[0] = 0x0;
                        _ws.Send(serializer.Serialize(req));
                        _ws.Send(cmd.ToArray());

                        // FIXME: check for byte to be 0
                        //var oldRegionSize = _regionSize;
                        //_regionSize = 1;
                        //do
                        //{
                        //    RequestType req2 = new RequestType() { Opcode = OpcodeType.GetAddress.ToString(), Space = "CMD", Operands = new List<string>(new string[] { 0x002C00.ToString("X"), 1.ToString("X") }) };
                        //    _ws.Send(serializer.Serialize(req2));
                        //    if (WaitHandle.WaitAny(_waitHandles) != 0) break;
                        //} while (_memory[0] != 0x0);
                        //_regionSize = oldRegionSize;
                    }
                }
                catch (Exception x)
                {

                }
                finally
                {
                    Monitor.Exit(_timerLock);
                }
            }
        }

        private void HandleException(Exception x)
        {
            toolStripStatusLabel1.Text = x.Message.ToString();
            _ws.Close();
            pictureConnected.Image = Resources.bullet_red;
        }

        private void GetDataAndResetHead()
        {
            if (_ws.ReadyState != WebSocketState.Open) return;

            RequestType req = new RequestType();
            req.Opcode = OpcodeType.GetAddress.ToString();
            req.Space = _region == 10 ? "CMD" : _region == 9 ? "MSU" : "SNES";
            req.Operands = new List<string>(new string[] { _regionBase.ToString("X"), _regionSize.ToString("X") });
            _ws.Send(serializer.Serialize(req));
            if (WaitHandle.WaitAny(_waitHandles, 1000) != 0) return;

            for (uint i = 0; i < _regionSize; i++)
            {
                _provider.WriteByteNoEvent(i, _memory[i]);
            }
            _ev.Reset();
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
        private string _port = "";

        // poll for reading SNES
        private System.Timers.Timer _timer = new System.Timers.Timer();
        private object _timerLock = new object();

        private void buttonExport_Click(object sender, EventArgs e)
        {
            FileStream fs = new FileStream(@"export-" + comboBoxRegion.SelectedItem.ToString() + ".bin", FileMode.Create, FileAccess.Write);
            fs.Write(_memory, 0, _regionSize);
            fs.Close();
        }

        private void textBoxBase_TextChanged(object sender, EventArgs e)
        {
            if (comboBoxRegion.SelectedItem.ToString() == "SD2SNES_RANGE") {
                try
                {
                    _regionBase = Convert.ToInt32(textBoxBase.Text, 16);
                }
                catch (Exception x) { }

            }
        }

        private void textBoxSize_TextChanged(object sender, EventArgs e)
        {
            if (comboBoxRegion.SelectedItem.ToString() == "SD2SNES_RANGE")
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
            Monitor.Enter(_timerLock);
            _timer.Stop();
            _ws.Close();
            //_ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnected", CancellationToken.None).Wait(3000);
            pictureConnected.Image = Resources.bullet_red;
            pictureConnected.Refresh();
            Monitor.Exit(_timerLock);
        }

        private void ws_Opened(object sender, EventArgs e)
        {
            //_ev.Set();
        }

        private void ws_MessageReceived(object sender, MessageEventArgs e)
        {
            if (e.Type == Opcode.Text)
            {
                _rsp = serializer.Deserialize<ResponseType>(e.Data);
                _ev.Set();
            }
            else if (e.Type == Opcode.Binary)
            {
                Array.Copy(e.RawData, 0, _memory, _offset, e.RawData.Length);

                _offset += e.RawData.Length;
                if (_offset >= _regionSize)
                {
                    _offset = 0;
                    _ev.Set();
                }
            }
        }


        private void ws_Error(object sender, EventArgs e)
        {
            //_ev.Set();
        }

        private void ws_Closed(object sender, EventArgs e)
        {
            //_ev.Set();
        }

        private void Connect()
        {
            _offset = 0;
            _ev.Reset();
            if (_ws != null && _ws.ReadyState == WebSocketState.Open)
            {
                _ws.Close();
                //if (WaitHandle.WaitAny(_waitHandles, 1000) != 0) return;
                _ev.Reset();
            }
            _ws = new WebSocket("ws://localhost:8080/");
            _ws.Log.Output = (_, __) => { };
            _ws.OnOpen += ws_Opened;
            _ws.OnMessage += ws_MessageReceived;
            _ws.OnClose += ws_Closed;
            _ws.OnError += ws_Error;
            _ws.WaitTime = TimeSpan.FromSeconds(4);
            _ws.Connect();
            if (_ws.ReadyState != WebSocketState.Open && _ws.ReadyState != WebSocketState.Connecting) throw new Exception("Connection timeout");

            RequestType req = new RequestType() { Opcode = OpcodeType.Name.ToString(), Space = "SNES", Operands = new List<string>(new string[] { "MemoryViewer" }) };
            _ws.Send(serializer.Serialize(req));

            _ev.Reset();
        }

        WebSocket _ws = new WebSocket("ws://localhost:8080/");
        ResponseType _rsp = new ResponseType();
        ManualResetEvent _ev = new ManualResetEvent(false);
        ManualResetEvent _term = new ManualResetEvent(false);
        JavaScriptSerializer serializer = new JavaScriptSerializer();
        int _offset = 0;

        WaitHandle[] _waitHandles = new WaitHandle[2];
        gsudebug gsuForm;
        int debugCnt = 0;

        private void buttonGsuDebug_Click(object sender, EventArgs e)
        {
            // Open dialog
            if (gsuForm == null || gsuForm.IsDisposed) gsuForm = new gsudebug();

            if (!gsuForm.Visible)
            {
                // Set box to MSU
                comboBoxRegion.SelectedIndex = 9;

                // Connect
                buttonRefresh.PerformClick();

                gsuForm.memory = _memory;
                gsuForm.parent = this;
                gsuForm.Show();
            }
        }

        public void GSUStep()
        {
            try
            {
                Monitor.Enter(_timerLock);
                RequestType req;
                byte[] tBuffer = new byte[Constants.MaxMessageSize];

                // 0xMASK_DATA_INDEX (As Address), GROUP (As size)
                foreach (var config in new int[] {
                                               0x000001 | (++debugCnt << 8), // enable trace
                                             })
                {
                    req = new RequestType() { Opcode = OpcodeType.PutAddress.ToString(), Space = "CONFIG", Operands = new List<string>(new string[] { config.ToString("X"), 0x3.ToString("X") }) };
                    _ws.Send(serializer.Serialize(req));
                    // send dummy write
                    _ws.Send(new ArraySegment<byte>(tBuffer, 0, 64).ToArray());
                }
            }
            finally {
                Monitor.Exit(_timerLock);
            }
        }
    }

}
