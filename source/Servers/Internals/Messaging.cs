using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using log4net;
using EQEmulator.Servers.Internals.Packets;
using EQEmulator.Servers.Internals.Data;
using EQEmulator.Servers.Internals.Entities;

namespace EQEmulator.Servers.Internals
{
    /// <summary>Corresponds to chat color on client.</summary>
    /// <remarks>Not sure about the whole color thing... maybe sometimes, but not always.</remarks>
    public enum MessageType
    {
        Default         = 0,
        DarkGrey        = 1,
        DarkGreen       = 2,
        DarkBlue        = 3,
        Purple          = 5,
        LightGrey       = 6,
        Common13        = 13,   // TODO: document wth 13 & 15 are for
        Common15        = 15,
        Say             = 256,
        Tell            = 257,
        Group           = 258,
        Guild           = 259,
        OOC             = 260,
        Auction         = 261,
        Shout           = 262,
        Emote           = 263,
        Spells          = 264,
        YouHitOther     = 265,
        OtherHitsYou    = 266,
        YouMissOther    = 267,
        OtherMissesYou  = 268,
        Broadcast       = 269,
        Skills          = 270,
        Disciplines     = 271,
        DefaultText     = 273,
        MerchantOffer   = 275,
        MerchantBuySell = 276,
        YourDeath       = 277,
        OtherDeath      = 278,
        OtherHits       = 279,
        OtherMisses     = 280,
        Who             = 281,
        YellForHelp     = 282,
        NonMelee        = 283,
        WornOff         = 284,
        MoneySplit      = 285,
        LootMessages    = 286,
        DiceRoll        = 287,
        OtherSpells     = 288,
        Fizzles         = 289,
        Chat            = 290,
        Channel1        = 291,
        Channel2        = 292,
        Channel3        = 293,
        Channel4        = 294,
        Channel5        = 295,
        Channel6        = 296,
        Channel7        = 297,
        Channel8        = 298,
        Channel9        = 299,
        Channel10       = 300,
        CritMelee       = 301   // TODO: add the rest
    }

