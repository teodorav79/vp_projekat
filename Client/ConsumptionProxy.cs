using System;
using System.ServiceModel;
using Common;

namespace Client
{
    public class ConsumptionProxy : IDisposable
    {
        private ChannelFactory<IConsumptionService> _factory;
        private IConsumptionService _channel;
        private bool _disposed;

        public IConsumptionService Channel { get { return _channel; } }

        public ConsumptionProxy(string endpointConfigName)
        {
            _factory = new ChannelFactory<IConsumptionService>(endpointConfigName);
            _channel = _factory.CreateChannel();
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
                string channelState = "n/a";
                var ch = _channel as ICommunicationObject;
                if (ch != null)
                {
                    channelState = ch.State.ToString();
                    try
                    {
                        if (ch.State == CommunicationState.Faulted) ch.Abort();
                        else ch.Close();
                    }
                    catch { ch.Abort(); }
                }
                _channel = null;

                string factoryState = "n/a";
                if (_factory != null)
                {
                    factoryState = _factory.State.ToString();
                    try
                    {
                        if (_factory.State == CommunicationState.Faulted) _factory.Abort();
                        else _factory.Close();
                    }
                    catch { _factory.Abort(); }
                    _factory = null;
                }
                ConsoleUi.Info("[DISPOSE] ConsumptionProxy: channel (" + channelState + ") i ChannelFactory (" + factoryState + ") zatvoreni.");
            }
            _disposed = true;
        }

        ~ConsumptionProxy()
        {
            Dispose(false);
        }
    }
}
