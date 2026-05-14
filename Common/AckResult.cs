using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public enum AckStatus
    {
        [EnumMember] Ok,
        [EnumMember] Missing,
        [EnumMember] InProgress,
        [EnumMember] Completed
    }

    [DataContract]
    public class AckResult
    {
        [DataMember]
        public AckStatus Status { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public int Received { get; set; }

        [DataMember]
        public int Total { get; set; }
    }
}
