using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using log4net;

namespace EQEmulator.Servers.Internals.Data
{
    internal partial class SpawnGroup
    {
        private static readonly ILog _log = LogManager.GetLogger(typeof(SpawnGroup));

        /// <summary>Randomly picks an NPC in this SpawnGroup, respecting spawn limits.  SpawnGroupEntries MUST be sorted.</summary>
        /// <returns>The chose NPC.  Null if no groups or NPCs found for the SpawnGroup.</returns>
        internal Npc PickNPC()
        {
            if (this.SpawnGroupEntries.Count == 0) {
                _log.WarnFormat("No spawn group entries (and thus no NPCs) found for spawn group ({0})", this.SpawnGroupID);
                return null;
            }

            int totalChance = 0;

            // TODO: Check limits for the group

            List<SpawnGroupEntry> possibles = new List<SpawnGroupEntry>(10);
            foreach (SpawnGroupEntry sge in this.SpawnGroupEntries) {
                // TODO: Check limits for the npc

                totalChance += sge.Chance;
                possibles.Add(sge);
            }

            if (totalChance == 0) {
                _log.WarnFormat("Spawn group entries for spawn group {0} have grand total spawn chance of zero!", this.SpawnGroupID);
                return null;
            }

            Random rand = new Random();
            int roll = rand.Next(0, totalChance);
            Npc npc = null;

            foreach (SpawnGroupEntry p in possibles) {
                if (roll < p.Chance) {    // less-than is good because the random number is exclusive of the upper bound
                    npc = p.Npc;
                    break;
                }
                else {
                    roll -= p.Chance;
                    //_log.DebugFormat("Mob {0} didn't make the cut and isn't going to spawn", p.Npc.Name);
                }
            }

            return npc;
        }
    }
}
