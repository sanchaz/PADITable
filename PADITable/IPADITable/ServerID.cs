using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace IPADITable
{
    public class ServerID
    {
        public static long get(string name, int binKeyL)
        {
            SHA1 sha = SHA1.Create();
            System.Text.ASCIIEncoding encoder = new System.Text.ASCIIEncoding();
            byte[] combined = encoder.GetBytes(name);
            sha.ComputeHash(combined);
            string urlHash = Convert.ToBase64String(sha.Hash);
            
            byte[] urlArray = Convert.FromBase64String(urlHash);
            long ret = BitConverter.ToInt64(urlArray, 0);

            ret = ret % (2 ^ binKeyL);
            if (ret < 0) { ret = -ret; }
            return ret;
        }
    }
}
