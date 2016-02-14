using System;
using IPADITable;
using System.Linq;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.Remoting;
using System.Net.Sockets;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;

namespace DataServer
{
    public class Server : MarshalByRefObject, IServer
    {
        //Number of maximum versions of a key
        private const int K = 2;
        //Central Directory URL
        public readonly string CDIR_URL;
        //Server URL
        private string mURL;
        //The server channel
        TcpChannel mServerChannel;
        //Indicates if a server was told to simulate failure or not
        private bool mFailed = false;
        //Transaction Manager
        private TransactionManager mTransactionManager;
        //Routing manager to route messages to other servers
        private NodeRouter mNodeRouter;
        //The key Manager
        private KeyManager mKeyManager;
        //Server thread to perform some operations like update replicas
        private ServerThread mServerThread;
        //Activator object for the central directory
        public static ICentralDir mCentralDir;

        public Server(string username, string port, string cDirUrl)
        {
            IDictionary prop = new Hashtable();
            prop["name"] = "ServerChannel." + username;
            prop["port"] = port;
            mServerChannel = new TcpChannel(prop, null, null);
            ChannelServices.RegisterChannel(mServerChannel, true);

            string serverName = username;
            RemotingServices.Marshal(this, serverName, typeof(IServer));
            String[] urls = ChannelServices.GetUrlsForObject(this);
            this.URL = urls[0];
            System.Console.WriteLine("Server instantiated with name " + serverName + " " + urls[0]);

            try
            {
                CDIR_URL = cDirUrl;
                mCentralDir = (ICentralDir)Activator.GetObject(typeof(IClient), CDIR_URL);

                if (mCentralDir == null)
                {
                    System.Console.WriteLine("Cannot get central directory object for " + CDIR_URL);
                    throw new RemoteException("Cannot get central directory object for " + CDIR_URL);
                }

            } catch (SocketException) {
                // throw new RemoteException("Could not connect to central directory: central directory url " + CDIR_URL);
                System.Console.WriteLine("Could not connect to central directory: central directory url " + CDIR_URL);
                Environment.Exit(0);
            }

            mKeyManager = new KeyManager(K);
            mTransactionManager = new TransactionManager(this, mKeyManager, K);
            mNodeRouter = new NodeRouter(mURL, mKeyManager, this);
            mServerThread = new ServerThread(mNodeRouter, mKeyManager);
        }

        #region IServer Implementation

        public void receiveNetworkMembership(List<Dictionary<UInt64, string>> topology)
        {
            if (mFailed)
            {
                System.Console.WriteLine("NETWORK Server Failed");
                //THROW EXCEPTION that indicates server has failed
                throw new RemoteServerFailedException("Could not execute action on server. Server has failed. Server Url " + mURL, mURL);
            }

            mNodeRouter.Clients = topology.ElementAt(0);
            mNodeRouter.Servers = topology.ElementAt(1);

            mNodeRouter.printParticipants();
        }