    public enum MessageStrings : uint
    {
        Generic9Strings             = 1,		//%1 %2 %3 %4 %5 %6 %7 %8 %9
        Target_Out_Of_Range			= 100,		//Your target is out of range, get closer!
        Target_Not_Found			= 101,		//Target player not found.
        Cannot_Bind					= 105,		//You cannot form an affinity with this area.  Try a city.
        Spell_Does_Not_Work_Here	= 106,		//This spell does not work here.
        SPELL_DOES_NOT_WORK_PLANE	= 107,		//This spell does not work on this plane.
        CANT_SEE_TARGET				= 108,		//You cannot see your target.
        MGB_STRING					= 113,		//The next group buff you cast will hit all targets in range.
        TARGET_TOO_FAR				= 124,		//Your target is too far away, get closer!
        PROC_TOOLOW					= 126,		//Your will is not sufficient to command this weapon.
        PROC_PETTOOLOW				= 127,		//Your pet's will is not sufficient to command its weapon.
        DOORS_LOCKED				= 130,		//It's locked and you're not holding the key.
        DOORS_CANT_PICK				= 131,		//This lock cannot be picked.
        DOORS_INSUFFICIENT_SKILL	= 132,		//You are not sufficiently skilled to pick this lock.
        DOORS_GM					= 133,		//You opened the locked door with your magic GM key.
        GAIN_XP						= 138,		//You gain experience!!
        GAIN_GROUPXP				= 139,		//You gain party experience!!
        BOW_DOUBLE_DAMAGE			= 143,		//Your bow shot did double dmg.
        FORAGE_GRUBS				= 150,		//You have scrounged up some fishing grubs.
        FORAGE_WATER				= 151,		//You have scrounged up some water.
        FORAGE_FOOD					= 152,		//You have scrounged up some food.
        FORAGE_DRINK				= 153,		//You have scrounged up some drink.
        FORAGE_NOEAT				= 154,		//You have scrounged up something that doesn't look edible.
        FORAGE_FAILED				= 155,		//You fail to locate any food nearby.
        ALREADY_FISHING				= 156,		//You are already fishing!
        FISHING_NO_POLE				= 160,		//You can't fish without a fishing pole, go buy one.
        FISHING_EQUIP_POLE			= 161,		//You need to put your fishing pole in your primary hand.
        FISHING_NO_BAIT				= 162,		//You can't fish without fishing bait, go buy some.
        FISHING_CAST				= 163,		//You cast your line.
        NOT_SCARING					= 164,		//You're not scaring anyone.
        FISHING_STOP				= 165,		//You stop fishing and go on your way.
        FISHING_LAND				= 166,		//Trying to catch land sharks perhaps?
        FISHING_LAVA				= 167,		//Trying to catch a fire elemental or something?
        FISHING_FAILED				= 168,		//You didn't catch anything.
        FISHING_POLE_BROKE			= 169,		//Your fishing pole broke!
        FISHING_SUCCESS				= 170,		//You caught, something...
        FISHING_SPILL_BEER			= 171,		//You spill your beer while bringing in your line.
        FISHING_LOST_BAIT			= 172,		//You lost your bait!
        SPELL_FIZZLE				= 173,		//Your spell fizzles!
        MISS_NOTE					= 180,		//You miss a note, bringing your song to a close!
        CannotUseItem               = 181,      // Your race, class or deity cannot use this item.
        ItemOutOfCharges            = 182,      // Item is out of charges
        TargetNoMana                = 191,      // Your target has no mana to affect
        TargetGroupMember           = 196,      // You must first target a group member
        InsufficientMana            = 199,      // Insufficient mana to cast this spell!
        SAC_TOO_LOW					= 203,		//This being is not a worthy sacrifice.
        SAC_TOO_HIGH				= 204,		//This being is too powerful to be a sacrifice.
        CANNOT_SAC_SELF				= 205,		//You cannot sacrifice yourself.
        SILENCED_CANT_CAST          = 207,      // You *CANNOT* cast spells, you have been silenced!
        CANNOT_AFFECT_PC			= 210,		//That spell can not affect this target PC.
        SPELL_NEED_TAR				= 214,		//You must first select a target for this spell!
        ONLY_ON_CORPSES				= 221,		//This spell only works on corpses.
        CANT_DRAIN_SELF				= 224,		//You can't drain yourself!
        CORPSE_NOT_VALID			= 230,		//This corpse is not valid.
        CANNOT_MEZ					= 239,		//Your target cannot be mesmerized.
        CANNOT_MEZ_WITH_SPELL		= 240,		//Your target cannot be mesmerized (with this spell).
        IMMUNE_STUN					= 241,		//Your target is immune to the stun portion of this effect.
        IMMUNE_ATKSPEED				= 242,		//Your target is immune to changes in its attack speed.
        IMMUNE_FEAR					= 243,		//Your target is immune to fear spells.
        IMMUNE_MOVEMENT				= 244,		//Your target is immune to changes in its run speed.
        ONLY_ONE_PET				= 246,		//You cannot have more than one pet at a time.
        CANNOT_CHARM_YET			= 248,		//Your target is too high of a level for your charm spell.
        NO_PET						= 255,		//You do not have a pet.
        CORPSE_CANT_SENSE			= 262,		//You cannot sense any corpses for this PC in this zone.
        SPELL_NO_HOLD				= 263,		//Your spell did not take hold.
        CANNOT_CHARM				= 267,		//This NPC cannot be charmed.
        NO_INSTRUMENT_SKILL			= 269,		//Stick to singing until you learn to play this instrument.
        REGAIN_AND_CONTINUE			= 270,		//You regain your concentration and continue your casting.
        SPELL_WOULDNT_HOLD          = 271,      // Your spell would not have taken hold on your target.
        MISSING_SPELL_COMP			= 272,		//You are missing some required spell components.
        DISCIPLINE_CONLOST			= 278,		//You lose the concentration to remain in your fighting discipline.
        REZ_REGAIN					= 289,		//You regain some experience from resurrection.
        DUP_LORE					= 290,		//Duplicate lore items are not allowed. 
        TGB_ON						= 293,		//Target other group buff is *ON*.
        TGB_OFF						= 294,		//Target other group buff is *OFF*.
        LDON_SENSE_TRAP1			= 306,		//You do not Sense any traps.
        TRADESKILL_NOCOMBINE		= 334,		//You cannot combine these items in this container type!
        TRADESKILL_FAILED			= 336,		//You lacked the skills to fashion the items together.
        TRADESKILL_TRIVIAL			= 338,		//You can no longer advance your skill from making this item.
        TRADESKILL_SUCCEED			= 339,		//You have fashioned the items together to create something new!
        MEND_CRITICAL				= 349,		//You magically mend your wounds and heal considerable damage.
        MEND_SUCCESS				= 350,		//You mend your wounds and heal some damage.
        MEND_WORSEN					= 351,		//You have worsened your wounds!
        MEND_FAIL					= 352,		//You have failed to mend your wounds.
        LDON_SENSE_TRAP2			= 367,		//You have not detected any traps.
        LOOT_LORE_ERROR				= 371,		//You cannot loot this Lore Item.  You already have one.
        PICK_LORE					= 379,		//You cannot pick up a lore item you already possess.
        CONSENT_DENIED				= 390,		//You do not have consent to summon that corpse.
        DISCIPLINE_RDY				= 393,		//You are ready to use a new discipline now.
        CONSENT_INVALID_NAME		= 397,		//Not a valid consent name.
        CONSENT_NPC					= 398,		//You cannot consent NPC\'s.
        CONSENT_YOURSELF			= 399,		//You cannot consent yourself.
        SONG_NEEDS_DRUM				= 405,		//You need to play a percussion instrument for this song
        SONG_NEEDS_WIND				= 406,		//You need to play a wind instrument for this song
        SONG_NEEDS_STRINGS			= 407,		//You need to play a stringed instrument for this song
        SONG_NEEDS_BRASS			= 408,		//You need to play a brass instrument for this song
        AA_GAIN_ABILITY				= 410,		//You have gained the ability "%T1" at a cost of %2 ability %T3.
        AA_IMPROVE					= 411,		//You have improved %T1 %2 at a cost of %3 ability %T4.
        AA_REUSE_MSG				= 413,		//You can use the ability %B1(1) again in %2 hour(s) %3 minute(s) %4 seconds.
        AA_REUSE_MSG2				= 414,		//You can use the ability %B1(1) again in %2 minute(s) %3 seconds.
        BEGINS_TO_GLOW				= 422,		//Your %1 begins to glow.
        ALREADY_INVIS				= 423,		//%1 tries to cast an invisibility spell on you, but you are already invisible.
        YOU_ARE_PROTECTED			= 424,		//%1 tries to cast a spell on you, but you are protected.
        TARGET_RESISTED				= 425,		//Your target resisted the %1 spell.
        YOU_RESIST					= 426,		//You resist the %1 spell!
        SUMMONING_CORPSE			= 429,		//Summoning your corpse.
        SUMMONING_CORPSE_OTHER		= 430,		//Summoning %1's corpse.
        MISSING_SPELL_COMP_ITEM		= 433,		//You are missing %1.
        OTHER_HIT_NONMELEE			= 434,		//%1 was hit by non-melee for %2 points of damage.
        SPELL_WORN_OFF_OF			= 436,		//Your %1 spell has worn off of %2.
        SPELL_WORN_OFF				= 437,		//Your %1 spell has worn off.
        INTERRUPT_SPELL				= 439,		//Your spell is interrupted.
        LOSE_LEVEL					= 442,		//You LOST a level! You are now level %1!
        GAIN_ABILITY_POINT			= 446,		//You have gained an ability point!  You now have %1 ability point%2.
        GAIN_LEVEL					= 447,		//You have gained a level! Welcome to level %1!
        GAIN_LANGUAGE_POINT         = 449,      // Some kind of message related to gaining a point in a language skill
        OTHER_LOOTED_MESSAGE		= 466,		//--%1 has looted a %2--
        LOOTED_MESSAGE				= 467,		//--You have looted a %1--
        FACTION_WORST				= 469,		//Your faction standing with %1 could not possibly get any worse.
        FACTION_WORSE				= 470,		//Your faction standing with %1 got worse.
        FACTION_BEST				= 471,		//Your faction standing with %1 could not possibly get any better.
        FACTION_BETTER				= 472,		//Your faction standing with %1 got better.
        PET_REPORT_HP				= 488,		//I have %1 percent of my hit points left.
        CORPSE_DECAY1				= 495,		//This corpse will decay in %1 minute(s) %2 seconds.
        DISC_LEVEL_ERROR			= 503,		//You must be a level %1 ... to use this discipline.
        DISCIPLINE_CANUSEIN			= 504,		//You can use a new discipline in %1 minutes %2 seconds.
        PVP_ON						= 552,		//You are now player kill and follow the ways of Discord.
        GENERIC_STRINGID_SAY		= 554,		//%1 says '%T2'
        CANNOT_WAKE					= 555,		//%1 tells you, 'I am unable to wake %2, master.'
        GM_GAINXP					= 1002,	//[GM] You have gained %1 AXP and %2 EXP (%3).
        FINISHING_BLOW				= 1009,	//%1 scores a Finishing Blow!!
        ASSASSINATES				= 1016,	//%1 ASSASSINATES their victim!!
        CRITICAL_HIT				= 1023,	//%1 scores a critical hit! (%2)
        RESISTS_URGE				= 1025,	//%1 resists their urge to flee.
        BERSERK_START				= 1027,	//%1 goes into a berserker frenzy!
        DIVINE_INTERVENTION			= 1029,	//%1 has been rescued by divine intervention!
        BERSERK_END					= 1030,	//%1 is no longer berserk.
        GATES						= 1031,	//%1 Gates.
        GENERIC_SAY					= 1032,	//%1 says '%2'
        OTHER_REGAIN_CAST			= 1033,	//%1 regains concentration and continues casting.
        GENERIC_SHOUT				= 1034,	//%1 shouts '%2'
        GENERIC_EMOTE				= 1036,	//%1 %2
        DISCIPLINE_FEARLESS			= 1076,	//%1 becomes fearless.
        DUEL_FINISHED				= 1088,	//dont know text
        EATING_MESSAGE				= 1091,	//Chomp, chomp, chomp...  %1 takes a bite from a %2.
        DRINKING_MESSAGE			= 1093,	//Glug, glug, glug...  %1 takes a drink from a %2.
        PET_SIT_STRING				= 1130,	//Changing position, Master.
        PET_CALMING					= 1131,	//Sorry, Master..calming down.
        PET_FOLLOWING				= 1132,	//Following you, Master.
        PET_GUARDME_STRING			= 1133,	//Guarding you, Master.
        PET_GUARDINGLIFE			= 1134,	//Guarding with my life..oh splendid one.
        PET_GETLOST_STRING			= 1135,	//As you wish, oh great one.
        PET_LEADERIS				= 1136,	//My leader is %3.
        MERCHANT_BUSY				= 1143,	//I'm sorry, I am busy right now.
        MERCHANT_GREETING			= 1144,	//Welcome to my shop, %3.
        MERCHANT_HANDY_ITEM1		= 1145,	//Hello there, %3. How about a nice %4?
        MERCHANT_HANDY_ITEM2		= 1146,	//Greetings, %3. You look like you could use a %4.
        MERCHANT_HANDY_ITEM3		= 1147,	//Hi there %3, just browsing?  Have you seen the %4 I just got in?
        MERCHANT_HANDY_ITEM4		= 1148,	//Welcome to my shop, %3. You would probably find a %4 handy.
        AA_POINT					= 1197,	//point
        AA_POINTS					= 1215,	//points
        SPELL_FIZZLE_OTHER			= 1218,	//%1's spell fizzles!
        MISSED_NOTE_OTHER			= 1219,	//A missed note brings %1's song to a close!
        CORPSE_DECAY_NOW			= 1227,	//This corpse is waiting to expire.
        SURNAME_REJECTED			= 1374,	//Your new surname was rejected.  Please try a different name.
        DUEL_DECLINE				= 1383,	//%1 has declined your challenge to duel to the death.
        DUEL_ACCEPTED				= 1384,	//%1 has already accepted a duel with someone else.
        DUEL_CONSIDERING			= 1385,	//%1 is considering a duel with someone else.
        PLAYER_REGAIN				= 1394,	//You have control of yourself again.
        IN_USE						= 1406,	//Someone else is using that.  Try again later.
        DUEL_FLED					= 1408,	//%1 has defeated %2 in a duel to the death! %3 has fled like a cowardly dog!
        RECEIVED_PLATINUM			= 1452,	//You receive %1 Platinum from %2.
        RECEIVED_GOLD				= 1453,	//You receive %1 Gold from %2.
        RECEIVED_SILVER				= 1454,	//You receive %1 Silver from %2.
        RECEIVED_COPPER				= 1455,	//You receive %1 Copper from %2.
        STRING_FEIGNFAILED			= 1456,	//%1 has fallen to the ground.
        DOORS_SUCCESSFUL_PICK		= 1457,	//You successfully picked the lock.
        PLAYER_CHARMED				= 1461,	//You lose control of yourself!
        TRADER_BUSY					= 1468,	//That Trader is currently with a customer. Please wait until their transaction is finished.
        WHOALL_NO_RESULTS			= 5029,	//There are no players in EverQuest that match those who filters.
        PETITION_NO_DELETE			= 5053,	//You do not have a petition in the queue.
        PETITION_DELETED			= 5054,	//Your petition was successfully deleted.
        GAIN_RAIDEXP				= 5085,	//You gained raid experience!
        ADVENTURE_COMPLETE			= 5147,	//You received %1 points for successfully completing the adventure.
        PET_ATTACKING				= 5501,	//%1 tells you, 'Attacking %2 Master.'
        DISCIPLINE_REUSE_MSG		= 5807,	//You can use the ability %1 again in %2 hour(s) %3 minute(s) %4 seconds.
        DISCIPLINE_REUSE_MSG2		= 5808,	//You can use the ability %1 again in %2 minute(s) %3 seconds.
        AA_NO_TARGET				= 5825,	//You must first select a target for this ability!
        GENERIC_STRING				= 6688,	//%1  (used to any basic message)
        SENTINEL_TRIG_YOU			= 6724,	//You have triggered your sentinel.
        SENTINEL_TRIG_OTHER			= 6725,	//%1 has triggered your sentinel.
        IDENTIFY_SPELL				= 6765,	//Item Lore: %1.
        LDON_DONT_KNOW_TRAPPED		= 7552,	//You do not know if this object is trapped.
        LDON_HAVE_DISARMED			= 7553,	//You have disarmed %1!
        LDON_ACCIDENT_SETOFF		= 7554,	//You accidentally set off the trap!
        LDON_HAVE_NOT_DISARMED		= 7555,	//You have not disarmed %1.
        LDON_ACCIDENT_SETOFF2		= 7556,	//You accidentally set off the trap!
        LDON_CERTAIN_TRAP			= 7557,	//You are certain that %1 is trapped.
        LDON_CERTAIN_NOT_TRAP		= 7558,	//You are certain that %1 is not trapped.
        LDON_CANT_DETERMINE_TRAP	= 7559,	//You are unable to determine if %1 is trapped.
        LDON_PICKLOCK_SUCCESS		= 7560,	//You have successfully picked %1!
        LDON_PICKLOCK_FAILURE		= 7561,	//You have failed to pick %1.
        LDON_STILL_LOCKED			= 7562,	//You cannot open %1, it is locked.
        LDON_BASH_CHEST				= 7563,	//%1 try to %2 %3, but do no damage.
        DOORS_NO_PICK				= 7564,	//You must have a lock pick in your inventory to do this.
        LDON_NO_LOCKPICK			= 7564,	//You must have a lock pick in your inventory to do this.
        LDON_WAS_NOT_LOCKED			= 7565,	//%1 was not locked.
        LDON_WAS_NOT_TRAPPED		= 7566,	//%1 was not trapped
        GAIN_GROUP_LEADERSHIP_POINT	= 8585,	//
        GAIN_RAID_LEADERSHIP_POINT	= 8589,	//
        MAX_GROUP_LEADERSHIP_POINTS	= 8584,	//
        MAX_RAID_LEADERSHIP_POINTS	= 8591,	//
        LEADERSHIP_EXP_ON			= 8653,	//
        LEADERSHIP_EXP_OFF			= 8654,	//
        CURRENT_SPELL_EFFECTS		= 8757,	//%1's current spell effects: 
        GAIN_GROUP_LEADERSHIP_EXP	= 8788,	//
        GAIN_RAID_LEADERSHIP_EXP	= 8789,	//
        BUFF_MINUTES_REMAINING		= 8799,	//%1 (%2 minutes remaining)
        OTHER_HIT_DOT				= 9072,	//%1 has taken %2 damage from your %3.
        HIT_NON_MELEE				= 9073,	//%1 hit %2 for %3 points of non-melee damage.
        STRIKETHROUGH_STRING		= 9078,	//You strike through your opponent's defenses!
        NEW_SPELLS_AVAIL			= 9149,	//You have new spells available to you.  Check the merchants near your guild master.
        FACE_ACCEPTED				= 12028,	//Facial features accepted.
        SPELL_LEVEL_TO_LOW			= 12048,	//You will have to achieve level %1 before you can scribe the %2.
        ATTACKFAILED				= 12158,	//%1 try to %2 %3, but %4!
        HIT_STRING					= 12183,	//hit
        CRUSH_STRING				= 12191,	//crush
        PIERCE_STRING				= 12193,	//pierce
        KICK_STRING					= 12195,	//kick
        STRIKE_STRING				= 12197,	//strike
        BACKSTAB_STRING				= 12199,	//backstab
        BASH_STRING					= 12201,	//bash
        GUILD_NOT_MEMBER			= 12242,	//You are not a member of any guild.
        NOT_IN_CONTROL				= 12368,	//You do not have control of yourself right now.
        ALREADY_CASTING				= 12442,	//You are already casting a spell!
        NOT_HOLDING_ITEM			= 12452,	//You are not holding an item!
        LDON_SENSE_TRAP3			= 12476,	//You don't sense any traps.
        INTERRUPT_SPELL_OTHER		= 12478,	//%1's casting is interrupted!
        YOU_HIT_NONMELEE			= 12481,	//You were hit by non-melee for %1 damage.
        BEAM_SMILE					= 12501,	//%1 beams a smile at %2
        SONG_ENDS_ABRUPTLY			= 12686,	//Your song ends abruptly.
        SONG_ENDS					= 12687,	//Your song ends.
        SONG_ENDS_OTHER				= 12688,	//%1's song ends.
        SONG_ENDS_ABRUPTLY_OTHER	= 12689,	//%1's song ends abruptly.
        DIVINE_AURA_NO_ATK			= 12695,	//You can't attack while invulnerable!
        TRY_ATTACKING_SOMEONE		= 12696,	//Try attacking someone other than yourself, it's more productive.
        BACKSTAB_WEAPON				= 12874,	//You need a piercing weapon as your primary weapon in order to backstab
        MORE_SKILLED_THAN_I			= 12931,	//%1 tells you, 'You are more skilled than I!  What could I possibly teach you?'
        SURNAME_EXISTS				= 12939,	//You already have a surname.  Operation failed.
        SURNAME_LEVEL				= 12940,	//You can only submit a surname upon reaching the 20th level.  Operation failed.
        SURNAME_TOO_LONG			= 12942,	//Surname must be less than 20 characters in length.
        NOW_INVISIBLE				= 12950,	//%1 is now Invisible.
        NOW_VISIBLE					= 12951,	//%1 is now Visible.
        GUILD_NOT_MEMBER2			= 12966,	//You are not in a guild.
        DISC_LEVEL_USE_ERROR		= 13004,	//You are not sufficient level to use this discipline.
        Toggle_On					= 13172,	//Asking server to turn ON your incoming tells.
        Toggle_Off					= 13173,	//Asking server to turn OFF all incoming tells for you.
        Duel_Inprogress				= 13251,	//You have already accepted a duel with someone else cowardly dog.
        Generic_Miss				= 15041     //%1 missed %2
    }

