using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using EQEmulator.Servers.ServerTalk;
using EQEmulator.Servers.Internals;
using EQEmulator.Servers.Internals.Data;

namespace EQEmulator.Servers
{
    public partial class WorldServer : IWorldService
    {
        /// <summary>Called by the Login Server when a client is inbound.</summary>
        /// <param name="clientIp">If local the netbios name of the client, else the IP address of the client.</param>
        /// <param name="isLocal">Whether the client is in the local net or is remote.</param>
        public void ExpectNewClient(string clientIp, bool isLocal)
        {
            //_log.Debug("Recv ExpectNewClient call.");

            // add to expected clients list
            _authClientsLock.EnterWriteLock();
            try
            {
                // Don't add if already in expected list (might have crashed out and coming back in)
                if (!_authClients.ContainsKey(clientIp))
                {
                    _authClients.Add(clientIp, new ClientWho(clientIp, isLocal));
                    //if (_expClients.Count == 1)
                    //    _expClientsStaleRemoval.Change(CLIENT_AUTH_CLEANUP_INTERVAL, CLIENT_AUTH_CLEANUP_INTERVAL);    // only start the timer if this is the first one we've added
                }
                //else
                //    _authClients[clientIp].TimeAdded.AddMinutes(1);  // bump thier auth window
            }
            catch (Exception e)
            {
                _log.Error("Error trying to add an expected client.", e);
            }
            finally
            {
                _authClientsLock.ExitWriteLock();
            }
        }

        /// <summary>Called by a Zone Server when something significant happens that World should be tracking for the /who commands.</summary>
        public void UpdateWho(string clientIp, int charId, string charName, bool gm, bool admin, int zoneId, byte race, byte charClass,
            byte level, bool anonymous, int guildId, bool lfg)
        {
            _authClientsLock.EnterWriteLock();
            try
            {
                ClientWho cw = null;
                if (!_authClients.TryGetValue(clientIp, out cw))
                    _log.ErrorFormat("Attempted to update who info for client {0} ({1}) that is not being tracked.", charName, clientIp);
                else
                {
                    cw.GM = gm;
                    cw.ZoneId = zoneId;
                    cw.Race = race;
                    cw.Class = charClass;
                    cw.Level = level;
                    cw.Anonymous = anonymous;
                    cw.GuildId = guildId;
                    cw.LFG = lfg;
                }
            }
            catch (Exception e)
            {
                _log.Error("Error updating who information.", e);
            }
            finally
            {
                _authClientsLock.ExitWriteLock();
            }

            _log.DebugFormat("Who info updated for {0}", charName);
        }

        /// <summary>Called by a Zone Server when a client drops or otherwise logs out.  Removes client auth info as well as who info.</summary>
        public void ClientLogged(string clientIp)
        {
            _authClientsLock.EnterWriteLock();
            try
            {
                if (!_authClients.Remove(clientIp))
                    _log.WarnFormat("Attempted to remove client info for client {0} that is not being tracked.", clientIp);
                else
                {
                    _log.DebugFormat("Client info removed for {0}", clientIp);

                    // TODO: call login to decrement its counter
                }
            }
            catch (Exception e)
            {
                _log.Error("Error removing client information.", e);
            }
            finally
            {
                _authClientsLock.ExitWriteLock();
            }
        }

        /// <summary>Handles a client zoning from one zone server to another.  This is sent from a zone server.</summary>
        /// <returns>0 = success, 1 = max clients reached, 2 = zone locked, 3 = general fubar error.</returns>
        public int ZoneToZone(ZoneToZone ztz)
        {
            _log.InfoFormat("ZoneToZone request for {0}: current zone is {1}, target zone is {2}", ztz.CharName, ztz.CurrentZoneId, ztz.RequestedZoneId);

            // TODO: add checks for appropriate ignoring of zone restrictions and the bypass of ExpectNewClient calls this would require

            // Try to fetch the zone process
            ZoneProcess zp = GetZoneProcess(ztz.RequestedZoneId);
            if (zp == null)
                return 3;   // couldn't boot the zone for some reason, check the logs TODO: return different codes for No server free, zone full, etc.

            int retCode = zp.ZoneService.ExpectNewClient(ztz.CharId, ztz.ClientIp, ztz.IsLocalNet);
            if (retCode > 0)
            {
                _log.ErrorFormat("Attempt to zone to {0} failed with error code {1}", ztz.RequestedZoneId, retCode);
                return retCode;
            }

            _log.InfoFormat("ExpectNewClient successful for zone {0} during a ZoneToZone request", ztz.RequestedZoneId);
            
            // Update the who info for this client
            _authClientsLock.EnterWriteLock();
            try
            {
                ClientWho cw = _authClients[ztz.ClientIp];
                cw.ZoneId = ztz.RequestedZoneId;
            }
            catch (Exception e)
            {
                _log.Error("Error updating who information.", e);
            }
            finally
            {
                _authClientsLock.ExitWriteLock();
            }

            _log.DebugFormat("Who info updated for {0}", ztz.CharName);

            return 0;
        }

        /// <summary>Called by a dynamic zone server when it unloads its zone.</summary>
        /// <param name="port">Port number of the zone server.</param>
        public void ZoneUnloaded(int port)
        {
            ZoneProcess zp = null;

            lock (((System.Collections.ICollection)_zoneProcesses).SyncRoot)
            {
                _zoneProcesses.TryGetValue(port, out zp);

                if (zp == null)
                {
                    _log.ErrorFormat("Zone told us to unload a zone by a port number that we're not tracking ({0}).", port);
                    return;
                }

                zp.ZoneId = null;
            }

            _log.InfoFormat("Recycling port {0} due to release by a dynamic zone server ({1}).", port, zp.ZoneId);
        }

        public short? GetSkillCap(byte skillId, byte classId, byte level)
        {
            SkillCap skillCap = _skillCaps.FirstOrDefault(sc => sc.SkillID == skillId && sc.Class == classId && sc.Level == level);
            if (skillCap != null)
                return skillCap.Cap;
            else
                return null;
        }

        public Spell GetSpellById(uint spellId)
        {
            return _spells.SingleOrDefault(s => s.SpellID == spellId);
        }

        public int GetMaxSpellId()
        {
            if (_spells != null)
                return _spells.Count;
            else
                return 0;
        }
    }
}
