using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Common;

namespace Client
{
    public class CsvDayLoader : IDisposable
    {
        private readonly string _filePath;
        private readonly string _countryCode;
        private readonly DateTime _selectedDay;
        private readonly string _rejectsPath;

        private FileStream _fileStream;
        private StreamReader _reader;
        private FileStream _rejectsStream;
        private StreamWriter _rejectsWriter;

        private bool _disposed;

        private int _actualColIndex = -1;
        private int _forecastColIndex = -1;
        private int _utcColIndex = -1;
        private int _cetColIndex = -1;

        public int RejectedCount { get; private set; }
        public int LoadedCount { get; private set; }

        public string FilePath { get { return _filePath; } }
        public string CountryCode { get { return _countryCode; } }
        public DateTime SelectedDay { get { return _selectedDay; } }
        public string RejectsPath { get { return _rejectsPath; } }

        public CsvDayLoader(string filePath, string countryCode, DateTime selectedDay, string rejectsPath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("CSV putanja je obavezna.", "filePath");
            if (string.IsNullOrWhiteSpace(countryCode))
                throw new ArgumentException("CountryCode je obavezan.", "countryCode");

            _filePath = filePath;
            _countryCode = countryCode.Trim().ToUpperInvariant();
            _selectedDay = selectedDay.Date;
            _rejectsPath = rejectsPath;

            if (!File.Exists(_filePath))
                throw new FileNotFoundException("CSV fajl ne postoji.", _filePath);

            _fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _reader = new StreamReader(_fileStream);

            _rejectsStream = new FileStream(_rejectsPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _rejectsWriter = new StreamWriter(_rejectsStream);
            _rejectsWriter.WriteLine("row_index,reason,original_line");

            ReadHeaderAndValidateColumns();
        }

        private void ReadHeaderAndValidateColumns()
        {
            string header = _reader.ReadLine();
            if (header == null)
                throw new InvalidDataException("CSV nema header red.");

            string[] columns = header.Split(',');

            string actualName = _countryCode + "_load_actual_entsoe_transparency";
            string forecastName = _countryCode + "_load_forecast_entsoe_transparency";

            for (int i = 0; i < columns.Length; i++)
            {
                string col = columns[i].Trim();
                if (col.Equals("utc_timestamp", StringComparison.OrdinalIgnoreCase))
                    _utcColIndex = i;
                else if (col.Equals("cet_cest_timestamp", StringComparison.OrdinalIgnoreCase))
                    _cetColIndex = i;
                else if (col.Equals(actualName, StringComparison.OrdinalIgnoreCase))
                    _actualColIndex = i;
                else if (col.Equals(forecastName, StringComparison.OrdinalIgnoreCase))
                    _forecastColIndex = i;
            }

            if (_utcColIndex < 0)
                throw new InvalidOperationException("Konfiguraciona greska: kolona 'utc_timestamp' ne postoji u CSV.");
            if (_cetColIndex < 0)
                throw new InvalidOperationException("Konfiguraciona greska: kolona 'cet_cest_timestamp' ne postoji u CSV.");
            if (_actualColIndex < 0 || _forecastColIndex < 0)
            {
                throw new InvalidOperationException(string.Format(
                    "Konfiguraciona greska: za zemlju '{0}' ne postoje obe kolone actual i forecast (trazeno: {1}, {2}).",
                    _countryCode, actualName, forecastName));
            }
        }

        public IEnumerable<HourlyConsumptionSample> EnumerateDaySamples()
        {
            int rowIndex = 0;
            string line;
            while ((line = _reader.ReadLine()) != null)
            {
                rowIndex++;
                HourlyConsumptionSample sample;
                if (TryParseRow(line, rowIndex, out sample))
                {
                    if (sample.TimestampUtc.Date == _selectedDay ||
                        sample.TimestampLocal.Date == _selectedDay)
                    {
                        LoadedCount++;
                        yield return sample;
                    }
                }
            }
        }

        private bool TryParseRow(string line, int rowIndex, out HourlyConsumptionSample sample)
        {
            sample = null;
            if (string.IsNullOrWhiteSpace(line))
            {
                WriteReject(rowIndex, "prazan red", line);
                return false;
            }

            string[] parts = line.Split(',');
            int needed = Math.Max(Math.Max(_utcColIndex, _cetColIndex),
                                  Math.Max(_actualColIndex, _forecastColIndex));
            if (parts.Length <= needed)
            {
                WriteReject(rowIndex, "premalo kolona", line);
                return false;
            }

            string utcStr = parts[_utcColIndex].Trim();
            string cetStr = parts[_cetColIndex].Trim();
            string actualStr = parts[_actualColIndex].Trim();
            string forecastStr = parts[_forecastColIndex].Trim();

            DateTime utc;
            if (!TryParseUtc(utcStr, out utc))
            {
                WriteReject(rowIndex, "neispravan utc_timestamp", line);
                return false;
            }

            DateTime local;
            if (!TryParseLocal(cetStr, out local))
            {
                WriteReject(rowIndex, "neispravan cet_cest_timestamp", line);
                return false;
            }

            if (IsMissing(actualStr))
            {
                WriteReject(rowIndex, "ActualMW je NaN/prazno", line);
                return false;
            }

            if (IsMissing(forecastStr))
            {
                WriteReject(rowIndex, "ForecastMW je NaN/prazno", line);
                return false;
            }

            double actual;
            if (!double.TryParse(actualStr, NumberStyles.Float, CultureInfo.InvariantCulture, out actual))
            {
                WriteReject(rowIndex, "ActualMW nije broj", line);
                return false;
            }

            double forecast;
            if (!double.TryParse(forecastStr, NumberStyles.Float, CultureInfo.InvariantCulture, out forecast))
            {
                WriteReject(rowIndex, "ForecastMW nije broj", line);
                return false;
            }

            sample = new HourlyConsumptionSample
            {
                TimestampUtc = utc,
                TimestampLocal = local,
                Hour = utc.Hour,
                ActualMW = actual,
                ForecastMW = forecast,
                CountryCode = _countryCode,
                RowIndex = rowIndex
            };
            return true;
        }

        private static bool IsMissing(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return true;
            string t = s.Trim();
            return t.Equals("NaN", StringComparison.OrdinalIgnoreCase) ||
                   t.Equals("nan", StringComparison.OrdinalIgnoreCase) ||
                   t.Equals("NA", StringComparison.OrdinalIgnoreCase) ||
                   t.Equals("null", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseUtc(string s, out DateTime dt)
        {
            return DateTime.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt);
        }

        private static bool TryParseLocal(string s, out DateTime dt)
        {
            return DateTime.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out dt);
        }

        private void WriteReject(int rowIndex, string reason, string originalLine)
        {
            RejectedCount++;
            string safe = originalLine == null ? "" : originalLine.Replace("\"", "\"\"");
            _rejectsWriter.WriteLine("{0},{1},\"{2}\"", rowIndex, reason, safe);
        }

        public int CountDaySamples()
        {
            // Pomocna metoda: prebroj redove izabranog dana bez slanja.
            // Resetuje stream na pocetak i potom ga vraca posle skeniranja.
            long savedPos = _fileStream.Position;
            _reader.DiscardBufferedData();
            _fileStream.Seek(0, SeekOrigin.Begin);
            _reader.ReadLine(); // skip header

            int count = 0;
            int rowIndex = 0;
            string line;
            while ((line = _reader.ReadLine()) != null)
            {
                rowIndex++;
                HourlyConsumptionSample s;
                if (TryParseRow(line, rowIndex, out s))
                {
                    if (s.TimestampUtc.Date == _selectedDay ||
                        s.TimestampLocal.Date == _selectedDay)
                    {
                        count++;
                    }
                }
            }

            // Posle prebrajanja, izbroj-faza zapisuje u rejects pa moramo da resetujemo brojace
            // i ponovo otvorimo rejects fajl (truncate) i pozicioniramo CSV reader.
            RejectedCount = 0;
            _rejectsWriter.Flush();
            _rejectsStream.SetLength(0);
            _rejectsStream.Seek(0, SeekOrigin.Begin);
            _rejectsWriter.WriteLine("row_index,reason,original_line");

            _reader.DiscardBufferedData();
            _fileStream.Seek(0, SeekOrigin.Begin);
            _reader.ReadLine(); // skip header back to data start

            return count;
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
                if (_rejectsWriter != null)
                {
                    try { _rejectsWriter.Flush(); } catch { }
                    _rejectsWriter.Dispose();
                    _rejectsWriter = null;
                }
                if (_rejectsStream != null)
                {
                    _rejectsStream.Dispose();
                    _rejectsStream = null;
                }
                if (_reader != null)
                {
                    _reader.Dispose();
                    _reader = null;
                }
                if (_fileStream != null)
                {
                    _fileStream.Dispose();
                    _fileStream = null;
                }
            }
            _disposed = true;
        }

        ~CsvDayLoader()
        {
            Dispose(false);
        }
    }
}
