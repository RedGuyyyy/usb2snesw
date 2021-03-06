﻿namespace usb2snes
{
    partial class usb2snesmemoryviewer
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(usb2snesmemoryviewer));
            this.comboBoxPort = new System.Windows.Forms.ComboBox();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripProgressBar1 = new System.Windows.Forms.ToolStripProgressBar();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.checkBoxAutoUpdate = new System.Windows.Forms.CheckBox();
            this.comboBoxRegion = new System.Windows.Forms.ComboBox();
            this.buttonExport = new System.Windows.Forms.Button();
            this.textBoxBase = new System.Windows.Forms.TextBox();
            this.textBoxSize = new System.Windows.Forms.TextBox();
            this.pictureConnected = new System.Windows.Forms.PictureBox();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.buttonGsuDebug = new System.Windows.Forms.Button();
            this.buttonSa1Debug = new System.Windows.Forms.Button();
            this.hexBox = new Be.Windows.Forms.HexBox();
            this.statusStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureConnected)).BeginInit();
            this.SuspendLayout();
            // 
            // comboBoxPort
            // 
            this.comboBoxPort.FormattingEnabled = true;
            this.comboBoxPort.Location = new System.Drawing.Point(396, 14);
            this.comboBoxPort.Name = "comboBoxPort";
            this.comboBoxPort.Size = new System.Drawing.Size(229, 21);
            this.comboBoxPort.TabIndex = 5;
            this.comboBoxPort.SelectedIndexChanged += new System.EventHandler(this.comboBoxPort_SelectedIndexChanged);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripProgressBar1,
            this.toolStripStatusLabel1});
            this.statusStrip1.Location = new System.Drawing.Point(0, 450);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(664, 22);
            this.statusStrip1.TabIndex = 7;
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
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(547, 17);
            this.toolStripStatusLabel1.Spring = true;
            this.toolStripStatusLabel1.Text = "idle";
            this.toolStripStatusLabel1.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // checkBoxAutoUpdate
            // 
            this.checkBoxAutoUpdate.AutoSize = true;
            this.checkBoxAutoUpdate.Checked = true;
            this.checkBoxAutoUpdate.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxAutoUpdate.Location = new System.Drawing.Point(12, 18);
            this.checkBoxAutoUpdate.Name = "checkBoxAutoUpdate";
            this.checkBoxAutoUpdate.Size = new System.Drawing.Size(83, 17);
            this.checkBoxAutoUpdate.TabIndex = 10;
            this.checkBoxAutoUpdate.Text = "AutoUpdate";
            this.checkBoxAutoUpdate.UseVisualStyleBackColor = true;
            // 
            // comboBoxRegion
            // 
            this.comboBoxRegion.FormattingEnabled = true;
            this.comboBoxRegion.Location = new System.Drawing.Point(101, 16);
            this.comboBoxRegion.Name = "comboBoxRegion";
            this.comboBoxRegion.Size = new System.Drawing.Size(121, 21);
            this.comboBoxRegion.TabIndex = 11;
            this.comboBoxRegion.SelectedIndexChanged += new System.EventHandler(this.comboBoxRegion_SelectedIndexChanged);
            // 
            // buttonExport
            // 
            this.buttonExport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonExport.Location = new System.Drawing.Point(12, 425);
            this.buttonExport.Name = "buttonExport";
            this.buttonExport.Size = new System.Drawing.Size(51, 23);
            this.buttonExport.TabIndex = 12;
            this.buttonExport.Text = "Export";
            this.buttonExport.UseVisualStyleBackColor = true;
            this.buttonExport.Click += new System.EventHandler(this.buttonExport_Click);
            // 
            // textBoxBase
            // 
            this.textBoxBase.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textBoxBase.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.textBoxBase.Location = new System.Drawing.Point(69, 426);
            this.textBoxBase.Name = "textBoxBase";
            this.textBoxBase.Size = new System.Drawing.Size(83, 20);
            this.textBoxBase.TabIndex = 13;
            this.textBoxBase.Text = "F50000";
            this.textBoxBase.TextChanged += new System.EventHandler(this.textBoxBase_TextChanged);
            // 
            // textBoxSize
            // 
            this.textBoxSize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textBoxSize.CharacterCasing = System.Windows.Forms.CharacterCasing.Upper;
            this.textBoxSize.Location = new System.Drawing.Point(158, 426);
            this.textBoxSize.Name = "textBoxSize";
            this.textBoxSize.Size = new System.Drawing.Size(83, 20);
            this.textBoxSize.TabIndex = 14;
            this.textBoxSize.Text = "2000";
            this.textBoxSize.TextChanged += new System.EventHandler(this.textBoxSize_TextChanged);
            // 
            // pictureConnected
            // 
            this.pictureConnected.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.pictureConnected.Image = global::usb2snesmemoryviewer.Properties.Resources.bullet_red;
            this.pictureConnected.InitialImage = ((System.Drawing.Image)(resources.GetObject("pictureConnected.InitialImage")));
            this.pictureConnected.Location = new System.Drawing.Point(373, 17);
            this.pictureConnected.Name = "pictureConnected";
            this.pictureConnected.Size = new System.Drawing.Size(17, 20);
            this.pictureConnected.TabIndex = 8;
            this.pictureConnected.TabStop = false;
            this.pictureConnected.Click += new System.EventHandler(this.pictureConnected_Click);
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Image = ((System.Drawing.Image)(resources.GetObject("buttonRefresh.Image")));
            this.buttonRefresh.Location = new System.Drawing.Point(631, 12);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(21, 23);
            this.buttonRefresh.TabIndex = 6;
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // buttonGsuDebug
            // 
            this.buttonGsuDebug.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonGsuDebug.Location = new System.Drawing.Point(577, 426);
            this.buttonGsuDebug.Name = "buttonGsuDebug";
            this.buttonGsuDebug.Size = new System.Drawing.Size(75, 23);
            this.buttonGsuDebug.TabIndex = 15;
            this.buttonGsuDebug.Text = "GSU Debug";
            this.buttonGsuDebug.UseVisualStyleBackColor = true;
            this.buttonGsuDebug.Visible = false;
            this.buttonGsuDebug.Click += new System.EventHandler(this.buttonGsuDebug_Click);
            // 
            // buttonSa1Debug
            // 
            this.buttonSa1Debug.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSa1Debug.Location = new System.Drawing.Point(496, 426);
            this.buttonSa1Debug.Name = "buttonSa1Debug";
            this.buttonSa1Debug.Size = new System.Drawing.Size(75, 23);
            this.buttonSa1Debug.TabIndex = 16;
            this.buttonSa1Debug.Text = "SA1 Debug";
            this.buttonSa1Debug.UseVisualStyleBackColor = true;
            this.buttonSa1Debug.Visible = false;
            this.buttonSa1Debug.Click += new System.EventHandler(this.buttonSa1Debug_Click);
            // 
            // hexBox
            // 
            this.hexBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.hexBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.hexBox.ColumnInfoVisible = true;
            this.hexBox.Enabled = false;
            this.hexBox.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.hexBox.LineInfoVisible = true;
            this.hexBox.Location = new System.Drawing.Point(12, 41);
            this.hexBox.Name = "hexBox";
            this.hexBox.ShadowSelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(60)))), ((int)(((byte)(188)))), ((int)(((byte)(255)))));
            this.hexBox.Size = new System.Drawing.Size(640, 377);
            this.hexBox.StringViewVisible = true;
            this.hexBox.TabIndex = 9;
            this.hexBox.UseFixedBytesPerLine = true;
            this.hexBox.VScrollBarVisible = true;
            // 
            // usb2snesmemoryviewer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(664, 472);
            this.Controls.Add(this.buttonSa1Debug);
            this.Controls.Add(this.buttonGsuDebug);
            this.Controls.Add(this.textBoxSize);
            this.Controls.Add(this.textBoxBase);
            this.Controls.Add(this.buttonExport);
            this.Controls.Add(this.comboBoxRegion);
            this.Controls.Add(this.checkBoxAutoUpdate);
            this.Controls.Add(this.hexBox);
            this.Controls.Add(this.pictureConnected);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.buttonRefresh);
            this.Controls.Add(this.comboBoxPort);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.MaximizeBox = false;
            this.Name = "usb2snesmemoryviewer";
            this.Text = "usb2snesviewer";
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureConnected)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonRefresh;
        private System.Windows.Forms.ComboBox comboBoxPort;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripProgressBar toolStripProgressBar1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.PictureBox pictureConnected;
        private Be.Windows.Forms.HexBox hexBox;
        private System.Windows.Forms.CheckBox checkBoxAutoUpdate;
        private System.Windows.Forms.ComboBox comboBoxRegion;
        private System.Windows.Forms.Button buttonExport;
        private System.Windows.Forms.TextBox textBoxBase;
        private System.Windows.Forms.TextBox textBoxSize;
        private System.Windows.Forms.Button buttonGsuDebug;
        private System.Windows.Forms.Button buttonSa1Debug;
    }
}

