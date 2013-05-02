using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using EQEmulator.Servers.Internals.Data;

namespace EQEmulator.Servers.Internals.Entities
{
    #region Event Data Structures
    internal class TryCastEventArgs : EventArgs
    {
        internal ushort SpellId { get; set; }
        internal bool Allowed { get; set; }
    }

    internal class InterruptCastEventArgs : EventArgs
    {
        internal uint SpellId { get; set; }
        internal MessageStrings Message { get; set; }
    }

    internal class BeginCastEventArgs : EventArgs
    {
        internal uint SpellId { get; set; }
        internal uint CastTime { get; set; }
    }
    #endregion

    /// <summary>Contains spell focused logic.  Includes spells, effects, abilities, buffs, etc.</summary>
    internal partial class Mob
    {
        protected bool _delayTimer = false;     // Rapid recast prevention indicator
        protected SimpleTimer _spellEndTimer;
        protected ushort _castingSpellSlotId = 0, _bardSong;
        protected Spell _castingSpell = null;
        protected int _hitWhileCastCount = 0, _castingSpellTimer = 0, _castingSpellTimerDuration = 0;
        protected uint _castingSpellInvSlotId = 0u;
        protected float _spellX = 0.0f, _spellY = 0.0f, _spellZ = 0.0f;

        internal event EventHandler<TryCastEventArgs> TryingToCast;
        internal event EventHandler<InterruptCastEventArgs> SpellCastInterrupted;
        internal event EventHandler<BeginCastEventArgs> SpellCastBegun;

        #region Properties
        internal bool IsCasting
        {
            get { return _castingSpell != null; }
        }
        #endregion

        /// <summary>Gets a spell from World by spell ID.  This base implementation should only be called when
        /// overriders have failed to load a spell internally.</summary>
        internal virtual Spell GetSpell(ushort spellId)
        {
            _log.InfoFormat("Getting spell {0} from world for mob {1}.", spellId, this.Name);
            return ZoneServer.WorldSvc.GetSpellById(spellId);   // TODO: optimize to cache a certain number of these local to a zone?
        }

        internal virtual void InterruptSpell(uint? spellId)
        {
            InterruptSpell(spellId ?? _castingSpell.SpellID, null);   // if null spellId, use the currently casting spellId
        }

        internal virtual void InterruptSpell(uint? spellId, MessageStrings? msg)
        {
            uint intSpellId = spellId ?? _castingSpell.SpellID;

            ResetCastingVars();
            Spell s = GetSpell((ushort)intSpellId);
            _log.DebugFormat("{0} has had spell {1} interrupted.", this.Name, s == null ? "None" : s.SpellName);

            if (intSpellId == 0)    // If there is no spell to interrupt, begone
                return;

            // TODO: stop bard songs

            // Get the correct message if one hasn't been specified
            if (msg == null)
                msg = s.IsBardSong ? MessageStrings.SONG_ENDS_ABRUPTLY : MessageStrings.INTERRUPT_SPELL;

            // Signal the mob manager that a spell was interrupted
            OnSpellCastInterrupted(new InterruptCastEventArgs() { SpellId = intSpellId, Message = msg.Value });
        }

        /// <summary>Resets casting state variables</summary>
        private void ResetCastingVars()
        {
            _hitWhileCastCount = 0;
            _spellEndTimer.Stop();
            _delayTimer = false;
            _castingSpell = null;
            _spellTarget = null;
            _castingSpellSlotId = 0;
            _castingSpellInvSlotId = 0;
            _castingSpellTimer = 0;
            _castingSpellTimerDuration = 0;
        }

        /// <summary>Stores location values for use in determining spell interruption via movement</summary>
        protected void SaveSpellCastLoc()
        {
            _spellX = this.X;
            _spellY = this.Y;
            _spellZ = this.Z;
        }

        /// <summary>For when the mob casts spells & uses abilities.  Clickies are handled in UseItemEffect().</summary>
        internal virtual bool CastSpell(ushort spellId, uint slotId, uint targetId)
        {
            _log.DebugFormat("{0} casting spell {1} on target {2}, spell slot {3}", this.Name, spellId, targetId, slotId);

            if (_castingSpell.SpellID == spellId)
                ResetCastingVars();

            Spell spell = GetSpell(spellId);    // Get the spell object
            if (spell == null) {
                _log.ErrorFormat("Attempt by {0} to fetch mem'd spell {1} got a null value.", this.Name, spellId);
                return false;
            }

            return BeginSpell(spell, slotId, targetId);
        }

