using System;
using System.Collections.Generic;
using System.Data.Linq;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using EQEmulator.Servers.Internals.Data;
using EQEmulator.Servers.Internals.Entities;
using EQEmulator.Servers.Internals.Packets;

namespace EQEmulator.Servers.Internals
{
    public enum ZoneConnectionState
    {
        Connecting,
        Connected,
        LinkDead,
        Kicked,
        Disconnected,
        ClientError,
        All
    }

    public enum ZoneMode
    {
        ZoneToSafeCoords,   // Succor & Evac - always send ZonePlayerToBind structure to client
        GMSummon,           // Only a GM Summon - always send ZonePlayerToBind structure to client
        ZoneToBindPoint,    // Death only - always send ZonePlayerToBind structure to client
        Solicited,          // Portal, translocate, evacs that have specific coordinates in the spell data - always send ZonePlayerToBind structure to client
        Unsolicited,
        GateToBindPoint,    // Gate spell or translocate-to-bind-point spell - always send ZonePlayerToBind structure to client
        SummonPC,           // COH spell or some other type of in-zone only summons
        Rewind,
        EvacToSafeCoords
    }

    internal enum ServerCommand
    {
        Zone,
        GoTo
    }

    internal class DeferredPacket
    {
        private EQRawApplicationPacket _appPacket;
        private bool _ackReq;

        public DeferredPacket(EQRawApplicationPacket appPacket, bool ackReq)
        {
            _appPacket = appPacket;
            _ackReq = ackReq;
        }

        public EQRawApplicationPacket AppPacket
        {
            get { return _appPacket; }
        }

        public bool AckReq
        {
            get { return _ackReq; }
        }
    }

    #region Event Data Structures
    internal class ServerCommandEventArgs : EventArgs
    {
        internal ServerCommand Command { get; set; }
        internal Dictionary<string, string> Arguments { get; set; }

        internal ServerCommandEventArgs(ServerCommand cmd, Dictionary<string, string> args)
        {
            Command = cmd;
            Arguments = args;
        }
    }
    #endregion

    internal partial class ZonePlayer : Entities.Mob, IDisposable
    {
        // Timer interval values (in milliseconds)
        public const int CLIENT_PROXIMITY_INTERVAL = 1000;
        public const int LINKDEAD_TIMER = 10000;
        public const int TARGETING_RANGE = 200;     // Range for /assist and /target
        public const int DEAD_TIMER = 5000;
        public const int CAMP_TIMER = 29000;
        internal const byte MIN_STATUS_TO_BE_GM = 40;
        internal const byte MIN_STATUS_TO_USE_GM_CMDS = 80;
        internal const byte MIN_STATUS_TO_KICK = 150;
        internal const byte MIN_STATUS_TO_AVOID_FALLING = 100;
        internal const byte MIN_STATUS_TO_HAVE_INVALID_SPELLS = 80;
        internal const byte MIN_STATUS_TO_HAVE_INVALID_SKILLS = 80;
        internal const byte MIN_STATUS_TO_IGNORE_ZONE_FLAGS = 100;
        internal const byte MIN_STATUS_TO_EDIT_OTHER_GUILDS = 100;

        // Events raised by various actions of the player. Subscribed to by the Mob Manager
        internal event EventHandler<EventArgs> LevelGained;
        internal event EventHandler<ServerCommandEventArgs> ServerCommand;
        
        private Queue<DeferredPacket> _deferredPackets = new Queue<DeferredPacket>(20);
        private object _defPacksLock = new object(), _saveLock = new object();
        private int _acctId, _charId, _aggroCount = 0;
        private short _acctStatus;
        private ZoneConnectionState _connState;
        private uint _guildId = Guild.GUILD_NONE, _tributeMasterId = 0xFFFFFFFF;
        private byte _guildRank, _gmStatus = 0;
        private bool _lfg, _afk, _autoAttack = false, _autoFire = false, _meditating, _onBoat, _tracking, _gmHidden = false;
        private bool _grouped = false, _raidGrouped = false;
        private uint _maxEndurance, _lastReportedEnd, _lastReportedMana;
        private PlayerProfile _pp = new PlayerProfile();
        private ZoneMode _zoneMode = ZoneMode.Unsolicited;
        private bool _zoning = false, _feigning = false;
        private ushort _zoneSummonId;
        private float _zoneSummonX, _zoneSummonY, _zoneSummonZ;
        private byte _ignoreZoneRestrictionsReason = 0;     // TODO: document WTF these can be (1 appears to be "because a GM did it")
        private Timer _autoSaveTimer;
        private InventoryManager _invMgr = null;
        private SimpleTimer _ldTimer, _hpUpdateTimer, _deadTimer, _campTimer, _restTimer;
        private MessagingManager _msgMgr = null;
        private int _haste = 0;

        public ZonePlayer(short entityId, Data.Character toon, ushort zoneId, Client client)
            : base(entityId, toon.Name, toon.Surname, toon.X, toon.Y, toon.Z, toon.Heading.Value)
        {
            this.Client = client;
            _acctId = toon.AccountID;
            _charId = toon.CharacterID;
            _acctStatus = toon.Account.Status;
            _connState = ZoneConnectionState.Connecting;

            // TODO: change storage to the cached profile

            _pp.ZoneId = (ushort)zoneId;
            this.Class = toon.Class.Value;
            this.Level = toon.CharLevel;
            _pp.XP = toon.XP;   // Bypass property setter to avoid packet sending
            _pp.Points = toon.PracticePoints.Value;

            _pp.STR = (uint)toon.STR.Value;
            _pp.STA = (uint)toon.STA.Value;
            _pp.DEX = (uint)toon.DEX.Value;
            _pp.AGI = (uint)toon.AGI.Value;
            _pp.INT = (uint)toon.INT.Value;
            _pp.WIS = (uint)toon.WIS.Value;
            _pp.CHA = (uint)toon.CHA.Value;

            this.Race = toon.Race.Value;
            _baseRace = toon.Race.Value;
            this.Gender = toon.Gender.Value;
            _baseGender = toon.Gender.Value;
            this.Deity = (uint)toon.Deity.Value;
            _hairColor = toon.HairColor.Value;
            _hairStyle = toon.HairStyle.Value;
            _beardColor = toon.BeardColor.Value;
            _beard = toon.Beard.Value;
            _eyeColor1 = toon.EyeColor1.Value;
            _eyeColor2 = toon.EyeColor2.Value;
            _luclinFace = toon.Face.Value;
            this.GMStatus = toon.GMStatus;
            this.Platinum = toon.Platinum;
            this.Gold = toon.Gold;
            this.Silver = toon.Silver;
            this.Copper = toon.Copper;

            _pp.Birthday = Utility.TimeTFromDateTime(toon.CreatedDate);
            _pp.LastLogin = _pp.Birthday;
            //_pp.TimePlayedMin = 0;
            _pp.HungerLevel = (uint)toon.HungerLevel.Value;
            _pp.ThirstLevel = (uint)toon.ThirstLevel.Value;
            _pp.Languages = Encoding.ASCII.GetBytes(toon.Languages);

            if (this.X == -1 && this.Y == -1 && this.Z == -1)  // -1 is what these were set to on char creation
            {
                // set to zone safe points
                this.X = toon.Zone.SafeX;
                this.Y = toon.Zone.SafeY;
                this.Z = toon.Zone.SafeZ;

                _log.Debug("Char coords set to safe points");
            }

            // TODO: factions

            // TODO: old emu has code for invalid Z position fix

            _invMgr = new InventoryManager(toon.InventoryItems, this);
            _invMgr.ItemChargeUsed += new EventHandler<ItemChargeUseEventArgs>(InvMgr_ItemChargeUsed);
            _invMgr.ItemMoved += new EventHandler<ItemMoveEventArgs>(InvMgr_ItemMoved);

            // TODO: guild stuff
            _pp.GuildRank = (byte)Guild.GuildRank.None;   // TODO: implement guild tables and fetch this live

            _size = Mob.GetSize((CharRaces)_race);

            // TODO: AAs

            // Spells
            foreach (ScribedSpell ss in toon.ScribedSpells)
                _pp.SpellBook[ss.SlotID] = ss.SpellID;

            foreach (MemorizedSpell ms in toon.MemorizedSpells) {
                _pp.MemSpells[ms.SlotID] = ms.SpellID;
                _memdSpells[ms.SlotID] = ms.Spell;
            }

            // TODO: buffs?
            for (int i = 0; i < Character.MAX_BUFF; i++)    // blank out the buffs spell id
                Buffer.BlockCopy(BitConverter.GetBytes((uint)Spell.BLANK_SPELL), 0, _pp.BuffsBlob, (20 * i) + 4, 4);
                //_pp.BuffsBlob[i] = 0xFF;

            // TODO: binds
            _pp.Binds[0].ZoneId = (uint)zoneId;
            _pp.Binds[0].X = this.X;
            _pp.Binds[0].Y = this.Y;
            _pp.Binds[0].Z = this.Z;
            _pp.Binds[0].Heading = this.Heading;

            _pp.Mana = 0u;  // TODO: Mana
            _pp.HP = (uint)toon.HP.Value;
            _curHP = toon.HP.Value; // Bypass property settors to avoid packet sending

            CalcStatModifiers();    // Calculate stats (ac, attack, hp, bonuses, etc.)
            
            if (_pp.HP <= 0) {
                _pp.HP = (uint)this.MaxHP;  // If they were dead, let's set things back to full
                _curHP = this.MaxHP;
            }

            _pp.Endurance = _maxEndurance;

            // TODO: group shit

            // TODO: once zone init is coded, do underworld checks

            // Skills
            for (int i = 0; i < toon.Skills.Length; i++)
                _pp.Skills[i] = (uint)toon.Skills[i];   // may want to turn the db representation into an nchar if skill levels get > 255

            // TODO: tribute
            _pp.TributeTimeRemaining = uint.MaxValue;

            InitPlayerProfile();

            // Init timers
            int autoSaveInterval = WorldServer.ServerConfig.AutosaveIntervalSec * 1000;
            _autoSaveTimer = new Timer(new TimerCallback(AutosaveTimerCallback), null, autoSaveInterval, autoSaveInterval);
            _ldTimer = new SimpleTimer(0);
            _deadTimer = new SimpleTimer(0);
            _hpUpdateTimer = new SimpleTimer(0);
            _campTimer = new SimpleTimer(0);

            _msgMgr = new MessagingManager(this);
        }

        #region Properties
        public Client Client { get; set; }

        internal int CharId
        {
            get { return _charId; }
            set { _charId = value; }
        }

