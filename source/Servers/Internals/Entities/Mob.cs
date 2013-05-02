using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using EQEmulator.Servers.Internals.Packets;
using EQEmulator.Servers.Internals.Data;
using System.Threading;

namespace EQEmulator.Servers.Internals.Entities
{
    public enum CharClasses : ushort
    {
        Warrior = 1,
        Cleric = 2,
        Paladin = 3,
        Ranger = 4,
        ShadowKnight = 5,
        Druid = 6,
        Monk = 7,
        Bard = 8,
        Rogue = 9,
        Shaman = 10,
        Necromancer = 11,
        Wizard = 12,
        Magician = 13,
        Enchanter = 14,
        BeastLord = 15,
        Berserker = 16,
        WarriorGM = 20,
        ClericGM = 21,
        PaladinGM = 22,
        RangerGM = 23,
        ShadowKnightGM = 24,
        DruidGM = 25,
        MonkGM = 26,
        BardGM = 27,
        RogueGM = 28,
        ShamanGM = 29,
        NecromancerGM = 30,
        WizardGM = 31,
        MagicianGM = 32,
        EnchanterGM = 33,
        BeastlordGM = 34,
        BerserkerGM = 35,
        Banker = 40,
        Merchant = 41,
        AdventureRecruiter = 60,
        AdventureMerchant = 61,
        LDONTreasure = 62,      // objects you can use /open on - first seen in LDON
        Corpse = 62,            // only seen on Danvi's corpse in Akheva thus far
        TributeMaster = 64      // Not sure?
    }

    internal enum CasterClass
    {
        Wis  = 1,
        Int  = 2,
        None = 3
    }

    /// <summary>See Races.cs for complete list of races.</summary>
    internal enum Races : short
    {
        Human       = 1,
        Barbarian   = 2,
        Erudite     = 3,
        WoodElf     = 4,
        HighElf     = 5,
        DarkElf     = 6,
        HalfElf     = 7,
        Dwarf       = 8,
        Troll       = 9,
        Ogre        = 10,
        Halfling    = 11,
        Gnome       = 12,
        WereWolf    = 14,
        Wolf        = 42,
        Bear        = 43,
        LavaDragon  = 49,
        Skeleton    = 60,
        Tiger       = 63,
        Froglok2    = 74,	// TODO: Not sure why /who all reports race as 74 for frogloks
        Elemental   = 75,
        Alligator   = 91,
        EyeOfZomm   = 108,
        WolfElemental = 120,
        InvisibleMan  = 127,
        Iksar       = 128,
        Vahshir     = 130,
        Wurm        = 158,
        GhostDragon = 196,
        Froglok     = 330
    }

    public enum BodyType
    {
        Humanoid        = 1,
        Lycanthrope     = 2,
        Undead          = 3,
        Giant           = 4,
        Construct       = 5,
        ExtraPlanar     = 6,
        Magical         = 7,    // correct name?
        SummonedUndead  = 8,
        NoTarget        = 11,   // no name, can't target this type
        Vampire         = 12,
        AtenhaRa        = 13,
        GreaterAkheva   = 14,
        KhatiSha        = 15,
        Seru            = 16,   // unconfirmed
        Zek             = 19,
        Luggald         = 20,
        Animal          = 21,
        Insect          = 22,
        Monster         = 23,
        Summoned        = 24,   // Elemental?
        Plant           = 25,
        Dragon          = 26,
        Summoned2       = 27,
        Summoned3       = 28,
        VeliousDragon   = 30,
        Dragon3         = 32,
        Boxes           = 33,
        Muramite        = 34,
        NoTarget2       = 60,
        SwarmPet        = 63,
        InvisibleMan    = 66,   // no name, seen on "InvisMan", can be /targeted
        Special         = 67
    }   // BodyTypes above 64 make the mob not show up... show up where?  in /who all?

    public enum Stance : short
    {
        Standing    = 0x64,
        Freeze      = 0x66,
        Looting     = 0x69,
        Sitting     = 0x6e,
        Crouching   = 0x6f,
        Dead        = 0x73
    }

    internal enum AttackAnimation
    {
        Kick            = 1,
        Piercing        = 2,
        TwoHandSlashing = 3,
        TwoHandWeapon   = 4,
        OneHandWeapon   = 5,
        DualWield       = 6,
        TailRake        = 7,    // Also Slam, Dragon Punch
        HandToHand      = 8,
        ShootBow        = 9,
        RoundKick       = 11,
        SwarmAttack     = 20,   // Not 100% here
        FlyingKick      = 45,
        TigerClaw       = 46,
        EagleStrike     = 47
    }

    internal enum AttackAvoidanceType
    {
        Invulnerable    = -5,
        Dodge           = -4,
        Riposte         = -3,
        Parry           = -2,
        Block           = -1,
        Normal          = 0
    }

    internal enum ConLevel
    {
        Green       = 2,
        LightBlue   = 18,
        Blue        = 4,
        White       = 20,
        Yellow      = 15,
        Red         = 13
    }

    internal enum ResistType
    {
        None = 0,
        Magic = 1,
        Fire = 2,
        Cold = 3,
        Poison = 4,
        Disease = 5,
        Chromatic = 6,
        Prismatic = 7,
        Physical = 8 // See muscle shock, back swing
    }

    #region Event Data Structures
    internal class StanceChangedEventArgs : EventArgs
    {
        internal SpawnAppearanceType AppearanceType { get; set; }
        internal short StanceValue { get; set; }

        internal StanceChangedEventArgs(SpawnAppearanceType appearanceType, short stanceValue)
        {
            AppearanceType = appearanceType;
            StanceValue = stanceValue;
        }
    }

    internal class PosUpdateEventArgs : EventArgs
    {
        internal bool OnlyNearby { get; set; }
        internal bool UseDelta { get; set; }

        internal PosUpdateEventArgs(bool onlyToNearby, bool useDelta)
        {
            OnlyNearby = onlyToNearby;
            UseDelta = useDelta;
        }
    }

    internal class WearChangeEventArgs : EventArgs
    {
        internal WearChange WearChange { get; set; }

        internal WearChangeEventArgs(WearChange wearChange)
        {
            WearChange = wearChange;
        }
    }

    internal class AnimationEventArgs : EventArgs
    {
        internal AttackAnimation Anim { get; set; }

        internal AnimationEventArgs(AttackAnimation anim)
        {
            Anim = anim;
        }
    }

    internal class DamageEventArgs : EventArgs
    {
        internal ushort SourceId { get; set; }
        internal byte Type { get; set; }
        internal uint Damage { get; set; }
        internal ushort SpellId { get; set; }
    }

    internal class ChannelMessageEventArgs : EventArgs
    {
        internal Mob From { get; set; }
        internal MessageChannel Channel { get; set; }
        internal Language Language { get; set; }
        internal int LangSkill { get; set; }
        internal string Message { get; set; }
    }
    #endregion

    internal partial class Mob : Entity
    {
        private static int[] _monkDelaysHuman = {99,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,35,35,35,35,35,34,34,34,
                                                 34,34,33,33,33,33,33,32,32,32,32,32,31,31,31,31,31,30,30,30,29,29,29,28,28,28,27,26,24,22,20,20,20};

        private static int[] _monkDelaysIksar = {99,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,36,35,35,35,
                                                 35,35,34,34,34,34,34,33,33,33,33,33,32,32,32,32,32,31,31,31,30,30,30,29,29,29,28,27,24,22,20,20,20};

        private static int[] _monkDamage = {99,4,4,4,4,5,5,5,5,5,6,6,6,6,6,7,7,7,7,7,8,8,8,8,8,9,9,9,9,9,10,10,10,10,10,11,11,11,11,11,
                                            12,12,12,12,12,13,13,13,13,13,14,14,14,14,14,14,14,14,14,14,14,14,15,15,15,15};

        private const int TIC_INTERVAL = 6000;
        private const int ATTACK_INTERVAL = 2000;
        private const int THINK_INTERVAL = 150;         // originally 150
        private const int TARGET_CHECK_INTERVAL = 500;
        private const int MOVE_INTERVAL = 100;          // originally 100
        private const int SCAN_INTERVAL = 500;
        private const int CLIENT_SCAN_DELAY = 750;      // used in reverse aggro
        private const int ASSIST_CHECK_DELAY = 3000;    // how often an engaged NPC will yell for help
        private const int ENRAGED_DELAY = 360000;
        private const int ENRAGED_DURATION = 10000;
        private const float NPC_RUNANIM_RATIO = 26.0F;  // multiplier of emu speed to get client speed. Tweak if pathing mobs seem to jump forward or backwards. Could be dynamic based on avg ping?
        private const int NPC_SPEED_MULTIPLIER = 46;    // multiplier which yields map units from mob's movement rate
        private const int MANA_UPDATE_INTERVAL = 2000;  // 2 min
        private const int UNARMED_DAMAGE = 2;
        private const int UNARMED_DELAY = 36;
        private const int TOHIT_MAX = 95;
        private const int TOHIT_MIN = 5;
        private const byte DAMAGE_TYPE_FALLING = 0xFC;
        private const byte DAMAGE_TYPE_SPELL = 0xE7;
        private const byte DAMAGE_TYPE_UNKNOWN = 0xFF;
        private const int AI_SPELL_RANGE = 100;     // Max range of buffs

