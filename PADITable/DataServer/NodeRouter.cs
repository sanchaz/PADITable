using System;
using IPADITable;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Security.Cryptography;

namespace DataServer
{
    public class NodeRouter
    {
        //The clients and the servers (ID's and URL's)
        private Dictionary<UInt64, string> mClients;
        private Dictionary<UInt64, string> mServers;
        //Length of binary keys in server chord ring
        private const UInt64 BINARY_KEY_LENGTH = 6;
        //this servers node number
        private UInt64 mID;
        //This server's url
        private string mURL;
        //indicates the last server node number that was acting as a replica
        private UInt64 mReplicaOfThis;
        //indicates the last server node number that was using this server as a replica
        private UInt64 mThisIsReplicaOf;
        //Object used on locks
        private Object thisReplicaObject;
        private Object thisIsReplicaObject;
        //Key manager to perform update operations on other servers
        private KeyManager mKeyManager;
        //The server to which this router belongs
        private Server mServer;

        public NodeRouter(string url, KeyManager kManager, Server server) 
        {
            mClients = new Dictionary<UInt64, string>();
            mServers = new Dictionary<UInt64, string>();
            mURL = url;
            mServer = server;
            thisReplicaObject = new Object();
            thisIsReplicaObject = new Object();

            mID = ParticipantID.get(url, BINARY_KEY_LENGTH);
            System.Console.WriteLine("MID ------- " + mID);

            mKeyManager = kManager;
        }

        /// <summary>
        /// Prints all the active participants in the network
        /// </summary>
        public void printParticipants()
        {
            foreach (string name in mClients.Values)
            {
                System.Console.WriteLine("CLIENTS ----- " + name);
            }

            foreach (string name in mServers.Values)
            {
                System.Console.WriteLine("SERVERS ----- " + name + " " + mServers.Count);
            }
        }

        /// <summary>
        /// Sets the server key
        /// </summary>
        public void setNewReplicas()
        {
            if (mServers.Count > 1)
            {
                for (int i = 0; i < mServers.Count; i++)
                {
                    //If it is one in the mddle of the list
                    if (mServers.ElementAt(i).Key == mID && i != 0 && i != (mServers.Count - 1))
                    {
                        mReplicaOfThis = mServers.ElementAt(i + 1).Key;
                        mThisIsReplicaOf = mServers.ElementAt(i - 1).Key;
                        System.Console.WriteLine("REPLICAS1 " + mID + " my " + mReplicaOfThis + " me " + mThisIsReplicaOf + " " + mServers.Count);
                        break;
                    }
                    //If it is the first on the list
                    else if (mServers.Keys.ElementAt(i) == mID && i == 0)
                    {
                        mReplicaOfThis = mServers.ElementAt(i + 1).Key;
                        mThisIsReplicaOf = mServers.ElementAt(mServers.Count - 1).Key;
                        System.Console.WriteLine("REPLICAS2 " + mID + " my " + mReplicaOfThis + " me " + mThisIsReplicaOf + " " + mServers.Count);
                        break;
                    }
                    //If it is the last on the list
                    else if (mServers.Keys.ElementAt(i) == mID && i == (mServers.Count - 1))
                    {
                        mReplicaOfThis = mServers.ElementAt(0).Key;
                        mThisIsReplicaOf = mServers.ElementAt(i - 1).Key;
                        System.Console.WriteLine("REPLICAS3 " + mID + " my " + mReplicaOfThis + " me " + mThisIsReplicaOf + " " + mServers.Count);
                        break;
                    }
                }

                update(mKeyManager.StoredKeys);
            }
            else
            {
                mReplicaOfThis = mID;
                mThisIsReplicaOf = mID;
            }
        }

