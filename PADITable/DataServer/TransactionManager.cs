using System;
using IPADITable;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

namespace DataServer
{
    public class TransactionManager
    {
        //Maximum number of values per key
        private readonly int K;
        //List of transaction contexts
        private ConcurrentDictionary<long, TransactionContext> mTransactions;
        //The lock manager to manage concurrency
        private LockManager mLockManager;
        //The key manager which stores the permanent values for keys
        private KeyManager mKeyManager;
        //The server to which it belongs
        private Server mServer;

        public TransactionManager(Server server, KeyManager kManager, int k)
        {
            mTransactions = new ConcurrentDictionary<long, TransactionContext>();
            mLockManager = new LockManager();
            mKeyManager = kManager;
            mServer = server;
            K = k;
        }
        
        /// <summary>
        /// Adds a transaction  to the list of active transactions
        /// </summary>
        /// <param name="txID"></param>
        public void addTransaction(long txID)
        {
            if (mTransactions.ContainsKey(txID)) { System.Console.WriteLine("Transaction found with id " + txID); return; }
            else if (mTransactions.TryAdd(txID, new TransactionContext(txID, K)))
            {
                mTransactions[txID].TransactionState = TransactionStates.INITIATED;
                mTransactions[txID].Active = true;
                System.Console.WriteLine("New transaction added with id " + txID + " " + mTransactions[txID].Active);
                return; 
            }
            throw new RemoteException("Could not add transaction with id " + txID);
        }

        /// <summary>
        /// Gets the transaction context for the transaction with id txID
        /// </summary>
        /// <param name="txID"></param>
        /// <returns></returns>
        public TransactionContext getTransactionContext(long txID)
        {
            TransactionContext value;
            if (mTransactions.TryGetValue(txID, out value))
            {
                return value;
            }
            else
            {
                throw new RemoteException("Could not get transaction context for transaction: " + txID);
            }
        }

        /// <summary>
        /// Removes a transaction from the list of active transactions
        /// </summary>
        /// <param name="txID"></param>
        public void removeTransaction(long txID)
        {
            TransactionContext tx;
            if (mTransactions.ContainsKey(txID) && !(mTransactions[txID].Active))
            {
                if (mTransactions.TryRemove(txID, out tx)) { return; }
                throw new RemoteException("Could not remove transaction with id: " + txID);
            }
            else
            {
                throw new RemoteException("Could not get transaction with id: " + txID);
            }
        }

        /// <summary>
        /// Sets the state of a transaction
        /// </summary>
        /// <param name="txID"></param>
        /// <param name="state"></param>
        public void setTransactionState(long txID, TransactionStates state)
        {
            TransactionContext tx;
            if (mTransactions.TryGetValue(txID, out tx))
            {
                tx.TransactionState = state;
            }
            else
            {
                throw new RemoteException("Could not get transaction with id: " + txID);
            }
        }

