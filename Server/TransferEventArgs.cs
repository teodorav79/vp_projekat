using System;

namespace Server
{
    public class TransferEventArgs : EventArgs
    {
        public string CountryCode { get; set; }
        public DateTime Date { get; set; }
        public string SourceFileName { get; set; }
        public int TotalSamples { get; set; }
        public int Received { get; set; }

        public double Progress
        {
            get { return TotalSamples > 0 ? 100.0 * Received / TotalSamples : 0.0; }
        }
    }
}
