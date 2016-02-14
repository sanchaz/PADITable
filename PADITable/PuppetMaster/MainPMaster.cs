using System;
using System.Windows.Forms;
using System.Collections.Generic;
using IPADITable;

namespace PuppetMaster
{
    static class MainPMaster
    {

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new window());
        }
    }
}
