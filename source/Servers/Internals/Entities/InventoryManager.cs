using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using log4net;
using EQEmulator.Servers.Internals.Data;
using System.Threading;

namespace EQEmulator.Servers.Internals.Entities
{
    /*
     *  Numbering for personal inventory goes top to bottom, then left to right
     *  It's the opposite for inside bags: left to right, then top to bottom
     *  Example:
     *      inventory:	containers:
     *	    1 6			1 2
     *	    2 7			3 4
     *	    3 8			5 6
     *	    4 9			7 8
     *	    5 10		9 10
     *
     *  Personal inventory goes from 0 (Charm) to 29 (last of the 8 main slots)
     *  Personal inventory bags (of which there are 8) are as such:
     *      22: 251->260
     *      23: 261->270
     *      24: 271->280
     *      25: 281->290
     *      26: 291->300
     *      27: 301->310
     *      28: 311->320
     *      29: 321->330
    */
    internal enum InventorySlot
    {
        Charm               = 0,
        PersonalBegin       = Charm,
        Ear1                = 1,
        Head                = 2,
        Face                = 3,
        Ear2                = 4,
        Neck                = 5,
        Shoulder            = 6,
        Arms                = 7,
        Back                = 8,
        Bracer1             = 9,
        Bracer2             = 10,
        Range               = 11,
        Hands               = 12,
        Primary             = 13,
        Secondary           = 14,
        Ring1               = 15,
        Ring2               = 16,
        Chest               = 17,
        Legs                = 18,
        Feet                = 19,
        Waist               = 20,
        Ammo                = 21,
        EquipSlotsEnd       = Ammo,
        PersonalSlotsBegin  = 22,
        PersonalSlotsEnd    = 29,
        PersonalEnd         = PersonalSlotsEnd,
        Cursor              = 30,
        PersonalBagsBegin   = 251,
        PersonalBagsEnd     = 330,
        CursorBagBegin      = 331,
        CursorBagEnd        = 340,
        Tradeskill          = 1000,
        Augment             = 1001,
        BankBegin           = 2000,
        BankEnd             = 2015,
        BankBagsBegin       = 2031,
        BankBagsEnd         = 2190,
        SharedBankBegin     = 2500,
        SharedBankEnd       = 2501,
        SharedBankBagsBegin = 2531,
        SharedBankBagsEnd   = 2550,
        TradeWindowBegin    = 3000,
        TradeWindowEnd      = 3007,
        CursorEnd           = 0xFFFE,
        Invalid             = 0xFFFF
    }

    internal enum EquipableType : byte    // indexes into item arrays (called materials in orig emu - not sure why... material is armor appearance related)
    {
        Head        = 0,
        Chest       = 1,
        Arms        = 2,
        Bracer      = 3,
        Hands       = 4,
        Legs        = 5,
        Feet        = 6,
        Primary     = 7,
        Secondary   = 8,
        Unequipable = 0xFF
    }

    [Flags]
    internal enum InventoryLocations
    {
        None        = 0,
        Equipped    = 1 << 0,   // Actually worn
        Personal    = 1 << 1,   // On the character somewhere
        Bank        = 1 << 2,
        SharedBank  = 1 << 3,
        Trading     = 1 << 4,
        Cursor      = 1 << 5,
        All         = (Equipped | Personal | Bank | SharedBank | Trading | Cursor)
    }

    #region Event Data Structures
    internal class ItemMoveEventArgs : EventArgs
    {
        internal uint FromSlotID { get; set; }
        internal uint ToSlotID { get; set; }    // Use 0xFFFFFFFF for deletes.
        internal uint Quantity { get; set; }    // A value greater than zero signifies deleting items from a stack.
        internal bool NotifyClient { get; set; }

        internal ItemMoveEventArgs(uint fromSlotID, uint toSlotID, uint quantity, bool notifyClient)
        {
            this.FromSlotID = fromSlotID;
            this.ToSlotID = toSlotID;
            this.Quantity = quantity;
            this.NotifyClient = notifyClient;
        }
    }

    internal class ItemChargeUseEventArgs : EventArgs
    {
        internal uint SlotID { get; set; }
        internal uint Charges { get; set; }

        internal ItemChargeUseEventArgs(uint slotID, uint charges)
        {
            this.SlotID = slotID;
            this.Charges = charges;
        }
    }
    #endregion

