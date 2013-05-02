using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using EQEmulator.Servers.Internals.Data;
using EQEmulator.Servers.Internals.Packets;

namespace EQEmulator.Servers.Internals.Entities
{
    internal enum SpecialAttacks
    {
        Summon = 0,     // = 'S',
        Enrage,         // = 'E',
        Rampage,        // = 'R',
        AreaRampage,    // = 'r',
        Flurry,         // = 'F',
        Triple,         // = 'T',
        Quad,           // = 'Q',
        Bane,           // = 'b',
        Magical,        // = 'm',
        RangedAttack,   // = 'Y'
    }

    internal enum SpecialDefenses
    {
        Unslowable = 0,         // 'U',
        Unmezzable,             // = 'M',
        Uncharmable,            // = 'C',
        Unstunable,             // = 'N',
        Unsnareable,            // = 'I',
        Unfearable,             // = 'D',
        ImmuneMelee,            // = 'A',
        ImmuneMagic,            // = 'B',
        ImmuneFleeing,          // = 'f',
        ImmuneMeleeExceptBane,  // = 'O',
        ImmuneMeleeNonmagical,  // = 'W',
        ImmuneAggro,            // = 'H'    Won't ever aggro
        ImmuneTarget,           // = 'G'    Can't ever be aggro'd
        ImmuneCastingFromRange, // = 'g',
        ImmuneFeignDeath,       // = 'd'
    }

    #region Event Data Structures
    
    #endregion

    class NpcMob : Mob
    {
        // Timer delay values - all in ms
        private const int AUTOCAST_DELAY = 1000;
        private const int GLOBAL_POS_UPDATE_INTERVAL = 60000;
        private const int HP_UPDATE_INTERVAL = 1000;
        private const int MAX_SPECIAL_ATTACKS = 10;
        private const int MAX_SPECIAL_DEFENSES = 15;
        private const float OOC_REGEN_PCT = .05f;   // Percent of hp the npc regens each tick while out of combat

        private int _npcDbId, _gridId, _curWpIdx = 1;
        private float _aggroRange, _assistRange;
        private int _accuracy, _meleeTexture1, _meleeTexture2;
        private short _mr, _cr, _dr, _fr, _pr;
        private bool _willAggroNPCs, _taunting = false, _patrol = false, _enraged = false;
        private List<InventoryItem> _lootItems = new List<InventoryItem>(10);
        private Dictionary<uint, InventoryItem> _equipedItems = new Dictionary<uint, InventoryItem>(Character.MAX_EQUIPABLES); // Keyed by equip spot
        private byte _wanderType = 0, _pauseType = 0;
        private SortedList<int, Waypoint> _waypoints = null;
        private Waypoint _curWaypoint;
        private float? _guardX = null, _guardY = null, _guardZ = null, _guardHeading = null;
        private SimpleTimer _globalPositionUpdateTimer, _hpUpdateTimer;
        protected bool[] _specialAttacks = new bool[MAX_SPECIAL_ATTACKS];
        protected bool[] _specialDefenses = new bool[MAX_SPECIAL_DEFENSES];
        protected ushort[] _skills = new ushort[Character.MAX_SKILL];
        private int _minDmg, _maxDmg;

        public struct Waypoint
	    {
    		public float X;
            public float Y;
            public float Z;
            public short Pause;

            public Waypoint(float x, float y, float z, short pause)
            {
                this.X = x;
                this.Y = y;
                this.Z = z;
                this.Pause = pause;
            }
	    }

