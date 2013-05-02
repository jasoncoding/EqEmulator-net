using System;
using System.Threading;

using EQEmulator.Servers.ServerTalk;
using EQEmulator.Servers.Internals;

namespace EQEmulator.Servers
{
    public partial class ZoneServer : IZoneService
    {
        public bool BootUp(ushort zoneId, string zoneName, ZoneInstanceType ziType)
        {
            _log.InfoFormat("Booting {0}.", zoneName);

            // Initialize
            if (!Init(zoneId, ziType))
                return false;   // For now, return a bool... zone log will show failure msgs. TODO: Perhaps do a fault here for richer info?

            _loaded = true;
            _log.InfoFormat("Zone {0} loaded.", _zone.ShortName);
            return true;
        }

        /// <summary>Called by world when a client is inbound.</summary>
        /// <returns>0 = success, 1 = max clients reached, 2 = zone locked, 3 = general fubar error.</returns>
        public int ExpectNewClient(int charId, string clientIp, bool isLocal)
        {
            // Check if we were a hair late in disabling shutdown
            lock (_loadedLock)
            {
                if (!_loaded)
                {
                    _log.Info("Whoops, a client came in just after we shut the zone down.");
                    return 3;
                }

                _clientInc = true;  // in case the shutdown callback is waiting on this lock - prevents shutdown whilst we are logging in
            }

            int retVal = 0;
            if (_clients.Count >= this.Zone.MaxClients)
                retVal = 1;
            else if (_locked)
                retVal = 2;
            else
                AddClientAuth(clientIp, isLocal);   // add to expected clients list

            return retVal;
        }
    }
}