        internal int AccountId
        {
            get { return _acctId; }
            set { _acctId = value; }
        }

        internal short AccountStatus
        {
            get { return _acctStatus; }
            set { _acctStatus = value; }
        }

        internal ZoneConnectionState ConnectionState
        {
            get { return _connState; }
            set { _connState = value; }
        }

        /// <summary>Gets this instance of the player in a PlayerProfile struct.</summary>
        internal PlayerProfile PlayerProfile
        {
            get { return _pp; }
        }

        internal override string Name
        {
            get { return Encoding.ASCII.GetString(_pp.Name).TrimEnd('\0'); }
            set
            {
                if (!string.IsNullOrEmpty(value))
                    Buffer.BlockCopy(Encoding.ASCII.GetBytes(value), 0, _pp.Name, 0, value.Length);
                else
                    Array.Clear(_pp.Name, 0, _pp.Name.Length);
            }
        }

        internal override string Surname
        {
            get { return Encoding.ASCII.GetString(_pp.Surname).TrimEnd('\0'); }
            set
            {
                if (!string.IsNullOrEmpty(value))
                    Buffer.BlockCopy(Encoding.ASCII.GetBytes(value), 0, _pp.Surname, 0, value.Length);
                else
                    Array.Clear(_pp.Surname, 0, _pp.Surname.Length);
            }
        }

        internal byte[] SurnameBytes
        {
            get { return _pp.Surname; }
            set { _pp.Surname = value; }
        }

        internal override short STR
        {
            get { return (short)_pp.STR; }
            set { _pp.STR = (uint)value; }
        }

        internal override short STA
        {
            get { return (short)_pp.STA; }
            set { _pp.STA = (uint)value; }
        }

        internal override short DEX
        {
            get { return (short)_pp.DEX; }
            set { _pp.DEX = (uint)value; }
        }

        internal override short AGI
        {
            get { return (short)_pp.AGI; }
            set { _pp.AGI = (uint)value; }
        }

        internal override short INT
        {
            get { return (short)_pp.INT; }
            set { _pp.INT = (uint)value; }
        }

        internal override short WIS
        {
            get { return (short)_pp.WIS; }
            set { _pp.WIS = (uint)value; }
        }

        internal override short CHA
        {
            get { return (short)_pp.CHA; }
            set { _pp.CHA = (uint)value; }
        }

        internal override int HP
        {
            set
            {
                base.HP = value;    // keeps the pp's value sync'd to the class value
                _pp.HP = (uint)base.HP;
            }
        }

        internal override float Heading
        {
            get { return _pp.Heading; }
            set { _pp.Heading = value; }
        }

        internal override float X
        {
            get { return _pp.X; }
            set { _pp.X = value; }
        }

        internal override float Y
        {
            get { return _pp.Y; }
            set { _pp.Y = value; }
        }

        internal override float Z
        {
            get { return _pp.Z; }
            set { _pp.Z = value; }
        }

        internal override short Race
        {
            get { return (short)_pp.Race; }
            set { _pp.Race = (uint)value; }
        }

        internal override byte Class
        {
            get { return (byte)_pp.Class; }
            set { _pp.Class = value; }
        }

        internal override byte Gender
        {
            get { return (byte)_pp.Gender; }
            set { _pp.Gender = value; }
        }

        internal override byte Level
        {
            get { return _pp.Level; }
            set
            {
                if (_pp.Level == 0) {   // For when first set
                    _pp.Level = value;
                    _pp.Level1 = value;
                }
                else if (value > _pp.Level) {
                    byte oldLevel = _pp.Level;
                    _pp.Level = value;
                    _pp.Level1 = value;

                    if (this.ConnectionState == ZoneConnectionState.Connected) {
                        _msgMgr.SendMessageID(15, MessageStrings.GAIN_LEVEL, (value).ToString());
                        OnLevelGained(new EventArgs());
                    }

                    _pp.Points += 5;

                    LevelUpdate lu = new LevelUpdate()
                    {
                        NewLevel = value,
                        OldLevel = oldLevel,
                        XP = (uint)(((float)(_pp.XP - Character.GetXpForLevel(value)) / (float)(Character.GetXpForLevel(value + 1) - Character.GetXpForLevel(value))) * 330)
                    };
                    EQApplicationPacket<LevelUpdate> luPack = new EQApplicationPacket<LevelUpdate>(AppOpCode.LevelUpdate, lu);
                    this.Client.SendApplicationPacket(luPack);
                    OnStanceChanged(new StanceChangedEventArgs(SpawnAppearanceType.WhoLevel, value));

                    // TODO: update raid

                    CalcStatModifiers();
                    this.HP = _maxHP;   // TODO: Set the new max hp somewhere
                    this.Mana = _maxMana;
                    Save();
                }
                else if (value < _pp.Level) {
                    _pp.Level = value;
                    _pp.Level1 = value;
                    _msgMgr.SendMessageID(15, MessageStrings.LOSE_LEVEL, value.ToString());
                }

                //_log.DebugFormat("Setting level for {0} to {1}", this.Name, value);
            }
        }

        internal override uint Deity
        {
            get { return _pp.Deity; }
            set { _pp.Deity = value; }
        }

        internal bool IsLFG
        {
            get { return _lfg; }
        }

        internal bool IsAFK
        {
            get { return _afk; }
            set { _afk = value; }
        }

        internal bool IsAutoAttacking
        {
            get { return _autoAttack; }
            set
            {
                _autoAttack = value;

                // TODO: should there be a check here for AI controlled?
                if (_autoAttack)
                    SetAttackTimer();
                else {      // disable attack timers if we aren't auto-attacking anymore
                    _attackTimer.Enabled = false;
                    _rangedAttackTimer.Enabled = false;
                    _dwAttackTimer.Enabled = false;
                }
            }
        }

        internal bool IsAutoFiring
        {
            get { return _autoFire; }
        }

        internal bool IsMeditating
        {
            get { return _meditating; }
        }

        internal bool IsOnBoat
        {
            get { return _onBoat; }
        }

        internal bool IsTracking
        {
            get { return _tracking; }
        }

        internal bool IsGrouped
        {
            get { return _grouped; }
        }

        internal bool IsRaidGrouped
        {
            get { return _raidGrouped; }
        }

        internal bool IsGM
        {
            get { return _gmStatus >= MIN_STATUS_TO_BE_GM; }
        }

        internal byte GMStatus
        {
            get { return _gmStatus; }
            set
            {
                _gmStatus = value;
                _pp.GM = this.IsGM ? (byte)1 : (byte)0;
            }
        }

        internal bool IsGMHidden
        {
            get { return _gmHidden; }
        }

        internal byte Anonymous
        {
            get { return _pp.Anon; }
            set { _pp.Anon = value; }
        }

        internal uint GuildId
        {
            get { return _guildId; }
        }

        internal uint TributeMasterId
        {
            get { return _tributeMasterId; }
        }

        internal ZoneMode ZoneMode
        {
            get { return _zoneMode; }
            set { _zoneMode = value; }
        }

        internal ushort ZoneId
        {
            get { return _pp.ZoneId; }
            set { _pp.ZoneId = value; }
        }

        internal bool IsZoning
        {
            get { return _zoning; }
            set { _zoning = value; }
        }

        internal ushort ZoneSummonID
        {
            get { return _zoneSummonId; }
            set { _zoneSummonId = value; }
        }

        public float ZoneSummonZ
        {
            get { return _zoneSummonZ; }
            set { _zoneSummonZ = value; }
        }

        public float ZoneSummonY
        {
            get { return _zoneSummonY; }
            set { _zoneSummonY = value; }
        }

        public float ZoneSummonX
        {
            get { return _zoneSummonX; }
            set { _zoneSummonX = value; }
        }

        public byte IgnoreZoneRestrictionsReason
        {
            get { return _ignoreZoneRestrictionsReason; }
            set { _ignoreZoneRestrictionsReason = value; }
        }

        internal InventoryManager InvMgr
        {
            get { return _invMgr; }
        }

        internal override uint Platinum
        {
            get { return _pp.Platinum; }    // TODO: recalc weight within getter and setter
            set { _pp.Platinum = value; }
        }

        internal override uint Gold
        {
            get { return _pp.Gold; }    // TODO: recalc weight within getter and setter
            set { _pp.Gold = value; }
        }

        internal override uint Silver
        {
            get { return _pp.Silver; }  // TODO: recalc weight within getter and setter
            set { _pp.Silver = value; }
        }

        internal override uint Copper
        {
            get { return _pp.Copper; }  // TODO: recalc weight within getter and setter
            set { _pp.Copper = value; }
        }

        internal bool IsShowingHelm
        {
            get { return _pp.ShowHelm == 1; }
            set { _pp.ShowHelm = value ? 1u : 0; }
        }

        internal bool IsAutoSplitting
        {
            get { return _pp.AutoSplit == 1; }
            set { _pp.AutoSplit = value ? 1u : 0; }
        }

        internal bool IsFeigning
        {
            get { return _feigning; }
            set
            {
                //if (value) {
                    // TODO: depop horse
                    // TODO: clear feign aggro
                    // TODO: start a forget timer
                //}
                //else
                    // TODO: disable forget timer

                _feigning = value;
            }
        }

        internal uint Endurance
        {
            get { return _pp.Endurance; }
            set
            {
                value = Math.Max(value, 0); // zero is the min for endurance
                value = Math.Min(value, _maxEndurance); // cap

                if (value != _pp.Endurance) {
                    _pp.Endurance = value;
                    OnManaStaChanged(new EventArgs());
                }
            }
        }

        internal override int Mana
        {
            get { return (int)_pp.Mana; }
            set 
            {
                value = Math.Max(value, 0); // zero is the min for mana
                value = Math.Min(value, _maxMana);  // cap

                if (value != _pp.Mana) {
                    _pp.Mana = (uint)value;
                    OnManaStaChanged(new EventArgs());
                }
            }
        }

        internal MessagingManager MsgMgr
        {
            get { return _msgMgr; }
        }

        internal override bool Invulnerable
        {
            get
            {
                if (this.ConnectionState != ZoneConnectionState.Connected)
                    return true;
                else
                    return base.Invulnerable;
            }
            set { base.Invulnerable = value; }
        }

