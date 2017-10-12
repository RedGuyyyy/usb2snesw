using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Threading;

using usb2snes.utils;
using usb2snes.Properties;

namespace usb2snes
{
    public class ClientManager
    {

        private static readonly int MaxTooltipLength = 63; // framework constraint

        private Dictionary<string, IEnumerable<ClientGroup>> projectDict = new Dictionary<string, IEnumerable<ClientGroup>>();
        private readonly NotifyIcon notifyIcon;

        private Dictionary<Process, string> children = new Dictionary<Process, string>();

        public ClientManager(NotifyIcon notifyIcon)
        {
            this.notifyIcon = notifyIcon;
        }

        public bool IsDecorated { get; private set; }

        # region context menu creation


        public void BuildContextMenu(ContextMenuStrip contextMenuStrip)
        {
            //contextMenuStrip.Items.Clear();
            contextMenuStrip.Items.AddRange(
                projectDict.Keys.OrderBy(project => project).Select(project => BuildSubMenu(project)).ToArray());
        }

        private ToolStripMenuItem BuildSubMenu(string project)
        {
            var menuItem = new ToolStripMenuItem(project);
            menuItem.DropDownItems.AddRange(
                projectDict[project]
                    .OrderBy(serverGroup => serverGroup.Name)
                    .Select(serverGroup => ToolStripMenuItemWithHandler(
                        serverGroup.Name, serverGroup.ProcessRef, serverGroup.EnabledCount, serverGroup.DisabledCount, serverGroupItem_Click))
                    .ToArray());
            return menuItem;
        }

        # endregion context menu creation

        # region hosts file analysis

        //private static readonly string HostsCommentMarker = "#";
        //private static readonly string FilteringPattern = HostsCommentMarker + @"\s*\[([^/]+)/([^]]+)\]";
        //private static readonly Regex FilteringRegex = new Regex(FilteringPattern);
        // Each host line must have this suffix format to be considered:
        //  #  [ ProjectName / ServerGroupName ]
        // This regex has 2 subgroups capturing this information:
        //private static readonly int ProjectSubGroupIndex = 1;
        //private static readonly int ServerGroupSubGroupIndex = 2;
        
        private class ClientGroup
        {
            public string Name { get; set; }
            public Process ProcessRef { get; set; }
            public int EnabledCount { get; set; }
            public int DisabledCount { get; set; }
            public string Path { get; set; }
            //public ServerGroup(string name) { Name = name; }
        }

        /// <summary>
        /// Builds the snes and client connections.
        /// </summary>
        public void BuildClientAssociations()
        {
            //hostFileData = core.GetDeviceList();
            CreateMap();
            //SetNotifyIconToolTip();
        }


        private void CreateMap()
        {
            projectDict.Clear();

            List<ClientGroup> l;

            l = new List<ClientGroup>();
            foreach (var c in children)
            {
                l.Add(new ClientGroup { Name = c.Value, ProcessRef = c.Key, EnabledCount = 0, DisabledCount = 0 });
            }
            if (l.Count != 0) projectDict.Add("active clients", l);

            l = new List<ClientGroup>();
            foreach (var p in new string[] { "FileViewer", "MemoryViewer"})
            {
                l.Add(new ClientGroup { Name = p, ProcessRef = null, EnabledCount = 0, DisabledCount = 0 });
            }
            foreach (var p in Settings.Default.RegisteredAppList)
            {
                l.Add(new ClientGroup { Name = Path.GetFileName(p), Path = p, ProcessRef = null, EnabledCount = 0, DisabledCount = 0 });
            }
            l.Add(new ClientGroup { Name = "<Add Application>", ProcessRef = null, EnabledCount = 0, DisabledCount = 0 });
            projectDict.Add("clients", l);
        }

        # endregion hosts file analysis

        # region details form support

