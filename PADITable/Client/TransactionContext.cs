using System;
using IPADITable;
using System.Collections.Generic;
using System.Linq;


namespace Client
{
    class TransactionContext
    {
        //Store the state of a transaction
        private long mTxID;
        //Store the old ant the new values of key
        private Dictionary<string, List<string>> mValuesOfKey;
        //The state of the transaction
        private TransactionStates mTansactionState;
        //If the transaction is active
        private bool mStillActive;
        //List of servers participating in a transaction (updated when new transaction begins, or some server is down and we need another one)
        private Dictionary<string, string> mKeyToServer;
        //Store the old register values of the client
        List<string> oldRegVals;

        public TransactionContext()
        {
            mTxID = DateTime.Now.Ticks;
            mValuesOfKey = new Dictionary<string, List<string>>();
            mTansactionState = TransactionStates.UNDEFINED;
            mKeyToServer = new Dictionary<string, string>();
            oldRegVals = new List<string>();
            mStillActive = false;
        }

        //Adds a value accessed from a key
        public void addKeyValue(string key, string newValue)
        {
            if (mValuesOfKey.ContainsKey(key))
            {
                mValuesOfKey[key].Add(newValue);
            }
            else
            {
                List<string> valuesList = new List<string>();
                valuesList.Add(newValue);
                mValuesOfKey.Add(key, valuesList);
            }
        }

        /// <summary>
        /// Prints the key values for all keys accessed in this transaction
        /// </summary>
        public void printKeyValues()
        {
            foreach (string key in mValuesOfKey.Keys)
            {
                System.Console.Write(key + ": ");
                foreach (string value in mValuesOfKey[key])
                {
                    System.Console.Write(value + " ");
                }
                System.Console.Write("; ");
            }
        }

        /// <summary>
        /// Prints the urls of the servers active on this transaction
        /// </summary>
        public void printServers()
        {
            if (mKeyToServer == null)
            {
                System.Console.WriteLine("No servers active on this transaction");
            }
            else
            {
                foreach (string url in mKeyToServer.Values)
                {
                    System.Console.WriteLine(url);
                }
            }
        }

        /// <summary>
        /// Adds a server to the list of servers used by this transaction
        /// </summary>
        /// <param name="url"></param>
        public void addServer(string key, string url)
        {
            if (mKeyToServer.ContainsKey(key))
            {
                return;
            }
            else
            {
                mKeyToServer.Add(key, url);
                System.Console.WriteLine("SERVER ADDED FOR KEY " + key + " " + url);
            }
        }

        /// <summary>
        /// Removes a server from the list of servers
        /// </summary>
        /// <param name="key"></param>
        public void removeServer(string key)
        {
            if (!(mKeyToServer.ContainsKey(key)))
            {
                return;
            }
            else
            {
                mKeyToServer.Remove(key);
            }
        }

        public long TxID { get { return mTxID; } set { mTxID = value; } }
        public Dictionary<string ,List<string>> ValuesOfKey { set { mValuesOfKey = value; } }
        public TransactionStates TransactionState { get { return mTansactionState; } set { mTansactionState = value; } }
        public bool Active { get { return mStillActive; } set { mStillActive = value; } }
        public Dictionary<string, string> Servers { get { return mKeyToServer; } set { mKeyToServer = value; } }
        public List<string> OldVals { get { return oldRegVals; } set { oldRegVals = value; } }
    }
}
