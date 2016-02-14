using System;
using IPADITable;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace Client
{
    public class NodeRouter
    {
        //Length of binary keys in server chord ring
        private const UInt64 BINARY_KEY_LENGTH = 6;
        //The clients and the servers
        private Dictionary<UInt64, string> mClients;
        private Dictionary<UInt64, string> mServers;

        public NodeRouter() 
        {
            mClients = new Dictionary<UInt64, string>();
            mServers = new Dictionary<UInt64, string>();
        }

        /// <summary>
        /// Returns the server to which we send the message
        /// </summary>
        /// <param name="key">The key we want to execute a put or get operation</param>
        /// <returns></returns>
        public string getServerToSend(string key)
        {
            lock (mServers)
            {
                if ((mServers != null) && mServers.Count > 0)
                {
                    UInt64 serverKey = ParticipantID.get(key, BINARY_KEY_LENGTH);
                    System.Console.WriteLine("SERVERKEY " + key + " " + serverKey);
                    foreach (UInt64 skey in mServers.Keys) { System.Console.WriteLine("SERVER KEY - " + skey + " " + mServers[skey]); }
                    //Retornar server consoante valor achado
                    string serverURL;

                    if (mServers.TryGetValue(serverKey, out serverURL))
                    {
                        return serverURL;
                    }

                    System.Console.WriteLine("No server found with key equals to key computed using the key for the operation: " + serverKey);

                    for (int i = 0; i < mServers.Count; i++)
                    {
                        if ((i == 0) && ((serverKey > mServers.ElementAt(mServers.Count - 1).Key) ||
                            (serverKey <= mServers.ElementAt(i).Key))) { System.Console.WriteLine("RETURNING  " + mServers.ElementAt(i).Value); return mServers.ElementAt(i).Value; }
                        else if ((i != 0) && (serverKey > mServers.ElementAt(i - 1).Key) && (serverKey <= mServers.ElementAt(i).Key))
                        { System.Console.WriteLine("RETURNING  " + mServers.ElementAt(i).Value); return mServers.ElementAt(i).Value; }
                    }

                    System.Console.WriteLine("RETURNING LAST " + mServers.ElementAt(mServers.Count - 1).Value);
                    return mServers.ElementAt(mServers.Count - 1).Value;
                }
                else
                {
                    System.Console.WriteLine("RegisterManager: getServerToSend, empty list of servers");
                    throw new RemoteException("Empty list of servers");
                }
            }
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
                System.Console.WriteLine("SERVERS ----- " + name);
            }
        }

        public Dictionary<UInt64, string> Clients { get { return mClients; } set { mClients = value; } }
        public Dictionary<UInt64, string> Servers { get { return mServers; } set { lock (mServers) { mServers = value; } } }
    }
}
