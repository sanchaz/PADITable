using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace IPADITable
{
    public class ParticipantID
    {
        private static SHA1 sha = SHA1.Create();
        private static System.Text.ASCIIEncoding encoder = new System.Text.ASCIIEncoding();

        public static UInt64 get(string name, UInt64 binKeyL)
        {
            byte[] combined = encoder.GetBytes(name);
            sha.ComputeHash(combined);
            string urlHash = Convert.ToBase64String(sha.Hash);
            
            byte[] urlArray = Convert.FromBase64String(urlHash);
            UInt64 ret = BitConverter.ToUInt64(urlArray, 0);

            ret = ret % ((UInt64)Math.Pow(2, binKeyL));

            return ret;
        }
    }
}
