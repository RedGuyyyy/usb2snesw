using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using usb2snes.utils;

// The bridge is based on a tooltray app for reading the hosts file.  The original license and other details provided below.

/*
 * The official license for this file is shown next.
 * Unofficially, consider this e-postcardware as well:
 * if you find this module useful, let us know via e-mail, along with
 * where in the world you are and (if applicable) your website address.
 */

/* ***** BEGIN LICENSE BLOCK *****
 * Version: MIT License
 *
 * Copyright (c) 2010 Michael Sorens http://www.simple-talk.com/author/michael-sorens/
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 *
 * ***** END LICENSE BLOCK *****
 */

namespace usb2snes
{
    public class HostManager
    {

        private static readonly int MaxTooltipLength = 63; // framework constraint

        //private readonly HostFileManager hostFileManager = new HostFileManager();
        private Dictionary<string, IEnumerable<ServerGroup>> projectDict = new Dictionary<string, IEnumerable<ServerGroup>>();
        private List<core.Port> hostFileData;
        private readonly NotifyIcon notifyIcon;

        public HostManager(NotifyIcon notifyIcon)
        {
            this.notifyIcon = notifyIcon;
        }

        public bool IsDecorated { get; private set; }

        # region context menu creation



        public void BuildContextMenu(ContextMenuStrip contextMenuStrip)
        {
            contextMenuStrip.Items.Clear();
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
                        serverGroup.Name, serverGroup.EnabledCount, serverGroup.DisabledCount, serverGroupItem_Click))
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
        
        private class ServerGroup
        {
            public string Name { get; set; }
            public int EnabledCount { get; set; }
            public int DisabledCount { get; set; }
            //public ServerGroup(string name) { Name = name; }
        }

        /// <summary>
        /// Builds the snes and client connections.
        /// </summary>
        public void BuildServerAssociations()
        {
            hostFileData = core.GetDeviceList();
            CreateMap();
            SetNotifyIconToolTip();
        }


        private void CreateMap()
        {
            projectDict.Clear();
            List<ServerGroup> l = new List<ServerGroup>();
            foreach (var p in hostFileData)
            {
                l.Add(new ServerGroup { Name = p.Name, EnabledCount = 0, DisabledCount = 0 });
            }
            projectDict.Add("sd2snes", l);
        }

        private void SetNotifyIconToolTip()
        {
            string activeServerGroupsText =
                string.Join("\n",
                     projectDict.Keys
                         .Select(project => new
                                 {
                                     project,
                                     activeServerGroups = string.Join(", ",
								    	 projectDict[project]
                                             .Where(
                                                 serverGroup =>
                                                 serverGroup.EnabledCount > 0 &&
                                                 serverGroup.DisabledCount == 0)
                                             .Select(serverGroup => serverGroup.Name)
                                     )
                                 }
                         )
                         .Where(item => item.activeServerGroups.Length > 0)
                         .Select(item => item.project + ": " + item.activeServerGroups));

            IsDecorated = activeServerGroupsText.Length > 0;

            int activeLineCount = projectDict.Keys
                .SelectMany(p => projectDict[p], (project, serverGroup) => serverGroup.EnabledCount).Sum();

            string toolTipText = string.Format("{0}\n{1} of {2} lines",
                (IsDecorated ? activeServerGroupsText : "No sd2snes connected!"), activeLineCount, hostFileData.Count);

            notifyIcon.Text = toolTipText.Length >= MaxTooltipLength ?
                toolTipText.Substring(0, MaxTooltipLength - 3) + "..." : toolTipText;
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

        # endregion details form support
        
        # region event handlers

        private void serverGroupItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem itemClicked = ((ToolStripMenuItem)sender);
            var targetServerGroup = itemClicked.Text;
            var targetProject = itemClicked.OwnerItem.Text;
            bool found = false;

            /*
            foreach (var p in projectDict["sd2snes"])
            {
                if (p.EnabledCount > 0)
                {
                    found = (p.Name == targetServerGroup);
                    p.EnabledCount = 0;
                    p.EnabledCount = 1;
                    core.Disconnect();
                    break;
                }
            }

            if (core.Connected()) core.Disconnect();

            if (!found)
            {
                core.Connect(targetServerGroup);
                foreach (var p in projectDict["sd2snes"]) if (p.Name == itemClicked.Text) { p.EnabledCount = 1; p.DisabledCount = 0; }
            }
            */

            CreateMap(); // regen the map to reflect this update (successful or not) for tooltip processing
            SetNotifyIconToolTip();
        }

        # endregion event handlers

        # region support methods

        private ToolStripMenuItem ToolStripMenuItemWithHandler(
            string displayText, int enabledCount, int disabledCount, MouseEventHandler eventHandler)
        {
            var item = new ToolStripMenuItem(displayText);
            if (eventHandler != null) { item.MouseDown += eventHandler; }

            item.Image = (enabledCount > 0 && disabledCount > 0) ? Properties.Resources.signal_yellow
                         : (enabledCount > 0) ? Properties.Resources.signal_green
                         : (disabledCount > 0) ? Properties.Resources.signal_red
                         : null;
            item.ToolTipText = (enabledCount > 0 && disabledCount > 0) ?
                                                 string.Format("{0} {0} unknown", enabledCount, disabledCount)
                         : (enabledCount > 0) ? string.Format("{0} connected", enabledCount)
                         : (disabledCount > 0) ? string.Format("{0} disabled", disabledCount)
                         : "";
            return item;
        }

        public ToolStripMenuItem ToolStripMenuItemWithHandler(string displayText, MouseEventHandler eventHandler)
        {
            return ToolStripMenuItemWithHandler(displayText, 0, 0, eventHandler);
        }

        # endregion support methods

    }
}
