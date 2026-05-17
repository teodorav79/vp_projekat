using System;
using System.Globalization;
using Common;

namespace Server
{
    public class Analytics
    {
        private readonly double _alpha;
        private readonly double _beta;
        private readonly double _spikeDelta;
        private readonly double _dailyLimit;
        private readonly string _meterId;

        private bool _hasPrevious;
        private double _previousActual;
        private int _previousHour;
        private double _dailySum;
        private bool _dailyLimitRaised;

        public Analytics(double alpha, double beta, double spikeDelta, double dailyLimit, string meterId)
        {
            _alpha = alpha;
            _beta = beta;
            _spikeDelta = spikeDelta;
            _dailyLimit = dailyLimit;
            _meterId = meterId;
        }

        public void Process(HourlyConsumptionSample s, object sender)
        {
            if (s == null) return;
            if (double.IsNaN(s.ActualMW) || double.IsNaN(s.ForecastMW)) return;

            if (s.ForecastMW > 0.0)
            {
                if (s.ActualMW < _alpha * s.ForecastMW)
                {
                    EventBus.RaiseWarning(sender, new WarningEventArgs
                    {
                        Type = WarningType.UnderConsumptionWarning,
                        Hour = s.Hour,
                        ActualMW = s.ActualMW,
                        ForecastMW = s.ForecastMW,
                        MeterID = _meterId,
                        Threshold = _alpha,
                        Message = string.Format(CultureInfo.InvariantCulture,
                            "ActualMW {0} < {1} * ForecastMW {2}", s.ActualMW, _alpha, s.ForecastMW)
                    });
                }
                else if (s.ActualMW > _beta * s.ForecastMW)
                {
                    EventBus.RaiseWarning(sender, new WarningEventArgs
                    {
                        Type = WarningType.OverConsumptionWarning,
                        Hour = s.Hour,
                        ActualMW = s.ActualMW,
                        ForecastMW = s.ForecastMW,
                        MeterID = _meterId,
                        Threshold = _beta,
                        Message = string.Format(CultureInfo.InvariantCulture,
                            "ActualMW {0} > {1} * ForecastMW {2}", s.ActualMW, _beta, s.ForecastMW)
                    });
                }
            }

            if (_hasPrevious)
            {
                double delta = s.ActualMW - _previousActual;
                if (Math.Abs(delta) > _spikeDelta)
                {
                    string dir = delta > 0 ? "up" : "down";
                    EventBus.RaiseWarning(sender, new WarningEventArgs
                    {
                        Type = WarningType.ConsumptionSpikeWarning,
                        Hour = s.Hour,
                        ActualMW = s.ActualMW,
                        ForecastMW = s.ForecastMW,
                        MeterID = _meterId,
                        Delta = delta,
                        Direction = dir,
                        Threshold = _spikeDelta,
                        Message = string.Format(CultureInfo.InvariantCulture,
                            "|delta| {0} > SpikeDeltaMW {1} (smer: {2}), H[{3}]->H[{4}]",
                            Math.Abs(delta), _spikeDelta, dir, _previousHour, s.Hour)
                    });
                }
            }
            _hasPrevious = true;
            _previousActual = s.ActualMW;
            _previousHour = s.Hour;

            _dailySum += s.ActualMW;
            if (!_dailyLimitRaised && _dailySum > _dailyLimit)
            {
                _dailyLimitRaised = true;
                EventBus.RaiseWarning(sender, new WarningEventArgs
                {
                    Type = WarningType.DailyLimitExceededWarning,
                    Hour = s.Hour,
                    ActualMW = s.ActualMW,
                    ForecastMW = s.ForecastMW,
                    MeterID = _meterId,
                    DailySum = _dailySum,
                    Threshold = _dailyLimit,
                    Message = string.Format(CultureInfo.InvariantCulture,
                        "DailySum {0} > DailyLimitMW {1}", _dailySum, _dailyLimit)
                });
            }
        }
    }
}
