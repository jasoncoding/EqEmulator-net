using System;
using System.Runtime.InteropServices;

using EQEmulator.Servers.Internals.Data;
using System.Text;

namespace EQEmulator.Servers.Internals.Packets
{
    [Flags]
    internal enum SessionFormat : byte
    {
        Normal      = 0x00,
        Compressed  = 0x01,
        Encoded     = 0x04
    }

    internal enum SpawnAppearanceType : ushort
    {
        Die         = 0,    // causes client to keel over and zone to bind point
        WhoLevel    = 1,    // the level that shows up on /who
        Invis       = 3,    // 0 = visible, 1= invis
        PVP         = 4,    // 0 = blue, 1 = pvp (red)
        Light       = 5,    // light type emitted by player (lightstone, SBS, etc.)
        Animation   = 14,   // 100=standing, 110=sitting, 111=ducking, 115=feigned, 105=looting (see stance enum)
        Sneak       = 15,	// 0 = normal, 1 = sneaking
        SpawnID     = 16,	// server to client, sets player spawn id
        HP			= 17,	// Client->Server, my HP has changed (like regen tic)
        Linkdead	= 18,	// 0 = normal, 1 = linkdead
        Levitate	= 19,	// 0=off, 1=flymode, 2=levitate
        GM			= 20,	// 0 = normal, 1 = GM - all odd numbers seem to make it GM
        Anon		= 21,	// 0 = normal, 1 = anon, 2 = roleplay
        GuildID		= 22,
        GuildRank	= 23,	// 0=member, 1=officer, 2=leader
        AFK			= 24,	// 0 = normal, 1 = afk
        Split		= 28,	// 0 = normal, 1 = autosplit on
        Size		= 29,	// spawn's size
        NPCName		= 31,	// change PC's name's color to NPC color 0 = normal, 1 = npc name
        ShowHelm    = 43    // 0 = do not show helmet graphic, 1 = show
    }

    internal enum ZoneError     // Only see one of these being used in old emu
    {
        Success         = 1,
        NoMsg           = 0,
        NotReady        = -1,
        ValidPC         = -2,
        StoryZone       = -3,
        NoExpansion     = -6,
        NoExperience    = -7
    }

    internal enum ItemPacketType    // Used to distinguish what type of item packet is being sent
    {
        ViewLink        = 0x00,
        Merchant        = 0x64,
        TradeView       = 0x65,
        Loot            = 0x66,
        Trade           = 0x67,
        CharInventory   = 0x69,
        SummonItem      = 0x6A,
        WorldContainer  = 0x6B,
        TributeItem     = 0x6C
    }

    /// <summary>The type of damage done to an entity.</summary>
    internal enum DamageType : byte
    {
        Lava    = 0xFA,
        Falling = 0xFC,
        Spell   = 0xE7,
        Unknown = 0xFF
    }

    [StructLayout(LayoutKind.Sequential, Pack=1)]
    struct SessionRequest
    {
        public uint CRCLength;    // Probably CRC length
        public uint SessionId;
        public uint MaxLength;    // Max size of client's UDP buffer
    }

    [StructLayout(LayoutKind.Sequential, Pack=1)]
    struct SessionResponse
    {
        public uint SessionId;
        public uint Key;
        public byte CRCLength;      // usually (always?) set to 2
        public byte Format;         // flags; compressed = 0x01, encoded = 0x04, nada = 0x00
        public byte UnknownB;
        public uint MaxLength;    // Max size of server's UDP buffer
        public uint UnknownC;
    }

    [StructLayout(LayoutKind.Sequential, Pack=1)]
    struct SessionStats             //Deltas are in ms, representing round trip times
    {
/*000*/ public ushort RequestID;
/*002*/ public uint LastLocalDelta;
/*006*/ public uint AverageDelta;
/*010*/ public uint LowDelta;
/*014*/ public uint HighDelta;
/*018*/ public uint LastRemoteDelta;
/*022*/ public UInt64 PacketsSent;
/*030*/ public UInt64 PacketsRecieved;
/*038*/
    }

    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet=CharSet.Ansi)]
    struct LoginInfo
    {
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType=UnmanagedType.U1, SizeConst=64)]
/*000*/ public byte[] LoginInfoData;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType=UnmanagedType.U1, SizeConst=124)]
/*064*/	public byte[] unknown064;
/*188*/	public byte zoning;			// 01 if zoning, 00 if not
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType=UnmanagedType.U1, SizeConst=275)]
/*189*/	public byte[] unknown189;
/*488*/
    }

    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet=CharSet.Ansi)]
    struct GuildsList
    {
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
        public byte[] Head; // First on guild list seems to be empty...
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = (Guild.MAX_NUMBER_GUILDS * 64))]
        public byte[] Guilds;
    }

    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet=CharSet.Ansi)]
    struct LogServer     // used with: ApplicationOpCode.LogServer
    {
/*000*/	public uint	    unknown000;
/*004*/	public uint	    unknown004;
/*008*/	public uint	    unknown008;
/*012*/	public uint	    unknown012;     // htonl(1) on live
/*016*/	public uint	    unknown016;	    // htonl(1) on live
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 12)]
/*020*/	public byte[]   unknown020;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 32)]
/*032*/	public byte[]   WorldShortName;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 32)]
/*064*/	public byte[]   unknown064;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 16)]
/*096*/	public byte[]   unknown096;	    // 'pacman' on live
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 16)]
/*112*/	public byte[]   unknown112;	    // '64.37,148,36' on live
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 48)]
/*128*/	public byte[]   unknown128;
/*176*/	public uint	    unknown176;	    // htonl(0x00002695)
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 80)]
/*180*/	public byte[]   unknown180;	    // 'eqdataexceptions@mail.station.sony.com' on live
/*260*/	public byte	    unknown260;	    // 0x01 on live
/*261*/	public byte	    unknown261;	    // 0x01 on live
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 2)]
/*262*/	public byte[]   unknown262;
/*264*/
    }

    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet=CharSet.Ansi)]
    internal class CharacterSelect
    {
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.I4, SizeConst = 10)]
/*0000*/public int[]    Race = new int[10];             // Character's Race
        //[MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 360)]
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U4, SizeConst = 90)]
        public uint[] EquipColors = new uint[90];
