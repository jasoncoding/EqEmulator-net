using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using EQEmulator.Servers.ExtensionMethods;
using EQEmulator.Servers.Internals.Packets;
using EQEmulator.Servers.Internals.Data;

namespace EQEmulator.Servers.Internals.Entities
{
    internal class Corpse : Mob
    {
        internal const int DECAYMS_EMPTY_NPC_CORPSE = 0;
        internal const int DECAYMS_NPC_CORPSE = 1200000;
        internal const int DECAYMS_BOSS_NPC_CORPSE = 3600000;
        internal const int MAX_LOOTERS = 72;    // Now that's a lot of looters... raid max?

        private int _looterId = 0;  // CharID of the active looter
        private object _looterIdSyncLock = new object(); // Clients could try to loot at the same time (frequently do, actually)
        protected SimpleTimer _decayTimer;
        protected List<InventoryItem> _lootItems = new List<InventoryItem>(10);
        protected List<int> _allowedLooters = new List<int>(6);   // CharIDs... not entity IDs
        protected bool _changed = false;

        /// <summary>For use with NPC mobs.</summary>
        /// <param name="mob">Mob that died.</param>
        /// <param name="decayTime">Miliseconds until corpse decay.</param>
        /// <param name="lootItems">Items which will be part of the corpse's loot.</param>
        internal Corpse(Mob mob, List<InventoryItem> lootItems)
            : base(mob.ID, mob.Name.RemoveDigits() + "'s corpse" + mob.ID.ToString(), "", mob.X, mob.Y, mob.Z, mob.Heading)
        {
            if (lootItems != null)
                _lootItems.AddRange(lootItems);
            this.Platinum = mob.Platinum;
            this.Gold = mob.Gold;
            this.Silver = mob.Silver;
            this.Copper = mob.Copper;
            
            _changed = false;

            if (IsEmpty())
                _decayTimer = new SimpleTimer(DECAYMS_EMPTY_NPC_CORPSE);
            else
                _decayTimer = new SimpleTimer(mob.Level > 54 ? DECAYMS_BOSS_NPC_CORPSE : DECAYMS_NPC_CORPSE);

            _gender = mob.Gender;
            _race = mob.Race;
            _bodyType = mob.BodyType;
            _size = mob.Size;
            _texture = mob.Texture;
            _helmTexture = mob.HelmTexture;
        }

        #region Properties
        internal override bool Dead
        {
            get { return true; }    // Duh
        }

        internal override uint Platinum
        {
            set
            {
                if (base.Platinum != value) {
                    base.Platinum = value;
                    _changed = true;
                }
            }
        }

        internal override uint Gold
        {
            set
            {
                if (base.Gold != value) {
                    base.Gold = value;
                    _changed = true;
                }
            }
        }

        internal override uint Silver
        {
            set
            {
                if (base.Silver != value) {
                    base.Silver = value;
                    _changed = true;
                }
            }
        }

        internal override uint Copper
        {
            set
            {
                if (base.Copper != value) {
                    base.Copper = value;
                    _changed = true;
                }
            }
        }

        internal TimeSpan DecayTime
        {
            get { return _decayTimer.GetRemainingTime(); }
        }
        #endregion

        protected internal override void Init()
        {
        }

        internal override void DePop()
        {
            _decayTimer.Enabled = false;
            base.DePop();
        }

        internal override bool Process()
        {
            if (IsDePopped)
                return false;   // Remove from the corpse list please

            // TODO: do graveyard checks

            if (_decayTimer.Check()) {

                // TODO: bury corpse in shadowrest?

                DePop();
            }

            return true;
        }

        internal override EQEmulator.Servers.Internals.Packets.Spawn GetSpawn()
        {
            Packets.Spawn s = base.GetSpawn();
            s.MaxHpCategory = 120;
            s.NPC = 2;
            return s;
        }

        protected override int GetEquipmentMaterial(EquipableType et)
        {
            InventoryItem invItem = _lootItems.FirstOrDefault(li => (li != null && li.SlotID == (int)InventoryManager.GetEquipableSlot(et)));
            //_log.DebugFormat("{0} is getting the material for looted item at {1} ({2})", this.Name, et, invItem);

            if (invItem == null) {
                // Nothing equipped in that slot, so use the predefined texture
                switch (et) {
                    case EquipableType.Head:
                        return HelmTexture;
                    case EquipableType.Chest:
                        return Texture;
                    default:
                        return 0;   // npc has nothing in the slot and it's not a slot that has a predefined texture
                }
            }

            // Npc has something in the slot, let's get it
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
            InventorySlot slot = InventoryManager.GetEquipableSlot(et);
            InventoryItem invItem = _lootItems.FirstOrDefault(li => (li != null && li.SlotID == (int)slot));
            //_log.DebugFormat("{0} is getting the color for looted item at {1} ({2})", this.Name, et, invItem);

            if (invItem != null)
                return invItem.Color;

            //_log.WarnFormat("Unable to locate an item at slot {0} while asking for the color.", slot);
            return 0;
        }

