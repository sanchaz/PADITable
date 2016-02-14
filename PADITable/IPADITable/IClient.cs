using System; //Datetime belongs to this namespace
using System.Collections.Generic; //List belongs to this namespace
using System.Collections.Concurrent; //Concurrent dictionary for thread safe in the central directory accesses

namespace IPADITable
{
    public interface IClient
    {
        //To receive the topology of the network from the central directory
        void receiveNetworkMembership(List<Dictionary<UInt64, string>> topology);

        //To puppet master
        void registerInCentralDirectory();
        //The client begins a transaction
        void beginTransaction();
        //The client assigns the value to the register regVal
        void storeRegisterValue(int regVal, string value);
        //The client executes put operation in the specified key using the value in the specified register
        void putRegisterKey(int regVal, string key);
        //The client executes a get operation and stores the result in the register
        void getRegisterKey(int regVal, string key);
        //The client executes a blind put operation in the specified key using the specified value
        void putValKeyValue(string key, string value);
        //The client changes the string stored in the specified key to lowercase
        void toLowerRegister(int regVal);
        //The client changes the string stored in the specified key to uppercase
        void toUpperRegister(int regVal);
        //The client concatenates the strings in regVal1 and regVal2 and stores the result in regVal1
        void concatRegisters(int regVal1, int regVal2);
        //The client commits a running transaction
        void commitTransaction();
        //The client returns to the puppet master an array with the contents of all registers
        List<string> dump();
        //Receives file name with list of commands for the client to execute
        void exeScript(string fileName);
        //To exit the execution
        void exit();
        //The client waits for a time interval before doing anything else
        void wait(int millis);
    }
}
