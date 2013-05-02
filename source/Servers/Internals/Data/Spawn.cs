using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EQEmulator.Servers.Internals.Data
{
    internal partial class Spawn
    {
        private DateTime _lastSpawned = DateTime.MinValue;
        private bool _alive = false;    // HACK: fix this to something real?

        /// <summary>Set to DateTime.MaxValue to prevent from spawning.</summary>
        public DateTime LastSpawned
        {
            get { return _lastSpawned; }
            set
            {
                _lastSpawned = value;
                _alive = true;          // HACK: fix this to something real?
            }
        }

        internal bool ReadyForRespawn()
        {
            if (!_alive && DateTime.Now.Subtract(_lastSpawned).TotalSeconds > this.RespawnTime)
                return true;
            else
                return false;
        }
    }
}
