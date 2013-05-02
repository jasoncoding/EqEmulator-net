using System;
using System.Collections.Generic;
using System.Threading;

using EQEmulator.Servers.Internals.Entities;
using EQEmulator.Servers.Internals.Packets;
using log4net;
using EQEmulator.Servers.Internals.Data;

namespace EQEmulator.Servers.Internals
{
    /// <summary>Holds references to and manages ALL of the zone's mobs.  This includes clients, npcs, etc.</summary>
    /// <remarks>
    /// Mobs raise, and thus the MobManager handles, these events:
    ///     - stance changed
    ///     - position updated (with & without deltas)
    ///     - wear changed
    ///     - hp updates
    ///     - mana updates
    /// 
    /// To remove a mob from the manager, you DePop() it.
    /// </remarks>
    internal class MobManager : IDisposable
    {
        protected static readonly ILog _log = LogManager.GetLogger(typeof(MobManager));
        private const int LOCK_TIMEOUT = 5000;
        private ZoneServer _zoneSvr = null;
        private Dictionary<int, Mob> _mobs = null;  // Keyed by entity ID - holds the zone's clients & npcs
        private ReaderWriterLockSlim _mobsListLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private Dictionary<int, Corpse> _corpses = null;    // Keyed by entity ID
        private ReaderWriterLockSlim _corpseListLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private SimpleTimer _corpseTimer = new SimpleTimer(2000);
        List<int> _mobsToRemove = null; // keys to remove
        private List<int> _corpsesToRemove = null;  // keys to remove
        
        // Events raised by various actions of the managed mobs. Subscribed to by the Zone Server

        internal MobManager (ZoneServer zoneSvr)
	    {
            _zoneSvr = zoneSvr;
            Init();
	    }

        internal int MobCount
        {
            get { return _mobs.Count; }
        }

        internal int CorpseCount
        {
            get { return _corpses.Count; }
        }

        internal ReaderWriterLockSlim MobListLock
        {
            get { return _mobsListLock; }
        }

        #region Iterators
        /// <summary>Not thread safe... obtain the list lock before using.</summary>
        internal IEnumerable<Mob> AllMobs
        {
            get
            {
                foreach (KeyValuePair<int, Mob> kvp in _mobs)
                    yield return kvp.Value;
            }
        }

        /// <summary>Not thread safe... obtain the list lock before using.</summary>
        internal IEnumerable<ZonePlayer> Clients
        {
            get
            {
                foreach (KeyValuePair<int, Mob> kvp in _mobs)
                    if (kvp.Value is ZonePlayer)
                        yield return kvp.Value as ZonePlayer;
            }
        }

        /// <summary>Not thread safe... obtain the list lock before using.</summary>
        internal IEnumerable<Corpse> Corpses
        {
            get
            {
                foreach (KeyValuePair<int, Corpse> kvp in _corpses)
                    yield return kvp.Value;
            }
        }
        #endregion

        internal void Init()
        {
            _mobs = new Dictionary<int, Mob>(400);
            _corpses = new Dictionary<int, Corpse>(50);
            _corpsesToRemove = new List<int>(20);
            _mobsToRemove = new List<int>(20);
            _corpseTimer.Enabled = false;
        }

        public void Process()
        {
            ProcessMobs();

            ProcessCorpses();
        }

        private void ProcessMobs()
        {
            if (_mobsListLock.TryEnterUpgradeableReadLock(LOCK_TIMEOUT)) {
                try {
                    foreach (Mob m in this.AllMobs) {   // Process all mobs (including players)
                        if (!m.Process()) {
                            _mobsToRemove.Add(m.ID);
                            RemoveFromAllTargets(m);
                        }
                    }

                    if (_mobsToRemove.Count > 0) {
                        if (_mobsListLock.TryEnterWriteLock(LOCK_TIMEOUT)) {
                            try {
                                foreach (int key in _mobsToRemove) {
                                    _log.Debug("Removing mob " + key);
                                    _mobs.Remove(key);
                                }
                            }
                            catch (Exception ex) {
                                _log.Error("Error removing a mob... ", ex);
                            }
                            finally {
                                _mobsListLock.ExitWriteLock();
                            }

                            _mobsToRemove.Clear();
                        }
                        else
                            _log.Error("Unable to establish a write lock on mobs list.");
                    }
                }
                finally {
                    _mobsListLock.ExitUpgradeableReadLock();
                }
            }
            else
                _log.Error("Unable to establish an upgradeable read lock on mobs list.");
        }