        internal int XP
        {
            get { return (int)_pp.XP; }
            set
            {
                if (value == this.XP)
                    return;

                uint xpHigh = Character.GetXpForLevel(this.Level + 1);
                uint xpLow = Character.GetXpForLevel(this.Level);
                int actualXpDiff = 0;

                if (value < _pp.XP) {
                    uint xpToLose = (uint)Math.Min(Math.Abs(value), _pp.XP);  // Can't lose more xp than we have
                    _pp.XP -= xpToLose;
                    actualXpDiff = -(int)xpToLose;

                    this.MsgMgr.SendSpecialMessage(MessageType.Default, "You have lost experience.");
                }
                else if (value > _pp.XP) {
                    if (this.Level == WorldServer.ServerConfig.LevelCap) {  // Cap the xp gain
                        uint maxXp = Character.GetXpForLevel(this.Level + 1) - 1000;    // Just shy of the next, unattainable, level
                        actualXpDiff = (int)Math.Min(maxXp - _pp.XP, value);
                    }
                    else
                        actualXpDiff = value;

                    _pp.XP += (uint)actualXpDiff;

                    // Send various messages
                    if (this.IsGrouped)
                        this.MsgMgr.SendMessageID(15, MessageStrings.GAIN_GROUPXP);
                    else if (this.IsRaidGrouped)
                        this.MsgMgr.SendMessageID(15, MessageStrings.GAIN_RAIDEXP);
                    else
                        this.MsgMgr.SendMessageID(15, MessageStrings.GAIN_XP);
                }

                // TODO: if rez regain xp, a flag should be set somewhere or it should be set somewhere else entirely

                // See if we've gained or lost a level (max of one level at a time)
                if (_pp.XP < xpLow && this.Level > 2) {
                    this.Level--;
                    xpHigh = Character.GetXpForLevel(this.Level + 1);   // Reset the high and low boundaries so the XP packet looks right
                    xpLow = Character.GetXpForLevel(this.Level);
                }
                else if (_pp.XP > xpHigh) {
                    this.Level++;
                    xpHigh = Character.GetXpForLevel(this.Level + 1);   // Reset the high and low boundaries so the XP packet looks right
                    xpLow = Character.GetXpForLevel(this.Level);
                }

                // TODO: determine if we've gained/lost any AA points (shouldn't this be elsewhere?)

                float xpRatio = (float)(_pp.XP - xpLow) / (float)(xpHigh - xpLow);    // IMPORTANT: use the xp as a whole, not the amount we've just gained
                XpUpdate xu = new XpUpdate() { XP = (uint)(xpRatio * 330.0f) };
                EQApplicationPacket<XpUpdate> xuPack = new EQApplicationPacket<XpUpdate>(AppOpCode.ExpUpdate, xu);
                Client.SendApplicationPacket(xuPack);

                //_log.DebugFormat("XP gained: {0}. Current XP now: {1}. XP packet sent with Low: {2} High: {3} funky xp value of {4} ({5})",
                //    actualXpDiff, _pp.XP, xpLow, xpHigh, xu.XP, xpRatio * 330.0f);
            }
        }
        #endregion

        private void InitPlayerProfile()
        {
            _pp.HairColor = _hairColor;
            _pp.BeardColor = _beardColor;
            _pp.EyeColor1 = _eyeColor1;
            _pp.EyeColor2 = _eyeColor2;
            _pp.HairStyle = _hairStyle;
            _pp.Beard = _beard;
            
            _pp.Face = _luclinFace;
            _pp.GuildId = this.GuildId;
            // TODO: money in bank, shared, & on cursor
            
            // TODO: buffs
            // TODO: group shit

            // Equipable item materials
            _pp.ItemMaterial[(int)EquipableType.Head] = (uint)GetEquipmentMaterial(EquipableType.Head);
            _pp.ItemMaterial[(int)EquipableType.Arms] = (uint)GetEquipmentMaterial(EquipableType.Arms);
            _pp.ItemMaterial[(int)EquipableType.Bracer] = (uint)GetEquipmentMaterial(EquipableType.Bracer);
            _pp.ItemMaterial[(int)EquipableType.Hands] = (uint)GetEquipmentMaterial(EquipableType.Hands);
            _pp.ItemMaterial[(int)EquipableType.Primary] = (uint)GetEquipmentMaterial(EquipableType.Primary);
            _pp.ItemMaterial[(int)EquipableType.Secondary] = (uint)GetEquipmentMaterial(EquipableType.Secondary);
            _pp.ItemMaterial[(int)EquipableType.Chest] = (uint)GetEquipmentMaterial(EquipableType.Chest);
            _pp.ItemMaterial[(int)EquipableType.Legs] = (uint)GetEquipmentMaterial(EquipableType.Legs);
            _pp.ItemMaterial[(int)EquipableType.Feet] = (uint)GetEquipmentMaterial(EquipableType.Feet);

            _pp.AirRemaining = 60;   // max so player doesn't drown on zone in if underwater
            _pp.PVP = WorldServer.ServerConfig.PVP ? (byte)1 : (byte)0;
            _pp.Expansions = (uint)WorldServer.ServerConfig.Expansions;
            
            // last step... crc
            byte[] structBuffer = new byte[Marshal.SizeOf(_pp)];
            GCHandle handle = GCHandle.Alloc(structBuffer, GCHandleType.Pinned);
            IntPtr buffer = handle.AddrOfPinnedObject();
            Marshal.StructureToPtr(_pp, buffer, false);
            handle.Free();
            _pp.Checksum = CRC.ComputeChecksum(structBuffer);
        }

        /// <summary>Called by the server to run final initialization on the player object.  Called when client is logged in a ready for play.</summary>
        protected internal void SetLockedAndLoaded()
        {
            this.ConnectionState = ZoneConnectionState.Connected;   // Make sure this is the last step

            // Init timers
            _hpUpdateTimer.Start(1800);
        }

        internal override void StartAI()
        {
            base.StartAI();

            // TODO: start up player AI stuff
        }

        internal override Internals.Packets.Spawn GetSpawn()
        {
            Internals.Packets.Spawn s = base.GetSpawn();

            s.Surname = SurnameBytes;
            s.Afk = this.IsAFK ? (byte)1 : (byte)0;
            s.Anon = this.Anonymous;
            s.GM = this.IsGM ? (byte)1 : (byte)0;
            s.GuildId = this.GuildId;
            s.NPC = 0;  // TODO: handle "becoming" an NPC
            s.IsNpc = 0;
            s.IsPet = 0;
            s.GuildRank = 0xFF; // TODO: Guild rank shit
            s.Size = 0;
            s.RunSpeed = this.IsGM ? 3.125F : this.BaseRunSpeed;  // TODO: could also check that the account is flagged for GM speed if we want

            // Equipment    TODO: equip & colors might make sense to refactor into mob
            s.EquipHelmet = (uint)GetEquipmentMaterial(EquipableType.Head);
            s.EquipChest = (uint)GetEquipmentMaterial(EquipableType.Chest);
            s.EquipArms = (uint)GetEquipmentMaterial(EquipableType.Arms);
            s.EquipBracers = (uint)GetEquipmentMaterial(EquipableType.Bracer);
            s.EquipHands = (uint)GetEquipmentMaterial(EquipableType.Hands);
            s.EquipLegs = (uint)GetEquipmentMaterial(EquipableType.Legs);
            s.EquipFeet = (uint)GetEquipmentMaterial(EquipableType.Feet);
            s.EquipPrimary = (uint)GetEquipmentMaterial(EquipableType.Primary);
            s.EquipSecondary = (uint)GetEquipmentMaterial(EquipableType.Secondary);

            // Colors
            s.HelmetColor = GetEquipmentColor(EquipableType.Head);
            s.ChestColor = GetEquipmentColor(EquipableType.Chest);
            s.ArmsColor = GetEquipmentColor(EquipableType.Arms);
            s.BracersColor = GetEquipmentColor(EquipableType.Bracer);
            s.HandsColor = GetEquipmentColor(EquipableType.Hands);
            s.LegsColor = GetEquipmentColor(EquipableType.Legs);
            s.FeetColor = GetEquipmentColor(EquipableType.Feet);
            s.PrimaryColor = GetEquipmentColor(EquipableType.Primary);
            s.SecondaryColor = GetEquipmentColor(EquipableType.Secondary);

            return s;
        }

        protected override int GetEquipmentMaterial(EquipableType et)
        {
            InventoryItem invItem = _invMgr[(int)InventoryManager.GetEquipableSlot(et)];
            if (invItem == null)
                return 0;

            if (et == EquipableType.Primary || et == EquipableType.Secondary) {   // Held items need the idfile
                if (invItem.Item.IDFile.Length > 2)
                    return int.Parse(invItem.Item.IDFile.Substring(2));
                else
                    return invItem.Item.Material;
            }
            else
                return invItem.Item.Material;
        }

        protected override uint GetEquipmentColor(EquipableType et)
        {
            InventoryItem invItem = _invMgr[(int)InventoryManager.GetEquipableSlot(et)];
            if (invItem == null)
                return 0;
            else
                return invItem.Item.Color ?? 0;     // TODO: tints (LoY era).  Key off of the profile's useTint member?
        }

        protected override Item GetEquipment(EquipableType equipSlot)
        {
            InventoryItem invItem = _invMgr[(int)InventoryManager.GetEquipableSlot(equipSlot)];

            return invItem != null ? invItem.Item : null;
        }

        protected override Item GetEquipment(InventorySlot invSlot)
        {
            InventoryItem invItem = _invMgr[(int)invSlot];
            if (invItem == null)
                return null;

            return invItem.Item;
        }

        internal void GiveMoney(uint plat, uint gold, uint silver, uint copper)
        {
            // Bypass property setters to more efficiently set it all in one go
            _pp.Platinum += plat;
            _pp.Gold += gold;
            _pp.Silver += silver;
            _pp.Copper += copper;

            RecalculateWeight();
        }

        /// <summary>Automatically locates and places the specified item into an appropriate slot.</summary>
        /// <returns>True if item was successfully placed.</returns>
        internal bool AutoGiveItem(ref InventoryItem invItem)
        {
            Item item = invItem.Item;

            // First, try to equip
            if (item.ValidateEquipable(this.Race, this.Class, this.Level)) {    // Useable in a worn slot? TODO: check for attuneable items
                for (int i = 0; i <= (int)InventorySlot.EquipSlotsEnd; i++) {
                    if (this.InvMgr[i] == null) {   // Empty slot?
                        if (i == (int)InventorySlot.Primary && item.IsTwoHandedWeapon && this.InvMgr[(int)InventorySlot.Secondary] != null)
                            continue;   // can't equip a two hander with something already in the secondary slot
                        else if (i == (int)InventorySlot.Secondary) {
                            if (this.InvMgr[(int)InventorySlot.Primary] != null && this.InvMgr[(int)InventorySlot.Primary].Item.IsTwoHandedWeapon)
                                continue;   // can't equip in secondary if primary is a two hander
                            if (item.IsWeapon && !this.CanDualWield())
                                continue;   // can't dual wield, so no equiping for you
                        }

                        if (item.ValidateEquipable(i)) {    // Good so far, but can we use it in this slot?
                            GiveItem(invItem, i);
                            return true;
                        }
                    }
                }
            }

            // Second, try to stack it
            if (item.stackable) {
                _log.DebugFormat("Auto-stacking {1} item(s) of {0}", item.Name, invItem.Charges);
                if (GiveStack(ref invItem)) {   // Was ALL of the stack stacked somewhere?
                    CalcStatModifiers();
                    return true;
                }
            }

            // Last, put into a free spot in inventory
            int intoSlotId = this.InvMgr.GetFreeSlot(item.Size, item.IsContainer);

            if (intoSlotId != (int)InventorySlot.Invalid) {
                _log.DebugFormat("Found free slot for {0} at {1}", item.Name, intoSlotId);
                GiveItem(invItem, intoSlotId);
                return true;
            }

            _log.Debug("Can't find a free spot in inventory, all full apparently.");
            return false;
        }

