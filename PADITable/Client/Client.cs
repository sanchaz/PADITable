using System;
using IPADITable;
using System.Linq;
using System.Threading;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Remoting;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;

namespace Client
{
    //Delegate to make wait asynchronous calls
    public delegate void WaitDelegate(int millis);

    public class Client : MarshalByRefObject, IClient
    {
        //Length of binary keys in server chord ring
        private const int BINARY_KEY_LENGTH = 6;
        //Central Directory URL
        public readonly string CDIR_URL;
        //The client url
        private string mURL;
        //The channel used for communication
        private TcpChannel mClientChannel;
        //The central directory
        private ICentralDir mCentralDir;
        //The message router
        private NodeRouter mNodeRouter;
        //The context of the active transaction
        private TransactionContext mActiveTransaction;
        //The register manager
        private RegisterManager mRegisterManager;
        //The thread which is used to perform comunication with servers
        private ClientThread mClientThread;
        //When the thread is "sleeping"
        private static bool isSleep;

        public Client(string username, string port, string cDirUrl)
        {
            //With 0 as port the remoting services will chose an available port automatically
            IDictionary prop = new Hashtable();
            prop["name"] = "ClientChannel." + username;
            prop["port"] = port;
            mClientChannel = new TcpChannel(prop, null, null);
            ChannelServices.RegisterChannel(mClientChannel, true);

            string clientName = username;
            RemotingServices.Marshal(this, clientName, typeof(IClient));

            String[] urls = ChannelServices.GetUrlsForObject(this);

            this.URL = urls[0];

            System.Console.WriteLine("Client instantiated with name " + clientName + " and URL " + urls[0]);

            try
            {
                CDIR_URL = cDirUrl;
                mCentralDir = (ICentralDir)Activator.GetObject(typeof(IClient), CDIR_URL);

                if (mCentralDir == null)
                {
                    System.Console.WriteLine("Cannot get central directory object for " + CDIR_URL);
                    throw new RemoteException("Cannot get central directory object for " + CDIR_URL);
                }
            }
            catch (SocketException)
            {
                throw new RemoteException("Could not connect to central directory: central directory url " + CDIR_URL);
            }   

            System.Console.WriteLine("Central Directory with URL " + CDIR_URL);

            isSleep = false;
            mRegisterManager = new RegisterManager();
            mNodeRouter = new NodeRouter();
            mClientThread = new ClientThread();

            System.Console.WriteLine("Register Manager created");
            System.Console.WriteLine("Node Router created");
            System.Console.WriteLine("Client Thread created");

            mActiveTransaction = new TransactionContext();
            System.Console.Write("Transaction Context created with id: " + mActiveTransaction.TxID + " valuesOfKey: ");
            mActiveTransaction.printKeyValues();
            System.Console.WriteLine("state: " + mActiveTransaction.TransactionState);
        }

        #region ICLIENT IMPLEMENTATION

        /// <summary>
        /// Updates the netwrok view of the client
        /// </summary>
        /// <param name="topology"> A list with the dictionaries which contain the participants(clients and servers)</param>
        public void receiveNetworkMembership(List<Dictionary<UInt64, string>> topology)
        {
            mNodeRouter.Clients = topology.ElementAt(0);
            mNodeRouter.Servers = topology.ElementAt(1);
            
            mNodeRouter.printParticipants();
        }

