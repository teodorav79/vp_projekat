using System;

namespace Server
{
    public static class EventBus
    {
        public static event EventHandler<TransferEventArgs> OnTransferStarted;
        public static event EventHandler<SampleEventArgs> OnSampleReceived;
        public static event EventHandler<TransferEventArgs> OnTransferCompleted;
        public static event EventHandler<WarningEventArgs> OnWarningRaised;

        public static void RaiseTransferStarted(object sender, TransferEventArgs e)
        {
            var h = OnTransferStarted;
            if (h != null) h(sender, e);
        }

        public static void RaiseSampleReceived(object sender, SampleEventArgs e)
        {
            var h = OnSampleReceived;
            if (h != null) h(sender, e);
        }

        public static void RaiseTransferCompleted(object sender, TransferEventArgs e)
        {
            var h = OnTransferCompleted;
            if (h != null) h(sender, e);
        }

        public static void RaiseWarning(object sender, WarningEventArgs e)
        {
            var h = OnWarningRaised;
            if (h != null) h(sender, e);
        }
    }
}