        public NpcMob(int entityId, Npc npcData, int gridId, float x, float y, float z, float heading)
            : base(entityId, npcData.Name, npcData.SurName, npcData.AC, (short)npcData.Attack, (short)npcData.STR, (short)npcData.STA,
                (short)npcData.DEX, (short)npcData.AGI, npcData.INT, npcData.WIS, npcData.CHA, npcData.Hp, npcData.Hp, npcData.Gender,
                (short)npcData.HpRegenRate, (short)npcData.ManaRegenRate, npcData.Race, npcData.NpcClass, npcData.NpcLevel, (BodyType)npcData.BodyType,
                0, x, y, z, heading, npcData.Size, npcData.RunSpeed, 0)
	    {
            _npcDbId = npcData.NpcID;
            _gridId = gridId;
            _seeInvis = npcData.SeeInvis;
            _seeInvisToUndead = npcData.SeeInvisUndead;
            _seeHide = npcData.SeeHide;
            _seeImprovedHide = npcData.SeeImprovedHide;
            _texture = npcData.Texture;
            _helmTexture = npcData.HelmTexture;
            _hairColor = (byte)npcData.HairColorLuclin;
            _beardColor = (byte)npcData.BeardColorLuclin;
            _eyeColor1 = (byte)npcData.EyeColorLuclin;
            _eyeColor2 = (byte)npcData.EyeColor2Luclin;
            _hairStyle = (byte)npcData.HairStyleLuclin;
            _luclinFace = (byte)npcData.Face;
            _beard = (byte)npcData.BeardLuclin;
            _aggroRange = npcData.AggroRadius;
            _assistRange = npcData.AggroRadius;
            _findable = npcData.Findable;
            _trackable = npcData.Trackable;
            _accuracy = npcData.Accuracy;
            _attackSpeed = npcData.AttackSpeed;
            _willAggroNPCs = npcData.NpcAggro;
            _meleeTexture1 = npcData.MeleeTexture1;
            _meleeTexture2 = npcData.MeleeTexture2;
            _minDmg = npcData.MinDamage;
            _maxDmg = npcData.MaxDamage;
            
            // TODO: timers... combat event, swarm, class attack, knight attack, assist, enraged, taunt
            _globalPositionUpdateTimer = new SimpleTimer(GLOBAL_POS_UPDATE_INTERVAL);   // TODO: does this belong in mob?
            _hpUpdateTimer = new SimpleTimer(HP_UPDATE_INTERVAL);

            // Mana regen rate adjustments for lazy db updaters
            _manaRegen = Math.Max(_manaRegen, (short)0);
            if (GetCasterClass() != CasterClass.None && _manaRegen == 0)
                _manaRegen = (short)((this.Level / 10) + 4);

            // TODO: HP regen adjustments (Gives low end mobs no regen if set to 0 in db.  Makes low end mobs more killable)
            _hpRegen = Math.Max(_hpRegen, (short)0);
            if (_hpRegen == 0) {

            }

            // min and max dmg adjustments
            if (_maxDmg == 0) {
                int acAdj = 12;

                if (this.Level >= 66) {
                    if (_minDmg == 0)
                        _minDmg = 200;
                    if (_maxDmg == 0)
                        _maxDmg = ((((99000) * (this.Level - 64)) / 400) * acAdj / 10);
                }
                else if (this.Level >= 60 && this.Level <= 65) {
                    if (_minDmg == 0)
                        _minDmg = (this.Level + (this.Level / 3));
                    if (_maxDmg == 0)
                        _maxDmg = (this.Level * 3) * acAdj / 10;
                }
                else if (this.Level >= 51 && this.Level <= 59) {
                    if (_minDmg == 0)
                        _minDmg = (this.Level + (this.Level / 3));
                    if (_maxDmg == 0)
                        _maxDmg = (this.Level * 3) * acAdj / 10;
                }
                else if (this.Level >= 40 && this.Level <= 50) {
                    if (_minDmg == 0)
                        _minDmg = this.Level;
                    if (_maxDmg == 0)
                        _maxDmg = (this.Level * 3) * acAdj / 10;
                }
                else if (this.Level >= 28 && this.Level <= 39) {
                    if (_minDmg == 0)
                        _minDmg = this.Level / 2;
                    if (_maxDmg == 0)
                        _maxDmg = ((this.Level * 2) + 2) * acAdj / 10;
                }
                else if (this.Level <= 27) {
                    if (_minDmg == 0)
                        _minDmg = 1;
                    if (_maxDmg == 0)
                        _maxDmg = (this.Level * 2) * acAdj / 10;
                }

                int clFact = GetClassLevelFactor();
                _minDmg = (_minDmg * clFact) / 220;
                _maxDmg = (_maxDmg * clFact) / 220;
            }

            // TODO: calc max mana and then set mana

            // Adjust resists if needed
            _mr = npcData.MR < 1 ? (short)((this.Level * 11) / 10) : npcData.MR;
            _cr = npcData.MR < 1 ? (short)((this.Level * 11) / 10) : npcData.CR;
            _dr = npcData.MR < 1 ? (short)((this.Level * 11) / 10) : npcData.DR;
            _fr = npcData.MR < 1 ? (short)((this.Level * 11) / 10) : npcData.FR;
            _pr = npcData.MR < 1 ? (short)((this.Level * 11) / 10) : npcData.PR;

            // TODO: faction

            // TODO: spells

            // TODO: set guard spot?

            if (npcData.Loot != null)
                AddLoot(npcData);   // Give npc some loot

            //_log.DebugFormat("{0} has {1} items - {2} of which are equipped...", npcData.Name, _lootItems.Count, _equipedItems.Count);
            //foreach (KeyValuePair<uint, LootItem> kvp in _equipedItems) {
            //    _log.DebugFormat("slot {0} has itemId {1}", kvp.Key, kvp.Value.ItemID);
            //}

            if (npcData.NpcSpecialAttacks != null)
                ParseSpecialAttacks(npcData.NpcSpecialAttacks);

            StartAI();
	    }

        #region Properties
        internal override bool IsAIControlled
        {
            get { return true; }
        }

        internal int NpcDbId
        {
            get { return _npcDbId; }
        }

        internal bool Roamer
        {
            get { return (_waypoints != null && _waypoints.Count > 1); }
        }

        internal bool Guarding
        {
            get { return _guardHeading != null; }
        }

        protected internal override Mob TargetMob
        {
            set
            {
                if (base.TargetMob != value) {
                    _log.DebugFormat("NPC {0} has acquired new target {1}", this.Name, value == null ? "NULL" : value.Name);
                    
                    if (value != null) {
                        base.TargetMob = value;

                        // TODO: handle some shit to do with swarms

                        SetAttackTimer();
                    }
                    else {
                        _attackTimer.Enabled = false;
                        _rangedAttackTimer.Enabled = false;
                        _dwAttackTimer.Enabled = false;
                    }
                }
            }
        }

