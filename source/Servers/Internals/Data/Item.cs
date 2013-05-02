using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using EQEmulator.Servers.Internals.Entities;

namespace EQEmulator.Servers.Internals.Data
{
    internal enum ItemType : byte
    {
        OneHandSlash    = 0,
        TwoHandSlash    = 1,
        Pierce          = 2,
        OneHandBash     = 3,
        TwoHandBash     = 4,
        Bow             = 5,
        Throwing        = 7,
        Shield          = 8,
        Armor           = 10,
        Misc            = 11,   // A lot of random stuff has this item type
        LockPick        = 12,
        Food            = 14,
        Drink           = 15,
        LightSource     = 16,
        Stackable       = 17,   // Not all stackable items are this item type...
        Bandage         = 18,
        ThrowingV2      = 19,
        Spell           = 20,   // Spells and tomes
        Potion          = 21,
        WindInstrument  = 23,
        StringInstrument= 24,
        BrassInstrument = 25,
        DrumInstrument  = 26,
        Arrow           = 27,
        Jewelry         = 29,
        Skull           = 30,
        Tome            = 31,
        Note            = 32,
        Key             = 33,
        Coin            = 34,
        TwoHandPierce   = 35,
        FishingPole     = 36,
        FishingBait     = 37,
        Alcohol         = 38,
        Compass         = 40,
        Posion          = 42,
        HandToHand      = 45,
        Singing         = 50,
        AllInstruments  = 51,
        Charm           = 52,
        Augment         = 54,
        AugmentSolvent  = 55,
        AugmentDistill  = 56
    }

    internal enum ItemEffectType
    {
        CombatProc  = 0,
        ClickEffect = 1,
        WornEffect  = 2,
        Expendable  = 3,
        EquipClick  = 4,
        ClickEffect2= 5,    // Prolly need a better name
        Focus       = 6,
        Scroll      = 7
    }

    internal partial class Item
    {
        internal const int NORENT_EXPIRATION = 1800;    // seconds until norent items expire
        internal const int ITEM_CLASS_COMMON = 0;
        internal const int ITEM_CLASS_CONTAINER = 1;
        internal const int ITEM_CLASS_BOOK = 2;

        private static int _nextItemSerialNum = 1;
        private static object _serialNumLock = new object();

        private int _serialNum;

        internal static int GetNextItemSerialNumber()   // TODO: this will have to move to world server if we scale out to multiple boxes
        {
            lock (_serialNumLock) {
                if (_nextItemSerialNum == int.MaxValue)
                    _nextItemSerialNum = 1;

                return _nextItemSerialNum++;
            }
        }

        #region Properties
        public int SerialNumber
        {
            get { return _serialNum; }
        }

        public bool IsNoRent
        {
            get { return this.NoRent == 0 ? true : false; }
            set { this.NoRent = (value ? (byte)255 : (byte)0); }
        }

        public bool IsNoDrop
        {
            get { return this.NoDrop == 0 ? true : false; }
            set { this.NoDrop = (value ? (byte)255 : (byte)0); }
        }

        public bool IsWeapon
        {
            get
            {
                if (ItemClass != ITEM_CLASS_COMMON)
                    return false;

                if (ItemType == (byte)Data.ItemType.Arrow && Damage != 0)
                    return true;
                else
                    return ((Damage != 0) && (Delay != 0));
            }
        }

        public bool IsTwoHandedWeapon
        {
            get
            {
                if (!this.IsWeapon)
                    return false;

                if (this.ItemType == (byte)Data.ItemType.TwoHandSlash
                    || this.ItemType == (byte)Data.ItemType.TwoHandBash
                    || this.ItemType == (byte)Data.ItemType.TwoHandPierce) {
                    return true;
                }
                else
                    return false;
            }
        }

        public bool IsAmmo
        {
            get
            {
                if (ItemType == (byte)EQEmulator.Servers.Internals.Data.ItemType.Arrow ||
                    ItemType == (byte)EQEmulator.Servers.Internals.Data.ItemType.Throwing ||
                    ItemType == (byte)EQEmulator.Servers.Internals.Data.ItemType.ThrowingV2)
                    return true;

                return false;
            }
        }

        public bool IsExpendable
        {
            get
            {
                return (ClickType == (int)ItemEffectType.Expendable) || (ItemType == (byte)EQEmulator.Servers.Internals.Data.ItemType.Potion);
            }
        }

        public bool IsLore
        {
            get
            {
                return this.LoreGroup != 0;     // Items use LoreGroup to indicate lore (a non-zero value mean lore)
            }
        }

        public bool IsContainer
        {
            get
            {
                return this.ItemClass == ITEM_CLASS_CONTAINER;
            }
        }
        #endregion

        partial void OnCreated()
        {
            _serialNum = Item.GetNextItemSerialNumber();
        }

        /// <summary>Validates this item's abililty to be placed into the specified slotId.</summary>
        internal bool ValidateSlot(int slotId)
        {
            if (slotId <= (int)InventorySlot.EquipSlotsEnd)
                return ValidateEquipable(slotId);

            return true;
        }

        /// <summary>Validates this item's abililty to be EQUIPPED into the specified slotId.</summary>
        internal bool ValidateEquipable(int slotId)
        {
            if (slotId > (int)InventorySlot.EquipSlotsEnd)
                throw new ArgumentException("Specified slot Id is not an equipable slot", "slotId");

            if (this.slots == 0)
                return false;

            return (this.slots & (1 << slotId)) > 0;
        }

        /// <summary>Validates this item's abililty to be equipped by the specified race, class, and level.</summary>
        internal bool ValidateEquipable(int charRace, int charClass, int charLevel)
        {
            if (this.slots == 0)
                return false;

            int raceTmp = Character.GetRaceIndex((CharRaces)charRace);  // necessary (for later races) for bit field mapping in item data
            bool passedClass = (this.Classes & (1 << (charClass - 1))) > 0;
            bool passedRace = (this.Races & (1 << (raceTmp - 1))) > 0;
            bool passedLevel = this.ReqLevel <= charLevel;

            // TODO: validate deity

            return passedClass && passedRace && passedLevel;
        }

        /// <summary>Validates this item's abililty to be equipped by the specified race, class, level and slotId.</summary>
        internal bool ValidateEquipable(int charRace, int charClass, int charLevel, int slotId)
        {
            // TODO: validate deity
            return ValidateEquipable(charRace, charClass, charLevel) && ValidateEquipable(slotId);
        }

        public override string ToString()
        {
            return this.Name + "(" + this.ItemID + ")";
        }
    }
}
