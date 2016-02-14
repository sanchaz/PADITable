using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace IPADITable
{
    public interface IServer
    {
        //To client
        //Used to insert a value in the key in the context of a transaction txID 
        void put(string key, string value, long txID);
        //Returns null or a pair (value, timestamp) with the highest timestamp, that should be visible to the client executing the operation
        Pair<string, long> get(string key, long txID);
        //Check if the server is ready to commit a transaction
        bool prepare(long txID);
        //Used to commit a transaction identified by txID
        void commit(long txID);
        //Used to abort a transactions identified by txID
        void abort(long txID);
        //Returns null or a list of tuples (key, node, value, timestamp, state), more than one in case of multiple version
        List<Tuples<string, string, string, long, TransactionStates>> getAll(string key);

        //To central Directory
        void receiveNetworkMembership(List<Dictionary<UInt64, string>> topology);

        //To puppetMaster
        void registerInCentralDirectory();
        void Fail();
        void exit();

        //To server
        void updateKeyValues(ConcurrentDictionary<string, List<Pair<string, long>>> storedKeys);
    }
}
