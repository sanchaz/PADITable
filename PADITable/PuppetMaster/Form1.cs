using System;
using IPADITable;
using System.Windows;
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
    public delegate void DelAddMsg(string username, List<string> msgs);
    //To show some error message with an some error which ocurred during a command
    public delegate void DelErrorMsg(string msg);
    //To show all values returned in a getAll method
    public delegate void DelAddGetAll(List<List<Tuples<string, string, string, long, TransactionStates>>> tuples);

    public partial class window : Form
    {
        //The window which shows the lists of participants
        //public ParticipantList mParticipants;
        private Parser mParser;
        public Log mLog;

        public window()
        {
            mParser = new Parser(this);
            InitializeComponent();
            //mParticipants = new ParticipantList();
            mLog = new Log();
            this.StartPosition = FormStartPosition.CenterScreen;
            //mParticipants.Top = this.Top;
            //mParticipants.Left -= (this.Left - this.Width - 20);
            //mParticipants.Show();

            mLog.Top = this.Top;
            mLog.Left += (this.Left + this.Width + 20);
            mLog.Show();
        }

        private void exitMouseClick(object sender, MouseEventArgs e)
        {
            mParser.exit();
            Environment.Exit(0);
        }

        private void onLoadScript(object sender, EventArgs e)
        {
            responseTextBox.Text = "";
            mParser.FileStream = null;
            mParser.FileName = null;
            mParser.IsLoaded = false;
            OpenFileDialog scriptChooser = new OpenFileDialog();
            scriptChooser.InitialDirectory = "C:\\";
            scriptChooser.Filter = "ps Files (*.ps)|*.ps| txt Files (*.txt)|*.txt";
            scriptChooser.FilterIndex = 2;

            if (scriptChooser.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string fileName = scriptChooser.FileName;
                    if ((mParser.FileStream = new System.IO.StreamReader(fileName)) != null)
                    {
                        mParser.FileName = fileName;
                        mParser.FileDirectory = System.IO.Path.GetDirectoryName(fileName);
                        System.Windows.Forms.MessageBox.Show("Script loaded: " + scriptChooser.FileName);
                    }
                } catch(Exception ex) {
                    System.Windows.Forms.MessageBox.Show("Could not read file from disk. Original error: " + ex.Message);
                }
            }
        }

        private void onCommandClick(object sender, EventArgs e)
        {
            string command = commandTextBox.Text;
            string[] commandSplit = command.Split(' ');
            switch (commandSplit[0])
            {
                case "RUN":
                    if (!isScriptLoaded()) { System.Windows.Forms.MessageBox.Show("Please load a file before execute a command"); return; }
                    mParser.FileLine++;
                    mParser.run();
                    break;
                case "STEP":
                    if (!isScriptLoaded()) { System.Windows.Forms.MessageBox.Show("Please load a file before execute a command"); return; }
                    if (commandSplit.Length != 2)
                    {
                        System.Windows.Forms.MessageBox.Show("Bad command: STEP N");
                        break;
                    }
                    mParser.FileLine++;
                    mParser.stepN(Int32.Parse(commandSplit[1]));
                    break;
                case "CONNECT":
                    if (commandSplit.Length != 3)
                    {
                        System.Windows.Forms.MessageBox.Show("Bad command: CONNECT <USERNAME> <IP>:<PORT>");
                        break;
                    }
                    try
                    {
                        string port = commandSplit[2].Split(':').ElementAt(1);
                        mParser.FileLine++;
                        mParser.addParticipant(commandSplit[1], port);
                    } catch (Exception ex) { System.Windows.Forms.MessageBox.Show(ex.Message); } 
                    break;
                case "DISCONNECT":
                    if (commandSplit.Length != 3)
                    {
                        System.Windows.Forms.MessageBox.Show("Bad command: DISCONNECT <USERNAME> <IP>:<PORT>");
                        break;
                    }
                    mParser.FileLine++;
                    mParser.tellServerToFail(commandSplit[1]);
                    break;
                default:
                    mParser.executeInstruction(commandSplit);
                    //System.Windows.Forms.MessageBox.Show("Command not recognized, recognized commands: RUN; STEP N; CONNECT USERNAME; DISCONNECT USERNAME");
                    break;
            }
        }

        /// <summary>
        /// Checks if the user already loaded a script into the puppet master or not
        /// </summary>
        /// <returns></returns>
        public bool isScriptLoaded()
        {
            if (!mParser.IsLoaded)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Prints the values returned in a dump command
        /// </summary>
        /// <param name="msgs"></param>
        public void addMsg(string username, List<string> msgs)
        {
            responseTextBox.Text += "Register values in " + username + ":";
            if (msgs.Count > 0)
            {
                for(int i = 0; i < 10; i++)
                {
                    if (msgs[i] == null) { responseTextBox.Text += " N/D"; }
                    else { responseTextBox.Text += " " + msgs[i]; }
                }
            }
            else 
            {
                for (int i = 0; i < 10; i++)
                {
                        responseTextBox.Text += " N/D";
                }
            }
            responseTextBox.Text += Environment.NewLine;
        }

        /// <summary>
        /// Prints all the values returned in a getAll method
        /// </summary>
        /// <param name="tuples"></param>
        public void addGetAll(List<List<Tuples<string, string, string, long, TransactionStates>>> tuples)
        {
            foreach (List<Tuples<string, string, string, long, TransactionStates>> list in tuples)
            {
                foreach (Tuples<string, string, string, long, TransactionStates> tuple in list)
                {
                    responseTextBox.Text += "GETALL: " + tuple.Key + " " + tuple.Node + " " + tuple.Value + " " + tuple.Timestamp + " " + tuple.State;
                    responseTextBox.Text += Environment.NewLine;
                }
                responseTextBox.Text += Environment.NewLine;
            }
        }

        /// <summary>
        /// Shows a dialog with an error message which ocurred in the execution of a command
        /// </summary>
        /// <param name="msg"></param>
        public void showErrorMessage(string msg)
        {
            System.Windows.Forms.MessageBox.Show(msg);
        }

        //Gets the form which shows the active participants
        //public ParticipantList Participants { get { return mParticipants; } }

        /// <summary>
        /// Called when the button which is meant to run a loaded script is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnRunScriptBtnClicked(object sender, MouseEventArgs e)
        {
            if (!isScriptLoaded()) { System.Windows.Forms.MessageBox.Show("Please load a file before execute a script"); return; }
            mParser.executeScript();
        }
    }
}