        internal int Accuracy
        {
            get { return _accuracy; }
            set { _accuracy = value; }
        }

        internal bool Enraged
        {
            get { return _enraged; }
            set { _enraged = value; }
        }

        internal override bool IsAttackable
        {
            get
            {
                if (!base.IsAttackable)
                    return false;

                if (this.BodyType == BodyType.NoTarget || this.BodyType == BodyType.NoTarget2)
                    return false;   // Unable to attack untargetable mobs

                // TODO: handle LDON treasure

                return true;
            }
        }

        internal List<InventoryItem> LootItems
        {
            get { return _lootItems; }
        }
        #endregion

        protected void ParseSpecialAttacks(string str)
        {
            for (int i = 0; i < str.Length; i++) {
                    switch (str[i]) {
                        case 'E':
                            _specialAttacks[(int)SpecialAttacks.Enrage] = true;
                            break;
                        case 'F':
                            _specialAttacks[(int)SpecialAttacks.Flurry] = true;
                            break;
                        case 'R':
                            _specialAttacks[(int)SpecialAttacks.Rampage] = true;
                            break;
                        case 'r':
                            _specialAttacks[(int)SpecialAttacks.AreaRampage] = true;
                            break;
                        case 'S':
                            _specialAttacks[(int)SpecialAttacks.Summon] = true;
                            // TODO: timer
                            break;
                        case 'T':
                            _specialAttacks[(int)SpecialAttacks.Triple] = true;
                            break;
                        case 'Q':
                            _specialAttacks[(int)SpecialAttacks.Triple] = true; // Quad needs triple to work correctly
                            _specialAttacks[(int)SpecialAttacks.Quad] = true;
                            break;
                        case 'b':
                            _specialAttacks[(int)SpecialAttacks.Bane] = true;
                            break;
                        case 'm':
                            _specialAttacks[(int)SpecialAttacks.Magical] = true;
                            break;
                        case 'U':
                            _specialDefenses[(int)SpecialDefenses.Unslowable] = true;
                            break;
                        case 'M':
                            _specialDefenses[(int)SpecialDefenses.Unmezzable] = true;
                            break;
                        case 'C':
                            _specialDefenses[(int)SpecialDefenses.Uncharmable] = true;
                            break;
                        case 'N':
                            _specialDefenses[(int)SpecialDefenses.Unstunable] = true;
                            break;
                        case 'I':
                            _specialDefenses[(int)SpecialDefenses.Unsnareable] = true;
                            break;
                        case 'D':
                            _specialDefenses[(int)SpecialDefenses.Unfearable] = true;
                            break;
                        case 'A':
                            _specialDefenses[(int)SpecialDefenses.ImmuneMelee] = true;
                            break;
                        case 'B':
                            _specialDefenses[(int)SpecialDefenses.ImmuneMagic] = true;
                            break;
                        case 'f':
                            _specialDefenses[(int)SpecialDefenses.ImmuneFleeing] = true;
                            break;
                        case 'O':
                            _specialDefenses[(int)SpecialDefenses.ImmuneMeleeExceptBane] = true;
                            break;
                        case 'W':
                            _specialDefenses[(int)SpecialDefenses.ImmuneMeleeNonmagical] = true;
                            break;
                        case 'H':
                            _specialDefenses[(int)SpecialDefenses.ImmuneAggro] = true;
                            break;
                        case 'G':
                            _specialDefenses[(int)SpecialDefenses.ImmuneTarget] = true;
                            break;
                        case 'g':
                            _specialDefenses[(int)SpecialDefenses.ImmuneCastingFromRange] = true;
                            break;
                        case 'd':
                            _specialDefenses[(int)SpecialDefenses.ImmuneFeignDeath] = true;
                            break;
                        case 'Y':
                            _specialAttacks[(int)SpecialAttacks.RangedAttack] = true;
                            break;
                        default:
                            throw new ArgumentException("Unsupported special defense or special attack character.", "str");
                    }
                }
        }

        internal override bool HasSpecialDefense(SpecialDefenses specDef)
        {
            return _specialDefenses[(int)specDef];
        }

        internal override bool HasSpecialAttack(SpecialAttacks specAtt)
        {
            return _specialAttacks[(int)specAtt];
        }

