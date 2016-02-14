using System;
using System.Collections.Generic;
using System.Linq;
using IPADITable;
using System.Collections.Concurrent;

namespace DataServer
{
    public class KeyManager
    {
        //The maximum of versions of a key
        private readonly int K;
        //Dictionary with the most up to date values for keys
        private ConcurrentDictionary<string, List<Pair<string, long>>> mStoredKeys;

        public KeyManager(int k)
        {
            mStoredKeys = new ConcurrentDictionary<string, List<Pair<string, long>>>();
            K = k;
        }

        /// <summary>
        /// Update the currently stored map from another map
        /// </summary>
        /// <param name="keyMap"></param>
        public void addAllKeyValues(ConcurrentDictionary<string, List<Pair<string, long>>> keyMap)
        {
            foreach (string key in keyMap.Keys)
            {
                mStoredKeys.AddOrUpdate(key, keyMap[key], (oldKey, oldValue) =>
                                                            {
                                                                foreach (Pair<string, long> pair in keyMap[key])
                                                                {
                                                                    if (!(oldValue.Contains(pair))) { oldValue.Add(pair); }
                                                                }
                                                                oldValue.Sort(KeyValueComparer);

                                                                int oldSize = oldValue.Count;

                                                                while (oldSize > K)
                                                                {
                                                                    oldValue.RemoveAt(0);
                                                                    oldSize--;
                                                                }
                                                                
                                                                return oldValue;
                                                            });
            }
        }

        /// <summary>
        /// Adds values to some key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="values"></param>
        public void addKeyValue(string key, List<Pair<string, long>> values)
        {
            if (mStoredKeys.ContainsKey(key))
            {
                foreach (Pair<string, long> pair in values)
                {
                    mStoredKeys[key].Add(pair);
                }
                return;
            }
            else if (mStoredKeys.TryAdd(key, values)) { return; }
            else
            {
                throw new RemoteException("Could not found key: " + key);
            }
        }

        /// <summary>
        /// Gets the most up to date value for a key
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public List<Pair<string, long>> getKeyValue(string key)
        {
            List<Pair<string, long>> values;
            if(mStoredKeys.TryGetValue(key, out values))
            {
                return values;
            }
            else
            {
                throw new RemoteException("Key not found: " + key);
            }
        }

        /// <summary>
        /// Comparision function of the key value pairs
        /// </summary>
        /// <param name="element1"></param>
        /// <param name="element2"></param>
        /// <returns></returns>
        static int KeyValueComparer(Pair<string, long> element1, Pair<string, long> element2)
        {
            return element1.Timestamp.CompareTo(element2.Timestamp);
        }

        public ConcurrentDictionary<string, List<Pair<string, long>>> StoredKeys { set { mStoredKeys = value; } get { return mStoredKeys; } }
    }
}
