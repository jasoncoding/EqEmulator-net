using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.ServiceModel;
using System.Text;

using EQEmulator.Servers.Internals;
using EQEmulator.Servers.Internals.Entities;
using EQEmulator.Servers.Internals.Packets;
using EQEmulator.Servers.Internals.Data;

namespace EQEmulator.Servers
{
    public partial class ZoneServer
    {
        private void SendWeather()
        {
            // TODO: send weather packets to all clients
        }

        /// <summary>Sends packets of spawn information to a single client, max of 100 spawns at a time.</summary>
        private void SendSpawnsBulk(Client client)
        {
            int perPack = _mobMgr.MobCount < 100 ? _mobMgr.MobCount : 100;    // Top out each bulk send at 100
            int totalToSend = _mobMgr.MobCount;
            //_log.DebugFormat("detecting initially that we're going to need to send {0} total spawn", totalMobs);

            int i = 0, t = 0;
            int size = Marshal.SizeOf(typeof(Internals.Packets.Spawn));
            byte[] spawnsBytes = new byte[perPack * size];
            EQRawApplicationPacket spawnsPack;

            foreach (Mob mob in _mobMgr.AllMobs)    // Piss on locking it for this early operation
            {
                if (mob.IsInZone())
                {
                    if (mob is ZonePlayer && ((ZonePlayer)mob).IsGMHidden)
                    {
                        totalToSend--;
                        continue;
                    }

                    if (i == perPack)
                    {
                        // Send packet
                        spawnsPack = new EQRawApplicationPacket(AppOpCode.ZoneSpawns, client.IPEndPoint, spawnsBytes);
                        client.SendApplicationPacket(spawnsPack);

                        perPack = (totalToSend - t) < 100 ? totalToSend - t : 100;
                        spawnsBytes = new byte[perPack * size];
                        //_log.DebugFormat("created spawn array for {0} spawn.  t at {1}", perPack, t);
                        i = 0;
                    }

                    // append spawn bytes
                    Internals.Packets.Spawn s = mob.GetSpawn();
                    byte[] spawnBytes = Utility.SerializeStruct<Internals.Packets.Spawn>(s);
                    Buffer.BlockCopy(spawnBytes, 0, spawnsBytes, i * size, size);
                    i++;
                    t++;    // bump num of total spawns sent
                }
                else
                    totalToSend--;
            }

            if (i != 0) // Don't send anything if there is nothing to send
            {
                Array.Resize(ref spawnsBytes, i * size);   // Make sure we don't send empty spawns (might have had omissions)

                // Send packet
                spawnsPack = new EQRawApplicationPacket(AppOpCode.ZoneSpawns, client.IPEndPoint, spawnsBytes);
                //_log.DebugFormat("Sending Bulk Spawns Dump: {0}", BitConverter.ToString(spawnsBytes));
                client.SendApplicationPacket(spawnsPack);
            }

            //_log.DebugFormat("sent {0} total spawn", t);
        }

        private void SendCorpsesBulk(Client client)
        {
            int perPack = _mobMgr.CorpseCount < 100 ? _mobMgr.CorpseCount : 100;    // Top out each bulk send at 100
            int totalToSend = _mobMgr.CorpseCount;

            //_log.DebugFormat("Detecting initially that we're going to need to send {0} total corpses", totalToSend);

            int i = 0, t = 0;
            int size = Marshal.SizeOf(typeof(Internals.Packets.Spawn));
            byte[] spawnsBytes = new byte[perPack * size];
            EQRawApplicationPacket spawnsPack;

            foreach (Corpse mob in _mobMgr.Corpses)    // Piss on locking it for this early operation
            {
                if (i == perPack) {
                    // Send packet
                    spawnsPack = new EQRawApplicationPacket(AppOpCode.ZoneSpawns, client.IPEndPoint, spawnsBytes);
                    client.SendApplicationPacket(spawnsPack);

                    perPack = (totalToSend - t) < 100 ? totalToSend - t : 100;
                    spawnsBytes = new byte[perPack * size];
                    i = 0;
                }

                // append spawn bytes
                Internals.Packets.Spawn s = mob.GetSpawn();
                byte[] spawnBytes = Utility.SerializeStruct<Internals.Packets.Spawn>(s);
                Buffer.BlockCopy(spawnBytes, 0, spawnsBytes, i * size, size);
                i++;
                t++;    // bump num of total spawns sent
            }

            if (i != 0) // Don't send anything if there is nothing to send
            {
                Array.Resize(ref spawnsBytes, i * size);   // Make sure we don't send empty spawns (might have had omissions)

                // Send packet
                spawnsPack = new EQRawApplicationPacket(AppOpCode.ZoneSpawns, client.IPEndPoint, spawnsBytes);
                //_log.DebugFormat("Sending Bulk Spawns Dump: {0}", BitConverter.ToString(spawnsBytes));
                client.SendApplicationPacket(spawnsPack);
            }

            //_log.DebugFormat("sent {0} total spawn", t);
        }

