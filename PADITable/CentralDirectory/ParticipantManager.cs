using System;
using IPADITable;
using System.Linq;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;
using System.Collections.Generic;

namespace CentralDirectory
{
    class ParticipantManager
    {
        //Length of binary keys in server chord ring
        private const UInt64 BINARY_KEY_LENGTH = 6;
        //Dictionaries to store the clients and servers active in the system. (key - hashcode of url ; value - url)
        private ConcurrentDictionary<UInt64, string> mClients;
        private ConcurrentDictionary<UInt64, string> mServers;

        public ParticipantManager()
        {
            this.mClients = new ConcurrentDictionary<UInt64, string>();
            this.mServers = new ConcurrentDictionary<UInt64, string>();
        }

        /// <summary>
        /// Adds a client to the list of clients which the central directory knows.
        /// </summary>
        /// <param name="nick"> nickname of the client </param>
        /// <param name="url"> url to get the client </param>
        public void addClient(string url)
        {
            UInt64 clientID = ParticipantID.get(url, BINARY_KEY_LENGTH);

            if(mClients.TryAdd(clientID, url))
            {
                System.Console.WriteLine("Client added " + url + " " + clientID);
                return;
            }
            else
            {
                while (true)
                {
                    clientID++;
                    if (mClients.TryAdd(clientID, url))
                    {
                        System.Console.WriteLine("Client added " + url + " " + clientID);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Removes a client with key nick
        /// </summary>
        /// <param name="nick"></param>
        /// <param name="url"></param>
        public void removeClient(string url)
        {
            UInt64 clientID = ParticipantID.get(url, BINARY_KEY_LENGTH);
            string retURL = "";

            if (mClients.TryRemove(clientID, out retURL))
            {
                System.Console.WriteLine("Client removed: " + url);
                return;
            }

            System.Console.WriteLine("Client not found for url " + url);
        }

        /// <summary>
        /// Add a server to the list of servers which the central directory knows
        /// </summary>
        /// <param name="nick"></param>
        /// <param name="url"></param>
        public void addServer(string url)
        {
            UInt64 serverID = ParticipantID.get(url, BINARY_KEY_LENGTH);

            if (mServers.TryAdd(serverID, url))
            {
                System.Console.WriteLine("Server added " + url + " " + serverID);
            }
            else
            {
                while (true)
                {
                    serverID = (serverID + 1) % ((UInt64)Math.Pow(2.0, BINARY_KEY_LENGTH));
                    if (mServers.TryAdd(serverID, url))
                    {
                        System.Console.WriteLine("Server added " + url + " " + serverID);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Orders the clients map
        /// </summary>
        public Dictionary<UInt64, string> order(ConcurrentDictionary<UInt64, string> cDict)
        {
            IEnumerable<KeyValuePair<UInt64, string>> orderedMap = cDict.OrderBy(element => element.Key);
            Dictionary<UInt64, string> dict = orderedMap.ToDictionary(el => el.Key, el => el.Value);

            return dict;
        }

        /// <summary>
        /// Removes a server with key nick
        /// </summary>
        /// <param name="nick"> The nickname of the server to be removed </param>
        public void removeServer(string url)
        {
            UInt64 serverID = ParticipantID.get(url, BINARY_KEY_LENGTH);
            string retURL = "";

            if (mServers.TryRemove(serverID, out retURL))
            {
                System.Console.WriteLine("Server removed: " + url);
                return;
            }

            System.Console.WriteLine("Server not found for url " + url);
        }

        /// <summary>
        /// Returns the list of clients
        /// </summary>
        public ConcurrentDictionary<UInt64, string> MClients { get { return mClients; } }

        /// <summary>
        /// Returns the list of servers
        /// </summary>
        public ConcurrentDictionary<UInt64, string> MServers { get { return mServers; } }
    }
}