        /// <summary>
        /// the client initiates a new transaction
        /// </summary>
        public void  beginTransaction()
        {
            while (isSleep) ;
            if (mCentralDir == null)
            {
                System.Console.WriteLine("Could not get object for central directory with URL: " + CDIR_URL);
            }
            else if (mActiveTransaction.Active)
            {
                System.Console.WriteLine("beginTransaction: There is already an active transaction");
                throw new RemoteException("beginTransaction: There is already an active transaction");
            }
            else
            {
                try
                {
                    long txID = mCentralDir.getTransactionID(mURL);
                    mActiveTransaction.TxID = txID;
                    mActiveTransaction.ValuesOfKey = new Dictionary<string, List<string>>();
                    mActiveTransaction.Servers = new Dictionary<string, string>();
                    mActiveTransaction.OldVals = mRegisterManager.Registers.Values.ToList();
                    mActiveTransaction.TransactionState = TransactionStates.INITIATED;
                    mActiveTransaction.Active = true;
                    System.Console.Write("New active transaction with id: " + mActiveTransaction.TxID + " valuesOfKey: ");
                    mActiveTransaction.printKeyValues();
                    System.Console.WriteLine("state: " + mActiveTransaction.TransactionState);

                } catch (SocketException ex) {
                    System.Console.WriteLine("Could not call method beginTransaction, " + ex.Message);
                    throw new RemoteException("beginTransaction: Could not get response, " + ex.Message);
                } catch (TimeoutException ex) {
                    System.Console.WriteLine("beginTransaction: Could not get response, " + ex.Message);
                    throw new RemoteException("beginTransaction: Could not get response, " + ex.Message);
                }
            }
        }

        /// <summary>
        /// the client assigns the given value to the given register,
        /// where register is a value in the range 1...10 and value is a string
        /// </summary>
        /// <param name="regVal"></param>
        /// <param name="value"></param>
        public void  storeRegisterValue(int regVal, string value)
        {
            while (isSleep) ;
            mRegisterManager.setRegisterValue(regVal, value);
        }

        /// <summary>
        /// the client executes a put operation in the specied key using the value stored in specied register
        /// </summary>
        /// <param name="regVal"></param>
        /// <param name="key"></param>
        public void  putRegisterKey(int regVal, string key)
        {
            while (isSleep) ;
            if (mActiveTransaction.Active)
            {
                string registerValue;
                string serverUrl;
                if (mActiveTransaction.Servers.TryGetValue(key, out serverUrl))
                { }
                else
                {
                    serverUrl = mNodeRouter.getServerToSend(key);
                }
                try
                {
                    System.Console.WriteLine("PUT " + serverUrl);
                    registerValue = mRegisterManager.getRegisterValue(regVal);
                    IServer server = (IServer)Activator.GetObject(typeof(IServer), serverUrl);
                    if (server == null)
                    {
                        System.Console.WriteLine("Could not get server with url: " + serverUrl);
                        System.Console.WriteLine("SEND ABORT");
                        sendAbortToAllMinus(null);
                        restoreRegisters();
                        return;
                    }
                    server.put(key, registerValue, mActiveTransaction.TxID);
                    mActiveTransaction.addServer(key, serverUrl);
                    System.Console.WriteLine("Put operation complete on server " + serverUrl);
                } catch (SocketException ex) {
                    System.Console.WriteLine("Could not call method putRegisterKey: " + ex.Message);
                    System.Console.WriteLine("SEND ABORT");
                    sendAbortToAllMinus(null);
                    restoreRegisters();
                    throw new RemoteException("putRegisterKey: Could not call method: " + ex.Message);
                } catch (TimeoutException ex) {
                    System.Console.WriteLine("putRegisterKey: Could not get response, " + ex.Message);
                    System.Console.WriteLine("SEND ABORT");
                    sendAbortToAllMinus(null);
                    restoreRegisters();
                    throw new RemoteException("putRegisterKey: Could not get response, " + ex.Message);
                } catch (RemoteException ex) {
                    mActiveTransaction.addServer(key, serverUrl);
                    System.Console.WriteLine("putRegisterKey: " + ex.Message);
                    System.Console.WriteLine("SEND ABORT");
                    sendAbortToAllMinus(null);
                    restoreRegisters();
                    throw ex;
                } catch(AbortException ex) {
                    mActiveTransaction.addServer(key, serverUrl);
                    System.Console.WriteLine("putRegisterKey: " + ex.Message);
                    System.Console.WriteLine("SEND ABORT");
                    sendAbortToAllMinus(null);
                    restoreRegisters();
                    throw ex;
                } catch (RemoteServerFailedException ex) {
                    if (deRegisterServer(ex.URL()))
                    {
                        Thread.Sleep(5000);
                        mActiveTransaction.removeServer(key);
                        putRegisterKey(regVal, key);
                    }
                }
            }
            else if(!mActiveTransaction.Active)
            {
                System.Console.WriteLine("putRegisterKey: There is no active transaction please initiate one");
                throw new RemoteException("putRegisterKey: There is no active transaction please initiate one");
            }
        }