        // Events raised by various actions of the mob. Subscribed to by the Mob Manager
        internal event EventHandler<EventArgs> StanceChanged;
        internal event EventHandler<StanceChangedEventArgs> StanceChangedEx;
        internal event EventHandler<PosUpdateEventArgs> PositionUpdated;
        internal event EventHandler<WearChangeEventArgs> WearChanged;
        internal event EventHandler<EventArgs> HPUpdated;
        internal event EventHandler<EventArgs> ManaUpdated;
        internal event EventHandler<AnimationEventArgs> PlayAnimation;
        internal event EventHandler<DamageEventArgs> Damaged;
        internal event EventHandler<EventArgs> IdleScanning;
        internal event EventHandler<ChannelMessageEventArgs> ChannelMessage;

        protected short _attack = 0, _str = 0, _sta = 0, _dex = 0, _agi = 0, _int = 0, _wis = 0, _cha = 0;
        protected short _magicResist = 0, _coldResist = 0, _fireResist = 0, _deathResist = 0, _poisonResist = 0;
        protected int _ac = 0, _curHP = 0, _maxHP = 0, _baseHP = 0, _curMana = 0, _maxMana = 0;
        protected short _hpRegen = 0, _manaRegen = 0;
        protected byte _gender = 0, _baseGender = 0, _class = 0, _level = 0;
        protected short _race = 0, _baseRace = 0;
        protected byte _texture = 0xFF, _helmTexture = 0xFF, _light = 0;
        protected byte _hairColor = 0xFF, _beardColor = 0xFF, _eyeColor1 = 0xFF, _eyeColor2 = 0xFF, _hairStyle = 0xFF, _luclinFace = 0xFF, _beard = 0xFF;
        protected BodyType _bodyType = BodyType.Humanoid;
        protected Stance _stance = Stance.Standing;
        protected uint _deity = 0;   // TODO: anyone but zonePlayer need this?
        private uint _copper = 0, _silver = 0, _gold = 0, _platinum = 0;
        protected float _deltaX = 0.0F, _deltaY = 0.0F, _deltaZ = 0.0F;
        protected int _deltaHeading = 0, _haste = 0;
        protected float _size, _baseRunSpeed = 0.7F, _attackSpeed = 0.0F;   // % of increase or decrease in attack speed (not haste)
        protected ushort _animation, _runAnimSpeed = 0;
        protected bool _findable = false, _trackable = true, _moved = false, _moving = false, _walkTimerCompleted = false;
        protected bool _invulnerable = false, _invis = false, _invisToUndead = false, _invisToAnimals = false, _sneaking = false, _hidden = false, _improvedHidden = false;
        protected bool _seeInvis = false, _seeInvisToUndead = false, _seeInvisToAnimals = false, _seeHide = false, _seeImprovedHide = false;
        protected bool _mezzed = false, _stunned = false, _charmed = false, _rooted = false, _silenced = false, _inWater = false;
        protected float _tarX = 0.0f, _tarY = 0.0f, _tarZ = 0.0f, _tarVector = 0.0f, _tarXVec = 0.0f, _tarYVec = 0.0f, _tarZVec = 0.0f;
        protected byte _tarIdx = 0;
        private int? _followID = null;
        protected SimpleTimer _thinkTimer, _walkTimer, _moveTimer, _scanTimer, _ticTimer, _attackTimer, _rangedAttackTimer, _dwAttackTimer;
        protected SimpleTimer _stunTimer, _manaUpdateTimer, _aiTargetCheckTimer;
        protected DateTime _lastChange = DateTime.MinValue;
        protected bool _aiControlled = false, _dead = false, _feared = false;
        protected Mob _target = null, _spellTarget = null;
        private int _targetCount = 0;
        protected HateManager _hateMgr = null;
        // TODO: add lists for buffs

        public Mob(int id, string name, string surName, float xPos, float yPos, float zPos, float heading)
            : base(id, name, surName, xPos, yPos, zPos, heading)
        {
            Init();
        }
        
        public Mob(int id, string name, string surName, int ac, short attack, short str, short sta, short dex, short agi, short intel, short wis,
            short cha, int curHp, int maxHp, byte gender, short hpRegen, short manaRegen, short race, byte mobClass, byte level, BodyType bodyType,
            byte deity, float xPos, float yPos, float zPos, float heading, float size, float runSpeed, byte light)
            : this(id, name, surName, xPos, yPos, zPos, heading)
        {
            _ac = ac;
            _attack = attack;
            _str = str;
            _sta = sta;
            _dex = dex;
            _agi = agi;
            _int = intel;
            _wis = wis;
            _cha = cha;
            _curHP = curHp;
            _maxHP = maxHp;
            _baseHP = maxHp;
            _gender = gender;
            _baseGender = gender;
            _race = race;
            _baseRace = race;
            _class = mobClass;
            _bodyType = bodyType;
            _deity = deity;
            _level = level;
            _size = size;
            _baseRunSpeed = runSpeed;
            _light = light;
            _hpRegen = hpRegen;
            _manaRegen = manaRegen;
        }

        internal static float GetSize(CharRaces cr)
        {
            switch (cr) {
                case CharRaces.Ogre:
                    return 9;
                case CharRaces.Troll:
                    return 8;
                case CharRaces.Vahshir:
                case CharRaces.Froglok:
                case CharRaces.Barbarian:
                    return 7;
                case CharRaces.Human:
                case CharRaces.Erudite:
                case CharRaces.HighElf:
                case CharRaces.Iksar:
                    return 6;
                case CharRaces.HalfElf:
                    return 5.5F;
                case CharRaces.WoodElf:
                case CharRaces.DarkElf:
                    return 5;
                case CharRaces.Dwarf:
                    return 4;
                case CharRaces.Halfling:
                    return 3.5F;
                case CharRaces.Gnome:
                    return 3;
                default:
                    return 0;
            }
        }

        internal static byte GetDamageTypeForSkill(Skill skill)
        {
            switch (skill) {
                case Skill.OneHandBlunt:
                case Skill.TwoHandBlunt:
                    return 0;
                case Skill.OneHandSlashing:
                case Skill.TwoHandSlashing:
                    return 1;
                case Skill.Archery:
                    return 7;
                case Skill.Backstab:
                    return 8;
                case Skill.Bash:
                    return 10;
                case Skill.DragonPunch:
                    return 21;
                case Skill.EagleStrike:
                    return 23;
                case Skill.FeignDeath:
                    return 4;
                case Skill.FlyingKick:
                    return 30;
                case Skill.HandToHand:
                    return 4;
                case Skill.Kick:
                    return 30;
                case Skill.Piercing:
                    return 36;
                case Skill.RoundKick:
                    return 30;
                case Skill.Throwing:
                    return 51;
                case Skill.TigerClaw:
                    return 23;
                case Skill.Abjure:
                case Skill.Alteration:
                case Skill.BrassInstruments:
                case Skill.Conjuration:
                case Skill.Divination:
                case Skill.Evocation:
                case Skill.Singing:
                case Skill.StringedInstruments:
                case Skill.WindInstruments:
                case Skill.PercussionInstruments:
                    return DAMAGE_TYPE_SPELL;
                case Skill.ApplyPoison:
                case Skill.BindWound:
                case Skill.Block:
                case Skill.Channeling:
                case Skill.Defense:
                case Skill.Disarm:
                case Skill.DisarmTraps:
                case Skill.Dodge:
                case Skill.DoubleAttack:
                case Skill.DualWield:
                case Skill.Forage:
                case Skill.Hide:
                case Skill.Meditate:
                case Skill.Mend:
                case Skill.Offense:
                case Skill.Parry:
                case Skill.Pick_lock:
                case Skill.Riposte:
                case Skill.SafeFall:
                case Skill.SenseHeading:
                case Skill.Sneak:
                case Skill.SpecializeAbjure:
                case Skill.SpecializeAlteration:
                case Skill.SpecializeConjuration:
                case Skill.SpecializeDivination:
                case Skill.SpecializeEvocation:
                case Skill.PickPockets:
                case Skill.Swimming:
                case Skill.Tracking:
                case Skill.Fishing:
                case Skill.MakePoison:
                case Skill.Tinkering:
                case Skill.Research:
                case Skill.Alchemy:
                case Skill.Baking:
                case Skill.Tailoring:
                case Skill.SenseTraps:
                case Skill.Blacksmithing:
                case Skill.Fletching:
                case Skill.Brewing:
                case Skill.AlcoholTolerance:
                case Skill.Begging:
                case Skill.JewelryMaking:
                case Skill.Pottery:
                case Skill.Intimidation:
                case Skill.Berserking:
                case Skill.Taunt:
                case Skill.Frenzy:
                case Skill.GenericTradeskill:
                    return DAMAGE_TYPE_UNKNOWN;
                default:
                    throw new ArgumentException("Unsupported value.", "skill");
            }
        }

