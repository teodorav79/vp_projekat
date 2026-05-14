using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class DataFormatFault
    {
        [DataMember]
        public string Reason { get; set; }

        [DataMember]
        public int RowIndex { get; set; }

        [DataMember]
        public string OriginalLine { get; set; }
    }
}