        /// <summary>
        /// the client executes a get operation and stores the resulting value in the given register
        /// </summary>
        /// <param name="regVal"></param>
        /// <param name="key"></param>
        public void  getRegisterKey(int regVal, string key)
        {
            while (isSleep) ;
            if (mActiveTransaction.Active)
            {
                string serverUrl;
                if (mActiveTransaction.Servers.TryGetValue(key, out serverUrl))
                { }
                else
                {
                    serverUrl = mNodeRouter.getServerToSend(key);
                }

                try
                {
                    System.Console.WriteLine("GET " + serverUrl);
                    IServer server = (IServer)Activator.GetObject(typeof(IServer), serverUrl);
                    if (server == null)
                    { 
                        System.Console.WriteLine("Could not get server with url: " + serverUrl);
                        System.Console.WriteLine("SEND ABORT");
                        sendAbortToAllMinus(null);
                        restoreRegisters();
                        return;
                    }
                    Pair<string, long> result = server.get(key, mActiveTransaction.TxID);
                    if (result == null)
                    {
                        mActiveTransaction.addServer(key, serverUrl);
                        System.Console.WriteLine("SEND ABORT");
                        sendAbortToAllMinus(null);
                        restoreRegisters();
                        System.Console.WriteLine("Could not perform get operation on server " + serverUrl + " using key " + key);
                        throw new RemoteException("Could not perform get operation on server " + serverUrl + " using key " + key);
                    }
                    else if (mRegisterManager.setRegisterValue(regVal, result.Value))
                    {
                        mActiveTransaction.addServer(key, serverUrl);
                        System.Console.WriteLine("Get operation complete on server " + serverUrl);
                    }
                    else
                    {
                        mActiveTransaction.addServer(key, serverUrl);
                        sendAbortToAllMinus(null);
                        System.Console.WriteLine("Could not perform get operation nor set a new value for register with the value got from key: " + key);
                        throw new RemoteException("Could not perform get operation nor set a new value for register with the value got from key: " + key);
                    }
                } catch (SocketException ex) {
                    System.Console.WriteLine("Could not call method getRegisterKey: " + ex.Message);
                    System.Console.WriteLine("SEND ABORT");
                    sendAbortToAllMinus(null);
                    restoreRegisters();
                    throw new RemoteException("getRegisterKey: Could not call method: " + ex.Message);
                } catch (TimeoutException ex) {
                    System.Console.WriteLine("getRegisterKey: Could not get response, " + ex.Message);
                    System.Console.WriteLine("SEND ABORT");
                    sendAbortToAllMinus(null);
                    restoreRegisters();
                    throw new RemoteException("getRegisterKey: Could not get response, " + ex.Message);
                } catch (RemoteException ex) {
                    mActiveTransaction.addServer(key, serverUrl);
                    System.Console.WriteLine("getRegisterKey: " + ex.Message);
                    System.Console.WriteLine("SEND ABORT");
                    sendAbortToAllMinus(null);
                    restoreRegisters();
                    throw ex;
                } catch (RemoteServerFailedException ex) {
                    if (deRegisterServer(ex.URL()))
                    {
                        Thread.Sleep(5000);
                        mActiveTransaction.removeServer(key);
                        getRegisterKey(regVal, key);
                    }
                } catch (AbortException ex) {
                    mActiveTransaction.addServer(key, serverUrl);
                    System.Console.WriteLine("getRegisterKey: " + ex.Message);
                    System.Console.WriteLine("SEND ABORT");
                    sendAbortToAllMinus(null);
                    restoreRegisters();
                    throw ex;
                }
            }
            else if(!mActiveTransaction.Active)
            {
                System.Console.WriteLine("getRegisterKey: There is no active transaction please initiate one");
                throw new RemoteException("getRegisterKey: There is no active transaction please initiate one");
            }
        }