        internal override Packets.Spawn GetSpawn()
        {
            Packets.Spawn s = base.GetSpawn();
            s.NPC = 1;
            s.IsNpc = 1;

            //_log.DebugFormat("NPC {0} set with x of {1}({2})({3}), y of {4}({5})({6}) and z of {7}({8})({9})", this.Name, this.X, s.DeltaHeadingAndX, s.XPos,
            //    this.Y, s.YAndAnimation, s.YPos, this.Z, s.ZAndDeltaY, s.ZPos);

            // TODO: handle case of being a pet (or not, pet class should derive from this class and override this)
            s.IsPet = 0;

            // Equipment    TODO: equip & colors might make sense to refactor into mob
            s.EquipHelmet = (uint)GetEquipmentMaterial(EquipableType.Head);
            s.EquipChest = (uint)GetEquipmentMaterial(EquipableType.Chest);
            s.EquipArms = (uint)GetEquipmentMaterial(EquipableType.Arms);
            s.EquipBracers = (uint)GetEquipmentMaterial(EquipableType.Bracer);
            s.EquipHands = (uint)GetEquipmentMaterial(EquipableType.Hands);
            s.EquipLegs = (uint)GetEquipmentMaterial(EquipableType.Legs);
            s.EquipFeet = (uint)GetEquipmentMaterial(EquipableType.Feet);
            s.EquipPrimary = (uint)GetEquipmentMaterial(EquipableType.Primary);
            s.EquipSecondary = (uint)GetEquipmentMaterial(EquipableType.Secondary);

            // Colors
            s.HelmetColor = GetEquipmentColor(EquipableType.Head);
            s.ChestColor = GetEquipmentColor(EquipableType.Chest);
            s.ArmsColor = GetEquipmentColor(EquipableType.Arms);
            s.BracersColor = GetEquipmentColor(EquipableType.Bracer);
            s.HandsColor = GetEquipmentColor(EquipableType.Hands);
            s.LegsColor = GetEquipmentColor(EquipableType.Legs);
            s.FeetColor = GetEquipmentColor(EquipableType.Feet);
            s.PrimaryColor = GetEquipmentColor(EquipableType.Primary);
            s.SecondaryColor = GetEquipmentColor(EquipableType.Secondary);
            
            return s;
        }

        internal void LoadGrid(int zoneId, EmuDataContext dbCtx)
        {
            if (_gridId < 1)
                return;

            //_log.DebugFormat("Loading grid {0} for {1}", _gridId, _name);

            Grid grid = dbCtx.Grids.SingleOrDefault(g => g.GridID == _gridId && g.ZoneID == zoneId);

            if (grid == null)
            {
                _log.WarnFormat("Unable to locate grid {0} in db for {1}... why does it have a non-zero grid id?", _gridId, this.Name);
                return;
            }

            _wanderType = grid.WanderType;
            _pauseType = grid.PauseType;

            var ges = from ge in dbCtx.GridEntries
                      where ge.GridID == _gridId && ge.ZoneID == zoneId
                      orderby ge.Ordinal
                      select new { Ordinal = ge.Ordinal, X = ge.X, Y = ge.Y, Z = ge.Z, Pause = ge.Pause };

            //_log.DebugFormat("Found {0} grid entries - creating that many waypoints.", ges.Count());
            _waypoints = new SortedList<int, Waypoint>(ges.Count());
            foreach (var ge in ges)     // TODO: may need some code for sending midair waypoints to the ground
                _waypoints.Add(ge.Ordinal, new Waypoint(ge.X, ge.Y, ge.Z, ge.Pause));

            if (_waypoints.Count > 1)
            {
                //_log.Debug("Minimum waypoint found was " + _waypoints.Keys.Min());
                UpdateWaypoint(_waypoints.Keys.Min());
                PauseAtWaypoint();
                SendTo(_curWaypoint.X, _curWaypoint.Y, _curWaypoint.Z);
                if (_wanderType == 1 || _wanderType == 2)
                    CalculateNewWaypoint();
            }
        }

        internal void UpdateWaypoint(int ordinal)
        {
            _curWaypoint = _waypoints[ordinal];

            // TODO: perhaps fix up Z pathing
        }

        internal void AddLoot(Npc npc)
        {
            Random rand = new Random();

            // Coin
            if (npc.Loot.MinCash > npc.Loot.MaxCash)
                _log.ErrorFormat("{0}'s minCash({1}) > maxCash({2}), please fix.", npc.Name, npc.Loot.MinCash, npc.Loot.MaxCash);
            else if (npc.Loot.MaxCash > 0) {
                uint cash = 0;

                if (npc.Loot.MaxCash == npc.Loot.MinCash)
                    cash = npc.Loot.MaxCash;
                else
                    cash = (uint)rand.Next((int)npc.Loot.MinCash, (int)npc.Loot.MaxCash + 1);

                if (cash != 0) {
                    this.Platinum = cash / 1000;
                    cash -= this.Platinum * 1000;
                    this.Gold = cash / 100;
                    cash -= this.Gold * 100;
                    this.Silver = cash / 10;
                    cash -= this.Silver * 10;
                    this.Copper = cash;
                }
            }

            // Items
            int sumProb = 0, roll = 0, probIter = 0;
            
            foreach (LootEntry le in npc.Loot.LootEntries) {
                for (int i = 0; i < le.MaxDrops; i++) {
                    if (rand.Next(0, 100) < le.Probability) {
                        sumProb = le.LootDrops.Sum(ld => ld.Probability);     // Get total probability of all loot drops
                        roll = rand.Next(0, sumProb + 1);

                        probIter = 0;
                        foreach (LootDrop ld in le.LootDrops) {
                            probIter += ld.Probability;
                            if (roll < probIter) {
                                InventoryItem invItem = new InventoryItem()
                                {
                                    Charges = ld.ItemCharges,
                                    Color = ld.Item.Color ?? 0,
                                    Item = ld.Item,
                                    ItemID = ld.ItemID,
                                    SlotID = 0
                                };

                                AddItem(invItem, ld.Equipable, false);
                                break;
                            }
                        }
                    }
                }
            }
        }

