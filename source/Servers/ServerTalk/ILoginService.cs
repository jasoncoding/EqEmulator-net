using System;
using System.ServiceModel;

namespace EQEmulator.Servers.ServerTalk
{
    [ServiceContract]
    public interface ILoginService
    {
        [OperationContract(IsOneWay = true)]
        void ClientLogged();
    }
}