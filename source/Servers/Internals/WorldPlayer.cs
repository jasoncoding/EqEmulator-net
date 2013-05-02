using System;

namespace EQEmulator.Servers.Internals
{
    public enum WorldOnlineState
    {
        Offline,
        Online,
        CharSelect,
        Zoning,
        InZone
    }

    /// <summary>Represents info for the actual in game player for the world server</summary>
    internal class WorldPlayer
    {
        private int _acctId;
        private short _acctStatus;
        private string _charName;   // minor duplication, but necessary
        private bool _isLocalNet;
        private int _charId;
        private ushort _zoneId;
        private string _zoneName;
        private WorldOnlineState _onlineStatus;

        internal WorldPlayer(int accountId, short status, string charName, bool isLocal)
        {
            _acctId = accountId;
            _acctStatus = status;
            _charName = charName;
            _isLocalNet = isLocal;
            _onlineStatus = WorldOnlineState.Offline;
        }

        public int AccountId
        {
            get { return _acctId; }
            set { _acctId = value; }
        }

        public short AccountStatus
        {
            get { return _acctStatus; }
            set { _acctStatus = value; }
        }

        public int CharId
        {
            get { return _charId; }
            set { _charId = value; }
        }

        public string CharName
        {
            get { return _charName; }
            set { _charName = value; }
        }

        public bool IsLocalNet
        {
            get { return _isLocalNet; }
            set { _isLocalNet = value; }
        }

        public ushort ZoneId
        {
            get { return _zoneId; }
            set { _zoneId = value; }
        }

        public string ZoneName
        {
            get { return _zoneName; }
            set { _zoneName = value; }
        }

        public WorldOnlineState OnlineStatus
        {
            get { return _onlineStatus; }
            set { _onlineStatus = value; }
        }
    }
}
