using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using EQEmulator.Servers.Internals.Data;
using EQEmulator.Servers.Internals.Packets;
using EQEmulator.Servers.Internals.Entities;

namespace EQEmulator.Servers.Internals
{
    /// <summary>This partial handles spells, buffs, special abilities, etc. that are considered magic.</summary>
    internal partial class ZonePlayer
    {
        private const uint SPELL_BAR_UNLOCK = 0x2bc;

        private Dictionary<uint, Spell> _memdSpells = new Dictionary<uint, Spell>(Character.MAX_MEMSPELL);  // keyed by mem'd slotId

        /// <summary>Gets a spell from internal spell storage by spellId.</summary>
        internal override Spell GetSpell(ushort spellId)
        {
            // First try memorized spells
            Spell s = _memdSpells.SingleOrDefault(kvp => kvp.Value.SpellID == spellId).Value;

            // Failing that, try the base implementation
            if (s == null)
                s = base.GetSpell(spellId);

            return s;
        }

        #region Packet Sends
        /// <summary>Sends client the a packet for spell memorization actions.</summary>
        private void SendMemorizeSpell(uint spellId, uint slotId, SpellMemorize sm)
        {
            MemorizeSpell ms = new MemorizeSpell() { SlotId = slotId, SpellId = spellId, Scribing = (uint)sm };
            EQApplicationPacket<MemorizeSpell> msPacket = new EQApplicationPacket<MemorizeSpell>(AppOpCode.MemorizeSpell, ms);
            this.Client.SendApplicationPacket(msPacket);
        }

        internal void SendSpellBarEnable(uint spellId)
        {
            ManaChange mc = new ManaChange()
            {
                NewMana = (uint)this.Mana,
                SpellId = spellId,
                Stamina = this.Endurance
            };

            EQApplicationPacket<ManaChange> mcPacket = new EQApplicationPacket<ManaChange>(AppOpCode.ManaChange, mc);
            this.Client.SendApplicationPacket(mcPacket);
        }

        private void SendSpellBarDisable()
        {
            SendMemorizeSpell(0u, SPELL_BAR_UNLOCK, SpellMemorize.Spellbar);
        }
        #endregion

        internal void ScribeSpell(uint spellId, uint slotId, bool updateClient)
        {
            if (slotId > Character.MAX_SPELLBOOK || slotId < 0) {
                _log.ErrorFormat("Tried to scribe spell {0} into spell book slot {1}", spellId, slotId);
                return;
            }

            // If we're sending packets, see if we need to un-scribe a spell before we scribe this one
            if (updateClient) {
                if (this.PlayerProfile.SpellBook[slotId] != Spell.BLANK_SPELL)
                    UnscribeSpell(slotId, updateClient);
            }

            this.PlayerProfile.SpellBook[slotId] = spellId; // scribe
            _log.DebugFormat("Scribed spell {0} at spell book slot {1}", spellId, slotId);

            if (updateClient)
                SendMemorizeSpell(spellId, slotId, SpellMemorize.Scribing); // Tell the client we scribed the spell
        }

        internal void UnscribeSpell(uint slotId, bool updateClient)
        {
            if (slotId > Character.MAX_SPELLBOOK || slotId < 0) {
                _log.ErrorFormat("Tried to unscribe a spell from spell book slot {0}", slotId);
                return;
            }

            this.PlayerProfile.SpellBook[slotId] = Spell.BLANK_SPELL; // unscribe

            if (updateClient) {
                DeleteSpell ds = new DeleteSpell() { SpellSlot = (short)slotId, Success = 1 };
                EQApplicationPacket<DeleteSpell> dsPacket = new EQApplicationPacket<DeleteSpell>(AppOpCode.DeleteSpell, ds);
                Client.SendApplicationPacket(dsPacket);
            }
        }

        /// <summary>Gets the slot Id in the spellbook of the specified spell Id</summary>
        /// <returns>-1 if not found, otherwise 0 - 400</returns>
        internal int GetSpellBookSlotBySpellId(uint spellId)
        {
            for (int i = 0; i < Character.MAX_SPELLBOOK; i++) {
                if (_pp.SpellBook[i] == spellId)
                    return i;
            }

            return -1;
        }

        /// <summary>Determines if we have a spell of the specified spellId scribed in our spellbook</summary>
        internal bool HasSpellScribed(uint spellId)
        {
            return GetSpellBookSlotBySpellId(spellId) != -1;
        }