    internal enum MessageChannel
    {
        Guild   = 0,
        Group   = 2,
        Shout   = 3,
        Auction = 4,
        OOC     = 5,
        Broadcast = 6,
        Tell    = 7,
        Say     = 8,
        GMSay   = 11,
        Raid    = 15
    }

    internal enum MessageFilter
    {
        None        = 0,
        GuildChat   = 1,    // 0=hide, 1=show
        Social      = 2,    // 0=hide, 1=show
        GroupChat   = 3,    // 0=hide, 1=show
        Shout       = 4,    // 0=hide, 1=show
        Auction     = 5,    // 0=hide, 1=show
        OOC         = 6,    // 0=hide, 1=show
        BadWord     = 7,    // 0=hide, 1=show
        PCSpell     = 8,    // 0=hide, 1=show
        NPCSpell    = 9,	// 0=show, 1=hide
        BardSong    = 10,	// 0=show, 1=mine only, 2=group only, 3=hide
        SpellCrit   = 11,	// 0=show, 1=mine only, 2=hide
        MeleeCrit   = 12,	// 0=show, 1=hide
        SpellDamage = 13,	// 0=show, 1=mine only, 2=hide
        MyMiss      = 14,	// 0=hide, 1=show
        OthersMiss  = 15,	// 0=hide, 1=show
        OthersHit   = 16,	// 0=hide, 1=show
        MissedMe    = 17,	// 0=hide, 1=show
        DmgShield   = 18,	// 0=show, 1=hide
        DOT         = 19,	// 0=show, 1=hide
        PetHit      = 20,	// 0=show, 1=hide
        PetMiss     = 21,	// 0=show, 1=hide
        FocusEffect = 22,	// 0=show, 1=hide
        PetSpell    = 23,	// 0=show, 1=hide
        HOT         = 24,	// 0=show, 1=hide
        Unknown25   = 25,
        Unknown26   = 26,
        Unknown27   = 27,
        Unknown28   = 28
    }

