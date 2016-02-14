using System;
using System.Collections.Generic;
using System.Linq;
using IPADITable;
using System.Threading;

namespace Client
{
    class MainClient
    {
        static void Main(string[] args)
        {
            if (args.Length < 3) { Environment.Exit(0); }
            Client client = new Client(args[0], args[1], args[2]);
            
            System.Console.WriteLine("<enter> para sair...");
            System.Console.ReadLine();
        }
    }
}