        internal void MemorizeSpell(Spell spell, uint slotId, bool updateClient)
        {
            if (slotId > Character.MAX_MEMSPELL || slotId < 0) {
                _log.ErrorFormat("Tried to mem a spell into slot {0}", slotId);
                return;
            }

            // If we're sending packets, see if we need to forget a spell before we mem this one
            if (updateClient) {
                if (this.PlayerProfile.MemSpells[slotId] != Spell.BLANK_SPELL)
                    ForgetSpell(slotId, updateClient);
            }

            this.PlayerProfile.MemSpells[slotId] = spell.SpellID; // Memorize
            _memdSpells[slotId] = spell;
            _log.DebugFormat("Memorized spell {0} at slot {1}", spell.SpellName, slotId);

            if (updateClient)
                SendMemorizeSpell(spell.SpellID, slotId, SpellMemorize.Memorize); // Tell the client the spell was memorized
        }

        internal void ForgetSpell(uint slotId, bool updateClient)
        {
            if (slotId > Character.MAX_MEMSPELL || slotId < 0) {
                _log.ErrorFormat("Tried to forget a spell at slot {0}", slotId);
                return;
            }

            _log.DebugFormat("Forgot spell {0} at slot {1}", _memdSpells[slotId].SpellName, slotId);
            this.PlayerProfile.MemSpells[slotId] = Spell.BLANK_SPELL; // unmemorize
            _memdSpells[slotId] = null;

            if (updateClient)
                SendMemorizeSpell(Spell.BLANK_SPELL, slotId, SpellMemorize.Forget);   // Tell the client it was forgotten
        }

        internal override bool CastSpell(ushort spellId, uint slotId, uint targetId)
        {
            uint spellIdToCast = 0u;

            if (slotId == (int)SpellSlot.Ability && spellId == Spell.LAY_ON_HANDS && this.Class == (byte)CharClasses.Paladin) {
                // TODO: check LoH timer

                spellIdToCast = Spell.LAY_ON_HANDS;
                // TODO: start LoH timer
            }
            else if (slotId == (int)SpellSlot.Ability && (spellId == Spell.HARM_TOUCH || spellId == Spell.HARM_TOUCH2) && this.Class == (byte)CharClasses.ShadowKnight) {
                // TODO: check HT timer

                spellIdToCast = this.Level < 40 ? Spell.HARM_TOUCH : Spell.HARM_TOUCH2;
                // TODO: start HT timer
            }

            // TODO: check discs and make sure they aren't coming through here (should be handled elsewhere)

            if (slotId < Character.MAX_MEMSPELL) {
                spellIdToCast = this.PlayerProfile.MemSpells[slotId]; // Get the spellId from the profiles's mem'd spells

                if (spellIdToCast != spellId) {
                    InterruptSpell(spellId);    // Cheating bastards TODO: log?
                    return false;
                }
            }

            Spell spell = GetSpell((ushort)spellIdToCast);    // Get the spell object
            if (spell == null) {
                _log.ErrorFormat("Attempt by {0} to fetch spell {1} got a null value.", this.Name, spellIdToCast);
                return false;
            }

            // Check for fizzle (on mem'd spells - not clickies or abilities)
            if (slotId < Character.MAX_MEMSPELL && CheckFizzle(spell)) {
                MessageStrings fizzleMsg = spell.IsBardSong ? MessageStrings.MISS_NOTE : MessageStrings.SPELL_FIZZLE;
                InterruptSpell(spellIdToCast, fizzleMsg);

                // Fizzle 1/4 of the mana away
                int usedMana = spell.Mana / 4;
                this.Mana -= usedMana;
                _log.DebugFormat("{0} fizzled away {1} mana.", this.Name, usedMana);
                return false;
            }

            // TODO: Check for and stop a song for bards

            return base.CastSpell((ushort)spellIdToCast, slotId, targetId);
        }

        // TODO: never finished this...
        //internal override bool FinishSpell(Spell spell, uint slotId, uint targetId, uint? invSlotId)
        //{
        //    if (slotId != (uint)SpellSlot.UseItem && slotId != (uint)SpellSlot.PotionBelt && spell.RecastTime > 1000) {
        //        // TODO: check spell recast persistent timer
        //    }
        //    else if (slotId == (uint)SpellSlot.UseItem || slotId == (uint)SpellSlot.PotionBelt) {
        //        InventoryItem ii = this.InvMgr[(int)invSlotId];
        //        if (ii != null && ii.Item.recastdelay > 0) {
        //            // TODO: check item recast type persistent timer
        //        }
        //    }

