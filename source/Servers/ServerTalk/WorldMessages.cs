using System.Runtime.Serialization;

namespace EQEmulator.Servers.ServerTalk
{
    [DataContract]
    public class ZoneToZone
    {
        [DataMember]
        public string CharName;
        [DataMember]
        public int CharId;
        [DataMember]
        public string ClientIp;
        [DataMember]
        public bool IsLocalNet;
        [DataMember]
        public ushort RequestedZoneId;
        [DataMember]
        public ushort CurrentZoneId;
        [DataMember]
        public short AccountStatus;
        [DataMember]
        public byte IgnoreRestrictions;
    }
}