    internal class MessagingManager
    {
        private const char COMMAND_CHAR = '!';
        public const int SAY_RANGE = 200;

        protected static readonly ILog _log = LogManager.GetLogger(typeof(MessagingManager));

        private ZonePlayer _zp = null;
        private Dictionary<string, CommandEntry> _cmdHandlers = null;   // Key is cmd name
        private ArgumentSemanticAnalyzer _analyzer = null;

        public MessagingManager (ZonePlayer zp)
        {
            _zp = zp;
            LoadCommandHandlers();
        }

        private void LoadCommandHandlers()
        {
            _cmdHandlers = new Dictionary<string, CommandEntry>(100);
            _cmdHandlers.Add("damage", 
                new CommandEntry() { 
                    ReqGmStatus = 100, 
                    Usage = "[/amt:1] [/type:4 (hand-to-hand)] [/aggro:0]",
                    Description = "Causes an amount of a type of damage with optional aggro created.",
                    Command = new Action<Dictionary<string, string>>(CmdDamage)
                });
            _cmdHandlers.Add("kill",
                new CommandEntry()
                {
                    ReqGmStatus = 100,
                    Usage = "",
                    Description = "Kills the targetted mob, optionally giving you the xp.",
                    Command = new Action<Dictionary<string, string>>(CmdKill)
                });
            _cmdHandlers.Add("zone",
                new CommandEntry()
                {
                    ReqGmStatus = 100,
                    Usage = "{/name:zoneName} [/x:0] [y:0] [z:0]",
                    Description = "Zones you to the [safe] coords in the named zone.",
                    Command = new Action<Dictionary<string, string>>(CmdZone)
                });
            _cmdHandlers.Add("summonitem",
                new CommandEntry()
                {
                    ReqGmStatus = 100,
                    Usage = "{/id:itemId}",
                    Description = "Gives you the item of the specified Id.",
                    Command = new Action<Dictionary<string, string>>(CmdSummonItem)
                });
        }