    /// <summary></summary>
    /// <remarks>
    /// A char's inventory is comprised of:
    ///     the cursor queue, bank, shared bank, personal inventory slots, equipable slots (on body) and the various bag slots for inventory,
    ///     bank, shared bank, cursor and trade window.
    /// </remarks>
    internal class InventoryManager
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(InventoryManager));

        internal const int MAX_ITEMS_PER_BAG = 10;
        internal const uint DELETE_SLOT = 0xFFFFFFFF;   // Recognized by client for destroying an item

        // Events raised by various inventory actions. Subscribed to by the ZonePlayer.
        internal event EventHandler<ItemMoveEventArgs> ItemMoved;   // Also used for deletes.
        internal event EventHandler<ItemChargeUseEventArgs> ItemChargeUsed;

        // TODO: implement getters and setters for each of the inventory types
        private Dictionary<int, InventoryItem> _invItems = null;   // keyed by inventory slot id
        private Queue<InventoryItem> _cursorQueue = new Queue<InventoryItem>(5);    // Items on cursor are in a queue, not just a slot
        private ZonePlayer _zp = null;

        public InventoryManager(IList<InventoryItem> items)
            : this(items, null) { }

        internal InventoryManager(IList<InventoryItem> items, ZonePlayer zp)
        {
            _zp = zp;
            //_log.DebugFormat("InvMgr initializing wtih {0} items.", items.Count);
            _invItems = new Dictionary<int, InventoryItem>(items.Count + 10);
            foreach (InventoryItem invItem in items) {     // Shouldn't need to worry about the cursor queue here - should only be loading slots
                //_log.DebugFormat("InvMgr loading {0} in slot {1}", invItem.Item.Name, invItem.SlotID);
                _invItems[invItem.SlotID] = invItem;
            }

            CalculateSubItems();
        }

        #region Static members
        internal static InventorySlot GetEquipableSlot(EquipableType equipType)
        {
            switch (equipType) {
                case EquipableType.Head:
                    return InventorySlot.Head;
                case EquipableType.Chest:
                    return InventorySlot.Chest;
                case EquipableType.Arms:
                    return InventorySlot.Arms;
                case EquipableType.Bracer:
                    return InventorySlot.Bracer1;
                case EquipableType.Hands:
                    return InventorySlot.Hands;
                case EquipableType.Legs:
                    return InventorySlot.Legs;
                case EquipableType.Feet:
                    return InventorySlot.Feet;
                case EquipableType.Primary:
                    return InventorySlot.Primary;
                case EquipableType.Secondary:
                    return InventorySlot.Secondary;
                default:
                    return InventorySlot.Invalid;
            }
        }

        internal static EquipableType GetEquipableType(InventorySlot invSlot)
        {
            switch (invSlot) {
                case InventorySlot.Head:
                    return EquipableType.Head;
                case InventorySlot.Arms:
                    return EquipableType.Arms;
                case InventorySlot.Bracer1:
                    return EquipableType.Bracer;
                case InventorySlot.Bracer2:
                    return EquipableType.Bracer;
                case InventorySlot.Hands:
                    return EquipableType.Hands;
                case InventorySlot.Primary:
                    return EquipableType.Primary;
                case InventorySlot.Secondary:
                    return EquipableType.Secondary;
                case InventorySlot.Chest:
                    return EquipableType.Chest;
                case InventorySlot.Legs:
                    return EquipableType.Legs;
                case InventorySlot.Feet:
                    return EquipableType.Feet;
                default:
                    return EquipableType.Unequipable;
            }
        }
        
        internal static bool SlotSupportsContainers(int slotID)
        {
            if ((slotID >= (int)InventorySlot.PersonalSlotsBegin && slotID <= (int)InventorySlot.PersonalSlotsEnd) ||
                (slotID >= (int)InventorySlot.BankBegin && slotID <= (int)InventorySlot.BankEnd) ||
                (slotID >= (int)InventorySlot.SharedBankBegin && slotID <= (int)InventorySlot.SharedBankEnd) ||
                (slotID == (int)InventorySlot.Cursor) ||			    // Cursor
                (slotID >= (int)InventorySlot.TradeWindowBegin && slotID <= (int)InventorySlot.TradeWindowEnd))	// Trade window
                return true;
            
            return false;
        }

        internal static bool SlotIsInContainer(int slotID)
        {
            if ((slotID >= (int)InventorySlot.PersonalBagsBegin && slotID <= (int)InventorySlot.PersonalBagsEnd) ||
                (slotID >= (int)InventorySlot.BankBagsBegin && slotID <= (int)InventorySlot.BankBagsEnd) ||
                (slotID >= (int)InventorySlot.SharedBankBagsBegin && slotID <= (int)InventorySlot.SharedBankBagsEnd))
                return true;
            
            return false;
        }

        /// <summary>Gets the slotID for an item within a container.</summary>
        internal static int GetSlotIdWithinContainer(int containerSlotID, int containerIdx)
        {
            int slotIdx = 0;

            // Get the first slot in the container
            if (containerSlotID == (int)InventorySlot.Cursor)
                slotIdx = (int)InventorySlot.CursorBagBegin;
            else if (containerSlotID >= (int)InventorySlot.PersonalSlotsBegin && containerSlotID <= (int)InventorySlot.PersonalSlotsEnd)
                slotIdx = (int)InventorySlot.PersonalBagsBegin + ((containerSlotID - (int)InventorySlot.PersonalSlotsBegin) * MAX_ITEMS_PER_BAG);
            else if (containerSlotID >= (int)InventorySlot.BankBegin && containerSlotID <= (int)InventorySlot.BankEnd)
                slotIdx = (int)InventorySlot.BankBagsBegin + ((containerSlotID - (int)InventorySlot.BankBegin) * MAX_ITEMS_PER_BAG);
            else if (containerSlotID >= (int)InventorySlot.BankBegin && containerSlotID <= (int)InventorySlot.BankEnd)
                slotIdx = (int)InventorySlot.SharedBankBagsBegin + ((containerSlotID - (int)InventorySlot.SharedBankBegin) * MAX_ITEMS_PER_BAG);
            else
                throw new ArgumentOutOfRangeException("containerSlotID", "Specified slot ID (" + containerSlotID.ToString() + ") invalid for a container.");

            //_log.DebugFormat("For container at slot {0}, calculating the slotID for container idx {1} as {2}", containerSlotID, containerIdx, slotIdx + containerIdx);
            return slotIdx + containerIdx;  // Return the first slot plus the index of the item within
        }
        #endregion

        private void CalculateSubItems()
        {
            CalculateSubItems(InventoryLocations.All);
        }

        /// <summary>Determines if any containers have sub-items and adds them to the relevant InventoryItem.</summary>
        internal void CalculateSubItems(InventoryLocations invLoc)
        {
            InventoryItem parentItem = null, subItem = null;
            int slotsIdx = 0, bagIdx = 0, bagRangeLow = 0, bagRangeHigh = 0;

            // Personal inventory bags
            if ((invLoc & InventoryLocations.Personal) == InventoryLocations.Personal) {
                for (int i = (int)InventorySlot.PersonalSlotsBegin; i <= (int)InventorySlot.PersonalSlotsEnd; i++) {
                    if (_invItems.TryGetValue(i, out parentItem)) {
                        if (parentItem != null && parentItem.Item.ItemClass == Item.ITEM_CLASS_CONTAINER) {
                            // Looks like we are a container in a personal inventory slot
                            //_log.DebugFormat("{0} is a container in personal inventory slot {1}", parentItem.Item.Name, i);
                            parentItem.ClearSubItems();
                            slotsIdx = (i - (int)InventorySlot.PersonalBagsBegin) % MAX_ITEMS_PER_BAG;
                            bagRangeLow = (int)InventorySlot.PersonalBagsBegin + slotsIdx * MAX_ITEMS_PER_BAG;
                            bagRangeHigh = (int)InventorySlot.PersonalBagsEnd - slotsIdx * MAX_ITEMS_PER_BAG;

                            for (int bagSlotsIdx = bagRangeLow; bagSlotsIdx <= bagRangeHigh; bagSlotsIdx++) {
                                if (_invItems.TryGetValue(bagSlotsIdx, out subItem) && subItem != null) {
                                    // And we contain something
                                    //_log.DebugFormat("Containing {0}", subItem.Item.Name);
                                    bagIdx = (bagSlotsIdx - (int)InventorySlot.PersonalBagsBegin) % MAX_ITEMS_PER_BAG;
                                    //_log.DebugFormat("Calculated subitem {0} at bag index {1}, inventory index {2}", subItem.Item.Name, bagIdx, bagSlotsIdx);
                                    parentItem.AddSubItem(bagIdx, subItem);
                                }
                            }
                        }
                    }
                }
            }

            // Bank bags
            if ((invLoc & InventoryLocations.Bank) == InventoryLocations.Bank) {
                for (int i = (int)InventorySlot.BankBegin; i <= (int)InventorySlot.BankEnd; i++) {
                    if (_invItems.TryGetValue(i, out parentItem)) {
                        if (parentItem != null && parentItem.Item.ItemClass == Item.ITEM_CLASS_CONTAINER) {
                            // Looks like we are a container in a bank slot
                            _log.DebugFormat("{0} is a container in bank slot {1}", parentItem.Item.Name, i);
                            parentItem.ClearSubItems();
                            slotsIdx = (i - (int)InventorySlot.BankBagsBegin) % MAX_ITEMS_PER_BAG;
                            bagRangeLow = (int)InventorySlot.BankBagsBegin + slotsIdx * MAX_ITEMS_PER_BAG;
                            bagRangeHigh = (int)InventorySlot.BankBagsEnd - slotsIdx * MAX_ITEMS_PER_BAG;

                            for (int bagSlotsIdx = bagRangeLow; bagSlotsIdx <= bagRangeHigh; bagSlotsIdx++) {
                                if (_invItems.TryGetValue(bagSlotsIdx, out subItem) && subItem != null) {
                                    // And we contain something
                                    bagIdx = (bagSlotsIdx - (int)InventorySlot.BankBagsBegin) % MAX_ITEMS_PER_BAG;
                                    parentItem.AddSubItem(bagIdx, subItem);
                                }
                            }
                        }
                    }
                }
            }

            // Shared bank bags
            if ((invLoc & InventoryLocations.SharedBank) == InventoryLocations.SharedBank) {
                for (int i = (int)InventorySlot.SharedBankBegin; i <= (int)InventorySlot.SharedBankEnd; i++) {
                    if (_invItems.TryGetValue(i, out parentItem)) {
                        if (parentItem != null && parentItem.Item.ItemClass == Item.ITEM_CLASS_CONTAINER) {
                            // Looks like we are a container in a shared bank slot
                            _log.DebugFormat("{0} is a container in shared bank slot {1}", parentItem.Item.Name, i);
                            parentItem.ClearSubItems();
                            slotsIdx = (i - (int)InventorySlot.SharedBankBagsBegin) % MAX_ITEMS_PER_BAG;
                            bagRangeLow = (int)InventorySlot.SharedBankBagsBegin + slotsIdx * MAX_ITEMS_PER_BAG;
                            bagRangeHigh = (int)InventorySlot.SharedBankBagsEnd - slotsIdx * MAX_ITEMS_PER_BAG;

                            for (int bagSlotsIdx = bagRangeLow; bagSlotsIdx <= bagRangeHigh; bagSlotsIdx++) {
                                if (_invItems.TryGetValue(bagSlotsIdx, out subItem) && subItem != null) {
                                    // And we contain something
                                    bagIdx = (bagSlotsIdx - (int)InventorySlot.SharedBankBagsBegin) % MAX_ITEMS_PER_BAG;
                                    parentItem.AddSubItem(bagIdx, subItem);
                                }
                            }
                        }
                    }
                }
            }

            // TODO: augmentations (LoY era)
        }

        /// <summary>Attempts to get/set an Item via a slot Id.  Works for all inventory, including the cursor.</summary>
        /// <param name="slotID">The slotID, not the equipable type id, to get the item by.</param>
        /// <returns>InventoryItem with a safely loaded Item.  Null if nothing in the specified slot.</returns>
        internal InventoryItem this[int slotID]
        {
            get
            {
                if (slotID == (int)InventorySlot.Cursor)
                    return _cursorQueue.Count > 0 ? _cursorQueue.Peek() : null;
                else {
                    InventoryItem invItem;
                    _invItems.TryGetValue(slotID, out invItem);
                    return invItem;
                }
            }
            set
            {
                if (value != null) {
                    value.SlotID = slotID;
                }

                if (slotID == (int)InventorySlot.Cursor) {
                    if (value != null)
                        _cursorQueue.Enqueue(value);
                }
                else
                    _invItems[slotID] = value;
            }
        }

        /// <summary>Gets the first occurance of an item with the specified id in the specified location(s).</summary>
        internal InventoryItem GetItemByLocation(int itemId, InventoryLocations invLoc)
        {
            // TODO: Add checks for augments

            if ((invLoc & InventoryLocations.Equipped) == InventoryLocations.Equipped) {
                foreach (InventoryItem invItem in this.EquippedItems()) {   // Check more restrictive first
                    if (invItem.ItemID == itemId)
                        return invItem;
                }
            }

            if ((invLoc & InventoryLocations.Personal) == InventoryLocations.Personal) {
                foreach (InventoryItem invItem in this.AllPersonalItems()) {
                    if (invItem.ItemID == itemId)
                        return invItem;
                }
            }

            if ((invLoc & InventoryLocations.Bank) == InventoryLocations.Bank) {
                foreach (InventoryItem invItem in this.AllBankItems()) {
                    if (invItem.ItemID == itemId)
                        return invItem;
                }
            }

            if ((invLoc & InventoryLocations.SharedBank) == InventoryLocations.SharedBank) {
                foreach (InventoryItem invItem in this.AllSharedBankItems()) {
                    if (invItem.ItemID == itemId)
                        return invItem;
                }
            }

            if ((invLoc & InventoryLocations.Trading) == InventoryLocations.Trading) {  // Look in trading window slots
                foreach (InventoryItem invItem in this.TradingItems()) {
                    if (invItem.ItemID == itemId)
                        return invItem;
                }
            }

            if ((invLoc & InventoryLocations.Cursor) == InventoryLocations.Cursor) {    // Look in the cursor queue
                foreach (InventoryItem ii in _cursorQueue) {
                    if (ii.ItemID == itemId)
                        return ii;
                }
            }

            return null;
        }

        /// <summary>Gets the first occurance of an item with the specified lore group in the specified location(s).</summary>
        internal InventoryItem GetItemByLoreGroupByLocation(int loreGroupId, InventoryLocations invLoc)
        {
            // TODO: Add checks for augments

            if ((invLoc & InventoryLocations.Equipped) == InventoryLocations.Equipped) {
                foreach (InventoryItem invItem in this.EquippedItems()) {   // Check more restrictive first
                    if (invItem.Item.LoreGroup == loreGroupId)
                        return invItem;
                }
            }

            if ((invLoc & InventoryLocations.Personal) == InventoryLocations.Personal) {
                foreach (InventoryItem invItem in this.AllPersonalItems()) {
                    if (invItem.Item.LoreGroup == loreGroupId)
                        return invItem;
                }
            }

            if ((invLoc & InventoryLocations.Bank) == InventoryLocations.Bank) {
                foreach (InventoryItem invItem in this.AllBankItems()) {
                    if (invItem.Item.LoreGroup == loreGroupId)
                        return invItem;
                }
            }

            if ((invLoc & InventoryLocations.SharedBank) == InventoryLocations.SharedBank) {
                foreach (InventoryItem invItem in this.AllSharedBankItems()) {
                    if (invItem.Item.LoreGroup == loreGroupId)
                        return invItem;
                }
            }

            if ((invLoc & InventoryLocations.Trading) == InventoryLocations.Trading) {  // Look in trading window slots
                foreach (InventoryItem invItem in this.TradingItems()) {
                    if (invItem.Item.LoreGroup == loreGroupId)
                        return invItem;
                }
            }

            if ((invLoc & InventoryLocations.Cursor) == InventoryLocations.Cursor) {    // Look in the cursor queue
                foreach (InventoryItem ii in _cursorQueue) {
                    if (ii.Item.LoreGroup == loreGroupId)
                        return ii;
                }
            }

            return null;
        }

        #region Iterators
        /// <summary>Enumerates every item in the inventory.  Cursor items NOT included.</summary>
        /// <remarks>As long as the sub-items have been calculated, the enumerated items will have any sub-items represented.</remarks>
        internal IEnumerable<InventoryItem> AllItems()
        {
            foreach (KeyValuePair<int, InventoryItem> kvp in _invItems) {
                if (kvp.Value != null)
                    yield return kvp.Value;
            }
        }

        /// <summary>Enumerates equipped inventory items.  Useful for working with items as a flattened collection and not worrying
        /// about bags, etc.</summary>
        internal IEnumerable<InventoryItem> EquippedItems()
        {
            var invItems = from ii in _invItems
                           where ii.Key <= (int)InventorySlot.EquipSlotsEnd
                           select ii.Value;

            foreach (InventoryItem invItem in invItems) {
                if (invItem != null)
                    yield return invItem;
            }
        }

        /// <summary>Enumerates items in the personal inventory slots.  This includes equipped slots.</summary>
        /// <remarks>As long as the sub-items have been calculated, the enumerated items will have any sub-items represented.</remarks>
        internal IEnumerable<InventoryItem> PersonalSlotItems()
        {
            InventoryItem invItem = null;

            for (int i = (int)InventorySlot.PersonalBegin; i <= (int)InventorySlot.PersonalEnd; i++) {
                if (_invItems.TryGetValue(i, out invItem))
                    yield return invItem;
            }
        }

        /// <summary>Enumerates every personal inventory item; equipped, slots and contents of bags.  Doesn't include cursor.</summary>
        internal IEnumerable<InventoryItem> AllPersonalItems()
        {
            var invItems = from ii in _invItems
                           where ii.Key <= (int)InventorySlot.PersonalSlotsEnd 
                                || (ii.Key >= (int)InventorySlot.PersonalBagsBegin && ii.Key <= (int)InventorySlot.PersonalBagsEnd)
                           select ii.Value;

            foreach (InventoryItem invItem in invItems) {
                if (invItem != null)
                    yield return invItem;
            }
        }

        /// <summary>Enumerates items in the bank slots.</summary>
        /// <remarks>As long as the sub-items have been calculated, the enumerated items will have any sub-items represented.</remarks>
        internal IEnumerable<InventoryItem> BankSlotItems()
        {
            InventoryItem invItem = null;

            for (int i = (int)InventorySlot.BankBegin; i <= (int)InventorySlot.BankEnd; i++) {
                if (_invItems.TryGetValue(i, out invItem))
                    yield return invItem;
            }
        }

        /// <summary>Enumerates every bank inventory item - not just those in the slots.  Useful for working with items as a
        /// flattened collection and not worrying about bags, etc.</summary>
        internal IEnumerable<InventoryItem> AllBankItems()
        {
            var invItems = from ii in _invItems
                           where ((ii.Key >= (int)InventorySlot.BankBegin && ii.Key <= (int)InventorySlot.BankEnd)
                                || (ii.Key >= (int)InventorySlot.BankBagsBegin && ii.Key <= (int)InventorySlot.BankBagsEnd))
                           select ii.Value;

            foreach (InventoryItem invItem in invItems) {
                if (invItem != null)
                    yield return invItem;
            }
        }

        /// <summary>Enumerates items in the shared bank slots.</summary>
        /// <remarks>As long as the sub-items have been calculated, the enumerated items will have any sub-items represented.</remarks>
        internal IEnumerable<InventoryItem> SharedBankSlotItems()
        {
            InventoryItem invItem = null;

            for (int i = (int)InventorySlot.SharedBankBegin; i <= (int)InventorySlot.SharedBankEnd; i++) {
                if (_invItems.TryGetValue(i, out invItem))
                    yield return invItem;
            }
        }

        /// <summary>Enumerates every shared bank inventory item - not just those in the slots.  Useful for working with items as a
        /// flattened collection and not worrying about bags, etc.</summary>
        internal IEnumerable<InventoryItem> AllSharedBankItems()
        {
            var invItems = from ii in _invItems
                           where ((ii.Key >= (int)InventorySlot.SharedBankBegin && ii.Key <= (int)InventorySlot.SharedBankEnd)
                                || (ii.Key >= (int)InventorySlot.SharedBankBagsBegin && ii.Key <= (int)InventorySlot.SharedBankBagsEnd))
                           select ii.Value;

            foreach (InventoryItem invItem in invItems) {
                if (invItem != null)
                    yield return invItem;
            }
        }

        internal IEnumerable<InventoryItem> TradingItems()
        {
            var invItems = from ii in _invItems
                           where (ii.Key >= (int)InventorySlot.TradeWindowBegin) && (ii.Key <= (int)InventorySlot.TradeWindowEnd)
                           select ii.Value;

            foreach (InventoryItem invItem in invItems) {
                if (invItem != null)
                    yield return invItem;
            }
        }

        internal IEnumerable<InventoryItem> CursorItems()
        {
            foreach (InventoryItem ii in _cursorQueue)
                yield return ii;
        }
        #endregion

        internal bool ValidateSlot(InventoryItem invItem, int slotID)
        {
            if (invItem.Item.ItemClass == Item.ITEM_CLASS_CONTAINER)    // Is item a bag?
                return InventoryManager.SlotSupportsContainers(slotID);
            else if (InventoryManager.SlotIsInContainer(slotID))    // Is item IN a bag?
                return invItem.Item.Size <= GetBagForSlot(slotID).Item.BagSize;     // Can it fit?
            else if (slotID > (int)InventorySlot.EquipSlotsEnd)     // Everything else left should be cool
                return true;
            else if (invItem.Item.ValidateEquipable(_zp.Race, _zp.Class, _zp.Level, slotID))    // Can we equip it?
                return true;
            else
                return false;
        }

        /// <summary>Returns the container for the specified slotID.</summary>
        /// <returns>Null if no bag is found for the specified slotID.</returns>
        internal InventoryItem GetBagForSlot(int slotID)
        {
            //_log.DebugFormat("Trying to get the bag for slot {0}", slotID);
            int bagSlotID = -1;
            if (slotID >= (int)InventorySlot.PersonalBagsBegin && slotID <= (int)InventorySlot.PersonalBagsEnd)
                bagSlotID = (int)InventorySlot.PersonalSlotsBegin + ((slotID - (int)InventorySlot.PersonalBagsBegin) / MAX_ITEMS_PER_BAG);

            if (slotID >= (int)InventorySlot.BankBagsBegin && slotID <= (int)InventorySlot.BankBagsEnd)
                bagSlotID = (int)InventorySlot.BankBegin + ((slotID - (int)InventorySlot.BankBagsBegin) / 16);

            if (slotID >= (int)InventorySlot.SharedBankBagsBegin && slotID <= (int)InventorySlot.SharedBankBagsEnd)
                bagSlotID = (int)InventorySlot.SharedBankBegin + ((slotID - (int)InventorySlot.SharedBankBagsBegin) / 2);

            if (bagSlotID == -1)
                throw new ArgumentException("Specified slotID isn't a container slot.");

            //_log.DebugFormat("Trying to fetch the bag at slot {0}", bagSlotID);
            return _invItems[bagSlotID];
        }

        /// <summary>Clears slots of any norent items.</summary>
        /// <remarks>Shouldn't need locking here as it is only called during login.</remarks>
        internal void ClearNoRent()
        {
            List<int> slotIDs = new List<int>(10);

            foreach (KeyValuePair<int, InventoryItem> kvp in _invItems) {
                if (kvp.Value.Item.IsNoRent)
                    slotIDs.Add((int)kvp.Value.ItemID);
            }

            if (slotIDs.Count > 0)
                DeleteItems(slotIDs.ToArray(), true);
        }

        /// <summary>Erases items for the container at the given slot id.</summary>
        /// <param name="bagSlotID">slot id of the container in question.</param>
        internal void ClearBagContents(int bagSlotID)
        {
            int baseSlotID = GetSlotIdWithinContainer(bagSlotID, 0);

            for (int i = 0; i < MAX_ITEMS_PER_BAG; i++)
                this[baseSlotID + i] = null;
        }

        /// <summary>Clears the player of all items on thier person.</summary>
        internal void ClearPersonalSlotItems()
        {
            // Do this the quick way, don't need any sub-items calculated and such that DeleteItem() will try to do
            for (int i = (int)InventorySlot.PersonalBegin; i <= (int)InventorySlot.PersonalEnd; i++)
                _invItems[i] = null;

            _cursorQueue.Clear();
        }

        /// <summary>Deletes an item from the inventory.  Fire and forget... doesn't check if something is there first.</summary>
        /// <param name="slotID">Inventory slot id of the item to delete.</param>
        /// <param name="quantity">Amount of the stack to delete.  Zero for everything.</param>
        /// <param name="notifyClient">Whether we should tell the client about the deletion.</param>
        internal void DeleteItem(int slotID, byte quantity, bool notifyClient)
        {
            InventoryItem invItem = this[slotID];
            if (invItem == null)
                return;   // Don't log an error on non-existing items... this is fire and forget

            bool deleteItem = true;
            
            // Determine if object should poof or just a quantity of charges on the item should be removed
            if (quantity > 0) {
                invItem.Charges -= quantity;

                if (invItem.Charges <= 0) { // No charges left?
                    if (invItem.Item.stackable || (!invItem.Item.stackable && (invItem.Item.MaxCharges == 0 || invItem.Item.IsExpendable)))
                        deleteItem = true;      // Stackable or expendable item with no charges left
                    else
                        deleteItem = false;     // Not stackable & not an expendable item
                }
                else
                    deleteItem = false;   // Item still has charges, or it is an item that isn't expendable
            }

            if (deleteItem) {
                _invItems.Remove(slotID);   // ok if cursor, Remove() doesn't throw if non-existant key is passed to it
                
                if (InventoryManager.SlotIsInContainer(slotID))
                    CalculateSubItems();
            }

            if (slotID == (int)InventorySlot.Cursor)
                _cursorQueue.Dequeue();     // TODO: if we want to persist cursor contents to the db, issue a delete here.  Right now in-mem only

            if (notifyClient) {
                if (deleteItem) {
                    OnItemMoved(new ItemMoveEventArgs((uint)slotID, DELETE_SLOT, 0, notifyClient));
                }
                else {
                    if (!invItem.Item.stackable) {
                        // Non-stackable item with charges (e.g. item w/ clicky effect).  Delete a charge
                        OnItemChargeUsed(new ItemChargeUseEventArgs((uint)slotID, quantity));
                    }
                    else {
                        // Stackable - delete from the stack
                        OnItemMoved(new ItemMoveEventArgs((uint)slotID, DELETE_SLOT, quantity, notifyClient));
                    }
                }
            }
        }

        internal void DeleteItems(int[] slotIDs, bool notifyClient)
        {
            foreach (int slotID in slotIDs)
                DeleteItem(slotID, 0, notifyClient);
        }

        /// <summary>Moves an item.</summary>
        /// <returns>True if an item was moved, else false.</returns>
        internal bool SwapItem(uint fromSlot, uint toSlot, byte numberInStack)
        {
            if (fromSlot == toSlot)
                return false;    // Nothing further to do here... Item summon (or maybe clicked and then re-dropped to same spot)

            InventoryItem srcInvItem = this[(int)fromSlot];
            InventoryItem destInvItem = this[(int)toSlot];
            if (srcInvItem == null) {
                _log.ErrorFormat("Tried to move an item from slot {0} but that slot doesn't have anything.", fromSlot);
                return false;
            }

            //_log.DebugFormat("Source slot {0} contains {1} with {2} charges.", fromSlot, srcInvItem.Item.Name, srcInvItem.Charges);
            //_log.DebugFormat("Dest slot {0} contains {1} with {2} charges.", toSlot, destInvItem != null ? destInvItem.Item.Name : "nothing", destInvItem != null ? destInvItem.Charges : 0);

            if (toSlot == DELETE_SLOT) {    // Item deletion
                //_log.DebugFormat("Deleting item from slot {0}", fromSlot);
                DeleteItem((int)fromSlot, numberInStack, false);
                return true;
            }

            // TODO: some combat related stuff

            if (srcInvItem.Charges > 0 && (srcInvItem.Charges < numberInStack || numberInStack > srcInvItem.Item.stacksize)) {
                _log.ErrorFormat("Tried to swap {0} but {1} only has {2} charges.", numberInStack, srcInvItem.Item.Name, srcInvItem.Charges);
                return false;
            }

            // TODO: trader related crap

            // TODO: world container stuff

            // TODO: stuff related to item exchange with another player/mob

            //_log.Debug("numberInStack: " + numberInStack.ToString());
            if (numberInStack > 0) {
                if (destInvItem != null) {
                    if (destInvItem.ItemID != srcInvItem.ItemID) {  // Can items stack?
                        _log.ErrorFormat("Tried to stack slot {0} contents on slot {1} contents but they are incompatible ({2} != {3})", srcInvItem.SlotID, destInvItem.SlotID, srcInvItem.ItemID, destInvItem.ItemID);
                        return false;
                    }

                    if (destInvItem.Charges < destInvItem.Item.stacksize) {
                        // There is room in destination for more in the stack
                        int numToMove = destInvItem.Item.stacksize - destInvItem.Charges.Value;
                        numToMove = Math.Min(numToMove, numberInStack);     // only move up to how many we have room for
                        //_log.DebugFormat("Moving stack of {0} from slot {1} to slot {2} which has {3}/{4} charges", numberInStack, fromSlot, toSlot, destInvItem.Charges, destInvItem.Item.stacksize);

                        DeleteItem(srcInvItem.SlotID, (byte)numToMove, false);
                        destInvItem.Charges += (byte)numToMove;
                    }
                    else
                        return false;    // Stack is full, so do nothing
                }
                else {
                    // Nothing in dest slot
                    if (numberInStack >= srcInvItem.Charges) {
                        // Move the entire stack
                        //_log.DebugFormat("Moving the entire stack ({0}) from slot {1} to slot {2}", srcInvItem.Charges, fromSlot, toSlot);
                        if (!SwapItem(srcInvItem, (int)fromSlot, destInvItem, (int)toSlot, false))
                            _log.WarnFormat("Stack of {2} {0} cannot be placed into slot {1}", srcInvItem.Item.Name, toSlot, numberInStack);
                    }
                    else {
                        // Split stack
                        DeleteItem(srcInvItem.SlotID, numberInStack, false);
                        //_log.DebugFormat("Moving a split stack ({0}) from slot {1} to slot {2}", numberInStack, fromSlot, toSlot);
                        InventoryItem newInvItem = srcInvItem.ShallowCopy();
                        newInvItem.Charges = numberInStack;
                        this[(int)toSlot] = newInvItem;
                    }
                }
            }
            else {
                if (!SwapItem(srcInvItem, (int)fromSlot, destInvItem, (int)toSlot, true))
                    _log.WarnFormat("{0} cannot be placed into slot {1}", srcInvItem.Item.Name, toSlot);
            }

            if (InventoryManager.SlotIsInContainer((int)toSlot) || InventoryManager.SlotIsInContainer((int)fromSlot)
                || srcInvItem.Item.ItemClass == Item.ITEM_CLASS_CONTAINER || (destInvItem != null && destInvItem.Item.ItemClass == Item.ITEM_CLASS_CONTAINER))
                CalculateSubItems();

            return true;
        }

        /// <summary>Swaps a whole item or stack.</summary>
        /// <returns>True if swap occurs, else false.</returns>
        private bool SwapItem(InventoryItem srcInvItem, int fromSlotId, InventoryItem destInvItem, int toSlotId, bool fireEvent)
        {
            // Check that the source item is able to go to the dest slot and that the destination item non-existant or can be swapped
            if (ValidateSlot(srcInvItem, toSlotId) && (destInvItem == null || ValidateSlot(destInvItem, fromSlotId))) {
                if (fromSlotId == (int)InventorySlot.Cursor)
                    _cursorQueue.Dequeue(); // If we're moving something from the cursor, dequeue it

                if (srcInvItem.Item.ItemClass == Item.ITEM_CLASS_CONTAINER) { // Are we moving a container from the source slot?
                    // Clear items from any possible containers
                    ClearBagContents(toSlotId);     // We can assume the items are both containers as we've validated by this point
                    ClearBagContents(fromSlotId);

                    // Move source container's items to the destination container slots
                    int idx = 0;
                    foreach (InventoryItem invItem in srcInvItem.SubItems()) {
                        int toContSlotID = GetSlotIdWithinContainer(toSlotId, idx);
                        int fromContSlotID = GetSlotIdWithinContainer(fromSlotId, idx);
                        //_log.DebugFormat("Moving item from within a container @ slot {0} to slot {1}", fromContSlotID, toContSlotID);

                        this[toContSlotID] = invItem;
                        idx++;
                    }
                }

                if (destInvItem != null && destInvItem.Item.ItemClass == Item.ITEM_CLASS_CONTAINER) {
                    // Move destination container's items to the source container slots
                    int idx = 0;
                    int fromContSlotID = GetSlotIdWithinContainer(fromSlotId, idx);
                    foreach (InventoryItem invItem in destInvItem.SubItems())
                        this[fromContSlotID + idx++] = invItem;
                }

                // Swap the source and destination items
                this[toSlotId] = srcInvItem;
                this[fromSlotId] = destInvItem;

                if (fireEvent) {
                    OnItemMoved(new ItemMoveEventArgs((uint)fromSlotId, (uint)toSlotId, 0, false));
                    if (destInvItem != null)
                        OnItemMoved(new ItemMoveEventArgs((uint)toSlotId, (uint)fromSlotId, 0, false));
                }

                return true;
            }
            else
                return false;
        }

        internal void MoveCharges(ref InventoryItem invItemFrom, InventoryItem invItemTo)
        {
            int chargesEmpty = invItemTo.Item.stacksize - invItemTo.Charges.Value;
            int chargesToMove = invItemFrom.Charges < chargesEmpty ? invItemFrom.Charges.Value : chargesEmpty;
            invItemTo.Charges += (byte)chargesToMove;
            invItemFrom.Charges -= (byte)chargesToMove;

            ZoneServer.SendItemPacket(_zp.Client, invItemTo, invItemTo.SlotID, Packets.ItemPacketType.Trade);
            _log.DebugFormat("Moved {0} charges from item {1} in slot {2} to item {3} in slot {4}",
                chargesToMove, invItemFrom.Item.Name, invItemFrom.SlotID, invItemTo.Item.Name, invItemTo.SlotID);
        }
        
        /// <summary>Checks if the player has any lore items that conflict with the specified item.</summary>
        /// <returns>True if there is a conflict, else false.</returns>
        internal bool CheckLoreConflict(Item item)
        {
            if (!item.IsLore)
                return false;

            if (item.LoreGroup == -1)
                return GetItemByLocation((int)item.ItemID, ~InventoryLocations.SharedBank) != null;
            else
                return GetItemByLoreGroupByLocation((int)item.ItemID, ~InventoryLocations.SharedBank) != null;
        }

        internal int GetFreeSlot(byte size, bool isContainer)
        {
            InventoryItem invItem = null;

            for (int i = (int)InventorySlot.PersonalSlotsBegin; i <= (int)InventorySlot.PersonalSlotsEnd; i++) {
                invItem = this[i];

                if (invItem == null)
                    return i;
                else if (!isContainer && invItem.Item.IsContainer && invItem.Item.BagSize >= size) {
                    // Ok, nothing free in main 8 slots, find room in a bag for something other than a bag
                    int baseBagSlotId = InventoryManager.GetSlotIdWithinContainer(i, 0);
                    for (int ii = 0; ii < invItem.Item.slots; ii++) {
                        if (this[baseBagSlotId + ii] == null)
                            return baseBagSlotId + ii;  // Found an empty slot in a bag
                    }
                }
            }

            return (int)InventorySlot.Invalid;
        }

        #region Event Handlers
        protected void OnItemMoved(ItemMoveEventArgs e)
        {
            EventHandler<ItemMoveEventArgs> handler = ItemMoved;

            if (handler != null)
                handler(this, e);
        }

        protected void OnItemChargeUsed(ItemChargeUseEventArgs e)
        {
            EventHandler<ItemChargeUseEventArgs> handler = ItemChargeUsed;

            if (handler != null)
                handler(this, e);
        }
        #endregion
    }
}