using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace IPADITable
{
    [Serializable()]
    public class RemoteServerFailedException : Exception
    {
        private string exMsg;
        private string mURL;

        public RemoteServerFailedException() { }

        protected RemoteServerFailedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            if (info != null)
            {
                this.exMsg = info.GetString("exMsg");
                this.mURL = info.GetString("mURL");
            }
        }

        public RemoteServerFailedException(string msg, string url) { exMsg = msg; mURL = url; }

        public override void GetObjectData(SerializationInfo info,
                                            StreamingContext context)
        {
            base.GetObjectData(info, context);

            if (info != null)
            {
                info.AddValue("exMsg", this.exMsg);
                info.AddValue("mURL", this.mURL);
            }
        }

        public string getMessage() { return exMsg; }
        public string URL() { return mURL; }

    }
}
