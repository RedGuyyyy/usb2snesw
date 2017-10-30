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
        JavaScriptSerializer serializer = new JavaScriptSerializer();
        private usbint_server_flags_e bootFlags = usbint_server_flags_e.NONE;

        public usb2snesfileviewer()
        {
            InitializeComponent();
            //PopulateTreeViewLocal();
            listViewRemote.ListViewItemSorter = new Sorter();
            listViewLocal.ListViewItemSorter = new Sorter();
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

        private void RefreshListViewRemote()
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
                    WaitSocket();
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
                    toolStripStatusLabel1.Text = x.Message.ToString();
                    connected = false;
                    EnableButtons(false);
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
                    if (type == 1 && Path.GetExtension(name).ToLower() != ".sfc" && Path.GetExtension(name).ToLower() != ".smc" && Path.GetExtension(name).ToLower() != ".fig") continue;

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
            SetSocket();
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

                _ws.OnOpen += ws_Opened;
                _ws.OnMessage += ws_MessageReceived;
                _ws.OnClose += ws_Closed;

                _ws.Connect();
                WaitSocket();

                RequestType req = new RequestType() { Opcode = OpcodeType.DeviceList.ToString(), Space = "SNES" };
                _ws.Send(serializer.Serialize(req));
                WaitSocket();

                //RequestType req = new RequestType() { Opcode = OpcodeType.DeviceList.ToString(), Space = "SNES" };
                //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                //_ws.Send(serializer.Serialize(req));
                //var rsp = GetResponse();

                // get device list
                foreach (var port in _rsp.Results)
                    comboBoxPort.Items.Add(port);

                if (comboBoxPort.Items.Count != 0)
                {
                    comboBoxPort.SelectedIndex = -1;
                    comboBoxPort.SelectedIndex = 0;
                }
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                connected = false;
                EnableButtons(false);
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

                        RefreshListViewRemote();
                    }
                }
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                //_port.Disconnect();
                connected = false;
                EnableButtons(false);
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
                toolStripStatusLabel1.Text = x.Message.ToString();
                //_port.Disconnect();
                connected = false;
                EnableButtons(false);
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
                }
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                //_port.Disconnect();
                connected = false;
                EnableButtons(false);
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
                toolStripStatusLabel1.Text = x.Message.ToString();
                connected = false;
                EnableButtons(false);
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
                toolStripStatusLabel1.Text = x.Message.ToString();
                connected = false;
                EnableButtons(false);
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
                toolStripStatusLabel1.Text = x.Message.ToString();
                connected = false;
                EnableButtons(false);
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

                            applyPatch(fileName, safeFileName);
                        }
                    }
                }
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                connected = false;
                EnableButtons(false);
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
                toolStripStatusLabel1.Text = x.Message.ToString();
                connected = false;
                EnableButtons(false);
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
                            RequestType req = new RequestType() { Opcode = OpcodeType.PutAddress.ToString(), Space = "SNES", Operands = new List<string>(new string[] { 0xF00000.ToString("X"), fs.Length.ToString("X") }) };
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
                toolStripStatusLabel1.Text = x.Message.ToString();
                connected = false;
                EnableButtons(false);
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

                    if (true)
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

                                applyPatch(fileName, safeFileName);
                            }

                            // perform reset
                            bootFlags = usbint_server_flags_e.ONLYRESET;
                            buttonBoot.PerformClick();
                            bootFlags = usbint_server_flags_e.NONE;
                        }
                    }
                    else if (false)
                    {
                        RequestType req = new RequestType() { Opcode = OpcodeType.Info.ToString(), Space = "SNES" };
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
                    //else if (false)
                    //{
                    //    // offset = 0xinvmask,data,regnum size = 0xvalue
                    //    byte[] tBuffer = new byte[Constants.MaxMessageSize];

                    //    foreach (var config in new int[] { 0x004101, 0x008902, 0x008103, 0x000004, 0x000107, 0x000100 }) {
                    //        var req = new RequestType() { Opcode = OpcodeType.PutAddress.ToString(), Space = "CONFIG", Operands = new List<string>(new string[] { config.ToString("X"), 0x01.ToString("X") }) };
                    //        if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                    //        // dummy write
                    //        if (!_ws.SendAsync(new ArraySegment<byte>(tBuffer, 0, 64), WebSocketMessageType.Binary, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                    //    }

                    //    foreach (var config in new int[] { 0x000001, 0x000000 })
                    //    {
                    //        var req = new RequestType() { Opcode = OpcodeType.GetAddress.ToString(), Space = "CONFIG", Operands = new List<string>(new string[] { config.ToString("X"), 0x01.ToString("X") }) };
                    //        if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                    //        bool dataEnd = false;
                    //        while (!dataEnd)
                    //        {
                    //            var rsp = GetData();
                    //            dataEnd = rsp.Item1;
                    //        }
                    //    }

                    //}
                    //else
                    //{
                    //    // test vector operations

                    //    byte[] tBuffer = new byte[Constants.MaxMessageSize];
                    //    int fileSize;
                    //    int curSize;

                    //    var r = new Random();

                    //    for (int c = 0; c < 0x7ffffffe; c++)
                    //    {
                    //        // NORESP=1
                    //        for (int i = 0; i < 1 + r.Next(9); i++)
                    //        {
                    //            RequestType req;
                    //            r.NextBytes(tBuffer);

                    //            var tuple = new List<string>();
                    //            fileSize = 0;
                    //            // FIXME: force 2
                    //            for (int j = 0; j < 1 + r.Next(8); j++)
                    //            {
                    //                tuple.Add((0xE40000 + j * 0x400 + r.Next(0x300)).ToString("X"));
                    //                int size = 1 + r.Next(255);
                    //                tuple.Add(size.ToString("X"));
                    //                fileSize += size;
                    //            }

                    //            // write data
                    //            req = new RequestType() { Opcode = OpcodeType.PutAddress.ToString(), Space = "SNES", Operands = tuple };
                    //            if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                    //            //Array.Clear(tBuffer, 0, tBuffer.Length);
                    //            curSize = 0;

                    //            while (curSize < fileSize)
                    //            {
                    //                int bytesToWrite = Math.Min(Constants.MaxMessageSize, fileSize - curSize);
                    //                curSize += bytesToWrite;
                    //                // need to limit the segment size to send correct amount
                    //                if (!_ws.SendAsync(new ArraySegment<byte>(tBuffer, 0, bytesToWrite), WebSocketMessageType.Binary, curSize >= fileSize, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                    //            }

                    //            req = new RequestType() { Opcode = OpcodeType.GetAddress.ToString(), Space = "SNES", Operands = tuple };
                    //            if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                    //            bool dataEnd = false;
                    //            while (!dataEnd)
                    //            {
                    //                var rsp = GetData();
                    //                dataEnd = rsp.Item1;

                    //                for (int j = 0; j < rsp.Item2.Length; j++)
                    //                {
                    //                    if (rsp.Item2[j] != tBuffer[j % tBuffer.Length])
                    //                    {
                    //                        throw new Exception("bad data[" + j + "]: " + rsp.Item2[j] + " != " + tBuffer[j % tBuffer.Length]);
                    //                    }
                    //                }
                    //            }
                    //        }                            
                    //    }
                    //}

                }
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                //_port.Disconnect();
                connected = false;
                EnableButtons(false);
            }
        }

        private void applyPatch(string fileName, string safeFileName)
        {
            for (int i = 0; i < openFileDialog1.FileNames.Length; i++)
            {
                IPS ips = new IPS();
                ips.Parse(fileName);

                byte[] tBuffer = new byte[Constants.MaxMessageSize];
                int curSize = 0;

                // send write command
                int patchNum = 0;
                foreach (var patch in ips.Items)
                {
                    RequestType req = new RequestType() { Opcode = OpcodeType.PutAddress.ToString(),
                                                          Space = "SNES",
                                                          Operands = new List<string>(new string[] { patch.address.ToString("X"), patch.data.Count.ToString("X") }),
                                                          Flags = new List<string>(new string[] { (patchNum == 0 && i == 0) ? "CLRX" : (patchNum == ips.Items.Count - 1 && i == openFileDialog1.FileNames.Length - 1) ? "SETX" : "NONE" })
                                                          };
                    //if (!_ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(serializer.Serialize(req))), WebSocketMessageType.Text, true, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                    _ws.Send(serializer.Serialize(req));

                    // write data
                    Array.Clear(tBuffer, 0, tBuffer.Length);
                    curSize = 0;
                    toolStripProgressBar1.Value = 0;
                    toolStripProgressBar1.Enabled = true;
                    toolStripStatusLabel1.Text = "uploading ram: " + safeFileName;

                    while (curSize < patch.data.Count)
                    {
                        int bytesToWrite = Math.Min(Constants.MaxMessageSize, patch.data.Count - curSize);
                        Array.Clear(tBuffer, 0, tBuffer.Length);
                        Array.Copy(patch.data.ToArray(), curSize, tBuffer, 0, bytesToWrite);

                        curSize += bytesToWrite;
                        // need to limit the segment size to send correct amount
                        //if (!_ws.SendAsync(new ArraySegment<byte>(tBuffer, 0, bytesToWrite), WebSocketMessageType.Binary, curSize >= patch.data.Count, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
                        _ws.Send(new ArraySegment<byte>(tBuffer, 0, bytesToWrite).ToArray());

                        toolStripProgressBar1.Value = 100 * curSize / patch.data.Count;
                    }
                    toolStripStatusLabel1.Text = "idle";
                    toolStripProgressBar1.Enabled = false;

                    patchNum++;
                }
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
                //deleteToolStripMenuItem1.Enabled = false;
                renameToolStripMenuItem1.Enabled = false;

                {
                    var info = listViewLocal.HitTest(e.X, e.Y);

                    makeDirToolStripMenuItem1.Enabled = true;
                    refreshToolStripMenuItem1.Enabled = true;

                    var loc = e.Location;
                    loc.Offset(listViewLocal.Location);

                    if (info.Item != null)
                    {
                        //deleteToolStripMenuItem1.Enabled = true;
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
                RefreshListViewLocal();
                RefreshListViewRemote();
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

        private class IPS
        {
            public IPS() { Items = new List<Patch>(); }

            public class Patch
            {
                public Patch() { data = new List<Byte>(); }

                public int address; // 24b file address
                public List<Byte> data;
            }

            public List<Patch> Items;

            public void Parse(string fileName)
            {
                int index = 0;

                FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                // make sure the first few characters match string
                byte[] buffer = new byte[512];

                System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
                fs.Read(buffer, 0, 5);
                for (int i = 0; i < 5; i++)
                {
                    if (buffer[i] != enc.GetBytes("PATCH")[i])
                        throw new Exception("IPS: error parsing PATCH");
                }
                index += 5;

                bool foundEOF = false;
                while (!foundEOF)
                {
                    int bytesRead = 0;

                    // read address
                    bytesRead = fs.Read(buffer, 0, 3);
                    // check EOF
                    if (index == fs.Length - 3 || index == fs.Length - 6)
                    {
                        foundEOF = true;
                        // check for EOF
                        for (int i = 0; i < 3; i++)
                        {
                            if (buffer[i] != enc.GetBytes("EOF")[i])
                            {
                                foundEOF = false;
                                break;
                            }
                        }
                    }

                    if (!foundEOF)
                    {
                        Patch patch = new Patch();
                        Items.Add(patch);

                        // get address
                        if (bytesRead != 3) throw new Exception("IPS: error parsing address");
                        patch.address = buffer[0]; patch.address <<= 8;
                        patch.address |= buffer[1]; patch.address <<= 8;
                        patch.address |= buffer[2]; patch.address <<= 0;
                        index += bytesRead;

                        // get length
                        bytesRead = fs.Read(buffer, 0, 2);
                        if (bytesRead != 2) throw new Exception("IPS: error parsing length");
                        int length = buffer[0]; length <<= 8;
                        length |= buffer[1]; length <<= 0;
                        index += bytesRead;

                        // check if RLE
                        if (length == 0)
                        {
                            // RLE
                            bytesRead = fs.Read(buffer, 0, 3);
                            if (bytesRead != 3) throw new Exception("IPS: error parsing RLE count/byte");
                            int count = buffer[0]; count <<= 8;
                            count |= buffer[1]; count <<= 0;
                            Byte val = buffer[2];
                            index += bytesRead;

                            patch.data.AddRange(Enumerable.Repeat(val, count));
                        }
                        else
                        {
                            int count = 0;
                            while (count < length)
                            {
                                bytesRead = fs.Read(buffer, 0, Math.Min(buffer.Length, length - count));
                                if (bytesRead == 0) throw new Exception("IPS: error parsing data");
                                count += bytesRead;
                                index += bytesRead;
                                patch.data.AddRange(buffer.Take(bytesRead));
                            }
                        }
                    }
                }

                // ignore truncation
                if (index != fs.Length && index != fs.Length - 3)
                    throw new Exception("IPS: unexpected end of file");

                fs.Close();
            }
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
                toolStripStatusLabel1.Text = x.Message.ToString();
                connected = false;
                EnableButtons(false);
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
                }
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                connected = false;
                EnableButtons(false);
            }

        }

        //ResponseType GetResponse()
        //{
        //    ResponseType rsp = new ResponseType();
        //    byte[] receiveBuffer = new byte[Constants.MaxMessageSize];
        //    JavaScriptSerializer serializer = new JavaScriptSerializer();

        //    var reqTask = _ws.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
        //    if (!reqTask.Wait(3000)) return rsp;
        //    var result = reqTask.Result;

        //    if (result.MessageType == WebSocketMessageType.Close)
        //    {
        //        if (!_ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
        //    }
        //    else if (result.MessageType == WebSocketMessageType.Text)
        //    {
        //        int count = result.Count;

        //        var messageString = Encoding.UTF8.GetString(receiveBuffer, 0, count);
        //        //rsp = new ResponsePacketType();
        //        rsp = serializer.Deserialize<ResponseType>(messageString);

        //        while (result.EndOfMessage == false)
        //        {
        //            if (count > Constants.MaxMessageSize)
        //            {
        //                string closeMessage = string.Format("Maximum message size: {0} bytes.", Constants.MaxMessageSize);
        //                if (!_ws.CloseAsync(WebSocketCloseStatus.MessageTooBig, closeMessage, CancellationToken.None).Wait(3000)) throw new Exception("socket timeout");
        //                return rsp;
        //            }

        //            count = 0;
        //            var rspTask = _ws.ReceiveAsync(new ArraySegment<Byte>(receiveBuffer, count, Constants.MaxMessageSize - count), CancellationToken.None);
        //            if (!rspTask.Wait(3000)) throw new Exception("socket timeout");
        //            result = rspTask.Result;
        //            count += result.Count;

        //            messageString = Encoding.UTF8.GetString(receiveBuffer, 0, count);
        //            var r = serializer.Deserialize<ResponseType>(messageString);
        //            rsp.Results.AddRange(r.Results);
        //        }

        //    }

        //    return rsp;
        //}

        //Tuple<bool, Byte[]> GetData()
        //{
        //    byte[] receiveBuffer = new byte[Constants.MaxMessageSize];

        //    var t = _ws.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
        //    if (!t.Wait(3000)) throw new Exception("socket timeout");
        //    var result = t.Result;

        //    if (result.MessageType != WebSocketMessageType.Binary) throw new Exception("GetData: unexpected amount of data");

        //    Array.Resize(ref receiveBuffer, result.Count);

        //    return Tuple.Create(result.EndOfMessage, receiveBuffer);
        //}

        void SetClient()
        {
            _evClient.Set();
        }

        void WaitClient()
        {
            _evClient.WaitOne();
            _evClient.Reset();
        }

        void SetSocket()
        {
            _ev.Set();
        }

        void WaitSocket()
        {
            _ev.WaitOne();
            _ev.Reset();
        }

    }
}
