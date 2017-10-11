namespace usb2snes
{
    partial class DetailsForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DetailsForm));
            this.hostsDataGridView = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.hostsDataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // hostsDataGridView
            // 
            this.hostsDataGridView.AllowUserToAddRows = false;
            this.hostsDataGridView.AllowUserToDeleteRows = false;
            this.hostsDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.hostsDataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hostsDataGridView.Location = new System.Drawing.Point(0, 0);
            this.hostsDataGridView.Name = "hostsDataGridView";
            this.hostsDataGridView.ReadOnly = true;
            this.hostsDataGridView.Size = new System.Drawing.Size(562, 342);
            this.hostsDataGridView.TabIndex = 0;
            // 
            // DetailsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(562, 342);
            this.Controls.Add(this.hostsDataGridView);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "DetailsForm";
            this.Text = "Connections";
            this.Load += new System.EventHandler(this.DetailsForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.hostsDataGridView)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView hostsDataGridView;

    }
}