        internal void AddItem(InventoryItem invItem, bool equip, bool wearChange)
        {
            if (equip) {
                EquipableType equipSlot = EquipableType.Unequipable;
                short equipMat = 0;

                if (invItem.Item.Material == 0 || (invItem.Item.slots & (1 << (int)InventorySlot.Primary | 1 << (int)InventorySlot.Secondary)) > 0) {
                    if (invItem.Item.IDFile.Length > 2)
                        equipMat = short.Parse(invItem.Item.IDFile.Substring(2));
                }
                else
                    equipMat = (short)invItem.Item.Material;

                if ((invItem.Item.slots & (1 << (int)InventorySlot.Primary)) > 0 && (!_equipedItems.ContainsKey((byte)EquipableType.Primary))) {
                    // TODO: add weapon procs (need spells in I think)

                    equipSlot = EquipableType.Primary;
                }
                else if ((invItem.Item.slots & (1 << (int)InventorySlot.Secondary)) > 0 && (!_equipedItems.ContainsKey((byte)EquipableType.Secondary))) {
                    if (this.Level >= 13 || invItem.Item.Damage == 0) {     // TODO: add check here for if we are a pet... pets will take anything?
                        if (invItem.Item.ItemType == (byte)ItemType.OneHandBash || invItem.Item.ItemType == (byte)ItemType.OneHandSlash
                        || invItem.Item.ItemType == (byte)ItemType.Shield || invItem.Item.ItemType == (byte)ItemType.Pierce) {

                            // TODO: add weapon procs (need spells in I think)

                            equipSlot = EquipableType.Secondary;
                        }
                    }
                }
                else if ((invItem.Item.slots & (1 << (int)InventorySlot.Head)) > 0 && (!_equipedItems.ContainsKey((byte)EquipableType.Head)))
                    equipSlot = EquipableType.Head;
                else if ((invItem.Item.slots & (1 << (int)InventorySlot.Chest)) > 0 && (!_equipedItems.ContainsKey((byte)EquipableType.Chest)))
                    equipSlot = EquipableType.Chest;
                else if ((invItem.Item.slots & (1 << (int)InventorySlot.Arms)) > 0 && (!_equipedItems.ContainsKey((byte)EquipableType.Arms)))
                    equipSlot = EquipableType.Arms;
                else if ((invItem.Item.slots & ((1 << (int)InventorySlot.Bracer1) | (1 << (int)InventorySlot.Bracer2))) > 0 && (!_equipedItems.ContainsKey((byte)EquipableType.Bracer)))
                    equipSlot = EquipableType.Bracer;
                else if ((invItem.Item.slots & (1 << (int)InventorySlot.Hands)) > 0 && (!_equipedItems.ContainsKey((byte)EquipableType.Hands)))
                    equipSlot = EquipableType.Hands;
                else if ((invItem.Item.slots & (1 << (int)InventorySlot.Legs)) > 0 && (!_equipedItems.ContainsKey((byte)EquipableType.Legs)))
                    equipSlot = EquipableType.Legs;
                else if ((invItem.Item.slots & (1 << (int)InventorySlot.Feet)) > 0 && (!_equipedItems.ContainsKey((byte)EquipableType.Feet)))
                    equipSlot = EquipableType.Feet;

                if (equipSlot != EquipableType.Unequipable) {
                    _equipedItems[(byte)equipSlot] = invItem;   // Equip the item
                    invItem.SlotID = (int)InventoryManager.GetEquipableSlot(equipSlot);

                    if (wearChange) {
                        WearChange wc = new WearChange();
                        wc.SpawnId = (short)this.ID;
                        wc.WearSlotId = (byte)equipSlot;
                        wc.Material = equipMat;
                        OnWearChanged(new WearChangeEventArgs(wc)); // Fire wear changed event
                    }

                    CalcStatModifiers();
                }
            }

            _lootItems.Add(invItem);    // SlotID stored if it was equipped
        }

        internal void GiveItem(InventoryItem invItem)
        {
            InventoryItem newItem = invItem.ToNewInventoryItem();
            AddItem(newItem, true, true);
        }

        protected override int GetEquipmentMaterial(EquipableType et)
        {
            if (!_equipedItems.ContainsKey((byte)et)) {
                // Nothing equipped in that slot, so use the predefined texture
                switch (et) {
                    case EquipableType.Head:
                        return HelmTexture;
                    case EquipableType.Chest:
                        return Texture;
                    case EquipableType.Primary:
                        return _meleeTexture1;
                    case EquipableType.Secondary:
                        return _meleeTexture2;
                    default:
                        return 0;   // npc has nothing in the slot and it's not a slot that has a predefined texture
                }
            }

            // Npc has something in the slot, let's get it
            if (et == EquipableType.Primary || et == EquipableType.Secondary) {   // Held items need the idfile
                if (_equipedItems[(byte)et].Item.IDFile.Length > 2)
                    return int.Parse(_equipedItems[(byte)et].Item.IDFile.Substring(2));
                else
                    return _equipedItems[(byte)et].Item.Material;
            }
            else
                return _equipedItems[(byte)et].Item.Material;
        }