/*0040*///public byte[]   EquipColors = new byte[360];
        //public Color[]  EquipColors = new Color[90];    // Characters Equipment Colors  WAS: Color_Struct[10][9]
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 10)]
/*0400*/public byte[]   BeardColor = new byte[10];	    // Characters beard Color
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 10)]
/*0410*/public byte[]   Hair = new byte[10];			// Characters hair style
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.I4, SizeConst = 90)]
/*0420*/public int[]    Equip = new int[90];			// WAS: int[10][9]  0=helm, 1=chest, 2=arm, 3=bracer, 4=hand, 5=leg, 6=boot, 7=melee1, 8=melee2  (Might not be)
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.I4, SizeConst = 10)]
/*0780*/public int[]    Secondary = new int[10];		// Characters secondary IDFile number
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 10)]
/*0820*/public byte[]   unknown820;	    // 10x ff
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 2)]
/*0830*/public byte[]   unknown830;	    // 2x 00
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.I4, SizeConst = 10)]
/*0832*/public int[]    Deity = new int[10];			// Characters Deity
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 10)]
/*0872*/public byte[]   GoHome = new byte[10];		    // 1=Go Home available, 0=not
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 10)]
/*0882*/public byte[]   Tutorial = new byte[10];		// 1=Tutorial available, 0=not
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 10)]
/*0892*/public byte[]   Beard = new byte[10];		    // Characters Beard Type
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 10)]
/*0902*/public byte[]   unknown902;	    // 10x ff
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.I4, SizeConst = 10)]
/*0912*/public int[]    Primary = new int[10];		// Characters primary IDFile number
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 10)]
/*0952*/public byte[]   HairColor = new byte[10];	    // Characters Hair Color
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 2)]
/*0962*/public byte[]   unknown962;     // 2x 00
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.I4, SizeConst = 10)]
/*0964*/public int[]    Zone = new int[10];			// Characters Current Zone
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 10)]
/*1004*/public byte[]   Class = new byte[10];		    // Characters Classes
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 10)]
/*1014*/public byte[]   Face = new byte[10];			// Characters Face Type
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 640)]
/*1024*/public byte[]   Name = new byte[640];			// Characters Names  WAS: char[10][64]
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 10)]
/*1664*/public byte[]   Gender = new byte[10];		    // Characters Gender
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 10)]
/*1674*/public byte[]   EyeColor1 = new byte[10];	    // Characters Eye Color
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 10)]
/*1684*/public byte[]   EyeColor2 = new byte[10];	    // Characters Eye 2 Color
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 10)]
/*1694*/public byte[]   Level = new byte[10];		    // Characters Levels
/*1704*/

        public CharacterSelect()
        {
            this.unknown820 = new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff };
            this.unknown902 = new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff };
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class Color
    {
        public uint ColorValue = 0;

        public Color(uint color)
        {
            this.ColorValue = color;
        }

        public byte Blue
        {
            get { return BitConverter.GetBytes(this.ColorValue)[0] ; }
        }
        public byte Green
        {
            get { return BitConverter.GetBytes(this.ColorValue)[1]; }
        }
        public byte Red
        {
            get { return BitConverter.GetBytes(this.ColorValue)[2]; }
        }
        public bool UseTint
        {
            get { return BitConverter.GetBytes(this.ColorValue)[3] == 0xFF; }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet=CharSet.Ansi)]
    struct EnterWorld
    {
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
/*000*/	public byte[] Name;
/*064*/	public uint	Tutorial;	// 01 on "Enter Tutorial", 00 if not
/*068*/	public uint	ReturnHome; // 01 on "Return Home", 00 if not
    }

    [StructLayout(LayoutKind.Sequential, Pack=1, CharSet=CharSet.Ansi)]
    struct NameGeneration
    {
/*000*/ public int Race;
/*004*/	public int Gender;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
/*008*/	public byte[] Name;
/*072*/
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class CharacterCreate
    {
/*000*/ public uint Class;
/*004*/ public uint HairColor;	// Might be hairstyle
/*008*/ public uint BeardColor;	// Might be beard
/*012*/ public uint Beard;		// Might be beardcolor
/*016*/ public uint Gender;
/*020*/ public uint Race;
/*024*/ public uint StartZone;  // 0 = odus, 1 = qeynos, 2 = halas, 3 = rivervale, 4 = freeport, 5 = neriak, 6 = gukta/grobb, 7 = ogguk, 8 = kaladim, 9 = gfay, 10 = felwithe, 11 = akanon, 12 = cabalis, 13 = shar vahl
/*028*/ public uint HairStyle;	// Might be haircolor
/*032*/ public uint Deity;
/*036*/ public uint STR;
/*040*/ public uint STA;
/*044*/ public uint AGI;
/*048*/ public uint DEX;
/*052*/ public uint WIS;
/*056*/ public uint INT;
/*060*/ public uint CHA;
/*064*/ public uint Face;		// Could be unknown0076
/*068*/ public uint EyeColor1;	// its possible we could have these switched
/*073*/ public uint EyeColor2;	// since setting one sets the other we really can't check
/*076*/ public uint unknown0076;	// Could be face
/*080*/
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class PlayerProfile
{
/*0000*/	public uint	    Checksum;			// Checksum from CRC32::SetEQChecksum
/*0004*/    public uint     Gender;				// Player Gender - 0 Male, 1 Female
/*0008*/    public uint     Race;				// Player race
/*0012*/    public uint     Class;				// Player class
/*0016*/    public uint     Unknown16;          // *** Placeholder ***
/*0020*/    public byte     Level;              // Level of player
/*0021*/    public byte     Level1;             // Level of player (again for some reason?)
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 2)]
/*0022*/    public byte[]   Unknown22;          // *** Placeholder ***
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 5)]
            public Bind[]   Binds  = new Bind[5];
            //[MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 100)]
            //public byte[]   BindBlob;           // Was Bind[5].. see below
/*0024*/	//BindStruct[]	    Binds[5];       // Bind points (primary is first)
/*0124*/    public uint     Deity;				// deity
/*0128*/	public uint     Intoxication;       // Alcohol level (ticks til sober?)
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U4, SizeConst = Character.MAX_MEMSPELL)]
/*0132*/	public uint[]	SpellSlotRefresh = new uint[Character.MAX_MEMSPELL];    // in ms
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
/*0168*/    public byte[]   Unknown168;
/*0172*/	public byte		HairColor;			// Player hair color
/*0173*/	public byte		BeardColor;			// Player beard color
/*0174*/	public byte		EyeColor1;			// Player left eye color
/*0175*/	public byte		EyeColor2;			// Player right eye color
/*0176*/	public byte		HairStyle;			// Player hair style
/*0177*/	public byte		Beard;				// Beard type
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 10)]
/*0178*/    public byte[]   Unknown178;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U4, SizeConst = Character.MAX_EQUIPABLES)]
/*0188*/	public uint[]	ItemMaterial = new uint[Character.MAX_EQUIPABLES];  // Item texture/material of worn/held items
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 48)]
/*0224*/    public byte[]   Unknown224;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4 * Character.MAX_EQUIPABLES)]
            public byte[]   ColorBlob = new byte[4 * Character.MAX_EQUIPABLES]; // was Color_Struct[9]... see below
/*0272*/	//Color_Struct	ItemTint[MAX_MATERIALS];
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.I4, SizeConst = 2 * Character.MAX_AA)]
            public int[]    AABlob = new int[2 * Character.MAX_AA]; // was AA_Array[240]... see below
/*0308*/	//AA_Array		aa_array[MAX_PP_AA_ARRAY];
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
/*2220*/    public byte[]   Unknown2220;
/*2224*/	public uint		Points;				// Unspent Practice points
/*2228*/	public uint		Mana;				// current mana
/*2232*/	public uint		HP;				    // current hp (without adj. for equip, etc.)
/*2236*/	public uint		STR;				// Strength
/*2240*/	public uint		STA;				// Stamina
/*2244*/	public uint		CHA;				// Charisma
/*2248*/	public uint		DEX;				// Dexterity
/*2252*/	public uint		INT;				// Intelligence
/*2256*/	public uint		AGI;				// Agility
/*2260*/	public uint		WIS;				// Wisdom
/*2264*/	public byte		Face;				// Player face
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 47)]
/*2265*/    public byte[]   Unknown2265;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U4, SizeConst = Character.MAX_SPELLBOOK)]
/*2312*/	public uint[]	SpellBook = new uint[Character.MAX_SPELLBOOK];  // List of spells in spellbook
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 448)]
/*3912*/    public byte[]   Unknown3912;        // all 0xFF after last spell
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U4, SizeConst = Character.MAX_MEMSPELL)]
/*4360*/	public uint[]	MemSpells = new uint[Character.MAX_MEMSPELL];   // List of memorized spells
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 32)]
/*4396*/    public byte[]   Unknown4396;
/*4428*/	public uint		Platinum;			// Platinum Pieces on player
/*4432*/	public uint		Gold;				// Gold Pieces on player
/*4436*/	public uint		Silver;				// Silver Pieces on player
/*4440*/	public uint		Copper;				// Copper Pieces on player
/*4444*/	public uint		PlatinumCursor;	    // Platinum on cursor
/*4448*/	public uint		GoldCursor;		    // Gold on cursor
/*4452*/	public uint		SilverCursor;		// Silver on cursor
/*4456*/	public uint		CopperCursor;		// Copper on cursor
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U4, SizeConst = Character.MAX_SKILL)]
/*4460*/	public uint[]	Skills = new uint[Character.MAX_SKILL]; // List of skills
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 236)]
/*4760*/    public byte[]   Unknown4760;
/*4996*/	public uint		Toxicity;	        // From drinking potions (each potion adds 3, 15 = too toxic)
/*5000*/	public uint		ThirstLevel;        // ticks til next drink
/*5004*/	public uint		HungerLevel;        // ticks til next eat
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 20 * Character.MAX_BUFF)]
            public byte[]   BuffsBlob = new byte[20 * Character.MAX_BUFF];  // see below for what was
