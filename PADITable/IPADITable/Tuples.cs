using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using System.Text;

//Used for the method getAll(key)

namespace IPADITable
{
    [Serializable()]
    public class Tuples<K, N, V, T, S>
    {
        public Tuples() { }

        public Tuples(K first, N second, V third, T fourth, S fifth)
        {
            this.Key = first;
            this.Node = second;
            this.Value = third;
            this.Timestamp = fourth;
            this.State = fifth;
        }

        public K Key { get; set; }
        public N Node { get; set; }
        public V Value { get; set; }
        public T Timestamp { get; set; }
        public S State { get; set; }
    }
}