        //    // Prevent rapid recast - sometimes happens if spell gems get out of sync and the player casts again
        //    if (_delayTimer) {
        //        _log.InfoFormat("{0}'s casting of {1} canceled: recast to quickly.", this.Name, spell.SpellName);
        //        this.MsgMgr.SendSpecialMessage(MessageType.Common13, "You are unable to focus.");
        //        InterruptSpell(null, null);
        //        return false;
        //    }

        //    if(!base.FinishSpell(spell, slotId, targetId, invSlotId))
        //        return false;   // Base implementation decided that we moved too far, were hit too much or whatever - failed to finish the spell

        //    // Spell has now been completed

        //    // TODO: check for consumable component usages and reagent focus items

        //    if (invSlotId != null) {
        //        // TODO: check if item is still in inventory and deduct charge or delete if necessary
        //    }

        //    // TODO: try a sympathetic proc
        //}

        internal override bool TrySpellCasting(ushort spellId)
        {
            if (this.IsAIControlled) {
                MsgMgr.SendMessageID((uint)MessageType.Common13, MessageStrings.NOT_IN_CONTROL);
                return false;   // Don't bother with further checks
            }

            bool retVal = base.TrySpellCasting(spellId);

            if (!retVal) {  // If the spell isn't going to be cast, send some pertinent packets back to client
                if (_silenced)
                    MsgMgr.SendMessageID((uint)MessageType.Common13, MessageStrings.SILENCED_CANT_CAST);

                SendSpellBarEnable(spellId);
            }

            return retVal;
        }

        /// <summary>Checks if the mob fizzles the spell.</summary>
        /// <returns>True if the spell cast fizzled.</returns>
        internal bool CheckFizzle(Spell spell)
        {
            if (this.IsGM)
                return false;   // GM's don't fizzle

            int noFizLvl = 0;

            // TODO: Check AA skills, item mods, etc. that reduce fizzle chances

            int reqLvl = spell.GetReqLevelForClass((CharClasses)this.Class);
            if (reqLvl <= noFizLvl)
                return false;

            // Par (as in golf) skill level
            int parSkill = reqLvl * 5 - 10; // Some lag behind in skill levels isn't immediately harsh on fizzles
            parSkill = Math.Min(parSkill, 235);
            parSkill += reqLvl;     // Should be max of 270 for level 65 spell

            // Actual skill level
            int actSkill = (int)GetSkillLevel((Skill)spell.Skill);
            actSkill += this.Level;

            float specialize = 0.0f;    // TODO: spell specialization & spell casting mastery AA (reduces fizzle chance)

            float diff = parSkill + spell.BaseFizzle - actSkill;    // 0 is on par, > 0 higher chance of fizzle, < 0 lower

            // Adjust for prime class skill levels - higher means less fizzles
            if ((CharClasses)this.Class == CharClasses.Bard)
                diff -= (this.CHA - 110) / 20.0f;
            else if (GetCasterClass() == CasterClass.Wis)
                diff -= (this.WIS - 125) / 20.0f;
            else if (GetCasterClass() == CasterClass.Int)
                diff -= (this.INT - 125) / 20.0f;

            float baseFizChance = 10.0f;
            float fizChance = baseFizChance - specialize + diff / 5.0f;
            fizChance = Math.Max(Math.Min(fizChance, 95.0f), 1.0f); // Always at least a 1% chance to fail and 5% to succeed

            // TODO: adjust bard song for Internal Metronome AA

            Random rand = new Random();
            double fizRoll = rand.NextDouble() * 100;
            _log.DebugFormat("Fizzle check: spell: {0}, fizzle chance: {1}, diff: {2}, roll: {3}", spell.SpellName, fizChance, diff, fizRoll);

            return fizRoll > fizChance ? false : true;
        }

        internal override void InterruptSpell(uint? spellId, MessageStrings? msg)
        {
            base.InterruptSpell(spellId, msg);
        }

        internal override bool UseItemEffect(uint invSlotId)
        {
            // TODO: add code to prevent MQ2 exploits?

            // TODO: get spell effect from item

            return base.UseItemEffect(invSlotId);
        }

        internal override short GetActualManaCost(Spell spell)
        {
            // TODO: implement player specific checks for mana cost reducers

            // TODO: if using MGB and spell supports MGB, mana is x2
            return spell.Mana;
        }
    }
}