        private void SendZoneCancel(ZoneChange zc, Client client)
        {
            // send client right back to where they were - TODO: could this be improved?
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(client.ZonePlayer.Name), 0, zc.CharName, 0, client.ZonePlayer.Name.Length);
            zc.ZoneID = (ushort)this.Zone.ZoneID;
            zc.Success = 1;
            EQApplicationPacket<ZoneChange> zcPack = new EQApplicationPacket<ZoneChange>(AppOpCode.ZoneChange, zc);
            client.SendApplicationPacket(zcPack);

            client.ZonePlayer.ZoneMode = ZoneMode.Unsolicited;
        }

        private void SendZoneError(ZoneChange zc, ZoneError errCode, Client client)
        {
            _log.InfoFormat("Zone {0} is not available for {1} because it wasn't found, insufficient flags, insufficient level or locked zone", zc.ZoneID, client.ZonePlayer.Name);

            ZoneChange zcOut = new ZoneChange(client.ZonePlayer.Name);
            zcOut.ZoneID = zc.ZoneID;
            zcOut.Success = (int)errCode;
            EQApplicationPacket<ZoneChange> zcPack = new EQApplicationPacket<ZoneChange>(AppOpCode.ZoneChange, zcOut);
            client.SendApplicationPacket(zcPack);
        }

        internal void SendLogoutPackets(Client client)
        {
            CancelTrade ct = new CancelTrade();
            ct.FromID = (uint)client.ZonePlayer.ID;
            ct.Action = 7;  // TODO: figure out wtf 7 is and constant-ize it (looks like a group action of "update")
            EQApplicationPacket<CancelTrade> ctPack = new EQApplicationPacket<CancelTrade>(AppOpCode.CancelTrade, ct);
            client.SendApplicationPacket(ctPack);

            EQRawApplicationPacket plrPack = new EQRawApplicationPacket(AppOpCode.PreLogoutReply, client.IPEndPoint, null);
            client.SendApplicationPacket(plrPack);
        }

        internal void SendStancePacket(SpawnAppearanceType appearType, Mob mob, bool wholeZone, bool ignoreSelf)
        {
            this.SendStancePacket(appearType, mob, (short)mob.Stance, wholeZone, ignoreSelf);
        }

        internal void SendStancePacket(SpawnAppearanceType appearType, Mob mob, short stanceValue, bool wholeZone, bool ignoreSelf)
        {
            this.SendStancePacket(appearType, mob, stanceValue, wholeZone, ignoreSelf, null);
        }

        internal void SendStancePacket(SpawnAppearanceType appearType, Mob mob, short stanceValue, bool wholeZone, bool ignoreSelf, Client target)
        {
            SpawnAppearance sa = new SpawnAppearance((ushort)mob.ID, (ushort)appearType, (uint)stanceValue);
            EQApplicationPacket<SpawnAppearance> saPack = new EQApplicationPacket<SpawnAppearance>(AppOpCode.SpawnAppearance, sa);

            if (wholeZone)
                this.QueuePacketToClients(mob, saPack, ignoreSelf);
            else if (target != null)
                QueuePacketToClient(target, saPack, false, ZoneConnectionState.Connected);
            else if (mob is ZonePlayer)
                QueuePacketToClient(((ZonePlayer)mob).Client, saPack, false, ZoneConnectionState.Connected);
        }

        /// <summary>Sends the complete inventory to the client.  Used during login.</summary>
        internal void SendInventoryBulk(Client client, Character toon)
        {
            // First let's see if we need to clear any norent items
            bool noRentExpired = DateTime.Now.Subtract(toon.LastSeenDate ?? DateTime.MinValue).TotalSeconds >= Item.NORENT_EXPIRATION;
            if (noRentExpired)
                client.ZonePlayer.InvMgr.ClearNoRent();

            StringBuilder serItems = new StringBuilder();

            // Old emu checks for validity of an item for the slot here, not sure if that's necessary... validation is when placing in a slot
            foreach (InventoryItem invItem in client.ZonePlayer.InvMgr.PersonalSlotItems()) {
                //_log.DebugFormat("During bulk inv send, found {0} in personal inv items at slot {1} ====> {2}", invItem.Item.Name, invItem.SlotID, invItem.Serialize());
                serItems = serItems.Append(invItem.Serialize() + '\0');
            }

            foreach (InventoryItem invItem in client.ZonePlayer.InvMgr.BankSlotItems()) {
                //_log.DebugFormat("During bulk inv send, found {0} in bank items at slot {1}.", invItem.Item.Name, invItem.SlotID);
                serItems = serItems.Append(invItem.Serialize() + '\0');
            }

            foreach (InventoryItem invItem in client.ZonePlayer.InvMgr.SharedBankSlotItems()) {
                //_log.DebugFormat("During bulk inv send, found {0} in shared bank items at slot {1}.", invItem.Item.Name, invItem.SlotID);
                serItems = serItems.Append(invItem.Serialize() + '\0');
            }

            //_log.DebugFormat("Serialized Inventory Dump: {0}", serItems.ToString());
            EQRawApplicationPacket invPack = new EQRawApplicationPacket(AppOpCode.CharInventory, client.IPEndPoint, Encoding.ASCII.GetBytes(serItems.ToString()));
            client.SendApplicationPacket(invPack);
        }

