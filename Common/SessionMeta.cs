using System;
using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class SessionMeta
    {
        [DataMember]
        public string CountryCode { get; set; }

        [DataMember]
        public DateTime Date { get; set; }

        [DataMember]
        public string SourceFileName { get; set; }

        [DataMember]
        public int TotalSamples { get; set; }
    }
}