        private void ProcessCorpses()
        {
            if (!_corpseTimer.Check())
                return;

            _corpseListLock.EnterUpgradeableReadLock();
            try {
                foreach (Corpse c in this.Corpses) {
                    if (!c.Process()) {
                        _corpsesToRemove.Add(c.ID);
                        RemoveFromAllTargets(c);
                        _zoneSvr.SendDespawnPacket(c);  // Tell everyone we've left TODO: could queue on a background thread if necessary
                    }
                }

                if (_corpsesToRemove.Count > 0) {
                    _corpseListLock.EnterWriteLock();
                    try {
                        foreach (int key in _corpsesToRemove) {
                            _log.Debug("Removing corpse " + key);
                            _corpses.Remove(key);
                        }

                        _corpsesToRemove.Clear();
                    }
                    catch (Exception ex) {
                        _log.Error("Error removing corpses... ", ex);
                    }
                    finally {
                        _corpseListLock.ExitWriteLock();
                    }
                }

                _corpseTimer.Enabled = _corpses.Count > 0;
            }
            finally {
                _corpseListLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>Will not return depopped mobs.</summary>
        public Mob this[int entityID]
        {
            get
            {
                Mob mob = null;

                if (_mobs.TryGetValue(entityID, out mob) && !mob.IsDePopped)
                    return _mobs[entityID];
                else if (_corpses.ContainsKey(entityID))
                    return _corpses[entityID];
                else
                    return null;
            }
        }

        internal Corpse GetCorpse(int corpseId)
        {
            Corpse c;
            _corpses.TryGetValue(corpseId, out c);
            return c;
        }

        /// <summary>Thread safe.</summary>
        internal void AddMob(Mob mob)
        {
            // Subscribe to general mob events
            mob.StanceChanged += Mob_StanceChanged;
            mob.PositionUpdated += Mob_PositionUpdate;
            mob.PlayAnimation += Mob_PlayAnimation;
            mob.Damaged += new EventHandler<DamageEventArgs>(Mob_Damaged);
            mob.WearChanged += new EventHandler<WearChangeEventArgs>(Mob_WearChanged);
            mob.IdleScanning += new EventHandler<EventArgs>(Mob_IdleScanning);
            mob.HPUpdated +=new EventHandler<EventArgs>(Mob_HpUpdated);
            mob.ChannelMessage += new EventHandler<ChannelMessageEventArgs>(Mob_ChannelMessage);
            mob.TryingToCast += new EventHandler<TryCastEventArgs>(Mob_TryingToCast);
            mob.SpellCastInterrupted += new EventHandler<InterruptCastEventArgs>(Mob_SpellCastInterrupted);
            mob.SpellCastBegun += new EventHandler<BeginCastEventArgs>(Mob_SpellCastBegun);

            // Subscribe to ZonePlayer events
            if (mob is ZonePlayer) {
                ((ZonePlayer)mob).LevelGained += new EventHandler<EventArgs>(ZonePlayer_LevelGained);
                ((ZonePlayer)mob).ServerCommand += new EventHandler<ServerCommandEventArgs>(ZonePlayer_ServerCommand);
            }

            // Subscribe to NPC specific events
            if (mob is NpcMob) {
                
            }

            _mobsListLock.EnterWriteLock();
            try {
                _mobs.Add(mob.ID, mob);     // Added by entity id (not db id)
            }
            catch (Exception ex) {
                _log.Error("Error adding a mob to the mob mgr... ", ex);
            }
            finally {
                _mobsListLock.ExitWriteLock();
            }
        }

        void Mob_SpellCastBegun(object sender, BeginCastEventArgs bce)
        {
            Mob m = sender as Mob;
            BeginCast bc = new BeginCast() { CasterId = (ushort)m.ID, SpellId = (ushort)bce.SpellId, CastTime = bce.CastTime };
            EQApplicationPacket<BeginCast> bcPacket = new EQApplicationPacket<BeginCast>(AppOpCode.BeginCast, bc);
            _zoneSvr.QueuePacketToNearbyClients(m, bcPacket, 200.0f, false);
        }

        internal void AddCorpse(Corpse corpse)
        {
            // Subscribe to corpse events
            corpse.WearChanged += new EventHandler<WearChangeEventArgs>(Mob_WearChanged);

            _corpseListLock.EnterWriteLock();
            try {
                _corpses.Add(corpse.ID, corpse);    // Added by entity id (not db id)
            }
            catch (Exception ex) {
                _log.Error("Error adding a corpse to the mob mgr... ", ex);
            }
            finally {
                _corpseListLock.ExitWriteLock();
            }

            if (!_corpseTimer.Enabled)
                _corpseTimer.Start();
        }

        /// <summary>Removes the mob from all of the mobs's hate list.</summary>
        internal void RemoveFromAllHateLists(Mob mob)
        {
            _mobsListLock.EnterReadLock();
            try {
                foreach (Mob m in this.AllMobs)
                    m.HateMgr.RemoveHate(mob);
            }
            catch (Exception ex) {
                _log.Error("Error removing a mob from hate lists in the mob mgr... ", ex);
            }
            finally {
                _mobsListLock.ExitReadLock();
            }
        }

        /// <summary>Removes the specified mob from the targets of all mobs, if they have it targeted.</summary>
        internal void RemoveFromAllTargets(Mob mob)
        {
            _mobsListLock.EnterReadLock();
            try {
                foreach (Mob m in this.AllMobs) {
                    if (m.TargetMob == mob) {
                        m.HateMgr.RemoveHate(mob);
                        m.TargetMob = null;
                    }
                }
            }
            catch (Exception ex) {
                _log.Error("Error removing a mob from targets in the mob mgr... ", ex);
            }
            finally {
                _mobsListLock.ExitReadLock();
            }
        }

        /// <summary>Only way to come back from this call is to re-Init - ONLY USE WHEN ZONE IS UNLOADING.</summary>
        /// <remarks>Use Dispose() when zone is tearing down.</remarks>
        internal void Clear()
        {
            _mobsListLock.EnterWriteLock();
            try
            {
                _mobs = null;
            }
            finally
            {
                _mobsListLock.ExitWriteLock();
            }

            _corpseListLock.EnterWriteLock();
            try {
                _corpses = null;
            }
            finally {
                _corpseListLock.ExitWriteLock();
            }

            _corpseTimer.Enabled = false;
        }

        private void NpcDeath(NpcMob npc, Mob lastAttacker, uint spellId, byte damageType, uint damage)
        {
            RemoveFromAllTargets(npc);  // Remove NPC from any entity's target & hate lists

            // Send Death Packet
            Death d = new Death()
            {
                SpawnId = (uint)npc.ID,
                KillerId = lastAttacker == null ? 0u : (uint)lastAttacker.ID,
                BindToZoneId = 0u,
                SpellId = spellId == 0u ? 0xFFFFFFFF : spellId,
                AttackSkillId = damageType,
                Damage = damage
            };
            EQApplicationPacket<Death> dPack = new EQApplicationPacket<Death>(AppOpCode.Death, d);
            _zoneSvr.QueuePacketToClients(lastAttacker, dPack, false);  // TODO: this should be cool with a null lastAttacker, right?

            // TODO: something about respawn?

            Mob killer = npc.HateMgr.GetTopHated();
            Mob xpMob = npc.HateMgr.GetTopDamager();

            // Give xp out
            if (xpMob == null)
                xpMob = killer;
            if (xpMob == null)
                xpMob = lastAttacker;

            // TODO: if xpMob is pet, get the owner

            ZonePlayer xpClient = xpMob as ZonePlayer;
            if (xpClient != null) {
                // TODO: handle raid split, LDON adventure crap, etc.

                float groupBonus = 1.0f;
                if (xpClient.IsGrouped) {
                    // TODO: handle group split

                    // TODO: add group xp bonus
                }
                else {
                    ConLevel conLvl = Mob.GetConsiderDificulty(xpClient.Level, npc.Level);
                    if (conLvl != ConLevel.Green) {
                        // TODO: figure high con bonus, if any
                        int baseXp = (int)(npc.Level * npc.Level * groupBonus * _zoneSvr.Zone.XPMultiplier);
                        xpClient.GiveXP((int)(npc.Level * npc.Level * 75 * _zoneSvr.Zone.XPMultiplier), conLvl);
                    }
                }

                // TODO: raise death merit event?
            }

            // TODO: faction hits

            // Make a corpse and add to the manager
            if (npc.IsLootable) {   // TODO: add additional checks for stuff like a guard killing the mob, etc.
                Corpse c = new Corpse(npc, npc.LootItems);
                AddCorpse(c);

                // TODO: if killer is a pet, get the owner
                if (xpClient != null)
                    c.AllowLooter(killer as ZonePlayer);
            }

            // TODO: raise script event
            _log.DebugFormat("NPC {0} has died", npc.ID);
        }

        private void PlayerDeath(ZonePlayer zp, Mob lastAttacker, uint spellId, byte damageType, uint damage)
        {
            _log.DebugFormat("{0} bought the farm. Fatal blow dealt by {1} with {2} damage, skill {3}, spell {4}.", zp.Name, lastAttacker.Name,
                damage, damageType, spellId);

            if (zp.IsDePopped) {
                _log.WarnFormat("{0} is trying to die more than once or something... is already depopped!", zp.Name);
                return;
            }

            _zoneSvr.SendLogoutPackets(zp.Client);

            // Make the become corpse packet and queue to player before Death opCode packet
            BecomeCorpse bc = new BecomeCorpse()
            {
                SpawnId = (uint)zp.ID,
                X = zp.X,
                Y = zp.Y,
                Z = zp.Z
            };
            EQApplicationPacket<BecomeCorpse> bcPack = new EQApplicationPacket<BecomeCorpse>(AppOpCode.BecomeCorpse, bc);
            _zoneSvr.QueuePacketToClient(zp.Client, bcPack, true, ZoneConnectionState.All);

            Death d = new Death()
            {
                SpawnId = (uint)zp.ID,
                KillerId = lastAttacker == null ? 0u : (uint)lastAttacker.ID,
                CorpseId = (uint)zp.ID,
                BindToZoneId = zp.PlayerProfile.Binds[0].ZoneId,
                SpellId = spellId == 0u ? 0xFFFFFFFF : spellId,
                AttackSkillId = spellId != 0u ? (uint)0xe7 : damageType,
                Damage = damage
            };
            EQApplicationPacket<Death> dPack = new EQApplicationPacket<Death>(AppOpCode.Death, d);
            _zoneSvr.QueuePacketToClients(lastAttacker, dPack, false);

            RemoveFromAllTargets(zp);
            // orig emu removed self from its own hate list... don't understand why you'd do that, so skipping

            if (!zp.IsGM) {     // GM's don't get penalized from dieing... muwhahaha

                // Figure out how much xp we lose, if any
                int xpLoss = 0;
                if (zp.Level >= WorldServer.ServerConfig.LevelDeathDoesXPLoss)  // TODO: don't lose xp when we have become an NPC?
                    xpLoss = (int)(zp.XP * WorldServer.ServerConfig.XPLossModifier);

                if (lastAttacker is ZonePlayer) // TODO: also check if the attacker is a pet owned by a player (no xp loss in that case)
                    xpLoss = 0; // Don't lose xp when dueling

                _log.DebugFormat("{0} is losing {1} xp due to his ass died.", zp.Name, xpLoss);
                zp.XP -= xpLoss;

                // TODO: fade buffs (ALL - good and bad)
                // TODO: unmem spells (don't send packets)

                PlayerCorpse pc = new PlayerCorpse(zp, xpLoss, _zoneSvr.HasGraveyard());
                AddCorpse(pc);
                _zoneSvr.QueuePacketToClients(zp, bcPack, true);    // send the become corpse packet to all players in the zone
            }
            else {
                // TODO: fade just detrimental buffs
            }

            // Send player to bind point
            _zoneSvr.MovePlayer(zp, zp.PlayerProfile.Binds[0].ZoneId, 0, zp.PlayerProfile.Binds[0].X, zp.PlayerProfile.Binds[0].Y, zp.PlayerProfile.Binds[0].Z, zp.PlayerProfile.Binds[0].Heading, ZoneMode.ZoneToBindPoint);
        }

        #region Event Handlers
        void Mob_StanceChanged(object sender, EventArgs e)
        {
            Mob mob = sender as Mob;
            _zoneSvr.SendStancePacket(SpawnAppearanceType.Animation, mob, true, false);

            if (sender is ZonePlayer)
            {
                if (((ZonePlayer)sender).IsAIControlled)
                    _zoneSvr.SendStancePacket(SpawnAppearanceType.Animation, mob, (short)Stance.Freeze, false, false);
            }
        }

        void Mob_StanceChanged(object sender, StanceChangedEventArgs sce)
        {
            Mob mob = sender as Mob;
            _zoneSvr.SendStancePacket(sce.AppearanceType, mob, sce.StanceValue, true, false);
        }

        void Mob_PositionUpdate(object sender, PosUpdateEventArgs e)
        {
            Mob mob = sender as Mob;
            PlayerPositionUpdateServer ppus;

            if (e.UseDelta)
            {
                ppus = mob.GetSpawnUpdate();
                EQApplicationPacket<PlayerPositionUpdateServer> ppusPack = new EQApplicationPacket<PlayerPositionUpdateServer>(AppOpCode.ClientUpdate, ppus);
                _zoneSvr.QueuePacketToNearbyClients(mob, ppusPack, 800, true);
            }
            else
            {
                ppus = mob.GetSpawnUpdateNoDelta();
                EQApplicationPacket<PlayerPositionUpdateServer> ppusPack = new EQApplicationPacket<PlayerPositionUpdateServer>(AppOpCode.ClientUpdate, ppus);
                if (e.OnlyNearby)
                    _zoneSvr.QueuePacketToNearbyClients(mob, ppusPack, 800, true);
                else
                    _zoneSvr.QueuePacketToClients(mob, ppusPack, true);
            }
        }

        void Mob_WearChanged(object sender, WearChangeEventArgs wce)
        {
            //_log.DebugFormat("Sending wear change for {0} ({1})", sender, wce.WearChange);
            EQApplicationPacket<WearChange> wcPack = new EQApplicationPacket<WearChange>(AppOpCode.WearChange, wce.WearChange);
            _zoneSvr.QueuePacketToClients(sender as Mob, wcPack, true);
        }

        void Mob_HpUpdated(object sender, EventArgs e)
        {
            Mob mob = sender as Mob;
            HPUpdateRatio hpRatio = mob.GetHPUpdateRatio();
            HPUpdateExact hpExact = mob.GetHPUpdateExact(); // TODO: adjust for itembonuses

            EQApplicationPacket<HPUpdateRatio> hpRatioPacket = new EQApplicationPacket<HPUpdateRatio>(AppOpCode.MobHealth, hpRatio);
            EQApplicationPacket<HPUpdateExact> hpExactPacket = new EQApplicationPacket<HPUpdateExact>(AppOpCode.HPUpdate, hpExact);

            // Send to those who have us targeted
            _zoneSvr.QueuePacketToClientsByTarget(mob, hpRatioPacket, true, false);
            // TODO: queue to groups for npc health - whatever that means

            // TODO: once groups are in, send to group

            // TODO: send to raid

            // TODO: once pets are in, send to master/pet

            ZonePlayer zp = sender as ZonePlayer;
            if (zp != null) {
                _zoneSvr.QueuePacketToClient(zp.Client, hpExactPacket, true, ZoneConnectionState.All);  // Send exact hps to self
                //_log.DebugFormat("Sent exact HPs to self - max:{0} cur:{1}", hpExact.MaxHP, hpExact.CurrentHP);
            }

            // TODO: HP Script Event
        }

        void Mob_PlayAnimation(object sender, AnimationEventArgs ae)
        {
            Mob mob = sender as Mob;
            Animation anim = new Animation() { Action = 10, SpawnID = (ushort)mob.ID, Value = (byte)ae.Anim }; 
            EQApplicationPacket<Animation> animPack = new EQApplicationPacket<Animation>(AppOpCode.Animation, anim);
            _zoneSvr.QueuePacketToNearbyClients(mob, animPack, 200.0f, false, true);
        }

        void Mob_Damaged(object sender, DamageEventArgs de)
        {
            Mob sourceMob = this[de.SourceId];  // This safely gets a null if sourceId == 0
            Mob targetMob = sender as Mob;

            if (targetMob.Dead) {
                if (targetMob is NpcMob)    // TODO: will want to handle pets here as well
                    NpcDeath(targetMob as NpcMob, sourceMob, de.SpellId, de.Type, de.Damage);
                else if (targetMob is ZonePlayer)
                    PlayerDeath(targetMob as ZonePlayer, sourceMob, de.SpellId, de.Type, de.Damage);

                return;
            }

            CombatDamage cd = new CombatDamage()
            {
                TargetId = (ushort)targetMob.ID,
                SourceId = de.SourceId,
                DamageType = de.Type,
                Damage = de.Damage,
                SpellId = de.SpellId
            };

            EQApplicationPacket<CombatDamage> cdPack = new EQApplicationPacket<CombatDamage>(AppOpCode.Damage, cd);

            // TODO: choose right filter and then add to packet queue methods

            // Send to attacker if it's a player    TODO: verify this sends spell dmg
            if (de.SourceId != 0 && sourceMob is ZonePlayer)
                _zoneSvr.QueuePacketToClient(((ZonePlayer)sourceMob).Client, cdPack, true, ZoneConnectionState.Connected);

            // Send to nearby players
            _zoneSvr.QueuePacketToNearbyClients(targetMob, cdPack, 200.0f, true, true, new int[] { de.SourceId });

            // Send to defender if it's a player
            if (targetMob is ZonePlayer) {
                //_log.DebugFormat("Telling {0} they were damaged for {1} points.", targetMob.Name, de.Damage);
                _zoneSvr.QueuePacketToClient(((ZonePlayer)targetMob).Client, cdPack, true, ZoneConnectionState.Connected);
            }
        }

        void ZonePlayer_LevelGained(object sender, EventArgs e)
        {
            ZonePlayer zp = sender as ZonePlayer;
            LevelAppearance la = new LevelAppearance()
            {
                SpawnID = (uint)zp.ID,
                Parm1 = 0x4d,
                Parm2 = 0x4d + 1,
                Parm3 = 0x4d + 2,
                Parm4 = 0x4d + 3,
                Parm5 = 0x4d + 4,
                Value1a = 1,
                Value2a = 2,
                Value3a = 1,
                Value3b = 1,
                Value4a = 1,
                Value4b = 1,
                Value5a = 2
            };
            EQApplicationPacket<LevelAppearance> laPack = new EQApplicationPacket<LevelAppearance>(AppOpCode.LevelAppearance, la);
            _zoneSvr.QueuePacketToNearbyClients(zp, laPack, 200.0f, false, true);

            _zoneSvr.UpdateWorldWho(zp.Client);
            _log.InfoFormat("{0} has dinged to level {1}", zp.Name, zp.Level);
        }

        void ZonePlayer_ServerCommand(object sender, ServerCommandEventArgs e)
        {
            _zoneSvr.DispatchServerCommand(sender as ZonePlayer, e.Command, e.Arguments);
        }

        void Mob_IdleScanning(object sender, EventArgs e)
        {
            Mob scanner = sender as Mob;

            _mobsListLock.EnterReadLock();
            try {
                foreach (Mob mob in this.AllMobs) {
                    if (scanner.CheckAggro(mob)) {
                        scanner.HateMgr.AddHate(mob, 0, 0);
                        return;
                    }
                }
            }
            catch (Exception ex) {
                _log.Error("Error when scanning for nearby mobs... ", ex);
            }
            finally {
                _mobsListLock.ExitReadLock();
            }
        }

        /// <summary>Sends a channel message to all clients.</summary>
        void Mob_ChannelMessage(object sender, ChannelMessageEventArgs cme)
        {
            this.MobListLock.EnterReadLock();
            try {
                foreach (ZonePlayer zp in this.Clients) {

                    // See if we need to set up a filter
                    MessageFilter filter = MessagingManager.GetChannelMessageFilter(cme.Channel);

                    if (cme.Channel != MessageChannel.Say || zp.Distance(cme.From) < MessagingManager.SAY_RANGE) { // Only SAY is limited in range
                        
                        // TODO: Ensure the filters pass

                        if (cme.Channel == MessageChannel.GMSay && !zp.IsGM)
                            return; // Don't send /petition & /pr to non-GMs
                        
                        zp.SendChannelMessage(cme.From.Name, string.Empty, cme.Channel, cme.Language, cme.LangSkill, cme.Message);
                    }
                }
            }
            catch (Exception ex) {
                _log.Error("Error while iterating mobs list to send channel message... ", ex);
            }
            finally {
                this.MobListLock.ExitReadLock();
            }
        }

        // Fired when mob is running checks for if it can cast a spell / use an ability, etc.
        void Mob_TryingToCast(object sender, TryCastEventArgs e)
        {
            // No combat zone == no detrimental spells
            if (!_zoneSvr.Zone.CanCombat && (sender as Mob).GetSpell(e.SpellId).IsDetrimental) {
                e.Allowed = false;
                if (sender is ZonePlayer)
                    (sender as ZonePlayer).MsgMgr.SendMessageID((uint)MessageType.Common13, MessageStrings.SPELL_WOULDNT_HOLD);
            }

            // TODO: check for blocked spells in zone, can levitate, outdoor/indoor, gate prevention (GMs ignore)
        }

        void Mob_SpellCastInterrupted(object sender, InterruptCastEventArgs ice)
        {
            ZonePlayer zp = sender as ZonePlayer;   // a player trigger this event?
            if (zp != null) {
                // Send the client a packet
                InterruptCast ic = new InterruptCast() { SpawnId = (uint)zp.ID, MessageId =  (uint)ice.Message};
                EQRawApplicationPacket icPacket = new EQRawApplicationPacket(AppOpCode.InterruptCast, zp.Client.IPEndPoint, ic.Serialize());
                _zoneSvr.QueuePacketToClient(zp.Client, icPacket, true, ZoneConnectionState.All);

                zp.SendSpellBarEnable(ice.SpellId);
            }

            // Now notify people in the area

            // First determine the message they should be sent
            MessageStrings othersMsg = MessageStrings.INTERRUPT_SPELL_OTHER;
            switch (ice.Message) {
                case MessageStrings.SPELL_FIZZLE:
                    othersMsg = MessageStrings.SPELL_FIZZLE_OTHER;
                    break;
                case MessageStrings.MISS_NOTE:
                    othersMsg = MessageStrings.MISSED_NOTE_OTHER;
                    break;
                case MessageStrings.SONG_ENDS_ABRUPTLY:
                    othersMsg = MessageStrings.SONG_ENDS_ABRUPTLY_OTHER;
                    break;
                case MessageStrings.SONG_ENDS:
                    othersMsg = MessageStrings.SONG_ENDS_OTHER;
                    break;
            }

            Mob m = sender as Mob;
            InterruptCast icb = new InterruptCast() { MessageId = (uint)othersMsg, SpawnId = (uint)m.ID, Message = m.DisplayName };
            EQRawApplicationPacket icbPacket = new EQRawApplicationPacket(AppOpCode.InterruptCast, null, icb.Serialize());
            _zoneSvr.QueuePacketToNearbyClients(m, icbPacket, 200.0f, true, true);  // TODO: pass in PC or NPC spell for the queue call to filter on when filters are in
        }
        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            _mobsListLock.Dispose();
            _corpseListLock.Dispose();
        }

        #endregion
    }
}
