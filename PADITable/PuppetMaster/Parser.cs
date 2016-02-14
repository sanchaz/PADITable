using System;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Linq;
using IPADITable;
using System.IO;
using System.Threading;

namespace PuppetMaster
{
    public class Parser
    {
        //The form to be able to execute methods in the form
        private window mWindow;
        //The stream reader from which we are going to send messages
        private StreamReader mFileStream;
        //The file name
        private string mFileName;
        //The directory of the loaded script
        private string mFileDirectory;
        //Boolean telling if a file has been loaded
        private bool isLoaded;
        //To track the line number with the command we are executing
        private int fileLine;
        //The list of clients
        private Dictionary<string, IClient> mClients;
        //The list of servers
        private Dictionary<string, IServer> mServers;
        //The central directory
        private ICentralDir mCDir;
        //Central directory url
        private string centralURL;

        public Parser(window form)
        {
            mWindow = form;
            isLoaded = false;
            fileLine = 1;

            mClients = new Dictionary<string, IClient>();
            mServers = new Dictionary<string, IServer>();

            //The value 0 gives us the first available port
            TcpChannel pMasterChannel = new TcpChannel(0);
            ChannelServices.RegisterChannel(pMasterChannel, true);
        }

        /// <summary>
        /// Checks if all the isntructions of a script are to be execute or just a few, and executes them
        /// </summary>
        public void executeScript()
        {
            exit();
            fileLine = 1;
            string line;
            if ((line = mFileStream.ReadLine()) != null)
            {
                string[] instruction = line.Split(' ');
                try
                {
                    if (instruction[0].CompareTo("RUN") == 0) { fileLine++; run(); }
                    else if (instruction[0].CompareTo("STEP") == 0 && instruction.Length >= 2)
                    { 
                        fileLine++;
                        stepN(Int32.Parse(instruction[1]));
                        while ((line = mFileStream.ReadLine()) != null)
                        {
                            string[] stepInstruction = line.Split(' ');
                            if (stepInstruction[0].CompareTo("STEP") == 0 && stepInstruction.Length == 2)
                            {
                                System.Windows.Forms.MessageBox.Show("Click to execute another step instruction: stoped at " + fileLine);
                                fileLine++;
                                stepN(Int32.Parse(stepInstruction[1]));
                            }
                            else
                            {
                                break;
                            }
                        }
                        System.Windows.Forms.MessageBox.Show("Script executed");
                    }
                    else { mWindow.showErrorMessage("Unrecognized instruction at line " + fileLine + ", RUN or STEP <N> expected"); }
                } catch (Exception e) {
                    mWindow.showErrorMessage(e.Message + " at line " + fileLine + " " + e.GetType().ToString());
                    return;
                }
            }
            else
            {
                mWindow.showErrorMessage("The script is empty");
            }
        }

        /// <summary>
        /// Executes all instructions found on a loaded script
        /// </summary>
        public void run()
        {
            string line;
            while((line = mFileStream.ReadLine()) != null)
            {
                string[] instruction = line.Split(' ');

                executeInstruction(instruction);
                fileLine++;
            }
            System.Windows.Forms.MessageBox.Show("Script executed");
        }

        /// <summary>
        /// Executes the first n instructions found on a loaded script
        /// </summary>
        /// <param name="n"></param>
        public void stepN(int n)
        {
            for (int i = 1; i <= n; i++)
            {
                string line;
                if((line = mFileStream.ReadLine()) == null)
                {
                    System.Windows.Forms.MessageBox.Show("Could not read more from file. Lines read " + i);
                    return;
                }

                string[] instruction = line.Split(' ');

                executeInstruction(instruction);
                fileLine++;
            }
        }

        /// <summary>
        /// Executes a single instruction
        /// </summary>
        /// <param name="instruction"></param>
        /// <returns></returns>
        public bool executeInstruction(string[] instruction)
        {
            switch (instruction[0])
            {
                case "CONNECT":
                    if (instruction.Length < 3)
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                        new Object[] { "Malformed command in file " + mFileName + " at line " + fileLine });
                        return false;
                    }

