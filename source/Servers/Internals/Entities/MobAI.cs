using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EQEmulator.Servers.Internals.Entities
{
    internal partial class Mob
    {
        internal virtual void StartAI()
        {
            _thinkTimer = new SimpleTimer(THINK_INTERVAL, true);
            _walkTimer = new SimpleTimer(0);
            _moveTimer = new SimpleTimer(MOVE_INTERVAL);
            _scanTimer = new SimpleTimer(SCAN_INTERVAL);
            _aiTargetCheckTimer = new SimpleTimer(TARGET_CHECK_INTERVAL);
            // TODO: feign remember timer

            _deltaX = _deltaY = _deltaZ = 0.0F;
            _deltaHeading = 0;
            _hateMgr.Clear();
            _runAnimSpeed = 0;
            this.IsAIControlled = true;
            _lastChange = DateTime.Now;
        }

        /// <summary>Handles various actions by a mob including combat, movement, etc.</summary>
        internal void ProcessAI()
        {
            if (!this.IsAIControlled || this.IsCasting)
                return;

            if (!(_thinkTimer.Check() || _attackTimer.Peek()))
                return;

            // TODO: fear pathing

            // TODO: trigger an event signal if needed?

            if (this.IsEngaged)
                ProcessAICombat();
            else {
                // TODO: check feign death remember timer?

                /* Un-engaged AI is the following: (1) check if mob should cast a spell whilst idling
                                                   (2) scan for shit to beat on
                                                   (3) attempt to move */

                if (CheckForIdleSpellCast())
                    return;     // We are casting a spell, so will do nothing else for now
                else if (_scanTimer.Check()) {
                    OnIdleScanning(new EventArgs());    // Look for something to beat on
                }

                if (_moveTimer.Check() && !IsRooted) {
                    this.RunAnimSpeed = 0;  // TODO: why are we doing this here?

                    // TODO: handle pet stuff here or in a pet class that overrides this routine

                    if (_followID != null) {
                        // TODO: implement following logic
                    }
                    else {
                        // TODO: wait a bit after fighting

                        ProcessAIMovement();
                    }
                }
            }
        }

        /// <summary>Down chain entities should override to provide specific handling of movement for their types.</summary>
        internal virtual void ProcessAIMovement()
        {
            throw new NotImplementedException();
        }

        /// <summary></summary>
        internal virtual void ProcessAICombat()
        {
            if (this.IsRooted)
                this.TargetMob = this.HateMgr.GetClosest();
            else if (_aiTargetCheckTimer.Check())
                this.TargetMob = _hateMgr.GetTopHated();

            if (this.TargetMob == null)
                return;     // No target, no reason to continue

            // This shouldn't happen but a minor race may occur if the mob manager hasn't removed the dead mob from our target yet
            if (this.TargetMob.Dead) {
                _hateMgr.RemoveHate(this.TargetMob);
                return;
            }

            // TODO: divine aura

            if (this.HPRatio < 15.0f) {
                // TODO: start enrage
            }

            if (IsWithinCombatRangeOf(this.TargetMob)) {
                if (_moveTimer.Check())
                    RunAnimSpeed = 0;

                if (this.IsMoving) {
                    this.IsMoving = false;
                    _moved = false;
                    this.Heading = CalculateHeadingToTarget(this.TargetMob.X, this.TargetMob.Y);
                    OnPositionUpdate(new PosUpdateEventArgs(true, false));  // No deltas - mobs warp
                    _tarIdx = 0;
                }

                if (IsAbleToAttack()) {
                    if (_attackTimer.Check()) { // Attack with main hand first
                        //_log.DebugFormat("{0}'s attack timer checked true.", this.Name);

                        if (this.IsAbleToAttack(this.TargetMob, false)) {   // TODO: if pet can't attack, should tell master
                            MeleeAttack(this.TargetMob, true, false);   // Actually attack something

                            // TODO: double, triple and quad attacks (no triple or quad for pets)

                            // TODO: various special attack checks (flurry, rampage, area rampage)

                            // TODO: if pet, check owner for various pet AA abilities
                        }
                        else
                            _log.DebugFormat("{0} is unable to attack {1}.", this.Name, this.TargetMob.Name);
                    }

                    if (_dwAttackTimer.Check()) {
                        // TODO: dual-wield attacks
                    }
                }
                else
                    _log.DebugFormat("{0} is unable to attack in general.", this.Name, this.TargetMob.Name);

                CheckForEngagedSpellCast();
            }
            else {
                // Can't reach target...
                //_log.DebugFormat("{0} can't reach {1} while engaged.", this.Name, this.TargetMob.Name);
                if (!_hateMgr.SummonMostHated()) {
                    // Can't summon, start pursuing // TODO: check for another mob in hate list with close hate value to use instead?
                    if (!CheckForPursuingSpellCast() && _moveTimer.Check()) {
                        // We didn't stop to cast a spell, process movement
                        if (!IsRooted) {
                            //_log.DebugFormat("{0} pursuing {1} while engaged.", this.Name, this.TargetMob.Name);
                            CalculateNewPosition(this.TargetMob.X, this.TargetMob.Y, this.TargetMob.Z, this.RunSpeed, true);    // TODO: use better pathing
                        }
                        else if (IsMoving) {
                            // Just got rooted
                            this.Heading = CalculateHeadingToTarget(this.TargetMob.X, this.TargetMob.Y);
                            this.RunAnimSpeed = 0;
                            OnPositionUpdate(new PosUpdateEventArgs(true, false));
                            this.IsMoving = false;
                            _moved = false;
                        }
                    }
                }
            }
        }

        /// <summary>Checks if mob wants to cast a spell while engaged in combat with another mob.</summary>
        /// <returns>True if the mob is going to cast a spell.</returns>
        protected bool CheckForEngagedSpellCast()
        {
            // TODO: implement once spells are in
            return false;
        }

        /// <summary>Checks if the mob wants to cast a spell while pursuing another mob.</summary>
        /// <returns>True if mob is going to cast a spell.</returns>
        protected bool CheckForPursuingSpellCast()
        {
            // TODO: implement once spells are in
            return false;
        }

        /// <summary>Checks if the mob wants to cast a spell while idling.</summary>
        /// <returns>True if mob is going to cast a spell.</returns>
        protected bool CheckForIdleSpellCast()
        {
            // TODO: implement once spells are in
            return false;
        }

        internal bool CheckAggro(Mob other)
        {
            return false;   // TODO: implement after faction is in
        }

        protected void OnIdleScanning(EventArgs e)
        {
            EventHandler<EventArgs> handler = IdleScanning;

            if (handler != null)
                handler(this, e);
        }
    }
}
