using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PuppetMaster
{
    //To add responses frmo clients with register values
    public delegate void DelAddErrorMessage(string username, string msg);

    public partial class Log : Form
    {
        public Log()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Adds an error message to the message log
        /// </summary>
        /// <param name="username"></param>
        /// <param name="msg"></param>
        public void addErrorMessage(string username, string msg)
        {
            if (username != "")
            {
                errorMsgTextBox.Text += username + ": " + msg;
                errorMsgTextBox.Text += Environment.NewLine;
            }
            else
            {
                errorMsgTextBox.Text += "General: " + msg;
                errorMsgTextBox.Text += Environment.NewLine;
            }
        }
    }
}