        /// <summary>
        /// the client executes a blind put operation in the specied key using the specied
        /// value (which does not depend of any previous read operation)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void  putValKeyValue(string key, string value)
        {
            while (isSleep) ;
            if (mActiveTransaction.Active)
            {
                string serverUrl;

                if (mActiveTransaction.Servers.TryGetValue(key, out serverUrl))
                { }
                else
                {
                    serverUrl = mNodeRouter.getServerToSend(key);
                }

                try
                {
                    System.Console.WriteLine("PutValKeyValue " + serverUrl);
                    IServer server = (IServer)Activator.GetObject(typeof(IServer), serverUrl);
                    if (server == null) 
                    {
                        System.Console.WriteLine("Could not get server with url: " + serverUrl);
                        System.Console.WriteLine("SEND ABORT");
                        sendAbortToAllMinus(null);
                        restoreRegisters();
                        return;
                    }
                    server.put(key, value, mActiveTransaction.TxID);
                    mActiveTransaction.addServer(key, serverUrl);
                    System.Console.WriteLine("PutValKeyValue operation complete on server " + serverUrl);
                } catch (SocketException ex) {
                    System.Console.WriteLine("Could not call method putValKeyValue: " + ex.Message);
                    System.Console.WriteLine("SEND ABORT");
                    sendAbortToAllMinus(null);
                    restoreRegisters();
                    throw new RemoteException("putValKeyValue: Could not call method," + ex.Message);
                } catch (TimeoutException ex) {
                    System.Console.WriteLine("putValKeyValue: Could not get response, " + ex.Message);
                    System.Console.WriteLine("SEND ABORT");
                    sendAbortToAllMinus(null);
                    restoreRegisters();
                    throw new RemoteException("putValKeyValue: Could not get response, " + ex.Message);
                } catch (RemoteException ex) {
                    mActiveTransaction.addServer(key, serverUrl);
                    System.Console.WriteLine("putValKeyValue: " + ex.Message);
                    System.Console.WriteLine("SEND ABORT");
                    sendAbortToAllMinus(null);
                    restoreRegisters();
                    throw ex;
                } catch (RemoteServerFailedException ex) {
                    if (deRegisterServer(ex.URL()))
                    {
                        Thread.Sleep(5000);
                        mActiveTransaction.removeServer(key);
                        putValKeyValue(key, value);
                    }
                } catch (AbortException ex) {
                    mActiveTransaction.addServer(key, serverUrl);
                    System.Console.WriteLine("putValKeyValue: " + ex.Message);
                    System.Console.WriteLine("SEND ABORT");
                    sendAbortToAllMinus(null);
                    restoreRegisters();
                    throw ex;
                }

            }
            else if (!mActiveTransaction.Active)
            {
                System.Console.WriteLine("putValKeyValue: There is no active transaction please initiate one");
                throw new RemoteException("putKeyVal: There is no active transaction please initiate one");
            }
        }

        /// <summary>
        /// the client changes the string stored in the specied register to lowercase characters
        /// </summary>
        /// <param name="regVal"></param>
        public void  toLowerRegister(int regVal)
        {
            while (isSleep) ;
            mRegisterManager.setRegisterToLower(regVal);
        }

        /// <summary>
        /// the client changes the string stored in the specied register to uppercase characters
        /// </summary>
        /// <param name="regVal"></param>
        public void  toUpperRegister(int regVal)
        {
            while (isSleep) ;
            mRegisterManager.setRegisterToUpper(regVal);
        }

        /// <summary>
        /// the client concatenates the string contained in register1 with the string contained in register2
        /// and stores the result in register
        /// </summary>
        /// <param name="regVal1"></param>
        /// <param name="regVal2"></param>
        public void  concatRegisters(int regVal1, int regVal2)
        {
            while (isSleep) ;
            mRegisterManager.concatRegisters(regVal1, regVal2);
        }

