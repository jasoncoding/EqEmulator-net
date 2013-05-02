using System;
using System.ServiceModel;

namespace EQEmulator.Servers.ServerTalk
{
    [ServiceContract]
    public interface IZoneService
    {
        [OperationContract]
        bool BootUp(ushort zoneId, string zoneName, ZoneInstanceType ziType);

        [OperationContract]
        int ExpectNewClient(int charId, string clientIp, bool isLocal);
    }
}