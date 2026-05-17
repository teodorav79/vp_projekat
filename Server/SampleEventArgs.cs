using System;
using Common;

namespace Server
{
    public class SampleEventArgs : EventArgs
    {
        public HourlyConsumptionSample Sample { get; set; }
        public int Received { get; set; }
        public int Total { get; set; }

        public double Progress
        {
            get { return Total > 0 ? 100.0 * Received / Total : 0.0; }
        }
    }
}