/*5008*/	//SpellBuff_Struct	Buffs[BUFF_COUNT];	// Buffs currently on the player
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U4, SizeConst = 100)]
/*5508*/	public uint[]   Disciplines;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 160)]
/*5908*/    public byte[]   Unknown5908;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U4, SizeConst = Character.MAX_RECAST_TYPES)]
/*6068*/    public uint[]   RecastTimers;       // Timers (GMT of last use)
/*6148*/	public uint		Endurance;
/*6152*/	public uint		AAPointsSpent;      // Number of spent AA points
/*6156*/	public uint		AAPoints;	        // Avaliable, unspent AA points
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
/*6160*/    public byte[]   Unknown6160;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 1280)]
            public byte[]   BandolierBlob;      // see below for what was
/*6164*/    //Bandolier_Struct	Bandoliers[MAX_PLAYER_BANDOLIER];
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 5120)]
/*7444*/    public byte[]   Unknown7444;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 288)]
            public byte[]   PotionBeltBlob;     // see below for what was
/*12564*/   //PotionBelt_Struct	PotionBelt;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 8)]
/*12852*/   public byte[]   Unknown12852;
/*12860*/   public uint     AvailableSlots = 0xFFFFFFFF;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 76)]
/*12864*/   public byte[]   Unknown12864;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
/*12940*/	public byte[]	Name = new byte[64];    // Name of player
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 32)]
/*13004*/	public byte[]	Surname = new byte[32]; // Last name of player
/*13036*/	public uint		GuildId;
/*13040*/	public uint		Birthday;			// characters bday
/*13044*/	public uint		LastLogin;			// char last save time
/*13048*/	public uint		TimePlayedMin;		// in minutes
/*13052*/	public byte		PVP;                // 1 = pvp, 0 = not pvp
/*13053*/	public byte		Anon;		        // 2=roleplay, 1=anon, 0=not anon
/*13054*/	public byte		GM;                 // 0 = no, 1 = yes (guessing)
/*13055*/	public byte		GuildRank;          // 0 = member, 1 = officer, 2 = guildleader
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 12)]
/*13056*/   public byte[]   Unknown13056;
/*13068*/	public uint		XP;                 // Current Experience
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 12)]
/*13072*/   public byte[]   Unknown13072;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = Character.MAX_LANGUAGE)]
/*13084*/	public byte[]	Languages = new byte[Character.MAX_LANGUAGE];
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
/*13112*/   public byte[]   Unknown13112;       // All 0x00 (language buffer?)
/*13116*/	public float	X;					// Player x position
/*13120*/	public float	Y;					// Player y position
/*13124*/	public float	Z;					// Player z position
/*13128*/	public float	Heading;			// Direction player is facing
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
/*13132*/   public byte[]   Unknown13132;       // *** Placeholder ***
/*13136*/	public uint		PlatinumBank;		// Platinum Pieces in Bank
/*13140*/	public uint		GoldBank;			// Gold Pieces in Bank
/*13144*/	public uint		SilverBank;		    // Silver Pieces in Bank
/*13148*/	public uint		CopperBank;		    // Copper Pieces in Bank
/*13152*/	public uint		PlatinumShared;     // Platinum shared between characters
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 84)]
/*13156*/   public byte[]   Unknown13156;
/*13240*/	public uint		Expansions;		    // expansion setting, bit field of expansions avaliable
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 12)]
/*13244*/   public byte[]   Unknown13244;
/*13256*/	public uint		AutoSplit;			// 0 = off, 1 = on
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 16)]
/*13260*/   public byte[]   Unknown13260;
/*13276*/	public ushort	ZoneId;			    // Current zone of the player
/*13278*/	public ushort	ZoneInstance;		// Instance ID
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = Character.MAX_GROUP_MEMBERS * 64)]
/*13280*/	public byte[] 	GroupMembers = new byte[384];   // All group members, including self - was char [MAX_GROUP_MEMBERS][64]
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
/*13664*/   public byte[]   GroupLeader;        // Leader of the group?
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 660)]
/*13728*/   public byte[]   Unknown13728;
/*14388*/   public uint     LeadAAActive;       // 0 = Leader AA off, 1 = Leader AA on
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
/*14392*/   public byte[]   Unknown14392;
/*14396*/	public int		LdonPointsGuk;		// the various earned adventure points by zone
/*14400*/	public int		LdonPointsMir;
/*14404*/	public int		LdonPointsMmc;
/*14408*/	public int		LdonPointsRuj;
/*14412*/	public int		LdonPointsTak;
/*14416*/	public int		LdonPointsAvailable;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 132)]
/*14420*/   public byte[]   Unknown14420;
/*14552*/	public uint		TributeTimeRemaining;	// Time remaining on tribute (in miliseconds)
/*14556*/	public uint		CareerTributePoints;    // Total favor points for char
/*14560*/   public uint     Unknown14560;       // *** Placeholder ***
/*14564*/	public uint		TributePoints;      // Current tribute points
/*14568*/   public uint     Unknown14568;       // *** Placeholder ***
/*14572*/	public uint		TributeActive;		// 1 = active, 0 = off
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 40)]
            public byte[]   TributeBlob;        // see below for what was