        internal static ConLevel GetConsiderDificulty(int myLevel, int otherLevel)
        {
            int diff = otherLevel - myLevel;
            ConLevel conLevel;

            if (myLevel <= 8) {
                if (diff <= -4)
                    conLevel = ConLevel.Green;
                else
                    conLevel = ConLevel.Blue;
            }
            else if (myLevel <= 9) {
                if (diff <= -6)
                    conLevel = ConLevel.Green;
                else if (diff <= -4)
                    conLevel = ConLevel.LightBlue;
                else
                    conLevel = ConLevel.Blue;
            }
            else if (myLevel <= 13) {
                if (diff <= -7)
                    conLevel = ConLevel.Green;
                else if (diff <= -5)
                    conLevel = ConLevel.LightBlue;
                else
                    conLevel = ConLevel.Blue;
            }
            else if (myLevel <= 15) {
                if (diff <= -7)
                    conLevel = ConLevel.Green;
                else if (diff <= -5)
                    conLevel = ConLevel.LightBlue;
                else
                    conLevel = ConLevel.Blue;
            }
            else if (myLevel <= 17) {
                if (diff <= -8)
                    conLevel = ConLevel.Green;
                else if (diff <= -6)
                    conLevel = ConLevel.LightBlue;
                else
                    conLevel = ConLevel.Blue;
            }
            else if (myLevel <= 21) {
                if (diff <= -9)
                    conLevel = ConLevel.Green;
                else if (diff <= -7)
                    conLevel = ConLevel.LightBlue;
                else
                    conLevel = ConLevel.Blue;
            }
            else if (myLevel <= 25) {
                if (diff <= -10)
                    conLevel = ConLevel.Green;
                else if (diff <= -8)
                    conLevel = ConLevel.LightBlue;
                else
                    conLevel = ConLevel.Blue;
            }
            else if (myLevel <= 29) {
                if (diff <= -11)
                    conLevel = ConLevel.Green;
                else if (diff <= -9)
                    conLevel = ConLevel.LightBlue;
                else
                    conLevel = ConLevel.Blue;
            }
            else if (myLevel <= 31) {
                if (diff <= -12)
                    conLevel = ConLevel.Green;
                else if (diff <= -9)
                    conLevel = ConLevel.LightBlue;
                else
                    conLevel = ConLevel.Blue;
            }
            else if (myLevel <= 33) {
                if (diff <= -13)
                    conLevel = ConLevel.Green;
                else if (diff <= -10)
                    conLevel = ConLevel.LightBlue;
                else
                    conLevel = ConLevel.Blue;
            }
            else if (myLevel <= 37) {
                if (diff <= -14)
                    conLevel = ConLevel.Green;
                else if (diff <= -11)
                    conLevel = ConLevel.LightBlue;
                else
                    conLevel = ConLevel.Blue;
            }
            else if (myLevel <= 41) {
                if (diff <= -16)
                    conLevel = ConLevel.Green;
                else if (diff <= -12)
                    conLevel = ConLevel.LightBlue;
                else
                    conLevel = ConLevel.Blue;
            }
            else if (myLevel <= 45) {
                if (diff <= -17)
                    conLevel = ConLevel.Green;
                else if (diff <= -13)
                    conLevel = ConLevel.LightBlue;
                else
                    conLevel = ConLevel.Blue;
            }
            else if (myLevel <= 49) {
                if (diff <= -18)
                    conLevel = ConLevel.Green;
                else if (diff <= -14)
                    conLevel = ConLevel.LightBlue;
                else
                    conLevel = ConLevel.Blue;
            }
            else if (myLevel <= 53) {
                if (diff <= -19)
                    conLevel = ConLevel.Green;
                else if (diff <= -15)
                    conLevel = ConLevel.LightBlue;
                else
                    conLevel = ConLevel.Blue;
            }
            else if (myLevel <= 55) {
                if (diff <= -20)
                    conLevel = ConLevel.Green;
                else if (diff <= -15)
                    conLevel = ConLevel.LightBlue;
                else
                    conLevel = ConLevel.Blue;
            }
            else {
                if (diff <= -21)
                    conLevel = ConLevel.Green;
                else if (diff <= -16)
                    conLevel = ConLevel.LightBlue;
                else
                    conLevel = ConLevel.Blue;
            }
            return conLevel;
        }

        #region Properties
        internal virtual int AC
        {
            get { return _ac; }
            set { _ac = value; }
        }

        internal virtual short Attack
        {
            get { return _attack; }
            set { _attack = value; }
        }

        internal virtual short STR
        {
            get { return _str; }
            set { _str = value; }
        }

        internal virtual short STA
        {
            get { return _sta; }
            set { _sta = value; }
        }

        internal virtual short DEX
        {
            get { return _dex; }
            set { _dex = value; }
        }

        internal virtual short AGI
        {
            get { return _agi; }
            set { _agi = value; }
        }

        internal virtual short INT
        {
            get { return _int; }
            set { _int = value; }
        }

        internal virtual short WIS
        {
            get { return _wis; }
            set { _wis = value; }
        }

        internal virtual short CHA
        {
            get { return _cha; }
            set { _cha = value; }
        }

        internal virtual short MagicResist
        {
            get { return _magicResist; }
            set { _magicResist = value; }
        }

        internal virtual short ColdResist
        {
            get { return _coldResist; }
            set { _coldResist = value; }
        }

        internal virtual short FireResist
        {
            get { return _fireResist; }
            set { _fireResist = value; }
        }

        internal virtual short DeathResist
        {
            get { return _deathResist; }
            set { _deathResist = value; }
        }

        internal virtual short PoisonResist
        {
            get { return _poisonResist; }
            set { _poisonResist = value; }
        }

        internal virtual ushort Animation
        {
            get { return _animation; }
            set { _animation = value; }
        }

        internal ushort RunAnimSpeed
        {
            get { return _runAnimSpeed; }
            set
            {
                if (value != _runAnimSpeed)
                {
                    _runAnimSpeed = value;
                    _lastChange = DateTime.Now;
                }
            }
        }

        internal virtual short Race
        {
            get { return _race; }
            set { _race = value; }
        }

        internal virtual byte Class
        {
            get { return _class; }
            set { _class = value; }
        }

        internal virtual float Size
        {
            get { return _size; }
        }

        internal virtual BodyType BodyType
        {
            get { return _bodyType; }
        }

        internal virtual byte Gender
        {
            get { return _gender; }
            set { _gender = value; }
        }

        internal virtual byte Level
        {
            get { return _level; }
            set { _level = value; }
        }

        internal virtual uint Deity
        {
            get { return (uint)_deity; }
            set { _deity = value; }
        }

        internal virtual uint Platinum
        {
            get { return _platinum; }
            set { _platinum = value; }
        }

        internal virtual uint Gold
        {
            get { return _gold; }
            set { _gold = value; }
        }

        internal virtual uint Silver
        {
            get { return _silver; }
            set { _silver = value; }
        }

        internal virtual uint Copper
        {
            get { return _copper; }
            set { _copper = value; }
        }

        internal virtual float BaseRunSpeed
        {
            get { return _baseRunSpeed; }
        }

        internal virtual float RunSpeed
        {
            get { return GetMovementSpeed(1.0f, 0); }
        }

        internal virtual float WalkSpeed
        {
            get { return GetMovementSpeed(0.5f, 0); }
        }

        internal virtual bool Findable
        {
            get { return _findable; }
        }

        internal virtual byte Light
        {
            get { return _light; }
        }

        internal virtual byte HairColor
        {
            get { return _hairColor; }
        }

        internal virtual byte HairStyle
        {
            get { return _hairStyle; }
        }

        internal virtual byte BeardColor
        {
            get { return _beardColor; }
        }

        internal virtual byte Beard
        {
            get { return _beard; }
        }

        internal virtual byte EyeColor1
        {
            get { return _eyeColor1; }
        }

        internal virtual byte EyeColor2
        {
            get { return _eyeColor2; }
        }

        internal virtual byte Face
        {
            get { return _luclinFace; }
        }

        internal virtual byte Texture
        {
            get { return _texture; }
        }

        internal virtual byte HelmTexture
        {
            get { return _helmTexture; }
        }

        internal virtual bool IsAIControlled
        {
            get { return _aiControlled; }
            set { _aiControlled = value; }
        }

        internal bool IsRooted
        {
            get { return _rooted; }
            set { _rooted = value; }
        }

        internal bool IsEngaged
        {
            get { return _hateMgr.Count > 0; }
        }

        internal bool IsMoving
        {
            get { return _moving; }
            set
            {
                _moving = value;
                _deltaX = 0.0f;
                _deltaY = 0.0f;
                _deltaZ = 0.0f;
                _deltaHeading = 0;
            }
        }

        internal override float Heading
        {
            set
            {
                if (base.Heading != value) {
                    base.Heading = value;
                    _lastChange = DateTime.Now;
                }
            }
        }

        internal Stance Stance
        {
            get { return _stance; }
            set
            {
                if (_stance != value)
                {
                    _stance = value;
                    OnStanceChanged(new EventArgs());
                }
            }
        }

        internal virtual bool Invulnerable
        {
            get { return _invulnerable; }
            set { _invulnerable = value; }
        }

        internal bool Invisible
        {
            get { return _invis; }
            set { _invis = value; }
        }

        internal bool InvisibleToUndead
        {
            get { return _invisToUndead; }
            set { _invisToUndead = value; }
        }

        internal bool InvisibleToAnimals
        {
            get { return _invisToAnimals; }
            set { _invisToAnimals = value; }
        }

        internal bool Sneaking
        {
            get { return _sneaking; }
            set
            {
                if (_sneaking != value) {
                    _sneaking = value;
                    OnStanceChanged(new StanceChangedEventArgs(SpawnAppearanceType.Sneak, value ? (short)1 : (short)0));
                }
            }
        }

        internal bool Hidden
        {
            get { return _hidden; }
            set { _hidden = value; }
        }

        internal bool ImprovedHidden
        {
            get { return _improvedHidden; }
            set { _improvedHidden = value; }
        }

        internal virtual int Haste
        {
            get { return _haste; }
            set { _haste = value; }
        }

        internal virtual bool Dead
        {
            get { return _dead; }
            set { _dead = value; }
        }

        internal virtual int HP
        {
            get { return _curHP; }
            set
            {
                int hpToUse = Math.Min(value, _maxHP);
                if (_curHP != hpToUse) {
                    _curHP = hpToUse;

                    if (_curHP <= 0)
                        _dead = true;

                    OnHPUpdated(new EventArgs());
                }
            }
        }

        internal int MaxHP
        {
            get { return _maxHP; }
            set { _maxHP = value; }
        }