        /// <summary>Called by CastSpell() & UseItemEffect().</summary>
        /// <returns>True if spell was successfully cast.</returns>
        /// <param name="slotId">Slot ID of the spell slot that is being cast: see SpellSlot enum.</param>
        /// <param name="targetId">Zero for no target</param>
        internal virtual bool BeginSpell(Spell spell, uint slotId, uint targetId)
        {
            uint origCastTime;
            _castingSpellSlotId = (ushort)slotId;

            // TODO: provide a parameter for a timer, timer duration and type?

            SaveSpellCastLoc();

            // If the spell doesn't require a target or it's a target optional spell and a target doesn't exist, then it's us;
            // unless TGB is on and the spell is TGB compatible
            if ((spell.IsGroupSpell || 
                spell.TargetType == (short)SpellTargetType.Self || 
                spell.TargetType == (short)SpellTargetType.AECaster ||
                spell.TargetType == (short)SpellTargetType.TargetOptional) && targetId == 0u)
            {
                _log.DebugFormat("Spell {0} auto-targeted the caster.  Group: {1}, Target Type: {2}", spell.SpellName, spell.IsGroupSpell, spell.TargetType);
                this.TargetMob = this;
            }

            origCastTime = spell.CastTime;
            if (spell.CastTime > 0)
                spell.CastTime = GetActualSpellCastTime(spell);     // Check for modified casting times

            // If we don't have a target, it's an issue
            if (this.TargetMob == null) {
                _log.DebugFormat("{0} cast spell {1} without a target - Canceling", this.Name, spell.SpellName);
                if (this is ZonePlayer) {
                    ((ZonePlayer)this).MsgMgr.SendMessageID((uint)MessageType.Common13, MessageStrings.SPELL_NEED_TAR);
                    InterruptSpell(null);
                }
                else
                    InterruptSpell(null);

                return false;
            }

            if (spell.Mana > 0)
                spell.Mana = GetActualManaCost(spell);  // Check for modified mana costs

            // Now see if we have enough mana to cast the spell
            if (spell.Mana > 0 && slotId != (uint)SpellSlot.UseItem) {
                if (this.Mana < spell.Mana) {

                    // Let NPCs with no mana cast the spell / use the ability
                    if (this is NpcMob && this.Mana == this.MaxMana)
                        spell.Mana = 0;
                    else {
                        _log.DebugFormat("{0} has insufficient mana for spell {1}: cur mana: {2}, mana cost: {3}", this.Name, spell.SpellName, this.Mana, spell.Mana);
                        if (this is ZonePlayer) {
                            ((ZonePlayer)this).MsgMgr.SendMessageID((uint)MessageType.Common13, MessageStrings.InsufficientMana);
                            InterruptSpell(null);
                        }
                        else
                            InterruptSpell(null);

                        return false;
                    }
                }
            }

            this.SpellTargetMob = this.TargetMob;   // Store the spell target and...
            _castingSpell = spell;                  // ...the spell for when the spell completes

            // If cast time is 0 finish it now
            if (spell.CastTime == 0) {
                FinishSpell(spell, slotId, null);
                return true;
            }

            _spellEndTimer.Start((int)spell.CastTime);   // Spell has a cast time so start the cast timer

            // If we're under AI, face our target (unless it's ourself)
            if (this.IsAIControlled) {
                this.RunAnimSpeed = 0;
                if (this != this.TargetMob)
                    FaceMob(this.TargetMob);
            }

            // Use original cast time, as the client calcs any reduced cast times on its own
            OnSpellCastBegun(new BeginCastEventArgs() { SpellId = spell.SpellID, CastTime = origCastTime });
            return true;
        }

        /// <summary>Invoked by Mob's Process()</summary>
        internal virtual void ProcessSpells()
        {
            // Check the rapid recast prevention timer
            if (_delayTimer && _spellEndTimer.Check()) {
                _spellEndTimer.Stop();
                _delayTimer = false;
                return;
            }

            // Check if a timed spell has finished casting
            if (_castingSpell != null && _spellEndTimer.Check()) {
                _spellEndTimer.Stop();
                _delayTimer = false;
                FinishSpell(_castingSpell, _castingSpellSlotId, _castingSpellInvSlotId);
            }
        }