/*14576*/	//Tribute_Struct		Tributes[MAX_PLAYER_TRIBUTES];
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 8)]
/*14616*/   public byte[]   Unknown14616;
/*14624*/	public uint		GroupLeadershipXp;  // Current group leadership xp (format might be 0-1000?)
/*14628*/   public uint     Unknown14628;
/*14632*/	public uint		RaidLeadershipXp;	// Current raid lead AA xp (format might be 0-2000?)
/*14636*/	public uint		GroupLeadershipPoints;  // Unspent group lead AA points
/*14640*/	public uint		RaidLeadershipPoints;   // Unspent raid lead AA points
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U4, SizeConst = 32)]
/*14644*/	public uint[]   LeaderAbilities;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 128)]
/*14772*/   public byte[]   Unknown14772;
/*14900*/	public uint		AirRemaining;       // Remaining air supply in seconds
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4608)]
/*14904*/   public byte[]   Unknown14904;
/*19512*/	public uint		XpAA;               // Xp earned in current AA point
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 40)]
/*19516*/   public byte[]   Unknown19516;
/*19556*/	public uint		CurrentRadCrystals; // Current count of radiant crystals
/*19560*/	public uint		CareerRadCrystals;  // Total count of radiant crystals ever
/*19564*/	public uint		CurrentEbonCrystals;// Current count of ebon crystals
/*19568*/	public uint		CareerEbonCrystals;	// Total count of ebon crystals ever
/*19572*/	public byte		GroupAutoConsent;   // 0=off, 1=on
/*19573*/	public byte		RaidAutoConsent;    // 0=off, 1=on
/*19574*/	public byte		GuildAutoConsent;   // 0=off, 1=on
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 5)]
/*19575*/   public byte[]   Unknown19575;       // *** placeholder ***
/*19580*/   public uint     ShowHelm;           // 0 = no, 1 = yes
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
/*19584*/   public byte[]   Unknown19584;       // *** placeholder ***
/*19588*/   public uint     Unknown19588;       // *** placeholder ***
/*19592*/

        /// <summary>new will init a struct's fields but won't fill arrays.</summary>
        internal PlayerProfile()
        {
            //this.Name = new byte[64];
            //this.Surname = new byte[32];
            //this.Binds = new Bind[5];
            ////this.BindBlob = new byte[100];
            //this.SpellSlotRefresh = new uint[Character.MAX_MEMSPELL];
            //this.ItemMaterial = new uint[Character.MAX_EQUIPABLES];
            //this.ColorBlob = new byte[4 * Character.MAX_EQUIPABLES];
            //this.AABlob = new int[2 * Character.MAX_AA];
            //this.Languages = new byte[Character.MAX_LANGUAGE];
            //this.SpellBook = new uint[Character.MAX_SPELLBOOK];
            //this.MemSpells = new uint[Character.MAX_MEMSPELL];
            //this.Skills = new uint[Character.MAX_SKILL];
            //this.BuffsBlob = new byte[20 * Character.MAX_BUFF];
            //this.GroupMembers = new byte[384];

            this.AvailableSlots = 0xFFFFFFFF;
            this.Unknown3912 = new byte[448];
            Buffer.BlockCopy(new string((char)0xFF, 448).ToCharArray(), 0, this.Unknown3912, 0, 448);
            this.Unknown4396 = new byte[32];
            Buffer.BlockCopy(new string((char)0xFF, 32).ToCharArray(), 0, this.Unknown4396, 0, 32);
            this.ShowHelm = 1;  // TODO: why is this always set?  Isn't this configurable in the client?
            this.Unknown12864 = new byte[] {0x78,0x03,0x00,0x00,0x1A,0x04,0x00,0x00,0x1A,0x04,0x00,0x00,0x19,0x00,0x00,0x00,0x19,0x00,0x00,0x00,
                                            0x19,0x00,0x00,0x00,0x0F,0x00,0x00,0x00,0x0F,0x00,0x00,0x00,0x0F,0x00,0x00,0x00,0x1F,0x85,0xEB,0x3E,
                                            0x33,0x33,0x33,0x3F,0x09,0x00,0x00,0x00,0x02,0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x14,0x00,0x00,0x00,
                                            0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00};
            //this.TributeBlob = new byte[40];
            //this.Disciplines = new uint[100];
            //this.LeaderAbilities = new uint[32];
            //this.BandolierBlob = new byte[1280];
            //this.PotionBeltBlob = new byte[288];

            for (int i = 0; i < Character.MAX_SPELLBOOK; i++)   // blank out the spellbook
                this.SpellBook[i] = Spell.BLANK_SPELL;

            for (int i = 0; i < Character.MAX_MEMSPELL; i++)    // blank out the memorized spells
                this.MemSpells[i] = Spell.BLANK_SPELL;
        }
    }

    struct Bind {
/*000*/ public uint ZoneId;
/*004*/ public float X;
/*008*/ public float Y;
/*012*/ public float Z;
/*016*/ public float Heading;
/*020*/
    }

    struct AA_Array
    {
	    public int AA;
	    public int Value;
    }

    struct SpellBuff
    {
/*000*/	public byte	    SlotId;		        //badly named... seems to be 2 for a real buff, 0 otherwise
/*001*/ public byte	    Level;
/*002*/	public byte	    BardModifier;
/*003*/	public byte	    Effect;			    //not real
/*004*/	public int	    SpellId;
/*008*/ public int	    Duration;
/*012*/	public short	DmgShieldRemaining; //these are really the caster's global player ID for wearoff
/*013*/ public byte	    PersistantBuff;	    //prolly not real, used for perm illusions
/*014*/ public byte	    Reserved;		    //proll not real, reserved will use for something else later
/*012*/	public int	    PlayerId;	        //'global' ID of the caster, for wearoff messages
    }

    struct Tribute_Struct
    {
	    uint Tribute;
	    uint Tier;
    }

    // Zone server information
    // Size: 130 bytes
    struct ZoneServerInfo   // used with: ApplicationOpCode.ZoneServerInfo
    {
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 128)]
/*000*/ public byte[]   IP;
/*128*/	public ushort	Port;
    }

    /// <summary>Old Emu says this struct can vary so I guess keep an eye out.</summary>
    struct ZoneUnavailable
    {
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 16)]
        public byte[]   ZoneName;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.I2, SizeConst = 4)]
        public short[]  unknown;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct ClientZoneEntry
    {
        public uint     unknown00;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
        public byte[]   CharName;
    }

    // Generic Spawn Struct
    // Length: 385 bytes
    // Used in: spawnZoneStruct, dbSpawnStruct, petStruct, newSpawnStruct
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct Spawn
    {
/*000*/ public byte     unknown0000;
/*001*/ public byte     GM;             // 0=no, 1=gm
/*002*/ public byte     unknown0003;
/*003*/ public byte     AATitle;        // 0=none, 1=general, 2=archtype, 3=class
/*004*/ public byte     unknown0004;
/*005*/ public byte     Anon;           // 0=normal, 1=anon, 2=roleplay
/*006*/ public byte     Face;           // Face id for players
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
/*007*/ public byte[]   Name;           // Player's Name
/*071*/ public ushort   Deity;          // Player's Deity
/*073*/ public ushort   unknown0073;
/*075*/ public float    Size;           // Model size
/*079*/ public uint     unknown0079;
/*083*/ public byte     NPC;            // 0=player,1=npc,2=pc corpse,3=npc corpse,a
/*084*/ public byte     Invis;          // Invis (0=not, 1=invis)
/*085*/ public byte     HairColor;      // Hair color
/*086*/ public byte     CurHpPct;       // Current hp %%% wrong
/*087*/ public byte     MaxHpCategory;  // Takes on the value 100 for players, 100 or 110 for NPCs and 120 for PC corpses...
/*088*/ public byte     Findable;       // 0=can't be found, 1=can be found
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 5)]
/*089*/ public byte[]   unknown0089;
/*094*/ public int      DeltaHeadingAndX;   // see below for original specs
            //signed   deltaHeading:10;// change in heading
            //signed   x:19;           // x coord
            //signed   padding0054:3;  // ***Placeholder
/*098*/ public int      YAndAnimation;  // see below for original specs 
            //signed   y:19;           // y coord
            //signed   animation:10;   // animation
            //signed   padding0058:3;  // ***Placeholder
/*102*/ public int      ZAndDeltaY;     // see below for original specs
            //signed   z:19;           // z coord
            //signed   deltaY:13;      // change in y
/*106*/ public int      DeltaXAndHeading;   // see below for original specs
            //signed   deltaX:13;      // change in x
            //unsigned heading:12;     // heading
            //signed   padding0066:7;  // ***Placeholder
/*110*/ public int      DeltaZ;     // see below for original specs
            //signed   deltaZ:13;      // change in z
            //signed   padding0070:19; // ***Placeholder
/*114*/ public byte     EyeColor1;      // Player's left eye color
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 24)]
/*115*/ public byte[]   unknown0115;
/*139*/ public byte     ShowHelm;       // 0=no, 1=yes
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
/*140*/ public byte[]   unknown0140;
/*144*/ public byte     IsNpc;          // 0=no, 1=yes
/*145*/ public byte     HairStyle;      // Hair style
/*146*/ public byte     Beard;          // Beard style (not totally, sure but maybe!)
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
/*147*/ public byte[]   unknown0147;
/*151*/ public byte     Level;          // Spawn Level
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
/*152*/ public byte[]   unknown0259;    // ***Placeholder
/*156*/ public byte     BeardColor;     // Beard color
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 32)]
/*157*/ public byte[]   Suffix;         // Player's suffix (of Veeshan, etc.)
/*189*/ public uint     PetOwnerId;     // If this is a pet, the spawn id of owner
/*193*/ public byte     GuildRank;      // 0=normal, 1=officer, 2=leader
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 3)]
/*194*/ public byte[]   unknown0194;
/*197*/ public uint     EquipHelmet;    // Equipment: Helmet Visual
/*201*/ public uint     EquipChest;     // Equipment: Chest Visual
/*205*/ public uint     EquipArms;      // Equipment: Arms Visual
/*209*/ public uint     EquipBracers;   // Equipment: Bracers Visual
/*213*/ public uint     EquipHands;     // Equipment: Hands Visual
/*217*/ public uint     EquipLegs;      // Equipment: Legs Visual
/*221*/ public uint     EquipFeet;      // Equipment: Feet Visual
/*225*/ public uint     EquipPrimary;   // Equipment: Primary Visual
/*229*/ public uint     EquipSecondary; // Equipment: Secondary Visual
/*233*/ public float    RunSpeed;       // Speed when running
/*036*/ public byte     Afk;            // 0=no, 1=afk
/*238*/ public uint     GuildId;        // Current guild
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 32)]
/*242*/ public byte[]   Title;          // Title
/*274*/ public byte     unknown0274;
/*275*/ public byte     Helm;           // Helm texture
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 8)]
/*276*/ public byte[]   SetTo0xFF;      // ***Placeholder (all ff)
/*284*/ public uint     Race;           // Spawn race
/*288*/ public uint     unknown0288;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 32)]
/*292*/ public byte[]   Surname;        // Player's Lastname
/*324*/ public float    WalkSpeed;      // Speed when walking
/*328*/ public byte     unknown0328;
/*329*/ public byte     IsPet;          // 0=no, 1=yes
/*330*/ public byte     Light;          // Spawn's lightsource %%% wrong
/*331*/ public byte     Class;          // Player's class
/*332*/ public byte     EyeColor2;      // Left eye color
/*333*/ public byte     unknown0333;
/*334*/ public byte     Gender;         // Gender (0=male, 1=female)
/*335*/ public byte     BodyType;       // Bodytype
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 3)]
/*336*/ public byte[]   unknown0336;
/*339*/ public byte     EquipChestMountColor;   /* Second place in packet for chest texture (usually 0xFF in live packets). Not sure why there are 2 of them,
                                        but it effects chest texture! Can also be mount color... (drogmor: 0=white, 1=black, 2=green, 3=red 
                                        horse: 0=brown, 1=white, 2=black, 3=tan) */