        internal float HPRatio
        {
            get { return this.MaxHP == 0 ? 0.0f : this.HP / (float)this.MaxHP; }
        }

        internal virtual int Mana
        {
            get { return _curMana; }
            set
            {
                value = Math.Max(value, 0); // zero is the min for mana
                value = Math.Min(value, _maxMana);  // cap

                if (value != _curMana)
                    _curMana = value;
            }
        }

        internal int MaxMana
        {
            get { return _maxMana; }
        }

        protected internal virtual Mob TargetMob
        {
            get { return _target; }
            set
            {
                if (_target != value) {
                    if (_target != null)
                        _target.Target(false);  // de-target the old target
                    
                    _target = value;

                    if (_target != null)
                        _target.Target(true);   // target the new target

                    // TODO: HoTT stuff
                }
            }
        }

        protected internal virtual Mob SpellTargetMob
        {
            get { return _spellTarget; }
            set { _spellTarget = value; }
        }

        /// <summary>Returns true if another entity has this entity targeted.</summary>
        protected internal bool IsTargeted
        {
            get { return _targetCount > 0; }
        }

        internal bool SeeInvis
        {
            get { return _seeInvis; }
            set { _seeInvis = value; }
        }

        internal bool SeeInvisToUndead
        {
            get { return _seeInvisToUndead; }
            set { _seeInvisToUndead = value; }
        }

        internal bool SeeInvisToAnimals
        {
            get { return _seeInvisToAnimals; }
            set { _seeInvisToAnimals = value; }
        }

        internal bool SeeHide
        {
            get { return _seeHide; }
            set { _seeHide = value; }
        }

        internal bool SeeImprovedHide
        {
            get { return _seeImprovedHide; }
            set { _seeImprovedHide = value; }
        }

        internal override bool IsAttackable
        {
            get
            {
                if (this.Dead || this.HP <= 0)
                    return false;

                return true;
            }
        }

        internal HateManager HateMgr
        {
            get { return _hateMgr; }
        }

        internal bool IsLootable
        {
            get
            {
                return (this.Class != (byte)CharClasses.Merchant && this.Class != (byte)CharClasses.AdventureMerchant) ;
            }
        }

        internal bool Sitting
        {
            get { return _stance == Stance.Sitting; }
        }

        internal bool Wounded
        {
            get { return this.HP < this.MaxHP; }
        }
        #endregion

        internal override void Save()
        {
            //throw new NotImplementedException();
        }
        
        protected internal virtual void Init()
        {
            // TODO: load buffs, procs, shielders?

            // start various timers
            _ticTimer = new SimpleTimer(TIC_INTERVAL);
            _attackTimer = new SimpleTimer(0);
            _dwAttackTimer = new SimpleTimer(0);
            _rangedAttackTimer = new SimpleTimer(0);
            _stunTimer = new SimpleTimer(0);
            _manaUpdateTimer = new SimpleTimer(MANA_UPDATE_INTERVAL);

            // TODO: pets

            _hateMgr = new HateManager(this);
        }

        /// <summary>Determines whether a caster is a Wis caster, Int caster of neither.</summary>
        protected CasterClass GetCasterClass()
        {
            CasterClass classInd;

            switch ((CharClasses)_class)
            {
                case CharClasses.Cleric:
                case CharClasses.Paladin:
                case CharClasses.Ranger:
                case CharClasses.Druid:
                case CharClasses.Shaman:
                case CharClasses.BeastLord:
                case CharClasses.ClericGM:
                case CharClasses.PaladinGM:
                case CharClasses.RangerGM:
                case CharClasses.BeastlordGM:
                case CharClasses.DruidGM:
                case CharClasses.ShamanGM:
                    classInd = CasterClass.Wis;
                    break;
                case CharClasses.ShadowKnight:
                case CharClasses.Bard:
                case CharClasses.Necromancer:
                case CharClasses.Wizard:
                case CharClasses.Magician:
                case CharClasses.Enchanter:
                case CharClasses.BardGM:
                case CharClasses.ShadowKnightGM:
                case CharClasses.NecromancerGM:
                case CharClasses.WizardGM:
                case CharClasses.MagicianGM:
                case CharClasses.EnchanterGM:
                    classInd = CasterClass.Int;
                    break;
                default:
                    classInd = CasterClass.None;
                    break;
            }

            return classInd;
        }

        protected int GetClassLevelFactor()
        {
            int multiplier = 0;
            switch ((CharClasses)this.Class) {
                case CharClasses.Warrior:
                    if (this.Level < 20)
                        multiplier = 220;
                    else if (this.Level < 30)
                        multiplier = 230;
                    else if (this.Level < 40)
                        multiplier = 250;
                    else if (this.Level < 53)
                        multiplier = 270;
                    else if (this.Level < 57)
                        multiplier = 280;
                    else if (this.Level < 60)
                        multiplier = 290;
                    else if (this.Level < 70)
                        multiplier = 300;
                    else
                        multiplier = 311;
                    break;
                case CharClasses.Druid:
                case CharClasses.Cleric:
                case CharClasses.Shaman:
                    if (this.Level < 70)
                        multiplier = 150;
                    else
                        multiplier = 157;
                    break;
                case CharClasses.Berserker:
                case CharClasses.Paladin:
                case CharClasses.ShadowKnight:
                    if (this.Level < 35)
                        multiplier = 210;
                    else if (this.Level < 45)
                        multiplier = 220;
                    else if (this.Level < 51)
                        multiplier = 230;
                    else if (this.Level < 56)
                        multiplier = 240;
                    else if (this.Level < 60)
                        multiplier = 250;
                    else if (this.Level < 68)
                        multiplier = 260;
                    else
                        multiplier = 270;
                    break;
                case CharClasses.Monk:
                case CharClasses.Bard:
                case CharClasses.Rogue:
                case CharClasses.BeastLord:
                    if (this.Level < 51)
                        multiplier = 180;
                    else if (this.Level < 58)
                        multiplier = 190;
                    else if (this.Level < 70)
                        multiplier = 200;
                    else
                        multiplier = 210;
                    break;
                case CharClasses.Ranger:
                    if (this.Level < 58)
                        multiplier = 200;
                    else if (this.Level < 70)
                        multiplier = 210;
                    else
                        multiplier = 220;
                    break;
                case CharClasses.Magician:
                case CharClasses.Wizard:
                case CharClasses.Necromancer:
                case CharClasses.Enchanter:
                    if (this.Level < 70)
                        multiplier = 120;
                    else
                        multiplier = 127;
                    break;
                default:
                    if (this.Level < 35)
                        multiplier = 210;
                    else if (this.Level < 45)
                        multiplier = 220;
                    else if (this.Level < 51)
                        multiplier = 230;
                    else if (this.Level < 56)
                        multiplier = 240;
                    else if (this.Level < 60)
                        multiplier = 250;
                    else
                        multiplier = 260;
                    break;
            }
            return multiplier;
        }

        internal virtual bool HasSpecialDefense(SpecialDefenses specDef)
        {
            return false;   // TODO: implement
        }

        internal virtual bool HasSpecialAttack(SpecialAttacks specAtt)
        {
            return false;   // TODO: implement
        }

        internal virtual Packets.Spawn GetSpawn()
        {
            Packets.Spawn s = new Packets.Spawn();
            s.Init();
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(this.Name), 0, s.Name, 0, this.Name.Length);
            if (!string.IsNullOrEmpty(this.Surname))
                Buffer.BlockCopy(Encoding.ASCII.GetBytes(this.Surname), 0, s.Surname, 0, this.Surname.Length);
            s.Heading = this.Heading;
            s.XPos = this.X;
            s.YPos = this.Y;
            s.ZPos = this.Z;
            s.SpawnId = (uint)this.ID;
            s.CurHpPct = (byte)(this.HPRatio * 100);
            s.MaxHpCategory = 100;
            s.Race = (uint)this.Race;
            s.RunSpeed = this.BaseRunSpeed;
            s.WalkSpeed = this.BaseRunSpeed / 2;
            s.Class = (byte)this.Class;
            s.Gender = (byte)this.Gender;
            s.Level = (byte)this.Level;
            s.Deity = (ushort)this.Deity;
            s.Findable = this.Findable ? (byte)1 : (byte)0;
            s.Light = this.Light;
            s.Invis = (_invis || _hidden) ? (byte)1 : (byte)0;
            //s.NPC = false ? (byte)1 : (byte)0;
            // TODO: pet owner id
            s.HairColor = this.HairColor > 0 ? this.HairColor : (byte)0xFF;
            s.HairStyle = this.HairStyle > 0 ? this.HairStyle : (byte)0xFF;
            if (this.Gender == 1)
                s.HairStyle = (byte)(s.HairStyle == 0xFF ? 0 : s.HairStyle);
            s.Beard = this.Beard > 0 ? this.Beard : (byte)0xFF;
            s.BeardColor = this.BeardColor > 0 ? this.BeardColor : (byte)0xFF;
            s.EyeColor1 = this.EyeColor1 > 0 ? this.EyeColor1 : (byte)0xFF;
            s.EyeColor2 = this.EyeColor2 > 0 ? this.EyeColor2 : (byte)0xFF;
            s.Face = this.Face;
            s.EquipChestMountColor = this.Texture;
            s.Helm = (this.HelmTexture != 0xFF) ? this.HelmTexture : (byte)0;
            s.GuildRank = 0xFF;
            s.Size = this.Size;
            s.BodyType = (byte)this.BodyType;

            return s;
        }