        internal void SendSpecialMessage(MessageType mt, string message)
        {
            // TODO: add message filtering once filters are in

            SpecialMessage msg = new SpecialMessage((uint)mt, message);
            EQRawApplicationPacket msgPack = new EQRawApplicationPacket(AppOpCode.SpecialMesg, _zp.Client.IPEndPoint, msg.Serialize());
            _zp.Client.SendApplicationPacket(msgPack, true);
        }

        internal void SendSpecialMessage(MessageType mt, string message, params object[] messageArgs)
        {
            // TODO: add message filtering once filters are in

            string fm = string.Format(message, messageArgs);

            SendSpecialMessage(mt, fm);
        }

        internal void SendMessageID(uint type, MessageStrings msgStr)
        {
            // TODO: add message filtering once filters are in

            SimpleMessage sm = new SimpleMessage() { Color = type, StringID = (uint)msgStr };
            EQApplicationPacket<SimpleMessage> smPack = new EQApplicationPacket<SimpleMessage>(AppOpCode.SimpleMessage, sm);
            _zp.Client.SendApplicationPacket(smPack, true);
        }

        /// <summary>Sends a formatted message.</summary>
        /// <param name="msgTxt">Separate multiple messages by null terminators.</param>
        internal void SendMessageID(uint type, MessageStrings msgStr, string msgTxt)
        {
            // TODO: add message filtering once filters are in

            if (type == (uint)MessageType.Emote)
                type = 4;

            FormattedMessage fm = new FormattedMessage() { StringId = (uint)msgStr, MsgType = type, MsgText = msgTxt };
            EQRawApplicationPacket fmPack = new EQRawApplicationPacket(AppOpCode.FormattedMessage, _zp.Client.IPEndPoint, fm.Serialize());
            _zp.Client.SendApplicationPacket(fmPack, true);
        }

