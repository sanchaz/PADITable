using IPADITable;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Timers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Serialization.Formatters;

namespace DataServer
{
    class ServerThread
    {
        //The object which handles communication
        private NodeRouter mNodeRouter;
        //The object which manages all keys
        private KeyManager mKeyManager;
        //The timer to be used in updating values from time to time
        private static Timer mTimer;
        //The constant time in which the thread tries to update the key value pairs (milliseconds)
        private const int interval = 50;

        public ServerThread(NodeRouter nodeRouter, KeyManager kManager)
        {
            mNodeRouter = nodeRouter;
            mKeyManager = kManager;
            mTimer = new Timer();
            mTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);

            mTimer.Interval = interval;
            mTimer.Enabled = true;
            //Default is true (flag wich tells if we want the event to be raised more than one time)
            //mTimer.AutoReset = true;
        }

        /// <summary>
        /// Updates both the replica of this node and the node which this one replicates (Called on commit)
        /// </summary>
        public void update(ConcurrentDictionary<string, List<Pair<string, long>>> values)
        {
            lock (mNodeRouter.Servers)
            {
                lock (mNodeRouter.ThisReplicaObject)
                {
                    lock (mNodeRouter.ThisIsReplicaObject)
                    {
                        mNodeRouter.update(values);
                    }
                }
            }
        }

        /// <summary>
        /// Event called every 5 seconds to update the keys
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        public void OnTimedEvent(object source ,ElapsedEventArgs e)
        {
            ConcurrentDictionary<string, List<Pair<string, long>>> keys = mKeyManager.StoredKeys;
            if (keys.Count > 0)
            {
                lock (mNodeRouter.Servers) 
                {
                    mNodeRouter.updateEvent(keys);
                }
            }
        }

        /// <summary>
        /// Stops the thread from raising the event of updating keys
        /// </summary>
        public void stopTimedEvent()
        {
            mTimer.Stop();
        }
    }
}