        internal virtual PlayerPositionUpdateServer GetSpawnUpdate()
        {
            PlayerPositionUpdateServer ppus = new PlayerPositionUpdateServer();
            ppus.SpawnId = (ushort)this.ID;
            ppus.XPos = this.X;
            ppus.YPos = this.Y;
            ppus.ZPos = this.Z;
            ppus.DeltaX = _deltaX;
            ppus.DeltaY = _deltaY;
            ppus.DeltaZ = _deltaZ;
            ppus.Heading = this.Heading;
            ppus.DeltaHeading = _deltaHeading;
            ppus.Padding0002 = 0;
            ppus.Padding0006 = 7;
            ppus.Padding0014 = 0x7f;
            ppus.Padding0018 = 0x5df27;
            ppus.Animation = _runAnimSpeed;

            return ppus;
        }

        internal virtual PlayerPositionUpdateServer GetSpawnUpdateNoDelta()
        {
            PlayerPositionUpdateServer ppus = new PlayerPositionUpdateServer();
            ppus.SpawnId = (ushort)this.ID;
            ppus.XPos = this.X;
            ppus.YPos = this.Y;
            ppus.ZPos = this.Z;
            ppus.DeltaX = 0;
            ppus.DeltaY = 0;
            ppus.DeltaZ = 0;
            ppus.Heading = this.Heading;
            ppus.Animation = 0;
            ppus.DeltaHeading = 0;
            ppus.Padding0002 = 0;
            ppus.Padding0006 = 7;
            ppus.Padding0014 = 0x7f;
            ppus.Padding0018 = 0x5df27;

            return ppus;
        }

        internal HPUpdateRatio GetHPUpdateRatio()
        {
            return new HPUpdateRatio()
            {
                SpawnID = (short)this.ID,
                HPRatio = (byte)(this.MaxHP == 0 ? 0 : (this.HPRatio * 100))
            };
        }

        internal HPUpdateExact GetHPUpdateExact()
        {
            return new HPUpdateExact()
            {
                CurrentHP = (uint)this.HP,
                MaxHP = this.MaxHP,
                SpawnID = (short)this.ID
            };
        }

        protected virtual int GetEquipmentMaterial(EquipableType et)
        {
            throw new NotImplementedException();
        }

        protected virtual uint GetEquipmentColor(EquipableType et)
        {
            throw new NotImplementedException();
        }

        internal virtual bool IsInZone()
        {
            return true;
        }

        internal override bool Process()
        {
            throw new NotImplementedException();
        }

        /// <summary>Performs a melee attack against the specified target mob.</summary>
        /// <param name="target">Mob being attacked.</param>
        /// <param name="isPrimaryHand">Is this the primary hand weapon as opposed to a dual wield attack.</param>
        /// <param name="riposte">Is this a riposte attack?</param>
        /// <returns>True if an attack succeeded, else false if the attack didn't succeed (for whatever reason).</returns>
        internal virtual bool MeleeAttack(Mob target, bool isPrimaryHand, bool riposte)
        {
            if (target == null)
                throw new ArgumentNullException("target");

            return true;
            // Anything else should be handled in the specific entity type
        }

        protected virtual bool IsAbleToAttack()
        {
            throw new NotImplementedException();
        }

        protected virtual bool IsAbleToAttack(Mob target, bool spellAttack)
        {
            if (target == null)
                throw new ArgumentNullException("other");

            return target.IsAttackable;
        }

        protected void GetSkillAndAnimForAttack(Item weapon, bool isPrimaryHand, out Skill skill, out AttackAnimation anim)
        {
            if (weapon != null && weapon.ItemClass == Item.ITEM_CLASS_COMMON) {
                switch ((ItemType)weapon.ItemType) {
                    case ItemType.OneHandSlash:
                        skill = Skill.OneHandSlashing;
                        anim = AttackAnimation.OneHandWeapon;
                        break;
                    case ItemType.TwoHandSlash:
                        skill = Skill.TwoHandSlashing;
                        anim = AttackAnimation.TwoHandSlashing;
                        break;
                    case ItemType.Pierce:
                        skill = Skill.Piercing;
                        anim = AttackAnimation.Piercing;
                        break;
                    case ItemType.OneHandBash:
                        skill = Skill.OneHandBlunt;
                        anim = AttackAnimation.OneHandWeapon;
                        break;
                    case ItemType.TwoHandBash:
                        skill = Skill.TwoHandBlunt;
                        anim = AttackAnimation.TwoHandWeapon;
                        break;
                    case ItemType.TwoHandPierce:
                        skill = Skill.Piercing;
                        anim = AttackAnimation.TwoHandWeapon;
                        break;
                    default:
                        skill = Skill.HandToHand;
                        anim = AttackAnimation.HandToHand;
                        break;
                }
            }
            else {
                skill = Skill.HandToHand;
                anim = AttackAnimation.HandToHand;
            }

            if (!isPrimaryHand)
                anim = AttackAnimation.DualWield;
        }

        /// <summary>Gets the damage for the specified target mob with the specified weapon.</summary>
        /// <param name="target">Mob that is being attacked.</param>
        /// <param name="weapon">Null indicates fists.</param>
        /// <returns>Amount of damage delivered.</returns>
        protected int GetWeaponDamage(Mob target, Item weapon)
        {
            int damage = 0, baneDamage = 0, elemDamage = 0;

            if (target.Invulnerable || target.HasSpecialDefense(SpecialDefenses.ImmuneMelee))
                return 0;

            if (target.HasSpecialDefense(SpecialDefenses.ImmuneMeleeNonmagical)) {
                if (weapon != null) {
                    if (weapon.IsMagic) {   // TODO: look for magic weapon buff as well
                        if (this is ZonePlayer && this.Level < weapon.RecLevel)
                            damage = ZonePlayer.CalcRecommendedLevelBonus(this.Level, weapon.RecLevel, weapon.Damage);
                        else
                            damage = weapon.Damage;

                        // TODO: accumulate weapon augmentation damage bonuses

                        damage = Math.Min(damage, 1);
                    }
                    else
                        return 0;   // Weapon not magical, but one is needed
                }
                else {
                    // TODO: add check below for pet ability to hit
                    if ((this.Class == (byte)CharClasses.Monk || this.Class == (byte)CharClasses.BeastLord) && this.Level >= 30)
                        damage = GetMonkHandToHandDamage();
                    else if (this.HasSpecialAttack(SpecialAttacks.Magical))
                        damage = 1;
                    else
                        return 0;   // No weapon and can't harm with hand to hand
                }
            }
            else {
                if (weapon != null) {
                    if (this is ZonePlayer && this.Level < weapon.RecLevel)
                        damage = ZonePlayer.CalcRecommendedLevelBonus(this.Level, weapon.RecLevel, weapon.Damage);
                    else
                        damage = weapon.Damage;

                    // TODO: accumulate weapon augmentation damage bonuses

                    damage = Math.Max(damage, 1);   // Minimum weapon damage of 1
                }
                else {
                    if (this.Class == (byte)CharClasses.Monk || this.Class == (byte)CharClasses.BeastLord)
                        damage = GetMonkHandToHandDamage();
                    else
                        damage = UNARMED_DAMAGE;
                }
            }

            // TODO: elemental damage (Don't add resist checks - just calculating POTENTIAL damage here)

            // TODO: bane damage

            return damage + baneDamage + elemDamage;
        }

        internal bool TryToHit(Mob attacker, Skill attackSkill, bool isPrimaryHand)
        {
            float hitChance = WorldServer.ServerConfig.BaseHitChance * 100;
            if (attacker is NpcMob) {
                hitChance += WorldServer.ServerConfig.NPCBonusHitChance;
                hitChance += (hitChance * ((NpcMob)attacker).Accuracy / 1000.0f);   // Accuracy can be set in NPC table over 1000 to give the NPC a greater hit chance
            }

            //_log.DebugFormat("Base hit chance with NPC bonus & NPC Accuracy: {0}", hitChance);

            int levelDiff = attacker.Level - this.Level;
            float range = attacker.Level / 4.0f + 3;    // This is changed from orig emu... I chose the attacker to base from for a tighter range

            if (levelDiff < 0) {
                // We are of higher level than the attacker
                if (levelDiff >= -range)
                    hitChance += levelDiff / range * WorldServer.ServerConfig.HitFallOffMinor;                      // 5
                else if (levelDiff >= -(range + 3.0f)) {
                    hitChance -= WorldServer.ServerConfig.HitFallOffMinor;
                    hitChance += ((levelDiff + range) / 3.0f) * WorldServer.ServerConfig.HitFallOffModerate;        // 7
                }
                else {
                    hitChance -= WorldServer.ServerConfig.HitFallOffMinor + WorldServer.ServerConfig.HitFallOffModerate;
                    hitChance += ((levelDiff + range + 3.0f) / 12.0f) * WorldServer.ServerConfig.HitFallOffMajor;   // Basically no fucking chance
                }
            }
            else
                hitChance += levelDiff * WorldServer.ServerConfig.HitBonusPerLevel;

            //_log.DebugFormat("Hit chance after level diff adj: {0} (attacker level: {1} defender level: {2} diff: {3})", hitChance, attacker.Level, this.Level, levelDiff);

            hitChance -= this.AGI * 0.05f;     // Adjust for agility TODO: const or db

            //_log.DebugFormat("Hit chance after adj for AGI: {0} (AGI: {1})", hitChance, this.AGI);

            if (attacker is ZonePlayer) {
                // TODO: weapon skill & defense skill falloffs?

                // TODO: handle client specific AA bonuses
            }

            // TODO: Add spell and item bonuses relating to ATTACKER melee skill and hit chance skill (Factor this into virtual GetChanceToHitBonuses())

            // TODO: subtract off the defender's avoidance (looks like spell and item bonus related)

            // TODO: defender's AA abilities which mitigate to hit chances

            if (hitChance < 1000.0f) {      // As long as a discipline isn't involved... TODO: what about guaranteed riposte vs. guaranteed hit?
                hitChance = Math.Max(hitChance, TOHIT_MIN);     // Chance to hit max: 95% min: 5%
                hitChance = Math.Min(hitChance, TOHIT_MAX);
            }

            Random rand = new Random();
            int toHitRoll = rand.Next(0, 100);

            //_log.DebugFormat("Final hit chance: {0} Roll: {1})", hitChance, toHitRoll);
            return toHitRoll <= hitChance;
        }

