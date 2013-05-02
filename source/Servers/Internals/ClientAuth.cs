using System;

namespace EQEmulator.Servers.Internals
{
    class ClientAuth
    {
        private string _clientIp;
        private bool _isLocal;      // really only used for world auth
        private DateTime _timeAdded;

        public ClientAuth(string ip, bool isLocal, DateTime timeAdded)
        {
            _clientIp = ip;
            _isLocal = isLocal;
            _timeAdded = timeAdded;
        }

        public bool IsLocal
        {
            get { return _isLocal; }
            set { _isLocal = value; }
        }

        public DateTime TimeAdded
        {
            get { return _timeAdded; }
            set { _timeAdded = value; }
        }

        public string ClientIp
        {
            get { return _clientIp; }
            set { _clientIp = value; }
        }
    }

    class ClientWho : ClientAuth
    {
        private string _charName;
        private int _charId, _zoneId, _guildId;
        private bool _gm, _admin, _anonymous, _lfg;
        private byte _race, _class, _level;

        public ClientWho(string clientIp, bool isLocal, int charId, string charName, bool gm, bool admin, int zoneId, byte race, byte charClass,
            byte level, bool anonymous, int guildId, bool lfg)
            : base(clientIp, isLocal, DateTime.Now)
        {
            _charId = charId;
            _charName = charName;
            _gm = gm;
            _admin = admin;
            _zoneId = zoneId;
            _race = race;
            _class = charClass;
            _level = level;
            _anonymous = anonymous;
            _guildId = guildId;
            _lfg = lfg;
        }

        public ClientWho(string clientIp, bool isLocal)
            : base(clientIp, isLocal, DateTime.Now)
        { }

        public string CharName
        {
            get { return _charName; }
            set { _charName = value; }
        }

        public int CharId
        {
            get { return _charId; }
            set { _charId = value; }
        }

        public int ZoneId
        {
            get { return _zoneId; }
            set { _zoneId = value; }
        }

        public int GuildId
        {
            get { return _guildId; }
            set { _guildId = value; }
        }

        public bool GM
        {
            get { return _gm; }
            set { _gm = value; }
        }

        public bool Admin
        {
            get { return _admin; }
            set { _admin = value; }
        }

        public bool Anonymous
        {
            get { return _anonymous; }
            set { _anonymous = value; }
        }

        public bool LFG
        {
            get { return _lfg; }
            set { _lfg = value; }
        }

        public byte Race
        {
            get { return _race; }
            set { _race = value; }
        }
        public byte Class
        {
            get { return _class; }
            set { _class = value; }
        }

        public byte Level
        {
            get { return _level; }
            set { _level = value; }
        }
    }
}
