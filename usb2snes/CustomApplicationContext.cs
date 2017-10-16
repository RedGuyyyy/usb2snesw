using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Reflection;
using System.Net.WebSockets;

using WindowsFormsApplication1;
using System.Collections.Generic;

using usb2snes.Properties;

/*
 * ==============================================================
 * @ID       $Id: MainForm.cs 971 2010-09-30 16:09:30Z ww $
 * @created  2008-07-31
 * ==============================================================
 *
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

    /// <summary>
    /// Framework for running application as a tray app.
    /// </summary>
    /// <remarks>
    /// Tray app code adapted from "Creating Applications with NotifyIcon in Windows Forms", Jessica Fosler,
    /// http://windowsclient.net/articles/notifyiconapplications.aspx
    /// </remarks>
    public class CustomApplicationContext : ApplicationContext
    {
        // Icon graphic from http://prothemedesign.com/circular-icons/
        private static readonly string IconFileName = "route.ico";
        private static readonly string DefaultTooltip = "Route HOST entries via context menu";
        private readonly HostManager hostManager;
        private readonly ClientManager clientManager;

        private readonly Server server;

        /// <summary>
		/// This class should be created and passed into Application.Run( ... )
		/// </summary>
		public CustomApplicationContext() 
		{
			InitializeContext();
            hostManager = new HostManager(notifyIcon);
            hostManager.BuildServerAssociations();
            //if (!hostManager.IsDecorated) { ShowIntroForm(); }
            clientManager = new ClientManager(notifyIcon);
            clientManager.BuildClientAssociations();

            server = new Server();
		}

        private void ContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = false;
            hostManager.BuildServerAssociations();
            hostManager.BuildContextMenu(notifyIcon.ContextMenuStrip);
            notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            clientManager.BuildClientAssociations();
            clientManager.BuildContextMenu(notifyIcon.ContextMenuStrip);
            notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            //notifyIcon.ContextMenuStrip.Items.Add(hostManager.ToolStripMenuItemWithHandler("&FileViewer", fileViewer_Click));
            //notifyIcon.ContextMenuStrip.Items.Add(hostManager.ToolStripMenuItemWithHandler("&MemoryViewer", memoryViewer_Click));
            //notifyIcon.ContextMenuStrip.Items.Add(hostManager.ToolStripMenuItemWithHandler("&SnesViewer", snesViewer_Click));
            //notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            //notifyIcon.ContextMenuStrip.Items.Add(hostManager.ToolStripMenuItemWithHandler("Show &Details", showDetailsItem_Click));
            notifyIcon.ContextMenuStrip.Items.Add(hostManager.ToolStripMenuItemWithHandler("&Help/About", showHelpItem_Click));
            notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            notifyIcon.ContextMenuStrip.Items.Add(hostManager.ToolStripMenuItemWithHandler("&Exit", exitItem_Click));
        }

        # region the child forms

        private DetailsForm detailsForm;
        private HelpForm helpForm;

        private void ShowIntroForm()
        {
            System.Windows.Forms.MessageBox.Show("Make sure your snes is booted and the sd2snes is connected to USB.  All available connections will be listed as sd2snes->COM#.");
            //if (helpForm == null)
            //{
            //    helpForm = new HelpForm { };
            //    helpForm.Closed += helpForm_Closed; // avoid reshowing a disposed form
            //    helpForm.Show();
            //}
            //else { helpForm.Activate(); }
        }

        private void ShowDetailsForm()
        {
            if (detailsForm == null)
            {
                detailsForm = new DetailsForm {HostManager = hostManager};
                detailsForm.Closed += detailsForm_Closed; // avoid reshowing a disposed form
                detailsForm.Show();
            }
            else { detailsForm.Activate(); }
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e) { ShowIntroForm();    }

        // From http://stackoverflow.com/questions/2208690/invoke-notifyicons-context-menu
        private void notifyIcon_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                mi.Invoke(notifyIcon, null);
            }
        }


        // attach to context menu items
        private void showHelpItem_Click(object sender, EventArgs e)     { ShowIntroForm();    }
        private void showDetailsItem_Click(object sender, EventArgs e)  { ShowDetailsForm();  }

        // null out the forms so we know to create a new one.
        private void detailsForm_Closed(object sender, EventArgs e)     { detailsForm = null; }
        private void helpForm_Closed(object sender, EventArgs e)        { helpForm = null;    }

        # endregion the child forms

        # region generic code framework

        private System.ComponentModel.IContainer components;	// a list of components to dispose when the context is disposed
        private NotifyIcon notifyIcon;				            // the icon that sits in the system tray

        private void InitializeContext()
        {
            components = new System.ComponentModel.Container();
            notifyIcon = new NotifyIcon(components)
                             {
                                 ContextMenuStrip = new ContextMenuStrip(),
                                 Icon = new Icon(IconFileName),
                                 Text = DefaultTooltip,
                                 Visible = true
                             };
            notifyIcon.ContextMenuStrip.Opening += ContextMenuStrip_Opening;
            notifyIcon.DoubleClick += notifyIcon_DoubleClick;
            notifyIcon.MouseUp += notifyIcon_MouseUp;
        }

        /// <summary>
		/// When the application context is disposed, dispose things like the notify icon.
		/// </summary>
		/// <param name="disposing"></param>
		protected override void Dispose( bool disposing )
		{
			if( disposing && components != null) { components.Dispose(); }
		}

		/// <summary>
		/// When the exit menu item is clicked, make a call to terminate the ApplicationContext.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
        private void exitItem_Click(object sender, EventArgs e) 
		{
			ExitThread();
		}

        /// <summary>
        /// If we are presently showing a form, clean it up.
        /// </summary>
        protected override void ExitThreadCore()
        {
            // before we exit, let forms clean themselves up.
            //if (introForm != null) { introForm.Close(); }
            if (detailsForm != null) { detailsForm.Close(); }

            clientManager.Close();
            server.Stop();

            notifyIcon.Visible = false; // should remove lingering tray icon
            notifyIcon.Icon = null;
            base.ExitThreadCore();

            // save settings
            Settings.Default.Save();
        }

        # endregion generic code framework

    }
}