        /// <summary>
        /// Performs a put operation on the specified key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="txID"></param>
        public void put(string key, string value, long txID)
        {
            System.Console.WriteLine("Enter put operation");
            //Will need to be a thread so server is not stuck doing this
            if (mFailed)
            {
                System.Console.WriteLine("Put Server failed");
                //THROW EXCEPTION
                throw new RemoteServerFailedException("Could not execute action on server. Server has failed. Server Url " + mURL, mURL);
            }
            //change to go to transaction context and update there
            System.Console.WriteLine("Executing put operation from transaction " + txID + " on key " + key + " with value " + value);
            mTransactionManager.putOperation(txID, key, value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="txID"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        public Pair<string, long> get(string key, long txID)
        {
            //Will need to be a thread so server is not stuck doing this
            if (mFailed)
            {
                //Call server failure response
                System.Console.WriteLine(" GET Server Failed");
                //throw failed
                throw new RemoteServerFailedException("Could not execute action on server. Server has failed. Server Url " + mURL, mURL);
            }

            System.Console.WriteLine("Executing get operation from transaction " + txID + " on key " + key);
            return mTransactionManager.getOperation(txID, key);
        }

        public void commit(long txID)
        {
            System.Console.WriteLine("COMMIT ON SERVER");
            if (mFailed)
            {
                System.Console.WriteLine("COMMIT Server Failed");
                //THROW EXCEPTION that indicates server has failed
                throw new RemoteServerFailedException("Could not execute action on server. Server has failed. Server Url " + mURL, mURL);
            }
            //mTransactionManager.setTransactionState(txID, TransactionStates.TENTATIVELYCOMMITED); //Not sure if correct
            ConcurrentDictionary<string, List<Pair<string, long>>> values = mTransactionManager.commitTransaction(txID);
            Thread thread = new Thread(new ThreadStart(() => mServerThread.update(values)));
            thread.Start();
        }

        /// <summary>
        /// Aborts a transaction
        /// </summary>
        /// <param name="txID"></param>
        public void abort(long txID)
        {
            if (mFailed == true)
            {
                System.Console.WriteLine("ABORT Server Failed");
                //THROW EXCEPTION that indicates server has failed
                throw new RemoteServerFailedException("Could not execute action on server. Server has failed. Server Url " + mURL, mURL);
            }
            try
            {
                mTransactionManager.abortTransaction(txID);
            }
            finally
            { System.Console.WriteLine("RETURNING FROM ABORT"); }
        }

        /// <summary>
        /// Checks if the server is ready to commit a transaction
        /// </summary>
        /// <param name="txID"></param>
        /// <returns></returns>
        public bool prepare(long txID)
        {
            if (mFailed)
            {
                System.Console.WriteLine("Server Failed");
                //THROW EXCEPTION that indicates server has failed
                throw new RemoteServerFailedException("Could not execute action on server. Server has failed. Server Url " + mURL, mURL);
            }
            return mTransactionManager.prepare(txID);
        }

        /// <summary>
        /// Get all values for a key
        /// </summary>
        /// <param name="key"></param>
        public List<Tuples<string, string, string, long, TransactionStates>> getAll(string key)
        {
            System.Console.WriteLine("GETALL " + key);
            return mTransactionManager.getAll(key);
        }

        /// <summary>
        /// To instruct the server to simulate failure
        /// </summary>
        public void Fail()
        {
            System.Console.WriteLine("SERVER FAILED");
            mServerThread.stopTimedEvent();
            mFailed = true;
        }

        /// <summary>
        /// To receive updates from the server which we replicate our keys or to receive updates to replicate keys
        /// </summary>
        /// <param name="replicatedKeys"></param>
        public void updateKeyValues(ConcurrentDictionary<string, List<Pair<string, long>>> toReplicateKeys)
        {
            if (mFailed == true)
            {
                System.Console.WriteLine("UPDATE Server Failed");
                //THROW EXCEPTION that indicates server has failed
                throw new RemoteServerFailedException("Could not execute action on server. Server has failed. Server Url " + mURL, mURL);
            }
            System.Console.WriteLine("Receive update values request");
            foreach (string key in toReplicateKeys.Keys)
            {
                foreach (Pair<string, long> pair in toReplicateKeys[key])
                {
                    System.Console.WriteLine("RECEIVED KEY " + key + " " + pair.Value);
                }
            }
            mKeyManager.addAllKeyValues(toReplicateKeys);
        }

        /// <summary>
        /// Stops executing
        /// </summary>
        public void exit()
        {
            mServerThread.stopTimedEvent();
            ChannelServices.UnregisterChannel(mServerChannel);
            Environment.Exit(0);
        }

        #endregion

        #region Server Functions

        /// <summary>
        /// The server tries to register in the central directory
        /// </summary>
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
                    if (mCentralDir.registerServer(mURL)) { return; }
                    throw new RemoteException("Could not create server: " + mURL);
                } catch (SocketException ex) {
                    System.Console.WriteLine("Could not call method registerServer: " + ex.Message);
                    throw ex;
                } catch (TimeoutException ex) {
                    System.Console.WriteLine("Could not get response: " + ex.Message);
                    throw ex;
                }
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
        /// Tries to unregister a server
        /// </summary>
        /// <param name="url"></param>
        public void deRegisterServer(string url)
        {
            if (mCentralDir == null)
            {
                System.Console.WriteLine("Cannot get central directory object");
                throw new RemoteException("Cannot get central directory object");
            }
            else
            {
                try
                {
                    mCentralDir.deregisterServer(url);
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("EXCEPTION " + e.Message);
                }
            }
        }

        #endregion

        public string URL { set { mURL = value; } get { return mURL; } }
    }
}