        /// <summary>
        /// the client returns to the puppet master an array with the contents of all registers
        /// </summary>
        public void  commitTransaction()
        {
            while (isSleep) ;
            if (!mActiveTransaction.Active)
            {
                System.Console.WriteLine("No active transaction running");
                throw new RemoteException("No active transaction running");
            }

            if (prepare())
            {
                mActiveTransaction.TransactionState = TransactionStates.TENTATIVELYCOMMITED;

                List<string> servers = new List<string>();

                foreach (string server in mActiveTransaction.Servers.Values)
                {
                    if (servers.Contains(server)) { continue; }
                    else { servers.Add(server); }
                }

                foreach (string url in servers)
                {
                    try
                    {
                        IServer server = (IServer)Activator.GetObject(typeof(IServer), url);
                        server.commit(mActiveTransaction.TxID);
                    } catch (RemoteServerFailedException ex) {
                        System.Console.WriteLine("Server " + ex.URL() + " failed");
                        deRegisterServer(ex.URL());
                    } catch (RemoteException ex) {
                        System.Console.WriteLine(ex.getMessage());
                    } catch(SocketException) {
                        System.Console.WriteLine("Could not commit transaction " + mActiveTransaction.TxID);
                        deRegisterServer(url);
                    }
                }
                mActiveTransaction.TransactionState = TransactionStates.COMMITED;
                mActiveTransaction.Active = false;
            }
            else if (mActiveTransaction.Servers.Count == 0)
            {
                mActiveTransaction.TransactionState = TransactionStates.COMMITED;
                mActiveTransaction.Active = false;
            }
            else
            {
                List<string> values = mActiveTransaction.OldVals;
                for (int i = 0; i < values.Count; i++)
                {
                    mRegisterManager.setRegisterValue((i + 1), values.ElementAt(i));
                }
                mActiveTransaction.TransactionState = TransactionStates.ABORTED;
                mActiveTransaction.Active = false;
                throw new AbortException("Could not get conditions to commit active transaction: txID " + mActiveTransaction.TxID
                                         + " transaction aborted");
            }
        }

        /// <summary>
        /// Waits for millis milliseconds
        /// </summary>
        /// <param name="millis"></param>
        public void wait(int millis)
        {
            Wait wait = new Wait();
            WaitDelegate wDel = new WaitDelegate(wait.waitFor);
            System.Console.WriteLine("CALLING WAIT " + millis);
            isSleep = true;
            wDel.BeginInvoke(millis, new AsyncCallback(CallBackMethod), wDel);
        }