        /// <summary>
        /// Updates the replica and the replicated servers of this server (Called on commit)
        /// </summary>
        public void update(ConcurrentDictionary<string, List<Pair<string, long>>> storedValues)
        {
            ConcurrentDictionary<string, List<Pair<string, long>>> ownValues = new ConcurrentDictionary<string, List<Pair<string, long>>>();
            ConcurrentDictionary<string, List<Pair<string, long>>> replicatedValues = new ConcurrentDictionary<string, List<Pair<string, long>>>();

            foreach (string key in storedValues.Keys)
            {
                if (keyBelongsToServer(key))
                {
                    if (ownValues.TryAdd(key, storedValues[key])) { }
                    else { }
                }
                else
                {
                    if (replicatedValues.TryAdd(key, storedValues[key])) { }
                    else { }
                }
            }

            System.Console.WriteLine("Own and replicated values ready to be sent");
            if (mID != mReplicaOfThis && mID != mThisIsReplicaOf)
            {
                try
                {
                    if (ownValues.Count > 0)
                    {
                        System.Console.WriteLine("Sending own values to " + mReplicaOfThis + " " + mServers[mReplicaOfThis]);
                        IServer replicaServer = (IServer)Activator.GetObject(typeof(IServer), mServers[mReplicaOfThis]);
                        replicaServer.updateKeyValues(ownValues);
                    }
                    if (replicatedValues.Count > 0)
                    {
                        System.Console.WriteLine("Sending replicated values to " + mThisIsReplicaOf + " " + mServers[mThisIsReplicaOf]);
                        IServer replicatedServer = (IServer)Activator.GetObject(typeof(IServer), mServers[mThisIsReplicaOf]);
                        replicatedServer.updateKeyValues(replicatedValues);
                    }
                }
                catch (SocketException ex)
                {
                    System.Console.WriteLine("Could not connect to server: " + ex.Source);
                }
                catch (RemoteServerFailedException ex)
                {
                    System.Console.WriteLine("Server " + ex.URL() + " is down");
                    mServer.deRegisterServer(ex.URL());
                    Thread.Sleep(500);
                }
            }
            else 
            {
                System.Console.WriteLine("The server does not replicated any other and is not a replica from other. Number of servers " + mServers.Count);
            }
        }

        /// <summary>
        /// Function called every 5 seconds to send the replicated keys to the original server
        /// </summary>
        /// <param name="storedValues"></param>
        public void updateEvent(ConcurrentDictionary<string, List<Pair<string, long>>> storedValues)
        {
            //if (mServers.Count > 2)
            {
                ConcurrentDictionary<string, List<Pair<string, long>>> replicatedValues = new ConcurrentDictionary<string, List<Pair<string, long>>>();
                foreach (string key in storedValues.Keys)
                {
                    if (!keyBelongsToServer(key))
                    {
                        List<Pair<string, long>> values;
                        if (mKeyManager.StoredKeys.TryGetValue(key, out values) && replicatedValues.TryAdd(key, values)) { } //CHANGED LINE
                    }
                }
                System.Console.WriteLine("Replicated values ready to be sent - size" + replicatedValues.Count);
                foreach (string key in replicatedValues.Keys)
                {
                    //System.Console.WriteLine("INSIDE SENDING REPLICATED VALUES");
                    string url = needUpdate(key);
                    if (url != null)
                    {
                        try
                        {
                            IServer server = (IServer)Activator.GetObject(typeof(IServer), url);
                            System.Console.WriteLine("Sending values to server: " + key + " url " + url);
                            server.updateKeyValues(replicatedValues);
                        }
                        catch (SocketException)
                        {
                            System.Console.WriteLine("Could not connect to server: " + url);
                        }
                        catch (RemoteServerFailedException ex)
                        {
                            System.Console.WriteLine("Server " + url + " is down");
                            mServer.deRegisterServer(ex.URL());
                        }
                    }
                    //System.Console.WriteLine("EXIT INSIDE SENDING REPLICATED VALUES");
                }
            }
        }

