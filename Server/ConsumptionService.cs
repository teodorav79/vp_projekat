using System;
using System.Configuration;
using System.Globalization;
using System.ServiceModel;
using Common;

namespace Server
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession,
                     ConcurrencyMode = ConcurrencyMode.Single)]
    public class ConsumptionService : IConsumptionService, IDisposable
    {
        private SessionMeta _meta;
        private int _received;
        private bool _sessionActive;
        private bool _disposed;
        private SessionFileWriter _writer;
        private Analytics _analytics;

        public AckResult StartSession(SessionMeta meta)
        {
            if (meta == null)
            {
                throw new FaultException<DataFormatFault>(
                    new DataFormatFault { Reason = "Meta je null." },
                    new FaultReason("StartSession: meta nije prosleđena."));
            }
            if (string.IsNullOrWhiteSpace(meta.CountryCode))
            {
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Field = "CountryCode", Reason = "Prazan CountryCode." },
                    new FaultReason("StartSession: CountryCode je obavezan."));
            }
            if (string.IsNullOrWhiteSpace(meta.SourceFileName))
            {
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Field = "SourceFileName", Reason = "Prazno ime izvora." },
                    new FaultReason("StartSession: SourceFileName je obavezan."));
            }
            if (meta.TotalSamples <= 0)
            {
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Field = "TotalSamples", Reason = "TotalSamples mora biti > 0." },
                    new FaultReason("StartSession: TotalSamples mora biti pozitivan."));
            }

            _meta = meta;
            _received = 0;
            _sessionActive = true;

            string dataRoot = ConfigurationManager.AppSettings["DataRoot"];
            _writer = new SessionFileWriter(dataRoot, meta.CountryCode.ToUpperInvariant(), meta.Date);

            double alpha = ReadDouble("UnderConsumptionAlpha", 0.85);
            double beta = ReadDouble("OverConsumptionBeta", 1.15);
            double spike = ReadDouble("SpikeDeltaMW", 500.0);
            double limit = ReadDouble("DailyLimitMW", double.MaxValue);
            _analytics = new Analytics(alpha, beta, spike, limit, meta.CountryCode.ToUpperInvariant());

            ConsoleUi.Blank();
            ConsoleUi.Info(string.Format(CultureInfo.InvariantCulture,
                "Pokrenut prenos: country={0}, date={1:yyyy-MM-dd}, source={2}, total={3}",
                meta.CountryCode, meta.Date, meta.SourceFileName, meta.TotalSamples));
            ConsoleUi.Info("Session fajl: " + _writer.SessionPath);
            ConsoleUi.Info("Rejects fajl: " + _writer.RejectsPath);
            ConsoleUi.Info(string.Format(CultureInfo.InvariantCulture,
                "Pragovi: alpha={0}, beta={1}, SpikeDeltaMW={2}, DailyLimitMW={3}",
                alpha, beta, spike, limit));

            EventBus.RaiseTransferStarted(this, new TransferEventArgs
            {
                CountryCode = meta.CountryCode,
                Date = meta.Date,
                SourceFileName = meta.SourceFileName,
                TotalSamples = meta.TotalSamples,
                Received = 0
            });

            return new AckResult
            {
                Status = AckStatus.Ok,
                Message = "Sesija zapoceta.",
                Received = 0,
                Total = meta.TotalSamples
            };
        }

        public AckResult PushSample(HourlyConsumptionSample sample)
        {
            if (!_sessionActive)
            {
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Field = "Session", Reason = "Sesija nije zapoceta." },
                    new FaultReason("PushSample pre StartSession."));
            }
            if (sample == null)
            {
                throw new FaultException<DataFormatFault>(
                    new DataFormatFault { Reason = "Uzorak je null." },
                    new FaultReason("PushSample: sample je null."));
            }

            if (double.IsNaN(sample.ActualMW) || double.IsNaN(sample.ForecastMW))
            {
                _received++;
                _writer.WriteReject(sample.RowIndex, "NaN vrednost", SerializeRow(sample));
                ConsoleUi.Reject(string.Format(CultureInfo.InvariantCulture,
                    "{0}-> Uzorak odbijen: NaN vrednost (row#{1})", _received, sample.RowIndex));
                return new AckResult
                {
                    Status = AckStatus.Missing,
                    Message = "NaN vrednost - tretirano kao missing.",
                    Received = _received,
                    Total = _meta.TotalSamples
                };
            }

            if (sample.Hour < 0 || sample.Hour > 23)
            {
                _writer.WriteReject(sample.RowIndex, "Hour van [0,23]", SerializeRow(sample));
                ConsoleUi.Reject(string.Format(CultureInfo.InvariantCulture,
                    "{0}-> Uzorak odbijen: Hour={1} van [0,23] (row#{2})",
                    _received + 1, sample.Hour, sample.RowIndex));
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Field = "Hour", Reason = "Hour van opsega [0,23].", RowIndex = sample.RowIndex },
                    new FaultReason("Validation: Hour van opsega."));
            }
            if (sample.ActualMW < 0)
            {
                _writer.WriteReject(sample.RowIndex, "ActualMW < 0", SerializeRow(sample));
                ConsoleUi.Reject(string.Format(CultureInfo.InvariantCulture,
                    "{0}-> Uzorak odbijen: ActualMW={1} < 0 (row#{2})",
                    _received + 1, sample.ActualMW, sample.RowIndex));
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Field = "ActualMW", Reason = "ActualMW < 0.", RowIndex = sample.RowIndex },
                    new FaultReason("Validation: ActualMW negativan."));
            }
            if (sample.ForecastMW < 0)
            {
                _writer.WriteReject(sample.RowIndex, "ForecastMW < 0", SerializeRow(sample));
                ConsoleUi.Reject(string.Format(CultureInfo.InvariantCulture,
                    "{0}-> Uzorak odbijen: ForecastMW={1} < 0 (row#{2})",
                    _received + 1, sample.ForecastMW, sample.RowIndex));
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Field = "ForecastMW", Reason = "ForecastMW < 0.", RowIndex = sample.RowIndex },
                    new FaultReason("Validation: ForecastMW negativan."));
            }
            if (!string.Equals(sample.CountryCode, _meta.CountryCode, StringComparison.OrdinalIgnoreCase))
            {
                _writer.WriteReject(sample.RowIndex, "CountryCode != sesija", SerializeRow(sample));
                ConsoleUi.Reject(string.Format(CultureInfo.InvariantCulture,
                    "{0}-> Uzorak odbijen: CountryCode '{1}' != sesija '{2}'",
                    _received + 1, sample.CountryCode, _meta.CountryCode));
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Field = "CountryCode", Reason = "CountryCode ne odgovara sesiji.", RowIndex = sample.RowIndex },
                    new FaultReason("Validation: pogresan CountryCode."));
            }

            _writer.WriteSample(sample);
            _received++;

            ConsoleUi.Ok(string.Format(CultureInfo.InvariantCulture,
                "{0,3} -> Uzorak uspesno obradjen: row#{1}, H={2}, Actual={3} MW, Forecast={4} MW",
                _received, sample.RowIndex, sample.Hour, sample.ActualMW, sample.ForecastMW));

            EventBus.RaiseSampleReceived(this, new SampleEventArgs
            {
                Sample = sample,
                Received = _received,
                Total = _meta.TotalSamples
            });

            _analytics.Process(sample, this);

            return new AckResult
            {
                Status = AckStatus.Ok,
                Message = "Uzorak primljen.",
                Received = _received,
                Total = _meta.TotalSamples
            };
        }

        public AckResult EndSession()
        {
            int total = _meta != null ? _meta.TotalSamples : 0;
            ConsoleUi.Info(string.Format(CultureInfo.InvariantCulture,
                "Zavrsen prenos: primljeno {0}/{1} ({2:F2}%)",
                _received, total, total > 0 ? 100.0 * _received / total : 0.0));

            EventBus.RaiseTransferCompleted(this, new TransferEventArgs
            {
                CountryCode = _meta != null ? _meta.CountryCode : "",
                Date = _meta != null ? _meta.Date : DateTime.MinValue,
                SourceFileName = _meta != null ? _meta.SourceFileName : "",
                TotalSamples = total,
                Received = _received
            });

            var result = new AckResult
            {
                Status = AckStatus.Completed,
                Message = "Sesija zavrsena.",
                Received = _received,
                Total = total
            };

            _sessionActive = false;
            DisposeWriter();
            return result;
        }

        private static double ReadDouble(string key, double defaultValue)
        {
            string raw = ConfigurationManager.AppSettings[key];
            double v;
            if (!string.IsNullOrWhiteSpace(raw) &&
                double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return v;
            return defaultValue;
        }

        private static string SerializeRow(HourlyConsumptionSample s)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "row_index={0};utc={1:o};local={2:o};hour={3};actual={4};forecast={5};country={6}",
                s.RowIndex, s.TimestampUtc, s.TimestampLocal, s.Hour, s.ActualMW, s.ForecastMW, s.CountryCode);
        }

        private void DisposeWriter()
        {
            if (_writer != null)
            {
                _writer.Dispose();
                _writer = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                DisposeWriter();
            }
            _disposed = true;
        }

        ~ConsumptionService()
        {
            Dispose(false);
        }
    }
}