        /// <summary>
        /// Two-phase commit prepare phase so the transaction can be certainly commited or aborted
        /// </summary>
        /// <returns></returns>
        public bool prepare()
        {
            if (mActiveTransaction.Servers.Count > 0)
            {
                System.Console.WriteLine("DENTRO PREPARE");
                List<bool> prepareAnswers = new List<bool>(mActiveTransaction.Servers.Count);
                List<string> servers = new List<string>();

                foreach(string server in mActiveTransaction.Servers.Values)
                {
                    if(servers.Contains(server)) { continue; }
                    else { servers.Add(server); }
                }

                foreach (string url in servers)
                {
                    System.Console.WriteLine("DENTRO PREPARE FOREACH " + url);
                    try
                    {
                        IServer server = (IServer)Activator.GetObject(typeof(IServer), url);
                        bool prepare = server.prepare(mActiveTransaction.TxID);
                        if (!prepare)
                        {
                            System.Console.WriteLine("DENTRO PREPARE IF SERVER PREPARE");
                            sendAbortToAllMinus(url);
                            return false;
                        }
                    }
                    catch (RemoteServerFailedException ex)
                    {
                        System.Console.WriteLine("Server " + ex.URL() + " has failed");
                        sendAbortToAllMinus(url);
                        deRegisterServer(ex.URL());
                        return false;
                    }
                    catch (RemoteException ex)
                    {
                        System.Console.WriteLine(ex.getMessage());
                        sendAbortToAllMinus(null);
                        return false;
                    }
                    catch (SocketException)
                    {
                        System.Console.WriteLine("Could not reach server " + url);
                        sendAbortToAllMinus(url);
                        mCentralDir.deregisterServer(url);
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sends abort to all servers but the one with string url
        /// </summary>
        /// <param name="url"></param>
        public void sendAbortToAllMinus(string url)
        {
            if (url != null)
            {
                List<string> servers = new List<string>();
                foreach (string server in mActiveTransaction.Servers.Values)
                {
                    if (servers.Contains(server)) { continue; }
                    else { servers.Add(server); }
                }

                foreach (string otherURL in servers)
                {
                    if (otherURL != url)
                    {
                        try
                        {
                            IServer server = (IServer)Activator.GetObject(typeof(IServer), otherURL);
                            server.abort(mActiveTransaction.TxID);
                            mActiveTransaction.TransactionState = TransactionStates.ABORTED;
                            mActiveTransaction.Active = false;
                        }
                        catch (RemoteServerFailedException ex)
                        {
                            System.Console.WriteLine("Server " + ex.URL() + " has failed");
                            deRegisterServer(url);
                        }
                        catch (RemoteException ex)
                        {
                            System.Console.WriteLine(ex.getMessage());
                            break;
                        }
                        catch (SocketException)
                        {
                            System.Console.WriteLine("Could not reach server " + url);
                            sendAbortToAllMinus(otherURL);
                            deRegisterServer(url);
                        }

                    }
                }
            }
            else
            {
                List<string> servers = new List<string>();
                foreach (string server in mActiveTransaction.Servers.Values)
                {
                    if (servers.Contains(server)) { continue; }
                    else { servers.Add(server); }
                }

                foreach (string otherURL in servers)
                {
                    try
                    {
                        IServer server = (IServer)Activator.GetObject(typeof(IServer), otherURL);
                        server.abort(mActiveTransaction.TxID);
                        mActiveTransaction.TransactionState = TransactionStates.ABORTED;
                        mActiveTransaction.Active = false;
                    }
                    catch (RemoteServerFailedException ex)
                    {
                        System.Console.WriteLine("Server " + ex.URL() + " has failed");
                        deRegisterServer(url);
                    }
                    catch (RemoteException ex)
                    {
                        System.Console.WriteLine(ex.getMessage());
                        break;
                    }
                    catch (SocketException)
                    {
                        System.Console.WriteLine("Could not reach server " + otherURL);
                        deRegisterServer(otherURL);
                    }
                }
            }

            System.Console.WriteLine("ABORT COMPLETE");
        }

        /// <summary>
        /// Sends the values of all register to the puppet master
        /// </summary>
        public List<string>  dump()
        {
            return new List<string>(mRegisterManager.Registers.Values);
        }

        /// <summary>
        /// Executes the commands listed in the file with fileName
        /// </summary>
        /// <param name="fileName"></param>
        public void exeScript(string fileName)
        {
            System.Console.WriteLine("Executing " + fileName);
            mClientThread.setClient(this);
            Thread thread = new Thread(new ThreadStart(() => mClientThread.execFile(fileName)));

            thread.Start();
        }

        /// <summary>
        /// Stops the execution
        /// </summary>
        public void exit()
        {
            ChannelServices.UnregisterChannel(mClientChannel);
            Environment.Exit(0);
        }

        /// <summary>
        /// Executes an instruction from a script
        /// </summary>
        /// <param name="instruction"></param>
        /// <returns></returns>
        public bool executeInstruction(string[] instruction)
        {
            switch (instruction[0])
            {
                case "BEGINTX":
                    try
                    {
                        beginTransaction();
                        return true;
                    }
                    catch (Exception ex) { System.Console.WriteLine(ex.Message); }
                    return false;

                case "STORE":
                    if (instruction.Length < 3)
                    {
                        return false;
                    }
                    try
                    {
                        storeRegisterValue(Int32.Parse(instruction[1]), instruction[2]);
                        return true;
                    }
                    catch (Exception ex) { System.Console.WriteLine(ex.Message); }
                    return false;

                case "PUT":
                    if (instruction.Length < 3)
                    {
                        return false;
                    }
                    try
                    {
                        putRegisterKey(Int32.Parse(instruction[1]), instruction[2]);
                        return true;
                    }
                    catch (Exception ex) { System.Console.WriteLine(ex.Message); }
                    return false;

                case "GET":
                    if (instruction.Length < 3)
                    {
                        return false;
                    }
                    try
                    {
                        getRegisterKey(Int32.Parse(instruction[1]), instruction[2]);
                        return true;
                    }
                    catch (Exception ex) { System.Console.WriteLine(ex.Message); }
                    return false;

                case "PUTVAL":
                    if (instruction.Length < 3)
                    {
                        return false;
                    }
                    try
                    {
                        putValKeyValue(instruction[1], instruction[2]);
                        return true;
                    }
                    catch (Exception ex) { System.Console.WriteLine(ex.Message); }
                    return false;

                case "TOLOWER":
                    if (instruction.Length < 2)
                    {
                        return false;
                    }
                    try
                    {
                        toLowerRegister(Int32.Parse(instruction[1]));
                        return true;
                    }
                    catch (Exception ex) { System.Console.WriteLine(ex.Message); }
                    return false;


                case "TOUPPER":
                    if (instruction.Length < 2)
                    {
                        return false;
                    }
                    try
                    {
                        toUpperRegister(Int32.Parse(instruction[1]));
                        return true;
                    }
                    catch (Exception ex) { System.Console.WriteLine(ex.Message); }
                    return false;

                case "CONCAT":
                    if (instruction.Length < 2)
                    {
                        return false;
                    }
                    try
                    {
                        concatRegisters(Int32.Parse(instruction[2]), Int32.Parse(instruction[3]));
                        return true;
                    }
                    catch (Exception ex) { System.Console.WriteLine(ex.Message); }
                    return false;

                case "COMMITTX":
                    try
                    {
                        commitTransaction();
                        return true;
                    }
                    catch (Exception ex) { System.Console.WriteLine(ex.Message); }
                    return false;

                default:
                    System.Console.WriteLine("Command not recognized: " + instruction[0]);
                    return false;
            }
        }

        #endregion

        #region CLIENT METHODS

        /// <summary>
        /// Tries to register a client in the central directory
        /// </summary>
        /// <returns></returns>
        public void registerInCentralDirectory()
        {
            if (mCentralDir == null)
            {
                System.Console.WriteLine("Could not get object for central directory with URL: " + CDIR_URL);
                throw new RemoteException("Could not get object for central directory with URL: " + CDIR_URL);
            }
            else
            {
                try
                {
                    if (mCentralDir.registerClient(mURL)) { return; }
                    throw new RemoteException("Could not create client: " + mURL);
                } catch (SocketException) {
                    System.Console.WriteLine("Could not call method registerClient: Central Directory is not running");
                    throw new RemoteException("Could not call method registerClient: Central Directory is not running");
                } catch (TimeoutException ex) {
                    System.Console.WriteLine("Could not get response: " + ex.Message);
                    throw ex;
                }

            }
        }

        /// <summary>
        /// Prints the values to console
        /// </summary>
        public void printRegisters()
        {
            mRegisterManager.print();
        }

        /// <summary>
        /// Tries to unregister a server
        /// </summary>
        /// <param name="url"></param>
        public bool deRegisterServer(string url)
        {
            if (mCentralDir == null)
            {
                System.Console.WriteLine("Cannot get central directory object for " + CDIR_URL);
                return false;
            }
            else
            {
                mCentralDir.deregisterServer(url);
                return true;
            }
        }

        /// <summary>
        /// So the lease never expires
        /// </summary>
        /// <returns></returns>
        public override object InitializeLifetimeService()
        {
            return null;
        }

        /// <summary>
        /// Restore the old values of registers
        /// </summary>
        public void restoreRegisters()
        {
            List<string> values = mActiveTransaction.OldVals;
            for (int i = 0; i < values.Count; i++)
            {
                mRegisterManager.setRegisterValue((i + 1), values.ElementAt(i));
            }
        }

        /// <summary>
        ///Sets the client URL 
        /// </summary>
        public string URL { set { mURL = value; } }

        /// <summary>
        /// The class which contains the asynchronous method to call
        /// </summary>
        class Wait
        {
            public void waitFor(int millis)
            {
                Thread.Sleep(millis);
                isSleep = false;
            }
        }

        /// <summary>
        /// Call back method when endInvoke is called on asynchronous call
        /// </summary>
        /// <param name="result"></param>
        void CallBackMethod(IAsyncResult result) { return; }

        #endregion
    }
}
