using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace IPADITable
{
    [Serializable()]
    public class DuplicateParticipantException : Exception
    {
        private string exMsg;

        public DuplicateParticipantException() { }

        protected DuplicateParticipantException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            if (info != null)
            {
                this.exMsg = info.GetString("exMsg");
            }
        }

        public DuplicateParticipantException(string msg) { exMsg = msg; }

        public override void GetObjectData(SerializationInfo info,
                                            StreamingContext context)
        {
            base.GetObjectData(info, context);

            if (info != null)
            {
                info.AddValue("exMsg", this.exMsg);
            }
        }

        public string getMessage() { return exMsg; }
    }
}
