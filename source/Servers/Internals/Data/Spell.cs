using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using EQEmulator.Servers.Internals.Entities;

namespace EQEmulator.Servers.Internals.Data
{
    internal enum SpellMemorize : uint
    {
        Scribing = 0,
        Memorize = 1,
        Forget   = 2,
        Spellbar = 3
    }

    /// <summary>
    /// Spell slot IDs: 0-8 are the nine spell gems, the rest are listed below
    /// </summary>
    internal enum SpellSlot : uint
    {
        Ability = 9,
        UseItem = 10,
        PotionBelt = 11,
        Discipline = 0xFFFFFFFF
    }

    [Flags]
    internal enum SpellType : ushort
    {
        Nuke            = 1,
        Heal            = 2,
        Root            = 4,
        Buff            = 8,
        Escape          = 16,
        Pet             = 32,
        Lifetap         = 64,
        Snare           = 128,
        DOT             = 256,
        Dispel          = 512,
        InCombatBuff    = 1024,
        Mez             = 2048,
        Charm           = 4096,
        Any             = 0xFFFF,
        Detrimental     = (Nuke | Root | Lifetap | Snare | DOT | Dispel | Mez | Charm),
        Beneficial      = (Heal | Buff | Escape | Pet | InCombatBuff)
    }

    internal enum EffectIndex : ushort
    {
        None        = 0,
        Calm        = 12,   // Lull and Alliance Spells
        DispelSight = 14,   // Dispel spells and spells like Bind Sight
        MemoryBlur  = 27,
        CalmSong    = 43    // Lull and Alliance Spells
    }

    internal enum SpellTargetType
    {
	    TargetOptional  = 0x01,
	    AEClientV1      = 0x02,
	    GroupTeleport   = 0x03,
	    AECaster        = 0x04,
	    Target          = 0x05,
	    Self            = 0x06,
	    AETarget        = 0x08,
	    Animal          = 0x09,
	    Undead          = 0x0a,
	    Summoned        = 0x0b,
	    Tap             = 0x0d,
	    Pet             = 0x0e,
	    Corpse          = 0x0f,
	    Plant           = 0x10,
	    Giant           = 0x11, // special giant
	    Dragon          = 0x12, // special dragon
	    TargetAETap     = 0x14,
	    UndeadAE        = 0x18,
	    SummonedAE      = 0x19,
	    AECaster2       = 0x20, // ae caster hatelist maybe?
	    HateList        = 0x21,
	    LDoNChestCursed = 0x22,
	    Muramite        = 0x23, // only works on special muramites
	    AreaClientOnly  = 0x24,
	    AreaNPCOnly     = 0x25,
	    SummonedPet     = 0x26,
	    GroupNoPets     = 0x27,
	    AEBard          = 0x28,
	    Group           = 0x29,
	    Directional     = 0x2a, // ae around this target between two angles
	    GroupClientAndPet = 0x2b,
	    Beam            = 0x2c  // like directional but facing in front of you always
    }

