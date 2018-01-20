using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;
using System.Collections;
using System.IO;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using usb2snes.utils;
using usb2snesfileviewer.Properties;
using usb2snes;

//using WebSocket4Net;
//using System.Net.WebSockets;
using WebSocketSharp;
using System.Web.Script.Serialization;
using System.Threading;

using System.Security;

namespace usb2snes
{
    public partial class usb2snesfileviewer : Form
    {
        //WebSocket _ws = new WebSocket("ws://localhost:8080/");
        //AutoResetEvent _ev = new AutoResetEvent(false);
        //ClientWebSocket _ws = new ClientWebSocket();
        WebSocket _ws = new WebSocket("ws://localhost:8080/");
        ResponseType _rsp = new ResponseType();
        Byte[] _data = null;
        AutoResetEvent _ev = new AutoResetEvent(false);
        AutoResetEvent _evClient = new AutoResetEvent(true);
        ManualResetEvent _term = new ManualResetEvent(false);
        JavaScriptSerializer serializer = new JavaScriptSerializer();
        private usbint_server_flags_e bootFlags = usbint_server_flags_e.NONE;

        WaitHandle[] _clientWaitHandles = new WaitHandle[2];
        WaitHandle[] _socketWaitHandles = new WaitHandle[2];

        public usb2snesfileviewer()
        {
            InitializeComponent();
            //PopulateTreeViewLocal();
            listViewRemote.ListViewItemSorter = new Sorter();
            listViewLocal.ListViewItemSorter = new Sorter();

            _ws.Log.Output = (_, __) => { };

            _clientWaitHandles[0] = _evClient;
            _clientWaitHandles[1] = _term;

            _socketWaitHandles[0] = _ev;
            _socketWaitHandles[1] = _term;

            checkBoxShowExtensions.CheckedChanged -= checkBoxShowExtensions_CheckedChanged;
            checkBoxShowExtensions.Checked = Settings.Default.ShowAllExtensions;
            checkBoxShowExtensions.CheckedChanged += checkBoxShowExtensions_CheckedChanged;
        }

        ~usb2snesfileviewer()
        {
            _term.Set();
        }

        private void GetDirectories(DirectoryInfo[] subDirs, TreeNode root)
        {

            foreach (DirectoryInfo subDir in subDirs)
            {
                TreeNode node = new TreeNode(subDir.Name, 0, 0);
                node.Tag = subDir;
                node.ImageKey = "folder";
                DirectoryInfo[] subSubDirs = subDir.GetDirectories();

                if (subSubDirs.Length != 0) GetDirectories(subSubDirs, node);

                root.Nodes.Add(node);
            }
        }

        private void RefreshListViewRemote(bool extendTimeout = false)
        {
            connected = false;

            if (comboBoxPort.SelectedIndex == -1)
            {
                buttonRefresh.PerformClick();
            }
            else
            {
                try
                {
                    listViewRemote.Clear();

                    RequestType req = new RequestType() { Opcode = OpcodeType.List.ToString(), Space = "SNES", Operands = new List<string>(new string[] { remoteDir }) };
                    _ws.Send(serializer.Serialize(req));
                    WaitSocket(extendTimeout);
                    //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                    //var rsp = GetResponse();

                    for (int i = 0; i < _rsp.Results.Count; i += 2)
                    {
                        int type = int.Parse(_rsp.Results[i + 0]);
                        string name = _rsp.Results[i + 1];
                        ListViewItem item = new ListViewItem(name, type);
                        ListViewItem.ListViewSubItem[] subItems = new ListViewItem.ListViewSubItem[] { new ListViewItem.ListViewSubItem(item, type == 0 ? "Directory" : "File"), new ListViewItem.ListViewSubItem(item, "") };
                        item.SubItems.AddRange(subItems);
                        listViewRemote.Items.Add(item);
                    }

                    connected = true;
                    EnableButtons(true);
                }
                catch (Exception x)
                {
                    HandleException(x);
                }
            }
        }

        private void RefreshListViewLocal()
        {
            try
            {
                listViewLocal.Clear();

                List<String> names = new List<String>(Directory.GetFileSystemEntries(localDir));
                names.Add(localDir + @"\..");

                foreach (string name in names)
                {
                    int type = (File.GetAttributes(name) & FileAttributes.Directory) == FileAttributes.Directory ? 0 : 1;
                    if (  !Settings.Default.ShowAllExtensions 
                          && type == 1
                          && Path.GetExtension(name).ToLower() != ".sfc"
                          && Path.GetExtension(name).ToLower() != ".smc"
                          && Path.GetExtension(name).ToLower() != ".fig"
                          && Path.GetFileName(name).ToLower() != "firmware.img"
                          && (!Path.GetFileName(name).ToLower().StartsWith("fpga_") || Path.GetExtension(name).ToLower() != ".bit")
                          //&& Path.GetExtension(name).ToLower() != ".srm"
                        ) continue;

                    ListViewItem item = new ListViewItem(System.IO.Path.GetFileName(name), type);
                    ListViewItem.ListViewSubItem[] subItems = new ListViewItem.ListViewSubItem[] { new ListViewItem.ListViewSubItem(item, type == 0 ? "Directory" : "File"), new ListViewItem.ListViewSubItem(item, "") };
                    item.SubItems.AddRange(subItems);
                    listViewLocal.Items.Add(item);
                }
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                // go back to where we were
                localDir = localDirPrev;
                RefreshListViewLocal();
            }
        }

        private class Sorter : System.Collections.IComparer
        {
            public System.Windows.Forms.SortOrder Order = SortOrder.Ascending;

            public int Compare(object x, object y) // IComparer Member
            {
                if (!(x is ListViewItem))
                    return (0);
                if (!(y is ListViewItem))
                    return (0);

                ListViewItem l1 = (ListViewItem)x;
                ListViewItem l2 = (ListViewItem)y;

                if (l1.ImageIndex != l2.ImageIndex)
                {
                    if (Order == SortOrder.Ascending) return l1.ImageIndex.CompareTo(l2.ImageIndex);
                    else return l2.ImageIndex.CompareTo(l1.ImageIndex);
                }
                else
                {
                    if (Order == SortOrder.Ascending) return l1.Text.CompareTo(l2.Text);
                    else return l2.Text.CompareTo(l1.Text);
                }
            }
        }

        private void listViewRemote_MouseDoubleClick(object sender, MouseEventArgs e)
        {

            if (e.Button == MouseButtons.Left)
            {
                if (connected)
                {
                    var info = listViewRemote.HitTest(e.X, e.Y);
                    if (info.Item != null)
                    {
                        if (info.Item.ImageIndex == 0 && info.Item.Text != ".")
                        {
                            remoteDirPrev = remoteDir;
                            if (info.Item.Text == "..")
                            {
                                String[] elements = remoteDir.Split('/');
                                elements = elements.Take(elements.Count() - 1).ToArray();
                                remoteDir = String.Join("/", elements);
                            }
                            else
                            {
                                // directory
                                remoteDir += '/' + info.Item.Text;
                            }
                            RefreshListViewRemote();
                            remoteDirNext = remoteDir;
                            backToolStripMenuItem.Enabled = true;
                            forwardToolStripMenuItem.Enabled = false;
                        }
                        else if (info.Item.ImageIndex == 1)
                        {
                            buttonBoot.PerformClick();
                        }
                    }
                }
            }
        }

        private void usb2snes_Load(object sender, EventArgs e)
        {
            localDir = Settings.Default.LocalDir;
            if (localDir == "" || !Directory.Exists(localDir)) localDir = System.IO.Directory.GetCurrentDirectory();
            localDirPrev = localDirNext = localDir;
            RefreshListViewLocal();

            // attempt to autoconnect
            buttonRefresh.PerformClick();
        }

        private void ws_Opened(object sender, EventArgs e)
        {
            SetSocket();
        }

        private void ws_MessageReceived(object sender, MessageEventArgs e)
        {
            if (e.Type == Opcode.Text)
            {
                _rsp = serializer.Deserialize<ResponseType>(e.Data);
                SetSocket();
            }
            else if (e.Type == Opcode.Binary)
            {
                WaitClient();
                _data = e.RawData;
                SetSocket();
            }
        }

        private void ws_Closed(object sender, EventArgs e)
        {
            _ev.Set();
            _evClient.Set();
        }

        /// <summary>
        /// buttonRefresh finds all sd2snes COM ports and attempts to connect to one of them.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            comboBoxPort.Items.Clear();
            comboBoxPort.ResetText();
            comboBoxPort.SelectedIndex = -1;

            connected = false;
            EnableButtons(false);

            try
            {
                _ev.Reset();
                _evClient.Set();
                // reconnect if unconnected or in bad state
                if (_ws != null && _ws.ReadyState == WebSocketState.Open)
                {
                    //_ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "close", CancellationToken.None);
                    //_ws = new ClientWebSocket();
                    _ws.Close();
                    WaitSocket();
                }
                //if (!_ws.ConnectAsync(new Uri("ws://localhost:8080/"), CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                _ws = new WebSocket("ws://localhost:8080/");
                _ws.Log.Output = (_, __) => { };

                _ws.OnOpen += ws_Opened;
                _ws.OnMessage += ws_MessageReceived;
                _ws.OnClose += ws_Closed;

                _ws.WaitTime = TimeSpan.FromSeconds(4);
                _ws.Connect();
                if (_ws.ReadyState != WebSocketState.Open && _ws.ReadyState != WebSocketState.Connecting) throw new Exception("Connection timeout");
                WaitSocket();

                RequestType req = new RequestType() { Opcode = OpcodeType.Name.ToString(), Space = "SNES", Operands = new List<string>(new string[] { "FileViewer" }) };
                _ws.Send(serializer.Serialize(req));
                req = new RequestType() { Opcode = OpcodeType.DeviceList.ToString(), Space = "SNES" };
                _ws.Send(serializer.Serialize(req));
                WaitSocket();

                // get device list
                foreach (var port in _rsp.Results)
                    comboBoxPort.Items.Add(port);

                if (comboBoxPort.Items.Count != 0)
                {
                    comboBoxPort.SelectedIndex = -1;
                    comboBoxPort.SelectedIndex = 0;
                }

                UpdateInfo();
            }
            catch (Exception x)
            {
                HandleException(x);
            }
        }