        /// <summary>Tries to avoid an attack that has already been determined to otherwise land a hit.</summary>
        /// <param name="attacker"></param>
        /// <param name="damage"></param>
        /// <returns>Damage amount after avoidance methods are checked. If successfully avoided the damage will be one of the AttackAvoidanceType values.</returns>
        internal int TryToAvoidDamage(Mob attacker, int damage)
        {
            float bonus = 0.0f;
            uint skill = 0;
            float riposteChance = 0.0f, blockChance = 0.0f, parryChance = 0.0f, dodgeChance = 0.0f;

            // TODO: determine guaranteed hits
            bool autoHit = false;

            // If this mob is enraged, it auto-ripostes (even guaranteed hits apparently)
            if (this is NpcMob && ((NpcMob)this).Enraged && !attacker.IsBehindMob(this)) {
                damage = (int)AttackAvoidanceType.Riposte;
            }

            // Try to Riposte
            if (damage > 0 && this.GetSkillLevel(Skill.Riposte) > 0 && !attacker.IsBehindMob(this)) {
                skill = this.GetSkillLevel(Skill.Riposte);

                if (this is ZonePlayer)
                    ((ZonePlayer)this).CheckForSkillUp(Skill.Riposte, attacker, 0);

                if (!autoHit) { // guaranteed hit discipline trumps riposte
                    bonus = 2.0f + skill / 60.0f + (this.DEX / 200);
                    // TODO: add in riposte related item and spell bonuses
                    riposteChance = bonus;
                }
            }

            // Try to Block
            bool blockFromRear = false;     // TODO: handle Hightened Awareness AA

            if (damage > 0 && this.GetSkillLevel(Skill.Block) > 0 && (!attacker.IsBehindMob(this) || blockFromRear)) {
                skill = this.GetSkillLevel(Skill.Block);

                if (this is ZonePlayer)
                    ((ZonePlayer)this).CheckForSkillUp(Skill.Block, attacker, 0);

                if (!autoHit) {
                    bonus = 2.0f + skill / 35.0f + (this.DEX / 200);
                    blockChance = riposteChance + bonus;
                }

                // TODO: handle Shield Block AA
            }

            // TODO: Try to Parry

            // TODO: Try to Dodge

            Random rand = new Random();
            int roll = rand.Next(1, 101);  // Roll between 1-100
            if (roll <= (riposteChance + blockChance + parryChance + dodgeChance)) {
                // Something worked, now which one was it...
                if (roll <= riposteChance)
                    damage = (int)AttackAvoidanceType.Riposte;
                else if (roll < blockChance)
                    damage = (int)AttackAvoidanceType.Block;
                else if (roll < parryChance)
                    damage = (int)AttackAvoidanceType.Parry;
                else if (roll < dodgeChance)
                    damage = (int)AttackAvoidanceType.Dodge;
            }

            return damage;
        }

        /// <summary>Attempts to mitigate the specified damage amount.</summary>
        /// <param name="attacker"></param>
        /// <param name="damage"></param>
        /// <returns>Resulting amount of damage after all mitigation has been applied.</returns>
        internal int TryToMitigateDamage(Mob attacker, int damage, int minDmg)
        {
            if (damage <= 0)
                throw new ArgumentOutOfRangeException("damage", "No need to mitigate damage of zero or less.");

            int totalMit = 0;
            
            // TODO: Accumulate bonuses from AA abilities related to damage mitigation

            // AC mitigation    TODO: examine these calcs and see if they are wacky or not
            int attackRating = 0;
            int defenseRating = this.AC;
            defenseRating += 125 + (totalMit * defenseRating / 100);
            int acEq100 = 125;

            if (this.Level < 20)
                acEq100 += 15 * this.Level;
            else if (this.Level < 50)
                acEq100 += (285 + ((this.Level - 19) * 30));
            else if (this.Level < 60)
                acEq100 += (1185 + ((this.Level - 49) * 60));
            else if (this.Level < 70)
                acEq100 += (1785 + ((this.Level - 59) * 90));
            else
                acEq100 += (2325 + ((this.Level - 69) * 125));

            if (attacker is ZonePlayer) // TODO: factor below into an AttackRating property?
                attackRating = 10 + attacker.Attack + (int)(attacker.STR + attacker.GetSkillLevel(Skill.Offense) * 9 / 10);
            else
                attackRating = 10 + attacker.Attack + (attacker.STR * 9 / 10);

            float combatRating = defenseRating - attackRating;
            combatRating = 100 * combatRating / acEq100;
            float d1Chance = 6.0f + ((combatRating * 0.39f) / 3);
            float d2d19Chance = 48.0f + (((combatRating * 0.39f) / 3) * 2);

            d1Chance = Math.Max(d1Chance, 1.0f);    // min chance of 1.0
            d2d19Chance = Math.Max(d2d19Chance, 5.0f);  // min chance of 5.0

            Random rand = new Random();
            double roll = rand.NextDouble() * 100;

            int interval = 0;
            if (roll <= d1Chance)
                interval = 1;
            else if (roll <= (d2d19Chance + d1Chance))
                interval = 1 + (int)((((roll - d1Chance) / d2d19Chance) * 18) + 1);
            else
                interval = 20;

            // TODO: the fuck is this shit?
            int db = minDmg;
            double di = ((double)(damage - minDmg) / 19);
            damage = db + (int)(di * (interval - 1));

            // TODO: reduce damage further from shielding item and AA which are based on the min dmg
            // TODO: reduce damage further from spells which are based upon pure damage (not min dmg)

            return Math.Max(damage, minDmg);    // Can't mitigate below min dmg
        }

        /// <summary>Gets the actual movement speed of the entity after all modifiers.  Should be called via down chain entities with
        /// appropriate bonuses applied.</summary>
        /// <param name="speedMod">1.0 for run speed, 0.5 for walk?</param>
        /// <param name="moveBonus">applied bonuses?</param>
        internal virtual float GetMovementSpeed(float speedMod, int moveBonus)
        {
            if (_rooted)
                return 0.0f;

            // TODO: accumulate bonuses
            
            moveBonus = Math.Max(moveBonus, -85);   // movement floor of -85 for very very slow movement

            if (moveBonus != 0)
                speedMod += (float)moveBonus / 100.0f;

            speedMod = Math.Max(speedMod, 0.0001f);

            return this.BaseRunSpeed * speedMod;
        }

        internal virtual uint GetSkillLevel(Skill skill)
        {
            throw new NotImplementedException();
        }

        internal void SendTo(float x, float y, float z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z + 0.1f;

            // TODO: fix up z
        }

        protected bool CalculateNewPosition(float x, float y, float z, float speed, bool checkZ)
        {
            if (this.ID == 0)
            {
                _log.Warn("For some reason this entity's ID is zero in CalcNewPos.");
                return true;    // Will this ever happen?
            }

            if (this.X == x && this.Y == y) // is mob at target coords?
            {
                _log.Debug("Mob determined to be standing or jumping - no update being sent.");

                if (this.Z != z)    // is the mob just jumping?
                {
                    this.Z = z;
                    return true;
                }
                else
                    return false;
            }

            if (_tarIdx < 20 && _tarX == x && _tarY == y)
            {
                this.X += _tarXVec * _tarVector;
                this.Y += _tarYVec * _tarVector;
                this.Z += _tarZVec * _tarVector;

                //_log.DebugFormat("Calculating new postion to {0},{1},{2}, old vector {3},{4},{5}", x, y, z, _tarXVec , _tarYVec, _tarZVec);

                if (checkZ) {
                    // TODO: fix up Z
                }

                _tarIdx++;
                return true;
            }

            if (_tarIdx > 50)
                _tarIdx--;
            else
                _tarIdx = 0;

            _tarX = x;          // saved member copies of target coords
            _tarY = y;
            _tarZ = z;
            float nx = this.X;  // saved local copies of current coords
            float ny = this.Y;
            float nz = this.Z;
            _tarXVec = x - nx;  // differences between target and current coords
            _tarYVec = y - ny;
            _tarZVec = z - nz;

            this.RunAnimSpeed = (ushort)(speed * NPC_RUNANIM_RATIO);
            speed *= NPC_SPEED_MULTIPLIER;

            float mag = (float)Math.Sqrt(_tarXVec * _tarXVec + _tarYVec * _tarYVec + _tarZVec * _tarZVec);
            _tarVector = speed / mag;

            int numSteps = (int)(mag * 20 / speed) + 1;
            //_log.DebugFormat("{0} has {1} steps left until waypoint hit.", this.Name, numSteps);

            if (numSteps < 20)
            {
                if (numSteps > 1)
                {
                    _tarVector = 1.0f;
                    _tarXVec /= numSteps;
                    _tarYVec /= numSteps;
                    _tarZVec /= numSteps;
                    this.X += _tarXVec;
                    this.Y += _tarYVec;
                    this.Z += _tarZVec;
                    _tarIdx = (byte)(22 - numSteps);
                    this.Heading = CalculateHeadingToTarget(x, y);
                }
                else
                {   // only one step left to get there.. jump
                    this.X = x;
                    this.Y = y;
                    this.Z = z;
                }
            }
            else
            {
                _tarVector /= 20;
                this.X += _tarXVec * _tarVector;
                this.Y += _tarYVec * _tarVector;
                this.Z += _tarZVec * _tarVector;
                this.Heading = CalculateHeadingToTarget(x, y);
            }

            // TODO: fix up z

            this.IsMoving = true;
            _moved = true;
            _deltaX = this.X - nx;
            _deltaY = this.Y - ny;
            _deltaZ = this.Z - nz;
            _deltaHeading = 0;

            OnPositionUpdate(new PosUpdateEventArgs(false, true));  // Uses deltas - mobs walk instead of warp
            this.Stance = Stance.Standing;
            _lastChange = DateTime.Now;
            return true;
        }