        //private static readonly string ServerPattern =
            //"(" + HostsCommentMarker + @"?)\s*((?:\d+\.){3}\d+)\s+(\S+)\s+" + HostsCommentMarker + @"\s*\[([^/]+)/([^]]+)\]";
        //private static readonly Regex ServerRegex = new Regex(ServerPattern);
        // Each host line must have this format to be considered, where the initial comment marker is optional:
        //  #  IpAddress   HostName  #  [ ProjectName / ServerGroupName ]
        // This regex has 5 subgroups capturing this information:
        //private static readonly int StatusIndex = 1; // presence or absence of a comment marker
        //private static readonly int IpAddressIndex = 2;
        //private static readonly int HostNameIndex = 3;
        //private static readonly int ProjectNameIndex = 4;
        //private static readonly int ServerGroupNameIndex = 5;

        private class Server
        {   // NB: the order here determines the order of the table in the details view.
            public string IpAddress { get; set; }
            public string HostName { get; set; }
            public string ProjectName { get; set; }
            public string ServerGroupName { get; set; }
            public string Status { get; set; }
        }

        public static readonly string EnabledLabel = "enabled";
        public static readonly string DisabledLabel = "disabled";
        public static readonly int EnabledColumnNumber = 4;
        //private static readonly int BalloonTimeout = 3000; // preferred timeout (msecs) though .NET enforces 10-sec minimum

        public bool IsEnabled(string cellValue)
        {
            return EnabledLabel.Equals(cellValue);
        }

        public void GenerateHostsDetails(DataGridView dgv)
        {
            //var servers = 
            //    hostFileData
            //    .Select(line => ServerRegex.Match(line))
            //    .Where(match => match.Success)
            //    .Select(match => new Server
            //    {
            //        Status = match.Groups[StatusIndex].ToString().Trim().Equals(HostsCommentMarker) ? DisabledLabel : EnabledLabel,
            //        HostName = match.Groups[HostNameIndex].ToString().Trim(),
            //        IpAddress = match.Groups[IpAddressIndex].ToString().Trim(),
            //        ProjectName = match.Groups[ProjectNameIndex].ToString().Trim(),
            //        ServerGroupName = match.Groups[ServerGroupNameIndex].ToString().Trim()
            //    });

            //dgv.DataSource = new BindingSource { DataSource = new SortableBindingList<Server>(servers.ToList()) };
        }

        #endregion details form support

        #region event handlers

        [System.Runtime.InteropServices.DllImport("User32.dll")]
        private static extern bool SetForegroundWindow(IntPtr handle);
        [System.Runtime.InteropServices.DllImport("User32.dll")]
        private static extern bool ShowWindow(IntPtr handle, int nCmdShow);
        [System.Runtime.InteropServices.DllImport("User32.dll")]
        private static extern bool IsIconic(IntPtr handle);

        private int instance = 0;