        internal static void SendItemPacket(Client client, InventoryItem invItem, ItemPacketType packetType)
        {
            SendItemPacket(client, invItem, invItem.SlotID, packetType);
        }

        internal static void SendItemPacket(Client client, InventoryItem invItem, int slotId, ItemPacketType packetType)
        {
            invItem.SlotID = slotId;
            _log.DebugFormat("Sending item packet for {0}", invItem);
            //_log.DebugFormat("Serialized inv item: {0}", invItem.Serialize());
            ItemPacket item = new ItemPacket(packetType, invItem.Serialize());
            AppOpCode opCode = packetType == ItemPacketType.ViewLink ? AppOpCode.ItemLinkResponse : AppOpCode.ItemPacket;
            //_log.DebugFormat("Serialized Item packet: {0}", BitConverter.ToString(item.Serialize()));
            EQRawApplicationPacket itemPack = new EQRawApplicationPacket(opCode, client.IPEndPoint, item.Serialize());
            client.SendApplicationPacket(itemPack);
        }

        internal void SendDespawnPacket(Mob despawner)
        {
            EQRawApplicationPacket dsPack = new EQRawApplicationPacket(AppOpCode.DeleteSpawn, null, BitConverter.GetBytes(despawner.ID));
            QueuePacketToClients(despawner, dsPack, true);
        }

        internal void SendLootRequestErrorPacket(Client client, byte response)
        {
            MoneyOnCorpse moc = new MoneyOnCorpse() { Response = response };
            EQApplicationPacket<MoneyOnCorpse> mocPack = new EQApplicationPacket<MoneyOnCorpse>(AppOpCode.MoneyOnCorpse, moc);
            client.SendApplicationPacket(mocPack);
        }

        internal void SendEndLootComplete(Client client)
        {
            EQRawApplicationPacket lcPack = new EQRawApplicationPacket(AppOpCode.LootComplete, client.IPEndPoint, null);
            QueuePacketToClient(client, lcPack, true, ZoneConnectionState.All);
        }

        #region World Service Calls
        internal void UpdateWorldWho(Client client)
        {
            try
            {
                //string clientAddr = GetClientAddress(client, WorldServer.ServerConfig.i

                WorldSvc.UpdateWho(client.IPEndPoint.Address.ToString(), client.ZonePlayer.ID, client.ZonePlayer.Name, client.ZonePlayer.IsGM,
                    client.ZonePlayer.IsGM, this.Zone.ZoneID, (byte)client.ZonePlayer.Race, (byte)client.ZonePlayer.Class, (byte)client.ZonePlayer.Level,
                    client.ZonePlayer.Anonymous == 1, (byte)client.ZonePlayer.GuildId, client.ZonePlayer.IsLFG);
            }
            catch (CommunicationException ce)    // Specific fault handlers go before the CommunicationException handler
            {
                _log.Error("Attempt to update world's who list errored out.", ce);
                _worldSvcClientChannel.Abort();
            }
            catch (TimeoutException te)
            {
                _log.Error("Attempt to update world's who list timed out.", te);
                _worldSvcClientChannel.Abort();
            }
            catch (Exception)
            {
                _worldSvcClientChannel.Abort();
                throw;
            }
        }

        private void RemoveWorldWho(string clientIp)
        {
            try
            {
                WorldSvc.ClientLogged(clientIp);
            }
            catch (CommunicationException ce)    // Specific fault handlers go before the CommunicationException handler
            {
                _log.Error("Attempt to update world's who list errored out.", ce);
                _worldSvcClientChannel.Abort();
            }
            catch (TimeoutException te)
            {
                _log.Error("Attempt to update world's who list timed out.", te);
                _worldSvcClientChannel.Abort();
            }
            catch (Exception)
            {
                _worldSvcClientChannel.Abort();
                throw;
            }
        }

        private void TellWorldWeUnloaded(int port)
        {
            try
            {
                WorldSvc.ZoneUnloaded(port);
            }
            catch (CommunicationException ce)    // Specific fault handlers go before the CommunicationException handler
            {
                _log.Error("Attempt to tell world we unloaded errored out.", ce);
                _worldSvcClientChannel.Abort();
            }
            catch (TimeoutException te)
            {
                _log.Error("Attempt to tell world we unloaded timed out.", te);
                _worldSvcClientChannel.Abort();
            }
            catch (Exception)
            {
                _worldSvcClientChannel.Abort();
                throw;
            }
        }
        #endregion
    }
}