                    try
                    {
                        string port = instruction[2].Split(':').ElementAt(1);
                        addParticipant(instruction[1], port);
                        return true;
                    }
                    catch (Exception) { return false; }
                case "DISCONNECT":
                    if (instruction.Length < 3)
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                        new Object[] { "Malformed command in file " + mFileName + " at line " + fileLine });
                        return false;
                    }

                    try
                    {
                        tellServerToFail(instruction[1]);
                        return true;
                    }
                    catch (Exception) { return false; }
                case "BEGINTX":
                    if (instruction.Length < 2)
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                        new Object[] { "Malformed command in file " + mFileName + " at line " + fileLine });
                        return false;
                    }
                    else if (!(mClients.ContainsKey(instruction[1])))
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                        new Object[] { "Client not found: " + instruction[1] + " at line " + fileLine });
                        return false;
                    }
                    return beginTx(instruction[1]);

                case "STORE":
                    if (instruction.Length < 4)
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                                    new Object[] { "Malformed command in file " + mFileName + " at line " + fileLine });
                        return false;
                    }
                    else if (!(mClients.ContainsKey(instruction[1])))
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                        new Object[] { "Client not found: " + instruction[1] + " at line " + fileLine });
                        return false;
                    }
                    return storeRegVal(instruction[1], Int32.Parse(instruction[2]), instruction[3]);

                case "PUT":
                    if (instruction.Length < 4)
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                                    new Object[] { "Malformed command in file " + mFileName + " at line " + fileLine});
                        return false;
                    }
                    else if (!(mClients.ContainsKey(instruction[1])))
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                        new Object[] { "Client not found: " + instruction[1] + " at line " + fileLine });
                        return false;
                    }
                    return putRegKey(instruction[1], Int32.Parse(instruction[2]), instruction[3]);

                case "GET":
                    if (instruction.Length < 4)
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                                    new Object[] { "Malformed command in file " + mFileName + " at line " + fileLine });
                        return false;
                    }
                    else if (!(mClients.ContainsKey(instruction[1])))
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                        new Object[] { "Client not found: " + instruction[1] + " at line " + fileLine });
                        return false;
                    }
                    return getRegKey(instruction[1], Int32.Parse(instruction[2]), instruction[3]);
                    

                case "PUTVAL":
                    if (instruction.Length < 4)
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                                    new Object[] { "Malformed command in file " + mFileName + " at line " + fileLine });
                        return false;
                    }
                    else if (!(mClients.ContainsKey(instruction[1])))
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                        new Object[] { "Client not found: " + instruction[1] + " at line " + fileLine });
                        return false;
                    }
                    return putValKeyVal(instruction[1], instruction[2], instruction[3]);

                case "TOLOWER":
                    if (instruction.Length < 3)
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                                    new Object[] { "Malformed command in file " + mFileName + " at line " + fileLine });
                        return false;
                    }
                    else if (!(mClients.ContainsKey(instruction[1])))
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                        new Object[] { "Client not found: " + instruction[1] + " at line " + fileLine });
                        return false;
                    }
                    return toLower(instruction[1], Int32.Parse(instruction[2]));

                case "TOUPPER":
                    if (instruction.Length < 3)
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                                    new Object[] { "Malformed command in file " + mFileName + " at line " + fileLine });
                        return false;
                    }
                    else if (!(mClients.ContainsKey(instruction[1])))
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                        new Object[] { "Client not found: " + instruction[1] + " at line " + fileLine });
                        return false;
                    }
                    return toUpper(instruction[1], Int32.Parse(instruction[2]));
                    
                case "CONCAT":
                    if (instruction.Length < 4)
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                                    new Object[] { "Malformed command in file " + mFileName + " at line " + fileLine });
                        return false;
                    }
                    else if (!(mClients.ContainsKey(instruction[1])))
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                        new Object[] { "Client not found: " + instruction[1] + " at line " + fileLine });
                        return false;
                    }
                    return concat(instruction[1], Int32.Parse(instruction[2]), Int32.Parse(instruction[3]));
                    
                case "COMMITTX":
                    if (instruction.Length < 2)
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                                    new Object[] { "Malformed command in file " + mFileName + " at line " + fileLine });
                        return false;
                    }
                    else if (!(mClients.ContainsKey(instruction[1])))
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                        new Object[] { "Client not found: " + instruction[1] + " at line " + fileLine });
                        return false;
                    }
                    return commitTx(instruction[1]);

                case "DUMP":
                    if (instruction.Length < 2)
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                                    new Object[] { "Malformed command in file " + mFileName + " at line " + fileLine });
                        return false;
                    }
                    else if (!(mClients.ContainsKey(instruction[1])))
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                        new Object[] { "Client not found: " + instruction[1] + " at line " + fileLine });
                        return false;
                    }
                    return dump(instruction[1]);
                case "GETALL":
                    if (instruction.Length < 2)
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                                    new Object[] { "Malformed command in file " + mFileName + " at line " + fileLine });
                        return false;
                    }
                    return getAll(instruction[1]);
                case "EXESCRIPT":
                    if (instruction.Length < 3)
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                                    new Object[] { "Malformed command in file " + mFileName + " at line " + fileLine });
                        return false;
                    }
                    return exeScript(instruction[1], instruction[2]);
                case "WAIT":
                    if (instruction.Length < 3)
                    {
                        mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                                    new Object[] { "Malformed command in file " + mFileName + " at line " + fileLine });
                        return false;
                    }
                    return wait(instruction[1], Int32.Parse(instruction[2]));
                default:
                    mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                                    new Object[] { "Command not recognized: " + instruction[0] + " at line " + fileLine });
                    return false;
            }
        }

        /// <summary>
        /// Executes an exescript command on a client
        /// </summary>
        /// <param name="username"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public bool exeScript(string username, string fileName)
        {
            IClient client;
            if (mClients.TryGetValue(username, out client))
            {
                client.exeScript(mFileDirectory + "\\" + fileName);
                return true;
            }

            mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage), new Object[] { "Invalid username " + username });
            return false;
        }

        /// <summary>
        /// Starts a transaction
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public bool beginTx(string username)
        {
            try
            {
                IClient client;
                if(mClients.TryGetValue(username, out client))
                {
                    client.beginTransaction(); 
                    return true;
                }

                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage), new Object[] { "Invalid username " + username });
                addErrorMessage(username, "Invalid username " + username);
                return false;

            } catch(Exception ex) {
                addErrorMessage(username, ex.GetBaseException().Message + " BEGINTX at line " + fileLine);
                return false;
            }
            /*catch (SocketException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.Message + " when calling BEGINTX at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.Message + " when calling BEGINTX at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } catch (TimeoutException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.Message + " when calling BEGINTX at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.Message + " when calling BEGINTX at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } catch (RemotingException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.Message + " when calling BEGINTX at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.Message + " when calling BEGINTX at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            }*/
        }

        /// <summary>
        /// Stores a value in a register
        /// </summary>
        /// <param name="username"></param>
        /// <param name="regVal"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        public bool storeRegVal(string username, int regVal, string val)
        {
            try
            {
                IClient client;
                if (mClients.TryGetValue(username, out client))
                {
                    client.storeRegisterValue(regVal, val);
                    return true;
                }

                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage), new Object[] { "Invalid username " + username });

                addErrorMessage(username, "Invalid username " + username);

                return false;
            } catch (Exception ex) {
                addErrorMessage(username, ex.Message + " STORE at line " + fileLine);
                return false;
            }
            /*catch (RemoteException ex)
            {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.getMessage() + " STORE at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.getMessage() + " STORE at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } catch (SocketException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.Message + " STORE at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.Message + " STORE at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } catch (TimeoutException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.Message + " STORE at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.Message + " STORE at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } catch (RemotingException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.Message + " STORE at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.Message + " STORE at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } */
        }
        
        /// <summary>
        /// Puts a value stored in a register in a key
        /// </summary>
        /// <param name="username"></param>
        /// <param name="regVal"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool putRegKey(string username, int regVal, string key)
        {
            try
            {
                IClient client;
                if (mClients.TryGetValue(username, out client))
                {
                    client.putRegisterKey(regVal, key);
                    return true;
                }

                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage), new Object[] { "Invalid username " + username });

                addErrorMessage(username, "Invalid username " + username);

                return false;
            } catch (Exception ex) {
                addErrorMessage(username, ex.GetBaseException().Message + " PUT at line " + fileLine);
                return false;
            }
            /*catch (RemoteException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.getMessage() + " in PUT at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.getMessage() + " in PUT at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } catch (SocketException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.Message + " in PUT at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.Message + " in PUT at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } catch (TimeoutException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.Message + " in PUT at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.Message + " in PUT at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } catch (RemotingException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.Message + " in PUT at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.Message + " in PUT at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } catch (AbortException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.getMessage() + " in PUT at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.Message + " in PUT at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            }*/
        }

        /// <summary>
        /// Gets a value from a key and stores it in a register
        /// </summary>
        /// <param name="username"></param>
        /// <param name="regVal"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool getRegKey(string username, int regVal, string key)
        {
            try
            {
                IClient client;
                if (mClients.TryGetValue(username, out client))
                {
                    client.getRegisterKey(regVal, key);
                    return true;
                }

                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage), new Object[] { "Invalid username " + username });

                addErrorMessage(username, "Invalid username " + username);

                return false;
            } catch (Exception ex) {
                addErrorMessage(username, ex.GetBaseException().Message + " GET at line " + fileLine);
                return false;
            }
            /*catch (RemoteException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.getMessage() + " GET at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.getMessage() + " GET at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } catch (SocketException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.Message + " GET at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.Message + " GET at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } catch (TimeoutException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.Message + " GET at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.Message + " GET at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } catch (RemotingException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.Message + " GET at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.Message + " GET at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } catch (AbortException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.getMessage() + " GET at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.Message + " GET at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            }*/
        }

        /// <summary>
        /// Stores a value in a key
        /// </summary>
        /// <param name="username"></param>
        /// <param name="key"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        public bool putValKeyVal(string username, string key, string val)
        {
            try
            {
                IClient client;
                if (mClients.TryGetValue(username, out client))
                {
                    client.putValKeyValue(key, val);
                    return true;
                }

                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage), new Object[] { "Invalid username " + username });

                addErrorMessage(username, "Invalid username " + username);

                return false;
            } catch (Exception ex) {
                addErrorMessage(username, ex.GetBaseException().Message + " PUTVAL at line " + fileLine);
                return false;
            }
            /*catch (RemoteException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.getMessage() + " PUTVAL at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.getMessage() + " PUTVAL at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } catch (SocketException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.Message + " PUTVAL at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.getMessage() + " PUTVAL at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } catch (TimeoutException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.Message + " PUTVAL at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.getMessage() + " PUTVAL at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } catch (RemotingException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.Message + " PUTVAL at line " + fileLine });

                List<string> error = new List<string>();
                error.Add(ex.getMessage() + " PUTVAL at line " + fileLine);
                mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, error });

                return false;
            } catch (AbortException ex) {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { ex.getMessage() + " PUTVAL at line " + fileLine });
                return false;
            }*/
        }

        /// <summary>
        /// Converts a value stored in a register to lower case
        /// </summary>
        /// <param name="username"></param>
        /// <param name="regVal"></param>
        /// <returns></returns>
        public bool toLower(string username, int regVal)
        {
            try
            {
                IClient client;
                if (mClients.TryGetValue(username, out client))
                {
                    client.toLowerRegister(regVal);
                    return true;
                }

                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage), new Object[] { "Invalid username " + username });
                addErrorMessage(username, "Invalid username " + username);
                return false;
            } catch (Exception ex) {
                addErrorMessage(username, ex.GetBaseException().Message + " TOLOWER at line " + fileLine);
                return false;
            }
            /*catch (RemoteException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.getMessage() + " TOLOWER at line " + fileLine });
                return false;
            } catch (SocketException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " TOLOWER at line " + fileLine });
                return false;
            } catch (TimeoutException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " TOLOWER at line " + fileLine });
                return false;
            } catch (RemotingException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " TOLOWER at line " + fileLine });
                return false;
            }*/
        }

        /// <summary>
        /// Convers a value stored in a register to upper case
        /// </summary>
        /// <param name="username"></param>
        /// <param name="regVal"></param>
        /// <returns></returns>
        public bool toUpper(string username, int regVal)
        {
            try
            {
                IClient client;
                if (mClients.TryGetValue(username, out client))
                {
                    client.toUpperRegister(regVal);
                    return true;
                }

                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage), new Object[] { "Invalid username " + username });
                addErrorMessage(username, "Invalid username " + username);
                return false;
            } catch (Exception ex) {
                addErrorMessage(username, ex.GetBaseException().Message + " TOUPPER at line " + fileLine);
                return false;
            }
            /*catch (RemoteException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.getMessage() + " TOUPPER at line " + fileLine });
                return false;
            } catch (SocketException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " TOUPPER at line " + fileLine });
                return false;
            } catch (TimeoutException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " TOUPPER at line " + fileLine });
                return false;
            } catch (RemotingException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " TOUPPER at line " + fileLine });
                return false;
            } */
        }

        /// <summary>
        /// Concats the values stored in two register and stores it in the first one
        /// </summary>
        /// <param name="username"></param>
        /// <param name="regVal1"></param>
        /// <param name="regVal2"></param>
        /// <returns></returns>
        public bool concat(string username, int regVal1, int regVal2)
        {
            try
            {
                IClient client;
                if (mClients.TryGetValue(username, out client))
                {
                    client.concatRegisters(regVal1, regVal2);
                    return true;
                }

                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage), new Object[] { "Invalid username " + username });
                addErrorMessage(username, "Invalid username " + username);
                return false;
            } catch(Exception ex) {
                addErrorMessage(username, ex.GetBaseException().Message + " CONCAT at line " + fileLine);
                return false;
            }
            /*catch (RemoteException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.getMessage() + " CONCAT at line " + fileLine });
                return false;
            } catch (SocketException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " CONCAT at line " + fileLine });
                return false;
            } catch (TimeoutException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " CONCAT at line " + fileLine });
                return false;
            } catch (RemotingException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " CONCAT at line " + fileLine });
                return false;
            }*/
        }

        /// <summary>
        /// Commits the active transaction on a client
        /// </summary>
        /// <returns></returns>
        public bool commitTx(string username)
        {
            try
            {
                IClient client;
                if (mClients.TryGetValue(username, out client))
                {
                    client.commitTransaction();
                    return true;
                }

                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage), new Object[] { "Invalid username " + username });
                addErrorMessage(username, "Invalid username " + username);
                return false;
            } catch (Exception ex) {
                addErrorMessage(username, ex.GetBaseException().Message + " COMMITTX at line " + fileLine);
                return false;
            }
            /*catch (RemoteException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.getMessage() + " COMMITTX at line " + fileLine });
                return false;
            } catch (SocketException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " COMMITTX at line " + fileLine });
                return false;
            } catch (TimeoutException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " COMMITTX at line " + fileLine });
                return false;
            } catch (RemotingException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " COMMITTX at line " + fileLine });
                return false;
            } catch (AbortException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.getMessage() + " COMMITTX at line " + fileLine });
                return false;
            } */
        }

        /// <summary>
        /// Dumps the values stored in all registers
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public bool dump(string username)
        {
            try
            {
                IClient client;
                if (mClients.TryGetValue(username, out client))
                {
                    List<string> regVals = client.dump();
                    mWindow.Invoke(new DelAddMsg(mWindow.addMsg), new Object[] { username, regVals });
                    return true;
                }

                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage), new Object[] { "Invalid username " + username });
                addErrorMessage(username, "Invalid username " + username);
                return false;
                
            } catch(Exception ex) {
                addErrorMessage(username, ex.GetBaseException().Message + " when calling DUMP at line " + fileLine);
                return false;
            } /*catch (SocketException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " when calling DUMP at line " + fileLine });
                return false;
            } catch (TimeoutException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " DUMP at line " + fileLine });
                return false;
            } catch (RemotingException ex) {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " when calling DUMP at line " + fileLine });
                return false;
            }*/
        }

        /// <summary>
        /// Gets all the tuples associated with the transactions which associates with the specified key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool getAll(string key)
        {
            List<List<Tuples<string, string, string, long, TransactionStates>>> keyTuples = new List<List<Tuples<string,string,string,long,TransactionStates>>>();
            foreach (IServer server in mServers.Values)
            {
                if (server != null)
                {
                    try
                    {
                        List<Tuples<string, string, string, long, TransactionStates>> list = server.getAll(key);
                        if (list != null)
                        {
                            keyTuples.Add(list);
                        }
                    } catch (Exception ex) {
                        addErrorMessage("", ex.GetBaseException().Message + " GETALL at line " + fileLine);
                    }
                    
                }
            }
            if (keyTuples.Count > 0) 
            {
                mWindow.Invoke(new DelAddGetAll(mWindow.addGetAll), new Object[] { keyTuples } );
                return true;
            }
            else 
            {
                //System.Windows.Forms.MessageBox.Show("No active transactions for key " + key);
                addErrorMessage("", "No active transactions for key " + key);
                return false;
            }
        }

        /// <summary>
        /// Adds a client or a server to the respective list of participants
        /// </summary>
        /// <param name="username"></param>
        public void addParticipant(string username, string port)
        {
            if (username.StartsWith("SERVER-"))
            {
                if (mServers.ContainsKey(username))
                {
                    //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                    //                            new Object[] { "Already connected to server " + username });

                    addErrorMessage(username, "Already connected to server " + username);
                    return;
                }
                string exeName = "\\DataServer\\bin\\Release\\DataServer.exe";

                executeParticipant(username, port, exeName);
                
            }
            else if (username.CompareTo("CENTRAL") == 0)
            {
                if (mCDir != null)
                {
                    //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                    //                            new Object[] { "Already connected to central directory " + centralURL });
                    addErrorMessage(username, "Already connected to central directory " + username);
                    return;
                }

                string exeName = "\\CentralDirectory\\bin\\Release\\CentralDirectory.exe";
                executeParticipant(username, port, exeName);
            }
            else
            {
                if (mClients.ContainsKey(username)) 
                {
                    //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                    //                            new Object[] { "Already connected to client " + username });
                    addErrorMessage(username, "Already connected to client " + username);
                    return;
                }

                string exeName = "\\Client\\bin\\Release\\Client.exe";
                executeParticipant(username, port, exeName);
            } 
        }

        /// <summary>
        /// Executes the process corresponding to a participant
        /// </summary>
        /// <param name="username"></param>
        /// <param name="port"></param>
        /// <param name="exeFile"></param>
        public void executeParticipant(string username, string port, string exeFile)
        {
            try
            {
                if(username.StartsWith("SERVER-"))
                {
                    Process.Start(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName + exeFile,
                                username + " " + port + " " + centralURL);
                    IServer server = (IServer)Activator.GetObject(typeof(IServer), "tcp://localhost:" + port + "/" + username);

                    if (server == null)
                    {
                        //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                        //                        new Object[] { "Could not get participant object for participant with username: " + username + " on port " + port });
                        addErrorMessage(username, "Could not get participant object for participant with username: " + username + " on port " + port);
                        return;
                    }
                    server.registerInCentralDirectory();
                    mServers.Add(username, server);
                    //mWindow.mParticipants.Invoke(new DelAddParticipant(mWindow.mParticipants.addServer), new Object[] { username });
                    //Thread.Sleep(1000);
                }
                else if(username.CompareTo("CENTRAL") == 0)
                {
                    Process.Start(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName + exeFile,
                                username + " " + port);
                    centralURL = "tcp://localhost:" + port + "/" + username;
                    ICentralDir central = (ICentralDir)Activator.GetObject(typeof(ICentralDir), centralURL);

                    if (central == null)
                    {
                        //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                        //                        new Object[] { "Could not get participant object for participant with username: " + username + " on port " + port });
                        addErrorMessage(username, "Could not get participant object for participant with username: " + username + " on port " + port);
                        return;
                    }
                    mCDir = central;
                    //Thread.Sleep(1000);
                }
                else
                {
                    Process.Start(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName + exeFile,
                                  username + " " + port + " " + centralURL);
                    IClient client = (IClient)Activator.GetObject(typeof(IClient), "tcp://localhost:" + port + "/" + username);

                    if (client == null)
                    {
                        //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                        //                        new Object[] { "Could not get participant object for participant with username: " + username + " on port " + port });
                        addErrorMessage(username, "Could not get participant object for participant with username: " + username + " on port " + port);
                        return;
                    }

                    client.registerInCentralDirectory();
                    mClients.Add(username, client);
                    //mWindow.mParticipants.Invoke(new DelAddParticipant(mWindow.mParticipants.addClient), new Object[] { username });
                    //Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { "Could not create participant - " + ex.Message });
                addErrorMessage(username, "Could not create participant - " + ex.GetBaseException().Message);
            }
            /*catch (ObjectDisposedException ex)
            {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { "Could not create participant - " + ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { "Could not create participant - " + ex.Message });
            }
            catch (RemoteException ex)
            {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { "Could not create participant - " + ex.getMessage() });
            }
            catch (IOException ex)
            {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { "Could not create participant - " + ex.GetBaseException().Message });
            }
            catch (SocketException ex)
            {
                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                //                            new Object[] { "Could not create participant - " + ex.Message });
            }
            catch (TimeoutException ex)
            {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { "Could not create participant - " + ex.Message });
            }
            catch (Win32Exception ex)
            {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { "Could not create participant - " + ex.Message });
            }
            catch (UriFormatException ex)
            {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { "Could not get participant - " + ex.Message });
            }*/
        }

        /// <summary>
        /// Executes an asynchronous get operation on a client
        /// </summary>
        /// <param name="username"></param>
        /// <param name="millis"></param>
        public bool wait(string username, int millis)
        {
            try
            {
                IClient client;
                if (mClients.TryGetValue(username, out client) && client != null)
                {
                    client.wait(millis);
                    return true;
                }

                //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage), new Object[] { "Invalid username " + username });
                addErrorMessage(username, "Invalid username " + username);
                return false;

            } catch (Exception ex) {
                addErrorMessage(username, ex.GetBaseException().Message + " when calling DUMP at line " + fileLine);
                return false;
            }
            /*catch (SocketException ex)
            {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " when calling DUMP at line " + fileLine });
                return false;
            }
            catch (TimeoutException ex)
            {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " DUMP at line " + fileLine });
                return false;
            }
            catch (RemotingException ex)
            {
                mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage),
                                            new Object[] { ex.Message + " when calling DUMP at line " + fileLine });
                return false;
            }*/
        }

        /// <summary>
        /// Tells the server to simulate a failure (simulation of fail-stop model)
        /// </summary>
        /// <param name="username"></param>
        public bool tellServerToFail(string username)
        {
            IServer server;
            if (mServers.TryGetValue(username, out server) && server != null)
            {
                try
                {
                    server.Fail();
                    //mWindow.mParticipants.Invoke(new DelRemParticipant(mWindow.mParticipants.remServer), new Object[] { username });
                    Thread.Sleep(1000);
                    return true;
                } catch(Exception ex) {
                    addErrorMessage(username, ex.GetBaseException().Message + " DISCONNECT at line " + fileLine);
                    return false;
                }
            }

            //mWindow.Invoke(new DelErrorMsg(mWindow.showErrorMessage), new Object[] { "Invalid username " + username });
            addErrorMessage(username, "Invalid username " + username);
            return false;
        }

        /// <summary>
        /// Stops the execution of all participants
        /// </summary>
        public void exit()
        {
            foreach(string key in mClients.Keys)
            {
                try
                {
                    mClients[key].exit();
                    //mWindow.mParticipants.Invoke(new DelRemParticipant(mWindow.mParticipants.remServer), new Object[] { key });
                } catch (SocketException) { 
                }
                catch (IOException) {
                }
            }
            mClients.Clear();

            foreach(string key in mServers.Keys)
            {
                try
                {
                    mServers[key].exit();
                    //mWindow.mParticipants.Invoke(new DelRemParticipant(mWindow.mParticipants.remClient), new Object[] { key });
                } catch (SocketException) {
                } catch(IOException) {
                }
            }
            mServers.Clear();

            if (mCDir != null)
            {
                try
                {
                    mCDir.exit();
                } catch (SocketException) {
                } catch (IOException) {
                }
            }
            mCDir = null;
            centralURL = null;
        }

        /// <summary>
        /// Adds an error message to the form output message box
        /// </summary>
        /// <param name="username"></param>
        /// <param name="msg"></param>
        public void addErrorMessage(string username, string msg)
        {
            mWindow.mLog.Invoke(new DelAddErrorMessage(mWindow.mLog.addErrorMessage), new Object[] { username, msg });
        }

        public StreamReader FileStream { set { mFileStream = value; isLoaded = true; } }
        public string FileName { set { mFileName = value; } }
        public int FileLine { get { return fileLine; } set { fileLine = value; } }
        public string FileDirectory { set { mFileDirectory = value; } }
        public bool IsLoaded { get { return isLoaded; } set { isLoaded = value; } }
    }
}
