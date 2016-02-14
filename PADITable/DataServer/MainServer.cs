using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataServer
{
    class MainServer
    {
        static void Main(string[] args)
        {
            if (args.Length < 3) { Environment.Exit(0); } //throw new ArgumentException("Bad arguments, server username and port needed"); }
        
            Server server = new Server(args[0], args[1], args[2]);
            System.Console.WriteLine("<enter> para sair...");
            System.Console.ReadLine();
        }
    }
}
