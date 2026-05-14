using System;
using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class HourlyConsumptionSample
    {
        [DataMember]
        public DateTime TimestampUtc { get; set; }

        [DataMember]
        public DateTime TimestampLocal { get; set; }

        [DataMember]
        public int Hour { get; set; }

        [DataMember]
        public double ActualMW { get; set; }

        [DataMember]
        public double ForecastMW { get; set; }

        [DataMember]
        public string CountryCode { get; set; }

        [DataMember]
        public int RowIndex { get; set; }

        public override string ToString()
        {
            return string.Format(
                "[{0}] {1} UTC ({2} local) H={3} Actual={4} MW Forecast={5} MW Row#{6}",
                CountryCode, TimestampUtc.ToString("o"), TimestampLocal.ToString("o"),
                Hour, ActualMW, ForecastMW, RowIndex);
        }
    }
}
