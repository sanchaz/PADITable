using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading;
using IPADITable;

namespace DataServer
{
    public class LockManager
    {
        //Dictionaties with lock information on keys
        private ConcurrentDictionary<long, List<string>> mWriteLocks;
        private ConcurrentDictionary<long, List<string>> mReadLocks;

        public LockManager()
        {
            mWriteLocks = new ConcurrentDictionary<long, List<string>>();
            mReadLocks = new ConcurrentDictionary<long, List<string>>();
        }

        /// <summary>
        /// Tries to grab a write lock for a key in the context of some transaction
        /// </summary>
        /// <param name="txID"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool getWriteLock(long txID, string key)
        {  
            if (Monitor.TryEnter(mWriteLocks, 1000))
            {
                try
                {   
                    if (mWriteLocks.ContainsKey(txID) && mWriteLocks[txID].Contains(key)) { return true; }

                    System.Console.WriteLine("Transaction " + txID + " has no write lock on " + key);
                    foreach (long keys in mWriteLocks.Keys)
                    {
                        if (txID != keys && mWriteLocks[keys].Contains(key))
                        {
                            System.Console.WriteLine("Transaction found with write lock on key " + key);
                            return false;
                        }
                    }
                    System.Console.WriteLine("No write locks found for key " + key);
                    if (Monitor.TryEnter(mReadLocks, 1000))
                    {
                        try
                        {
                            foreach (long tx in mReadLocks.Keys)
                            {
                                if (tx != txID && mReadLocks[tx].Contains(key))
                                {
                                    System.Console.WriteLine("Transaction found with read lock on key " + key);
                                    return false;
                                }
                            }
                            System.Console.WriteLine("No read locks found for key " + key);
                            List<string> value = new List<string>();
                            value.Add(key);
                            mWriteLocks.AddOrUpdate(txID, value, (writeKey, oldList) => { oldList.Add(key); return oldList; });
                            System.Console.WriteLine("Write lock added for transaction " + txID + " on key " + key);
                            return true;
                        } finally {
                            Monitor.Exit(mReadLocks);
                        }
                    }
                } finally {
                    Monitor.Exit(mWriteLocks);
                }
            }
            return false;
        }

        /// <summary>
        /// Tries to grab a read lock for a key in the context of some transaction
        /// </summary>
        /// <param name="txID"></param>
        /// <param name="key"></param>
        /// <returns>Pair<bool,bool> - First element means that key value can be read, second means if the latest can be read, 
        ///                            or the previous one stored</returns>
        public Pair<bool, bool> getReadLock(long txID, string key)
        {
            if(Monitor.TryEnter(mWriteLocks, 1000))
            {
                try
                {
                    if (mReadLocks.ContainsKey(txID) && mReadLocks[txID].Contains(key)) { return new Pair<bool,bool>(true,false); } 
                    if (mWriteLocks.ContainsKey(txID) && mWriteLocks[txID].Contains(key))
                    {
                        List<string> value = new List<string>();
                        value.Add(key);
                        mReadLocks.AddOrUpdate(txID, value, (readKey, oldList) => { oldList.Add(key); return oldList; });
                        System.Console.WriteLine("Read lock added for transaction " + txID + " on key " + key);
                        return new Pair<bool,bool>(true, false);
                    }
                    System.Console.WriteLine("Transaction " + txID + " has not write lock on " + key);
                    foreach (long keys in mWriteLocks.Keys)
                    {
                        if (txID != keys && mWriteLocks[keys].Contains(key))
                        {
                            return new Pair<bool, bool>(true, true);
                        }
                    }
                    System.Console.WriteLine("No write locks found for key " + key);
                    List<string> list = new List<string>();
                    list.Add(key);
                    mReadLocks.AddOrUpdate(txID, list, (readKey, oldList) => { oldList.Add(key); return oldList; });
                    System.Console.WriteLine("Read lock added for transaction " + txID + " on key " + key);
                    return new Pair<bool,bool>(true, false);
                } finally {
                    Monitor.Exit(mWriteLocks);
                }
            }

            return new Pair<bool,bool>(false, false);
        }

        /// <summary>
        /// Releases all locks for a transaction
        /// </summary>
        /// <param name="txID"></param>
        /// <returns></returns>
        public bool releaseLocks(long txID)
        {
            if(Monitor.TryEnter(mReadLocks, 1000) && Monitor.TryEnter(mWriteLocks, 1000))
            {
                try
                {
                    List<string> values;
                    if (mWriteLocks.ContainsKey(txID))
                    {
                        mWriteLocks.TryRemove(txID, out values);
                        System.Console.WriteLine("Write locks removed for transaction " + txID);
                    }
                    if (mReadLocks.ContainsKey(txID))
                    {
                        mReadLocks.TryRemove(txID, out values);
                        System.Console.WriteLine("Read locks removed for transaction " + txID);
                    }
                    return true;
                }
                finally
                {
                    Monitor.Exit(mWriteLocks);
                    Monitor.Exit(mReadLocks);
                }
            }

            return false;
        }
    }
}
