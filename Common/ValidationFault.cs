using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class ValidationFault
    {
        [DataMember]
        public string Field { get; set; }

        [DataMember]
        public string Reason { get; set; }

        [DataMember]
        public int RowIndex { get; set; }
    }
}