        protected bool IsEmpty()
        {
            return (this.Copper == 0 && this.Silver == 0 && this.Gold == 0 && this.Platinum == 0 && _lootItems.Count == 0);
        }

        internal void AllowLooter(ZonePlayer zp)
        {
            if (zp == null)
                throw new ArgumentNullException("zp");

            if (_allowedLooters.Count > MAX_LOOTERS)
                return;

            _allowedLooters.Add(zp.CharId);
        }

        /// <summary>Loots this corpse of money and, optionally, items.</summary>
        /// <param name="looterId">CharID of the looting character.</param>
        /// <returns>1 if the looting is successful, 0 if someone else is looting, 2 if not at this time.</returns>
        /// <remarks>"Not at this time" is given when it isn't your corpse to loot, not the looter in a raid, etc.</remarks>
        internal byte Loot(ZonePlayer looter, out uint plat, out uint gold, out uint silver, out uint copper, out List<InventoryItem> items)
        {
            plat = gold = silver = copper = 0u;
            items = _lootItems;

            if (this.IsDePopped)
                return 0;

            // Is it an allowed looter?
            if (_allowedLooters.Contains(looter.ID) || looter.IsGM) {
                lock (_looterIdSyncLock) {
                    if (_looterId == 0)
                        _looterId = looter.ID;
                    else
                        return 0;   // Someone else is looting it right now
                }

                plat = this.Platinum;
                gold = this.Gold;
                silver = this.Silver;
                copper = this.Copper;
                ClearCash();

                return 1;
            }
            else
                return 2;
        }

        /// <summary>Doesn't remove the item.  Call removeItem() to remove the item.</summary>
        internal InventoryItem LootItem(ZonePlayer zp, int itemIdx)
        {
            // Already in looting mode, so shouldn't need to run further access checks for this corpse... right?

            int actualItemIdx = itemIdx - 22; // decrement by 22 because we incremented by 22 when sending corpse's item list

            if (actualItemIdx >= _lootItems.Count) {
                _log.ErrorFormat("Tried to loot item with idx {0} but corpse only has {1} lootable items.", actualItemIdx, _lootItems.Count);
                return null;
            }

            InventoryItem invItem = _lootItems[actualItemIdx];  // Get the item
            if (invItem == null) {
                _log.ErrorFormat("Attempt to loot item with idx {0} produces a null value (already looted?). Corpse has {1} lootable items.", actualItemIdx, _lootItems.Count);
                return null;
            }

            if (zp.InvMgr.CheckLoreConflict(invItem.Item)) {
                zp.MsgMgr.SendMessageID(0, MessageStrings.LOOT_LORE_ERROR);
                _looterId = 0;
                return null;
            }

            _lootItems[actualItemIdx] = null;   // Remove the item from the corpse's lootable items

            // Send a wear change if the looted item was equipped
            if (invItem.SlotID != 0) {
                // It was equipped by the npc (non-zero slotID is an equipped item)
                EquipableType et = InventoryManager.GetEquipableType((InventorySlot)invItem.SlotID);
                _log.DebugFormat("{0} is looting equipped item {1} from {2} at slot {3} (equipable type {4})", zp.Name, invItem, this.Name,
                    invItem.SlotID, et);
                if (et != EquipableType.Unequipable)
                    TriggerWearChange(et);
            }

            return invItem;
        }

        //internal void RemoveItem(int itemIdx)
        //{
        //    int actualItemIdx = itemIdx - 22; // decrement by 22 because we incremented by 22 when sending corpse's item list

        //    if (actualItemIdx >= _lootItems.Count) {
        //        _log.ErrorFormat("Tried to remove item with idx {0} but corpse only has {1} lootable items.", actualItemIdx, _lootItems.Count);
        //        return;
        //    }

        //    InventoryItem invItem = _lootItems[actualItemIdx];  // Get the item

        //    if (invItem == null) {
        //        _log.ErrorFormat("Attempt to remove item with idx {0} produces a null value (already looted?). Corpse has {1} lootable items.", actualItemIdx, _lootItems.Count);
        //        return;
        //    }

        //    _lootItems[actualItemIdx] = null;   // Remove the item from the corpse's lootable items

        //    // Send a wear change if the looted item was equipped
        //    if (invItem.SlotID != 0) {
        //        // It was equipped by the npc (non-zero slotID is an equipped item)
        //        _log.DebugFormat("{0} has had equipped item {1} looted.", this.Name, invItem);
        //        EquipableType et = InventoryManager.GetEquipableType((InventorySlot)invItem.SlotID);
        //        if (et != EquipableType.Unequipable)
        //            TriggerWearChange(et);
        //    }
        //}

        /// <summary>Called when a looter is finished looting this corpse.</summary>
        /// <param name="looterId">CharId of the looter.</param>
        internal void StopLooting()
        {
            _lootItems.RemoveAll(li => li == null);     // Remove any looted items

            _looterId = 0;
            if (IsEmpty())
                DePop();
            else
                Save();
        }

        internal void ClearCash()
        {
            this.Platinum = 0;
            this.Gold = 0;
            this.Silver = 0;
            this.Copper = 0;
        }
    }
}