        protected override uint GetEquipmentColor(EquipableType et)
        {
            if (_equipedItems.ContainsKey((byte)et))
                return _equipedItems[(byte)et].Color;

            return 0;
        }

        protected override Item GetEquipment(EquipableType equipSlot)
        {
            InventoryItem invItem;
            if (_equipedItems.TryGetValue((byte)equipSlot, out invItem))
                return invItem.Item;
            else
                return null;
        }

        internal override uint GetSkillLevel(Skill skill)
        {
            return _skills[(int)skill];     // TODO: augment from item bonuses, buffs, etc.
        }

        internal void SetSkillLevel(Skill skill, ushort level)
        {
            _skills[(int)skill] = level;
        }

        protected override void CalcStatModifiers()
        {
            base.CalcStatModifiers();

            // TODO: calculate item bonuses
        }

        internal override void StartAI()
        {
            base.StartAI();

            // TODO: set up the autocast timer according to amount of spells we posses

            // TODO: add in our spells and special attacks

            // TODO: set guard position?

            if (!_willAggroNPCs)
                _scanTimer.Enabled = false;

            _aggroRange = _aggroRange == 0 ? 70.0f : _aggroRange;
            _assistRange = _assistRange == 0 ? 70.0f : _assistRange;
        }

        /// <summary>Processes various actions and attributes of the NPC, including movement.</summary>
        /// <returns>false if for any reason the Npc needs to be removed from the spawn list.</returns>
        internal override bool Process()
        {
            if (this.IsDePopped) {
                // TODO: if this is a pet, handle things?  Or handle this in a pet class?
                return false;
            }

            // If stunned, can we shake it off yet?
            if (_stunned && _stunTimer.Check()) {
                _stunned = false;
                _stunTimer.Enabled = false;
            }

            // TODO: spells

            if (_ticTimer.Check())
            {
                // Avoid ghosting by sending the position to everyone every so often
                if (_globalPositionUpdateTimer.Check())
                    OnPositionUpdate(new PosUpdateEventArgs(false, false));     // Warps mob to its location.

                // TODO: buffs upkeep

                // TODO: fleeing

                // hp and mana regen    // TODO: handle pet regen in pet class
                int regenBonus = this.Sitting ? 3 : 0;
                int oocRegenAmout = (int)(this.MaxHP * OOC_REGEN_PCT);
                if (this.Wounded) {
                    if (!this.IsEngaged) {    // OOC regen is much, much faster (remember how fast NPCs regenerated?)
                        if (_hpRegen > oocRegenAmout)
                            this.HP += _hpRegen;
                        else
                            this.HP += oocRegenAmout;
                    }
                    else
                        this.HP += _hpRegen;
                }

                if (this.Mana < this.MaxMana)
                    this.Mana += _manaRegen + regenBonus;

                // TODO: adventure shit
            }

            // HP updates
            if (_hpUpdateTimer.Check() && this.IsTargeted) {    // TODO: add check for pet (in derived pet class?)
                if (this.Wounded)
                    OnHPUpdated(new EventArgs());
            }

            if (_stunned || _mezzed)
                return true;    // no further actions will happen so return

            // TODO: enraged

            // TODO: handle assists

            ProcessAI();

            return true;
        }

        /// <summary>Manages NPC movement.</summary>
        /// <remarks>Normally idle movement is at walk speed.</remarks>
        internal override void ProcessAIMovement()
        {
            if (this.WalkSpeed <= 0.0F)
                return;     // unable to walk

            else if (this.Roamer)
            {
                if (_walkTimer.Check())
                {
                    _walkTimerCompleted = true;
                    _walkTimer.Enabled = false;
                }

                if (_gridId > 0 || _curWpIdx == -2)    // Ensure NPC has a grid or is quest controlled (-2 cur wp means quest controlled w/ no grid)
                {
                    if (_walkTimerCompleted)   // is time to pause at wp over?
                    {
                        if (_wanderType == 4 && _curWpIdx == _waypoints.Count)
                            this.DePop();   // wander type of 4 will depop at end of way points
                        else
                        {
                            _walkTimerCompleted = false;

                            if (_curWpIdx == -2)   // if under quest control, now done
                            {
                                _waypoints = null;
                                _curWpIdx = 1;
                            }

                            this.Stance = Stance.Standing;  // Orig emu sets an appearance here - not sure why

                            // TODO: raise waypoint hit event

                            // TODO: open doors near this npc

                            // Setup the next waypoint, if still on normal grid.  A quest that hooked the above event could have modified the grid
                            if (_gridId > 0)
                                CalculateNewWaypoint();
                        }
                    }
                    else if (!_walkTimer.Enabled)   // are we currently moving?
                    {
                        if (_curWaypoint.X == this.X && _curWaypoint.Y == this.Y)   // did we get to our waypoint?
                        {
                            // stop at the wp
                            //_log.DebugFormat("{0} reached waypoint {1} ({2},{3},{4}) on grid {5}", this.Name, _curWpIdx, X, Y, Z, _gridId);
                            
                            PauseAtWaypoint();
                            this.Stance = Stance.Standing;
                            this.IsMoving = false;
                            OnPositionUpdate(new PosUpdateEventArgs(true, false));  // Warps mob to its location.

                            // TODO: wipe Feign Memory at first waypoint?
                        }
                        else    // not there yet, keep moving
                            CalculateNewPosition(_curWaypoint.X, _curWaypoint.Y, _curWaypoint.Z, this.WalkSpeed, true); // not at waypoint yet, so keep moving
                    }
                }
                else if (_gridId < 0)
                {
                    _log.WarnFormat("Why does {0} have a gridId of less than zero ({1})? Quests aren't in yet!", this.Name, _gridId);
                    // this npc under quest control?
                }
            }
            else if (this.Guarding)
            {
                _log.WarnFormat("Why does {0} appear to be guarding? Guarding isn't in yet!", this.Name);
                // TODO: implement guard logic
            }
        }

