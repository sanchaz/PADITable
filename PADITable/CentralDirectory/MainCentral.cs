using System;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using IPADITable;

namespace CentralDirectory
{
    public class MainCentral
    {
        static void Main(string[] args)
        {
            if (args.Length < 2) { Environment.Exit(0); }
            CDService cdService = new CDService(args[0], args[1]);

            System.Console.WriteLine("<enter> para sair...");
            System.Console.ReadLine();
        }
    }
}