        /// <summary>Sends a formatted message.</summary>
        /// <param name="msgTxt">Separate multiple messages by null terminators.</param>
        internal void SendMessageID(uint type, MessageStrings msgStr, byte[] msgTxt)
        {
            // TODO: add message filtering once filters are in

            if (type == (uint)MessageType.Emote)
                type = 4;

            FormattedMessageBytes fmb = new FormattedMessageBytes() { StringId = (uint)msgStr, MsgType = type, MsgBytes = msgTxt };
            EQRawApplicationPacket fmPack = new EQRawApplicationPacket(AppOpCode.FormattedMessage, _zp.Client.IPEndPoint, fmb.Serialize());
            _zp.Client.SendApplicationPacket(fmPack, true);
        }

        /// <summary>Sends a formatted message.</summary>
        internal void SendMessageID(uint type, MessageStrings msgStr, params string[] messages)
        {
            SendMessageID(type, msgStr, string.Join("\0", messages));
        }

        /// <summary>Maps the filterable channel messages to the proper filter.</summary>
        internal static MessageFilter GetChannelMessageFilter(MessageChannel mc)
        {
            switch (mc) {
                case MessageChannel.Guild:
                    return MessageFilter.GuildChat;
                case MessageChannel.Group:
                    return MessageFilter.GroupChat;
                case MessageChannel.Shout:
                    return MessageFilter.Shout;
                case MessageChannel.Auction:
                    return MessageFilter.Auction;
                case MessageChannel.OOC:
                    return MessageFilter.OOC;
                default:
                    return MessageFilter.None;
            }
        }