        private void CalculateNewWaypoint()
        {
            int oldWpIdx = _curWpIdx;
            int ranMax1 = _curWpIdx;
            int ranMax2 = _waypoints.Count - _curWpIdx;
            bool atFirstWP = _curWpIdx == 1;
            bool atLastWP = _curWpIdx == _waypoints.Count;
            Random rand = new Random();

            switch (_wanderType)
            {
                case 0: // Circular
                    if (atLastWP)
                        _curWpIdx = 1;
                    else
                        _curWpIdx++;
                    break;
                case 1: // Random 5
                    ranMax1 = Math.Min(ranMax1, 5);
                    ranMax2 = Math.Min(ranMax2, 5);
                    _curWpIdx = _curWpIdx + rand.Next() % (ranMax1 + 1) - rand.Next() % (ranMax2 + 1);
                    break;
                case 2: // Random
                    _curWpIdx = (rand.Next() % _waypoints.Count) + (rand.Next() % 2);
                    break;
                case 3: // Patrol
                    if (atLastWP)
                        _patrol = true;
                    else if (atFirstWP)
                        _patrol = false;

                    if (_patrol)
                        _curWpIdx--;
                    else
                        _curWpIdx++;
                    break;
                case 4: // Single run
                    _curWpIdx++;
                    break;
                default:
                    throw new IndexOutOfRangeException("Wander Type set to unsupported value (" + _wanderType + ")");
            }

            _tarIdx = 52;   // force new packet to be sent extra 2 times

            if (_curWpIdx != oldWpIdx)  // update the waypoint if it changed
                UpdateWaypoint(_curWpIdx);
        }

        internal void PauseAtWaypoint()
        {
            if (_curWaypoint.Pause == 0)
                _walkTimer.Start(100);
            else
            {
                Random rand = new Random();
                switch (_pauseType)
                {
                    case 0: // random half
                        _walkTimer.Start((_curWaypoint.Pause - rand.Next() % _curWaypoint.Pause / 2) * 1000);
                        break;
                    case 1: // full
                        _walkTimer.Start(_curWaypoint.Pause * 1000);
                        break;
                    case 2: // random full
                        _walkTimer.Start((rand.Next() % _curWaypoint.Pause) * 1000);
                        break;
                }
            }
        }

        protected override bool CanDualWield()
        {
            return this.Level >= 13;    // NPCS get dual wield at level 13
        }

        /// <summary>Determines if we are able to attack.</summary>
        /// <remarks>Doesn't determine ability to attack with a specific item or ability, rather simply general ability to engage in combat.</remarks>
        protected override bool IsAbleToAttack()
        {
            /*  Things which prevent an NPC from attacking:
             *      - casting a spell (unless a bard)
             *      - being stunned or mezzed
             *      - being dead
             *      - being feared (or fleeing)
             *      - using divine aura
             */

            bool retVal = false;

            if (!this.Dead && (!this.IsCasting || this.Class == (byte)CharClasses.Bard) && !_stunned && !_mezzed && !_feared)
                retVal = true;

            // TODO: check for divine aura (not allowed)

            return retVal;
        }

        protected override bool IsAbleToAttack(Mob target, bool spellAttack)
        {
            if (!base.IsAbleToAttack(target, spellAttack))
                return false;

            // TODO: handle attacking another NPC?

            return true;
        }