        private void comboBoxPort_SelectedIndexChanged(object sender, EventArgs e)
        {
            EnableButtons(false);
            connected = false;

            if (comboBoxPort.SelectedIndex >= 0)
            {
                RequestType req = new RequestType() { Opcode = OpcodeType.Attach.ToString(), Space = "SNES", Operands = new List<string>(new string[] { comboBoxPort.SelectedItem.ToString() }) };
                //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                _ws.Send(serializer.Serialize(req));

                UpdateInfo();

                req = new RequestType() { Opcode = OpcodeType.AppVersion.ToString(), Space = "SNES" };
                _ws.Send(serializer.Serialize(req));
                WaitSocket();
                labelWebSocketVersion.Text = "WebSocket Version: " + _rsp.Results[0];

                remoteDirPrev = "";
                remoteDir = "";
                remoteDirNext = "";
                toolStripStatusLabel1.Text = "idle";
                RefreshListViewRemote();
            }
        }

        /// <summary>
        /// buttonUpload sends a file to the snes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonUpload_Click(object sender, EventArgs e)
        {
            try
            {
                if (connected)
                {
                    if (listViewLocal.SelectedItems.Count > 0)
                    {
                        foreach (ListViewItem item in listViewLocal.SelectedItems)
                        {
                            if (item.ImageIndex == 0) continue;

                            string fileName = localDir + @"\" + item.Text;
                            string safeFileName = item.Text;

                            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);

                            RequestType req = new RequestType() { Opcode = OpcodeType.PutFile.ToString(), Space = "SNES", Operands = new List<string>(new string[] { remoteDir + '/' + safeFileName, fs.Length.ToString("X") }) };
                            //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                            _ws.Send(serializer.Serialize(req));

                            // write data
                            int blockSize = Constants.MaxMessageSize;
                            byte[] tBuffer = new byte[blockSize];
                            Array.Clear(tBuffer, 0, tBuffer.Length);
                            int curSize = 0;
                            toolStripProgressBar1.Value = 0;
                            toolStripProgressBar1.Enabled = true;
                            toolStripStatusLabel1.Text = "uploading: " + safeFileName;
                            while (curSize < fs.Length)
                            {
                                int bytesToWrite = fs.Read(tBuffer, 0, blockSize);

                                curSize += bytesToWrite;
                                //if (!_ws.SendAsync(new ArraySegment<byte>(tBuffer, 0, bytesToWrite), WebSocketMessageType.Binary, curSize == fs.Length, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                                _ws.Send(new ArraySegment<byte>(tBuffer, 0, bytesToWrite).ToArray());
                                toolStripProgressBar1.Value = 100 * curSize / (int)fs.Length;
                            }
                            toolStripStatusLabel1.Text = "idle";
                            toolStripProgressBar1.Enabled = false;

                            fs.Close();
                        }

                        RefreshListViewRemote(true);
                    }
                }
            }
            catch (Exception x)
            {
                HandleException(x);
            }
        }

        /// <summary>
        /// buttonDownload gets a file from the snes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonDownload_Click(object sender, EventArgs e)
        {
            try
            {
                if (connected)
                {
                    //_port.Connect(((core.Port)comboBoxPort.SelectedItem).Name);

                    foreach (ListViewItem item in listViewRemote.SelectedItems)
                    {
                        if (item.ImageIndex == 1)
                        {
                            string name = remoteDir + '/' + item.Text;
                            if (name.Length < 256)
                            {
                                FileStream fs = new FileStream(localDir + @"\" + item.Text, FileMode.Create, FileAccess.Write);

                                RequestType req = new RequestType() { Opcode = OpcodeType.GetFile.ToString(), Space = "SNES", Operands = new List<string>(new string[] { name }) };
                                //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                                _ws.Send(serializer.Serialize(req));

                                WaitSocket();
                                // get size from response
                                int fileSize = int.Parse(_rsp.Results[0], System.Globalization.NumberStyles.HexNumber);

                                // read data
                                toolStripProgressBar1.Value = 0;
                                toolStripProgressBar1.Enabled = true;
                                toolStripStatusLabel1.Text = "downloading: " + name;

                                int curSize = 0;
                                do
                                {
                                    WaitSocket();
                                    fs.Write(_data, 0, _data.Length);
                                    SetClient();
                                    curSize += _data.Length;
                                    toolStripProgressBar1.Value = 100 * curSize / fileSize;
                                } while (curSize < fileSize);
                                toolStripProgressBar1.Value = 100;
                                toolStripStatusLabel1.Text = "idle";
                                toolStripProgressBar1.Enabled = false;

                                fs.Close();
                            }
                        }
                    }

                    //_port.Disconnect();

                    RefreshListViewRemote();
                    RefreshListViewLocal();
                }
            }
            catch (Exception x)
            {
                HandleException(x);
            }
        }

        /// <summary>
        /// buttonBoot executes a rom on the snes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonBoot_Click(object sender, EventArgs e)
        {
            try
            {
                if (connected)
                {
                    //_port.Connect(((core.Port)comboBoxPort.SelectedItem).Name);

                    foreach (ListViewItem item in listViewRemote.SelectedItems)
                    {
                        var ext = Path.GetExtension(item.Text);
                        if (item.ImageIndex == 1 && (ext.ToLower().Contains("sfc") | ext.ToLower().Contains("smc") | ext.ToLower().Contains("fig")))
                        {
                            string name = remoteDir + '/' + item.Text;
                            if (name.Length < 256)
                            {
                                RequestType req = new RequestType() { Opcode = OpcodeType.Boot.ToString(), Space = "SNES", Operands = new List<string>(new string[] { name }), Flags = new List<string>(new string[] { bootFlags.ToString() }) };
                                //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                                _ws.Send(serializer.Serialize(req));
                                break; // only boot the first file
                            }
                        }
                    }

                    UpdateInfo();
                }
            }
            catch (Exception x)
            {
                HandleException(x);
            }
        }

        /// <summary>
        /// buttonMkdir creates a directory on the sd2snes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonMkdir_Click(object sender, EventArgs e)
        {
            try
            {
                if (connected)
                {
                    String dirName = Microsoft.VisualBasic.Interaction.InputBox("Remote Directory Name", "MkDir", "");
                    if (dirName != "")
                    {
                        string name = remoteDir + '/' + dirName;
                        if (name.Length < 256)
                        {
                            RequestType req = new RequestType() { Opcode = OpcodeType.MakeDir.ToString(), Space = "SNES", Operands = new List<string>(new string[] { name }) };
                            //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                            _ws.Send(serializer.Serialize(req));
                            RefreshListViewRemote();
                        }
                    }
                }
            }
            catch (Exception x)
            {
                HandleException(x);
            }
        }

        /// <summary>
        /// buttonRename moves a file/directory on the sd2snes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonRename_Click(object sender, EventArgs e)
        {
            try
            {
                if (connected)
                {
                    foreach (ListViewItem item in listViewRemote.SelectedItems)
                    {
                        string name = remoteDir + '/' + item.Text;
                        String newName = Microsoft.VisualBasic.Interaction.InputBox("New Name", "Rename", item.Text);

                        if (newName != "" && name.Length < 256 && newName.Length < 256 - 8)
                        {
                            RequestType req = new RequestType() { Opcode = OpcodeType.Rename.ToString(), Space = "SNES", Operands = new List<string>(new string[] { name, newName }) };
                            //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                            _ws.Send(serializer.Serialize(req));
                        }
                    }

                    RefreshListViewRemote();
                }
            }
            catch (Exception x)
            {
                HandleException(x);
            }
        }

        /// <summary>
        /// buttonDelete removes a file from tne sd2snes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonDelete_Click(object sender, EventArgs e)
        {
            try
            {
                if (connected)
                {
                    if (listViewRemote.SelectedItems.Count > 0)
                    {
                        DialogResult res = MessageBox.Show("OK to Delete: '" + listViewRemote.SelectedItems[0].Text + (listViewRemote.SelectedItems.Count > 1 ? "' and others" : "'") + "?", "Delete Message", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);

                        if (res == DialogResult.Yes)
                        {
                            foreach (ListViewItem item in listViewRemote.SelectedItems)
                            {
                                string name = remoteDir + '/' + item.Text;
                                if (name.Length < 256)
                                {
                                    RequestType req = new RequestType() { Opcode = OpcodeType.Remove.ToString(), Space = "SNES", Operands = new List<string>(new string[] { name }) };
                                    //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                                    _ws.Send(serializer.Serialize(req));
                                }
                            }

                            RefreshListViewRemote();
                        }
                    }
                }
            }
            catch (Exception x)
            {
                HandleException(x);
            }

        }

        /// <summary>
        /// buttonPatch sends a patch update file to the sd2snes RAM
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonPatch_Click(object sender, EventArgs e)
        {
            try
            {
                if (connected)
                {
                    openFileDialog1.Title = "RAM IPS file to load";
                    openFileDialog1.Filter = "IPS File|*.ips"
                                           + "|All Files|*.*";
                    openFileDialog1.FileName = "";
                    openFileDialog1.InitialDirectory = Settings.Default.PatchDir;

                    if (openFileDialog1.ShowDialog() != DialogResult.Cancel)
                    {
                        Settings.Default.PatchDir = Path.GetDirectoryName(openFileDialog1.FileName);
                        for (int i = 0; i < openFileDialog1.FileNames.Length; i++)
                        {
                            string fileName = openFileDialog1.FileNames[i];
                            string safeFileName = openFileDialog1.SafeFileNames[i];

                            sendPatch(fileName, safeFileName);
                        }
                    }
                }
            }
            catch (Exception x)
            {
                HandleException(x);
            }
        }

        /// <summary>
        /// buttonGetState receives the $F00000 save state region
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonGetState_Click(object sender, EventArgs e)
        {
            try
            {
                if (connected)
                {
                    saveFileDialog1.Title = "State file to Save";
                    saveFileDialog1.Filter = "STATE File|*.ss0"
                                           + "|All Files|*.*";
                    saveFileDialog1.InitialDirectory = Settings.Default.GetStateDir;
                    saveFileDialog1.FileName = "save.ss0";

                    if (saveFileDialog1.ShowDialog() != DialogResult.Cancel)
                    {
                        Settings.Default.GetStateDir = Path.GetDirectoryName(saveFileDialog1.FileName);
                        FileStream fs = new FileStream(saveFileDialog1.FileName, FileMode.Create, FileAccess.Write);

                        int fileSize = 0x50000;
                        RequestType req = new RequestType() { Opcode = OpcodeType.GetAddress.ToString(), Space = "SNES", Operands = new List<string>(new string[] { 0xF00000.ToString("X"), fileSize.ToString("X") }) };
                        //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                        _ws.Send(serializer.Serialize(req));
                        //WaitSocket();

                        // read data
                        byte[] tBuffer = new byte[512];
                        Array.Clear(tBuffer, 0, tBuffer.Length);
                        toolStripProgressBar1.Value = 0;
                        toolStripProgressBar1.Enabled = true;
                        toolStripStatusLabel1.Text = "downloading: " + saveFileDialog1.FileName;

                        int curSize = 0;
                        do
                        {
                            WaitSocket();
                            fs.Write(_data, 0, _data.Length);
                            SetClient();
                            curSize += _data.Length;
                            toolStripProgressBar1.Value = 100 * curSize / fileSize;
                        } while (curSize < fileSize);

                        toolStripProgressBar1.Value = 100;
                        toolStripStatusLabel1.Text = "idle";
                        toolStripProgressBar1.Enabled = false;

                        fs.Close();

                        RefreshListViewRemote();
                        RefreshListViewLocal();
                    }
                }
            }
            catch (Exception x)
            {
                HandleException(x);
            }
        }

        /// <summary>
        /// buttonSetState sends the $F00000 save state region
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSetState_Click(object sender, EventArgs e)
        {
            try
            {
                if (connected)
                {
                    openFileDialog1.Title = "State file to Load";
                    openFileDialog1.Filter = "STATE File|*.ss0"
                                           + "|All Files|*.*";
                    openFileDialog1.FileName = "save.ss0";
                    openFileDialog1.InitialDirectory = Settings.Default.SetStateDir;

                    if (openFileDialog1.ShowDialog() != DialogResult.Cancel)
                    {
                        Settings.Default.SetStateDir = Path.GetDirectoryName(openFileDialog1.FileName);
                        for (int i = 0; i < openFileDialog1.FileNames.Length; i++)
                        {
                            string fileName = openFileDialog1.FileNames[i];
                            string safeFileName = openFileDialog1.SafeFileNames[i];

                            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                            // include load request
                            RequestType req = new RequestType() { Opcode = OpcodeType.PutAddress.ToString(), Space = "SNES", Operands = new List<string>(new string[] { 0xF00000.ToString("X"), fs.Length.ToString("X"), 0xFC2001.ToString("X"), 1.ToString("X") }) };
                            //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                            _ws.Send(serializer.Serialize(req));

                            // write data
                            byte[] tBuffer = new byte[512];
                            int curSize = 0;
                            Array.Clear(tBuffer, 0, tBuffer.Length);
                            toolStripProgressBar1.Value = 0;
                            toolStripProgressBar1.Enabled = true;
                            toolStripStatusLabel1.Text = "uploading: " + safeFileName;
                            while (curSize < fs.Length)
                            {
                                int bytesToWrite = fs.Read(tBuffer, 0, 512);
                                curSize += bytesToWrite;
                                //if (!_ws.SendAsync(new ArraySegment<byte>(tBuffer, 0, bytesToWrite), WebSocketMessageType.Binary, curSize >= fs.Length, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                                _ws.Send(new ArraySegment<byte>(tBuffer, 0, bytesToWrite).ToArray());
                                toolStripProgressBar1.Value = 100 * curSize / (int)fs.Length;
                            }
                            _ws.Send(new Byte[1] { 1 });
                            toolStripStatusLabel1.Text = "idle";
                            toolStripProgressBar1.Enabled = false;

                            fs.Close();
                        }

                        RefreshListViewRemote();
                    }
                }
            }
            catch (Exception x)
            {
                HandleException(x);
            }

        }

        /// <summary>
        /// buttonTest tests some new feature
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonTest_Click(object sender, EventArgs e)
        {
            try
            {
                if (connected)
                {
                    if (false)
                    {
                        for (int i = 0; i < 100000; i++)
                        {
                            UpdateInfo();
                            Thread.Sleep(200);
                        }

                    }
                    else if (true)
                    {
                        var start = System.DateTime.Now;

                        //var sl = new string[] { 0.ToString("X"), 255.ToString("X"), 0.ToString("X"), 255.ToString("X"), 0.ToString("X"), 255.ToString("X"), 0.ToString("X"), 255.ToString("X"), 0.ToString("X"), 255.ToString("X"), 0.ToString("X"), 255.ToString("X"), 0.ToString("X"), 255.ToString("X"), 0.ToString("X"), 255.ToString("X"), };
                        var sl = new string[] { 0.ToString("X"), 2048.ToString("X"), };
                        int index = 0;
                        int fileSize = 0;
                        foreach (var s in sl)
                        {
                            if ((index++ & 0x1) == 1) fileSize += Convert.ToInt32(s, 16);
                        }
                        var req = new RequestType() { Opcode = OpcodeType.GetAddress.ToString(), Space = "SNES", Operands = new List<string>(sl) };

                        _ws.Send(serializer.Serialize(req));
                        SetClient();

                        int curSize = 0;
                        while (curSize < fileSize)
                        {
                            WaitSocket();
                            curSize += _data.Length;
                            SetClient();
                        }
                        var time = DateTime.Now.Subtract(start);
                        int i = 0;
                    }
                    else if (true)
                    {
                        // SM 
                        RequestType req;
                        string msg;
                        byte[] tBuffer = new byte[Constants.MaxMessageSize];
                        bool streamEnd = false;

                        // connect to OBS
                        WebSocket ws = new WebSocket("ws://localhost:4444/");
                        ws.Log.Output = (_, __) => { };
                        ws.OnMessage += (object s, MessageEventArgs m) =>
                        {
                            if (m.Type == Opcode.Text)
                            {
                                var rsp = m.Data;
                            }
                        };

                        ws.WaitTime = TimeSpan.FromSeconds(4);
                        ws.Connect();
                        if (ws.ReadyState != WebSocketState.Open && ws.ReadyState != WebSocketState.Connecting) throw new Exception("Connection timeout");

                        // send draw call
                        msg = "{\"request-type\":\"SetShader\", \"message-id\":\"0\", \"shader-opcode\":\"drawcall\", \"num\":0, \"num_vert\":4}";
                        ws.Send(msg);

                        // configure usb2snes to collect position and animation data
                        // 0xMASK_DATA_INDEX (As Address), GROUP (As size)
                        foreach (var config in new int[] { (0x4 << 13) | (0x7 << 8) | 0x01, (0x1F << 8) | 0x00, // 0x80-0x83 - $71F top/bottom
                                                           (0x2 << 13) | (0x9 << 8) | 0x03, (0x11 << 8) | 0x02, // 0x90-0x91 - $911 Xscreen
                                                           (0x2 << 13) | (0x9 << 8) | 0x05, (0x15 << 8) | 0x04, // 0xA0-0xA1 - $915 Yscreen
                                                           (0x2 << 13) | (0xA << 8) | 0x07, (0xF6 << 8) | 0x06, // 0xB0-0xB1 - $AF6 Xpos
                                                           (0x2 << 13) | (0xA << 8) | 0x09, (0xFA << 8) | 0x08, // 0xC0-0xC1 - $AFA Ypos
                                                           0x000210, // enable capture
                                                         })
                        {
                            req = new RequestType() { Opcode = OpcodeType.PutAddress.ToString(), Space = "CONFIG", Operands = new List<string>(new string[] { config.ToString("X"), 0x02.ToString("X") }) };
                            _ws.Send(serializer.Serialize(req));
                            // send dummy write
                            _ws.Send(new ArraySegment<byte>(tBuffer, 0, 64).ToArray());
                        }

                        // Read out all data to sync up
                        req = new RequestType() { Opcode = OpcodeType.Stream.ToString(), Space = "MSU", Flags = new List<string>(new string[] { usbint_server_flags_e.STREAM_BURST.ToString() }), Operands = new List<string>(new string[] { 0.ToString("X"), 1.ToString("X") }) };
                        _ws.Send(serializer.Serialize(req));
                        SetClient();
                        streamEnd = false;
                        while (!streamEnd)
                        {
                            WaitSocket();
                            streamEnd = (_data[_data.Length - 2] == 0xFF && _data[_data.Length - 1] == 0xFF);
                            SetClient();
                        }

                        // Read out initialize data
                        uint screenX = 0, screenY = 0;
                        uint playerX = 0, playerY = 0;
                        uint animation = 0;
                        req = new RequestType() { Opcode = OpcodeType.GetAddress.ToString(), Space = "SNES", Operands = new List<string>(new string[] { 0xF5071F.ToString("X"), 0x04.ToString("X"),
                                                                                                                                                        0xF50911.ToString("X"), 0x02.ToString("X"),
                                                                                                                                                        0xF50915.ToString("X"), 0x02.ToString("X"),
                                                                                                                                                        0xF50AF6.ToString("X"), 0x02.ToString("X"),
                                                                                                                                                        0xF50AFA.ToString("X"), 0x02.ToString("X"),
                                                                                                                                                      }) };
                        _ws.Send(serializer.Serialize(req));
                        SetClient();
                        int curSize = 0;
                        while (curSize < 12)
                        {
                            WaitSocket();
                            curSize += _data.Length;
                            SetClient();
                        }
                        animation = (uint)((_data[3] << 24) | (_data[2] << 16) | (_data[1] << 8) | (_data[0] << 0));
                        screenX = (uint)((_data[5] << 8) | (_data[4] << 0));
                        screenY = (uint)((_data[7] << 8) | (_data[6] << 0));
                        playerX = (uint)((_data[9] << 8) | (_data[8] << 0));
                        playerY = (uint)((_data[11] << 8) | (_data[10] << 0));

                        // loop on position and animation data.  TODO: on NMI + synch send current data
                        while (true)
                        {
                            // FIXME: need synchronization.  For now just send as fast as possible
                            bool nmi = false;

                            // read out all data and apply it
                            req = new RequestType() { Opcode = OpcodeType.Stream.ToString(), Space = "MSU", Flags = new List<string>(new string[] { usbint_server_flags_e.STREAM_BURST.ToString() }) };
                            _ws.Send(serializer.Serialize(req));
                            SetClient();
                            streamEnd = false;
                            while (!streamEnd)
                            {
                                WaitSocket();
                                for (int i = 0; i < _data.Length && !streamEnd; i += 2)
                                {
                                    if (_data[i] >= 0x80 && _data[i] <= 0x83)
                                    {
                                        int index = _data[i] - 0x80;
                                        animation &= ~(0xFFU << (8 * index));
                                        animation |= (uint)(_data[i+1] << (8 * index));
                                    }
                                    else if (_data[i] >= 0xB0 && _data[i] <= 0xB1)
                                    {
                                        int index = _data[i] - 0xB0;
                                        playerX &= ~(0xFFU << (8 * index));
                                        playerX |= (uint)(_data[i + 1] << (8 * index));
                                    }                                
                                    else if (_data[i] >= 0xC0 && _data[i] <= 0xC1)
                                    {
                                        int index = _data[i] - 0xC0;
                                        playerY &= ~(0xFFU << (8 * index));
                                        playerY |= (uint)(_data[i + 1] << (8 * index));
                                    }

                                    // send current animation at NMI end
                                    if (_data[i] == 0xFF && _data[i+1] == 0x00)
                                    {
                                        if (_animationTable.ContainsKey(animation))
                                        {
                                            float x = (float)(playerX - 0x400);
                                            float y = (float)(playerY - 0x400);

                                            float u = _animationTable[animation][0];
                                            float v = _animationTable[animation][1];

                                            // send test vertices
                                            msg = "{\"request-type\":\"SetShader\", \"message-id\":\"0\", \"shader-opcode\":\"vertex-data\", \"draw\":0, \"vertices\":["
                                                + "{\"pos\":{\"x\":" + (x + 0.0).ToString()  + ", \"y\":" + (y + 0.0).ToString()  + ", \"z\":0.0}, \"uv\":{\"x\":" + (u + 0.0).ToString() + ", \"y\":" + (v + 0.0).ToString() + "}},"
                                                + "{\"pos\":{\"x\":" + (x + 63.0).ToString() + ", \"y\":" + (y + 0.0).ToString()  + ", \"z\":0.0}, \"uv\":{\"x\":" + (u + 1.0).ToString() + ", \"y\":" + (v + 0.0).ToString() + "}},"
                                                + "{\"pos\":{\"x\":" + (x + 0.0).ToString()  + ", \"y\":" + (y + 63.0).ToString() + ", \"z\":0.0}, \"uv\":{\"x\":" + (u + 0.0).ToString() + ", \"y\":" + (v + 1.0).ToString() + "}},"
                                                + "{\"pos\":{\"x\":" + (x + 63.0).ToString() + ", \"y\":" + (y + 63.0).ToString() + ", \"z\":0.0}, \"uv\":{\"x\":" + (u + 1.0).ToString() + ", \"y\":" + (v + 1.0).ToString() + "}}"
                                                + "]}";
                                            ws.Send(msg);
                                        }
                                    }

                                    streamEnd = (_data[i] == 0xFF && _data[i + 1] == 0xFF);
                                }

                                SetClient();
                            }
                        }

                        ws.Close();

                    }
                    else if (false)
                    {
                        openFileDialog1.Title = "RAM IPS file to load";
                        openFileDialog1.Filter = "IPS File|*.ips"
                                               + "|All Files|*.*";
                        openFileDialog1.FileName = "";
                        openFileDialog1.InitialDirectory = Settings.Default.TestDir;

                        if (openFileDialog1.ShowDialog() != DialogResult.Cancel)
                        {
                            Settings.Default.TestDir = Path.GetDirectoryName(openFileDialog1.FileName);
                            // boot currently selected ROM
                            bootFlags = usbint_server_flags_e.SKIPRESET;
                            buttonBoot.PerformClick();
                            bootFlags = usbint_server_flags_e.NONE;

                            // apply the selected patch
                            for (int i = 0; i < openFileDialog1.FileNames.Length; i++)
                            {
                                string fileName = openFileDialog1.FileNames[i];
                                string safeFileName = openFileDialog1.SafeFileNames[i];

                                sendPatch(fileName, safeFileName);
                            }

                            // perform reset
                            bootFlags = usbint_server_flags_e.ONLYRESET;
                            buttonBoot.PerformClick();
                            bootFlags = usbint_server_flags_e.NONE;
                        }
                    }
                    else if (false)
                    {
                        RequestType req = new RequestType() { Opcode = OpcodeType.AppVersion.ToString(), Space = "SNES" };
                        //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                        //var rsp = GetResponse();
                        _ws.Send(serializer.Serialize(req));
                        WaitSocket();
                    }
                    //else if (false)
                    //{
                    //    RequestType req = new RequestType() { Opcode = OpcodeType.GetAddress.ToString(), Space = "SNES", Operands = new List<string>(new string[] { 0xF00000.ToString("X"), 0x100.ToString("X"), 0xF10000.ToString("X"), 0x100.ToString("X"), 0xF20000.ToString("X"), 0x100.ToString("X") }) };
                    //    int size = 0x300;
                    //    //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                    //    _ws.Send(serializer.Serialize(req));
                    //    SetSocket();

                    //    int curSize = 0;
                    //    do
                    //    {
                    //        //var rsp = GetData();
                    //        WaitSocket();

                    //        SetSocket();
                    //        dataEnd = rsp.Item1;
                    //    } while (curSize < size);
                    //}
                    else if (false)
                    {
                        // offset = 0xinvmask,data,regnum size = 0xvalue
                        byte[] tBuffer = new byte[Constants.MaxMessageSize];

                        //foreach (var config in new int[] { 0x004101, 0x008902, 0x008103, 0x000004, 0x000107, 0x000100 })
                        //{
                        //    var req = new RequestType() { Opcode = OpcodeType.PutAddress.ToString(), Space = "CONFIG", Operands = new List<string>(new string[] { config.ToString("X"), 0x01.ToString("X") }) };
                        //    if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                        //    // dummy write
                        //    if (!_ws.SendAsync(new ArraySegment<byte>(tBuffer, 0, 64), WebSocketMessageType.Binary, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                        //}

                        //foreach (var config in new int[] { 0x000001, 0x000000 })
                        //{
                        //    var req = new RequestType() { Opcode = OpcodeType.GetAddress.ToString(), Space = "CONFIG", Operands = new List<string>(new string[] { config.ToString("X"), 0x01.ToString("X") }) };
                        //    if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                        //    bool dataEnd = false;
                        //    while (!dataEnd)
                        //    {
                        //        var rsp = GetData();
                        //        dataEnd = rsp.Item1;
                        //    }
                        //}

                        // 0xMASK_DATA_INDEX, GROUP
                        foreach (var config in new int[] { 0x000000, 0x002001 })
                        {
                            var req = new RequestType() { Opcode = OpcodeType.PutAddress.ToString(), Space = "CONFIG", Operands = new List<string>(new string[] { config.ToString("X"), 0x02.ToString("X") }) };
                            _ws.Send(serializer.Serialize(req));
                            // send dummy write
                            _ws.Send(new ArraySegment<byte>(tBuffer, 0, 64).ToArray());
                        }
                    }
                    else if (false)
                    {
                        byte[] tBuffer = new byte[Constants.MaxMessageSize];
                        int fileSize = 0x8;
                        int curSize = 0;

                        // write data
                        var req = new RequestType() { Opcode = OpcodeType.PutAddress.ToString(), Space = "SNES", Operands = new List<string>(new string[] { 0xFC2002.ToString("X"), (fileSize / 2).ToString("X"), 0xFC2002.ToString("X"), (fileSize / 2).ToString("X") }) };
                        //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                        _ws.Send(serializer.Serialize(req));

                        //Array.Clear(tBuffer, 0, tBuffer.Length);

                        tBuffer[0] = 0xFF;
                        tBuffer[1] = 0xFF;
                        tBuffer[2] = 0xFF;
                        tBuffer[3] = 0xFF;
                        tBuffer[4] = 0x50;
                        tBuffer[5] = 0x00;
                        tBuffer[6] = 0x60;
                        tBuffer[7] = 0x00;

                        curSize = 0;
                        while (curSize < fileSize)
                        {
                            int bytesToWrite = Math.Min(Constants.MaxMessageSize, fileSize - curSize);
                            curSize += bytesToWrite;
                            // need to limit the segment size to send correct amount
                            //if (!_ws.SendAsync(new ArraySegment<byte>(tBuffer, 0, bytesToWrite), WebSocketMessageType.Binary, curSize >= fileSize, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                            _ws.Send(new ArraySegment<byte>(tBuffer, 0, bytesToWrite).ToArray());
                        }

                    }
                    else if (false)
                    {
                        byte[] tBuffer = new byte[Constants.MaxMessageSize];
                        int fileSize = 0x2;
                        int curSize = 0;

                        for (int c = 0; c < 0x7ffffffe; c++)
                        {
                            var req = new RequestType() { Opcode = OpcodeType.GetAddress.ToString(), Space = "SNES", Operands = new List<string>(new string[] { 0xC0FFD5.ToString("X"), fileSize.ToString("X") }) };
                            //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                            _ws.Send(serializer.Serialize(req));
                            SetClient();

                            curSize = 0;
                            while (curSize < fileSize)
                            {
                                WaitSocket();
                                //var rsp = GetData();
                                //dataEnd = rsp.Item1;

                                curSize += _data.Length;
                                SetClient();
                            }
                        }
                    }
                    else
                    {
                        // test vector operations

                        byte[] tBuffer = new byte[Constants.MaxMessageSize];
                        int fileSize;
                        int curSize;

                        var r = new Random();

                        for (int c = 0; c < 0x7ffffffe; c++)
                        {
                            // NORESP=1
                            for (int i = 0; i < 1 + r.Next(9); i++)
                            {
                                RequestType req;
                                r.NextBytes(tBuffer);

                                var tuple = new List<string>();
                                fileSize = 0;
                                for (int j = 0; j < 1 + r.Next(8); j++)
                                {
                                    tuple.Add((0xE40000 + j * 0x400 + r.Next(0x200)).ToString("X"));
                                    int size = 1 + r.Next(0x1FF/*255*/);
                                    tuple.Add(size.ToString("X"));
                                    fileSize += size;
                                }

                                // write data
                                req = new RequestType() { Opcode = OpcodeType.PutAddress.ToString(), Space = "SNES", Operands = tuple };
                                //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                                _ws.Send(serializer.Serialize(req));

                                //Array.Clear(tBuffer, 0, tBuffer.Length);
                                curSize = 0;

                                while (curSize < fileSize)
                                {
                                    int bytesToWrite = Math.Min(Constants.MaxMessageSize, fileSize - curSize);
                                    curSize += bytesToWrite;
                                    // need to limit the segment size to send correct amount
                                    //if (!_ws.SendAsync(new ArraySegment<byte>(tBuffer, 0, bytesToWrite), WebSocketMessageType.Binary, curSize >= fileSize, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                                    _ws.Send(new ArraySegment<byte>(tBuffer, 0, bytesToWrite).ToArray());
                                }

                                req = new RequestType() { Opcode = OpcodeType.GetAddress.ToString(), Space = "SNES", Operands = tuple };
                                //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                                _ws.Send(serializer.Serialize(req));
                                SetClient();
                                curSize = 0;
                                while (curSize < fileSize)
                                {
                                    WaitSocket();
                                    //var rsp = GetData();
                                    //dataEnd = rsp.Item1;

                                    for (int j = 0; j < _data.Length; j++)
                                    {
                                        if (_data[j] != tBuffer[j % tBuffer.Length])
                                        {
                                            throw new Exception("bad data[" + j + "]: " + _data[j] + " != " + tBuffer[j % tBuffer.Length]);
                                        }
                                    }
                                    curSize += _data.Length;
                                    SetClient();
                                }
                            }
                        }
                    }

                }
            }
            catch (Exception x)
            {
                HandleException(x);
            }
        }

        private void sendPatch(string fileName, string safeFileName)
        {
            for (int i = 0; i < openFileDialog1.FileNames.Length; i++)
            {
                FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);

                byte[] tBuffer = new byte[Constants.MaxMessageSize];
                int curSize = 0;

                RequestType req = new RequestType() { Opcode = OpcodeType.PutIPS.ToString(), Space = "SNES", Operands = new List<string>(new string[] { "hook", fs.Length.ToString("X") }) };
                _ws.Send(serializer.Serialize(req));

                // write data
                curSize = 0;
                toolStripProgressBar1.Value = 0;
                toolStripProgressBar1.Enabled = true;
                toolStripStatusLabel1.Text = "uploading ram: " + safeFileName;

                while (curSize < fs.Length)
                {
                    int bytesToWrite = Math.Min(Constants.MaxMessageSize, (int)fs.Length - curSize);
                    Array.Clear(tBuffer, 0, tBuffer.Length);
                    fs.Read(tBuffer, 0, bytesToWrite);
                    //Array.Copy(patch.data.ToArray(), curSize, tBuffer, 0, bytesToWrite);

                    curSize += bytesToWrite;
                    // need to limit the segment size to send correct amount
                    //if (!_ws.SendAsync(new ArraySegment<byte>(tBuffer, 0, bytesToWrite), WebSocketMessageType.Binary, curSize >= patch.data.Count, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                    _ws.Send(new ArraySegment<byte>(tBuffer, 0, bytesToWrite).ToArray());

                    toolStripProgressBar1.Value = 100 * curSize / (int)fs.Length;
                }
                toolStripStatusLabel1.Text = "idle";
                toolStripProgressBar1.Enabled = false;
            }
        }

        private void makeDirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            buttonMkdir.PerformClick();
        }

        private void uploadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            buttonUpload.PerformClick();
        }

        private void downloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            buttonDownload.PerformClick();
        }

        private void bootToolStripMenuItem_Click(object sender, EventArgs e)
        {
            buttonBoot.PerformClick();
        }

        private void resetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            buttonSoftReset.PerformClick();
        }


        private void menuToolStripMenuItem_Click(object sender, EventArgs e)
        {
            buttonMenu.PerformClick();
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            buttonDelete.PerformClick();
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            buttonRename.PerformClick();
        }

        private void listViewRemote_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                refreshToolStripMenuItem.Enabled = false;
                bootToolStripMenuItem.Enabled = false;
                resetToolStripMenuItem.Enabled = false;
                makeDirToolStripMenuItem.Enabled = false;
                deleteToolStripMenuItem.Enabled = false;
                renameToolStripMenuItem.Enabled = false;

                if (connected)
                {
                    var info = listViewRemote.HitTest(e.X, e.Y);

                    refreshToolStripMenuItem.Enabled = true;
                    makeDirToolStripMenuItem.Enabled = true;
                    resetToolStripMenuItem.Enabled = true;

                    var loc = e.Location;
                    loc.Offset(listViewRemote.Location);

                    if (info.Item != null)
                    {
                        deleteToolStripMenuItem.Enabled = true;
                        renameToolStripMenuItem.Enabled = true;

                        if (info.Item.ImageIndex == 1)
                        {
                            bootToolStripMenuItem.Enabled = true;
                        }
                    }

                    this.contextMenuStripRemote.Show(this, loc);
                }
            }
        }

        private void backToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (remoteDir != remoteDirPrev)
            {
                // back
                remoteDirNext = remoteDir;
                remoteDir = remoteDirPrev;
                RefreshListViewRemote();
                backToolStripMenuItem.Enabled = false;
                forwardToolStripMenuItem.Enabled = true;
            }

        }

        private void forwardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // forward
            if (remoteDir != remoteDirNext)
            {
                remoteDirPrev = remoteDir;
                remoteDir = remoteDirNext;
                RefreshListViewRemote();
                backToolStripMenuItem.Enabled = true;
                forwardToolStripMenuItem.Enabled = false;
            }
        }

        private void listViewLocal_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var info = listViewLocal.HitTest(e.X, e.Y);
                if (info.Item != null)
                {
                    if (info.Item.ImageIndex == 0 && info.Item.Text != ".")
                    {
                        localDirPrev = localDir;
                        if (info.Item.Text == "..")
                        {
                            try
                            {
                                String dir = System.IO.Path.GetDirectoryName(localDir);
                                if (System.IO.Path.IsPathRooted(dir)) localDir = dir;
                            }
                            catch (Exception x)
                            {

                            }
                        }
                        else
                        {
                            // directory
                            localDir += @"\" + info.Item.Text;
                        }
                        RefreshListViewLocal();
                        localDirNext = localDir;
                        backToolStripMenuItem1.Enabled = true;
                        forwardToolStripMenuItem1.Enabled = false;
                    }
                    else if (info.Item.ImageIndex == 1)
                    {
                        buttonBoot.PerformClick();
                    }
                }
            }
        }

        private void usb2snes_FormClosed(object sender, FormClosedEventArgs e)
        {
            Settings.Default.LocalDir = localDir;
            Settings.Default.Save();
        }

        private void contextMenuStripRemote_Opening(object sender, CancelEventArgs e)
        {

        }

        private void backToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (localDir != localDirPrev)
            {
                // back
                localDirNext = localDir;
                localDir = localDirPrev;
                RefreshListViewLocal();
                backToolStripMenuItem1.Enabled = false;
                forwardToolStripMenuItem1.Enabled = true;
            }


        }

        private void forwardToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            // forward
            if (localDir != localDirNext)
            {
                localDirPrev = localDir;
                localDir = localDirNext;
                RefreshListViewLocal();
                backToolStripMenuItem.Enabled = true;
                forwardToolStripMenuItem.Enabled = false;
            }

        }

        private void listViewLocal_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                refreshToolStripMenuItem1.Enabled = false;
                makeDirToolStripMenuItem1.Enabled = false;
                renameToolStripMenuItem1.Enabled = false;

                {
                    var info = listViewLocal.HitTest(e.X, e.Y);

                    makeDirToolStripMenuItem1.Enabled = true;
                    refreshToolStripMenuItem1.Enabled = true;

                    var loc = e.Location;
                    loc.Offset(listViewLocal.Location);

                    if (info.Item != null)
                    {
                        renameToolStripMenuItem1.Enabled = true;
                    }

                    this.contextMenuStripLocal.Show(this, loc);
                }
            }

        }

        private void makeDirToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            String dirName = Microsoft.VisualBasic.Interaction.InputBox("Local Directory Name", "MkDir", "");
            if (dirName != "")
            {
                string name = localDir + '/' + dirName;
                if (name.Length < 256)
                {
                    Directory.CreateDirectory(name);
                    RefreshListViewLocal();
                }
            }

        }

        private void renameToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listViewLocal.SelectedItems)
            {
                string name = localDir + @"\" + item.Text;
                String newName = Microsoft.VisualBasic.Interaction.InputBox("New Name", "Rename", item.Text);

                if (newName != "")
                {
                    File.Move(name, localDir + @"\" + newName);
                }
            }

            RefreshListViewLocal();

        }

        private void usb2snes_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F5)
            {
                if (connected)
                {
                    RefreshListViewLocal();
                    RefreshListViewRemote();
                }
                else
                {
                    buttonRefresh.PerformClick();
                }
            }
        }

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RefreshListViewRemote();
        }

        private void refreshToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            RefreshListViewLocal();
        }

        private void buttonSoftReset_Click(object sender, EventArgs e)
        {
            try
            {
                if (connected)
                {
                    RequestType req = new RequestType() { Opcode = OpcodeType.Reset.ToString(), Space = "SNES" };
                    //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                    _ws.Send(serializer.Serialize(req));
                }
            }
            catch (Exception x)
            {
                HandleException(x);
            }
        }

        private void buttonMenu_Click(object sender, EventArgs e)
        {
            try
            {
                if (connected)
                {
                    RequestType req = new RequestType() { Opcode = OpcodeType.Menu.ToString(), Space = "SNES" };
                    //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                    _ws.Send(serializer.Serialize(req));

                    Thread.Sleep(300);

                    UpdateInfo();
                }
            }
            catch (Exception x)
            {
                HandleException(x);
            }

        }

        void UpdateInfo()
        {
            var req = new RequestType() { Opcode = OpcodeType.Info.ToString(), Space = "SNES" };
            _ws.Send(serializer.Serialize(req));
            WaitSocket();

            labelVersion.Text = "Firmware Version: " + _rsp.Results[0] + " (0x" + _rsp.Results[1] + ")";
            var name = _rsp.Results[2].Split('/').Last();
            romName.Text = "Rom Name: " + name.Substring(0, Math.Min(40, name.Length));
        }

        void SetClient()
        {
            _evClient.Set();
        }

        void WaitClient(bool extendTimeout = false)
        {
            // allow for a long timeout in the case of uploads
            if (WaitHandle.WaitAny(_clientWaitHandles, extendTimeout ? 120000 : 5000) == WaitHandle.WaitTimeout) throw new Exception("client timeout");
            _evClient.Reset();
        }

        void SetSocket()
        {
            _ev.Set();
        }

        void WaitSocket(bool extendTimeout = false)
        {
            // allow for a long timeout in the case of uploads
            if (WaitHandle.WaitAny(_socketWaitHandles, extendTimeout ? 120000 : 5000) == WaitHandle.WaitTimeout) throw new Exception("socket timeout");
            _ev.Reset();
        }

        void HandleException(Exception x)
        {
            toolStripStatusLabel1.Text = x.Message.ToString();
            connected = false;
            EnableButtons(false);
            _ev.Reset();
            _evClient.Set();
        }

        private void checkBoxShowExtensions_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.ShowAllExtensions = checkBoxShowExtensions.Checked;
            RefreshListViewLocal();
        }

        private Dictionary<uint, int[]> _animationTable = new Dictionary<uint, int[]>
        {
            { 0xd5f0d0e8, new int[] {0,0} },
            { 0xd1c1cdca, new int[] {9,17} },
            { 0xd1c8cdc3, new int[] {2,167} },
            { 0xd1c1cdd1, new int[] {9,18} },
            { 0xd1c8cdd8, new int[] {1,213} },
            { 0xd1cfcddf, new int[] {0,217} },
            { 0xd2a8d6bb, new int[] {0,233} },
            { 0xd1c8cddf, new int[] {0,213} },
            { 0xd1cfcdd8, new int[] {1,217} },
            { 0xd1cfcded, new int[] {4,217} },
            { 0xd1c8cded, new int[] {4,213} },
            { 0xd1cfcde6, new int[] {3,217} },
            { 0xd1c8cde6, new int[] {3,213} },
            { 0xd1bad189, new int[] {7,12} },
            { 0xd1c8cdfb, new int[] {0,214} },
            { 0xd1cfcdfb, new int[] {0,218} },
            { 0xd1cfcdf4, new int[] {1,218} },
            { 0xd1c8cdf4, new int[] {1,214} },
            { 0xd1d6d197, new int[] {1,12} },
            { 0xd2a8d6ec, new int[] {0,216} },
            { 0xd1d6d190, new int[] {1,11} },
            { 0xd2a8d6c9, new int[] {1,232} },
            { 0xd1accdca, new int[] {4,17} },
            { 0xd1ebd182, new int[] {8,11} },
            { 0xd2a8d6c2, new int[] {1,233} },
            { 0xd1bacdd1, new int[] {7,18} },
            { 0xd3b9cfd7, new int[] {57,178} },
            { 0xd1a5cdca, new int[] {2,17} },
            { 0xd1bacdca, new int[] {7,17} },
            { 0xd1a5cdd1, new int[] {2,18} },
            { 0xd1c8cdbc, new int[] {2,166} },
            { 0xd2a8d6d0, new int[] {2,232} },
            { 0xd1accdd1, new int[] {4,18} },
            { 0xd35ecfd7, new int[] {25,178} },
            { 0xd29ace10, new int[] {1,118} },
            { 0xd1c1d14a, new int[] {9,11} },
            { 0xd29ace17, new int[] {1,117} },
            { 0xd1e4d174, new int[] {6,12} },
            { 0xd1ebd17b, new int[] {8,12} },
            { 0xd1a5d13c, new int[] {2,11} },
            { 0xd1acd135, new int[] {4,11} },
            { 0xd1b3d12e, new int[] {5,11} },
            { 0xd1ddd143, new int[] {3,11} },
            { 0xd2d9ce79, new int[] {2,42} },
            { 0xd2d9ce72, new int[] {1,42} },
            { 0xd1e4d14a, new int[] {6,11} },
            { 0xd19ed12e, new int[] {0,11} },
            { 0xd2a1ce10, new int[] {4,118} },
            { 0xd1c1d174, new int[] {9,12} },
            { 0xd2d2ce64, new int[] {1,41} },
            { 0xd2a1ce17, new int[] {4,117} },
            //{ 0xd1f2cd45, new int[] {0,0} },
            { 0xd2d2ce6b, new int[] {2,41} },
            { 0xd1ddd166, new int[] {3,12} },
            { 0xd2a8d613, new int[] {0,232} },
            { 0xd32dcbee, new int[] {2,240} },
            { 0xd19ed158, new int[] {0,12} },
            { 0xd29ace5d, new int[] {1,73} },
            { 0xd33bcbfc, new int[] {1,240} },
            { 0xd1a5d16d, new int[] {2,12} },
            { 0xd794d75c, new int[] {2,212} },
            { 0xd29ace56, new int[] {1,74} },
            { 0xd1e4cd30, new int[] {6,9} },
            { 0xd1e4cd3e, new int[] {6,10} },
            { 0xd1d6cd37, new int[] {1,10} },
            { 0xd794d771, new int[] {2,211} },
            { 0xd4a7d04e, new int[] {7,179} },
            { 0xd1bad151, new int[] {7,11} },
            { 0xd1b3d158, new int[] {5,12} },
            { 0xd1acd15f, new int[] {4,12} },
            { 0xd2a1ce56, new int[] {4,74} },
            { 0xd2a1ce5d, new int[] {4,73} },
            { 0xd1d6cd29, new int[] {1,9} },
            { 0xd1cfd0cc, new int[] {3,40} },
            { 0xd79bd69f, new int[] {17,130} },
            { 0xd31fce17, new int[] {2,117} },
            { 0xd1f9ccf1, new int[] {0,155} },
            { 0xd1cfd0c5, new int[] {0,181} },
            { 0xd3b2ceb8, new int[] {65,178} },
            { 0xd1c8d0c5, new int[] {0,169} },
            { 0xd31fce10, new int[] {2,118} },
            { 0xd19ecc8f, new int[] {0,14} },
            { 0xd79bd68a, new int[] {9,130} },
            { 0xd3abceb8, new int[] {64,178} },
            { 0xd19ecc88, new int[] {0,13} },
            { 0xd1b3cca4, new int[] {5,15} },
            { 0xd1b3ccab, new int[] {5,16} },
            { 0xd79bd683, new int[] {1,130} },
            { 0xd3f8cee2, new int[] {42,178} },
            { 0xd1cfd0d3, new int[] {2,40} },
            { 0xd7bed69f, new int[] {22,130} },
            { 0xd7a2d683, new int[] {2,130} },
            { 0xd3f8cedb, new int[] {43,178} },
            { 0xd3f1ced4, new int[] {44,178} },
            { 0xd7b7d691, new int[] {13,130} },
            { 0xd2d2cbf5, new int[] {0,240} },
            { 0xd7b7d69f, new int[] {5,130} },
            { 0xd7b0d698, new int[] {20,130} },
            { 0xd7a9d683, new int[] {3,130} },
            { 0xd3eacec6, new int[] {46,178} },
            { 0xd7b0d69f, new int[] {12,130} },
            { 0xd7bed691, new int[] {14,130} },
            { 0xd7b7d698, new int[] {21,130} },
            { 0xd2c4cbf5, new int[] {4,199} },
            { 0xd326ce17, new int[] {5,117} },
            { 0xd7a9d698, new int[] {19,130} },
            { 0xd19eccab, new int[] {0,16} },
            { 0xd7a9d69f, new int[] {11,130} },
            { 0xd326ce10, new int[] {5,118} },
            { 0xd388cebf, new int[] {15,178} },
            { 0xd381ceb8, new int[] {16,178} },
            { 0xd3b9ce80, new int[] {56,178} },
            { 0xd19ecca4, new int[] {0,15} },
            { 0xd1b3cc88, new int[] {5,13} },
            { 0xd1b3cc8f, new int[] {5,14} },
            { 0xd3f1cecd, new int[] {45,178} },
            { 0xd7a2d69f, new int[] {18,130} },
            { 0xd786d6bb, new int[] {0,212} },
            { 0xd3b9ce87, new int[] {55,178} },
            { 0xd1e4cca4, new int[] {6,15} },
            { 0xd1cfcc8f, new int[] {1,134} },
            { 0xd1c8cc88, new int[] {1,224} },
            { 0xd396ced4, new int[] {12,178} },
            { 0xd794d6d0, new int[] {3,211} },
            { 0xd318ce5d, new int[] {5,73} },
            { 0xd32dce6b, new int[] {9,80} },
            { 0xd7ccd68a, new int[] {8,130} },
            { 0xd39dcedb, new int[] {11,178} },
            { 0xd1cfcc88, new int[] {1,133} },
            { 0xd1c8cc8f, new int[] {1,225} },
            { 0xd31fce56, new int[] {2,74} },
            { 0xd38fcec6, new int[] {14,178} },
            { 0xd311ce5d, new int[] {2,73} },
            { 0xd502d04e, new int[] {39,179} },
            { 0xd334ce79, new int[] {9,79} },
            { 0xd3c0ce8e, new int[] {54,178} },
            { 0xd1e4ccab, new int[] {6,16} },
            { 0xd7c5d68a, new int[] {7,130} },
            { 0xd786d6c9, new int[] {0,211} },
            { 0xd78dd6c2, new int[] {1,212} },
            { 0xd7ccd69f, new int[] {24,130} },
            { 0xd7c5d691, new int[] {15,130} },
            { 0xd3c0ce95, new int[] {53,178} },
            { 0xd55dd008, new int[] {29,179} },
            { 0xd794d6c2, new int[] {3,212} },
            { 0xd2b6cbee, new int[] {5,240} },
            { 0xd1d6cc8f, new int[] {1,14} },
            { 0xd7c5d69f, new int[] {23,130} },
            { 0xd396cecd, new int[] {13,178} },
            { 0xd3c7ce9c, new int[] {52,178} },
            { 0xd3e3cebf, new int[] {47,178} },
            { 0xd55dd001, new int[] {28,179} },
            { 0xd579d024, new int[] {1,179} },
            { 0xd78dd6d0, new int[] {1,211} },
            { 0xd1d6cc88, new int[] {1,13} },
            { 0xd3dcceb8, new int[] {48,178} },
            { 0xd3d5ceb1, new int[] {49,178} },
            { 0xd3ceceaa, new int[] {50,178} },
            { 0xd3c7cea3, new int[] {51,178} },
            { 0xd1e4cc8f, new int[] {6,14} },
            { 0xd564d00f, new int[] {30,179} },
            { 0xd1e4cc88, new int[] {6,13} },
            { 0xd572d01d, new int[] {0,179} },
            { 0xd326ce56, new int[] {5,74} },
            { 0xd1cfd0be, new int[] {2,39} },
            { 0xd1d6cca4, new int[] {1,15} },
            { 0xd1ebcc9d, new int[] {8,10} },
            { 0xd1c8d0b0, new int[] {0,168} },
            { 0xd1cfd0b7, new int[] {3,39} },
            { 0xd1ebcc96, new int[] {8,9} },
            { 0xd1d6ccab, new int[] {1,16} },
            { 0xd56bd016, new int[] {31,179} },
            { 0xd1cfd0b0, new int[] {0,180} },
            { 0xd39dcee2, new int[] {10,178} },
            { 0xd1bacc3b, new int[] {7,10} },
            { 0xd1a5cc26, new int[] {2,10} },
            { 0xd19ecc18, new int[] {0,10} },
            { 0xd1b3cc34, new int[] {0,200} },
            { 0xd1cfcc5e, new int[] {2,217} },
            { 0xd1d6cc42, new int[] {1,17} },
            { 0xd1c8cc5e, new int[] {1,230} },
            { 0xd1cfcc57, new int[] {0,183} },
            { 0xd1c8cc50, new int[] {1,228} },
            { 0xd1d6cc49, new int[] {1,18} },
            { 0xd1cfcc50, new int[] {0,182} },
            { 0xd1c8cc57, new int[] {1,229} },
            { 0xd1cfcc6c, new int[] {0,133} },
            { 0xd1c8cc6c, new int[] {1,226} },
            { 0xd1e4cc42, new int[] {6,17} },
            { 0xd5e9d040, new int[] {37,179} },
            { 0xd1cfcc65, new int[] {2,218} },
            { 0xd1bacc11, new int[] {7,9} },
            { 0xd1b3cc18, new int[] {5,10} },
            { 0xd580d02b, new int[] {2,179} },
            { 0xd1e4cc49, new int[] {6,18} },
            { 0xd1c8cc65, new int[] {1,231} },
            { 0xd5e9d047, new int[] {38,179} },
            { 0xd5b8d008, new int[] {61,179} },
            { 0xd5bfd00f, new int[] {62,179} },
            { 0xd1accc1f, new int[] {4,10} },
            { 0xd587d032, new int[] {3,179} },
            { 0xd1b3cc0a, new int[] {0,199} },
            { 0xd5b8d001, new int[] {60,179} },
            { 0xd1c8cc73, new int[] {1,227} },
            { 0xd1cfcc73, new int[] {0,134} },
            { 0xd587d039, new int[] {4,179} },
            { 0xd58ed047, new int[] {6,179} },
            { 0xd1c1cc0a, new int[] {9,9} },
            { 0xd37aceb1, new int[] {17,178} },
            { 0xd58ed040, new int[] {5,179} },
            { 0xd36ccea3, new int[] {19,178} },
            { 0xd5cdd01d, new int[] {32,179} },
            { 0xd5e2d032, new int[] {35,179} },
            { 0xd5c6d016, new int[] {63,179} },
            { 0xd19ecc49, new int[] {0,18} },
            { 0xd373ceaa, new int[] {18,178} },
            { 0xd35ece87, new int[] {23,178} },
            { 0xd5e2d039, new int[] {36,179} },
            { 0xd19ecc42, new int[] {0,17} },
            { 0xd1ddcc03, new int[] {3,9} },
            { 0xd35ece80, new int[] {24,178} },
            { 0xd365ce8e, new int[] {22,178} },
            { 0xd1ddcc2d, new int[] {3,10} },
            { 0xd36cce9c, new int[] {20,178} },
            { 0xd365ce95, new int[] {21,178} },
            { 0xd5d4d024, new int[] {33,179} },
            { 0xd5dbd02b, new int[] {34,179} },
            { 0xd1b3cc42, new int[] {5,17} },
            { 0xd1c1cc34, new int[] {9,10} },
            { 0xd1b3cc49, new int[] {5,18} },
            { 0xd510cf13, new int[] {44,179} },
            { 0xd509cf0c, new int[] {43,179} },
            { 0xd510cf1a, new int[] {45,179} },
            { 0xd525cf2f, new int[] {48,179} },
            { 0xd509cf05, new int[] {42,179} },
            { 0xd533cf3d, new int[] {50,179} },
            { 0xd541cf52, new int[] {53,179} },
            { 0xd541cf59, new int[] {54,179} },
            { 0xd52ccf36, new int[] {49,179} },
            { 0xd27ecc65, new int[] {3,73} },
            { 0xd27ecc5e, new int[] {3,74} },
            { 0xd2a8cc88, new int[] {0,247} },
            { 0xd2afcc8f, new int[] {0,248} },
            { 0xd54fcf60, new int[] {23,179} },
            { 0xd517cf21, new int[] {46,179} },
            { 0xd51ecf28, new int[] {47,179} },
            { 0xd293cca4, new int[] {0,118} },
            { 0xd27ecc49, new int[] {3,119} },
            { 0xd293ccab, new int[] {0,117} },
            { 0xd27ecc42, new int[] {3,120} },
            { 0xd5b1cff3, new int[] {58,179} },
            { 0xd5aacfec, new int[] {57,179} },
            { 0xd311cd5a, new int[] {0,99} },
            { 0xd5b1cffa, new int[] {59,179} },
            { 0xd303cd4c, new int[] {0,131} },
            { 0xd5aacfe5, new int[] {56,179} },
            { 0xd28cccdc, new int[] {1,163} },
            { 0xd4a7cef7, new int[] {8,179} },
            { 0xd605cc50, new int[] {0,209} },
            { 0xd2d9cc8f, new int[] {1,44} },
            { 0xd2d9cc81, new int[] {5,200} },
            { 0xd1a5cbfc, new int[] {2,9} },
            { 0xd1accbf5, new int[] {4,9} },
            { 0xd30acd53, new int[] {0,132} },
            { 0xd285ccdc, new int[] {1,194} },
            { 0xd4a7cefe, new int[] {9,179} },
            { 0xd2d2cc88, new int[] {1,43} },
            { 0xd60ccc57, new int[] {0,210} },
            { 0xd1b3cbee, new int[] {5,9} },
            { 0xd2c4cca4, new int[] {0,109} },
            { 0xd2cbccab, new int[] {0,110} },
            { 0xd285cce3, new int[] {1,196} },
            { 0xd605cc6c, new int[] {0,207} },
            { 0xd2d2ccb9, new int[] {3,232} },
            { 0xd2d9ccb2, new int[] {2,233} },
            { 0xd28ccce3, new int[] {1,154} },
            { 0xd19ecbee, new int[] {0,9} },
            { 0xd53acf4b, new int[] {52,179} },
            { 0xd318cd61, new int[] {0,100} },
            { 0xd53acf44, new int[] {51,179} },
            { 0xd60ccc73, new int[] {0,208} },
            { 0xd22accab, new int[] {1,106} },
            { 0xd342cdc3, new int[] {0,186} },
            { 0xd2cbcc49, new int[] {0,177} },
            { 0xd60ccc8f, new int[] {1,4} },
            { 0xd2c4cc42, new int[] {0,176} },
            { 0xd223cca4, new int[] {1,105} },
            { 0xd33bcdbc, new int[] {0,236} },
            { 0xd461cee9, new int[] {41,178} },
            { 0xd2bdcc34, new int[] {2,200} },
            { 0xd2d2cc5e, new int[] {2,103} },
            { 0xd605cc88, new int[] {1,3} },
            { 0xd32dcdbc, new int[] {0,166} },
            { 0xd2c4cc5e, new int[] {0,172} },
            { 0xd54fcfec, new int[] {25,179} },
            { 0xd22acc8f, new int[] {1,22} },
            { 0xd2bdcc18, new int[] {3,200} },
            { 0xd556cff3, new int[] {26,179} },
            { 0xd2c4cc6c, new int[] {0,43} },
            { 0xd2d2cc7a, new int[] {5,199} },
            { 0xd54fcfe5, new int[] {24,179} },
            { 0xd223cc88, new int[] {1,21} },
            { 0xd556cffa, new int[] {27,179} },
            { 0xd2cbcc65, new int[] {0,173} },
            { 0xd200d0b7, new int[] {3,1} },
            { 0xd2cbcc73, new int[] {0,44} },
            { 0xd207d0be, new int[] {2,1} },
            { 0xd246ccff, new int[] {2,136} },
            { 0xd269d0d3, new int[] {2,2} },
            { 0xd2b6cc0a, new int[] {2,199} },
            { 0xd2d9cc65, new int[] {2,104} },
            { 0xd20ed0cc, new int[] {3,2} },
            { 0xd2a8cc6c, new int[] {0,249} },
            { 0xd2d9cc1f, new int[] {0,190} },
            { 0xd238ccff, new int[] {0,198} },
            { 0xd23fccf8, new int[] {0,136} },
            { 0xd231ccf8, new int[] {2,198} },
            { 0xd2afcc65, new int[] {0,231} },
            { 0xd5aacf60, new int[] {55,179} },
            { 0xd19ed755, new int[] {0,204} },
            { 0xd293cc5e, new int[] {0,74} },
            { 0xd2a8cc65, new int[] {2,222} },
            { 0xd31fcdd1, new int[] {2,119} },
            { 0xd19ed74e, new int[] {0,203} },
            { 0xd293cc42, new int[] {0,120} },
            { 0xd2cbcc1f, new int[] {4,200} },
            { 0xd27eccab, new int[] {3,117} },
            { 0xd31fcdca, new int[] {2,120} },
            { 0xd2d2cc0a, new int[] {3,240} },
            { 0xd27ecca4, new int[] {3,118} },
            { 0xd293cc49, new int[] {0,119} },
            { 0xd2e7cc3b, new int[] {1,200} },
            { 0xd2afcc73, new int[] {0,250} },
            { 0xd326cdca, new int[] {5,120} },
            { 0xd2d9cc34, new int[] {3,190} },
            { 0xd406cee9, new int[] {9,178} },
            { 0xd2e0cc11, new int[] {4,240} },
            { 0xd2a8cc5e, new int[] {0,230} },
            { 0xd293cc65, new int[] {0,73} },
            { 0xd334cdc3, new int[] {0,167} },
            { 0xd326cdd1, new int[] {5,119} },
            { 0xd2a8cc50, new int[] {0,251} },
            { 0xd2afcc57, new int[] {0,246} },
            { 0xd19ed69f, new int[] {5,102} },
            { 0xd25bcd5a, new int[] {0,205} },
            { 0xd262cd61, new int[] {0,206} },
            { 0xd19ed698, new int[] {4,102} },
            { 0xd2cbcdc3, new int[] {1,79} },
            { 0xd2a8cda0, new int[] {1,166} },
            { 0xd2afcda7, new int[] {1,167} },
            { 0xd342cc49, new int[] {0,189} },
            { 0xd19ed691, new int[] {3,102} },
            { 0xd461cf6e, new int[] {40,178} },
            { 0xd19ed68a, new int[] {2,102} },
            { 0xd468cf7c, new int[] {38,178} },
            { 0xd461cf75, new int[] {39,178} },
            { 0xd19ed683, new int[] {1,102} },
            { 0xd484cfa6, new int[] {32,178} },
            { 0xd19ed6bb, new int[] {0,222} },
            { 0xd342cc65, new int[] {0,188} },
            { 0xd492cfbb, new int[] {61,178} },
            { 0xd484cfad, new int[] {63,178} },
            { 0xd19ed6b4, new int[] {8,102} },
            { 0xd334cc18, new int[] {2,190} },
            { 0xd19ed6ad, new int[] {7,102} },
            { 0xd238cd0d, new int[] {2,194} },
            { 0xd231cd06, new int[] {0,194} },
            { 0xd19ed6a6, new int[] {6,102} },
            { 0xd23fcd06, new int[] {0,163} },
            { 0xd246cd7d, new int[] {2,154} },
            { 0xd48bcfb4, new int[] {62,178} },
            { 0xd238cd7d, new int[] {2,196} },
            { 0xd231cd76, new int[] {0,196} },
            { 0xd23fcd76, new int[] {0,154} },
            { 0xd499cfd0, new int[] {58,178} },
            { 0xd29acdd1, new int[] {1,119} },
            { 0xd246cd0d, new int[] {2,163} },
            { 0xd19ed6d0, new int[] {1,221} },
            { 0xd29acdca, new int[] {1,120} },
            { 0xd499cfc9, new int[] {59,178} },
            { 0xd492cfc2, new int[] {60,178} },
            { 0xd334cc65, new int[] {1,173} },
            { 0xd24dcd1b, new int[] {0,185} },
            { 0xd19ed6c9, new int[] {0,221} },
            { 0xd19ed6c2, new int[] {1,222} },
            { 0xd342cc26, new int[] {1,190} },
            { 0xd33bcc5e, new int[] {0,238} },
            { 0xd406cf6e, new int[] {8,178} },
            { 0xd2a1cdca, new int[] {4,120} },
            { 0xd32dcc42, new int[] {1,176} },
            { 0xd2a1cdd1, new int[] {4,119} },
            { 0xd2f5cd84, new int[] {1,174} },
            { 0xd40dcf7c, new int[] {6,178} },
            { 0xd32dcc5e, new int[] {1,172} },
            { 0xd406cf75, new int[] {7,178} },
            { 0xd254cd22, new int[] {0,184} },
            { 0xd2fccd8b, new int[] {1,175} },
            { 0xd2c4cdbc, new int[] {1,80} },
            { 0xd33bcc42, new int[] {0,239} },
            { 0xd334cc49, new int[] {1,177} },
            { 0xd33bccb9, new int[] {5,215} },
            { 0xd41bcf98, new int[] {2,178} },
            { 0xd437cfb4, new int[] {30,178} },
            { 0xd19ed61a, new int[] {5,223} },
            { 0xd414cf91, new int[] {3,178} },
            { 0xd43ecfbb, new int[] {29,178} },
            { 0xd445cfc9, new int[] {27,178} },
            { 0xd19ed613, new int[] {7,223} },
            { 0xd40dcf83, new int[] {5,178} },
            { 0xd429cfa6, new int[] {0,178} },
            { 0xd285cd14, new int[] {1,198} },
            { 0xd1ebce79, new int[] {8,14} },
            { 0xd1ddce4f, new int[] {3,18} },
            { 0xd4dfcf4b, new int[] {20,179} },
            { 0xd1ddce48, new int[] {3,17} },
            { 0xd445cfd0, new int[] {26,178} },
            { 0xd28ccd14, new int[] {1,136} },
            { 0xd1ebce72, new int[] {8,13} },
            { 0xd4dfcf44, new int[] {19,179} },
            { 0xd1ddce41, new int[] {3,16} },
            { 0xd430cfad, new int[] {31,178} },
            { 0xd4bccf21, new int[] {14,179} },
            { 0xd414cf8a, new int[] {4,178} },
            { 0xd33bcca4, new int[] {0,237} },
            { 0xd21ccdbc, new int[] {0,80} },
            { 0xd4aecf0c, new int[] {11,179} },
            { 0xd1ebce48, new int[] {8,17} },
            { 0xd19ed63d, new int[] {2,223} },
            { 0xd1ddce79, new int[] {3,14} },
            { 0xd1ebce4f, new int[] {8,18} },
            { 0xd1acce09, new int[] {4,14} },
            { 0xd32dcc88, new int[] {2,43} },
            { 0xd4b5cf13, new int[] {12,179} },
            { 0xd1a5ce02, new int[] {2,13} },
            { 0xd19ed636, new int[] {4,223} },
            { 0xd1bace10, new int[] {7,15} },
            { 0xd1ebce41, new int[] {8,16} },
            { 0xd4aecf05, new int[] {10,179} },
            { 0xd1a5ce09, new int[] {2,14} },
            { 0xd1bace17, new int[] {7,16} },
            { 0xd1acce02, new int[] {4,13} },
            { 0xd1ddce72, new int[] {3,13} },
            { 0xd4b5cf1a, new int[] {13,179} },
            { 0xd19ed62f, new int[] {6,223} },
            { 0xd1a5ce17, new int[] {2,16} },
            { 0xd1bace09, new int[] {7,14} },
            { 0xd4e6cf52, new int[] {21,179} },
            { 0xd1a5ce10, new int[] {2,15} },
            { 0xd19ed628, new int[] {1,223} },
            { 0xd1bace02, new int[] {7,13} },
            { 0xd1acce17, new int[] {4,16} },
            { 0xd334cc8f, new int[] {2,44} },
            { 0xd1acce10, new int[] {4,15} },
            { 0xd422cf9f, new int[] {1,178} },
            { 0xd19ed621, new int[] {3,223} },
            { 0xd4e6cf59, new int[] {22,179} },
            { 0xd1c8ce09, new int[] {4,214} },
            { 0xd1c1ce02, new int[] {9,13} },
            { 0xd350d094, new int[] {2,235} },
            { 0xd1cfce09, new int[] {4,218} },
            { 0xd19ed659, new int[] {7,80} },
            { 0xd1c1ce09, new int[] {9,14} },
            { 0xd1c8ce02, new int[] {3,214} },
            { 0xd19ed652, new int[] {8,80} },
            { 0xd357d09b, new int[] {0,234} },
            { 0xd1cfce02, new int[] {3,218} },
            { 0xd1ebce3a, new int[] {8,15} },
            { 0xd1c1ce10, new int[] {9,15} },
            { 0xd4fbcf2f, new int[] {65,179} },
            { 0xd19ed64b, new int[] {1,66} },
            { 0xd1c1ce17, new int[] {9,16} },
            { 0xd215cdc3, new int[] {0,79} },
            { 0xd350d086, new int[] {0,235} },
            { 0xd19ed644, new int[] {0,223} },
            { 0xd4f4cf2f, new int[] {64,179} },
            { 0xd350d08d, new int[] {3,235} },
            { 0xd19ed67c, new int[] {2,80} },
            { 0xd47dcf9f, new int[] {33,178} },
            { 0xd46fcf8a, new int[] {36,178} },
            { 0xd4cacf2f, new int[] {16,179} },
            { 0xd4d8cf3d, new int[] {18,179} },
            { 0xd1ddce3a, new int[] {3,15} },
            { 0xd4d1cf36, new int[] {17,179} },
            { 0xd342ccab, new int[] {0,187} },
            { 0xd19ed675, new int[] {3,80} },
            { 0xd468cf83, new int[] {37,178} },
            { 0xd4c3cf28, new int[] {15,179} },
            { 0xd476cf98, new int[] {34,178} },
            { 0xd19ed66e, new int[] {4,80} },
            { 0xd342ccb2, new int[] {5,216} },
            { 0xd502cef7, new int[] {40,179} },
            { 0xd357d0a2, new int[] {3,234} },
            { 0xd19ed667, new int[] {5,80} },
            { 0xd43ecfc2, new int[] {28,178} },
            { 0xd502cefe, new int[] {41,179} },
            { 0xd19ed660, new int[] {6,80} },
            { 0xd46fcf91, new int[] {35,178} },
            { 0xd357d0a9, new int[] {2,234} },
        };
    }
}