        private void serverGroupItem_Click(object sender, MouseEventArgs e)
        {
            ToolStripMenuItem itemClicked = ((ToolStripMenuItem)sender);
            var targetServerGroup = itemClicked.Text;
            var targetProject = itemClicked.OwnerItem.Text;
            //var me = (MouseEventArgs)e;

            // show application

            if (targetProject == "clients")
            {
                if (targetServerGroup == "FileViewer")
                {
                    var p = Process.Start("usb2snesfileviewer");
                    children.Add(p, instance.ToString() + ": " + targetServerGroup);
                    instance++;
                    p.EnableRaisingEvents = true;
                    p.Exited += Child_FormClosing;
                    //var child = new WindowsFormsApplication1.usb2snes();
                    //children.Add(child, targetServerGroup);
                    //child.FormClosing += Child_FormClosing;
                    //child.Show();
                }
                else if (targetServerGroup == "MemoryViewer")
                {
                    var p = Process.Start("usb2snesmemoryviewer");
                    children.Add(p, instance.ToString() + ": " + targetServerGroup);
                    instance++;
                    p.EnableRaisingEvents = true;
                    p.Exited += Child_FormClosing;
                    //var child = new WindowsFormsApplication1.usb2snesviewer();
                    //children.Add(child, targetServerGroup);
                    //child.FormClosing += Child_FormClosing;
                    //child.Show();
                }
                else if (targetServerGroup == "<Add Application>")
                {
                    System.Windows.Forms.OpenFileDialog dlg = new OpenFileDialog();
                    dlg.Title = "Executable to Register";
                    dlg.Filter = "Exe File|*.exe"
                                 + "|All Files|*.*";
                    dlg.FileName = "";

                    if (dlg.ShowDialog() != DialogResult.Cancel)
                    {
                        for (int i = 0; i < dlg.FileNames.Length; i++)
                        {
                            string fileName = dlg.FileNames[i];
                            //string safeFileName = dlg.SafeFileNames[i];

                            Settings.Default.RegisteredAppList.Add(fileName);
                        }
                    }
                }
                else
                {
                    foreach (var client in projectDict["clients"])
                    {
                        if (targetServerGroup == client.Name)
                        {
                            if (e.Button == MouseButtons.Right)
                            {
                                var l = projectDict["clients"].ToList();
                                l.Remove(client);
                                projectDict["clients"] = l;
                                Settings.Default.RegisteredAppList.Remove(client.Path);
                            }
                            else
                            {
                                var p = new Process();
                                p.StartInfo.FileName = client.Path;
                                p.StartInfo.WorkingDirectory = Path.GetDirectoryName(client.Path);
                                p.StartInfo.UseShellExecute = false;
                                p.StartInfo.CreateNoWindow = false;
                                children.Add(p, instance.ToString() + ": " + targetServerGroup);
                                instance++;
                                p.EnableRaisingEvents = true;
                                p.Exited += Child_FormClosing;
                                p.Start();
                            }
                            break;
                        }
                    }
                }
            }
            else if (targetProject == "active clients")
            {
                // bring form to the top
                Process p = (Process)itemClicked.Tag;
                if (!p.HasExited)
                {
                    var h = p.MainWindowHandle;
                    if (IsIconic(h)) ShowWindow(h, 9);
                    SetForegroundWindow(h);
                }
                else
                {
                    children.Remove(p);
                }
            }

            CreateMap(); // regen the map to reflect this update (successful or not) for tooltip processing
            //SetNotifyIconToolTip();
        }

        private void Child_FormClosing(object sender, EventArgs e)
        {
            Process formRef = (Process)sender;

            children.Remove(formRef);
        }

        # endregion event handlers

        # region support methods

        private ToolStripMenuItem ToolStripMenuItemWithHandler(
            string displayText, Process process, int enabledCount, int disabledCount, MouseEventHandler eventHandler)
        {
            var item = new ToolStripMenuItem(displayText);
            if (eventHandler != null) { item.MouseDown += eventHandler; }

            item.Image = null;
            //(enabledCount > 0 && disabledCount > 0) ? Properties.Resources.signal_yellow
            //: (enabledCount > 0) ? Properties.Resources.signal_green
            //: (disabledCount > 0) ? Properties.Resources.signal_red
            //: null;
            item.ToolTipText = "";
            item.Tag = process;
                         //(enabledCount > 0 && disabledCount > 0) ?
                         //                        string.Format("{0} {0} unknown", enabledCount, disabledCount)
                         //: (enabledCount > 0) ? string.Format("{0} connected", enabledCount)
                         //: (disabledCount > 0) ? string.Format("{0} disabled", disabledCount)
                         //: "";
            return item;
        }

        public ToolStripMenuItem ToolStripMenuItemWithHandler(string displayText, MouseEventHandler eventHandler)
        {
            return ToolStripMenuItemWithHandler(displayText, null, 0, 0, eventHandler);
        }

        # endregion support methods

    }
}
