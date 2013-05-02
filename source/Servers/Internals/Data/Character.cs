using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Linq.SqlClient;
using System.Text;
using System.Text.RegularExpressions;

using log4net;
using EQEmulator.Servers.Internals.Packets;
using EQEmulator.Servers.Internals.Entities;
using System.Data.Linq;

namespace EQEmulator.Servers.Internals.Data
{
    public enum CharRaces : short
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
        Froglok2    = 74,	// TODO: Not sure why /who all reports race as 74 for frogloks
        Iksar       = 128,
        Vahshir     = 130,
        Froglok     = 330
    }

    public enum Skill : byte
    {
        OneHandBlunt            = 0,
        OneHandSlashing         = 1,
        TwoHandBlunt            = 2,
        TwoHandSlashing         = 3,
        Abjure                  = 4,
        Alteration              = 5,
        ApplyPoison             = 6,
        Archery                 = 7,
        Backstab                = 8,
        BindWound               = 9,
        Bash                    = 10,
        Block                   = 11,
        BrassInstruments        = 12,
        Channeling              = 13,
        Conjuration             = 14,
        Defense                 = 15,
        Disarm                  = 16,
        DisarmTraps             = 17,
        Divination              = 18,
        Dodge                   = 19,
        DoubleAttack            = 20,
        DragonPunch             = 21,	//aka Tail Rake
        DualWield               = 22,
        EagleStrike             = 23,
        Evocation               = 24,
        FeignDeath              = 25,
        FlyingKick              = 26,
        Forage                  = 27,
        HandToHand              = 28,
        Hide                    = 29,
        Kick                    = 30,
        Meditate                = 31,
        Mend                    = 32,
        Offense                 = 33,
        Parry                   = 34,
        Pick_lock               = 35,
        Piercing                = 36,
        Riposte                 = 37,
        RoundKick               = 38,
        SafeFall                = 39,
        SenseHeading            = 40,
        Singing                 = 41,
        Sneak                   = 42,
        SpecializeAbjure        = 43,
        SpecializeAlteration    = 44,
        SpecializeConjuration   = 45,
        SpecializeDivination    = 46,
        SpecializeEvocation     = 47,
        PickPockets             = 48,
        StringedInstruments     = 49,
        Swimming                = 50,
        Throwing                = 51,
        TigerClaw               = 52,
        Tracking                = 53,
        WindInstruments         = 54,
        Fishing                 = 55,
        MakePoison              = 56,
        Tinkering               = 57,
        Research                = 58,
        Alchemy                 = 59,
        Baking                  = 60,
        Tailoring               = 61,
        SenseTraps              = 62,
        Blacksmithing           = 63,
        Fletching               = 64,
        Brewing                 = 65,
        AlcoholTolerance        = 66,
        Begging                 = 67,
        JewelryMaking           = 68,
        Pottery                 = 69,
        PercussionInstruments   = 70,
        Intimidation            = 71,
        Berserking              = 72,
        Taunt                   = 73,
        Frenzy                  = 74,
        GenericTradeskill       = 75,
        Highest                 = Frenzy
    }

    //public enum StartZone : int
    //{
    //    Odus        = 0,
    //    Qeynos      = 1,
    //    Halas       = 2,
    //    Rivervale   = 3,
    //    Freeport    = 4,
    //    Neriak      = 5,
    //    GuktaGrobb  = 6,
    //    Ogguk       = 7,
    //    Kaladim     = 8,
    //    GFay        = 9,
    //    Felwithe    = 10,
    //    Akanon      = 11,
    //    Cabilis     = 12,
    //    SharVahl    = 13
    //}

    public enum Language : byte
    {
        CommonTongue    = 0,
        Barbarian       = 1,
        Erudian         = 2,
        Elvish          = 3,    // Don't these have other names (Tier'Dal or something)?
        DarkElvish      = 4,
        Dwarvish        = 5,
        Troll           = 6,
        Ogre            = 7,
        Gnomish         = 8,
        Halfling        = 9,
        ThievesCant     = 10,
        OldErudian      = 11,
        ElderElvish     = 12,
        Froglock        = 13,
        Goblin          = 14,
        Gnoll           = 15,
        Combine         = 16,
        ElderTierDal    = 17,
        LizardMan       = 18,
        Orcish          = 19,
        Faerie          = 20,
        Dragon          = 21,
        ElderDragon     = 22,
        DarkSpeech      = 23,
        VahShir         = 24,
        Unknown1        = 25,
        Unknown2        = 26
    }

    internal partial class Character
    {
        public const int MAX_BUFF = 25;
        public const int MAX_LANGUAGE = 28;
        public const int MAX_SPELLBOOK = 400;
        public const int MAX_MEMSPELL = 9;
        public const int MAX_SKILL = 75;
        public const int MAX_AA = 239;
        public const int MAX_GROUP_MEMBERS = 6;
        public const int MAX_EQUIPABLES = 9;
        public const int MAX_PLAYER_TRIBUTES = 5;
        public const int MAX_PLAYER_BANDOLIER = 4;
        public const int MAX_PLAYER_BANDOLIER_ITEMS = 4;
        public const int MAX_POTIONS_IN_BELT = 4;
        public const int MAX_RECAST_TYPES = 20;
        public const int CHARACTER_CLASS_COUNT = 16;

        private static readonly ILog _log = LogManager.GetLogger(typeof(Character));

        internal static CharacterSelect GetCharSelectData(int acctId)
        {
            CharacterSelect charSel = new CharacterSelect();

            byte[] noneBuf = Encoding.ASCII.GetBytes("<none>");
            for (int i = 0; i < 10; i++)
                Buffer.BlockCopy(noneBuf, 0, charSel.Name, i * 64, 6);

            using (EmuDataContext dbCtx = new EmuDataContext())
            {
                DataLoadOptions dlo = new DataLoadOptions();
                dlo.LoadWith<Character>(c => c.InventoryItems);
                dlo.LoadWith<InventoryItem>(ii => ii.Item);
                dbCtx.LoadOptions = dlo;

                var characters = from c in dbCtx.Characters
                                 where c.AccountID == acctId
                                 select c;

                int charIdx = 0;
                foreach (Character c in characters) {
                    // character info
                    if (c.Name.Length < 6)
                        Buffer.BlockCopy(Encoding.ASCII.GetBytes(c.Name + "\0\0"), 0, charSel.Name, charIdx * 64, c.Name.Length + 2);
                    else
                        Buffer.BlockCopy(Encoding.ASCII.GetBytes(c.Name), 0, charSel.Name, charIdx * 64, c.Name.Length);
                    charSel.Level[charIdx] = (byte)c.CharLevel;
                    charSel.Class[charIdx] = (byte)c.Class;
                    charSel.Race[charIdx] = (int)c.Race;
                    charSel.Gender[charIdx] = (byte)c.Gender;
                    charSel.Deity[charIdx] = (int)c.Deity;
                    charSel.Zone[charIdx] = (int)c.ZoneID;
                    charSel.HairColor[charIdx] = (byte)c.HairColor;
                    charSel.BeardColor[charIdx] = (byte)c.BeardColor;
                    charSel.EyeColor1[charIdx] = (byte)c.EyeColor1;
                    charSel.EyeColor2[charIdx] = (byte)c.EyeColor2;
                    charSel.Hair[charIdx] = (byte)c.HairStyle;
                    charSel.Beard[charIdx] = (byte)c.Beard;

                    // Get equiped items
                    if (c.InventoryItems.Count > 0) {
                        InventoryManager invMgr = new InventoryManager(c.InventoryItems);

                        for (int equipType = 0; equipType < MAX_EQUIPABLES; equipType++) {
                            InventoryItem ii = invMgr[(int)InventoryManager.GetEquipableSlot((EquipableType)equipType)];
                            if (ii == null)
                                continue;

                            charSel.Equip[charIdx * equipType] = ii.Item.Material;
                            charSel.EquipColors[charIdx * equipType] = ii.Item.Color ?? 0;   // TODO: tints (LoY era)

                            // Held items (set the idfile)
                            if (equipType == (int)EquipableType.Primary || equipType == (int)EquipableType.Secondary) {
                                if (ii.Item.IDFile.Length > 2) {
                                    int idFile = int.Parse(ii.Item.IDFile.Substring(2));
                                    if (equipType == (int)EquipableType.Primary)
                                        charSel.Primary[charIdx] = idFile;
                                    else
                                        charSel.Secondary[charIdx] = idFile;
                                }
                            }
                        }
                    }

                    charIdx++;
                }
            }

            return charSel;
        }

        internal static bool CheckNameFilter(string name)
        {
            if (!Regex.IsMatch(name, @"^[a-zA-Z\'\s]{4,64}"))    // all alpha, between 4 and 64 in len
                return false;
            
            using (EmuDataContext dbCtx = new EmuDataContext())
                if (dbCtx.NameFilters.Count(nf => SqlMethods.Like(name, nf.Filter)) > 0)
                    return false;

            return true;
        }

        internal static bool ReserveName(int acctId, string name)
        {
            using (EmuDataContext dbCtx = new EmuDataContext())
            {
                if (dbCtx.Characters.Count(c => c.Name == name) > 0)
                    return false;

                Character toon = new Character();
                toon.AccountID = acctId;
                toon.Name = name;
                toon.CreatedDate = DateTime.Now;
                dbCtx.Characters.InsertOnSubmit(toon);
                dbCtx.SubmitChanges();
            }
            
            return true;
        }

        internal static void Create(string charName, CharacterCreate charCreateStruct)
        {
            // TODO: old emu does some in depth validation of race/class combinations and ability scores.  Will add later if necessary

            using (EmuDataContext dbCtx = new EmuDataContext())
            {
                DataLoadOptions dlo = new DataLoadOptions();
                dlo.LoadWith<StartingItem>(si => si.Item);
                dbCtx.LoadOptions = dlo;

                Character toon = dbCtx.Characters.SingleOrDefault(c => c.Name == charName);
                toon.Race = (short)charCreateStruct.Race;
                toon.Class = (byte)charCreateStruct.Class;
                toon.Gender = (byte)charCreateStruct.Gender;
                toon.Deity = (int)charCreateStruct.Deity;
                toon.STR = (short)charCreateStruct.STR;
                toon.STA = (short)charCreateStruct.STA;
                toon.AGI = (short)charCreateStruct.AGI;
                toon.DEX = (short)charCreateStruct.DEX;
                toon.WIS = (short)charCreateStruct.WIS;
                toon.INT = (short)charCreateStruct.INT;
                toon.CHA = (short)charCreateStruct.CHA;
                toon.Face = (byte)charCreateStruct.Face;
                toon.EyeColor1 = (byte)charCreateStruct.EyeColor1;
                toon.EyeColor2 = (byte)charCreateStruct.EyeColor2;
                toon.HairStyle = (byte)charCreateStruct.HairStyle;
                toon.HairColor = (byte)charCreateStruct.HairColor;
                toon.Beard = (byte)charCreateStruct.Beard;
                toon.BeardColor = (byte)charCreateStruct.BeardColor;
                toon.LastSeenDate = DateTime.Now;
                toon.CharLevel = 1;
                toon.PracticePoints = 5;
                toon.HP = 1000;     // just here for dev, later will be set elsewhere
                toon.HungerLevel = 6000;
                toon.ThirstLevel = 6000;
                toon.Platinum = (uint)EQEmulator.Servers.WorldServer.ServerConfig.StartingPlat;
                toon.Gold = (uint)EQEmulator.Servers.WorldServer.ServerConfig.StartingGold;

                SetRacialStartingAbilities(ref toon);   // Sets languages and skills that are racially determined
                SetClassStartingAbilities(ref toon);    // Sets skills determined by class

                // Get the character's start zone
                StartZone startZone = dbCtx.StartZones.SingleOrDefault(sz => sz.PlayerChoice == charCreateStruct.StartZone
                    && sz.PlayerClass == charCreateStruct.Class && sz.PlayerDeity == charCreateStruct.Deity && sz.PlayerRace == charCreateStruct.Race);
                CharacterBind cb = new CharacterBind();
                toon.X = toon.Y = toon.Z = 0.0F;
                cb.X = cb.Y = cb.Z = 0.0F;
                toon.Heading = 0.0F;

                if (startZone != null)  // TODO: should heading for zone and bind be set to some default setting?
                {
                    // Found a start zone in the db... load up the bind info from that
                    toon.ZoneID = startZone.ZoneID;
                    toon.ZoneName = startZone.Zone.ShortName;
                    toon.X = startZone.X;
                    toon.Y = startZone.Y;
                    toon.Z = startZone.Z;

                    if (startZone.BindZoneID != null)
                    {
                        cb.ZoneID = startZone.BindZoneID.Value;
                        cb.X = startZone.BindX.Value;
                        cb.Y = startZone.BindY.Value;
                        cb.Z = startZone.BindZ.Value;
                    }
                    else
                        cb.ZoneID = startZone.ZoneID;
                }
                else
                    SetDefaultStartZone(charCreateStruct.StartZone, ref toon, ref cb);

                Zone zone = null;

                // Load safe points for start zone coords if necessary
                if (toon.X == 0.0F && toon.Y == 0.0F && toon.Z == 0.0F)
                {
                    zone = dbCtx.Zones.SingleOrDefault(z => z.ZoneID == toon.ZoneID);
                    toon.X = zone.SafeX;
                    toon.Y = zone.SafeY;
                    toon.Z = zone.SafeZ;
                }

                // Load safe points for start bind coords if necessary
                if (cb.X == 0.0F && cb.Y == 0.0F && cb.Z == 0.0F)
                {
                    zone = dbCtx.Zones.SingleOrDefault(z => z.ZoneID == cb.ZoneID);
                    if (zone != null)
                    {
                        cb.X = zone.SafeX;
                        cb.Y = zone.SafeY;
                        cb.Z = zone.SafeZ;
                    }
                    else
                        _log.ErrorFormat("Unable to load safe points for bind zone {0}", cb.ZoneID);
                }
                
                cb.Heading = toon.Heading.Value;
                toon.CharacterBinds.Add(cb);

                // Get starting items
                var startingItems = from si in dbCtx.StartingItems
                                    where (si.Race == toon.Race.Value || si.Race == 0) && (si.Class == toon.Class.Value || si.Class == 0)
                                    && (si.DeityID == (toon.Deity ?? 0) || si.DeityID == 0) && (si.ZoneID == toon.ZoneID.Value || si.ZoneID == 0)
                                    select si;

                int siSlotId = 22;    // for initial items with unspecified slots, just dump them to the personal inv slots
                foreach (StartingItem si in startingItems) {
                    InventoryItem ii = new InventoryItem {
                        ItemID = si.ItemID,
                        Charges = si.ItemCharges,
                        Color = si.Item.Color ?? 0,
                        SlotID = si.Slot
                    };

                    if (ii.SlotID < 0) {  // for unspecified inventory slots, find an open slot
                        ii.SlotID = siSlotId;
                        siSlotId++;
                    }

                    toon.InventoryItems.Add(ii);
                }

                dbCtx.SubmitChanges();
            }
        }

        private static void SetRacialStartingAbilities(ref Character toon)
        {
            byte[] langs = new byte[MAX_LANGUAGE];
            byte[] skills = new byte[MAX_SKILL];

            //skills[(byte)Skill.SenseHeading] = 100;     // This could be toggled for "classic" feel.

            switch ((CharRaces)toon.Race)
            {
                case CharRaces.Human:
                    langs[(byte)Language.CommonTongue] = 100;
                    break;
                case CharRaces.Barbarian:
                    langs[(byte)Language.CommonTongue] = 100;
                    langs[(byte)Language.Barbarian] = 100;
                    break;
                case CharRaces.Erudite:
                    langs[(byte)Language.CommonTongue] = 100;
                    langs[(byte)Language.Erudian] = 100;
                    break;
                case CharRaces.WoodElf:
                    langs[(byte)Language.CommonTongue] = 100;
                    langs[(byte)Language.Elvish] = 100;

                    skills[(byte)Skill.Forage] = 50;
                    skills[(byte)Skill.Hide] = 50;
                    break;
                case CharRaces.HighElf:
                    langs[(byte)Language.CommonTongue] = 100;
                    langs[(byte)Language.DarkElvish] = 25;
                    langs[(byte)Language.ElderElvish] = 25;
                    langs[(byte)Language.Elvish] = 100;
                    break;
                case CharRaces.DarkElf:
                    langs[(byte)Language.CommonTongue] = 100;
                    langs[(byte)Language.DarkElvish] = 100;
                    langs[(byte)Language.DarkSpeech] = 100;
                    langs[(byte)Language.ElderElvish] = 100;
                    langs[(byte)Language.Elvish] = 25;

                    skills[(byte)Skill.Hide] = 50;
                    break;
                case CharRaces.HalfElf:
                    langs[(byte)Language.CommonTongue] = 100;
                    langs[(byte)Language.Elvish] = 100;
                    break;
                case CharRaces.Dwarf:
                    langs[(byte)Language.CommonTongue] = 100;
                    langs[(byte)Language.Dwarvish] = 100;
                    langs[(byte)Language.Gnomish] = 25;
                    break;
                case CharRaces.Troll:
                    langs[(byte)Language.CommonTongue] = 95;
                    langs[(byte)Language.DarkSpeech] = 100;
                    langs[(byte)Language.Troll] = 100;
                    break;
                case CharRaces.Ogre:
                    langs[(byte)Language.CommonTongue] = 95;
                    langs[(byte)Language.DarkSpeech] = 100;
                    langs[(byte)Language.Ogre] = 100;
                    break;
                case CharRaces.Halfling:
                    langs[(byte)Language.CommonTongue] = 100;
                    langs[(byte)Language.Halfling] = 100;

                    skills[(byte)Skill.Hide] = 50;
                    skills[(byte)Skill.Sneak] = 50;
                    break;
                case CharRaces.Gnome:
                    langs[(byte)Language.CommonTongue] = 100;
                    langs[(byte)Language.Dwarvish] = 25;
                    langs[(byte)Language.Gnomish] = 100;

                    skills[(byte)Skill.Tinkering] = 50;
                    break;
                case CharRaces.Iksar:
                    langs[(byte)Language.CommonTongue] = 95;
                    langs[(byte)Language.DarkSpeech] = 100;
                    langs[(byte)Language.LizardMan] = 100;

                    skills[(byte)Skill.Forage] = 50;
                    skills[(byte)Skill.Swimming] = 100;
                    break;
                case CharRaces.Vahshir:
                    langs[(byte)Language.CommonTongue] = 100;
                    langs[(byte)Language.Combine] = 100;
                    langs[(byte)Language.Erudian] = 25;
                    langs[(byte)Language.VahShir] = 100;

                    skills[(byte)Skill.SafeFall] = 50;
                    skills[(byte)Skill.Sneak] = 50;
                    break;
                case CharRaces.Froglok:
                    langs[(byte)Language.CommonTongue] = 100;
                    langs[(byte)Language.Froglock] = 100;
                    langs[(byte)Language.Troll] = 25;

                    skills[(byte)Skill.Swimming] = 125;
                    break;
                default:
                    throw new Exception("race found not set in racial skill determination during char init.");
            }
            
            toon.Languages = Encoding.ASCII.GetString(langs);
            toon.Skills = Encoding.ASCII.GetString(skills);
        }

        private static void SetClassStartingAbilities(ref Character toon)
        {
            byte[] langs = Encoding.ASCII.GetBytes(toon.Languages);
            byte[] skills = Encoding.ASCII.GetBytes(toon.Skills);

            switch ((CharClasses)toon.Class)
            {
                case CharClasses.Druid:
                case CharClasses.Cleric:
                case CharClasses.Shaman:
                    skills[(byte)Skill.OneHandBlunt] = 5;
                    break;
                case CharClasses.Paladin:
                case CharClasses.Ranger:
                case CharClasses.ShadowKnight:
                case CharClasses.Warrior:
                    skills[(byte)Skill.OneHandSlashing] = 5;
                    break;
                case CharClasses.Monk:
                    skills[(byte)Skill.Dodge] = 5;
                    skills[(byte)Skill.DualWield] = 5;
                    skills[(byte)Skill.HandToHand] = 5;
                    break;
                case CharClasses.Bard:
                    skills[(byte)Skill.OneHandSlashing] = 5;
                    skills[(byte)Skill.Singing] = 5;
                    break;
                case CharClasses.Rogue:
                    skills[(byte)Skill.Piercing] = 5;
                    langs[(byte)Language.ThievesCant] = 100;
                    break;
                case CharClasses.Necromancer:
                case CharClasses.Wizard:
                case CharClasses.Magician:
                case CharClasses.Enchanter:
                    skills[(byte)Skill.Piercing] = 5;
                    break;
                case CharClasses.BeastLord:
                    skills[(byte)Skill.HandToHand] = 5;
                    break;
                case CharClasses.Berserker:     // might need confirmation from live
                    skills[(byte)Skill.TwoHandSlashing] = 5;
                    break;
                default:
                    throw new Exception("class found not set in racial skill determination during char init.");
            }

            toon.Languages = Encoding.ASCII.GetString(langs);
            toon.Skills = Encoding.ASCII.GetString(skills);
        }

        private static void SetDefaultStartZone(uint startZoneId, ref Character toon, ref CharacterBind cb)
        {
            switch (startZoneId)
            {
                case 0:
                    toon.ZoneID = 24;   // erudnext
                    cb.ZoneID = 38;     // tox
                    break;
                case 1:
                    toon.ZoneID = 2;   // north qeynos
                    cb.ZoneID = 2;     // north qeynos
                    break;
                case 2:
                    toon.ZoneID = 29;   // halas
                    cb.ZoneID = 30;     // everfrost
                    break;
                case 3:
                    toon.ZoneID = 19;   // rivervale
                    cb.ZoneID = 20;     // kithicor
                    break;
                case 4:
                    toon.ZoneID = 9;   // freeportw
                    cb.ZoneID = 9;     // freeportw
                    break;
                case 5:
                    toon.ZoneID = 40;   // neriaka
                    cb.ZoneID = 25;     // nektulos
                    break;
                case 6:
                    toon.ZoneID = 52;   // gukta
                    cb.ZoneID = 46;     // innothule
                    break;
                case 7:
                    toon.ZoneID = 49;   // oggok
                    cb.ZoneID = 47;     // feerrott
                    break;
                case 8:
                    toon.ZoneID = 60;   // kaladima
                    cb.ZoneID = 68;     // butcher
                    break;
                case 9:
                    toon.ZoneID = 54;   // gfaydark
                    cb.ZoneID = 54;     // gfaydark
                    break;
                case 10:
                    toon.ZoneID = 61;   // felwithea
                    cb.ZoneID = 54;     // gfaydark
                    break;
                case 11:
                    toon.ZoneID = 55;   // akanon
                    cb.ZoneID = 56;     // steamfont
                    break;
                case 12:
                    toon.ZoneID = 82;   // cabwest
                    cb.ZoneID = 78;     // fieldofbone
                    break;
                case 13:
                    toon.ZoneID = 155;   // sharvahl
                    cb.ZoneID = 155;     // sharvahl
                    break;
                default:
                    throw new ArgumentException("Invalid start zone: must be between 0 - 13", "startZoneId");
            }
        }

        internal static bool Delete(string charName)
        {
            using (EmuDataContext dbCtx = new EmuDataContext())
            {
                Data.Character charToDel = dbCtx.Characters.SingleOrDefault(c => c.Name == charName);
                if (charToDel != null)
                {
                    dbCtx.Characters.DeleteOnSubmit(charToDel);
                    dbCtx.SubmitChanges();
                    return true;
                }
            }

            return false;
        }

        internal static uint GetXpForLevel(int level)
        {
            int check_levelm1 = level - 1;
	        float mod;
            if (level < 31)
		        mod = 1.0F;
            else if (level < 36)
		        mod = 1.1F;
            else if (level < 41)
		        mod = 1.2F;
            else if (level < 46)
		        mod = 1.3F;
            else if (level < 52)
		        mod = 1.4F;
            else if (level < 53)
		        mod = 1.5F;
            else if (level < 54)
		        mod = 1.6F;
            else if (level < 55)
		        mod = 1.7F;
            else if (level < 56)
		        mod = 1.9F;
            else if (level < 57)
		        mod = 2.1F;
            else if (level < 58)
		        mod = 2.3F;
            else if (level < 59)
		        mod = 2.5F;
            else if (level < 60)
		        mod = 2.7F;
            else if (level < 61)
		        mod = 3.0F;
	        else
		        mod = 3.1F;

            // TODO: possibly add race & class xp modifications
        	
	        float modBase = check_levelm1 * check_levelm1 * check_levelm1;
            mod *= 1000;
            return (uint)(modBase * mod);
        }

        internal static int GetRaceIndex(CharRaces race)
        {
            switch (race) {
                case CharRaces.Iksar:
                    return 13;
                case CharRaces.Vahshir:
                    return 14;
                case CharRaces.Froglok:
                case CharRaces.Froglok2:
                    return 15;
                default:
                    return (int)race;
            }
        }

        partial void OnZoneIDChanged()
        {
            if (this.ZoneID == 0)
                this.ZoneName = string.Empty;
            else
                using (EmuDataContext dbCtx = new EmuDataContext())
                    this.ZoneName = dbCtx.Zones.Single(z => z.ZoneID == this.ZoneID).ShortName;
        }
    }
}