    internal enum SpellEffectType
    {
        CurrentHP           = 0,    // Heals and nukes, repeates every tic if in a buff
        ArmorClass		    = 1,
        ATK				    = 2,
        MovementSpeed	    = 3,    // SoW, SoC, etc
        STR				    = 4,
        DEX				    = 5,
        AGI				    = 6,
        STA				    = 7,
        INT				    = 8,
        WIS				    = 9,
        CHA				    = 10,   // Often used as a spacer, who knows why
        AttackSpeed		    = 11,
        Invisibility	    = 12,
        SeeInvis		    = 13,
        WaterBreathing	    = 14,
        CurrentMana		    = 15,
        Lull			    = 18,	// see SE_Harmony
        AddFaction		    = 19,	// Alliance line
        Blind			    = 20,
        Stun			    = 21,
        Charm			    = 22,
        Fear			    = 23,
        Stamina			    = 24,	// Invigor and such
        BindAffinity	    = 25,
        Gate			    = 26,	// Gate to bind point
        CancelMagic		    = 27,
        InvisVsUndead	    = 28,
        InvisVsAnimals	    = 29,
        ChangeFrenzyRad	    = 30,
        Mez				    = 31,
        SummonItem		    = 32,
        SummonPet		    = 33,
        DiseaseCounter	    = 35,
        PoisonCounter	    = 36,
        DivineAura		    = 40,
        Destroy			    = 41,	// Disintegrate, Banishment of Shadows
        ShadowStep		    = 42,
        Lycanthropy		    = 44,
        ResistFire		    = 46,
        ResistCold		    = 47,
        ResistPoison	    = 48,
        ResistDisease	    = 49,
        ResistMagic		    = 50,
        SenseDead		    = 52,
        SenseSummoned	    = 53,
        SenseAnimals	    = 54,
        Rune			    = 55,
        TrueNorth		    = 56,
        Levitate		    = 57,
        Illusion		    = 58,
        DamageShield	    = 59,
        Identify		    = 61,
        WipeHateList	    = 63,
        SpinTarget		    = 64,
        InfraVision		    = 65,
        UltraVision		    = 66,
        EyeOfZomm		    = 67,
        ReclaimPet		    = 68,
        TotalHP			    = 69,
        NecPet			    = 71,
        BindSight		    = 73,
        FeignDeath		    = 74,
        VoiceGraft		    = 75,
        Sentinel		    = 76,
        LocateCorpse	    = 77,
        AbsorbMagicAtt	    = 78,	// rune for spells
        CurrentHPOnce	    = 79,	// Heals and nukes, non-repeating if in a buff
        Revive			    = 81,	//resurrect
        SummonPC		    = 82,
        Teleport		    = 83,
        TossUp			    = 84,	// Gravity Flux
        WeaponProc		    = 85,	// i.e. Call of Fire
        Harmony			    = 86,	// what is SE_Lull??,. "Reaction Radius"
        MagnifyVision	    = 87,	// Telescope
        Succor			    = 88,	// Evacuate/Succor lines?
        ModelSize		    = 89,	// Shrink, Growth
        Cloak			    = 90,	// some kind of focus effect?
        SummonCorpse	    = 91,
        Calm			    = 92,	// Hate modifier. Enrageing blow
        StopRain		    = 93,	// Wake of Karana
        NegateIfCombat	    = 94,	// Component of Spirit of Scale
        Sacrifice		    = 95,
        Silence			    = 96,	// Cacophony
        ManaPool		    = 97,
        AttackSpeed2	    = 98,	// Melody of Ervaj
        Root			    = 99,
        HealOverTime	    = 100,
        CompleteHeal	    = 101,
        Fearless		    = 102,	// Valiant Companion
        CallPet			    = 103,	// Summon Companion
        Translocate		    = 104,
        AntiGate		    = 105,	// Translocational Anchor
        SummonBSTPet	    = 106,	// neotokyo: added BST pet support
        Familiar		    = 108,
        SummonItemIntoBag   = 109,	// Summon Jewelry Bag - summons stuff into container
        ResistAll		    = 111,
        CastingLevel	    = 112,
        SummonHorse         = 113,
        ChangeAggro		    = 114,	// chanter spells Horrifying Visage ...
        Hunger			    = 115,	// Song of Sustenance
        CurseCounter	    = 116,
        MagicWeapon		    = 117,	// Magical Monologue
        SingingSkill	    = 118,	// Amplification
        AttackSpeed3	    = 119,	// Frenzied Burnout
        HealRate		    = 120,	// Packmaster's Curse - not sure what this is
        ReverseDS		    = 121,
        Screech			    = 123,	// Form of Defense
        ImprovedDamage	    = 124,
        ImprovedHeal	    = 125,
        SpellResistReduction= 126,  //not implemented
        IncreaseSpellHaste	= 127,
        IncreaseSpellDuration   = 128,
        IncreaseRange		= 129,
        SpellHateMod		= 130,
        ReduceReagentCost	= 131,
        ReduceManaCost		= 132,
        LimitMaxLevel		= 134,
        LimitResist			= 135,
        LimitTarget			= 136,
        LimitEffect			= 137,
        LimitSpellType		= 138,
        LimitSpell			= 139,
        LimitMinDur			= 140,
        LimitInstant		= 141,
        LimitMinLevel		= 142,
        LimitCastTime		= 143,
        Teleport2			= 145,	// Banishment of the Pantheon
        PercentalHeal		= 147,
        // solar: 
        // base is the effectid the command applies to
        // max is the value to check against.. not sure if there's a formula for this
        // calc is the effect slot number plus 200
        StackingCommandBlock= 148,
        StackingCommandOverwrite = 149,
        DeathSave			= 150,
        SuspendPet			= 151,	// Suspend Minion. base1: 0 = lose buffs & equip, 1 = keep buffs & equip
        TemporaryPets		= 152,	// Swarm of Fear III
        BalanceHP			= 153,	// Divine Arbitration
        DispelDetrimental	= 154,
        IllusionCopy		= 156,	// Deception
        SpellDamageShield	= 157,	// Petrad's Protection
        Reflect				= 158,
        AllStats			= 159,	// Aura of Destruction
        MakeDrunk			= 160,
        MitigateSpellDamage	= 161,	//not implemented rune type, with max value
        MitigateMeleeDamage	= 162,	//not implemented rune type, with max value
        NegateAttacks		= 163,
        AppraiseLDonChest	= 164,
        DisarmLDoNTrap		= 165,
        UnlockLDoNChest		= 166,
        PetPowerIncrease	= 167,	
        MeleeMitigation		= 168,	//not implemented, unlimited for duration
        CriticalHitChance	= 169,
        SpellCritChance		= 170,	//not implemented, +% to crit
        CrippBlowChance		= 171,
        AvoidMeleeChance	= 172,
        RiposteChance		= 173,
        DodgeChance			= 174,
        ParryChance			= 175,
        DualWieldChance		= 176,
        DoubleAttackChance	= 177,
        MeleeLifetap		= 178,
        AllInstrunmentMod	= 179,
        ResistSpellChance	= 180,
        ResistFearChance	= 181,
        HundredHands		= 182,
        MeleeSkillCheck		= 183,
        HitChance			= 184,
        DamageModifier		= 185,
        MinDamageModifier	= 186,
        IncreaseBlockChance	= 188,	//not implemented
        CurrentEndurance	= 189,
        EndurancePool		= 190,	//not implemented
        Amnesia				= 191,	//Amnesia (Silence vs Melee Effect)
        Hate2				= 192,	//not implemented
        SkillAttack			= 193,
        FadingMemories		= 194,
        StunResist			= 195,
        Strikethrough		= 196,
        SkillDamageTaken	= 197,
        CurrentEnduranceOnce= 198,
        Taunt				= 199,	//Flutter Flight (9480). % chance to taunt the target
        ProcChance			= 200,
        RangedProc			= 201,
        IllusionOther		= 202,	
        MassGroupBuff		= 203,	
        GroupFearImmunity	= 204,
        Rampage				= 205,
        AETaunt				= 206,
        FleshToBone			= 207,
        FadingMemories2		= 209,
        PetShield			= 210,	//per lucy, not implemented
        AEMelee				= 211,	//per lucy, not implemented
        ProlongedDestruction= 212,	//per lucy, not implemented
        MaxHPChange 		= 214,	//Grace of the Order, Plague of Hulcror, not implemented
        Accuracy			= 216,	//not implemented
        PetCriticalHit		= 218, //aa effect
        SlayUndead			= 219,	//not implemented
        SkillDamageAmount	= 220,	//not implemented
        Packrat				= 221, //aa effect
        GiveDoubleRiposte	= 224, //aa effect
        GiveDoubleAttack	= 225,	//not implemented
        TwoHandBash			= 226, //aa effect, bash with a 2h sword
        ReduceSkillTimer	= 227,	//not implemented
        PersistantCasting	= 229, //aa effect
        DivineSave			= 232,	//not implemented (base == % chance on death to insta-res)
        ChannelingChance	= 235, //Appears to only be used in AAs
        GivePetGroupTarget	= 237, //aaeffect, makes pets targetable by group spells
        SetBreathLevel		= 246, //aa effect
        SecondaryForte		= 248, //aa effect, lets you gain a 2nd forte, gives you a 2nd specialize skill that can go past 50 to 100
        SecondaryDmgInc		= 249, //aa effect, sinister strikes
        Blank				= 254,
        PetDiscipline		= 257, //aa effect /pet hold
        TripleBackstab		= 258, //not implemented
        CombatStability		= 259, //aa effect
        AddInstrumentMod	= 260,
        RaiseStatCap		= 262, //added 10/21/08
        TradeSkillMastery	= 263,	//lets you raise more than one tradeskill above master.
        HastenedAASkill		= 264, //aa skill
        MasteryofPast		= 265, //aa skill, makes impossible to fizzle spell of base[x] level
        ExtraAttackChance	= 266, //not implemented
        PetDiscipline2		= 267, //aa effect /pet focus, /pet no cast
        ReduceTradeskillFail= 268, //aa effect, reduces chance to fail with given tradeskill by a percent chance
        BaseMovementSpeed	= 271, //mods basemove speed, doesn't stack with other move mods, aa effect
        CastingLevel2		= 272,
        CriticalDoTChance	= 273,	//not implemented
        CriticalHealChance	= 274,	//not implemented
        Ambidexterity		= 276,  //aa effect
        FinishingBlow	    = 278,  //aa effect
        Flurry				= 279,	//not implemented
        PetFlurry			= 280,
        NimbleEvasion		= 285,	//base1 = 100 for max
        SpellDamage			= 286,	//not implemented. adds direct spell damage
        FocusCombatDurationMod  = 287,
        ImprovedSpellEffect	= 289, //This effect turns into this? base1 = new spell id, happens when spell wears off
        Purify				= 291, //not implemented
        CriticalSpellChance	= 294, //not implemented
        SpellVulnerability	= 296,	//not implemented, base % increase in incoming spell damage
        Empathy				= 297, //some kind of damage focus effect, maybe defensive?
        ChangeHeight		= 298,	//not implemented
        WakeTheDead			= 299,
        Doppelganger		= 300,
        OffhandRiposteFail	= 304, //aa effect, enemy cannot riposte offhand attacks
        MitigateDamageShield= 305, //not implemented
        WakeTheDead2		= 306, //not implemented
        Appraisal			= 307, //not implemented
        SuspendMinion		= 308,
        YetAnotherGate		= 309, //not implemented, spell 5953
        ReduceReuseTimer	= 310, //not implemented
        NoCombatSkills		= 311, //not implemented
        Sanctuary			= 312, //not implemented
        Invisibility2		= 314, //not implemented
        InvisVsUndead2		= 315, //not implemented
        ItemManaRegenCapIncrease= 318, //aa effect increases amount of mana regen you can gain via items
        CriticalHealOverTimer   = 319, //not implemented
        ReduceHate			= 321, //not implemented
        GateToHomeCity		= 322,
        DefensiveProc		= 323,
        HPToMana			= 324, //not implemented
        SpellSlotIncrease	= 326, //aa effect, increases your spell slot availability
        MysticalAttune		= 327, //AA effect, increases amount of buffs that a player can have
        DelayDeath			= 328, //AA effect, increases how far you can fall below 0 hp before you die
        ManaAbsorbPercentDamage = 329, //not implemented
        CriticalDamageMob	= 330,	//not implemented
        Salvage				= 331, //chance to recover items that would be destroyed in failed tradeskill combine
        SummonToCorpse		= 332, //not implemented
        EffectOnFade		= 333, //not implemented
        BardAEDot			= 334,	//needs a better name (spell id 703 and 730)
        Unknown335			= 335,	//not implemented. blocks next spell base1 times, then the spell fades after it reaches 0 (like Rune, but for spell types). Puratus (8494), Influence of Rage (8762) & Atta's Depuration (9815)
        PercentXPIncrease	= 337,	//not implemented
        SummonAndResAllCorpses  = 338,	//not implemented
        TriggerOnCast		= 339,	//not implemented
        SpellTrigger     	= 340,	//chance to trigger spell
        ImmuneFleeing		= 342,	//not implemented
        InterruptCasting	= 343,	//not implemented. % chance to interrupt spells being cast every tic. Cacophony (8272)
        Unknown348			= 348,	//not implemented. looks like a rune based on how many times you cast a spell (cast a spell, decrease by 1. 0 = buff wears off)
        ManaBurn			= 350,	//not implemented. no idea on the calculation
										        //from http://everquest.allakhazam.com/db/spell.html?spell=8452;page=1;howmany=50#m1184016444141306266 for Mana Blaze (base1 = 9000):
										        //"As a wizard with 1k AA's (all damage) and 13k mana, I always land this AA for 36k. It only seems to use about 10k mana."
        PersistentEffect    = 351,	//not implemented. creates a trap/totem that casts a spell (spell id + base1?) when anything comes near it. can probably make a beacon for this
        Unknown352			= 352,	//not sure. looks to be some type of invulnerability? Test ITC (8755)
        Unknown353			= 353,	//not sure. looks to be some type of invulnerability? Test ISC (8756)
        Unknown354			= 354,	//not sure. looks to be some type of invulnerability? Test DAT (8757)
        Unknown355			= 355,	//not sure. looks to be some type of invulnerability? Test LT (8758)
        CurrentManaOnce		= 358,	//not implemented. increase/decrease mana once, like SE_CurrentHPOnce & SE_CurrentEnduranceOnce
        SpellOnKill			= 360,	//not implemented. has a base1 % to cast spell base2 when you kill a "challenging foe" (>= max?)
        Unknown361			= 361,	//not sure. looks to be same as SpellOnKill, except for detrimental spells only? Test Proc 2 (9407)
        SpellOnDeath		= 365,	//not implemented. casts base2 spell on the originator of the spell base1 % of the time when the person it was cast on dies. have to be >= max (level) for it to work?
        Unknown366			= 366,	//not sure. assume it has something to do with Corruption, maybe counters? wasn't implemented until Serpent's Spine, so not a big deal right now. Corr Test 1 (9428)
        AddBodyType			= 367,	//not implemented. adds body type of base1 so it can be affected by spells that are limited to that type (Plant, Animal, Undead, etc)
        FactionMod			= 368,	//not implemented. increases faction with base1 (faction id, live won't match up w/ ours) by base2
        CorruptionCounter	= 369,	//not implemented. wasn't added until Serpent's Spine, so we can't really do much w/ it
        ResistCorruption	= 370,	//not implemented. ditto
        InhibitMeleeAttacks	= 371,     //some type of melee slow
        CastOnWearoff		    = 373,   //Casts this spell on target when the buff wears off the target
        ApplyEffect			    = 374,  //Also casts these spells on the target when this spell lands
        BossSpellTrigger	    = 377,	//some sort of boss encounter effect, spell is cast if something happens
        ShadowStepDirectional   = 379,   //Shadowstep in a certain direction
        Knockdown			    = 380,   //small knock back + stun or feign?
        BlockDS				    = 382,  //something to do with blocking a % of certain ds?
        SympatheticProc		    = 383,   //focus on items that has chance to proc a spell when you cast
        Twincast			    = 399   //cast 2 spells for every 1
    }