        internal void ReceiveChannelMessage(string targetName, string message, int chanId, int langId, int langSkill)
        {
            if (string.IsNullOrEmpty(targetName))
                targetName = _zp.TargetMob == null ? null : _zp.TargetMob.Name;

            // TODO: cap the messages being sent per client to limit spamming

            // TODO: implement all channels
            MessageChannel chan = (MessageChannel)chanId;
            switch (chan) {
                case MessageChannel.Guild:
                    // TODO: implement when guilds are in
                    SendMessageID(0, MessageStrings.GUILD_NOT_MEMBER);
                    break;
                case MessageChannel.Group:
                    // TODO: implement when groups are in
                    break;
                case MessageChannel.Shout:
                    // TODO: check for pet with voice graft ability

                    _zp.OnChannelMessage(new ChannelMessageEventArgs() { Channel = chan, From = _zp, Language = (Language)langId, LangSkill = langSkill, Message = message });
                    break;
                case MessageChannel.Auction:
                    // TODO: perhaps implement a server wide auction channel

                    // TODO: check for pet with voice graft ability

                    _zp.OnChannelMessage(new ChannelMessageEventArgs() { Channel = chan, From = _zp, Language = (Language)langId, LangSkill = 100, Message = message });
                    break;
                case MessageChannel.OOC:
                    // TODO: throttled ooc, server wide ooc

                    // TODO: check for pet with voice graft ability

                    _zp.OnChannelMessage(new ChannelMessageEventArgs() { Channel = chan, From = _zp, Language = (Language)langId, LangSkill = 100, Message = message });
                    break;
                case MessageChannel.Tell:
                    // TODO: implement channel messaging on world server

                    break;
                case MessageChannel.Say:
                    if (message[0] == COMMAND_CHAR && message.Length > 1) {
                        _log.DebugFormat("Received command '{0}'", message);
                        ProcessCommand(message.Substring(1));   // strip the command char
                        break;
                    }

                    // TODO: check for pet with voice graft ability

                    // Send channel message to nearby clients
                    _zp.OnChannelMessage(new ChannelMessageEventArgs() { Channel = chan, From =  _zp, Language = (Language)langId, LangSkill = langSkill, Message = message });

                    // TODO: check if target is NPC and handle possible quest chat, etc.

                    break;
                case MessageChannel.Broadcast:
                case MessageChannel.GMSay:
                    if (_zp.GMStatus < ZonePlayer.MIN_STATUS_TO_USE_GM_CMDS)
                        SendSpecialMessage(MessageType.Default, "Only GMs can use this chat channel.");

                    // TODO: implement channel messaging on world server

                    break;
                case MessageChannel.Raid:
                    break;
                default:
                    SendSpecialMessage(MessageType.Default, "Channel {0} not implemented.", chanId);
                    break;
            }
        }

