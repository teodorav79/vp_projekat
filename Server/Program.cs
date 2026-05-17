using System;
using System.ServiceModel;

namespace Server
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ServiceHost host = null;
            ConsoleSubscriber subscriber = null;
            try
            {
                subscriber = new ConsoleSubscriber();
                host = new ServiceHost(typeof(ConsumptionService));
                host.Open();

                Console.WriteLine("WCF servis 'ConsumptionService' je pokrenut.");
                foreach (var ep in host.Description.Endpoints)
                {
                    Console.WriteLine("  Endpoint: {0}", ep.Address);
                    Console.WriteLine("  Binding : {0}", ep.Binding.Name);
                    Console.WriteLine("  Contract: {0}", ep.Contract.ContractType.FullName);
                }
                Console.WriteLine();
                Console.WriteLine("Pretplate aktivne: OnTransferStarted, OnSampleReceived, OnTransferCompleted, OnWarningRaised.");
                Console.WriteLine();
                Console.WriteLine("Pritisnite ENTER za zaustavljanje servisa...");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Greska prilikom pokretanja servisa: {0}", ex.Message);
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
                Console.WriteLine("Servis zatvoren. Resursi oslobodjeni.");
            }
        }
    }
}
