using System;
using System.Globalization;
using System.IO;
using Common;

namespace Server
{
    public class SessionFileWriter : IDisposable
    {
        private FileStream _sessionStream;
        private StreamWriter _sessionWriter;
        private FileStream _rejectsStream;
        private StreamWriter _rejectsWriter;
        private bool _disposed;

        public string SessionPath { get; private set; }
        public string RejectsPath { get; private set; }

        public SessionFileWriter(string dataRoot, string countryCode, DateTime date)
        {
            if (string.IsNullOrWhiteSpace(dataRoot)) dataRoot = "Data";
            string dir = Path.Combine(dataRoot, countryCode, date.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(dir);

            SessionPath = Path.Combine(dir, "session.csv");
            RejectsPath = Path.Combine(dir, "rejects.csv");

            bool sessionExisted = File.Exists(SessionPath) && new FileInfo(SessionPath).Length > 0;
            _sessionStream = new FileStream(SessionPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            _sessionWriter = new StreamWriter(_sessionStream);
            if (!sessionExisted)
            {
                _sessionWriter.WriteLine("row_index,timestamp_utc,timestamp_local,hour,actual_mw,forecast_mw,country_code");
                _sessionWriter.Flush();
            }

            bool rejectsExisted = File.Exists(RejectsPath) && new FileInfo(RejectsPath).Length > 0;
            _rejectsStream = new FileStream(RejectsPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            _rejectsWriter = new StreamWriter(_rejectsStream);
            if (!rejectsExisted)
            {
                _rejectsWriter.WriteLine("row_index,reason,original_row");
                _rejectsWriter.Flush();
            }
        }

        public void WriteSample(HourlyConsumptionSample s)
        {
            _sessionWriter.WriteLine("{0},{1},{2},{3},{4},{5},{6}",
                s.RowIndex,
                s.TimestampUtc.ToString("o", CultureInfo.InvariantCulture),
                s.TimestampLocal.ToString("o", CultureInfo.InvariantCulture),
                s.Hour,
                s.ActualMW.ToString(CultureInfo.InvariantCulture),
                s.ForecastMW.ToString(CultureInfo.InvariantCulture),
                s.CountryCode);
            _sessionWriter.Flush();
        }

        public void WriteReject(int rowIndex, string reason, string originalRow)
        {
            string safe = originalRow == null ? "" : originalRow.Replace("\"", "\"\"");
            _rejectsWriter.WriteLine("{0},{1},\"{2}\"", rowIndex, reason, safe);
            _rejectsWriter.Flush();
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
                if (_sessionWriter != null)
                {
                    try { _sessionWriter.Flush(); } catch { }
                    _sessionWriter.Dispose();
                    _sessionWriter = null;
                }
                if (_sessionStream != null)
                {
                    _sessionStream.Dispose();
                    _sessionStream = null;
                }
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
            }
            _disposed = true;
        }

        ~SessionFileWriter()
        {
            Dispose(false);
        }
    }
}
