using System;
using System.ServiceModel;
using Common;

namespace Server
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession,
                     ConcurrencyMode = ConcurrencyMode.Single)]
    public class ConsumptionService : IConsumptionService
    {
        private SessionMeta _meta;
        private int _received;
        private bool _sessionActive;

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

            Console.WriteLine();
            Console.WriteLine("=== StartSession ===");
            Console.WriteLine("  Country : {0}", meta.CountryCode);
            Console.WriteLine("  Date    : {0:yyyy-MM-dd}", meta.Date);
            Console.WriteLine("  Source  : {0}", meta.SourceFileName);
            Console.WriteLine("  Total   : {0} uzoraka", meta.TotalSamples);

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
                Console.WriteLine("  [{0}/{1}] MISSING (NaN) row#{2} H={3} -> rejects",
                    _received, _meta.TotalSamples, sample.RowIndex, sample.Hour);
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
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Field = "Hour", Reason = "Hour van opsega [0,23].", RowIndex = sample.RowIndex },
                    new FaultReason("Validation: Hour van opsega."));
            }

            if (sample.ActualMW < 0)
            {
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Field = "ActualMW", Reason = "ActualMW < 0.", RowIndex = sample.RowIndex },
                    new FaultReason("Validation: ActualMW negativan."));
            }

            if (sample.ForecastMW < 0)
            {
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Field = "ForecastMW", Reason = "ForecastMW < 0.", RowIndex = sample.RowIndex },
                    new FaultReason("Validation: ForecastMW negativan."));
            }

            if (!string.Equals(sample.CountryCode, _meta.CountryCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new FaultException<ValidationFault>(
                    new ValidationFault { Field = "CountryCode", Reason = "CountryCode ne odgovara sesiji.", RowIndex = sample.RowIndex },
                    new FaultReason("Validation: pogresan CountryCode."));
            }

            _received++;

            Console.WriteLine("  [{0}/{1}] OK row#{2} {3:yyyy-MM-dd HH:mm}Z H={4} Actual={5} MW Forecast={6} MW",
                _received, _meta.TotalSamples, sample.RowIndex, sample.TimestampUtc,
                sample.Hour, sample.ActualMW, sample.ForecastMW);

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
            Console.WriteLine("=== EndSession ===");
            Console.WriteLine("  Primljeno: {0}/{1}",
                _received, _meta != null ? _meta.TotalSamples : 0);

            var result = new AckResult
            {
                Status = AckStatus.Completed,
                Message = "Sesija zavrsena.",
                Received = _received,
                Total = _meta != null ? _meta.TotalSamples : 0
            };

            _sessionActive = false;
            return result;
        }
    }
}
