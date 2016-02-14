using System;
using IPADITable;
using System.Linq;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.Remoting.Channels;
using System.Threading;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Serialization.Formatters;


namespace CentralDirectory
{
    class CDThread
    {
        public CDThread() { }

        /// <summary>
        /// Sends the list of participants to all active participants in the system
        /// </summary>
        public void sendTopologyToAll(List<Dictionary<UInt64, string>> parts, CDService cDir)
        {
            System.Console.WriteLine("SEND TOPOLOGY");
            
            foreach (string url in parts.ElementAt(0).Values)
            {
                IClient participantToSend = (IClient)Activator.GetObject(typeof(IClient), url);
                participantToSend.receiveNetworkMembership(parts);
            }

            foreach (string url in parts.ElementAt(1).Values)
            {
                IServer participantToSend = (IServer)Activator.GetObject(typeof(IServer), url);
                try
                {
                    participantToSend.receiveNetworkMembership(parts);
                }
                catch (RemoteServerFailedException) { cDir.deregisterServer(url); }
            }
        }
    }
}
