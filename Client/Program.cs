using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.ServiceModel;
using System.Text;
using Common;

namespace Client
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.Title = "Evidencija potrosnje - CLIENT";
            }
            catch { }

            try
            {
                string csvPath = ConfigurationManager.AppSettings["CsvFilePath"];
                string countryCode = ConfigurationManager.AppSettings["CountryCode"];
                string selectedDayStr = ConfigurationManager.AppSettings["SelectedDay"];
                string rejectsPath = ConfigurationManager.AppSettings["RejectsFile"];
                string simulateAbortRaw = ConfigurationManager.AppSettings["SimulateAbortAfter"];
                int simulateAbortAfter;
                if (!int.TryParse(simulateAbortRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out simulateAbortAfter))
                    simulateAbortAfter = 0;

                if (string.IsNullOrWhiteSpace(csvPath))
                    throw new ConfigurationErrorsException("Nedostaje 'CsvFilePath' u App.config.");
                if (string.IsNullOrWhiteSpace(countryCode))
                    throw new ConfigurationErrorsException("Nedostaje 'CountryCode' u App.config.");
                if (string.IsNullOrWhiteSpace(selectedDayStr))
                    throw new ConfigurationErrorsException("Nedostaje 'SelectedDay' u App.config.");
                if (string.IsNullOrWhiteSpace(rejectsPath))
                    rejectsPath = "rejected_client.csv";

                DateTime selectedDay;
                if (!DateTime.TryParseExact(selectedDayStr, "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out selectedDay))
                {
                    throw new ConfigurationErrorsException("'SelectedDay' mora biti u formatu YYYY-MM-DD.");
                }

                ConsoleUi.Info("Klijent - Evidencija potrosnje");
                ConsoleUi.Info("CSV     : " + csvPath);
                ConsoleUi.Info("Country : " + countryCode);
                ConsoleUi.Info("Day     : " + selectedDay.ToString("yyyy-MM-dd"));
                ConsoleUi.Info("Rejects : " + rejectsPath);
                if (simulateAbortAfter > 0)
                    ConsoleUi.Info("Simulate: prekid prenosa posle " + simulateAbortAfter + " uzoraka");
                ConsoleUi.Blank();

                int totalForDay;
                using (var counter = new CsvDayLoader(csvPath, countryCode, selectedDay, rejectsPath))
                {
                    totalForDay = counter.CountDaySamples();
                    ConsoleUi.Info(string.Format(CultureInfo.InvariantCulture,
                        "Pre-scan: validnih={0}, odbijenih={1} -> {2}",
                        totalForDay, counter.RejectedCount, counter.RejectsPath));
                }

                if (totalForDay <= 0)
                {
                    ConsoleUi.Fault("STOP", "Nema validnih uzoraka za izabrani dan. Prekidam.");
                    ConsoleUi.Blank();
                    ConsoleUi.Info("Pritisnite ENTER za izlaz.");
                    Console.ReadLine();
                    return;
                }

                using (var loader = new CsvDayLoader(csvPath, countryCode, selectedDay, rejectsPath))
                using (var proxy = new ConsumptionProxy("ConsumptionEndpoint"))
                {
                    var meta = new SessionMeta
                    {
                        CountryCode = countryCode.ToUpperInvariant(),
                        Date = selectedDay,
                        SourceFileName = Path.GetFileName(csvPath),
                        TotalSamples = totalForDay
                    };

                    try
                    {
                        var startAck = proxy.Channel.StartSession(meta);
                        ConsoleUi.Info("StartSession ACK: " + startAck.Status + " - " + startAck.Message);
                        ConsoleUi.Blank();

                        int sent = 0;
                        int idx = 0;
                        foreach (var sample in loader.EnumerateDaySamples())
                        {
                            idx++;
                            if (simulateAbortAfter > 0 && sent >= simulateAbortAfter)
                            {
                                ConsoleUi.Fault("SIMULATION",
                                    "Prekid prenosa posle " + sent + " poslatih uzoraka. Bacam izuzetak da proverim oslobadjanje resursa.");
                                throw new InvalidOperationException("Simulacija prekida prenosa.");
                            }
                            try
                            {
                                var ack = proxy.Channel.PushSample(sample);
                                sent++;
                                double pct = ack.Total > 0 ? 100.0 * ack.Received / ack.Total : 0.0;
                                if (ack.Status == AckStatus.Missing)
                                {
                                    ConsoleUi.Reject(string.Format(CultureInfo.InvariantCulture,
                                        "{0,3}-> Uzorak odbijen na serveru: NaN/missing (row#{1})  [prenos u toku {2}/{3}, {4:F2}%]",
                                        idx, sample.RowIndex, ack.Received, ack.Total, pct));
                                }
                                else
                                {
                                    ConsoleUi.Ok(string.Format(CultureInfo.InvariantCulture,
                                        "{0,3} -> Uzorak uspesno obradjen: {1}  [prenos u toku {2}/{3}, {4:F2}%]",
                                        idx, ack.Message, ack.Received, ack.Total, pct));
                                }
                            }
                            catch (FaultException<ValidationFault> fex)
                            {
                                ConsoleUi.Reject(string.Format(CultureInfo.InvariantCulture,
                                    "{0,3}-> Uzorak odbijen zbog ne validnih podataka: {1} - {2} (row#{3})",
                                    idx, fex.Detail.Field, fex.Detail.Reason, fex.Detail.RowIndex));
                            }
                            catch (FaultException<DataFormatFault> fex)
                            {
                                ConsoleUi.Reject(string.Format(CultureInfo.InvariantCulture,
                                    "{0,3}-> Uzorak odbijen zbog formata: {1} (row#{2})",
                                    idx, fex.Detail.Reason, fex.Detail.RowIndex));
                            }
                        }

                        var endAck = proxy.Channel.EndSession();
                        ConsoleUi.Blank();
                        ConsoleUi.Info(string.Format(CultureInfo.InvariantCulture,
                            "EndSession ACK: {0} - primljeno {1}/{2}",
                            endAck.Status, endAck.Received, endAck.Total));
                        ConsoleUi.Info(string.Format(CultureInfo.InvariantCulture,
                            "Klijent: poslato {0}, odbijeno lokalno {1}",
                            sent, loader.RejectedCount));
                    }
                    catch (FaultException<ValidationFault> fex)
                    {
                        ConsoleUi.Fault("VALIDATION", "StartSession: " + fex.Detail.Field + " - " + fex.Detail.Reason);
                    }
                    catch (FaultException<DataFormatFault> fex)
                    {
                        ConsoleUi.Fault("DATAFORMAT", "StartSession: " + fex.Detail.Reason);
                    }
                    catch (CommunicationException cex)
                    {
                        ConsoleUi.Fault("COMM", cex.Message);
                    }
                    catch (TimeoutException tex)
                    {
                        ConsoleUi.Fault("TIMEOUT", tex.Message);
                    }
                    catch (InvalidOperationException sim) when (sim.Message == "Simulacija prekida prenosa.")
                    {
                        ConsoleUi.Fault("SIMULATION",
                            "Izuzetak uhvacen u inner-catch-u. Izlazim iz using-blokova: Dispose ce se pozvati za loader i proxy.");
                        throw;
                    }
                }

                ConsoleUi.Blank();
                ConsoleUi.Info("Pritisnite ENTER za izlaz.");
                Console.ReadLine();
            }
            catch (InvalidOperationException sim) when (sim.Message == "Simulacija prekida prenosa.")
            {
                ConsoleUi.Blank();
                ConsoleUi.Info("[SIMULATION] Posle bacanja izuzetka, using/finally su sigurno pozvali Dispose.");
                ConsoleUi.Info("[SIMULATION] Pogledaj iznad linije '[DISPOSE]' kao dokaz da su resursi oslobodjeni.");
                ConsoleUi.Blank();
                ConsoleUi.Info("Pritisnite ENTER za izlaz.");
                Console.ReadLine();
            }
            catch (InvalidOperationException ioex)
            {
                ConsoleUi.Fault("CONFIG", ioex.Message);
                ConsoleUi.Info("Pritisnite ENTER za izlaz.");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                ConsoleUi.Fault("ERROR", ex.Message);
                ConsoleUi.Info("Pritisnite ENTER za izlaz.");
                Console.ReadLine();
            }
        }
    }
}
