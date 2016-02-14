using System;
using IPADITable;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;


namespace Client
{
    /// <summary>
    /// To send unexpected messages, for ex when detects that a server is down
    /// </summary>
    class ClientThread
    {
        private Client mClient;

        public ClientThread() { }

        public void setClient(Client cli) { mClient = cli; }

        public void execFile(string fileName)
        {
            StreamReader mFileStream;
            try
            {
                if ((mFileStream = new System.IO.StreamReader(fileName)) != null)
                {
                    System.Console.WriteLine("Script loaded: " + fileName);

                    string line;
                    int fileLine = 1;

                    while ((line = mFileStream.ReadLine()) != null)
                    {
                        string[] instruction = line.Split(' ');
                        try
                        {
                            if ((instruction.Length >= 2) && instruction[0].CompareTo("WAIT") == 0) { wait(Int32.Parse(instruction[1])); }
                            try
                            {
                                mClient.executeInstruction(instruction);
                            } catch(Exception) {
                                //Just to avoid error messages being showed along the exection
                            }
                            fileLine++;
                        }
                        catch (Exception e)
                        {
                            System.Console.WriteLine(e.Message + " at line " + fileLine + " " + e.GetType().ToString());
                            return;
                        }
                    }

                    System.Console.WriteLine("Script completed");

                    mClient.printRegisters();
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Could not read file from disk. Original error: " + ex.Message);
            }
        }

        public void wait(int time) { Thread.Sleep(time); }
    }
}
