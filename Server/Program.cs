using System;
using System.ServiceModel;
using System.Text;

namespace Server
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.Title = "Evidencija potrosnje - SERVER";
            }
            catch { }

            ServiceHost host = null;
            ConsoleSubscriber subscriber = null;
            try
            {
                subscriber = new ConsoleSubscriber();
                host = new ServiceHost(typeof(ConsumptionService));
                host.Open();

                ConsoleUi.Info("WCF servis 'ConsumptionService' je pokrenut.");
                foreach (var ep in host.Description.Endpoints)
                {
                    ConsoleUi.Info("Endpoint: " + ep.Address);
                    ConsoleUi.Info("Binding : " + ep.Binding.Name);
                    ConsoleUi.Info("Contract: " + ep.Contract.ContractType.FullName);
                }
                ConsoleUi.Info("Pretplate aktivne: OnTransferStarted, OnSampleReceived, OnTransferCompleted, OnWarningRaised.");
                ConsoleUi.Blank();
                ConsoleUi.Info("Pritisnite ENTER za zaustavljanje servisa...");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                ConsoleUi.Fault("ERROR", "Greska prilikom pokretanja servisa: " + ex.Message);
            }
            finally
            {
                if (subscriber != null) subscriber.Unsubscribe();
                if (host != null)
                {
                    try
                    {
                        if (host.State == CommunicationState.Faulted) host.Abort();
                        else host.Close();
                    }
                    catch
                    {
                        host.Abort();
                    }
                }
                ConsoleUi.Info("Servis zatvoren. Resursi oslobodjeni.");
            }
        }
    }
}
