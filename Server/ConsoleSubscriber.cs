using System;

namespace Server
{
    public class ConsoleSubscriber
    {
        public ConsoleSubscriber()
        {
            EventBus.OnTransferStarted += OnTransferStarted;
            EventBus.OnSampleReceived += OnSampleReceived;
            EventBus.OnTransferCompleted += OnTransferCompleted;
            EventBus.OnWarningRaised += OnWarningRaised;
        }

        public void Unsubscribe()
        {
            EventBus.OnTransferStarted -= OnTransferStarted;
            EventBus.OnSampleReceived -= OnSampleReceived;
            EventBus.OnTransferCompleted -= OnTransferCompleted;
            EventBus.OnWarningRaised -= OnWarningRaised;
        }

        private void OnTransferStarted(object sender, TransferEventArgs e)
        {
            Console.WriteLine(">> [EVENT] OnTransferStarted: {0} {1:yyyy-MM-dd} src={2} total={3}",
                e.CountryCode, e.Date, e.SourceFileName, e.TotalSamples);
        }

        private void OnSampleReceived(object sender, SampleEventArgs e)
        {
            Console.WriteLine(">> [EVENT] OnSampleReceived: row#{0} H={1} actual={2} forecast={3} ({4}/{5}, {6:F2}%)",
                e.Sample.RowIndex, e.Sample.Hour, e.Sample.ActualMW, e.Sample.ForecastMW,
                e.Received, e.Total, e.Progress);
        }

        private void OnTransferCompleted(object sender, TransferEventArgs e)
        {
            Console.WriteLine(">> [EVENT] OnTransferCompleted: {0} {1:yyyy-MM-dd} primljeno {2}/{3} ({4:F2}%)",
                e.CountryCode, e.Date, e.Received, e.TotalSamples, e.Progress);
        }

        private void OnWarningRaised(object sender, WarningEventArgs e)
        {
            Console.WriteLine(">> [WARNING] {0} meter={1} H={2} actual={3} forecast={4} delta={5} dir={6} dailySum={7} threshold={8} : {9}",
                e.Type, e.MeterID, e.Hour, e.ActualMW, e.ForecastMW, e.Delta, e.Direction, e.DailySum, e.Threshold, e.Message);
        }
    }
}
