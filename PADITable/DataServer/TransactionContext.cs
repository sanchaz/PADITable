using System;
using IPADITable;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;


namespace DataServer
{
    public class TransactionContext
    {
        //The maximum number of versions which a key can have
        private readonly int K;
        //Store the state of a transaction
        private long mTxID;
        //Store the old and the new values of keys 
        private ConcurrentDictionary<string, List<Pair<string, long>>> mKeyValues;
        //The timestamp for the values of the keys
        private long mTimeStamp;
        //The state of the transaction
        private TransactionStates mTansactionState;
        //If the transaction is active
        private bool mStillActive;

        public TransactionContext(long txID, int k)
        {
            mTxID = txID;
            mTansactionState = TransactionStates.UNDEFINED;
            mKeyValues = new ConcurrentDictionary<string, List<Pair<string, long>>>();
            mTimeStamp = DateTime.Now.Ticks;
            mStillActive = false;
            K = k;
        }

        /// <summary>
        /// Adds a value to a transaction
        /// </summary>
        /// <param name="key"></param>
        /// <param name="newValue"></param>
        public void addKeyValue(string key, string newValue)
        {
            List<Pair<string, long>> list = new List<Pair<string, long>>();
            list.Add(new Pair<string, long>(newValue, mTimeStamp));
            mKeyValues.AddOrUpdate(key, list, (oldKey, oldVal) =>
                                              { oldVal.Add(new Pair<string, long>(newValue, oldVal[oldVal.Count - 1].Timestamp + 1)); return oldVal; });

            int oldSize = mKeyValues[key].Count;

            while (oldSize > K)
            {
                mKeyValues[key].RemoveAt(0);
                oldSize--;
            }

            System.Console.WriteLine("KEY VALUE VERSION " + key + " " +
                                      mKeyValues[key].ElementAt(mKeyValues[key].Count - 1).Value + 
                                      mKeyValues[key].ElementAt(mKeyValues[key].Count - 1).Timestamp);
        }

        /// <summary>
        /// Gets the latest value stored for some key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Pair<string, long> getValueForKey(string key)
        {
            if (!mKeyValues.ContainsKey(key)) { return null; }
            List<Pair<string, long>> values;
            if (mKeyValues.TryGetValue(key, out values))
            {
                return values[values.Count - 1];
            }

            return null;
        }

        /// <summary>
        /// Gets the previous version of a key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Pair<string, long> getPreviousValueForKey(string key)
        {
            if (!mKeyValues.ContainsKey(key)) { return null; }
            long lastVersion = mKeyValues[key].ElementAt(mKeyValues[key].Count - 1).Timestamp;
            List<Pair<string, long>> values;
            if (mKeyValues.TryGetValue(key, out values))
            {
                for(int i = values.Count - 1; i >= 0; i--)
                {
                    if (values.ElementAt(i).Timestamp < lastVersion) { return values.ElementAt(i); }
                }
            }

            return null;
        }

        /// <summary>
        /// Prints the key values for all keys accessed in this transaction
        /// </summary>
        public void printKeyValues()
        {
            if (mKeyValues.Count == 0)
            {
                System.Console.Write("Empty ");
            }
            else
            {
                for (int i = 0; i < mKeyValues.Count; i++)
                {
                    System.Console.Write(mKeyValues.ElementAt(i).Key + ": ");

                    for (int j = 0; j < mKeyValues.ElementAt(i).Value.Count; i++)
                    {
                        System.Console.Write(mKeyValues.ElementAt(i).Value.ElementAt(j).Value + " ");
                    }
                    System.Console.WriteLine();
                }
            }
        }

        public long TxID { get { return mTxID; } set { mTxID = value; } }
        public ConcurrentDictionary<string, List<Pair<string, long>>> ValuesOfKey { get { return mKeyValues; } set { mKeyValues = value; } }
        public TransactionStates TransactionState { get { return mTansactionState; } set { mTansactionState = value; } }
        public bool Active { get { return mStillActive; } set { System.Console.WriteLine("SETTING ACT " + value); mStillActive = value; } }
    }
}
