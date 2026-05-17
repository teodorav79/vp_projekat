using System;

namespace Server
{
    public enum WarningType
    {
        UnderConsumptionWarning,
        OverConsumptionWarning,
        ConsumptionSpikeWarning,
        DailyLimitExceededWarning
    }

    public class WarningEventArgs : EventArgs
    {
        public WarningType Type { get; set; }
        public int Hour { get; set; }
        public double ActualMW { get; set; }
        public double ForecastMW { get; set; }
        public string MeterID { get; set; }
        public double Delta { get; set; }
        public string Direction { get; set; }
        public double DailySum { get; set; }
        public double Threshold { get; set; }
        public string Message { get; set; }
    }
}
