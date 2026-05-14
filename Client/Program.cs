using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.ServiceModel;
using Common;

namespace Client
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string csvPath = ConfigurationManager.AppSettings["CsvFilePath"];
                string countryCode = ConfigurationManager.AppSettings["CountryCode"];
                string selectedDayStr = ConfigurationManager.AppSettings["SelectedDay"];
                string rejectsPath = ConfigurationManager.AppSettings["RejectsFile"];

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
                    throw new ConfigurationErrorsException(
                        "'SelectedDay' mora biti u formatu YYYY-MM-DD.");
                }

                Console.WriteLine("Klijent - Evidencija potrosnje");
                Console.WriteLine("  CSV       : {0}", csvPath);
                Console.WriteLine("  Country   : {0}", countryCode);
                Console.WriteLine("  Day       : {0:yyyy-MM-dd}", selectedDay);
                Console.WriteLine("  Rejects   : {0}", rejectsPath);
                Console.WriteLine();

                // Pre slanja: prebroj redove izabranog dana radi popunjavanja TotalSamples u meti.
                int totalForDay;
                using (var counter = new CsvDayLoader(csvPath, countryCode, selectedDay, rejectsPath))
                {
                    totalForDay = counter.CountDaySamples();
                    Console.WriteLine("Pronadjeno validnih uzoraka za dan: {0}", totalForDay);
                    Console.WriteLine("Odbijenih (pre-scan): {0} - upisano u {1}",
                        counter.RejectedCount, counter.RejectsPath);
                }

                if (totalForDay <= 0)
                {
                    Console.WriteLine("Nema validnih uzoraka za izabrani dan. Prekidam.");
                    Console.WriteLine("Pritisnite ENTER za izlaz.");
                    Console.ReadLine();
                    return;
                }

                // Slanje samplova preko WCF - sve resurse oslobadjamo preko using.
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
                        Console.WriteLine("Server StartSession ACK: {0} - {1}",
                            startAck.Status, startAck.Message);

                        int sent = 0;
                        foreach (var sample in loader.EnumerateDaySamples())
                        {
                            try
                            {
                                var ack = proxy.Channel.PushSample(sample);
                                sent++;
                                Console.WriteLine("  -> poslat row#{0} H={1} ack={2} ({3}/{4})",
                                    sample.RowIndex, sample.Hour, ack.Status, ack.Received, ack.Total);
                            }
                            catch (FaultException<ValidationFault> fex)
                            {
                                Console.WriteLine("  !! ValidationFault row#{0}: {1} - {2}",
                                    fex.Detail.RowIndex, fex.Detail.Field, fex.Detail.Reason);
                            }
                            catch (FaultException<DataFormatFault> fex)
                            {
                                Console.WriteLine("  !! DataFormatFault row#{0}: {1}",
                                    fex.Detail.RowIndex, fex.Detail.Reason);
                            }
                        }

                        var endAck = proxy.Channel.EndSession();
                        Console.WriteLine();
                        Console.WriteLine("Server EndSession ACK: {0} - primljeno {1}/{2}",
                            endAck.Status, endAck.Received, endAck.Total);
                        Console.WriteLine("Klijent: poslato {0}, odbijeno lokalno {1}.",
                            sent, loader.RejectedCount);
                    }
                    catch (FaultException<ValidationFault> fex)
                    {
                        Console.WriteLine("StartSession ValidationFault: {0} - {1}",
                            fex.Detail.Field, fex.Detail.Reason);
                    }
                    catch (FaultException<DataFormatFault> fex)
                    {
                        Console.WriteLine("StartSession DataFormatFault: {0}", fex.Detail.Reason);
                    }
                    catch (CommunicationException cex)
                    {
                        Console.WriteLine("Greska komunikacije: {0}", cex.Message);
                    }
                    catch (TimeoutException tex)
                    {
                        Console.WriteLine("Timeout: {0}", tex.Message);
                    }
                }

                Console.WriteLine();
                Console.WriteLine("Pritisnite ENTER za izlaz.");
                Console.ReadLine();
            }
            catch (InvalidOperationException ioex)
            {
                // Konfiguraciona greska iz CsvDayLoader (npr. nema kolona za zemlju)
                Console.WriteLine("Konfiguraciona greska: {0}", ioex.Message);
                Console.WriteLine("Pritisnite ENTER za izlaz.");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Greska: {0}", ex.Message);
                Console.WriteLine("Pritisnite ENTER za izlaz.");
                Console.ReadLine();
            }
        }
    }
}
