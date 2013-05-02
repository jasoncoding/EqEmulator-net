using System;
using System.ServiceModel;

using EQEmulator.Servers.Internals.Data;

namespace EQEmulator.Servers.ServerTalk
{
    [ServiceContract]
    public interface IWorldService
    {
        [OperationContract(IsOneWay=true)]
        void ExpectNewClient(string clientIp, bool isLocal);

        [OperationContract(IsOneWay = true)]
        void UpdateWho(string clientIp, int charId, string charName, bool gm, bool admin, int zoneId, byte race, byte charClass,
            byte level, bool anonymous, int guildId, bool lfg);

        [OperationContract(IsOneWay = true)]
        void ClientLogged(string clientIp);

        [OperationContract(IsOneWay = false)]
        int ZoneToZone(ZoneToZone ztz);

        [OperationContract(IsOneWay = true)]
        void ZoneUnloaded(int port);

        [OperationContract(IsOneWay = false)]
        short? GetSkillCap(byte skillId, byte classId, byte level);

        [OperationContract(IsOneWay = false)]
        Spell GetSpellById(uint spellId);

        [OperationContract(IsOneWay = false)]
        int GetMaxSpellId();
    }
}
