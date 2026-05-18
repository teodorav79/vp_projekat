using System;
using System.Globalization;

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
            ConsoleUi.Info(string.Format(CultureInfo.InvariantCulture,
                "Prenos je u toku... ({0} {1:yyyy-MM-dd}, total={2})",
                e.CountryCode, e.Date, e.TotalSamples));
        }

        private void OnSampleReceived(object sender, SampleEventArgs e)
        {
            ConsoleUi.Sample(string.Format(CultureInfo.InvariantCulture,
                "row#{0}, H={1}, Actual={2} MW, Forecast={3} MW   ({4}/{5}, {6:F2}%)",
                e.Sample.RowIndex, e.Sample.Hour, e.Sample.ActualMW, e.Sample.ForecastMW,
                e.Received, e.Total, e.Progress));
        }

        private void OnTransferCompleted(object sender, TransferEventArgs e)
        {
            ConsoleUi.Info(string.Format(CultureInfo.InvariantCulture,
                "Zavrsen prenos. Primljeno {0}/{1} ({2:F2}%)",
                e.Received, e.TotalSamples, e.Progress));
        }

        private void OnWarningRaised(object sender, WarningEventArgs e)
        {
            ConsoleUi.Blank();
            ConsoleUi.WarningHeader();
            string tag;
            string body;
            switch (e.Type)
            {
                case WarningType.UnderConsumptionWarning:
                    tag = "UNDER CONSUMPTION";
                    body = string.Format(CultureInfo.InvariantCulture,
                        "H={0}, Actual={1} MW, Forecast={2} MW, meter={3}, smer: ispod ocekivanog",
                        e.Hour, e.ActualMW, e.ForecastMW, e.MeterID);
                    break;
                case WarningType.OverConsumptionWarning:
                    tag = "OVER CONSUMPTION";
                    body = string.Format(CultureInfo.InvariantCulture,
                        "H={0}, Actual={1} MW, Forecast={2} MW, meter={3}, smer: iznad ocekivanog",
                        e.Hour, e.ActualMW, e.ForecastMW, e.MeterID);
                    break;
                case WarningType.ConsumptionSpikeWarning:
                    tag = "CONSUMPTION SPIKE";
                    body = string.Format(CultureInfo.InvariantCulture,
                        "ΔActual={0} MW, smer: {1}, H={2}, meter={3}",
                        e.Delta, e.Direction == "up" ? "iznad ocekivanog" : "ispod ocekivanog",
                        e.Hour, e.MeterID);
                    break;
                case WarningType.DailyLimitExceededWarning:
                    tag = "DAILY LIMIT EXCEEDED";
                    body = string.Format(CultureInfo.InvariantCulture,
                        "DailySum={0} MW > DailyLimitMW={1} MW, H={2}, meter={3}",
                        e.DailySum, e.Threshold, e.Hour, e.MeterID);
                    break;
                default:
                    tag = "WARNING";
                    body = e.Message;
                    break;
            }
            ConsoleUi.WarningBody(tag, body);
        }
    }
}