        protected float CalculateHeadingToTarget(float x, float y)
        {
            float angle;

            if (x - this.X > 0)
                angle = (float)(-90 + Math.Atan((float)(y - this.Y) / (float)(x - this.X)) * 180 / Math.PI);
            else if (x - this.X < 0)
                angle = (float)(90 + Math.Atan((float)(y - this.Y) / (float)(x - this.X)) * 180 / Math.PI);
            else
            {
                if (y - this.Y > 0)
                    angle = 0;
                else
                    angle = 180;
            }
            if (angle < 0)
                angle += 360;
            if (angle > 360)
                angle -= 360;
            return (256 * (360 - angle) / 360.0f);
        }

        /// <summary>Makes this mob turn to face the specified mob.</summary>
        /// <param name="mobToFace">The mob that should be faced. If null, faces the currently targeted mob.</param>
        protected void FaceMob(Mob other)
        {
            Mob mobToFace = other == null ? this.TargetMob : other;

            if (mobToFace == null) {
                _log.Error("Unable to face the mob - null was specified and we don't have a mob targeted.");
                return;
            }

            float oldHeading = this.Heading;
            float newHeading = CalculateHeadingToTarget(mobToFace.X, mobToFace.Y);
            if (oldHeading != newHeading) {
                this.Heading = newHeading;
                if (this.IsMoving)
                    OnPositionUpdate(new PosUpdateEventArgs(true, true));
                else
                    OnPositionUpdate(new PosUpdateEventArgs(true, false));
            }
        }

        protected virtual void CalcStatModifiers()
        {
            CalcSpellBonuses();
            CalcMaxHP();
            CalcMaxMana();

            // TODO: add a rooted check?
        }

        protected virtual int CalcMaxHP()
        {
            _maxHP = _baseHP;   // TODO: augment with item & spell bonuses
            return _maxHP;
        }

        protected virtual int CalcMaxMana()
        {
            int manaBonuses = 0;    // TODO: add up mana bonuses from spells & items

            switch (GetCasterClass()) {
                case CasterClass.Wis:
                    _maxMana = (((this.WIS / 2) + 1) * this.Level) + manaBonuses;
                    break;
                case CasterClass.Int:
                    _maxMana = (((this.INT / 2) + 1) * this.Level) + manaBonuses;
                    break;
                case CasterClass.None:
                default:
                    _maxMana = 0;
                    break;
            }

            return _maxMana;
        }

        protected void CalcSpellBonuses()
        {
            // TODO: implement
        }

        /// <summary>Sets the appropriate attack timer up with appropriate interval and starts the timer.</summary>
        protected void SetAttackTimer()
        {
            float hasteRate = 1.0F; // TODO: handle haste

            SimpleTimer tmr = null;
            Item item = null, priWeapon = null;

            for (int i = (int)InventorySlot.Range; i < (int)InventorySlot.Secondary; i++) {
                if (i == (int)InventorySlot.Primary)
                    tmr = _attackTimer;
                else if (i == (int)InventorySlot.Secondary)
                    tmr = _dwAttackTimer;
                else if (i == (int)InventorySlot.Range)
                    tmr = _rangedAttackTimer;
                else
                    continue;   // not worried about hands

                if (this is ZonePlayer) // TODO: allow pets & bots as well?
                    item = GetEquipment(InventoryManager.GetEquipableType((InventorySlot)i));   // Get the weapon item

                // Handle special offhand stuff
                if (i == (int)InventorySlot.Secondary) {
                    if (priWeapon != null) {
                        // If we have a 2H weapon in our main hand, no dual
                        if (priWeapon.ItemClass == Item.ITEM_CLASS_COMMON &&
                            (priWeapon.ItemType == (byte)ItemType.TwoHandSlash 
                            || priWeapon.ItemType == (byte)ItemType.TwoHandBash
                            || priWeapon.ItemType == (byte)ItemType.TwoHandPierce)) {
                            _dwAttackTimer.Enabled = false;
                            continue;
                        }
                    }

                    if (!this.CanDualWield()) {
                        _dwAttackTimer.Enabled = false;
                        continue;
                    }
                }

                // Have a valid weapon?
                if (item != null) {
                    if (item.ItemClass != Item.ITEM_CLASS_COMMON || item.Damage == 0 || item.Delay == 0)
                        item = null;    // not really, not a weapon
                    else if ((item.ItemType > (byte)ItemType.Throwing) && (item.ItemType != (byte)ItemType.HandToHand) && (item.ItemType != (byte)ItemType.TwoHandPierce))
                        item = null;    // not really, not a weapon
                }

                int speed = 0;
                if (item == null) { // Have a weapon?
                    // Nope
                    if (this.Class == (byte)CharClasses.Monk || this.Class == (byte)CharClasses.BeastLord) {
                        // monks use special delay based upon level or epic
                        speed = (int)(GetMonkHandToHandDelay() * (100.0f + _attackSpeed) * hasteRate);
                        speed = Math.Max(speed, 800);
                    }
                    else
                        speed = (int)(UNARMED_DELAY * (100.0f + _attackSpeed) * hasteRate);    // Not a monk or bl - using fist at regular delay of 2/36
                }
                else {
                    // Have a weapon, use its delay
                    //_log.DebugFormat("Using a weapon: {0} ({1}/{2})", item.Name, item.Damage, item.Delay);
                    speed = (int)(item.Delay * (100.0f + _attackSpeed) * hasteRate);
                    speed = Math.Max(speed, 800);

                    if (item.ItemType == (byte)ItemType.Bow || item.ItemType == (byte)ItemType.Throwing) {
                        // TODO: quiver haste?
                        speed *= 97 / 100;
                    }
                }

                tmr.Start(speed, true);
                if (i == (int)InventorySlot.Primary)
                    priWeapon = item;
            }

            _log.DebugFormat("{3}'s Attack timer(s) started. {0} {1} {2}", _attackTimer.Enabled ? "Attack:" + _attackTimer.Interval : "",
                _dwAttackTimer.Enabled ? "Dual-Wield:" + _dwAttackTimer.Interval : "", _rangedAttackTimer.Enabled ? "Ranged:" + _rangedAttackTimer.Interval : "",
                this.Name);
        }

        protected virtual Item GetEquipment(EquipableType equipSlot)
        {
            throw new NotImplementedException();
        }

        protected virtual Item GetEquipment(InventorySlot invSlot)
        {
            throw new NotImplementedException();
        }

        protected virtual bool CanDualWield()
        {
            throw new NotImplementedException();
        }

        /// <summary>Determines a monk's or beastlord's fist delay.</summary>
        /// <remarks>Data taken from www.monkly-business.com</remarks>
        protected int GetMonkHandToHandDelay()
        {
            // TODO: check for epic fists

            if (this.Race == (short)CharRaces.Human)
                return this.Level > 65 ? 24 : _monkDelaysHuman[this.Level];
            else
                return this.Level > 65 ? 25 : _monkDelaysIksar[this.Level];
        }

        /// <summary>Determines a monk's or beastlord's fist damage.</summary>
        /// <remarks>Data taken from www.monkly-business.com</remarks>
        protected int GetMonkHandToHandDamage()
        {
            // TODO: Check for epic fists

            if (this.Level > 65)
                return 19;
            else
                return _monkDamage[this.Level];
        }

        /// <summary>Begins or stops targeting this entity.</summary>
        /// <param name="target">True to target this entity, false to stop targeting.</param>
        protected internal void Target(bool target)
        {
            if (target) {
                Interlocked.Increment(ref _targetCount);
                //_log.DebugFormat("{0} has been targeted.", this.Name);
            }
            else {
                Interlocked.Decrement(ref _targetCount);
                //_log.DebugFormat("{0} has been de-targeted.", this.Name);
            }
        }