        internal override bool MeleeAttack(Mob target, bool isPrimaryHand, bool riposte)
        {
            if (!base.MeleeAttack(target, isPrimaryHand, riposte))
                return false;

            FaceMob(target);

            // Are we using a weapon?
            Item weapon = isPrimaryHand ? this.GetEquipment(EquipableType.Primary) : this.GetEquipment(EquipableType.Secondary);
            
            // Not much from the weapon is factored into the attack - just the skill type for the correct animations.
            if (weapon != null) {
                if (!isPrimaryHand && weapon.ItemType == (byte)ItemType.Shield) {
                    _log.DebugFormat("{0} attacking with {1}({2}) in secondary - attack with shield cancelled.", this.Name, weapon.Name, weapon.ItemID);
                    return false;
                }
            }

            // First calculate the skill and animation to use
            Skill attackSkill;
            AttackAnimation attackAnim;
            GetSkillAndAnimForAttack(weapon, isPrimaryHand, out attackSkill, out attackAnim);
            OnAnimation(new AnimationEventArgs(attackAnim));    // Cue the animation
            _log.DebugFormat("NPC attacking {0} with {1} in slot {2} using skill {3}", this.TargetMob.Name, weapon == null ? "Fist" : weapon.Name,
                isPrimaryHand ? "Primary" : "Secondary", attackSkill.ToString("f"));

            // Next figure out the potential damage
            int potDamage = GetWeaponDamage(target, weapon);
            int damage = 0;
            //_log.DebugFormat("Potential damage calculated as {0} for {1}", potDamage, this.Name);

            // If potential damage is more than zero we know it's POSSIBLE to hit the target
            if (potDamage > 0) {
                
                // Elemental & Bane dmg added differently than for PCs - where PCs would use the potential dmg (which has elem & bane included)
                // to figure max dmg, NPCs use the built-in min & max dmg values along with any elem and/or bane dmg.
                int elemBaneDmg = 0;
                if (weapon != null) {
                    if (weapon.BaneDmgBody == (int)target.BodyType)
                        elemBaneDmg += weapon.BaneDmgBodyAmt;
                    if (weapon.BaneDmgRace == target.Race)
                        elemBaneDmg += weapon.BaneDmgRaceAmt;
                    if (weapon.ElemDmgAmt > 0) {
                        // TODO: check for other's resist
                    }
                }

                int minDmg = _minDmg + elemBaneDmg;
                int maxDmg = _maxDmg + elemBaneDmg;

                // Ok, so we know we CAN hit... let's see if we DO hit
                if (target is ZonePlayer && ((ZonePlayer)target).Sitting) {
                    _log.DebugFormat("{0} is sitting - {1} hitting for max damage {2}", ((ZonePlayer)target).Name, this.Name, maxDmg);
                    damage = maxDmg;
                }
                else if (target.TryToHit(this, attackSkill, isPrimaryHand)) {
                    Random rand = new Random();
                    damage = rand.Next(minDmg, maxDmg + 1);
                    //_log.DebugFormat("{4}'s damage calculated to {0} (min {1}, max {2}, pot dmg {3})", damage, minDmg, maxDmg, potDamage, this.Name);

                    // With damage now calculated, see if the mob can avoid and mitigate
                    damage = target.TryToAvoidDamage(this, damage);

                    if (damage > 0) {
                        damage = target.TryToMitigateDamage(this, damage, minDmg);   // wasn't avoided, try to mitigate
                        // TODO: apply any damage bonuses (is this the right term... bonuses?... wasn't that done by now?
                        // TODO: try a critical hit (why are we trying this after the mitigation?)
                    }

                    //_log.DebugFormat("NPC damage after avoidance and mitigation: {0}", damage);
                }
                //else
                //    _log.DebugFormat("{0} missed {1}.", this.Name, this.TargetMob.Name);

                // TODO: cancel a riposte of a riposte
            }
            else
                damage = (int)AttackAvoidanceType.Invulnerable;

            target.Damage(this, damage, 0, attackSkill);    // Send attack damage - even zero and negative damage

            BreakSneakiness();

            // TODO: weapon procs

            // TODO: check if we're getting a riposte back?

            if (damage > 0) {
                // TODO: handle lifetap effects?

                // TODO: trigger defensive procs

                return true;
            }
            else
                return false;
        }

        internal override void Damage(Mob attacker, int dmgAmt, int spellId, Skill attackSkill)
        {
            // TODO: raise attacked script event

            // TODO: handle LDON treasure traps, etc.

            base.Damage(attacker, dmgAmt, spellId, attackSkill);

            // TODO: upstream pet class should send a msg to its owner

            // TODO: upstream pet class should check if it should flee
        }

        protected override void Die(Mob lastAttacker, int damage, int spellId, Skill attackSkill)
        {
            // TODO: kill pet - maybe handle in mob class?

            // TODO: fade buffs

            // TODO: something with LDON treasures

            base.Die(lastAttacker, damage, spellId, attackSkill);
        }

        internal override void InterruptSpell(uint? spellId, MessageStrings? msg)
        {
            // TODO: spell casting AI stop casting stuff

            base.InterruptSpell(spellId, msg);
        }

        internal override bool TrySpellCasting(ushort spellId)
        {
            bool retVal = base.TrySpellCasting(spellId);

            if (!retVal && this.IsCasting)
                SpellCastFinished(false, _castingSpellSlotId);

            return retVal;
        }

        internal override void InterruptSpell(uint? spellId)
        {
            if (this.IsCasting)
                SpellCastFinished(false, _castingSpellSlotId);

            base.InterruptSpell(spellId);
        }

        internal void SpellCastFinished(bool success, ushort slotId)
        {
            // TODO: implement
        }

        internal override void ProcessSpells()
        {
            base.ProcessSpells();

            // TODO: for swarm mobs, see if should depop
        }
    }
}