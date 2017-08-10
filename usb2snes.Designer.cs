namespace WindowsFormsApplication1
{
    partial class usb2snes
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void EnableButtons(bool enable)
        {
            buttonUpload.Enabled = enable;
            buttonDownload.Enabled = enable;
            buttonBoot.Enabled = enable;
            buttonMkdir.Enabled = enable;
            buttonDelete.Enabled = enable;
            buttonRename.Enabled = enable;
            buttonPatch.Enabled = enable;
            buttonGetState.Enabled = enable;
            buttonSetState.Enabled = enable;

            buttonTest.Enabled = enable;
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(usb2snes));
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripProgressBar1 = new System.Windows.Forms.ToolStripProgressBar();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.comboBoxPort = new System.Windows.Forms.ComboBox();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
            this.imageList = new System.Windows.Forms.ImageList(this.components);
            this.contextMenuStripRemote = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.refreshToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.backToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.forwardToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.makeDirToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.renameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.bootToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.listViewRemote = new System.Windows.Forms.ListView();
            this.buttonMkdir = new System.Windows.Forms.Button();
            this.buttonDelete = new System.Windows.Forms.Button();
            this.buttonRename = new System.Windows.Forms.Button();
            this.listViewLocal = new System.Windows.Forms.ListView();
            this.buttonBoot = new System.Windows.Forms.Button();
            this.contextMenuStripLocal = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.refreshToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.backToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.forwardToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.makeDirToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.renameToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.buttonUpload = new System.Windows.Forms.Button();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.buttonDownload = new System.Windows.Forms.Button();
            this.buttonPatch = new System.Windows.Forms.Button();
            this.buttonGetState = new System.Windows.Forms.Button();
            this.buttonSetState = new System.Windows.Forms.Button();
            this.buttonTest = new System.Windows.Forms.Button();
            this.statusStrip1.SuspendLayout();
            this.contextMenuStripRemote.SuspendLayout();
            this.contextMenuStripLocal.SuspendLayout();
            this.SuspendLayout();
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripProgressBar1,
            this.toolStripStatusLabel1});
            this.statusStrip1.Location = new System.Drawing.Point(0, 350);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(620, 22);
            this.statusStrip1.TabIndex = 2;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripProgressBar1
            // 
            this.toolStripProgressBar1.Enabled = false;
            this.toolStripProgressBar1.Name = "toolStripProgressBar1";
            this.toolStripProgressBar1.Size = new System.Drawing.Size(100, 16);
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(503, 17);
            this.toolStripStatusLabel1.Spring = true;
            this.toolStripStatusLabel1.Text = "idle";
            this.toolStripStatusLabel1.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // comboBoxPort
            // 
            this.comboBoxPort.FormattingEnabled = true;
            this.comboBoxPort.Location = new System.Drawing.Point(328, 14);
            this.comboBoxPort.Name = "comboBoxPort";
            this.comboBoxPort.Size = new System.Drawing.Size(253, 21);
            this.comboBoxPort.TabIndex = 3;
            this.comboBoxPort.SelectedIndexChanged += new System.EventHandler(this.comboBoxPort_SelectedIndexChanged);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            this.openFileDialog1.Multiselect = true;
            // 
            // imageList
            // 
            this.imageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList.ImageStream")));
            this.imageList.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList.Images.SetKeyName(0, "folderopened_yellow.png");
            this.imageList.Images.SetKeyName(1, "text_left.png");
            // 
            // contextMenuStripRemote
            // 
            this.contextMenuStripRemote.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.refreshToolStripMenuItem,
            this.backToolStripMenuItem,
            this.forwardToolStripMenuItem,
            this.toolStripSeparator3,
            this.makeDirToolStripMenuItem,
            this.renameToolStripMenuItem,
            this.deleteToolStripMenuItem,
            this.toolStripSeparator2,
            this.bootToolStripMenuItem});
            this.contextMenuStripRemote.Name = "contextMenuStrip1";
            this.contextMenuStripRemote.Size = new System.Drawing.Size(155, 170);
            this.contextMenuStripRemote.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStripRemote_Opening);
            // 
            // refreshToolStripMenuItem
            // 
            this.refreshToolStripMenuItem.Enabled = false;
            this.refreshToolStripMenuItem.Image = global::usb2snes.Properties.Resources.reload;
            this.refreshToolStripMenuItem.Name = "refreshToolStripMenuItem";
            this.refreshToolStripMenuItem.Size = new System.Drawing.Size(154, 22);
            this.refreshToolStripMenuItem.Text = "Refresh";
            this.refreshToolStripMenuItem.Click += new System.EventHandler(this.refreshToolStripMenuItem_Click);
            // 
            // backToolStripMenuItem
            // 
            this.backToolStripMenuItem.Enabled = false;
            this.backToolStripMenuItem.Image = global::usb2snes.Properties.Resources.gtk_goto_first_ltr;
            this.backToolStripMenuItem.Name = "backToolStripMenuItem";
            this.backToolStripMenuItem.Size = new System.Drawing.Size(154, 22);
            this.backToolStripMenuItem.Text = "Back";
            this.backToolStripMenuItem.Click += new System.EventHandler(this.backToolStripMenuItem_Click);
            // 
            // forwardToolStripMenuItem
            // 
            this.forwardToolStripMenuItem.Enabled = false;
            this.forwardToolStripMenuItem.Image = global::usb2snes.Properties.Resources.gtk_goto_last_ltr;
            this.forwardToolStripMenuItem.Name = "forwardToolStripMenuItem";
            this.forwardToolStripMenuItem.Size = new System.Drawing.Size(154, 22);
            this.forwardToolStripMenuItem.Text = "Forward";
            this.forwardToolStripMenuItem.Click += new System.EventHandler(this.forwardToolStripMenuItem_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(151, 6);
            // 
            // makeDirToolStripMenuItem
            // 
            this.makeDirToolStripMenuItem.Enabled = false;
            this.makeDirToolStripMenuItem.Name = "makeDirToolStripMenuItem";
            this.makeDirToolStripMenuItem.Size = new System.Drawing.Size(154, 22);
            this.makeDirToolStripMenuItem.Text = "Make Directory";
            this.makeDirToolStripMenuItem.Click += new System.EventHandler(this.makeDirToolStripMenuItem_Click);
            // 
            // renameToolStripMenuItem
            // 
            this.renameToolStripMenuItem.Enabled = false;
            this.renameToolStripMenuItem.Name = "renameToolStripMenuItem";
            this.renameToolStripMenuItem.Size = new System.Drawing.Size(154, 22);
            this.renameToolStripMenuItem.Text = "Rename";
            this.renameToolStripMenuItem.Click += new System.EventHandler(this.renameToolStripMenuItem_Click);
            // 
            // deleteToolStripMenuItem
            // 
            this.deleteToolStripMenuItem.Enabled = false;
            this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            this.deleteToolStripMenuItem.Size = new System.Drawing.Size(154, 22);
            this.deleteToolStripMenuItem.Text = "Delete";
            this.deleteToolStripMenuItem.Click += new System.EventHandler(this.deleteToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(151, 6);
            // 
            // bootToolStripMenuItem
            // 
            this.bootToolStripMenuItem.Enabled = false;
            this.bootToolStripMenuItem.Name = "bootToolStripMenuItem";
            this.bootToolStripMenuItem.Size = new System.Drawing.Size(154, 22);
            this.bootToolStripMenuItem.Text = "Boot";
            this.bootToolStripMenuItem.Click += new System.EventHandler(this.bootToolStripMenuItem_Click);
            // 
            // listViewRemote
            // 
            this.listViewRemote.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.listViewRemote.HideSelection = false;
            this.listViewRemote.LabelWrap = false;
            this.listViewRemote.Location = new System.Drawing.Point(328, 40);
            this.listViewRemote.Name = "listViewRemote";
            this.listViewRemote.Size = new System.Drawing.Size(280, 278);
            this.listViewRemote.SmallImageList = this.imageList;
            this.listViewRemote.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.listViewRemote.TabIndex = 6;
            this.listViewRemote.UseCompatibleStateImageBehavior = false;
            this.listViewRemote.View = System.Windows.Forms.View.List;
            this.listViewRemote.MouseClick += new System.Windows.Forms.MouseEventHandler(this.listViewRemote_MouseClick);
            this.listViewRemote.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.listViewRemote_MouseDoubleClick);
            // 
            // buttonMkdir
            // 
            this.buttonMkdir.Enabled = false;
            this.buttonMkdir.Location = new System.Drawing.Point(328, 324);
            this.buttonMkdir.Name = "buttonMkdir";
            this.buttonMkdir.Size = new System.Drawing.Size(50, 23);
            this.buttonMkdir.TabIndex = 7;
            this.buttonMkdir.Text = "MkDir";
            this.buttonMkdir.UseVisualStyleBackColor = true;
            this.buttonMkdir.Click += new System.EventHandler(this.buttonMkdir_Click);
            // 
            // buttonDelete
            // 
            this.buttonDelete.Enabled = false;
            this.buttonDelete.Location = new System.Drawing.Point(384, 324);
            this.buttonDelete.Name = "buttonDelete";
            this.buttonDelete.Size = new System.Drawing.Size(50, 23);
            this.buttonDelete.TabIndex = 8;
            this.buttonDelete.Text = "Delete";
            this.buttonDelete.UseVisualStyleBackColor = true;
            this.buttonDelete.Click += new System.EventHandler(this.buttonDelete_Click);
            // 
            // buttonRename
            // 
            this.buttonRename.Enabled = false;
            this.buttonRename.Location = new System.Drawing.Point(440, 324);
            this.buttonRename.Name = "buttonRename";
            this.buttonRename.Size = new System.Drawing.Size(56, 23);
            this.buttonRename.TabIndex = 9;
            this.buttonRename.Text = "Rename";
            this.buttonRename.UseVisualStyleBackColor = true;
            this.buttonRename.Click += new System.EventHandler(this.buttonRename_Click);
            // 
            // listViewLocal
            // 
            this.listViewLocal.LabelWrap = false;
            this.listViewLocal.Location = new System.Drawing.Point(12, 41);
            this.listViewLocal.Name = "listViewLocal";
            this.listViewLocal.Size = new System.Drawing.Size(277, 277);
            this.listViewLocal.SmallImageList = this.imageList;
            this.listViewLocal.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.listViewLocal.TabIndex = 10;
            this.listViewLocal.UseCompatibleStateImageBehavior = false;
            this.listViewLocal.View = System.Windows.Forms.View.List;
            this.listViewLocal.MouseClick += new System.Windows.Forms.MouseEventHandler(this.listViewLocal_MouseClick);
            this.listViewLocal.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.listViewLocal_MouseDoubleClick);
            // 
            // buttonBoot
            // 
            this.buttonBoot.Enabled = false;
            this.buttonBoot.Location = new System.Drawing.Point(502, 324);
            this.buttonBoot.Name = "buttonBoot";
            this.buttonBoot.Size = new System.Drawing.Size(48, 23);
            this.buttonBoot.TabIndex = 11;
            this.buttonBoot.Text = "Boot";
            this.buttonBoot.UseVisualStyleBackColor = true;
            this.buttonBoot.Click += new System.EventHandler(this.buttonBoot_Click);
            // 
            // contextMenuStripLocal
            // 
            this.contextMenuStripLocal.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.refreshToolStripMenuItem1,
            this.backToolStripMenuItem1,
            this.forwardToolStripMenuItem1,
            this.toolStripSeparator1,
            this.makeDirToolStripMenuItem1,
            this.renameToolStripMenuItem1});
            this.contextMenuStripLocal.Name = "contextMenuStrip1";
            this.contextMenuStripLocal.Size = new System.Drawing.Size(155, 120);
            // 
            // refreshToolStripMenuItem1
            // 
            this.refreshToolStripMenuItem1.Image = global::usb2snes.Properties.Resources.reload;
            this.refreshToolStripMenuItem1.Name = "refreshToolStripMenuItem1";
            this.refreshToolStripMenuItem1.Size = new System.Drawing.Size(154, 22);
            this.refreshToolStripMenuItem1.Text = "Refresh";
            this.refreshToolStripMenuItem1.Click += new System.EventHandler(this.refreshToolStripMenuItem1_Click);
            // 
            // backToolStripMenuItem1
            // 
            this.backToolStripMenuItem1.Image = global::usb2snes.Properties.Resources.gtk_goto_first_ltr;
            this.backToolStripMenuItem1.Name = "backToolStripMenuItem1";
            this.backToolStripMenuItem1.Size = new System.Drawing.Size(154, 22);
            this.backToolStripMenuItem1.Text = "Back";
            this.backToolStripMenuItem1.Click += new System.EventHandler(this.backToolStripMenuItem1_Click);
            // 
            // forwardToolStripMenuItem1
            // 
            this.forwardToolStripMenuItem1.Image = global::usb2snes.Properties.Resources.gtk_goto_last_ltr;
            this.forwardToolStripMenuItem1.Name = "forwardToolStripMenuItem1";
            this.forwardToolStripMenuItem1.Size = new System.Drawing.Size(154, 22);
            this.forwardToolStripMenuItem1.Text = "Forward";
            this.forwardToolStripMenuItem1.Click += new System.EventHandler(this.forwardToolStripMenuItem1_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(151, 6);
            // 
            // makeDirToolStripMenuItem1
            // 
            this.makeDirToolStripMenuItem1.Name = "makeDirToolStripMenuItem1";
            this.makeDirToolStripMenuItem1.Size = new System.Drawing.Size(154, 22);
            this.makeDirToolStripMenuItem1.Text = "Make Directory";
            this.makeDirToolStripMenuItem1.Click += new System.EventHandler(this.makeDirToolStripMenuItem1_Click);
            // 
            // renameToolStripMenuItem1
            // 
            this.renameToolStripMenuItem1.Name = "renameToolStripMenuItem1";
            this.renameToolStripMenuItem1.Size = new System.Drawing.Size(154, 22);
            this.renameToolStripMenuItem1.Text = "Rename";
            this.renameToolStripMenuItem1.Click += new System.EventHandler(this.renameToolStripMenuItem1_Click);
            // 
            // buttonUpload
            // 
            this.buttonUpload.Enabled = false;
            this.buttonUpload.ForeColor = System.Drawing.SystemColors.ControlText;
            this.buttonUpload.Image = global::usb2snes.Properties.Resources.gtk_goto_last_ltr;
            this.buttonUpload.Location = new System.Drawing.Point(295, 126);
            this.buttonUpload.Name = "buttonUpload";
            this.buttonUpload.Size = new System.Drawing.Size(27, 42);
            this.buttonUpload.TabIndex = 0;
            this.buttonUpload.UseVisualStyleBackColor = true;
            this.buttonUpload.Click += new System.EventHandler(this.buttonUpload_Click);
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Image = ((System.Drawing.Image)(resources.GetObject("buttonRefresh.Image")));
            this.buttonRefresh.Location = new System.Drawing.Point(587, 12);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(21, 23);
            this.buttonRefresh.TabIndex = 4;
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // buttonDownload
            // 
            this.buttonDownload.Enabled = false;
            this.buttonDownload.Image = global::usb2snes.Properties.Resources.gtk_goto_first_ltr;
            this.buttonDownload.Location = new System.Drawing.Point(295, 183);
            this.buttonDownload.Name = "buttonDownload";
            this.buttonDownload.Size = new System.Drawing.Size(27, 42);
            this.buttonDownload.TabIndex = 1;
            this.buttonDownload.UseVisualStyleBackColor = true;
            this.buttonDownload.Click += new System.EventHandler(this.buttonDownload_Click);
            // 
            // buttonPatch
            // 
            this.buttonPatch.Enabled = false;
            this.buttonPatch.Location = new System.Drawing.Point(556, 324);
            this.buttonPatch.Name = "buttonPatch";
            this.buttonPatch.Size = new System.Drawing.Size(52, 23);
            this.buttonPatch.TabIndex = 12;
            this.buttonPatch.Text = "Patch";
            this.buttonPatch.UseVisualStyleBackColor = true;
            this.buttonPatch.Click += new System.EventHandler(this.buttonPatch_Click);
            // 
            // buttonGetState
            // 
            this.buttonGetState.Enabled = false;
            this.buttonGetState.Location = new System.Drawing.Point(163, 324);
            this.buttonGetState.Name = "buttonGetState";
            this.buttonGetState.Size = new System.Drawing.Size(60, 23);
            this.buttonGetState.TabIndex = 13;
            this.buttonGetState.Text = "Get State";
            this.buttonGetState.UseVisualStyleBackColor = true;
            this.buttonGetState.Click += new System.EventHandler(this.buttonGetState_Click);
            // 
            // buttonSetState
            // 
            this.buttonSetState.Enabled = false;
            this.buttonSetState.Location = new System.Drawing.Point(229, 324);
            this.buttonSetState.Name = "buttonSetState";
            this.buttonSetState.Size = new System.Drawing.Size(60, 23);
            this.buttonSetState.TabIndex = 14;
            this.buttonSetState.Text = "Set State";
            this.buttonSetState.UseVisualStyleBackColor = true;
            this.buttonSetState.Click += new System.EventHandler(this.buttonSetState_Click);
            // 
            // buttonTest
            // 
            this.buttonTest.Enabled = false;
            this.buttonTest.Location = new System.Drawing.Point(12, 324);
            this.buttonTest.Name = "buttonTest";
            this.buttonTest.Size = new System.Drawing.Size(60, 23);
            this.buttonTest.TabIndex = 15;
            this.buttonTest.Text = "Test";
            this.buttonTest.UseVisualStyleBackColor = true;
            this.buttonTest.Click += new System.EventHandler(this.buttonTest_Click);
            // 
            // usb2snes
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(620, 372);
            this.Controls.Add(this.buttonTest);
            this.Controls.Add(this.buttonSetState);
            this.Controls.Add(this.buttonGetState);
            this.Controls.Add(this.buttonPatch);
            this.Controls.Add(this.buttonBoot);
            this.Controls.Add(this.listViewLocal);
            this.Controls.Add(this.buttonRename);
            this.Controls.Add(this.buttonDelete);
            this.Controls.Add(this.buttonMkdir);
            this.Controls.Add(this.listViewRemote);
            this.Controls.Add(this.buttonUpload);
            this.Controls.Add(this.buttonRefresh);
            this.Controls.Add(this.comboBoxPort);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.buttonDownload);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.KeyPreview = true;
            this.Name = "usb2snes";
            this.Text = "usb2snes";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.usb2snes_FormClosed);
            this.Load += new System.EventHandler(this.usb2snes_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.usb2snes_KeyDown);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.contextMenuStripRemote.ResumeLayout(false);
            this.contextMenuStripLocal.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonUpload;
        private System.Windows.Forms.Button buttonDownload;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.ToolStripProgressBar toolStripProgressBar1;
        private System.Windows.Forms.ComboBox comboBoxPort;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        private System.Windows.Forms.Button buttonRefresh;
        private System.Windows.Forms.ImageList imageList;

        private string remoteDirPrev = "";
        private string remoteDir = "";
        private string remoteDirNext = "";
        private string localDirPrev = "";
        private string localDir = "";
        private string localDirNext = "";
        private int bootFlags = 0;
        private System.Windows.Forms.ContextMenuStrip contextMenuStripRemote;
        private System.Windows.Forms.ToolStripMenuItem deleteToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem makeDirToolStripMenuItem;
        private System.Windows.Forms.ListView listViewRemote;
        private bool connected = false;
        private System.Windows.Forms.Button buttonMkdir;
        private System.Windows.Forms.Button buttonDelete;
        private System.Windows.Forms.Button buttonRename;
        private System.Windows.Forms.ToolStripMenuItem bootToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem renameToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem backToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem forwardToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ListView listViewLocal;
        private System.Windows.Forms.Button buttonBoot;
        private System.Windows.Forms.ContextMenuStrip contextMenuStripLocal;
        private System.Windows.Forms.ToolStripMenuItem backToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem forwardToolStripMenuItem1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem makeDirToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem renameToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem refreshToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem refreshToolStripMenuItem1;
        private System.Windows.Forms.Button buttonPatch;
        private System.Windows.Forms.Button buttonGetState;
        private System.Windows.Forms.Button buttonSetState;
        private System.Windows.Forms.Button buttonTest;
    }
}