    internal enum DamageShieldType
    {
        Decay       = 244,
        Chilled     = 245,
        Freezing    = 246,
        Torment     = 247,
        Burn        = 248,
        Thorns      = 249
    }

    internal enum CastActionType
    {
        SingleTarget,   // Causes effect to caster's target
        AETarget,       // Causes effect in AERange of caster's target + target
        AECaster,       // Causes effect in AERange of caster
        GroupSpell,     // Causes effect to caster and thier group
        CAHateList,     // Causes effect to all people on caster's hate list within some range
        Unknown
    }

    public partial class Spell
    {
        public const uint BLANK_SPELL = 0xFFFFFFFF;
        public const uint LEECH_TOUCH = 2766;
        public const uint LAY_ON_HANDS = 87;
        public const uint HARM_TOUCH = 88;
        public const uint HARM_TOUCH2 = 2821;
        public const uint IMP_HARM_TOUCH = 2774;
        public const uint NPC_HARM_TOUCH = 929;
        private const int EFFECT_COUNT = 12;

        private List<SpellEffect> _effects = new List<SpellEffect>(EFFECT_COUNT);

        partial void OnLoaded()
        {
            // Load up some convenience lists (source properties will be marked private)
            _effects[0] = new SpellEffect()
            {
                Base = _EffectBaseValue1 ?? 0,
                Limit = _EffectLimitValue1 ?? 0,
                Max = _EffectMaxValue1 ?? 0,
                Formula = _SpellValueFormula1 ?? 0,
                TypeId = _EffectId1 ?? 0
            };
            _effects[1] = new SpellEffect()
            {
                Base = _EffectBaseValue2 ?? 0,
                Limit = _EffectLimitValue2 ?? 0,
                Max = _EffectMaxValue2 ?? 0,
                Formula = _SpellValueFormula2 ?? 0,
                TypeId = _EffectId2 ?? 0
            };
            _effects[2] = new SpellEffect()
            {
                Base = _EffectBaseValue3 ?? 0,
                Limit = _EffectLimitValue3 ?? 0,
                Max = _EffectMaxValue3 ?? 0,
                Formula = _SpellValueFormula3 ?? 0,
                TypeId = _EffectId3 ?? 0
            };
            _effects[3] = new SpellEffect()
            {
                Base = _EffectBaseValue4 ?? 0,
                Limit = _EffectLimitValue4 ?? 0,
                Max = _EffectMaxValue4 ?? 0,
                Formula = _SpellValueFormula4 ?? 0,
                TypeId = _EffectId4 ?? 0
            };
            _effects[4] = new SpellEffect()
            {
                Base = _EffectBaseValue5 ?? 0,
                Limit = _EffectLimitValue5 ?? 0,
                Max = _EffectMaxValue5 ?? 0,
                Formula = _SpellValueFormula5 ?? 0,
                TypeId = _EffectId5 ?? 0
            };
            _effects[5] = new SpellEffect()
            {
                Base = _EffectBaseValue6 ?? 0,
                Limit = _EffectLimitValue6 ?? 0,
                Max = _EffectMaxValue6 ?? 0,
                Formula = _SpellValueFormula6 ?? 0,
                TypeId = _EffectId6 ?? 0
            };
            _effects[6] = new SpellEffect()
            {
                Base = _EffectBaseValue7 ?? 0,
                Limit = _EffectLimitValue7 ?? 0,
                Max = _EffectMaxValue7 ?? 0,
                Formula = _SpellValueFormula7 ?? 0,
                TypeId = _EffectId7 ?? 0
            };
            _effects[7] = new SpellEffect()
            {
                Base = _EffectBaseValue8 ?? 0,
                Limit = _EffectLimitValue8 ?? 0,
                Max = _EffectMaxValue8 ?? 0,
                Formula = _SpellValueFormula8 ?? 0,
                TypeId = _EffectId8 ?? 0
            };
            _effects[8] = new SpellEffect()
            {
                Base = _EffectBaseValue9 ?? 0,
                Limit = _EffectLimitValue9 ?? 0,
                Max = _EffectMaxValue9 ?? 0,
                Formula = _SpellValueFormula9 ?? 0,
                TypeId = _EffectId9 ?? 0
            };
            _effects[9] = new SpellEffect()
            {
                Base = _EffectBaseValue10 ?? 0,
                Limit = _EffectLimitValue10 ?? 0,
                Max = _EffectMaxValue10 ?? 0,
                Formula = _SpellValueFormula10 ?? 0,
                TypeId = _EffectId10 ?? 0
            };
            _effects[10] = new SpellEffect()
            {
                Base = _EffectBaseValue11 ?? 0,
                Limit = _EffectLimitValue11 ?? 0,
                Max = _EffectMaxValue11 ?? 0,
                Formula = _SpellValueFormula11 ?? 0,
                TypeId = _EffectId11 ?? 0
            };
            _effects[11] = new SpellEffect()
            {
                Base = _EffectBaseValue12 ?? 0,
                Limit = _EffectLimitValue12 ?? 0,
                Max = _EffectMaxValue12 ?? 0,
                Formula = _SpellValueFormula12 ?? 0,
                TypeId = _EffectId12 ?? 0
            };
        }

