using System.ServiceModel;

namespace Common
{
    [ServiceContract(SessionMode = SessionMode.Required)]
    public interface IConsumptionService
    {
        [OperationContract(IsInitiating = true, IsTerminating = false)]
        [FaultContract(typeof(DataFormatFault))]
        [FaultContract(typeof(ValidationFault))]
        AckResult StartSession(SessionMeta meta);

        [OperationContract(IsInitiating = false, IsTerminating = false)]
        [FaultContract(typeof(DataFormatFault))]
        [FaultContract(typeof(ValidationFault))]
        AckResult PushSample(HourlyConsumptionSample sample);

        [OperationContract(IsInitiating = false, IsTerminating = true)]
        AckResult EndSession();
    }
}