        /// <summary>Processes various commands sent from the client. Commands begin with the ! character.</summary>
        /// <remarks>Access Levels for commands:
        ///     0   Normal
        ///     10  Steward
        ///     20  Apprentice Guide
        ///     50  Guide
        ///     75  Senior Guide
        ///     85  GM-Tester
        ///     100 GM-Admin
        ///     150 GM-LeadAdmin
        ///     250 GM-SysAdmin
        ///     
        ///     NOTE: command parameters aren't validated for presence.  If a parameter is required, check for it in the handler.
        /// </remarks>
        private void ProcessCommand(string userCmdText)
        {
            // Parse the command & any argument(s) and then execute the command, if valid

            // First get the actual command
            userCmdText = userCmdText.Trim().ToLower();
            int spacePos = userCmdText.IndexOf(' ');
            string cmd = spacePos >= 0 ? userCmdText.Substring(0, spacePos) : userCmdText;
            CommandEntry cmdEntry = null;

            // Next try to find a matching command
            if (!_cmdHandlers.ContainsKey(cmd)) {
                SendSpecialMessage(MessageType.Default, "Unknown command !" + cmd + " Type !cmdlist to see available commands.");
                _log.WarnFormat("Received unknown command {0} from {1}", "!" + cmd, _zp.Name);
                return;
            }
            else
                cmdEntry = _cmdHandlers[cmd];

            IEnumerable<Argument> userArgs = null;
            Dictionary<string, string> argStrs = null;
            if (cmd != userCmdText) {
                // Next, if the actual command has switches, parse them
                string goodCmdText = cmdEntry.Usage.Replace("{", "").Replace("}", "").Replace("[", "").Replace("]", "");

                // Then validate the user entered switches
                userArgs = (from arg in userCmdText.Substring(spacePos + 1).Split(' ')
                            select new Argument(arg)).ToArray();

                _analyzer = new ArgumentSemanticAnalyzer();
                // Setup the actual cmd switches
                foreach (var arg in goodCmdText.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries)) {
                    // Add the verifier, 
                    int switchLen = arg.IndexOf(":");  // Just get the switch
                    if (switchLen < 0)
                        throw new CommandSyntaxException(cmd);

                    _analyzer.AddArgumentVerifier(new ArgumentDefinition(arg.Substring(0, switchLen), arg, cmdEntry.Description, x => x.IsCompoundSwitch));
                }

                // If the user entered command isn't valid, display error and proper usage
                if (!_analyzer.VerifyArguments(userArgs)) {
                    string invalidArgs = _analyzer.InvalidArgumentsDisplay();
                    SendSpecialMessage(MessageType.Default, invalidArgs);
                    SendSpecialMessage(MessageType.Default, ShowCmdUsage());
                    return;
                }

                argStrs = userArgs.ToDictionary(ua => ua.Switch, ua => ua.SubArguments[0]);
            }

            cmdEntry.Command(argStrs);    // Finally, execute the command
        }

        private string ShowCmdUsage()
        {
            StringBuilder sb = new StringBuilder(128);
            foreach (ArgumentDefinition definition in _analyzer.ArgumentDefinitions)
                sb = sb.AppendFormat("{0}: ({1}) Syntax: {2}", definition.ArgumentSwitch, definition.Description, definition.Syntax);

            return sb.ToString();
        }

        internal void CmdDamage(Dictionary<string, string> arguments)
        {
            if (_zp.TargetMob == null) {
                SendSpecialMessage(MessageType.Default, "You need a target before you go trying to damage something!");
                return;
            }

            string amountStr = "1", typeStr = "4", aggroStr = "1";  // defaults
            int amount, type;
            bool aggro;

            if (arguments != null) {
                if (arguments.ContainsKey("amt")) amountStr = arguments["amt"];
                if (arguments.ContainsKey("type")) typeStr = arguments["type"];
                if (arguments.ContainsKey("aggro")) aggroStr = arguments["aggro"];
            }

            amount = int.Parse(amountStr);
            type = int.Parse(typeStr);
            aggro = aggroStr == "1";

            _zp.TargetMob.Damage(aggro ? _zp : null, amount, 0, (Skill)type);
        }

        internal void CmdKill(Dictionary<string, string> arguments)
        {
            if (_zp.TargetMob == null) {
                SendSpecialMessage(MessageType.Default, "You need a target before you can kill it!");
                return;
            }

            if (_zp.TargetMob == _zp) {
                SendSpecialMessage(MessageType.Default, "To kill self, use !die instead.");
                return;
            }

            _zp.TargetMob.Kill();
        }

        internal void CmdZone(Dictionary<string, string> arguments)
        {
            if (arguments != null && arguments.ContainsKey("name"))
                _zp.OnServerCommand(new ServerCommandEventArgs(ServerCommand.Zone, arguments));
            else
                SendSpecialMessage(MessageType.Default, "Invalid args - usage: " + _cmdHandlers["zone"].Usage);
        }

        internal void CmdSummonItem(Dictionary<string, string> arguments)
        {
            string itemIdStr = "0";
            int itemId;

            if (arguments != null)
                arguments.TryGetValue("id", out itemIdStr);

            itemId = int.Parse(itemIdStr);

            // Get the inventory item we're summoning
            InventoryItem invItem = new InventoryItem();
            invItem.Charges = 1;
            Item item = null;
            using (EmuDataContext dbCtx = new EmuDataContext()) {
                dbCtx.ObjectTrackingEnabled = false;
                item = dbCtx.Items.SingleOrDefault(i => i.ItemID == itemId);
            }

            if (item == null) {
                SendSpecialMessage(MessageType.Default, "No item found with that Id.");
                return;
            }

            invItem.Item = item;

            if (_zp.TargetMob == null)
                _zp.GiveItem(invItem, (int)InventorySlot.Cursor);
            else {
                if (_zp.TargetMob is NpcMob)
                    ((NpcMob)_zp.TargetMob).GiveItem(invItem);
                else if (_zp.TargetMob is ZonePlayer)
                    ((ZonePlayer)_zp.TargetMob).GiveItem(invItem, (int)InventorySlot.Cursor);

                SendSpecialMessage(MessageType.Default, "Gave {0} to {1}.", item.Name, _zp.TargetMob.Name);
            }
        }
    }
}