        /// <summary>Gives an item to the player.  Ensure slot is valid by calling various validators before calling this function.</summary>
        internal InventoryItem GiveItem(InventoryItem invItem, int slotId)
        {
            //_log.DebugFormat("Placing item {0}({1}) into slot {2}", invItem.Item.Name, invItem.ItemID, slotId);
            InventoryItem newItem = invItem.ToNewInventoryItem();
            this.InvMgr[slotId] = newItem;

            ZoneServer.SendItemPacket(this.Client, newItem, ItemPacketType.Trade);

            // TODO: container contents - this may be tricky as each item (if any) in a container are sent separate from the container (above).
            // This means the above item probably can't have the subItems already calculated?  Also probably have to recurse this routine due to
            // arbitrary levels of nested containers

            // TODO: Does loot work differently than say trading items?  Orig emu had a separate giveLoot routine.  Maybe clone invItem to an
            // invItem that doesn't have subItems calculated

            this.InvMgr.CalculateSubItems(InventoryLocations.Personal);
            CalcStatModifiers();
            return this.InvMgr[slotId];
        }

        /// <summary>Gives a stack of items to the player.</summary>
        /// <returns>True if the ENTIRE stack is successfully given.</returns>
        internal bool GiveStack(ref InventoryItem invItem)
        {
            if (!invItem.Item.stackable || invItem.Charges >= invItem.Item.stacksize) {
                _log.ErrorFormat("Tried to stack item {0} with {1} charges.  Item {2} stackable with a stack size of {3}",
                    invItem.Item.Name, invItem.Charges, invItem.Item.stackable ? "is" : "isn't", invItem.Item.stacksize);
                return false;
            }

            InventoryItem ii = null;
            for (int i = (int)InventorySlot.PersonalSlotsBegin; i <= (int)InventorySlot.PersonalSlotsEnd; i++) {
                ii = this.InvMgr[i];
                if (ii != null) {
                    if (ii.ItemID == invItem.ItemID && ii.Charges < ii.Item.stacksize) {
                        // we have a matching item with available space
                        _log.DebugFormat("Trying to give {0} charges from item {1} to item {2} in slot {3} that has {4} charges",
                            invItem.Charges, invItem.Item.Name, ii.Item.Name, i, ii.Charges);
                        this.InvMgr.MoveCharges(ref invItem, ii);

                        if (invItem.Charges > 0)
                            return GiveStack(ref invItem);

                        return true;
                    }
                    else if (ii.Item.IsContainer) {
                        foreach (InventoryItem subItem in ii.SubItems()) {
                            if (subItem.ItemID == invItem.ItemID && subItem.Charges < subItem.Item.stacksize) {
                                // we have a matching sub-item with available space
                                this.InvMgr.MoveCharges(ref invItem, subItem);

                                if (invItem.Charges > 0)
                                    return GiveStack(ref invItem);

                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        internal override PlayerPositionUpdateServer GetSpawnUpdate()
        {
            PlayerPositionUpdateServer ppus = base.GetSpawnUpdate();
            ppus.Animation = this.Animation;
            return ppus;
        }

        internal override bool IsInZone()
        {
            return (_connState == ZoneConnectionState.Connected || _connState == ZoneConnectionState.LinkDead);
        }

        /// <summary>Processes each client update packet sent from the client, updating internal state.</summary>
        internal void ProcessClientUpdate(PlayerPositionUpdateClient ppuc, out SpawnAppearance sa, out PlayerPositionUpdateServer ppus)
        {
            float dist = 0, tmp;
            tmp = this.X - ppuc.XPos;
            dist += tmp * tmp;
            tmp = this.Y - ppuc.YPos;
            dist += tmp * tmp;
            dist = (float)Math.Sqrt(dist);

            // TODO: Perhaps a warp check in the future, for now just log stuff
            if (dist > 50.0f * 50.0f)
            {
                _log.WarnFormat("{0}: Large position change: {1} units", this.Name, Math.Sqrt(dist));
                _log.WarnFormat("Coords: ({0:F4}, {1:F4}, {2:F4}) -> ({3:F4}, {4:F4}, {5:F4})", this.X, this.Y, this.Z, ppuc.XPos, ppuc.YPos, ppuc.ZPos);
                _log.WarnFormat("Deltas: ({0:F2}, {1:F2}, {2:F2}) -> ({3:F2}, {4:F2}, {5:F2})", _deltaX, _deltaY, _deltaZ, ppuc.DeltaX, ppuc.DeltaY, ppuc.DeltaZ);
            }

            // TODO: proximity timer?

            _deltaX = ppuc.DeltaX;
            _deltaY = ppuc.DeltaY;
            _deltaZ = ppuc.DeltaZ;
            _deltaHeading = ppuc.DeltaHeading;
            this.Heading = ppuc.Heading;

            // TODO: tracking skill increase check

            sa = new SpawnAppearance();
            if (ppuc.YPos != this.Y || ppuc.XPos != this.X)
            {
                if (!_sneaking && !_invis)
                {
                    _hidden = false;
                    _improvedHidden = false;
                    sa = new SpawnAppearance((ushort)this.ID, 0x03, 0);
                }
            }

            ppus = new PlayerPositionUpdateServer();
            if (ppuc.YPos != this.Y || ppuc.XPos != this.X || ppuc.Heading != this.Heading || ppuc.Animation != this.Animation)
                ppus = GetSpawnUpdate();

            this.X = ppuc.XPos;
            this.Y = ppuc.YPos;
            this.Z = ppuc.ZPos;
            this.Animation = ppuc.Animation;
        }

        /// <summary>Persists all player data to the database. Thread safe. Call in a background thread if the save isn't needed immediately.</summary>
        /// <returns>True if successful, else false.</returns>
        internal override void Save()
        {
            lock (_saveLock) {
                using (EmuDataContext dbCtx = new EmuDataContext()) {
                    DataLoadOptions dlo = new DataLoadOptions();
                    dlo.LoadWith<Character>(c => c.InventoryItems);
                    dbCtx.LoadOptions = dlo;
                    //dbCtx.Log = new Log4NetWriter(typeof(ZonePlayer));

                    Character toon = dbCtx.Characters.SingleOrDefault(c => c.Name == this.Name);
                    toon.X = this.X;
                    toon.Y = this.Y;
                    toon.Z = this.Z;
                    toon.Heading = this.Heading;
                    toon.ZoneID = this.ZoneId;
                    toon.LastSeenDate = DateTime.Now;
                    toon.Platinum = this.Platinum;
                    toon.Gold = this.Gold;
                    toon.Silver = this.Silver;
                    toon.Copper = this.Copper;
                    toon.XP = _pp.XP;   // Going direct to the pp eliminates a cast
                    toon.CharLevel = this.Level;
                    toon.HP = this.HP;

                    // Convert skill values from the uint[] to a byte[]
                    byte[] skills = new byte[toon.Skills.Length];
                    for (int i = 0; i < _pp.Skills.Length; i++)
                        skills[i] = (byte)_pp.Skills[i];
                    toon.Skills = Encoding.ASCII.GetString(skills);

                    // Clear and then re-add the inventory, spellbook, mem'd spells (got a better idea?)
                    dbCtx.InventoryItems.DeleteAllOnSubmit(toon.InventoryItems);    // TODO: this gens many DELETE statements... could be improved
                    dbCtx.MemorizedSpells.DeleteAllOnSubmit(toon.MemorizedSpells);
                    dbCtx.ScribedSpells.DeleteAllOnSubmit(toon.ScribedSpells);
                    dbCtx.SubmitChanges();

                    foreach (InventoryItem invItem in this.InvMgr.AllItems()) {
                        //_log.DebugFormat("During save found item {0} in slot {1}", invItem.Item.Name, invItem.SlotID);
                        toon.InventoryItems.Add(invItem.ShallowCopy());
                    }

                    // Save spellbook
                    for (short i = 0; i < this.PlayerProfile.SpellBook.Length; i++) {
                        if (this.PlayerProfile.SpellBook[i] != Spell.BLANK_SPELL)   // Don't save blank spells
                            toon.ScribedSpells.Add(new ScribedSpell() { SlotID = i, SpellID = (ushort)this.PlayerProfile.SpellBook[i] });
                    }

                    // Save memorized spells
                    for (byte i = 0; i < this.PlayerProfile.MemSpells.Length; i++) {
                        if (this.PlayerProfile.MemSpells[i] != Spell.BLANK_SPELL)   // Don't save blank spells
                            toon.MemorizedSpells.Add(new MemorizedSpell() { SlotID = i, SpellID = (ushort)this.PlayerProfile.MemSpells[i] });
                    }

                    dbCtx.SubmitChanges();

                    // TODO: save cursor items
                }
            }

            _log.DebugFormat("{0} Saved.", this.Name);
        }

        /// <summary>Processes various tasks for the player, including when under AI control.</summary>
        /// <returns>False if client needs to be disconnected - due to LD or otherwise.</returns>
        internal override bool Process()
        {
            // Send any deferred packets
            lock (_defPacksLock)
            {
                while (_deferredPackets.Count > 0) {
                    DeferredPacket defPacket = _deferredPackets.Dequeue();
                    this.Client.SendApplicationPacket(defPacket.AppPacket, defPacket.AckReq);
                }
            }

            if (_hpUpdateTimer.Check())
                OnHPUpdated(new EventArgs());

            if (_manaUpdateTimer.Check())
                OnManaStaChanged(new EventArgs());

            if (this.Dead && _deadTimer.Check()) {
                // TODO: do something similar to ZoneClient().  Doesn't look the same... investigate

                // TODO: handle group stuff

                // TODO: handle raid stuff

                return false;
            }

            // TODO: charm crap

            // TODO: Task crap

            // TODO: revisit - it thinks we're LD on every disconnect
            //if (_ldTimer.Check()) {
            //    Save();
            //    Disconnect();
            //    return false;
            //}

            if (_campTimer.Check()) {
                Save();
                Disconnect();
                return false;
            }

            // If stunned, can we shake it off yet?
            if (_stunned && _stunTimer.Check()) {
                _stunned = false;
                _stunTimer.Enabled = false;
            }

            // TODO: Bard melody twisting & song stuff

            if (this.ConnectionState == ZoneConnectionState.LinkDead || IsAIControlled)
                ProcessAI();

            // TODO: Bind wound

            // TODO: Karma

            if (this.IsAutoAttacking || this.IsAutoFiring) {
                
                bool mayAttack = this.IsAbleToAttack();

                //_log.DebugFormat("Should we attack? AutoAttack: {0}, TargetMob: {1}, MayAttack: {2}, ");

                // Handle any potential ranged combat BEFORE melee combat checks
                if (this.IsAutoFiring) {
                    // TODO: ranged combat
                }

                if (this.TargetMob != null && mayAttack && _attackTimer.Check()) {
                    //_log.Debug("Auto-attacking...");
                    if (!IsWithinCombatRangeOf(this.TargetMob))
                        this.MsgMgr.SendMessageID(13, MessageStrings.TARGET_TOO_FAR);
                    else if (this.TargetMob == this)
                        this.MsgMgr.SendMessageID(13, MessageStrings.TRY_ATTACKING_SOMEONE);
                    else if (this.IsAbleToAttack(this.TargetMob, false)) {
                        // TODO: handle AE attacks
                        MeleeAttack(this.TargetMob, true, false);   // Actually attack something

                        // TODO: double, triple and quad attacks

                        // TODO: various melee AA abilities
                    }
                }

                // TODO: dual wield combat
                if (this.TargetMob != null && mayAttack && _dwAttackTimer.Check()) {

                }
            }

            // TODO: warrior and beserker berserk chance

            // TODO: position timer check

            // TODO: shield timer check

            ProcessSpells();    // Spells

            // TODO: endurance upkeep?

            // Stats upkeep, regen, fishing, etc.
            if (_ticTimer.Check() && !Dead)
                ProcessTic();

            // Check if we are disconnected or otherwise leaving
            if (this.ConnectionState == ZoneConnectionState.Disconnected)
                return false;   // signifies we need to be removed from mob mgr

            if (this.ConnectionState == ZoneConnectionState.Kicked) {
                Save();
                Disconnect();
                return false;
            }

            if (this.ConnectionState == ZoneConnectionState.ClientError) {
                Disconnect();
                return false;
            }

            // TODO: fix later... always thinks we're link dead on every disconnect
            //if (this.ConnectionState != ZoneConnectionState.LinkDead && this.Client.ConnectionState != Internals.ConnectionState.Established) {
            //    _log.InfoFormat("Linkdead client detected: {0}", this.Name);
            //    Disconnect();   // TODO: do we want to do this?  How do we resume this somehow?

            //    if (this.GM)
            //        return false;
            //    else if (!_ldTimer.Enabled) {
            //        _ldTimer.Start();
            //        this.ConnectionState = ZoneConnectionState.LinkDead;
            //        // TODO: initiate AI control
            //        OnStanceChanged(new StanceChangedEventArgs(SpawnAppearanceType.Linkdead, 1));
            //    }
            //}

            return true;
        }

        private void ProcessTic()
        {
            CalcMaxHP();
            CalcMaxMana();
            CalcAttack();
            CalcMaxEndurance();
            // TODO: calculate rest state

            // HP regen
            if (this.Wounded) {
                int regenAmt = GetRegenAmount(); // TODO: add regen from items & spells
                this.HP += (int)(regenAmt * WorldServer.ServerConfig.PCRegenMultiplier);    // TODO: may need a HP lock
                //_log.DebugFormat("{0} regenerated {1} HP. Total HP is now {2} ({3:P})", this.Name, regenAmt, this.HP, this.HP / (float)this.MaxHP);
            }

            // TODO: Mana regen

            // TODO: endurance regen

            // TODO: process buffs

            // TODO: process stamina (food, water, etc.)

            // TODO: tribute timer check

            // TODO: fishing
        }

        /// <summary>Make sure to check if able to attack prior to calling as this routine doesn't check if we're able to attack.</summary>
        /// <returns>False if the attack isn't allowed or did not succeed, true if attack successful.</returns>
        internal override bool MeleeAttack(Mob target, bool isPrimaryHand, bool riposte)
        {
            if (!base.MeleeAttack(target, isPrimaryHand, riposte))
                return false;

            // Are we using a weapon?
            Item weapon = isPrimaryHand ? this.GetEquipment(EquipableType.Primary) : this.GetEquipment(EquipableType.Secondary);
            if (weapon != null) {
                if (!weapon.IsWeapon) {
                    _log.DebugFormat("Attack cancelled: {0} is not a weapon.", weapon.Name);
                    return false;
                }
            }

            // Cool, so we're going to attack
            // First calculate the skill and animation to use
            Skill attackSkill;
            AttackAnimation attackAnim;
            GetSkillAndAnimForAttack(weapon, isPrimaryHand, out attackSkill, out attackAnim);
            OnAnimation(new AnimationEventArgs(attackAnim));    // Cue the animation
            //_log.DebugFormat("Attacking {0} with {1} in slot {2} using skill {3}", this.TargetMob.Name, weapon == null ? "Fist" : weapon.Name,
            //    isPrimaryHand ? "Primary" : "Secondary", attackSkill.ToString("f"));

            // Next figure out the potential damage
            int potDamage =  GetWeaponDamage(target, weapon);
            int damage = 0;
            //_log.DebugFormat("Potential damage calculated as {0}", potDamage);

            // If potential damage is more than zero we know it's POSSIBLE to hit the target
            if (potDamage > 0) {
                // TODO: Finishing blow attempt

                CheckForSkillUp(attackSkill, target, -5);
                CheckForSkillUp(Skill.Offense, target, -5);

                // Ok, so we know we CAN hit... let's see if we DO hit
                if (target.TryToHit(this, attackSkill, isPrimaryHand)) {
                    // TODO: Augment potDamage with any Beserker damage bonuses

                    int minHitDmg = 1;
                    int maxHitDmg = this.GetMaxDamage(potDamage, attackSkill);

                    // Level cap the damage
                    if (this.Level < 10)
                        maxHitDmg = Math.Min(maxHitDmg, 20);
                    else if (this.Level < 20)
                        maxHitDmg = Math.Min(maxHitDmg, 40);

                    // TODO: apply damage bonuses (affects min and max dmg as well as hate)

                    // TODO: adjust min damage with any applicable item or buff bonuses

                    maxHitDmg = Math.Max(maxHitDmg, minHitDmg);     // Ensure max is at least min

                    Random rand = new Random();
                    damage = rand.Next(minHitDmg, maxHitDmg + 1);
                    //_log.DebugFormat("Damage calculated to {0} (min {1}, max {2}, str {3}, skill {4}, pot dmg {5}, lvl {6})", damage, minHitDmg,
                    //    maxHitDmg, this.STR, GetSkillLevel(attackSkill), potDamage, this.Level);

                    // With damage now calculated, see if the mob can avoid or mitigate
                    damage = target.TryToAvoidDamage(this, damage);

                    if (damage > 0) {
                        damage = target.TryToMitigateDamage(this, damage, minHitDmg);   // wasn't avoided, try to mitigate
                        // TODO: apply any damage bonuses (is this the right term... bonuses?... wasn't that done by now?
                        // TODO: try a critical hit (why are we trying this after the mitigation?)
                    }

                    //_log.DebugFormat("Damage after avoidance and mitigation: {0}", damage);
                }
                else {
                    // We missed
                    //_log.Debug("Missed.");
                }

                // TODO: riposte

                // TODO: strikethrough
            }
            else
                damage = (int)AttackAvoidanceType.Invulnerable;

            target.Damage(this, damage, 0, attackSkill);    // Send attack damage - even zero and negative damage

            BreakSneakiness();

            // TODO: weapon procs

            if (damage > 0) {
                // TODO: handle lifetap effects?

                // TODO: trigger defensive procs

                return true;
            }
            else
                return false;
        }

        /// <summary></summary>
        /// <param name="attacker"></param>
        /// <param name="dmgAmt"></param>
        /// <param name="spellId">Id of the damaging spell.  Zero for no spell.</param>
        /// <param name="attackSkill"></param>
        internal override void Damage(Mob attacker, int dmgAmt, int spellId, Skill attackSkill)
        {
            if (this.Dead)
                throw new InvalidOperationException("Cannot damage a dead PC.");    // TODO: do we just want to return instead of throw?

            base.Damage(attacker, dmgAmt, spellId, attackSkill);

            if (dmgAmt > 0) {
                if (spellId == 0)
                    CheckForSkillUp(Skill.Defense, attacker, -5);
            }
        }

        protected override void Die(Mob lastAttacker, int damage, int spellId, Skill attackSkill)
        {
            // TODO: interrupt the spell being cast
            // TODO: clear pet
            // TODO: clear mount

            _deadTimer.Start(DEAD_TIMER);

            if (lastAttacker != null) {
                if (lastAttacker is NpcMob) {
                    // TODO: raise slay event for the NPC
                }

                // TODO: handle dueling
            }

            if (this.IsGrouped) {
                // TODO: tell group we zoned (here or in mobMgr?)
            }

            if (this.IsRaidGrouped) {
                // TODO: tell raid we zoned (here or in mobMgr?)
            }

            // TODO: clear proximity?

            base.Die(lastAttacker, damage, spellId, attackSkill);

            // Player's shit is now on their corpse, not on them
            this.Platinum = 0u;
            this.Gold = 0u;
            this.Silver = 0u;
            this.Copper = 0u;
            this.InvMgr.ClearPersonalSlotItems();
            // TODO: clear zone instance ID (when instancing is in)

            Save();
        }

        /// <summary></summary>
        /// <remarks>Normally this will be x2 of the potential damage. High STR & skill levels will nudge up the max dmg amount.</remarks>
        internal int GetMaxDamage(int potDmg, Skill attackSkill)
        {
            int maxVal = 0;

            if (this.Level <= 51) {
                int strOver75 = this.STR > 75 ? this.STR - 75 : 0;
                strOver75 = Math.Min(strOver75, 255);   // Cap STR bonus at 255
                maxVal = (int)(this.GetSkillLevel(attackSkill) + strOver75) / 2;

                maxVal = Math.Max(maxVal, 100);     // Minimum max dmg is 100
            }

            // TODO: handle levels above 51

            return 2 * potDmg * maxVal / 100;
        }

        internal void AddDeferredPacket(DeferredPacket defPack)
        {
            lock (_defPacksLock)
                _deferredPackets.Enqueue(defPack);
        }

        internal void Disconnect()
        {
            // TODO: remove from hate lists, raid lists, group lists, etc.
            
            this.ConnectionState = ZoneConnectionState.Disconnected;
            this.Client.Close();
        }

        internal override float GetMovementSpeed(float speedMod, int moveBonus)
        {
            // TODO: modify the below speedMod based on AAs that affect speed

            // TODO: cap run speed, taking into account fast as hell bards

            return base.GetMovementSpeed(speedMod, 0);
        }

        private int GetRegenAmount()
        {
            int level = this.Level;

            int hp = 0;
            if (level <= 19) {
                if (this.Sitting)
                    hp += 2;
                else
                    hp += 1;
            }
            else if (level <= 49) {
                if (this.Sitting)
                    hp += 3;
                else
                    hp += 1;
            }
            else if (level == 50) {
                if (this.Sitting)
                    hp += 4;

                else
                    hp += 1;
            }
            else if (level <= 55) {
                if (this.Sitting)
                    hp += 5;
                else
                    hp += 2;
            }
            else if (level <= 58) {
                if (this.Sitting)
                    hp += 6;
                else
                    hp += 3;
            }
            else if (level <= 65) {
                if (this.Sitting)
                    hp += 7;
                else
                    hp += 4;
            }
            else {
                if (this.Sitting)
                    hp += 8;
                else
                    hp += 5;
            }
            if (this.Race == (short)CharRaces.Iksar || this.Race == (short)CharRaces.Troll) {
                if (level <= 19) {
                    if (this.Sitting)
                        hp += 4;
                    else
                        hp += 2;
                }
                else if (level <= 49) {
                    if (this.Sitting)
                        hp += 6;
                    else
                        hp += 2;
                }
                else if (level == 50) {
                    if (this.Sitting)
                        hp += 8;
                    else
                        hp += 2;
                }
                else if (level == 51) {
                    if (this.Sitting)
                        hp += 12;
                    else
                        hp += 6;
                }
                else if (level <= 56) {
                    if (this.Sitting)
                        hp += 16;
                    else
                        hp += 10;
                }
                else if (level <= 65) {
                    if (this.Sitting)
                        hp += 18;
                    else
                        hp += 12;
                }
                else {
                    if (this.Sitting)
                        hp += 20;
                    else
                        hp += 10;
                }
            }
            // TODO: AA Regens

            if (_stunned || _mezzed)
                hp /= 4;

            return hp;
        }

        /// <summary></summary>
        protected override bool CanDualWield()
        {
            bool h2hdw = false;

            switch ((CharClasses)this.Class) {
                case CharClasses.Warrior:
                case CharClasses.Berserker:
                case CharClasses.Rogue:
                case CharClasses.WarriorGM:
                case CharClasses.BerserkerGM:
                case CharClasses.RogueGM:
                    if (this.Level < 13)
                        return false;
                    break;
                case CharClasses.Bard:
                case CharClasses.Ranger:
                case CharClasses.BardGM:
                case CharClasses.RangerGM:
                    if (this.Level < 17)
                        return false;
                    break;
                case CharClasses.BeastLord:
                case CharClasses.BeastlordGM:
                    if (this.Level < 17)
                        return false;
                    h2hdw = true;
                    break;
                case CharClasses.Monk:
                case CharClasses.MonkGM:
                    h2hdw = true;
                    break;
                default:
                    return false;
            }

            Item priItem = GetEquipment(EquipableType.Primary);
            if (priItem == null || priItem.ItemClass != Item.ITEM_CLASS_COMMON)
                return h2hdw;   // No weapon in hand, so using hand to hand - and only monks and bl's can dual wield fists

            return GetSkillLevel(Skill.DualWield) > 0;
        }

        internal bool CanGMAvoidFallingDamage()
        {
            return this.GMStatus >= MIN_STATUS_TO_AVOID_FALLING;
        }

        /// <summary>Determines if we are able to attack.</summary>
        /// <remarks>Doesn't determine ability to attack with a specific item or ability, rather simply general ability to engage in combat.</remarks>
        protected override bool IsAbleToAttack()
        {
            /*  Things which prevent us from attacking:
             *      - being under AI control (the AI does the attacks)
             *      - casting a spell (unless a bard)
             *      - not having a target
             *      - being stunned or mezzed
             *      - being dead
             *      - having recently used a ranged weapon
             *      - being feared (or fleeing)
             *      - using divine aura
             *      - somehow using a weapon that the player doesn't meet the reqs to use
             */

            bool retVal = false;

            if (!this.IsAIControlled && !this.Dead && (!this.IsCasting || this.Class == (byte)CharClasses.Bard) // TODO: add more checks for spells
                && !_stunned && !_mezzed && !_feared && this.Stance != Stance.Dead) {   // TODO: is the stance check necessary?  can it be rolled into this.Dead?
                retVal = true;
            }

            if (retVal && !_rangedAttackTimer.Peek())
                retVal = false;     // No melee attacking whilst range attacking, and no range attacking if delay hasn't expired

            // TODO: check for divine aura (not allowed)

            // TODO: check for not meeting weapon reqs

            return retVal;
        }

        protected override bool IsAbleToAttack(Mob target, bool spellAttack)
        {
            if (!base.IsAbleToAttack(target, spellAttack))
                return false;

            // TODO: check if we're trying to beat on our pet (not allowed)

            // TODO: handle dueling
            if (target is ZonePlayer)
                return false;

            return true;
        }

        internal void SetSkillLevel(Skill skill, uint level)
        {
            if (skill > Skill.Highest)
                throw new ArgumentException("Skill is beyond the range of legal skill values.", "skill");

            _pp.Skills[(uint)skill] = level;

            SkillUpdate su = new SkillUpdate() { SkillId = (uint)skill, SkillValue = level };
            EQApplicationPacket<SkillUpdate> suPack = new EQApplicationPacket<SkillUpdate>(AppOpCode.SkillUpdate, su);
            this.Client.SendApplicationPacket(suPack);
        }

        internal void SetLanguageSkillLevel(Language lang, int skillLevel)
        {
            if (lang > Language.Unknown2)
                throw new ArgumentException("Language is beyond the range of legal languages.", "lang");

            if (skillLevel <= 100) {
                _pp.Languages[(int)lang] = (byte)skillLevel;
                this.MsgMgr.SendMessageID((uint)MessageType.Skills, MessageStrings.GAIN_LANGUAGE_POINT);
            }
        }
        
        internal override uint GetSkillLevel(Skill skill)
        {
            if (skill > Skill.Highest)
                throw new ArgumentException("Skill is beyond the range of legal skill values.", "skill");

            return GetBaseSkillLevel(skill);    // TODO: augment from item bonuses, buffs, etc.
        }

        internal uint GetBaseSkillLevel(Skill skill)
        {
            if (skill > Skill.Highest)
                throw new ArgumentException("Skill is beyond the range of legal skill values.", "skill");

            return _pp.Skills[(uint)skill];
        }

        internal uint GetSkillCap(Skill skill)
        {
            if (skill > Skill.Highest)
                throw new ArgumentException("Skill is beyond the range of legal skill values.", "skill");

            byte level = Math.Min(this.Level, (byte)75);     // cap the level at which skill caps are checked
            short? skillCap = ZoneServer.WorldSvc.GetSkillCap((byte)skill, this.Class, level);

            if (skillCap == null) {
                _log.ErrorFormat("Unable to find a skill cap for skill: {0}, class: {1} and level: {2}", skill, this.Class, level);
                return 0;
            }

            return (uint)skillCap;
        }

        /// <summary>Checks if the player gains a skill point in a particular skill.</summary>
        /// <param name="skill">The skill to check for a skill up for.</param>
        /// <param name="target">The mob the check is being performed against. Null if not that kind of skill.</param>
        /// <param name="chanceMod">Modifier to add to the chance probability. A value of 80+ would almost ensure a skillup.</param>
        internal bool CheckForSkillUp(Skill skill, Mob target, int chanceMod)
        {
            if (this.IsAIControlled)
                return false;

            if (skill > Skill.Highest)
                throw new ArgumentException("Skill type is beyond the range of legal skill types.", "skill");

            uint skillLvl = GetBaseSkillLevel(skill);
            uint maxLvl = GetSkillCap(skill);

            if (skillLvl < maxLvl) {
                if (target != null) {
                    if (target is ZonePlayer || Mob.GetConsiderDificulty(this.Level, target.Level) == ConLevel.Green)
                        return false;   // TODO: add check for aggro immunity on target (spec attack)
                }

                // the higher the current skill level, the harder it is to skill up
                int chance = ((252 - (int)skillLvl) / 20) + chanceMod;
                chance = Math.Max(chance, 1);   // Always have at least a slim chance

                // TODO: add configurable global skill up modifier
                Random rand = new Random();
                if (rand.Next(100) < chance) {
                    SetSkillLevel(skill, skillLvl + 1);
                    _log.DebugFormat("{0} skilled up {1} from skill level {2} with a chance of {3}", this.Name, skill, skillLvl, chance);
                    return true;
                }
                //else
                //    _log.DebugFormat("{0} failed to skill up {1} from skill level {2} with a chance of {3}", this.Name, skill, skillLvl, chance);
            }
            else
                _log.DebugFormat("{0} unable to skill up {1} from skill level {2} due to a cap of {3}", this.Name, skill, skillLvl, maxLvl);

            return false;
        }

        internal bool CheckForSkillUp(Language lang, int teacherSkill)
        {
            int skillLvl = this.PlayerProfile.Languages[(int)lang];

            if (skillLvl < 100) {
                int chance = 5 + ((teacherSkill - skillLvl) / 10);     // the higher the teacher's skill than yours, the better chance to learn
                chance = Math.Max(chance, 1);   // Always have at least a slim chance
                
                // TODO: add configurable global skill up modifier
                Random rand = new Random();
                if (rand.Next(100) < chance) {
                    SetLanguageSkillLevel(lang, skillLvl + 1);
                    _log.DebugFormat("{0} skilled up {1} from skill level {2} with a chance of {3}", this.Name, lang, skillLvl, chance);
                    return true;
                }
                else
                    _log.DebugFormat("{0} failed to skill up {1} from skill level {2} with a chance of {3}", this.Name, lang, skillLvl, chance);
            }

            return false;
        }

        #region Statistic Calculations
        protected void RecalculateWeight()
        {
            // TODO: implement
        }

        protected override void CalcStatModifiers()
        {
            // TODO: calculate item bonuses
            // TODO: calculate food bonuses
            CalcSpellBonuses();

            // TODO: calculate AA bonuses

            RecalculateWeight();

            CalcAC();
            CalcAttack();
            CalcHaste();

            // TODO: calc all of our stats

            // TODO: calc all of our resistances

            CalcMaxHP();
            CalcMaxMana();
            CalcMaxEndurance();

            // TODO: add a rooted check?

            // TODO: add xp rate gain bonuses to a new xpRate member... much later. Not even sure when this would come in.
        }

        private int CalcAC()
        {
            int avoidance = CalcACModifier() + (((int)GetSkillLevel(Skill.Defense) * 16) / 9);
            Math.Max(avoidance, 0);

            int mitigation = 0;
            if (GetCasterClass() == CasterClass.Int) {
                mitigation = (int)GetSkillLevel(Skill.Defense) / 4 + 1;     // TODO: add item bonuses for ac
                mitigation -= 4;    // Guess it is off by 4
            }
            else {
                mitigation = (int)GetSkillLevel(Skill.Defense) / 3;     // TODO: add item bonuses for ac
                if (this.Class == (byte)CharClasses.Monk)
                    mitigation += this.Level * 13 / 10;     // 13/10 might be wrong but is close
            }

            int naturalAC = ((avoidance + mitigation) * 1000) / 847;
            if (this.Race == (short)CharRaces.Iksar) {
                naturalAC += 12;
                int iksarLevel = this.Level - 10;
                Math.Min(iksarLevel, 25);
                if (iksarLevel > 0)
                    naturalAC += iksarLevel * 12 / 10;
            }

            // TODO: add AC related spell bonuses

            this.AC = naturalAC;
            return this.AC;
        }

        private int CalcACModifier()
        {
            int agility = this.AGI;
            int level = this.Level;

            if (agility < 1 || level < 1)
                throw new Exception("Invalid values (< 1) for Agility and/or Level found while calculating the AC Modifier.");

            if (agility <= 74) {
                if (agility == 1)
                    return -24;
                else if (agility <= 3)
                    return -23;
                else if (agility == 4)
                    return -22;
                else if (agility <= 6)
                    return -21;
                else if (agility <= 8)
                    return -20;
                else if (agility == 9)
                    return -19;
                else if (agility <= 11)
                    return -18;
                else if (agility == 12)
                    return -17;
                else if (agility <= 14)
                    return -16;
                else if (agility <= 16)
                    return -15;
                else if (agility == 17)
                    return -14;
                else if (agility <= 19)
                    return -13;
                else if (agility == 20)
                    return -12;
                else if (agility <= 22)
                    return -11;
                else if (agility <= 24)
                    return -10;
                else if (agility == 25)
                    return -9;
                else if (agility <= 27)
                    return -8;
                else if (agility == 28)
                    return -7;
                else if (agility <= 30)
                    return -6;
                else if (agility <= 32)
                    return -5;
                else if (agility == 33)
                    return -4;
                else if (agility <= 35)
                    return -3;
                else if (agility == 36)
                    return -2;
                else if (agility <= 38)
                    return -1;
                else if (agility <= 65)
                    return 0;
                else if (agility <= 70)
                    return 1;
                else if (agility <= 74)
                    return 5;
            }
            else if (agility <= 137) {
                if (agility == 75) {
                    if (level <= 6)
                        return 9;
                    else if (level <= 19)
                        return 23;
                    else if (level <= 39)
                        return 33;
                    else
                        return 39;
                }
                else if (agility >= 76 && agility <= 79) {
                    if (level <= 6)
                        return 10;
                    else if (level <= 19)
                        return 23;
                    else if (level <= 39)
                        return 33;
                    else
                        return 40;
                }
                else if (agility == 80) {
                    if (level <= 6)
                        return 11;
                    else if (level <= 19)
                        return 24;
                    else if (level <= 39)
                        return 34;
                    else
                        return 41;
                }
                else if (agility >= 81 && agility <= 85) {
                    if (level <= 6)
                        return 12;
                    else if (level <= 19)
                        return 25;
                    else if (level <= 39)
                        return 35;
                    else
                        return 42;
                }
                else if (agility >= 86 && agility <= 90) {
                    if (level <= 6)
                        return 12;
                    else if (level <= 19)
                        return 26;
                    else if (level <= 39)
                        return 36;
                    else
                        return 42;
                }
                else if (agility >= 91 && agility <= 95) {
                    if (level <= 6)
                        return 13;
                    else if (level <= 19)
                        return 26;
                    else if (level <= 39)
                        return 36;
                    else
                        return 43;
                }
                else if (agility >= 96 && agility <= 99) {
                    if (level <= 6)
                        return 14;
                    else if (level <= 19)
                        return 27;
                    else if (level <= 39)
                        return 37;
                    else
                        return 44;
                }
                else if (agility == 100 && level >= 7) {
                    if (level <= 19)
                        return 28;
                    else if (level <= 39)
                        return 38;
                    else
                        return 45;
                }
                else if (level <= 6) {
                    return 15;
                }
                //level is > 6
                else if (agility >= 101 && agility <= 105) {
                    if (level <= 19)
                        return 29;
                    else if (level <= 39)
                        return 39;// not verified
                    else
                        return 45;
                }
                else if (agility >= 106 && agility <= 110) {
                    if (level <= 19)
                        return 29;
                    else if (level <= 39)
                        return 39;// not verified
                    else
                        return 46;
                }
                else if (agility >= 111 && agility <= 115) {
                    if (level <= 19)
                        return 30;
                    else if (level <= 39)
                        return 40;// not verified
                    else
                        return 47;
                }
                else if (agility >= 116 && agility <= 119) {
                    if (level <= 19)
                        return 31;
                    else if (level <= 39)
                        return 41;
                    else
                        return 47;
                }
                else if (level <= 19) {
                    return 32;
                }
                //level is > 19
                else if (agility == 120) {
                    if (level <= 39)
                        return 42;
                    else
                        return 48;
                }
                else if (agility <= 125) {
                    if (level <= 39)
                        return 42;
                    else
                        return 49;
                }
                else if (agility <= 135) {
                    if (level <= 39)
                        return 42;
                    else
                        return 50;
                }
                else {
                    if (level <= 39)
                        return 42;
                    else
                        return 51;
                }
            }
            else if (agility <= 300) {
                if (level <= 6) {
                    if (agility <= 139)
                        return (21);
                    else if (agility == 140)
                        return (22);
                    else if (agility <= 145)
                        return (23);
                    else if (agility <= 150)
                        return (23);
                    else if (agility <= 155)
                        return (24);
                    else if (agility <= 159)
                        return (25);
                    else if (agility == 160)
                        return (26);
                    else if (agility <= 165)
                        return (26);
                    else if (agility <= 170)
                        return (27);
                    else if (agility <= 175)
                        return (28);
                    else if (agility <= 179)
                        return (28);
                    else if (agility == 180)
                        return (29);
                    else if (agility <= 185)
                        return (30);
                    else if (agility <= 190)
                        return (31);
                    else if (agility <= 195)
                        return (31);
                    else if (agility <= 199)
                        return (32);
                    else if (agility <= 219)
                        return (33);
                    else if (agility <= 239)
                        return (34);
                    else
                        return (35);
                }
                else if (level <= 19) {
                    if (agility <= 139)
                        return (34);
                    else if (agility == 140)
                        return (35);
                    else if (agility <= 145)
                        return (36);
                    else if (agility <= 150)
                        return (37);
                    else if (agility <= 155)
                        return (37);
                    else if (agility <= 159)
                        return (38);
                    else if (agility == 160)
                        return (39);
                    else if (agility <= 165)
                        return (40);
                    else if (agility <= 170)
                        return (40);
                    else if (agility <= 175)
                        return (41);
                    else if (agility <= 179)
                        return (42);
                    else if (agility == 180)
                        return (43);
                    else if (agility <= 185)
                        return (43);
                    else if (agility <= 190)
                        return (44);
                    else if (agility <= 195)
                        return (45);
                    else if (agility <= 199)
                        return (45);
                    else if (agility <= 219)
                        return (46);
                    else if (agility <= 239)
                        return (47);
                    else
                        return (48);
                }
                else if (level <= 39) {
                    if (agility <= 139)
                        return (44);
                    else if (agility == 140)
                        return (45);
                    else if (agility <= 145)
                        return (46);
                    else if (agility <= 150)
                        return (47);
                    else if (agility <= 155)
                        return (47);
                    else if (agility <= 159)
                        return (48);
                    else if (agility == 160)
                        return (49);
                    else if (agility <= 165)
                        return (50);
                    else if (agility <= 170)
                        return (50);
                    else if (agility <= 175)
                        return (51);
                    else if (agility <= 179)
                        return (52);
                    else if (agility == 180)
                        return (53);
                    else if (agility <= 185)
                        return (53);
                    else if (agility <= 190)
                        return (54);
                    else if (agility <= 195)
                        return (55);
                    else if (agility <= 199)
                        return (55);
                    else if (agility <= 219)
                        return (56);
                    else if (agility <= 239)
                        return (57);
                    else
                        return (58);
                }
                else {	//lvl >= 40
                    if (agility <= 139)
                        return (51);
                    else if (agility == 140)
                        return (52);
                    else if (agility <= 145)
                        return (53);
                    else if (agility <= 150)
                        return (53);
                    else if (agility <= 155)
                        return (54);
                    else if (agility <= 159)
                        return (55);
                    else if (agility == 160)
                        return (56);
                    else if (agility <= 165)
                        return (56);
                    else if (agility <= 170)
                        return (57);
                    else if (agility <= 175)
                        return (58);
                    else if (agility <= 179)
                        return (58);
                    else if (agility == 180)
                        return (59);
                    else if (agility <= 185)
                        return (60);
                    else if (agility <= 190)
                        return (61);
                    else if (agility <= 195)
                        return (61);
                    else if (agility <= 199)
                        return (62);
                    else if (agility <= 219)
                        return (63);
                    else if (agility <= 239)
                        return (64);
                    else
                        return (65);
                }
            }
            else
                return (65 + ((agility - 300) / 21)); //seems about 21 agil per extra AC pt over 300...

            return 0;
        }

        private int CalcAttack()
        {
            // TODO: add up attack related bonuses from items, spells & groupLeadershipAAOffenseEnhancement
            this.Attack = 0;
            return this.Attack;
        }

        private int CalcHaste()
        {
            this.Haste = 0;     // TODO: calculate haste from items, spells, etc.
            return this.Haste;
        }

        internal uint CalcMaxEndurance()
        {
            int stats = this.STR + this.STA + this.DEX + this.AGI;
            int levelBase = this.Level * 15;
            int statsCapped800 = Math.Min(stats, 800);
            int bonusUpTo800 = statsCapped800 / 4;
            int bonus400To800 = 0, halfBonus400To800 = 0, bonus800Plus = 0, halfBonus800Plus = 0;

            if (stats > 400) {
                bonus400To800 = (statsCapped800 - 400) / 4;
                halfBonus400To800 = Math.Max(statsCapped800 - 400, 0) / 8;

                if (stats > 800) {
                    bonus800Plus = ((stats - 800) / 8) * 2;
                    halfBonus800Plus = (stats - 800) / 16;
                }
            }

            int bonusSum = bonusUpTo800 + bonus400To800 + halfBonus400To800 + bonus800Plus + halfBonus800Plus;

            _maxEndurance = (uint)(levelBase + (bonusSum * 3 * this.Level) / 40);   // TODO: add endurance related spell and item bonuses
            return _maxEndurance;
        }

        internal int CalcBaseHP()
        {
            int clf = GetClassLevelFactor();
            int over255 = Math.Max((this.STA - 255) / 2, 0);

            _baseHP = 5 + (this.Level * clf / 10) + (((this.STA - over255) * this.Level * clf / 3000)) + ((over255 * this.Level) * clf / 6000);
            //_log.DebugFormat("Base HP for {0} is calculated to be {1}", this.Name, _baseHP);
            return _baseHP;
        }

        protected override int CalcMaxHP()
        {
            int aaHpAugBase = 10000;    // TODO: add in max hp AA bonuses (Natural Durability, Physical Enhancement, Planar Durability, etc.)
            int maxHp = CalcBaseHP();   // TODO: add in item bonuses

            maxHp *= aaHpAugBase / 10000;
            // TODO: add in spell, aa & groupLeadershipAAHealthEnhancement bonuses

            this.MaxHP = maxHp;

            if (this.HP > this.MaxHP) {     // When zoning in for very first time, this will be true and reset the HPs to the real value
                _curHP = this.MaxHP;        // bypass property settor so as to avoid packet sends
                _pp.HP = (uint)this.MaxHP;
            }

            //_log.DebugFormat("Max HP for {0} is calculated to be {1}", this.Name, this.MaxHP);
            return this.MaxHP;
        }

        protected override int CalcMaxMana()
        {
            _maxMana = 0;   // TODO: implement
            return _maxMana;
        }
        #endregion

        /// <summary>Takes and slices the given xp amount and then places them into the various xp types for the player.</summary>
        /// <remarks>Incomming xp amount should be augmented for zone xp bonus, etc.</remarks>
        internal void GiveXP(int xpAmt, ConLevel conLvl)    // TODO: add a parameter for rez xp regain or have a separate method?
        {
            //_log.DebugFormat("Giving {0} xp to {1} before modifiers", xpAmt, this.Name);

            int aaXP = 0;
            float xpMod = 1.0f;

            // TODO: figure out what xp amount goes where... for now it all goes direct to main xp

            if ((Races)this.Race == Races.Halfling)     // TODO: shouldn't iksar, trolls and maybe some other be penalized on xp a bit?
                xpMod *= 1.05f;

            if ((CharClasses)this.Class == CharClasses.Rogue || (CharClasses)this.Class == CharClasses.Warrior)
                xpMod *= 1.05f;

            xpMod *= WorldServer.ServerConfig.XPModifier;   // Global xp modifier
            xpAmt = (int)(xpAmt * xpMod);

            // TODO: ConLevel XP scaling

            // TODO: Leadership XP

            _log.DebugFormat("Giving {0} xp to {1} after modifiers", xpAmt, this.Name);
            this.XP += xpAmt;

            // TODO: set AA XP
        }

        internal void RemoveAggro()
        {
            Interlocked.Decrement(ref _aggroCount);

            // TODO: do we want to manage a rest timer here?
        }

        /// <summary>Sends a communication message (tell, say, etc.) to the player.  Since we are sending this player the message, it is the listener.</summary>
        /// <param name="fromName">An optionally blank speaker's name.</param>
        /// <param name="toName">An optionally blank target's name.</param>
        /// <remarks>Messages should already be scrubbed for validity (GM messages intended only for GMs, etc.)</remarks>
        internal void SendChannelMessage(string fromName, string toName, MessageChannel chan, Language lang, int langSkill, string message)
        {
            ChannelMessage cm = new ChannelMessage();
            cm.ChannelId = (int)chan;
            cm.Message = message;
            cm.SpeakerName = string.IsNullOrEmpty(fromName) ? "ZServer" : fromName;     // Why 'ZServer'?

            // Looks like even though we know who the message is intended for, the target field can be blank
            if (!string.IsNullOrEmpty(toName))
                cm.TargetName = toName;
            else if (chan == MessageChannel.Tell)
                cm.TargetName = this.Name;

            int listenerSkill;
            if ((int)lang < Character.MAX_LANGUAGE) {   // Is the language being spoken within the allowable range
                listenerSkill = this.PlayerProfile.Languages[(int)lang];
                cm.LanguageId = (int)lang;
                if (chan == MessageChannel.Group && listenerSkill < 100) {  // group messages in unmastered languages, check for skill up
                    if ((this.PlayerProfile.Languages[(int)lang] <= langSkill) && fromName != this.Name)
                        CheckForSkillUp(lang, langSkill);
                }
            }
            else {
                // Not in allowable range, assume common tongue
                listenerSkill = this.PlayerProfile.Languages[(int)Language.CommonTongue];
                cm.LanguageId = (int)Language.CommonTongue;
            }

            // Set effective language skill = lower of sender and receiver skills
            int effSkill = Math.Min(langSkill, listenerSkill);
            effSkill = Math.Min(effSkill, 100); // Cap at 100
            cm.LanguageSkill = effSkill;

            EQRawApplicationPacket cmPack = new EQRawApplicationPacket(AppOpCode.ZonePlayerToBind, this.Client.IPEndPoint, cm.Serialize());
            this.Client.SendApplicationPacket(cmPack);
        }

        #region Timer Callbacks
        private void AutosaveTimerCallback(object state)
        {
            if (this.ConnectionState == ZoneConnectionState.Disconnected)
                return;

            // We're already on a thread pool thread, so no reason to background or async call the save method
            try {
                Save();
                this.MsgMgr.SendSpecialMessage(MessageType.Default, "Saved.");
            }
            catch (Exception e) {
                _log.Error("Autosave error.", e);
            }
        }
        #endregion

        #region Event Handlers
        void InvMgr_ItemMoved(object sender, ItemMoveEventArgs e)
        {
            // TODO: when attuneable items are in, check for attuneable and set invItem attuned

            // TODO: old emu set the equipment material here but I don't see that ever being used

            if (e.ToSlotID <= (uint)InventorySlot.EquipSlotsEnd)
                TriggerWearChange(InventoryManager.GetEquipableType((InventorySlot)e.ToSlotID));

            if (e.FromSlotID <= (uint)InventorySlot.EquipSlotsEnd)
                TriggerWearChange(InventoryManager.GetEquipableType((InventorySlot)e.FromSlotID));

            if (e.NotifyClient) {
                MoveItem mi = new MoveItem() { FromSlot = e.FromSlotID, NumberInStack = e.Quantity, ToSlot = e.ToSlotID };
                EQApplicationPacket<MoveItem> miPack = null;

                if (e.Quantity > 0) {   // Moving from a stack?
                    mi.NumberInStack = 0xFFFFFFFF;
                    if (e.ToSlotID == 0xFFFFFFFF)
                        miPack = new EQApplicationPacket<MoveItem>(AppOpCode.DeleteItem, mi);
                    else
                        miPack = new EQApplicationPacket<MoveItem>(AppOpCode.MoveItem, mi);     // TODO: this right?

                    for (int i = 0; i < e.Quantity; i++)
                        this.Client.SendApplicationPacket(miPack);
                }
                else {
                    mi.NumberInStack = 0xFFFFFFFF;
                    miPack = new EQApplicationPacket<MoveItem>(AppOpCode.MoveItem, mi);
                    this.Client.SendApplicationPacket(miPack);
                }
            }

            CalcStatModifiers();
        }

        void InvMgr_ItemChargeUsed(object sender, ItemChargeUseEventArgs e)
        {
            MoveItem mi = new MoveItem() { FromSlot = e.SlotID, NumberInStack = 0xFFFFFFFF, ToSlot = 0xFFFFFFFF };
            EQApplicationPacket<MoveItem> miPack = new EQApplicationPacket<MoveItem>(AppOpCode.DeleteCharge, mi);
            
            for (int i = 0; i < e.Charges; i++)
                this.Client.SendApplicationPacket(miPack);
        }

        protected void OnManaStaChanged(EventArgs e)
        {
            if (!(this.ConnectionState == ZoneConnectionState.Connected) || this.IsCasting) {
                if (_lastReportedMana != this.Mana || _lastReportedEnd != this.Endurance) {
                    ManaStaChange msc = new ManaStaChange() { NewEndurance = this.Endurance, NewMana = (uint)this.Mana, SpellId = 0 };
                    EQApplicationPacket<ManaStaChange> mscPack = new EQApplicationPacket<ManaStaChange>(AppOpCode.ManaChange, msc);
                    this.Client.SendApplicationPacket(mscPack);

                    _lastReportedEnd = this.Endurance;
                    _lastReportedMana = (uint)this.Mana;
                }
            }
        }

        protected void OnLevelGained(EventArgs e)
        {
            EventHandler<EventArgs> handler = LevelGained;

            if (handler != null)
                handler(this, e);
        }

        internal void OnServerCommand(ServerCommandEventArgs sce)
        {
            EventHandler<ServerCommandEventArgs> handler = ServerCommand;

            if (handler != null)
                handler(this, sce);
        }
        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            _autoSaveTimer.Dispose();
            _invMgr = null;
        }

        #endregion

        #region Static Members
        static internal int CalcRecommendedLevelBonus(int level, int recLevel, int baseStatVal)
        {
            if (level < recLevel) {
                int statMod = (level * 10000 / recLevel) * baseStatVal;

                if (statMod < 0)
                    statMod -= 5000;
                else
                    statMod += 5000;

                return statMod / 10000;
            }
            else
                return baseStatVal;
        }
        #endregion
    }
}