        /// <summary>Called when a spell finishes its timer - from both normal spells as well as items.</summary>
        /// <param name="invSlotId">Null when not a spell cast from an item.</param>
        /// <remarks>Does not check if it is valid to cast the spell, this should already be done by this point.  Also any specialized
        /// checks should be done before calling this base implementation as it tries to apply the spell.</remarks>
        internal virtual bool FinishSpell(Spell spell, uint slotId, uint? invSlotId)
        {
            // Make sure we're only casting one timed spell at a time
            if (_castingSpell.SpellID != spell.SpellID) {
                _log.InfoFormat("{0}'s casting of {1} canceled: already casting {2}.", this.Name, spell.SpellName, _castingSpell.SpellName);
                if (this is ZonePlayer)
                    ((ZonePlayer)this).MsgMgr.SendMessageID((uint)MessageType.Common13, MessageStrings.ALREADY_CASTING);
                InterruptSpell(null, null); // TODO: does it matter which spell is being interrupted here?
                return false;
            }

            // Check for anything that might have interrupted the spell (moving, etc.)
            bool bardSongMode = false, regainConcen = false;
            if (this.Class == (byte)CharClasses.Bard) {     // Bards can move when casting any spell
                if (spell.IsBardSong) {
                    // TODO: apply some logic to bard songs
                }
            }
            else {  // Not bard, check movement
                if (_hitWhileCastCount > 0 || this.X != _spellX || this.Y != _spellY || this.Z != _spellZ) {
                    // We moved or were hit, check for regain concentration
                    _hitWhileCastCount = Math.Min(_hitWhileCastCount, 15);
                    float channelChance, distMoved, dX, dY, distanceMod;
                    
                    // TODO: check for interrupts
                }
            }

            // TODO: try twin cast

            return ExecuteSpell(spell, this.SpellTargetMob, slotId, invSlotId);
        }

        /// <summary>Actually executes a spell and its effects on a specified target.</summary>
        /// <param name="spell"></param>
        /// <param name="target"></param>
        /// <param name="slotId"></param>
        /// <param name="invSlotId">Optional slot a used item is in that caused the spell.</param>
        /// <returns></returns>
        internal virtual bool ExecuteSpell(Spell spell, Mob target, uint slotId, uint? invSlotId)
        {
            CastActionType cat;
            Mob aeCenter = null;
            if (!DetermineSpellTargets(spell, target, ref aeCenter, out cat))
                return false;

            return true;    // TODO: unfinished
        }

        /// <summary>Based upon several factors, the spell being cast can have different targets.  This method determines those targets.</summary>
        protected bool DetermineSpellTargets(Spell spell, Mob target, ref Mob aeCenter, out CastActionType cat)
        {
            cat = CastActionType.AECaster;
            return false;   // TODO: unfinished
        }

        /// <summary>Checks that the mob is not in a state that disallows spell casting (silenced, feared, etc.)</summary>
        internal virtual bool TrySpellCasting(ushort spellId)
        {
            if (_castingSpell != null || _delayTimer || _spellEndTimer.Enabled || _stunned || _feared || _mezzed || _silenced) {
                _log.WarnFormat("Spell casting canceled: unable to cast due to Casting:{0}, Waiting: {1} Recast Timer Enabled:{2}, Stunned:{3}, Feared:{4}, Mez'd:{5}, Silenced:{6}",
                    _castingSpell.SpellID, _delayTimer, _spellEndTimer.Enabled, _stunned, _feared, _mezzed, _silenced);
                return false;
            }

            // TODO: check for divine aura (can't cast if using)

            // Raise event so that zone can run its checks (no-combat, blocked spells, etc.)
            TryCastEventArgs tce = new TryCastEventArgs()
            {
                SpellId = spellId,
                Allowed = true
            };
            OnTryingToCast(tce);

            return tce.Allowed;
        }

        internal virtual bool UseItemEffect(uint invSlotId)
        {
            // TODO: get spell effect from item and then invoke BeginSpell()

            _castingSpellInvSlotId = invSlotId;

            return true;
        }

        internal virtual uint GetActualSpellCastTime(Spell spell)
        {
            // TODO: implement checks for casting time reducers
            return spell.CastTime;
        }

        internal virtual short GetActualManaCost(Spell spell)
        {
            // TODO: implement checks for mana cost reducers
            return spell.Mana;
        }

        #region Event Handlers
        protected void OnTryingToCast(TryCastEventArgs e)
        {
            EventHandler<TryCastEventArgs> handler = TryingToCast;

            if (handler != null)
                handler(this, e);
        }

        protected void OnSpellCastInterrupted(InterruptCastEventArgs ice)
        {
            EventHandler<InterruptCastEventArgs> handler = SpellCastInterrupted;

            if (handler != null)
                handler(this, ice);
        }

        protected void OnSpellCastBegun(BeginCastEventArgs bce)
        {
            EventHandler<BeginCastEventArgs> handler = SpellCastBegun;

            if (handler != null)
                handler(this, bce);
        }
        #endregion
    }
}