        public short GetReqLevelForClass(CharClasses charClass)
        {
            switch (charClass) {
                case CharClasses.Warrior:
                    return this.WarriorReqLevel ?? 0;
                case CharClasses.Cleric:
                    return this.ClericReqLevel ?? 0;
                case CharClasses.Paladin:
                    return this.PaladinReqLevel ?? 0;
                case CharClasses.Ranger:
                    return this.RangerReqLevel ?? 0;
                case CharClasses.ShadowKnight:
                    return this.ShadowknightReqLevel ?? 0;
                case CharClasses.Druid:
                    return this.DruidReqLevel ?? 0;
                case CharClasses.Monk:
                    return this.MonkReqLevel ?? 0;
                case CharClasses.Bard:
                    return this.BardReqLevel ?? 0;
                case CharClasses.Rogue:
                    return this.RogueReqLevel ?? 0;
                case CharClasses.Shaman:
                    return this.ShamanReqLevel ?? 0;
                case CharClasses.Necromancer:
                    return this.NecromancerReqLevel ?? 0;
                case CharClasses.Wizard:
                    return this.WizardReqLevel ?? 0;
                case CharClasses.Magician:
                    return this.MagicianReqLevel ?? 0;
                case CharClasses.Enchanter:
                    return this.EnchanterReqLevel ?? 0;
                case CharClasses.BeastLord:
                    return this.BeastlordReqLevel ?? 0;
                case CharClasses.Berserker:
                    return this.BeserkerReqLevel ?? 0;
                default:
                    return 0;
            }
        }

