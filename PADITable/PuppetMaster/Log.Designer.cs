namespace PuppetMaster
{
    partial class Log
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
            this.errorMsgTextBox = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // errorMsgTextBox
            // 
            this.errorMsgTextBox.Location = new System.Drawing.Point(13, 13);
            this.errorMsgTextBox.Multiline = true;
            this.errorMsgTextBox.Name = "errorMsgTextBox";
            this.errorMsgTextBox.ReadOnly = true;
            this.errorMsgTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.errorMsgTextBox.Size = new System.Drawing.Size(609, 237);
            this.errorMsgTextBox.TabIndex = 0;
            // 
            // Log
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(634, 262);
            this.Controls.Add(this.errorMsgTextBox);
            this.Name = "Log";
            this.Text = "Log";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox errorMsgTextBox;
    }
}