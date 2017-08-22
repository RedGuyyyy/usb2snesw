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
using usb2snes.core;
using usb2snes.utils;
using usb2snes.Properties;

namespace WindowsFormsApplication1
{
    public partial class usb2snes : Form
    {

        public usb2snes()
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

                    core.Connect(((core.Port)comboBoxPort.SelectedItem).Name);
                    var list = (List<Tuple<int, string>>)core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_LS, core.usbint_server_space_e.USBINT_SERVER_SPACE_FILE, core.usbint_server_flags_e.USBINT_SERVER_FLAGS_NONE, remoteDir);
                    core.Disconnect();

                    foreach (var entry in list)
                    {
                        ListViewItem item = new ListViewItem(entry.Item2, entry.Item1);
                        ListViewItem.ListViewSubItem[] subItems = new ListViewItem.ListViewSubItem[] { new ListViewItem.ListViewSubItem(item, entry.Item1 == 0 ? "Directory" : "File"), new ListViewItem.ListViewSubItem(item, "") };
                        item.SubItems.AddRange(subItems);
                        listViewRemote.Items.Add(item);
                    }

                    connected = true;
                    EnableButtons(true);
                }
                catch (Exception x)
                {
                    toolStripStatusLabel1.Text = x.Message.ToString();
                    core.Disconnect();
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
                var deviceList = Win32DeviceMgmt.GetAllCOMPorts();
                foreach (var port in core.GetDeviceList())
                {
                    comboBoxPort.Items.Add(port);
                }

                if (comboBoxPort.Items.Count != 0) comboBoxPort.SelectedIndex = 0;
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                core.Disconnect();
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
                var port = (core.Port)comboBoxPort.SelectedItem;

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
                        core.Connect(((core.Port)comboBoxPort.SelectedItem).Name);

                        foreach (ListViewItem item in listViewLocal.SelectedItems)
                        {
                            if (item.ImageIndex == 0) continue;

                            string fileName = localDir + @"\" + item.Text;
                            string safeFileName = item.Text;

                            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);

                            core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_PUT, core.usbint_server_space_e.USBINT_SERVER_SPACE_FILE, core.usbint_server_flags_e.USBINT_SERVER_FLAGS_NONE, remoteDir + "/" + safeFileName, (uint)fs.Length);

                            // write data
                            byte[] tBuffer = new byte[512];
                            Array.Clear(tBuffer, 0, tBuffer.Length);
                            int curSize = 0;
                            toolStripProgressBar1.Value = 0;
                            toolStripProgressBar1.Enabled = true;
                            toolStripStatusLabel1.Text = "uploading: " + safeFileName;
                            while (curSize < fs.Length)
                            {
                                int bytesToWrite = fs.Read(tBuffer, 0, 512);
                                core.SendData(tBuffer, bytesToWrite);
                                curSize += bytesToWrite;
                                toolStripProgressBar1.Value = 100 * curSize / (int)fs.Length;
                            }
                            toolStripStatusLabel1.Text = "idle";
                            toolStripProgressBar1.Enabled = false;

                            fs.Close();
                        }

                        core.Disconnect();

                        RefreshListViewRemote();
                    }
                }
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                core.Disconnect();
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
                    core.Connect(((core.Port)comboBoxPort.SelectedItem).Name);

                    foreach (ListViewItem item in listViewRemote.SelectedItems)
                    {
                        if (item.ImageIndex == 1)
                        {
                            string name = remoteDir + '/' + item.Text;
                            if (name.Length < 256)
                            {
                                FileStream fs = new FileStream(localDir + @"\" + item.Text, FileMode.Create, FileAccess.Write);

                                int fileSize = (int)core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_GET, core.usbint_server_space_e.USBINT_SERVER_SPACE_FILE, core.usbint_server_flags_e.USBINT_SERVER_FLAGS_NONE, name);

                                // read data
                                byte[] tBuffer = new byte[512];
                                int curSize = 0;
                                Array.Clear(tBuffer, 0, tBuffer.Length);
                                toolStripProgressBar1.Value = 0;
                                toolStripProgressBar1.Enabled = true;
                                toolStripStatusLabel1.Text = "downloading: " + name;
                                while (curSize < fileSize)
                                {
                                    int prevSize = curSize;
                                    //curSize += serialPort1.Read(tBuffer, (curSize % 512), 512 - (curSize % 512));
                                    curSize += core.GetData(tBuffer, (curSize % 512), 512 - (curSize % 512));
                                    fs.Write(tBuffer, (prevSize % 512), curSize - prevSize);
                                    toolStripProgressBar1.Value = 100 * curSize / fileSize;
                                }
                                toolStripStatusLabel1.Text = "idle";
                                toolStripProgressBar1.Enabled = false;

                                fs.Close();
                            }
                        }
                    }

                    core.Disconnect();

                    RefreshListViewRemote();
                    RefreshListViewLocal();
                }
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                core.Disconnect();
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
                    core.Connect(((core.Port)comboBoxPort.SelectedItem).Name);

                    foreach (ListViewItem item in listViewRemote.SelectedItems)
                    {
                        var ext = Path.GetExtension(item.Text);
                        if (item.ImageIndex == 1 && (ext.ToLower().Contains("sfc") | ext.ToLower().Contains("smc") | ext.ToLower().Contains("fig")))
                        {
                            string name = remoteDir + '/' + item.Text;
                            if (name.Length < 256)
                            {
                                core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_BOOT, core.usbint_server_space_e.USBINT_SERVER_SPACE_FILE, (core.usbint_server_flags_e)bootFlags, name);
                                break; // only boot the first file
                            }
                        }
                    }

                    core.Disconnect();
                }
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                core.Disconnect();
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
                            core.Connect(((core.Port)comboBoxPort.SelectedItem).Name);
                            core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_MKDIR, core.usbint_server_space_e.USBINT_SERVER_SPACE_FILE, core.usbint_server_flags_e.USBINT_SERVER_FLAGS_NONE, name);
                            core.Disconnect();

                            RefreshListViewRemote();
                        }
                    }
                }
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                core.Disconnect();
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
                            core.Connect(((core.Port)comboBoxPort.SelectedItem).Name);
                            core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_MV, core.usbint_server_space_e.USBINT_SERVER_SPACE_FILE, core.usbint_server_flags_e.USBINT_SERVER_FLAGS_NONE, name, newName);
                            core.Disconnect();
                        }
                    }

                    RefreshListViewRemote();
                }
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                core.Disconnect();
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
                            core.Connect(((core.Port)comboBoxPort.SelectedItem).Name);

                            foreach (ListViewItem item in listViewRemote.SelectedItems)
                            {
                                string name = remoteDir + '/' + item.Text;
                                if (name.Length < 256)
                                {
                                    core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_RM, core.usbint_server_space_e.USBINT_SERVER_SPACE_FILE, core.usbint_server_flags_e.USBINT_SERVER_FLAGS_NONE, name);
                                }
                            }

                            core.Disconnect();

                            RefreshListViewRemote();
                        }
                    }
                }
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                core.Disconnect();
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

                    if (openFileDialog1.ShowDialog() != DialogResult.Cancel)
                    {
                        core.Connect(((core.Port)comboBoxPort.SelectedItem).Name);

                        for (int i = 0; i < openFileDialog1.FileNames.Length; i++)
                        {
                            string fileName = openFileDialog1.FileNames[i];
                            string safeFileName = openFileDialog1.SafeFileNames[i];

                            applyPatch(fileName, safeFileName);
                        }

                        core.Disconnect();
                    }
                }
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                core.Disconnect();
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
                    saveFileDialog1.FileName = "save.ss0";

                    if (saveFileDialog1.ShowDialog() != DialogResult.Cancel)
                    {
                        FileStream fs = new FileStream(saveFileDialog1.FileName, FileMode.Create, FileAccess.Write);

                        core.Connect(((core.Port)comboBoxPort.SelectedItem).Name);
                        int fileSize = (int)core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_GET, core.usbint_server_space_e.USBINT_SERVER_SPACE_SNES, core.usbint_server_flags_e.USBINT_SERVER_FLAGS_NONE, (uint)0xF00000, (uint)0x50000);

                        // read data
                        byte[] tBuffer = new byte[512];
                        int curSize = 0;
                        Array.Clear(tBuffer, 0, tBuffer.Length);
                        toolStripProgressBar1.Value = 0;
                        toolStripProgressBar1.Enabled = true;
                        toolStripStatusLabel1.Text = "downloading: " + saveFileDialog1.FileName;
                        while (curSize < fileSize)
                        {
                            int prevSize = curSize;
                            //curSize += serialPort1.Read(tBuffer, (curSize % 512), 512 - (curSize % 512));
                            curSize += core.GetData(tBuffer, (curSize % 512), 512 - (curSize % 512));
                            fs.Write(tBuffer, (prevSize % 512), curSize - prevSize);
                            toolStripProgressBar1.Value = 100 * curSize / fileSize;
                        }
                        toolStripStatusLabel1.Text = "idle";
                        toolStripProgressBar1.Enabled = false;

                        fs.Close();

                        core.Disconnect();

                        RefreshListViewRemote();
                        RefreshListViewLocal();
                    }
                }
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                core.Disconnect();
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

                    if (openFileDialog1.ShowDialog() != DialogResult.Cancel)
                    {
                        core.Connect(((core.Port)comboBoxPort.SelectedItem).Name);

                        for (int i = 0; i < openFileDialog1.FileNames.Length; i++)
                        {
                            string fileName = openFileDialog1.FileNames[i];
                            string safeFileName = openFileDialog1.SafeFileNames[i];

                            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                            core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_PUT, core.usbint_server_space_e.USBINT_SERVER_SPACE_SNES, core.usbint_server_flags_e.USBINT_SERVER_FLAGS_NONE, (uint)0xF00000, (uint)fs.Length);

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
                                core.SendData(tBuffer, bytesToWrite);
                                //serialPort1.Write(tBuffer, 0, bytesToWrite);
                                curSize += bytesToWrite;
                                toolStripProgressBar1.Value = 100 * curSize / (int)fs.Length;
                            }
                            toolStripStatusLabel1.Text = "idle";
                            toolStripProgressBar1.Enabled = false;

                            fs.Close();
                        }

                        core.Disconnect();

                        RefreshListViewRemote();
                    }
                }
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                core.Disconnect();
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
                    openFileDialog1.Title = "RAM IPS file to load";
                    openFileDialog1.Filter = "IPS File|*.ips"
                                           + "|All Files|*.*";
                    openFileDialog1.FileName = "";

                    if (openFileDialog1.ShowDialog() != DialogResult.Cancel)
                    {
                        // boot currently selected ROM
                        bootFlags = Convert.ToByte(core.usbint_server_flags_e.USBINT_SERVER_FLAGS_SKIPRESET);
                        buttonBoot.PerformClick();
                        bootFlags = 0;

                        // apply the selected patch
                        core.Connect(((core.Port)comboBoxPort.SelectedItem).Name);

                        for (int i = 0; i < openFileDialog1.FileNames.Length; i++)
                        {
                            string fileName = openFileDialog1.FileNames[i];
                            string safeFileName = openFileDialog1.SafeFileNames[i];

                            applyPatch(fileName, safeFileName);
                        }

                        core.Disconnect();

                        System.Threading.Thread.Sleep(500);

                        // perform reset
                        bootFlags = Convert.ToByte(core.usbint_server_flags_e.USBINT_SERVER_FLAGS_ONLYRESET);
                        buttonBoot.PerformClick();
                        bootFlags = 0;
                    }
                }
            }
            catch (Exception x)
            {
                toolStripStatusLabel1.Text = x.Message.ToString();
                core.Disconnect();
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

                byte[] tBuffer = new byte[512];
                int curSize = 0;

                // send write command
                int patchNum = 0;
                foreach (var patch in ips.Items)
                {
                    core.SendCommand(core.usbint_server_opcode_e.USBINT_SERVER_OPCODE_PUT, core.usbint_server_space_e.USBINT_SERVER_SPACE_SNES
                                    , (patchNum == 0 && i == 0) ? core.usbint_server_flags_e.USBINT_SERVER_FLAGS_CLRX
                                      : (patchNum == ips.Items.Count - 1 && i == openFileDialog1.FileNames.Length - 1) ? core.usbint_server_flags_e.USBINT_SERVER_FLAGS_SETX
                                      : core.usbint_server_flags_e.USBINT_SERVER_FLAGS_NONE
                                    , (uint)patch.address
                                    , (uint)patch.data.Count
                                    );

                    // write data
                    Array.Clear(tBuffer, 0, tBuffer.Length);
                    curSize = 0;
                    toolStripProgressBar1.Value = 0;
                    toolStripProgressBar1.Enabled = true;
                    toolStripStatusLabel1.Text = "uploading ram: " + safeFileName;

                    while (curSize < patch.data.Count)
                    {
                        int bytesToWrite = Math.Min(512, patch.data.Count - curSize);
                        Array.Clear(tBuffer, 0, tBuffer.Length);
                        Array.Copy(patch.data.ToArray(), curSize, tBuffer, 0, bytesToWrite);

                        core.SendData(tBuffer, bytesToWrite);

                        curSize += bytesToWrite;
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
                makeDirToolStripMenuItem.Enabled = false;
                deleteToolStripMenuItem.Enabled = false;
                renameToolStripMenuItem.Enabled = false;

                if (connected)
                {
                    var info = listViewRemote.HitTest(e.X, e.Y);

                    refreshToolStripMenuItem.Enabled = true;
                    makeDirToolStripMenuItem.Enabled = true;

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

                public int        address; // 24b file address
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
    }
}