/*340*/ public uint     SpawnId;        // Spawn Id
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
/*344*/ public byte[]   unknown0344;
/*348*/ public uint     HelmetColor;    // Color of helmet item (all of these colors were of type Color_Struct)
/*352*/ public uint     ChestColor;     // Color of chest item
/*356*/ public uint     ArmsColor;      // Color of arms item
/*360*/ public uint     BracersColor;   // Color of bracers item
/*364*/ public uint     HandsColor;     // Color of hands item
/*368*/ public uint     LegsColor;      // Color of legs item
/*372*/ public uint     FeetColor;      // Color of feet item
/*376*/ public uint     PrimaryColor;   // Color of primary item
/*380*/ public uint     SecondaryColor; // Color of secondary item
/*384*/ public byte     Lfg;            // 0=off, 1=lfg on
/*385*/

        /// <summary>new will init a struct's fields but won't fill arrays.</summary>
        internal void Init()
        {
            this.Name = new byte[64];
            this.Suffix = new byte[32];
            this.Title = new byte[32];
            this.SetTo0xFF = new byte[] { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff };
            this.Surname = new byte[32];
            this.DeltaHeadingAndX = 0;
            this.YAndAnimation = 0;
            this.ZAndDeltaY = 0;
            this.DeltaXAndHeading = 0;
            this.DeltaZ = 0;
            this.ShowHelm = 1;      // TODO: why is this always set?  Isn't this configurable in the client?
        }

        public float Heading
        {
            set { this.DeltaXAndHeading |= (Utility.FloatToEQ19(value) & 0x1FFE000); }
            get { return Utility.EQ19ToFloat((this.DeltaXAndHeading << 22) >> 22); }
        }

        public float XPos
        {
            set { this.DeltaHeadingAndX |= ((Utility.FloatToEQ19(value) << 10) & 0x1FFFFC00); }
            get { return Utility.EQ19ToFloat((this.DeltaHeadingAndX << 3) >> 13); }
        }

        public float YPos
        {
            set { this.YAndAnimation |= (Utility.FloatToEQ19(value) & 0x7FFFF); }
            get { return Utility.EQ19ToFloat((this.YAndAnimation << 13) >> 13); }
        }

        public float ZPos
        {
            set { this.ZAndDeltaY |= (Utility.FloatToEQ19(value) & 0x7FFFF); }
            get { return Utility.EQ19ToFloat((this.ZAndDeltaY << 13) >> 13); }
        }
    }

    // EverQuest Time Information:
    // 72 minutes per EQ Day
    // 3 minutes per EQ Hour
    // 6 seconds per EQ Tick (2 minutes EQ Time)
    // 3 seconds per EQ Minute
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct TimeOfDay
    {
        public byte Hour;
        public byte Minute;
        public byte Day;
        public byte Month;
        public int Year;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct TributeInfo
    {
        public int      Active;     // 0 == inactive, 1 == active
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U4, SizeConst = Tribute.MAX_PLAYER_TRIBUTES)]
	    public uint[]   Tributes;	// -1 == NONE
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U4, SizeConst = Tribute.MAX_PLAYER_TRIBUTES)]
	    public int[]    Tiers;		// all 00's
	    public int	    TributeMasterID;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct Weather
    {
        public uint Val1;   // generally 0x000000FF
        public uint Type;	// 0x31=rain, 0x02=snow(i think), 0 = normal
        public uint Mode;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct NewZone
    {
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
/*000*/ public	byte[]	CharName;			// Character Name
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 32)]
/*064*/	public  byte[]	ZoneShortName;	    // Zone Short Name
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 278)]
/*096*/	public  byte[]	ZoneLongName;	    // Zone Long Name
/*374*/	public  byte	ZoneType;			// Zone type (usually FF)
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
/*375*/	public  byte[]	FogRed;				// Zone fog (red)
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
/*379*/	public  byte[]	FogGreen;			// Zone fog (green)
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
/*383*/	public  byte[]	FogBlue;			// Zone fog (blue)
/*387*/	public  byte	Unknown323;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.R4, SizeConst = 4)]
/*388*/	public  float[]	FogMinClip;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.R4, SizeConst = 4)]
/*404*/	public  float[]	FogMaxClip;
/*420*/	public  float	Gravity;
/*424*/	public  byte	TimeType;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 49)]
/*425*/	public  byte[]	Unknown360;
/*474*/	public  byte	Sky;				// Sky Type
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 13)]
/*475*/	public  byte[]	Unknown331;			// ***Placeholder
/*488*/	public  float	ZoneExpMultiplier;	// Experience Multiplier
/*492*/	public  float	SafeY;				// Zone Safe Y
/*496*/	public  float	SafeX;				// Zone Safe X
/*500*/	public  float	SafeZ;				// Zone Safe Z
/*504*/	public  float	MaxZ;				// Guessed
/*508*/	public  float	Underworld;			// Underworld, min z (Not Sure?)
/*512*/	public  float	MinClip;			// Minimum View Distance
/*516*/	public  float	MaxClip;			// Maximum View DIstance
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 84)]
/*520*/	public  byte[]	UnknownEnd;		    // ***Placeholder
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 68)]
/*604*/	public  byte[]	ZoneShortName2;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 12)]
/*672*/	public  byte[]	Unknown672;
/*684*/	public  ushort	ZoneId;
/*686*/	public  ushort	ZoneInstance;
/*688*/	public  uint	Unknown688;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 8)]
/*692*/	public  byte[]	Unknown692;
/*700*/
        /// <summary>Convenience initializer - new will init a struct's fields but won't fill arrays.</summary>
        internal void Init()
        {
            this.CharName = new byte[64];
            this.ZoneShortName = new byte[32];
            this.ZoneLongName = new byte[278];
            this.ZoneShortName2 = new byte[68];
            this.FogRed = new byte[4];
            this.FogGreen = new byte[4];
            this.FogBlue = new byte[4];
            this.FogMinClip = new float[4];
            this.FogMaxClip = new float[4];
        }
    }

    /// <summary>Changes client appearance for all other clients in zone</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct SpawnAppearance
    {
/*000*/ public ushort  SpawnId;    // ID of the spawn
/*002*/ public ushort  Type;       // Values associated with the type
/*004*/ public uint    Parameter;  // Type of data sent
/*008*/
        public SpawnAppearance(ushort spawnId, ushort type, uint parameter)
        {
            this.SpawnId = spawnId;
            this.Type = type;
            this.Parameter = parameter;
        }
    }

    /// <summary>Zone in send name - 136 bytes</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct ZoneInSendName
    {
/*000*/ public int      Unknown0;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
/*004*/	public byte[]   Name;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
/*068*/	public byte[]   Name2;
/*132*/ public int      Unknown132;
/*136*/
        /// <summary>Convenience initializer - new will init a struct's fields but won't fill arrays.</summary>
        internal void Init()
        {
            this.Name = new byte[64];
            this.Name2 = new byte[64];
        }
    }

    /// <summary>Guild Message of the Day - 648 bytes</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct GuildMOTD
    {
/*0000*/ public	int	    Unknown0;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
/*0004*/ public	byte[]	Name;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
/*0068*/ public byte[]	SetByName;
/*0132*/ public	int	    Unknown132;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 512)]
/*0136*/ public	byte[]	Motd;
/*0648*/
        /// <summary>Convenience initializer - new will init a struct's fields but won't fill arrays.</summary>
        internal void Init()
        {
            this.Name = new byte[64];
            this.SetByName = new byte[64];
            this.Motd = new byte[512];
        }
    }

    /// <summary>Set Server Filter - 116 bytes - Tells server what messages to cull for this client.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct SetServerFilter
    {
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U4, SizeConst = 29)]
        public uint[] Filters;  // see FilterType enum in messaging class
    }

    /// <summary>Player position update.  Sent from client->server to update player position on server.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct PlayerPositionUpdateClient
    {
/*0000*/ public ushort  SpawnId;
/*0002*/ public ushort  Sequence;	    //increments one each packet
/*0004*/ public float   YPos;           // y coord
/*0008*/ public float   DeltaZ;         // Change in z
/*0012*/ public float   DeltaX;         // Change in x
/*0016*/ public float   DeltaY;         // Change in y
/*0020*/ public int     AnimationAndDeltaHeading;   // See below for original
//       int    animation:10,       // animation
//              delta_heading:10,   // change in heading
//              padding0020:12;     // ***Placeholder (mostly 1)
/*0024*/ public float   XPos;           // x coord
/*0028*/ public float   ZPos;           // z coord
/*0032*/ public ushort  HeadingAndPadding;          // See below for original
 //      ushort heading:12,     // Directional heading
 //      padding0004:4;         // ***Placeholder
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 2)]
/*0034*/ public byte[]  Unknown0006;    // ***Placeholder
/*0036*/
        public int DeltaHeading
        {
            get { return (this.AnimationAndDeltaHeading << 12) >> 22; }
        }

        public float Heading
        {
            get { return Utility.EQ19ToFloat((this.HeadingAndPadding << 4) >> 4); }
        }

        public ushort Animation
        {
            get { return (ushort)((this.AnimationAndDeltaHeading << 22) >> 22); }
        }
    }

    /// <summary>Spawn position update.  Sent from server->client to update position of a spawn in zone (NPC or PC).</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct PlayerPositionUpdateServer
    {
/*0000*/ public ushort  SpawnId;
/*0002*/ public int     DeltaHeadingAndXPos;    // See below for original
//       sint32	delta_heading:10,   // change in heading
//              x_pos:19,           // x coord
//              padding0002:3;      // ***Placeholder
/*0006*/ public int     YPosAndAnimation;       // See below for original
 //      sint32	y_pos:19,           // y coord
 //             animation:10,       // animation
 //             padding0006:3;      // ***Placeholder
/*0010*/ public int     ZPosAndDeltaY;          // See below for original
//       sint32	z_pos:19,           // z coord
//              delta_y:13;         // change in y
/*0014*/ public int     DeltaXAndHeading;       // See below for original
//       sint32	delta_x:13,         // change in x
//              heading:12,         // heading
//              padding0014:7;      // ***Placeholder
/*0018*/ public int     DeltaZAndPadding;                 // See below for original
//       sint32	delta_z:13,         // change in z
//              padding0018:19;     // ***Placeholder
/*0022*/
        public int DeltaHeading
        {
            set { this.DeltaHeadingAndXPos |= (Utility.FloatToEQ13(value) & 0x3FF); }
        }

        public float XPos
        {
            //get { return Utility.EQ19ToFloat((this.DeltaHeadingAndXPos & 0x3FFFF8) >> 3); }
            set { this.DeltaHeadingAndXPos |= ((Utility.FloatToEQ19(value) << 10) & 0x1FFFFC00); }
        }

        public int Padding0002
        {
            set { this.DeltaHeadingAndXPos |= value << 29; }
        }

        public float YPos
        {
            set { this.YPosAndAnimation |= (Utility.FloatToEQ19(value) & 0x7FFFF); }
        }

        public ushort Animation
        {
            set { this.YPosAndAnimation |= ((value << 19) & 0x1FF80000); }
        }

        public int Padding0006
        {
            set { this.YPosAndAnimation |= value << 29; }
        }

        public float ZPos
        {
            set { this.ZPosAndDeltaY |= (Utility.FloatToEQ19(value) & 0x7FFFF); }
        }

        public float DeltaY
        {
            set { this.ZPosAndDeltaY |= Utility.FloatToEQ13(value) << 19; }
        }

        public float DeltaX
        {
            set { this.DeltaXAndHeading |= (Utility.FloatToEQ13(value) & 0x1FFF); }
        }

        public float Heading
        {
            set { this.DeltaXAndHeading |= ((Utility.FloatToEQ19(value) << 13) & 0x1FFE000); }
        }

        public int Padding0014
        {
            set { this.DeltaXAndHeading |= (value << 25); }
        }

        public float DeltaZ
        {
            set { this.DeltaZAndPadding |= (Utility.FloatToEQ13(value) & 0x1FFF); }
        }

        public int Padding0018
        {
            set { this.DeltaZAndPadding |= (value << 13); }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct ZonePoints
    {
        public int              Count;
        public ZonePointEntry[] ZonePointEntries; // Always add one extra to the end after all zonePoints

        public ZonePoints(int numZonePoints)
        {
            this.Count = numZonePoints;
            this.ZonePointEntries = new ZonePointEntry[numZonePoints + 1];
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct ZonePointEntry
    {
/*000*/ public  int     Iterator;
/*004*/	public  float	Y;
/*008*/	public  float	X;
/*012*/	public  float	Z;
/*016*/	public  float	Heading;
/*020*/	public  short	ZoneId;
/*022*/	public  short	ZoneInstance;   // LDoN instance
/*024*/
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class WearChange
    {
        public short    SpawnId;
        public short    Material;
        public uint     Color;
        //public byte     Blue;
        //public byte     Green;
        //public byte     Red;
        //public byte     UseTint;    // If there's a tint this is 0xFF
        public byte     WearSlotId;

        public override string ToString()
        {
            return string.Format("SpawnId:{0} Material:{1} Color:{2} WearSlotId: {3}", this.SpawnId, this.Material, this.Color, this.WearSlotId);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct ZoneDoor
    {
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 32)]
/*000*/ public byte[]  Name;        // Filename of Door
/*032*/ public float   YPos;        // y loc
/*036*/ public float   XPos;        // x loc
/*040*/ public float   ZPos;        // z loc
/*044*/ public float   Heading;
/*048*/ public int     Incline;     // rotates the whole door
/*052*/ public short   Size;        // 100 is normal, smaller number = smaller model
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 6)]
/*054*/ public byte[]  Unknown054;
/*060*/ public byte    DoorId;      // door's id #
/*061*/ public byte    OpenType;
/*  Open types:
 * 66 = PORT1414 (Qeynos)
 * 55 = BBBOARD (Qeynos)
 * 100 = QEYLAMP (Qeynos)
 * 56 = CHEST1 (Qeynos)
 * 5 = DOOR1 (Qeynos)   */
/*062*/ public byte    StateAtSpawn;
/*063*/ public byte    InvertState; // if this is 1, the door is normally open
/*064*/ public int     DoorParam;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 12)]
/*068*/ public byte[]  Unknown068;  // mostly 0s, the last 3 bytes are something tho
/*080*/
        internal void Init()
        {
            this.Name = new byte[32];
            this.Unknown068 = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01 };
        }
    }

    /// <summary>Used with ClickDoor OpCode - when a door is click by the client.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct ClickDoor
    {
/*000*/ public uint     DoorId;
/*004*/ public byte     PickLockSkill;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 3)]
/*005*/ public byte[]   Unknown05;
/*008*/ public uint     ItemId;
/*012*/ public ushort   PlayerId;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 2)]
/*014*/ public byte[]   Unknown14;
/*016*/ 
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct MoveDoor
    {
        public byte DoorId;
        public byte Action;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct ZoneChange
    {
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
        public byte[]   CharName;
        public ushort   ZoneID;
        public ushort   InstanceID;
        public float    Y;
        public float    X;
        public float    Z;
        public uint     ZoneReason;     // 0x0A might be death
        public int      Success;        // =0 client->server, =1 server->client, -X=specific error

        public ZoneChange(string charName)
        {
            this.CharName = new byte[64];
            Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(charName), 0, this.CharName, 0, charName.Length);
            this.ZoneID = 0;
            this.InstanceID = 0;
            this.Y = 0.0F;
            this.X = 0.0F;
            this.Z = 0.0F;
            this.ZoneReason = 0;
            this.Success = 0;
        }

        public void Init()
        {
            this.CharName = new byte[64];
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct CancelTrade
    {
        public uint FromID;
        public uint Action;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class MoveItem
    {
        public uint FromSlot;
        public uint ToSlot;
        public uint NumberInStack;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class ManaStaChange
    {
        public uint NewMana;
        public uint NewEndurance;
        public uint SpellId;
        public uint Unknown12;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class BookRequest
    {
        public byte Window;     // Where to display the text (0xFF means new window)
        public byte Type;       // 0=scroll, 1=book, 2=item info.. prolly others
        public uint InvSlot;    // Used in SoF and later clients
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 20)]
        public byte[] TextFile = new byte[20];
    }

    /// <summary>Use ` as a newline character in the text.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class BookText
    {
        public byte Window;     // Where to display the text (0xFF means new window)
        public byte Type;       // 0=scroll, 1=book, 2=item info.. prolly others
        //public uint InvSlot;    // Used in SoF and later clients
        // Last member is a variable length string for the text of the book
    }

    /// <summary>Embedded variable length string make this packet a pain to serialize nice and clean.</summary>
    internal class SpecialMessage
    {
        private byte[] _header = {0x00, 0x00, 0x00};    // 04 04 00 (for #emote style msg)
        private uint _msgType;          // Represents the color of text (see MessageType enum)
        private uint _targetSpawnId;    // Who it is being said to
        private string _sayer;          // Who is the source of the message
        private byte[] _unknown12 = {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};
        private string _msgText;    // The message itself

        #region Properties
        public byte[] Header
        {
            get { return _header; }
            set { _header = value; }
        }

        public uint MsgType
        {
            get { return _msgType; }
            set { _msgType = value; }
        }

        public uint TargetSpawnId
        {
            get { return _targetSpawnId; }
            set { _targetSpawnId = value; }
        }

        public string Sayer
        {
            get { return _sayer; }
            set { _sayer = value; }
        }

        public string MsgText
        {
            get { return _msgText; }
            set { _msgText = value; }
        }
        #endregion

        internal SpecialMessage(uint msgType, string msgText)
            : this(msgType, 0, string.Empty, msgText) { }

        internal SpecialMessage(uint msgType, uint targetSpawnId, string sayer, string msgText)
        {
            _msgType = msgType;
            _targetSpawnId = targetSpawnId;
            _sayer = sayer;
            _msgText = msgText;
        }

        internal byte[] Serialize()
        {
            int size = 23 + _sayer.Length + 1 + _msgText.Length + 1;    // + 1's are for trailing null terminators
            byte[] msgBytes = new byte[size];

            Buffer.BlockCopy(_header, 0, msgBytes, 0, 3);
            Buffer.BlockCopy(BitConverter.GetBytes(_msgType), 0, msgBytes, 3, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(_targetSpawnId), 0, msgBytes, 7, 4);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(_sayer + '\0'), 0, msgBytes, 11, _sayer.Length + 1);
            //Buffer.BlockCopy(_unknown12, 0, msgBytes, 11 + _sayer.Length + 1, 12);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(_msgText + '\0'), 0, msgBytes, 11 + _sayer.Length + 1 + 12, _msgText.Length + 1);

            return msgBytes;
        }
    }

    /// <summary>For sending predefined messages to the client.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct SimpleMessage
    {
        public uint StringID;
        public uint Color;
        public uint Unknown8;
    }

    internal class FormattedMessage
    {
        private uint _stringId;
        private uint _msgType;      // Represents the color of text (see MessageType enum)
        private string _msgText;    // The message itself

        #region Properties
        public uint StringId
        {
            get { return _stringId; }
            set { _stringId = value; }
        }

        public uint MsgType
        {
            get { return _msgType; }
            set { _msgType = value; }
        }

        public string MsgText
        {
            get { return _msgText; }
            set { _msgText = value; }
        }
        #endregion

        internal byte[] Serialize()
        {
            int size = 12 + _msgText.Length + 1;    // 12 for two known int and one unknown int.  + 1 is for trailing null terminator
            byte[] msgBytes = new byte[size];

            Buffer.BlockCopy(BitConverter.GetBytes(_stringId), 0, msgBytes, 4, 4);   // First four bytes are an unknown int value
            Buffer.BlockCopy(BitConverter.GetBytes(_msgType), 0, msgBytes, 8, 4);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(_msgText + '\0'), 0, msgBytes, 12, _msgText.Length + 1);
            
            return msgBytes;
        }
    }

    internal class FormattedMessageBytes
    {
        private uint _stringId;
        private uint _msgType;      // Represents the color of text (see MessageType enum)
        private byte[] _msgBytes;    // The message bytes

        #region Properties
        public uint StringId
        {
            get { return _stringId; }
            set { _stringId = value; }
        }

        public uint MsgType
        {
            get { return _msgType; }
            set { _msgType = value; }
        }

        public byte[] MsgBytes
        {
            get { return _msgBytes; }
            set { _msgBytes = value; }
        }
        #endregion

        internal byte[] Serialize()
        {
            int size = 12 + _msgBytes.Length + 1;   // + 1 is for trailing null terminator
            byte[] msgBytes = new byte[size];

            Buffer.BlockCopy(BitConverter.GetBytes(_stringId), 0, msgBytes, 4, 4);   // First four bytes are an unknown int value
            Buffer.BlockCopy(BitConverter.GetBytes(_msgType), 0, msgBytes, 8, 4);
            Buffer.BlockCopy(_msgBytes, 0, msgBytes, 12, _msgBytes.Length);

            return msgBytes;
        }
    }

    /// <summary>Packet for those entites that don't need an exact HP count.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal struct HPUpdateRatio
    {
        public short SpawnID;
        public byte HPRatio;
    }

    /// <summary>Packet for those entities that need an exact HP count.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class HPUpdateExact
    {
        public uint CurrentHP;
        public int  MaxHP;
        public short SpawnID;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct ClientTarget
    {
        public uint TargetID;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct Animation
    {
        public ushort SpawnID;
        public byte Action;
        public byte Value;
    }

    /// <summary>This packet causes the "You have been struck" and the regular melee message like "You try to pierce", etc.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class CombatDamage
    {
        public ushort TargetId;
        public ushort SourceId;
        public byte DamageType; // Slashing, etc.  231 (0xE7) for spells
        public ushort SpellId;
        public uint Damage;
        public uint Unknown11;
        public uint Sequence;   // Matches the Action packet's sequence property (probably to tie them together)
        public uint Unknown19;
    }

    /// <summary>Used upon spawn death blow.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class Death
    {
        public uint SpawnId;
        public uint KillerId;
        public uint CorpseId;
        public uint BindToZoneId;
        public uint SpellId;
        public uint AttackSkillId;
        public uint Damage;
        public uint Unknown028;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal struct XpUpdate
    {
        public uint XP;         // Current experience ratio from 0 to 330
        public uint Unknown04;  // Marked as possibly AAXP in orig emu but there is a separate packet for that
    }

    /// <summary>Sends a little graphic on level up.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class LevelAppearance
    {
        public uint SpawnID;
        public uint Parm1;
        public uint Value1a;
        public uint Value1b;
        public uint Parm2;
        public uint Value2a;
        public uint Value2b;
        public uint Parm3;
        public uint Value3a;
        public uint Value3b;
        public uint Parm4;
        public uint Value4a;
        public uint Value4b;
        public uint Parm5;
        public uint Value5a;
        public uint Value5b;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class LevelUpdate
    {
        public uint NewLevel;   // New Level
        public uint OldLevel;   // Old Level
        public uint XP;         // Current XP
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class MoneyOnCorpse
    {
        public byte Response;   // 0 = someone else is, 1 = OK, 2 = not at this time
        public byte Unknown01 = 0x5a;
        public byte Unknown02 = 0x40;
        public byte Unknown03 = 0;
        public uint Platinum;
        public uint Gold;
        public uint Silver;
        public uint Copper;
    }

    internal class ItemPacket
    {
        private uint _itemPacketType;   // Type of item packet (see ItemPacketType enum)
        private string _serItem;        // The message itself

        internal ItemPacket(ItemPacketType packetType, string serializedItem)
        {
            _itemPacketType = (uint)packetType;
            _serItem = serializedItem;
        }

        internal byte[] Serialize()
        {
            int size = 4 + _serItem.Length + 1;     // + 1 is for trailing null terminator
            byte[] msgBytes = new byte[size];

            Buffer.BlockCopy(BitConverter.GetBytes(_itemPacketType), 0, msgBytes, 0, 4);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(_serItem), 0, msgBytes, 4, _serItem.Length);
            
            return msgBytes;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class LootingItem
    {
        public uint LooteeId;
        public uint LooterId;
        public ushort SlotIdx;  // Not really the slot ID, rather the index into the items sent to the client
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 2)]
        public byte[] Unknown12 = { 0x00, 0x00 };
        public uint AutoLoot;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class BecomeCorpse
    {
        public uint SpawnId;
        public float Y;
        public float X;
        public float Z;
    }

    internal class ZonePlayerToBind
    {
        private uint _bindZoneId;
        private float _x;
        private float _y;
        private float _z;
        private float _heading;
        private string _zoneName;   // Null terminated. Text the titanium client displays on player death. Can send zone name or whatever. Shows in client as "Return to xxx, please wait..."

        internal ZonePlayerToBind(uint bindZoneId, float x, float y, float z, float heading, string zoneName)
        {
            _bindZoneId = bindZoneId;
            _x = x;
            _y = y;
            _z = z;
            _heading = heading;
            _zoneName = zoneName;
        }

        internal byte[] Serialize()
        {
            int size = 20 + _zoneName.Length + 1;   // 20 known sizes of members... + 1 is for trailing null terminator
            byte[] msgBytes = new byte[size];

            Buffer.BlockCopy(BitConverter.GetBytes(_bindZoneId), 0, msgBytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(_x), 0, msgBytes, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(_y), 0, msgBytes, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(_z), 0, msgBytes, 12, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(_heading), 0, msgBytes, 16, 4);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(_zoneName + '\0'), 0, msgBytes, 20, _zoneName.Length + 1);

            return msgBytes;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class EnvDamage
    {
        public uint EntityId;
        public ushort Unknown4;
        public uint DamageAmount;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 12)]
        public byte[] Unknown10 = new byte[12];
        public byte DamageType;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
        public byte[] Unknown23 = new byte[4];
        public ushort Constant = 0xFFFF;
        public ushort Unknown29;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class DeleteItem
    {
        public uint FromSlotId;
        public uint ToSlotId;
        public uint NumberInStack;
    }

    internal class ChannelMessage
    {
        private string _targetName;     // max 64 char
        private string _speakerName;    // max 64 char
        private int _langId;            // language
        private int _chanId;            // chat channel
        private int _langSkill;         // player's skill in the language
        private string _msg;            // the spoken message (null terminated)

        #region Properties
        public string TargetName
        {
            get { return _targetName; }
            set
            {
                if (value.Length > 64)
                    throw new ArgumentOutOfRangeException("TargetName", value, "Target name can be a max of 64 chars in length.");

                _targetName = value;
            }
        }

        public string SpeakerName
        {
            get { return _speakerName; }
            set
            {
                if (value.Length > 64)
                    throw new ArgumentOutOfRangeException("SpeakerName", value, "Speaker name can be a max of 64 chars in length.");

                _speakerName = value;
            }
        }

        public int LanguageId
        {
            get { return _langId; }
            set { _langId = value; }
        }

        public int ChannelId
        {
            get { return _chanId; }
            set { _chanId = value; }
        }

        public int LanguageSkill
        {
            get { return _langSkill; }
            set { _langSkill = value; }
        }

        public string Message
        {
            get { return _msg; }
            set { _msg = value; }
        }
        #endregion

        internal byte[] Serialize()
        {
            int size = 64 + 64  // The two names
                + 12            // three ints
                + 8             // an unknown two element int array
                + _msg.Length + 1;    // + 1 is for trailing null terminator
            byte[] msgBytes = new byte[size];

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(_targetName), 0, msgBytes, 0, 64);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(_speakerName), 0, msgBytes, 64, 64);
            Buffer.BlockCopy(BitConverter.GetBytes(_langId), 0, msgBytes, 128, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(_chanId), 0, msgBytes, 132, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(_langSkill), 0, msgBytes, 144, 4);  // 8 bytes of unknown comes before this
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(_msg + '\0'), 0, msgBytes, 148, _msg.Length + 1);

            return msgBytes;
        }

        static internal ChannelMessage Deserialize(byte[] rawBytes)
        {
            ChannelMessage cm = new ChannelMessage();

            cm.TargetName = Encoding.ASCII.GetString(rawBytes, 0, 64);
            cm.SpeakerName = Encoding.ASCII.GetString(rawBytes, 64, 64);
            cm.LanguageId = BitConverter.ToInt32(rawBytes, 128);
            cm.ChannelId = BitConverter.ToInt32(rawBytes, 132);
            cm.LanguageSkill = BitConverter.ToInt32(rawBytes, 144);
            cm.Message = Encoding.ASCII.GetString(rawBytes, 148, rawBytes.Length - 148);  // easy since it's the last element in the byte array

            return cm;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class RequestClientZoneChange
    {
        public ushort ZoneId;
        public ushort InstanceId;
        public float Y;
        public float X;
        public float Z;
        public float Heading;
        public uint Type;       // Whatever you send here, the client sends back to server in ZoneChange.ZoneReason
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal struct SkillUpdate
    {
        public uint SkillId;
        public uint SkillValue;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class Consider
    {
        public uint PlayerId;
        public uint TargetId;
        public uint Faction;
        public uint Level;
        public int CurrentHP;
        public int MaxHP;
        public byte PVP;    // PVP Con flag (0 or 1)
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 3)]
        public byte[] Unknown3 = new byte[3];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class MemorizeSpell
    {
        public uint SlotId;     // Spot in the spellbook (memorized slot)
        public uint SpellId;    // Corresponds to SpellId field in db & client spell file
        public uint Scribing;   // 1 = memorizing, 0 = scribing to book, 2 if un-memming
        public uint Unknown12;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class DeleteSpell
    {
        public short SpellSlot;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 2)]
        public byte[] Unknown02 = new byte[2];
        public byte Success;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 3)]
        public byte[] Unknown06 = new byte[3];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class CastSpell
    {
        public uint SpellSlotId;        // Type of spell being activated (ability, item, potion or disc.)
        public uint SpellId;
        public uint InventorySlotId;    // slot for clicky item, 0xFFFF is a normal cast
        public uint TargetId;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 4)]
        public byte[] Unknown = new byte[4];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class ManaChange
    {
        public uint NewMana;        // New mana amount
        public uint Stamina;
        public uint SpellId;
        public uint Unknown12;
    }

    /// <summary>Usage of this packet is the same as a formatted message and channel message.</summary>
    internal class InterruptCast
    {
        public uint SpawnId { get; set; }
        public uint MessageId { get; set; }
        public string Message { get; set; }

        internal byte[] Serialize()
        {
            int size =  8               // Two uints
                + Message.Length + 1;   // + 1 is for trailing null terminator
            byte[] msgBytes = new byte[size];

            Buffer.BlockCopy(BitConverter.GetBytes(this.SpawnId), 0, msgBytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(this.MessageId), 0, msgBytes, 4, 4);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(this.Message + '\0'), 0, msgBytes, 8, this.Message.Length + 1);

            return msgBytes;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal class BeginCast
    {
        public ushort CasterId;
        public ushort SpellId;
        public uint CastTime;   // In miliseconds
    }
}