        public bool IsBenefical
        {
            get
            {
                if (this.GoodEffect == 1) {     // Is spell marked beneficial?
                    SpellTargetType stt = (SpellTargetType)this.TargetType;
                    if (stt != SpellTargetType.Self || stt != SpellTargetType.Pet) {
                        if (this.IsEffectInSpell(SpellEffectType.CancelMagic))
                            return false;
                    }

                    if (stt == SpellTargetType.Target || stt == SpellTargetType.AETarget || stt == SpellTargetType.Animal || stt == SpellTargetType.Undead || stt == SpellTargetType.Pet) {
                        EffectIndex sai = (EffectIndex)(this.SpellAffectIndex ?? 0);
                        if ((this.ResistType ?? 0) == (short)EQEmulator.Servers.Internals.Entities.ResistType.Magic) {
                            if (sai == EffectIndex.Calm || sai == EffectIndex.DispelSight || sai == EffectIndex.MemoryBlur || sai == EffectIndex.CalmSong)
                                return false;
                        }
                        else {
                            // Bind Sight and Cast Sight
                            if (sai == EffectIndex.DispelSight && (this.Skill == (short)EQEmulator.Servers.Internals.Data.Skill.Divination))
                                return false;
                        }
                    }
                }

                return this.GoodEffect != 0 || IsGroupSpell;
            }
        }

        public bool IsDetrimental
        {
            get { return !IsBenefical; }
        }

        internal bool IsEffectInSpell(SpellEffectType set)
        {
            return _effects.Count(e => e.TypeId == (int)set) > 0;
        }

        public bool IsGroupSpell
        {
            get
            {
                return (this.TargetType == (short)SpellTargetType.AEBard
                    && this.TargetType == (short)SpellTargetType.Group
                    && this.TargetType == (short)SpellTargetType.GroupTeleport);
            }
        }

        public bool IsBardSong
        {
            get
            {
                return this.BardReqLevel < 255;
            }
        }
    }

    /// <summary>Contains info about a spell's effects.  Part of a Spell object.</summary>
    public class SpellEffect
    {
        public float Base { get; set; }
        public float Limit { get; set; }
        public float Max { get; set; }
        public int Formula { get; set; }
        public int TypeId { get; set; }
    }
}
