using System;
using IPADITable;
using System.Linq;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;

namespace CentralDirectory
{
    class CDService : MarshalByRefObject, ICentralDir
    {
        private TcpChannel mCDChannel;
        private ParticipantManager mParticipantMngr;
        private CDThread mCdThread;

        public CDService(string username, string port)
        {
            mCDChannel = new TcpChannel(Int32.Parse(port));
            ChannelServices.RegisterChannel(mCDChannel, true);

            RemotingServices.Marshal(this, username, typeof(ICentralDir));
            string[] urls = ChannelServices.GetUrlsForObject(this);
            System.Console.WriteLine("Central Directory instantiated with URL " + urls[0]);

            mParticipantMngr = new ParticipantManager();
            mCdThread = new CDThread();
        }

        /// <summary>
        /// Sends a transaction id to a client
        /// </summary>
        /// <param name="url"> URL of the client to send tx id</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public long getTransactionID(String url)
        {
            Console.WriteLine("getTransationID: Sending transaction ID to: " + url);

            return DateTime.Now.Ticks;
        }

        /// <summary>
        /// Takes note of a new client that is joining the network
        /// </summary>
        /// <param name="nick">nickname of the client</param>
        /// <param name="clientURL">url of the client</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool registerClient(string url)
        {
            try
            {
                mParticipantMngr.addClient(url);
                Console.WriteLine("registerClient: Sending network topology to all participants");
                List<Dictionary<UInt64, string>> participants = new List<Dictionary<UInt64, string>>();
                ConcurrentDictionary<UInt64, string> clients = mParticipantMngr.MClients;
                ConcurrentDictionary<UInt64, string> servers = mParticipantMngr.MServers;
                participants.Add(mParticipantMngr.order(clients));
                participants.Add(mParticipantMngr.order(servers));
                Thread thread = new Thread(new ThreadStart(() => mCdThread.sendTopologyToAll(participants, this)));

                thread.Start();
                return true;
            }
            catch(ArgumentException ex) {
                System.Console.WriteLine(ex.Message);

                System.Console.WriteLine("registerClient: Sending error to client: " + url);
                throw ex;
            }
        }

        /// <summary>
        /// Removes the client from the system netwrok topology
        /// </summary>
        /// <param name="url"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void deregisterClient(string url)
        {
            mParticipantMngr.removeClient(url);

            Console.WriteLine("deregisterClient: Sending network topology to all participants");
            List<Dictionary<UInt64, string>> participants = new List<Dictionary<UInt64, string>>();
            ConcurrentDictionary<UInt64, string> clients = mParticipantMngr.MClients;
            ConcurrentDictionary<UInt64, string> servers = mParticipantMngr.MServers;
            participants.Add(mParticipantMngr.order(clients));
            participants.Add(mParticipantMngr.order(servers));
          
            Thread thread = new Thread(new ThreadStart(() => mCdThread.sendTopologyToAll(participants, this)));

            thread.Start();
        }

        /// <summary>
        /// Informs that a server is down
        /// </summary>
        /// <param name="nick">the nickname of the server which is down</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void serverDown(string url)
        {
            mParticipantMngr.removeServer(url);
            Console.WriteLine("serverDown: Sending network topology to all participants");
            List<Dictionary<UInt64, string>> participants = new List<Dictionary<UInt64, string>>();
            ConcurrentDictionary<UInt64, string> clients = mParticipantMngr.MClients;
            ConcurrentDictionary<UInt64, string> servers = mParticipantMngr.MServers;
            participants.Add(mParticipantMngr.order(clients));
            participants.Add(mParticipantMngr.order(servers));

            Thread thread = new Thread(new ThreadStart(() => mCdThread.sendTopologyToAll(participants, this)));

            thread.Start();
        }

        /// <summary>
        /// Takes notes of a new server which is joining the network
        /// </summary>
        /// <param name="nick">nickname of the server</param>
        /// <param name="url">url of the server</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool registerServer(string url)
        {
            try
            {
                mParticipantMngr.addServer(url);
                Console.WriteLine("registerServer: Sending network topology to all participants");
                List<Dictionary<UInt64, string>> participants = new List<Dictionary<UInt64, string>>();
                ConcurrentDictionary<UInt64, string> clients = mParticipantMngr.MClients;
                ConcurrentDictionary<UInt64, string> servers = mParticipantMngr.MServers;
                participants.Add(mParticipantMngr.order(clients));
                participants.Add(mParticipantMngr.order(servers));

                Thread thread = new Thread(new ThreadStart(() => mCdThread.sendTopologyToAll(participants, this)));

                thread.Start();
                return true;
            } catch (ArgumentException ex) {
                System.Console.WriteLine(ex.Message);

                System.Console.WriteLine("registerServer: Sending error to server: " + url);
                throw ex;
            }
        }

        /// <summary>
        /// Removes a server from the system network topology
        /// </summary>
        /// <param name="url"></param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void deregisterServer(string url)
        {
            mParticipantMngr.removeServer(url);

            Console.WriteLine("deregisterServer: Sending network topology to all participants");
            List<Dictionary<UInt64, string>> participants = new List<Dictionary<UInt64, string>>();
            ConcurrentDictionary<UInt64, string> clients = mParticipantMngr.MClients;
            ConcurrentDictionary<UInt64, string> servers = mParticipantMngr.MServers;
            participants.Add(mParticipantMngr.order(clients));
            participants.Add(mParticipantMngr.order(servers));
            
            Thread thread = new Thread(new ThreadStart(() => mCdThread.sendTopologyToAll(participants, this)));

            thread.Start();
        }

        /// <summary>
        /// Stops the execution of the central directory
        /// </summary>
        public void exit()
        {
            ChannelServices.UnregisterChannel(mCDChannel);
            Environment.Exit(0);
        }

        /// <summary>
        /// So the lease never expires
        /// </summary>
        /// <returns></returns>
        public override object InitializeLifetimeService()
        {
            return null;
        }
    }
}
