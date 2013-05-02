using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using log4net;

namespace EQEmulator.Servers.Internals.Entities
{
    internal class HateEntry
    {
        internal Mob HatedMob { get; set; }
        internal int Damage { get; set; }
        internal int Hate { get; set; }
    }

    internal class HateManager
    {
        protected static readonly ILog _log = LogManager.GetLogger(typeof(Entity));
        private Dictionary<int, HateEntry> _hateList = null;    // keyed by mob id of the hated mob
        private Mob _hater = null;

        internal HateManager(Mob hater)
        {
            _hateList = new Dictionary<int, HateEntry>(20);
            _hater = hater;
        }

        internal int Count
        {
            get { return _hateList.Count; }
        }

        public HateEntry this[int entityID]
        {
            get
            {
                if (_hateList.ContainsKey(entityID))
                    return _hateList[entityID];
                else
                    return null;
            }
        }

        /// <summary>Not thread safe... obtain the list lock before using.</summary>
        internal IEnumerable<HateEntry> AllMobs
        {
            get
            {
                foreach (KeyValuePair<int, HateEntry> kvp in _hateList)
                    yield return kvp.Value;
            }
        }

        internal void AddHate(Mob mob, int hate, int damage)
        {
            lock (((ICollection)_hateList).SyncRoot) {
                if (!_hateList.ContainsKey(mob.ID))
                    _hateList.Add(mob.ID, new HateEntry() { HatedMob = mob, Hate = hate, Damage = damage });
                else {
                    HateEntry hatedMob = _hateList[mob.ID];
                    hatedMob.Hate += hate;
                    hatedMob.Damage += damage;
                }
            }

            //_log.DebugFormat("{0} hate & {1} damage added for {2}.  Total hate: {3}& damage: {4}", hate, damage, mob.Name, this[mob.ID].Hate, this[mob.ID].Damage);
        }

        /// <summary>Removes all hate for the specified mob and picks a new mob to hate.  
        /// To remove incremental amounts, pass a negative value to AddHate().</summary>
        internal void RemoveHate(Mob mob)
        {
            lock (((ICollection)_hateList).SyncRoot)
                _hateList.Remove(mob.ID);

            // TODO: if no longer hating anyone, raise a no longer engaged event?

            if (_hater.TargetMob == mob)
                _hater.TargetMob = this.GetTopHated();
        }

        internal Mob GetTopDamager()
        {
            if (_hateList.Count > 0)
                return _hateList.Values.Aggregate((agg, next) => next.Damage > agg.Damage ? next : agg).HatedMob;
            else
                return null;
        }

        internal Mob GetTopHated()
        {
            if (_hateList.Count > 0)
                return _hateList.Values.Aggregate((agg, next) => next.Hate > agg.Hate ? next : agg).HatedMob;
            else
                return null;
        }

        internal Mob GetClosest()
        {
            return _hateList.Values.Aggregate((agg, next) => next.HatedMob.DistanceNoRootNoZ(_hater) > agg.HatedMob.DistanceNoRootNoZ(_hater) ? next : agg).HatedMob;
        }

        internal void Clear()
        {
            lock (((ICollection)_hateList).SyncRoot) {
                foreach (KeyValuePair<int, HateEntry> kvp in _hateList) {
                    ZonePlayer zp = kvp.Value.HatedMob as ZonePlayer;
                    if (zp != null)
                        zp.RemoveAggro();
                }
            }

            _hateList.Clear();
        }

        internal bool SummonMostHated()
        {
            return false;
            // TODO: summon the most hated mob
        }
    }
}
