using System;

namespace IPADITable
{
    public interface ICentralDir
    {
        //Used by the client to get a transaction id
        long getTransactionID(string url);
        //Used by the client to register itself
        bool registerClient(string clientURL);
        //Used by the client to deregister itself
        void deregisterClient(string clientURL);

        //Used by a node which detects that a data server is down
        void serverDown(string url);
        //Used by the server to register itself
        bool registerServer(string serverURL);
        //Used by the server to deregister itself
        void deregisterServer(string serverURL);

        //Stops the running central directory
        void exit();
    }
}
