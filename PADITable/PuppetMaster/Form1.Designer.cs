namespace PuppetMaster
{
    partial class window
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
            this.exitBtn = new System.Windows.Forms.Button();
            this.loadBtn = new System.Windows.Forms.Button();
            this.commandBtn = new System.Windows.Forms.Button();
            this.commandTextBox = new System.Windows.Forms.TextBox();
            this.responseTextBox = new System.Windows.Forms.TextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // exitBtn
            // 
            this.exitBtn.Location = new System.Drawing.Point(353, 242);
            this.exitBtn.Name = "exitBtn";
            this.exitBtn.Size = new System.Drawing.Size(120, 25);
            this.exitBtn.TabIndex = 0;
            this.exitBtn.Text = "Exit";
            this.exitBtn.UseVisualStyleBackColor = true;
            this.exitBtn.MouseClick += new System.Windows.Forms.MouseEventHandler(this.exitMouseClick);
            // 
            // loadBtn
            // 
            this.loadBtn.Location = new System.Drawing.Point(17, 242);
            this.loadBtn.Name = "loadBtn";
            this.loadBtn.Size = new System.Drawing.Size(120, 25);
            this.loadBtn.TabIndex = 5;
            this.loadBtn.Text = "Load Script";
            this.loadBtn.UseVisualStyleBackColor = true;
            this.loadBtn.Click += new System.EventHandler(this.onLoadScript);
            // 
            // commandBtn
            // 
            this.commandBtn.Location = new System.Drawing.Point(17, 211);
            this.commandBtn.Name = "commandBtn";
            this.commandBtn.Size = new System.Drawing.Size(120, 25);
            this.commandBtn.TabIndex = 6;
            this.commandBtn.Text = "Command";
            this.commandBtn.UseVisualStyleBackColor = true;
            this.commandBtn.Click += new System.EventHandler(this.onCommandClick);
            // 
            // commandTextBox
            // 
            this.commandTextBox.Location = new System.Drawing.Point(162, 214);
            this.commandTextBox.Name = "commandTextBox";
            this.commandTextBox.Size = new System.Drawing.Size(311, 20);
            this.commandTextBox.TabIndex = 7;
            // 
            // responseTextBox
            // 
            this.responseTextBox.Location = new System.Drawing.Point(17, 13);
            this.responseTextBox.Multiline = true;
            this.responseTextBox.Name = "responseTextBox";
            this.responseTextBox.ReadOnly = true;
            this.responseTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.responseTextBox.Size = new System.Drawing.Size(456, 192);
            this.responseTextBox.TabIndex = 8;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(162, 242);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(120, 25);
            this.button1.TabIndex = 9;
            this.button1.Text = "Run Script";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.OnRunScriptBtnClicked);
            // 
            // window
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(495, 279);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.responseTextBox);
            this.Controls.Add(this.commandTextBox);
            this.Controls.Add(this.commandBtn);
            this.Controls.Add(this.loadBtn);
            this.Controls.Add(this.exitBtn);
            this.MaximizeBox = false;
            this.Name = "window";
            this.Text = "Puppet Master";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button exitBtn;
        private System.Windows.Forms.Button loadBtn;
        private System.Windows.Forms.Button commandBtn;
        private System.Windows.Forms.TextBox commandTextBox;
        private System.Windows.Forms.TextBox responseTextBox;
        private System.Windows.Forms.Button button1;
    }
}

