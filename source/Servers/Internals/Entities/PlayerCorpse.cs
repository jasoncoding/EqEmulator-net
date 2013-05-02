using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EQEmulator.Servers.Internals.Data;

namespace EQEmulator.Servers.Internals.Entities
{
    internal class PlayerCorpse : Corpse
    {
        internal const int DECAYMS_EMPTY_PC_CORPSE = 1200000;  // 20 min for a naked corpse (that hasn't been looted by the PC)
        internal const int PC_CORPSE_LEVEL_LOW = 10;
        internal const int PC_CORPSE_LEVEL_MID = 50;
        internal const int DECAYMS_PC_CORPSE_LOW = 3600000;    // 10th level or less (1 hour)
        internal const int DECAYMS_PC_CORPSE_MID = 172800000; // 2 days
        internal const int DECAYMS_PC_CORPSE_HIGH = 604800000; // 1 week
        private const int GRAVEYARD_TIMEOUT_MS = 1200000;   // ms until player corpse is moved to a zone's graveyard, if the zone has one

        private int _charId = 0, _xpLoss = 0;
        private bool _rezzed = false;
        private SimpleTimer _graveyardTimer;

        internal PlayerCorpse(ZonePlayer zp, int xpLoss, bool zoneHasGraveyard)
            : base(zp, null)
        {
            _charId = zp.CharId;
            _hairColor = zp.HairColor;
            _beardColor = zp.BeardColor;
            _eyeColor1 = zp.EyeColor1;
            _hairStyle = zp.HairStyle;
            _luclinFace = zp.Face;
            _beard = zp.Beard;
            _xpLoss = xpLoss;

            // TODO: don't move items if PC had "become" an NPC?
            _lootItems = zp.InvMgr.AllPersonalItems().ToList<InventoryItem>();
            _lootItems.AddRange(zp.InvMgr.CursorItems());
            _lootItems.RemoveAll(ii => ii.Item.IsNoRent);

            if (IsEmpty())
                _decayTimer.Start(DECAYMS_EMPTY_PC_CORPSE);
            else {
                if (zp.Level <= PC_CORPSE_LEVEL_LOW)
                    _decayTimer = new SimpleTimer(DECAYMS_PC_CORPSE_LOW);
                else if (zp.Level <= PC_CORPSE_LEVEL_MID)
                    _decayTimer = new SimpleTimer(DECAYMS_PC_CORPSE_MID);
                else
                    _decayTimer = new SimpleTimer(DECAYMS_PC_CORPSE_HIGH);
            }

            if (zoneHasGraveyard)
                _graveyardTimer = new SimpleTimer(GRAVEYARD_TIMEOUT_MS);

            // TODO: get item tints and set for their corpse

            // TODO: soulbound items not switched to corpse, but put in normal slots on PC, not in a bag or what have you

            _allowedLooters.Add(zp.CharId);
            Save();
        }

        public bool Rezzed
        {
            get { return _rezzed; }
            set { _rezzed = value; } 
        }

        /// <summary>Saves player corpses to the database.</summary>
        internal override void Save()
        {
            if (!_changed)
                return;
            else {
                // TODO: save the corpse to the db
            }
        }

        internal override void DePop()
        {
            // TODO: remove corpse from the database (if it was saved there)

            base.DePop();
        }

        internal override EQEmulator.Servers.Internals.Packets.Spawn GetSpawn()
        {
            Packets.Spawn s = base.GetSpawn();
            s.NPC = 3;
            return s;
        }
    }
}