        /// <summary>
        /// Checks if the server is responsible for some key which it was not supposed to be
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string needUpdate(string key)
        {
            if (mServers.Count > 1)
            {
                //System.Console.WriteLine("INSIDE NEED UPDATE");
                UInt64 serverKey = ParticipantID.get(key, BINARY_KEY_LENGTH); //CHANGED LINE
                int serverCount = mServers.Count;
                for (int i = 0; i < mServers.Count; i++)
                {
                    //System.Console.WriteLine("INSIDE NEED UPDATE FOR");
                    //The third element before this one
                    int serverIndex = ((i - 3) > 0) ? ((i - 3) % serverCount) : (((i - 3) % serverCount) + serverCount);
                    //System.Console.WriteLine("INSIDE NEED UPDATE FOR 1");
                    //The second element before this one
                    int serverIndex1 = ((i - 2) > 0) ? ((i - 2) % serverCount) : (((i - 2) % serverCount) + serverCount);
                    //System.Console.WriteLine("INSIDE NEED UPDATE FOR 2");
                    //The first element before this one
                    int serverIndex2 = ((i - 1) > 0) ? ((i - 1) % serverCount) : (((i - 1) % serverCount) + serverCount);
                    //System.Console.WriteLine("INSIDE NEED UPDATE FOR 3");
                    //Check if it has some key which belongs to the server before the one this replicates
                    if (serverCount == 2 && (mServers.ElementAt(i).Key == mID) && (serverKey > mID)) { return mServers[mThisIsReplicaOf]; } //CHANGED LINE
                    if ((i == 1) && (mServers.ElementAt(i).Key == mID) && (serverKey > mServers.ElementAt(serverIndex1).Key))
                    {
                        //System.Console.WriteLine("S INSIDE NEED UPDATE");
                        return mServers.ElementAt(serverIndex2).Value;
                    }
                    //System.Console.WriteLine("INSIDE NEED UPDATE FOR 5");
                    //Check if it has some key which is to be replicated now to the server just before him
                    if ((mServers.ElementAt(i).Key == mID) && (serverKey > mServers.ElementAt(serverIndex).Key)
                        && (serverKey <= mServers.ElementAt(serverIndex1).Key))
                    {
                        //System.Console.WriteLine("S INSIDE NEED UPDATE");
                        return mServers.ElementAt(serverIndex1).Value;
                    }
                    //System.Console.WriteLine("INSIDE NEED UPDATE FOR 6");
                    if ((mServers.ElementAt(i).Key == mID) && (serverKey > mServers.ElementAt(serverIndex1).Key)
                        && (serverKey <= mServers.ElementAt(serverIndex2).Key))
                    {
                        //System.Console.WriteLine("S INSIDE NEED UPDATE");
                        return mServers.ElementAt(serverIndex2).Value;
                    }
                    //System.Console.WriteLine("INSIDE NEED UPDATE FOR 4");
                }
                //System.Console.WriteLine("S INSIDE NEED UPDATE");
                return null;
            }
            return null;
        }

        /// <summary>
        /// Checks if the server has some key which it should not be responsible for at a moment
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool keyBelongsToServer(string key)
        {
            UInt64 serverKey = ParticipantID.get(key, BINARY_KEY_LENGTH);
            System.Console.WriteLine("serverKey " + key + " " + serverKey);
            if (mServers.Count == 1) { return true; }
            if ((mServers.ElementAt(0).Key == mID) && ((serverKey > mThisIsReplicaOf) ||
                (serverKey <= mID))) { return true; }
            else if ((serverKey > mThisIsReplicaOf) && (serverKey <= mID)) { return true; }
            return false;
        }

        public Dictionary<UInt64, string> Clients { set { mClients = value; } }
        public Dictionary<UInt64, string> Servers
        {
            get { return mServers; }
            set
            {
                lock (mServers)
                {
                    lock (thisReplicaObject)
                    {
                        lock (thisIsReplicaObject)
                        {
                            mServers = value;
                            setNewReplicas();
                        }
                    }
                }                                              
            }
        }

        public Object ThisReplicaObject { get { return thisReplicaObject; } }
        public Object ThisIsReplicaObject { get { return thisIsReplicaObject; } }
    }
}