        /// <summary>
        /// Tries to perform a get operation on the specified key in the context of a transaction
        /// </summary>
        /// <param name="txID"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public Pair<string, long> getOperation(long txID, string key)
        {
            addTransaction(txID);

            Pair<bool, bool> lockResult = mLockManager.getReadLock(txID, key);
            if (mTransactions[txID].Active && lockResult.Value)
            {
                if (!lockResult.Timestamp)
                {
                    Pair<string, long> value = mTransactions[txID].getValueForKey(key);
                    if (value != null) { return value; }
                    else
                    {
                        List<Pair<string, long>> valuesList;

                        if (mKeyManager.StoredKeys.TryGetValue(key, out valuesList) && valuesList.Count > 0)
                        {
                            mTransactions[txID].ValuesOfKey.TryAdd(key, valuesList);
                            System.Console.WriteLine("Getting value for key " + key);
                            return valuesList.ElementAt(valuesList.Count - 1);
                        }
                    }
                }
                else
                {
                    Pair<string, long> value = mTransactions[txID].getPreviousValueForKey(key);

                    if (value != null) { return value; }
                    else
                    {
                        List<Pair<string, long>> valuesList;

                        if (mKeyManager.StoredKeys.TryGetValue(key, out valuesList) && valuesList.Count > 1)
                        {
                            mTransactions[txID].ValuesOfKey.TryAdd(key, valuesList);
                            System.Console.WriteLine("Getting value for key " + key);
                            Pair<string, long> previous = mTransactions[txID].getPreviousValueForKey(key);
                            return previous;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Tries to perform a put operation in a key with some value, in the context of some transaction
        /// </summary>
        /// <param name="txID"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public void putOperation(long txID, string key, string value)
        {
            addTransaction(txID);
            if (mTransactions[txID].Active && mLockManager.getWriteLock(txID, key))
            {
                mTransactions[txID].addKeyValue(key, value);
                System.Console.WriteLine("Value " + value + " added to key " + key);
                return;
            }
            throw new AbortException("Unable to perform put operation on key: " + key + " , in the context of transaction: " + txID);
        }

        /// <summary>
        /// Aborts a transaction and does nothing with the changed values
        /// </summary>
        /// <param name="txID"></param>
        public void abortTransaction(long txID)
        {
            if (!mTransactions.ContainsKey(txID))
            {
                return;
            }
            TransactionContext tContext;
            
            if (mTransactions.TryGetValue(txID, out tContext) && tContext.Active)
            {
                while (!mLockManager.releaseLocks(txID)) { }

                tContext.TransactionState = TransactionStates.ABORTED;
                System.Console.WriteLine("SETTING FALSE ABORT OPERATION");
                tContext.Active = false;
                return;
            }
            else
            {
                System.Console.WriteLine("TX ACTIVE " + tContext.Active + " " + txID);
                throw new RemoteException("Could not abort transaction: " + txID);
            }
        }

        /// <summary>
        /// Commits a transaction moving all keys from the context to the KeyManager
        /// </summary>
        /// <param name="txID"></param>
        public ConcurrentDictionary<string, List<Pair<string, long>>> commitTransaction(long txID)
        {
            if (!mTransactions.ContainsKey(txID))
            {
                throw new RemoteException("Transaction with id " + txID + " not found, could not complete abort operation");
            }
            TransactionContext txContext;
            if (mTransactions.TryGetValue(txID, out txContext) && txContext.Active)
            {
                ConcurrentDictionary<string, List<Pair<string, long>>> keyValues = txContext.ValuesOfKey;
                mKeyManager.addAllKeyValues(keyValues);
                while (!mLockManager.releaseLocks(txID)) { }
                
                txContext.TransactionState = TransactionStates.COMMITED;
                txContext.Active = false;

                return keyValues;
            }
           
            throw new RemoteException("Could not execute commit operation on transaction: " + txID);
        }

        /// <summary>
        /// Checks if it is possible to commit a transaction
        /// </summary>
        /// <returns></returns>
        public bool prepare(long txID)
        {
            if (mTransactions.ContainsKey(txID))
            {
                mTransactions[txID].TransactionState = TransactionStates.TENTATIVELYCOMMITED;
                return true;
            }
            else { return false; }
        }

        /// <summary>
        /// Returns all keys being read or written in a transaction
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public List<Tuples<string, string, string, long, TransactionStates>> getAll(string key)
        {
            List<Tuples<string, string, string, long, TransactionStates>> ret = new List<Tuples<string, string, string, long, TransactionStates>>();
            if (mTransactions.Count > 0)
            {
                foreach (long txID in mTransactions.Keys)
                {
                    TransactionContext tc = mTransactions[txID];
                    foreach (string tKey in tc.ValuesOfKey.Keys)
                    {
                        if (tKey.CompareTo(key) == 0)
                        {
                            System.Console.WriteLine("PASSED COMPARISION " + key);
                            foreach (Pair<string, long> p in tc.ValuesOfKey[key])
                            {
                                Tuples<string, string, string, long, TransactionStates> t = new Tuples<string, string, string, long, TransactionStates>(key, mServer.URL, p.Value, p.Timestamp, tc.TransactionState);
                                ret.Add(t);
                            }
                        }
                    }
                }
            }
            if (ret.Count > 0) { return ret; }
            return null;
        }
    }
}