        /// <summary></summary>
        /// <param name="attacker">Optional parameter of whom is causing the damage.</param>
        /// <param name="dmgAmt">Amount of damage caused.</param>
        /// <param name="spellId">Id of the spell responsible for the damage. Zero indicates no spell.</param>
        internal virtual void Damage(Mob attacker, int dmgAmt, int spellId, Skill attackSkill)
        {
            // TODO: is this from a damage shield?

            int hate = dmgAmt * 2;  // TODO: this value may need tweaked
            if (attacker != null) {
                if (attacker is ZonePlayer) {
                    if (!((ZonePlayer)attacker).IsFeigning)   // only hate on the player if they aren't fd'ing
                        _hateMgr.AddHate(attacker, hate, dmgAmt);
                }
                else
                    _hateMgr.AddHate(attacker, hate, dmgAmt);   // TODO: pets seem to have thier own calc for hate (see NPC::Attack in orig emu)
            }

            if (dmgAmt > 0) {   // Is there some damage actually being done?
                if (attacker != null) {     // Is someone doing the damage?
                    // TODO: harm touch

                    // TODO: lifetap spell effects?
                }

                // TODO: pet shit

                // TODO: rune stuff

                _log.DebugFormat("{0} has taken {1} damage. {2} HP left ({3:P}).", this.Name, dmgAmt, this.HP - dmgAmt, (this.HP - dmgAmt) / (float)this.MaxHP);
                if (dmgAmt >= this.HP) {
                    // TODO: try death save via AA abilities, etc. (in a virtual method)

                    // TODO: do we do the damage msg here or elsewhere?
                    this.Die(attacker, dmgAmt, spellId, attackSkill);
                    return;
                }

                this.HP -= dmgAmt;

                if (this.Sneaking)
                    this.Sneaking = false;

                // TODO: remove mez

                // TODO: check stun chances if bashing

                // TODO: handle chance of spell breaking root - (spellId of 0 is no spell)

                // TODO: handle chance of casting interrupt
            }

            OnDamaged(new DamageEventArgs()
                      {
                          SourceId = (ushort)(attacker == null ? 0 : attacker.ID),
                          Damage = (uint)dmgAmt,
                          Type = Mob.GetDamageTypeForSkill(attackSkill),
                          SpellId = (ushort)spellId
                      }
            );
        }

        protected virtual void Die(Mob lastAttacker, int damage, int spellId, Skill attackSkill)
        {
            this.HP = 0;

            OnDamaged(new DamageEventArgs()
            {
                SourceId = (ushort)(lastAttacker == null ? 0 : lastAttacker.ID),
                Damage = (uint)damage,
                Type = Mob.GetDamageTypeForSkill(attackSkill),
                SpellId = (ushort)spellId
            });

            _hateMgr.Clear();
            DePop();
        }

        internal void Kill()
        {
            Die(null, 100000, 0, Skill.HandToHand);
        }

        /// <summary>Determines if this mob is invisible to another mob.</summary>
        /// <param name="other">Mob that may or may not be capable of seeing this mob.</param>
        internal bool IsInvisibleTo(Mob other)
        {
            // check regular invisibility
            if (this.Invisible && !other.SeeInvis)
                return true;

            // check invis vs. undead
            if (other.BodyType == BodyType.Undead || other.BodyType == BodyType.SummonedUndead) {
                if (this.InvisibleToUndead && !other.SeeInvisToUndead)
                    return true;
            }

            // check invis vs. animals
            if (other.BodyType == BodyType.Animal)
                if (this.InvisibleToAnimals && !other.SeeInvisToAnimals)
                    return true;

            if (this.Hidden) {
                if (!other.SeeHide && !other.SeeImprovedHide)
                    return true;
            }

            if (this.ImprovedHidden && !other.SeeImprovedHide)
                return true;

            if (this.Sneaking && IsBehindMob(other))
                return true;

            return false;
        }

        internal void BreakSneakiness()
        {
            // TODO: fade various invis effects

            if (this.Hidden || this.ImprovedHidden)
                OnStanceChanged(new StanceChangedEventArgs(SpawnAppearanceType.Invis, 0));

            _invis = false;
            _invisToAnimals = false;
            _invisToUndead = false;
            _hidden = false;
            _improvedHidden = false;
        }

        #region Distance calculation routines
        internal float Distance(Mob other)
        {
            float xDiff = other.X - this.X;
            float yDiff = other.Y - this.Y;
            float zDiff = other.Z - this.Z;

            return (float)Math.Sqrt((xDiff * xDiff) + (yDiff * yDiff) + (zDiff * zDiff));
        }

        internal float DistanceNoZ(Mob other)
        {
            float xDiff = other.X - this.X;
            float yDiff = other.Y - this.Y;

            return (float)Math.Sqrt((xDiff * xDiff) + (yDiff * yDiff));
        }

        internal float DistanceNoRoot(Mob other)
        {
            float xDiff = other.X - this.X;
            float yDiff = other.Y - this.Y;
            float zDiff = other.Z - this.Z;

            return ((xDiff * xDiff) + (yDiff * yDiff) + (zDiff * zDiff));
        }

        internal float DistanceNoRootNoZ(Mob other)
        {
            float xDiff = other.X - this.X;
            float yDiff = other.Y - this.Y;

            return ((xDiff * xDiff) + (yDiff * yDiff));
        }

        internal override void CheckCoordLosNoZLeaps(float curX, float curY, float curZ, float targetX, float targetY, float targetZ, float perWalk)
        {
            throw new NotImplementedException();
        }

        /// <summary>Determines if we are behind a given mob.  Useful for hide, backstab and riposte checks.</summary>
        /// <param name="other">Mob we are seeing if we are behind.</param>
        internal bool IsBehindMob(Mob other)
        {
            float angle, lengthB, vectorX, vectorY;
            float mobX = -(other.X);	// mob xlocation (inverse because eq is confused)
            float mobY = other.Y;		// mobylocation
            float heading = other.Heading;	// mob heading
            heading = (heading * 360.0f) / 256.0f;	// convert to degrees
            if (heading < 270)
                heading += 90;
            else
                heading -= 270;
            heading = heading * 3.1415f / 180.0f;	// convert to radians
            vectorX = mobX + (10.0f * (float)Math.Cos(heading));	// create a vector based on heading
            vectorY = mobY + (10.0f * (float)Math.Sin(heading));	// of mob length 10

            //length of mob to player vector
            //lengthb = (float)sqrtf(pow((-playerx-mobx),2) + pow((playery-moby),2));
            lengthB = (float)Math.Sqrt(((-(this.X) - mobX) * (-(this.X) - mobX)) + ((this.Y - mobY) * (this.Y - mobY)));

            // calculate dot product to get angle
            angle = (float)Math.Acos(((vectorX - mobX) * (-(this.X) - mobX) + (vectorY - mobY) * (this.Y - mobY)) / (10 * lengthB));
            angle = angle * 180f / 3.1415f;
            if (angle > 90.0) //not sure what value to use (90*2=180 degrees is front)
                return true;
            else
                return false;
        }

        /// <summary>Determines if this mob is within combat range of another mob.</summary>
        /// <param name="other">Mob that may or may not be within combat range of this mob.</param>
        internal bool IsWithinCombatRangeOf(Mob other)
        {
            float sizeMod = this.Size;
            float otherSizeMod = other.Size;

            Races thisRace = (Races)this.Race;
            if (thisRace == Races.LavaDragon || thisRace == Races.Wurm || thisRace == Races.GhostDragon)    // For races with fixed size
                sizeMod = 60.0f;
            else if (sizeMod < 6.0f)
                sizeMod = 8.0f;

            Races otherRace = (Races)this.Race;
            if (otherRace == Races.LavaDragon || otherRace == Races.Wurm || otherRace == Races.GhostDragon)    // For races with fixed size
                otherSizeMod = 60.0f;
            else if (sizeMod < 6.0f)
                otherSizeMod = 8.0f;

            sizeMod = Math.Max(sizeMod, otherSizeMod);

            // not 100% sure what's going on here... seems to be scaling the size modifier a bit
            if (sizeMod > 29)
                sizeMod *= sizeMod;
            else if (sizeMod > 19)
                sizeMod *= sizeMod * 2;
            else
                sizeMod *= sizeMod * 4;

            // Prevent ridiculously sized hit boxes
            if (sizeMod > 10000.0f)
                sizeMod /= 7;

            if (this.DistanceNoRootNoZ(other) <= sizeMod)
                return true;

            return false;
        }
        #endregion

        #region Triggers
        internal void TriggerWearChange(EquipableType et)
        {
            WearChange wc = new WearChange();
            wc.SpawnId = (short)this.ID;
            wc.WearSlotId = (byte)et;
            wc.Material = (short)GetEquipmentMaterial(et);
            wc.Color = GetEquipmentColor(et);
            OnWearChanged(new WearChangeEventArgs(wc)); // Fire wear changed event
        }
        #endregion

        #region Event Handlers
        protected void OnStanceChanged(EventArgs e)
        {
            EventHandler<EventArgs> handler = StanceChanged;

            if (handler != null)
                handler(this, e);
        }

        protected void OnStanceChanged(StanceChangedEventArgs sce)
        {
            EventHandler<StanceChangedEventArgs> handler = StanceChangedEx;

            if (handler != null)
                handler(this, sce);
        }

        protected void OnPositionUpdate(PosUpdateEventArgs e)
        {
            EventHandler<PosUpdateEventArgs> handler = PositionUpdated;

            if (handler != null)
                handler(this, e);
        }

        protected void OnWearChanged(WearChangeEventArgs wce)
        {
            EventHandler<WearChangeEventArgs> handler = WearChanged;

            if (handler != null)
                handler(this, wce);
        }

        protected void OnHPUpdated(EventArgs e)
        {
            EventHandler<EventArgs> handler = HPUpdated;

            if (handler != null)
                handler(this, e);
        }

        protected void OnAnimation(AnimationEventArgs ae)
        {
            EventHandler<AnimationEventArgs> handler = PlayAnimation;

            if (handler != null)
                handler(this, ae);
        }

        protected void OnDamaged(DamageEventArgs de)
        {
            EventHandler<DamageEventArgs> handler = Damaged;

            if (handler != null)
                handler(this, de);
        }

        internal void OnChannelMessage(ChannelMessageEventArgs cme)
        {
            EventHandler<ChannelMessageEventArgs> handler = ChannelMessage;

            if (handler != null)
                handler(this, cme);
        }
        #endregion
    }
}
