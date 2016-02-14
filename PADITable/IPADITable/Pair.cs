//Used for the method get(key, timestamp)
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace IPADITable
{
    [Serializable()]
    public class Pair<T,U>
    {
        public Pair() { }

        public Pair(T first, U second)
        {
            this.Value = first;
            this.Timestamp = second;
        }

        public T Value { get; set; }
        public U Timestamp { get; set; }
    }
}
