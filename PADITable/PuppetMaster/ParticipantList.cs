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
    public delegate void DelAddParticipant(string username);
    public delegate void DelRemParticipant(string username);

    public partial class ParticipantList : Form
    {
        public ParticipantList()
        {
            InitializeComponent();
        }

        public void addClient(string username)
        {
            clientsListBox.Items.Add(username);
        }

        public void addServer(string username)
        {
            ServersListBox.Items.Add(username);
        }

        public void remServer(string username)
        {
            ServersListBox.Items.Remove(username);
        }

        public void remClient(string username)
        {
            clientsListBox.Items.Remove(username);
        }
    }
}
