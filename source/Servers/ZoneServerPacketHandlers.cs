using System;
using System.Data.Linq;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using EQEmulator.Servers.Internals;
using EQEmulator.Servers.Internals.Data;
using EQEmulator.Servers.Internals.Entities;
using EQEmulator.Servers.Internals.Packets;
using System.Collections.Generic;
using System.Threading;

namespace EQEmulator.Servers
{
    public partial class ZoneServer
    {
        #region ConnectING OpCode Handlers
        internal void HandleZoneEntry(EQRawApplicationPacket packet, Client client)
        {
            // disable shut down timer in case the player zoned to the same zone and thus bypassed the normal routines
            _shutdownTimer.Change(Timeout.Infinite, Timeout.Infinite);  // TODO: add logic for static and instanced zones

            EQApplicationPacket<ClientZoneEntry> czePacket = new EQApplicationPacket<ClientZoneEntry>(packet);
            //_log.Debug(czePacket.DumpStruct());

            if (!_authClients.ContainsKey(client.IPEndPoint.Address.ToString()))
            {
                _log.InfoFormat("Client at {0} attempted to connect but was not found among expected clients... kicking.", client.IPEndPoint.ToString());
                client.Close();     // No need for a graceful kick that handles removal of groups, etc... client just got here. (or did he?)
                return;
            }

            // Get the client's character info
            string charName = Encoding.ASCII.GetString(czePacket.PacketStruct.CharName);
            charName = charName.Substring(0, charName.IndexOf('\0'));   // damn char name is probably only 28 chars long
            Character toon = null;
            DataLoadOptions dlo = new DataLoadOptions();
            dlo.LoadWith<Character>(c => c.Account);
            dlo.LoadWith<Character>(c => c.Zone);
            dlo.LoadWith<Character>(c => c.InventoryItems);
            dlo.LoadWith<InventoryItem>(ii => ii.Item);
            dlo.LoadWith<Character>(c => c.MemorizedSpells);
            dlo.LoadWith<MemorizedSpell>(ms => ms.Spell);
            dlo.LoadWith<Character>(c => c.ScribedSpells);
            // TODO: load zone flags once in

            using (EmuDataContext dbCtx = new EmuDataContext())
            {
                dbCtx.ObjectTrackingEnabled = false;
                dbCtx.LoadOptions = dlo;
                toon = dbCtx.Characters.SingleOrDefault(c => c.Name == charName);
            }

            if (toon == null) {
                _log.ErrorFormat("Couldn't find character {0} in database after a good auth in zone.", charName);
                client.Close();     // Again, no need for a graceful kick (or is there?)
                return;
            }

            client.ZonePlayer = new ZonePlayer(GetNewEntityId(), toon, Zone.ZoneID, client);   // Initialize the client's player obj
            AddMob(client.ZonePlayer);  // add client to the zone's list of mobs

            // Send player profile packet
            EQApplicationPacket<PlayerProfile> ppPacket = new EQApplicationPacket<PlayerProfile>(AppOpCode.PlayerProfile, client.ZonePlayer.PlayerProfile);
            //_log.DebugFormat("structure size: {0}", System.Runtime.InteropServices.Marshal.SizeOf(ppPacket.PacketStruct));
            //_log.Debug(ppPacket.DumpStruct());
            //_log.DebugFormat("Bytes Sending Dump: {0}", BitConverter.ToString(ppPacket.RawPacketData));
            client.SendApplicationPacket(ppPacket);
            //_log.DebugFormat("Recv ZoneEntry for {0}.  Auth passed, char info found and loaded.", charName);
            _log.DebugFormat("Sent {0} hp to client on initial zone in.", client.ZonePlayer.PlayerProfile.HP);

            // TODO: pets?

            // Send Zone Entry Packet
            Internals.Packets.Spawn spawnStruct = client.ZonePlayer.GetSpawn();
            spawnStruct.ZPos += 6;      // adding 6 to Z height seems to help spawning too low
            spawnStruct.CurHpPct = 1;   // Set a couple of properties that
            spawnStruct.NPC = 0;        // make sense for a zone entry packet

            EQApplicationPacket<Internals.Packets.Spawn> spawnPacket = new EQApplicationPacket<Internals.Packets.Spawn>(AppOpCode.ZoneEntry, spawnStruct);
            //_log.Debug(spawnPacket.DumpStruct());
            client.SendApplicationPacket(spawnPacket);

            // TODO: send client packets about traders
            SendSpawnsBulk(client);
            SendCorpsesBulk(client);

            // Send Time of Day Packet TODO: implement a real time of day system
            TimeOfDay todStruct = new TimeOfDay();
            todStruct.Year = 3190;
            todStruct.Month = (byte)2;
            todStruct.Day = (byte)2;
            todStruct.Hour = (byte)2;
            todStruct.Minute = (byte)2;
            EQApplicationPacket<TimeOfDay> todPacket = new EQApplicationPacket<TimeOfDay>(AppOpCode.TimeOfDay, todStruct);
            //_log.Debug(todPacket.DumpStruct());
            client.SendApplicationPacket(todPacket);

            // TODO: Tribute packets & timer
            TributeInfo tiStruct = new TributeInfo();
            tiStruct.Active = (int)client.ZonePlayer.PlayerProfile.TributeActive;
            tiStruct.TributeMasterID = (int)client.ZonePlayer.TributeMasterId;      // no one seems to know what this does
            EQApplicationPacket<TributeInfo> tiPacket = new EQApplicationPacket<TributeInfo>(AppOpCode.TributeUpdate, tiStruct);
            //_log.Debug(tiPacket.DumpStruct());
            client.SendApplicationPacket(tiPacket);

            EQRawApplicationPacket ttPacket = new EQRawApplicationPacket(AppOpCode.TributeTimer, client.IPEndPoint, BitConverter.GetBytes(Tribute.TRIBUTE_DURATION));
            client.SendApplicationPacket(ttPacket);

            SendInventoryBulk(client, toon);    // Send character inventory packets
            // TODO: send inventory packets about items on cursor (except for the first item, which is in the above bulk send)

            // TODO: Task packets

            // Send weather packet - client keys off this to advance to the next stage (the sending of ReqNewZone)
            Weather weathStruct = new Weather();
            weathStruct.Val1 = 0x000000FF;
            weathStruct.Type = 0x00;    // TODO: once weather is fully implemented, change
            EQApplicationPacket<Weather> weathPacket = new EQApplicationPacket<Weather>(AppOpCode.Weather, weathStruct);
            //_log.Debug(weathPacket.DumpStruct());
            client.SendApplicationPacket(weathPacket);

            //_log.DebugFormat("profile, zone entry, time of day and weather packets all sent for {0} {1}.", client.ZonePlayer.Name, client.ZonePlayer.Surname);
        }

        internal void BlackHoleHandler(EQRawApplicationPacket packet, Client client)
        {
            return;
        }

        /// <summary>Sent after Zone Entry</summary>
        internal void HandleReqNewZone(EQRawApplicationPacket packet, Client client)
        {
            // Send New Zone Packet
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(client.ZonePlayer.Name), 0, this.Zone.NewZoneStruct.CharName, 0, client.ZonePlayer.Name.Length);
            EQApplicationPacket<NewZone> newZonePacket = new EQApplicationPacket<NewZone>(AppOpCode.NewZone, this.Zone.NewZoneStruct);
            client.SendApplicationPacket(newZonePacket);

            // Send Titles Packet - seems there is some consternation over why they are sent here...
            // TODO: implement titles manager
            EQRawApplicationPacket titlesPacket = new EQRawApplicationPacket(AppOpCode.CustomTitles, client.IPEndPoint, new byte[] { 0x00, 0x00, 0x00, 0x00 });
            client.SendApplicationPacket(titlesPacket);
        }

        /// <summary>Evidently each zone manages all the different AAs.  Hopefully we can omit these for now.</summary>
        internal void HandleSendAATable(EQRawApplicationPacket packet, Client client)
        {
            // TODO: load and send AA data that will be populated in zone.Init()
        }

        internal void HandleUpdateAA(EQRawApplicationPacket packet, Client client)
        {
            // Sends much the same AA data that is present in the player profile
            byte[] aaBlob = new byte[8 * Character.MAX_AA];     // 2 ints for each AA
            Buffer.BlockCopy(client.ZonePlayer.PlayerProfile.AABlob, 0, aaBlob, 0, Character.MAX_AA);   // struct data is in ints for convenience
            EQRawApplicationPacket aaPacket = new EQRawApplicationPacket(AppOpCode.RespondAA, client.IPEndPoint, aaBlob);
            client.SendApplicationPacket(aaPacket);
        }

        /// <summary>Handles request from client to send regular tributes (non-guild).  Hopefully we can omit these for now.</summary>
        internal void HandleSendTributes(EQRawApplicationPacket packet, Client client)
        {
            // TODO: load and send tribute data
        }

        /// <summary>Handles request from client to send guild tributes.  Hopefully we can omit these for now.</summary>
        internal void HandleSendGuildTributes(EQRawApplicationPacket packet, Client client)
        {
            // TODO: load and send guild tribute data
        }

        internal void HandleReqClientSpawn(EQRawApplicationPacket packet, Client client)
        {
            // Send Zone Doors
            if (Zone.Doors.Count > 0)
            {
                int structSize = Marshal.SizeOf(typeof(ZoneDoor));
                byte[] zdBytes = new byte[Zone.Doors.Count * structSize];

                ZoneDoor ds;
                byte[] dsBytes = null;
                for (int i = 0; i < this.Zone.Doors.Count; i++)
                {
                    ds = this.Zone.Doors[i].GetDoorStruct();
                    dsBytes = new byte[Marshal.SizeOf(ds)];

                    GCHandle handle = GCHandle.Alloc(dsBytes, GCHandleType.Pinned);
                    IntPtr buffer = handle.AddrOfPinnedObject();
                    Marshal.StructureToPtr(ds, buffer, false);
                    handle.Free();

                    Buffer.BlockCopy(dsBytes, 0, zdBytes, i * structSize, structSize);
                }

                EQRawApplicationPacket dPack = new EQRawApplicationPacket(AppOpCode.SpawnDoor, client.IPEndPoint, zdBytes);
                client.SendApplicationPacket(dPack);
            }

            // Send Zone Objects
            // TODO: load and send objects

            // Send Zone Points
            if (this.Zone.ZonePoints.Count > 0)
            {
                //ZonePoints zps = new ZonePoints(Zone.ZonePoints.Count);

                //for (int i = 0; i < this.Zone.ZonePoints.Count; i++)
                //{
                //    zps.ZonePointEntry[i].Iterator = this.Zone.ZonePoints[i].Number;
                //    zps.ZonePointEntry[i].X = this.Zone.ZonePoints[i].TargetX;
                //    zps.ZonePointEntry[i].Y = this.Zone.ZonePoints[i].TargetY;
                //    zps.ZonePointEntry[i].Z = this.Zone.ZonePoints[i].TargetZ;
                //    zps.ZonePointEntry[i].Heading = this.Zone.ZonePoints[i].TargetHeading;
                //    zps.ZonePointEntry[i].ZoneId = (short)this.Zone.ZonePoints[i].TargetZoneID;
                //    // Instance member would be populated here for instanced zones
                //}

                // This is going to be ugly... can't even use a structure at this point TODO: fix in the future
                int size = 4 + ((Zone.ZonePoints.Count + 1) * 24);
                byte[] zpBytes = new byte[size];
                Buffer.BlockCopy(BitConverter.GetBytes(Zone.ZonePoints.Count), 0, zpBytes, 0, 4);  // Set the count

                ZonePointEntry zpe;
                byte[] zpeBytes = null;
                for (int i = 0; i < this.Zone.ZonePoints.Count; i++)
                {
                    zpe = new ZonePointEntry();
                    zpe.Iterator = this.Zone.ZonePoints[i].Number;
                    zpe.X = this.Zone.ZonePoints[i].TargetX;
                    zpe.Y = this.Zone.ZonePoints[i].TargetY;
                    zpe.Z = this.Zone.ZonePoints[i].TargetZ;
                    zpe.Heading = this.Zone.ZonePoints[i].TargetHeading;
                    zpe.ZoneId = (short)this.Zone.ZonePoints[i].TargetZoneID;
                    // Instance member would be populated here for instanced zones

                    zpeBytes = new byte[Marshal.SizeOf(zpe)];
                    unsafe
                    {
                        fixed (byte* ptr = zpeBytes)
                        {
                            *((ZonePointEntry*)ptr) = zpe;
                        }
                    }
                    Buffer.BlockCopy(zpeBytes, 0, zpBytes, 4 + (i * 24), 24);
                }

                EQRawApplicationPacket zpPack = new EQRawApplicationPacket(AppOpCode.SendZonepoints, client.IPEndPoint, zpBytes);
                client.SendApplicationPacket(zpPack);
            }

            // Send AAStats
            EQRawApplicationPacket aaStatsPacket = new EQRawApplicationPacket(AppOpCode.SendAAStats, client.IPEndPoint, null);
            client.SendApplicationPacket(aaStatsPacket);

            // Send SendExpZoneIn - tells client they can continue - we're done for now
            EQRawApplicationPacket sezPacket = new EQRawApplicationPacket(AppOpCode.SendExpZonein, client.IPEndPoint, null);
            client.SendApplicationPacket(sezPacket);

            // TODO: Additional bazaar and adventure stuff can possibly go here later...
        }

        /// <summary>Sent from client after we've sent SendExpZonein.</summary>
        internal void HandleSendExpZoneIn(EQRawApplicationPacket packet, Client client)
        {
            //_log.Debug("Send Exp Zone In OPCode recv.");

            // Send Spawn Appearance
            SpawnAppearance sa = new SpawnAppearance();
            sa.Type = (ushort)SpawnAppearanceType.SpawnID;
            sa.Parameter = (uint)client.ZonePlayer.ID;
            EQApplicationPacket<SpawnAppearance> saPacket = new EQApplicationPacket<SpawnAppearance>(AppOpCode.SpawnAppearance, sa);
            client.SendApplicationPacket(saPacket);

            // Inform the rest of the game about client by sending them its spawn struct (if client doesn't want to be hidden)
            if (!client.ZonePlayer.IsGMHidden)
            {
                Internals.Packets.Spawn spawn = client.ZonePlayer.GetSpawn();
                EQApplicationPacket<Internals.Packets.Spawn> spawnPacket = new EQApplicationPacket<Internals.Packets.Spawn>(AppOpCode.NewSpawn, spawn);
                QueuePacketToClients(client.ZonePlayer, spawnPacket, ZoneConnectionState.Connected, true);
            }

            // TODO: If client over level 50, send AAStats

            // Send xp packets

            uint xpHigh = Character.GetXpForLevel(client.ZonePlayer.Level + 1);
            uint xpLow = Character.GetXpForLevel(client.ZonePlayer.Level);
            float xpRatio = (float)(client.ZonePlayer.XP - xpLow) / (float)(xpHigh - xpLow);
            XpUpdate xu = new XpUpdate() { XP = (uint)(xpRatio * 330.0f) };
            EQApplicationPacket<XpUpdate> xuPack = new EQApplicationPacket<XpUpdate>(AppOpCode.ExpUpdate, xu);
            client.SendApplicationPacket(xuPack);

            // TODO: If client over level 50, send AA Timers

            // Send SendExpZoneIn
            EQRawApplicationPacket sezPacket = new EQRawApplicationPacket(AppOpCode.SendExpZonein, client.IPEndPoint, null);
            client.SendApplicationPacket(sezPacket);

            // Send ZoneinSendName
            ZoneInSendName zisn = new ZoneInSendName();
            zisn.Init();
            Buffer.BlockCopy(client.ZonePlayer.PlayerProfile.Name, 0, zisn.Name, 0, 64);
            Buffer.BlockCopy(client.ZonePlayer.PlayerProfile.Name, 0, zisn.Name2, 0, 64);
            zisn.Unknown0 = 0x0A;
            EQApplicationPacket<ZoneInSendName> zisnPacket = new EQApplicationPacket<ZoneInSendName>(AppOpCode.RaidUpdate, zisn);
            client.SendApplicationPacket(zisnPacket);

            // TODO: Once guild support is added, send guild members if client is in a guild

            // Send GuildMOTD... even if client isn't in a guild
            // TODO: Once guild support is added, send the real motd
            GuildMOTD gmotd = new GuildMOTD();
            gmotd.Init();
            gmotd.Unknown0 = 0;
            Buffer.BlockCopy(client.ZonePlayer.PlayerProfile.Name, 0, gmotd.Name, 0, 64);
            EQApplicationPacket<GuildMOTD> gmotdPacket = new EQApplicationPacket<GuildMOTD>(AppOpCode.GuildMOTD, gmotd);
            client.SendApplicationPacket(gmotdPacket);
        }

        /// <summary>Handles the setting of message filters.</summary>
        internal void HandleSetServerFilter(EQRawApplicationPacket packet, Client client)
        {
            // TODO: implement server message filtering and store filters.  Fairly straightforward... perhaps have a "messaging" class.
        }

        /// <summary></summary>
        internal void HandleClientReady(EQRawApplicationPacket packet, Client client)
        {
            //_log.Debug("Client ready OPCode recv.");

            // Send a msg to world for it to update its internal client list for \who all and set the online status to inZone
            UpdateWorldWho(client);
            
            // TODO: within zonePlayer, implement and start timers for position
            client.ZonePlayer.SetLockedAndLoaded();

            // TODO: load zone flags

            // TODO: Set GM Flag for GMs and send petition queue

            // TODO: reapply buffs?

            // Send wearChange packets to all clients about our 8 slots
            for (int i = 0; i < Character.MAX_EQUIPABLES; i++)
                client.ZonePlayer.TriggerWearChange((EquipableType)i);

            // TODO: Once pets are in, apparently we need to send wearChange packets to all clients about pet's 8 slots

            // TODO: Once pets are in, apparently we need to send pet buffs to client

            // TODO: Once groups are in, may want to refresh group info from db

            // TODO: orig code sends weather to all clients but this might be a bit inefficient, no?  Perhaps just send this client a weather packet?
        }
        #endregion

        #region ConnectED OpCode Handlers
        /// <summary></summary>
        internal void HandleClientUpdate(EQRawApplicationPacket packet, Client client)
        {
            // TODO: check for AI controlled?

            EQApplicationPacket<PlayerPositionUpdateClient> ppucPacket = new EQApplicationPacket<PlayerPositionUpdateClient>(packet);
            if (ppucPacket.PacketStruct.SpawnId != client.ZonePlayer.ID)
            {
                _log.Warn("Received client update for spawn id other than this client.");
                return;
            }
            else
            {
                SpawnAppearance sa;
                PlayerPositionUpdateServer ppus;
                client.ZonePlayer.ProcessClientUpdate(ppucPacket.PacketStruct, out sa, out ppus);

                if (sa.SpawnId != 0)
                {
                    EQApplicationPacket<SpawnAppearance> saPacket = new EQApplicationPacket<SpawnAppearance>(AppOpCode.SpawnAppearance, sa);
                    QueuePacketToClients(client.ZonePlayer, saPacket, ZoneConnectionState.Connected, true);
                }

                if (ppus.SpawnId != 0)
                {
                    EQApplicationPacket<PlayerPositionUpdateServer> ppusPack = new EQApplicationPacket<PlayerPositionUpdateServer>(AppOpCode.ClientUpdate, ppus);
                    if (client.ZonePlayer.IsGMHidden)
                    {
                        // TODO: send only to admins?
                    }
                    else
                        QueuePacketToNearbyClients(client.ZonePlayer, ppusPack, 300.0F, true);
                }
            }
        }

        /// <summary></summary>
        internal void HandleWearChange(EQRawApplicationPacket packet, Client client)
        {
            EQApplicationPacket<WearChange> wcPack = new EQApplicationPacket<WearChange>(packet);

            if (wcPack.PacketStruct.SpawnId != client.ZonePlayer.ID)
            {
                _log.WarnFormat("Client recv'd a WearChange for a spawn id other than its own ({0}).", wcPack.PacketStruct.SpawnId);
                return;
            }

            QueuePacketToClients(client.ZonePlayer, wcPack, true);
        }

        /// <summary></summary>
        internal void HandleClickDoor(EQRawApplicationPacket packet, Client client)
        {
            EQApplicationPacket<ClickDoor> cdPack = new EQApplicationPacket<ClickDoor>(packet);
            byte doorId = (byte)cdPack.PacketStruct.DoorId;
            Door d = FindDoor(doorId);
            if (d != null)
            {
                _log.DebugFormat("Client clicked door {0}.", doorId);
                MoveDoor? md = d.Click(client.ZonePlayer);
                
                if (md != null)
                {
                    EQApplicationPacket<MoveDoor> mdPack = new EQApplicationPacket<MoveDoor>(AppOpCode.MoveDoor, md.Value);
                    QueuePacketToClients(client.ZonePlayer, mdPack, false);

                    if (d.TriggerDoor != null)
                    {
                        Door td = FindDoor(d.TriggerDoor.Value);
                        if (td != null)
                        {
                            _log.DebugFormat("Client triggered door {0}.", td.Ordinal);
                            md = td.Click(client.ZonePlayer);
                            EQApplicationPacket<MoveDoor> mdPack2 = new EQApplicationPacket<MoveDoor>(AppOpCode.MoveDoor, md.Value);
                            QueuePacketToClients(client.ZonePlayer, mdPack2, false);
                        }
                        else
                        {
                            client.ZonePlayer.MsgMgr.SendSpecialMessage(MessageType.Default, "ERROR: Door to be triggered doesn't seem to exist, please contact a GM.");
                            _log.WarnFormat("Door {0} triggered door {1} that doesn't exist.", cdPack.PacketStruct.DoorId, d.TriggerDoor);
                        }
                    }
                }

                // TODO: raise door clicked event
            }
            else
            {
                client.ZonePlayer.MsgMgr.SendSpecialMessage(MessageType.Default, "ERROR: Door id {0} doesn't seem to exist, please contact a GM.", doorId);
                _log.WarnFormat("Client clicked door {0} that doesn't exist.", doorId);
            }
        }

        /// <summary></summary>
        internal void HandleZoneChange(EQRawApplicationPacket packet, Client client)
        {
            EQApplicationPacket<ZoneChange> zcPack = new EQApplicationPacket<ZoneChange>(packet);
            ushort targetZoneId = 0;
            ZonePoint zp = null;
            ZonePlayer player = client.ZonePlayer;
            player.IsZoning = true;

            if (zcPack.PacketStruct.ZoneID == 0)
            {
                // Client doesn't know where they are going, try to figure it out for them
                switch (client.ZonePlayer.ZoneMode)
                {
                    case ZoneMode.EvacToSafeCoords:
                    case ZoneMode.ZoneToSafeCoords:
                        // for now assume it is this zone (could be a cheat tho)
                        // TODO: could start a cheat timer here
                        targetZoneId = this.Zone.ZoneID;
                        break;
                    case ZoneMode.GMSummon:
                        targetZoneId = player.ZoneSummonID;
                        break;
                    case ZoneMode.GateToBindPoint:
                    case ZoneMode.ZoneToBindPoint:
                        targetZoneId = (ushort)player.PlayerProfile.Binds[0].ZoneId;
                        break;
                    case ZoneMode.Solicited:    // this is when we told the client to zone somewhere (we should know where they are going)
                        // TODO: could start a cheat timer here?
                        targetZoneId = player.ZoneSummonID;
                        break;
                    case ZoneMode.Unsolicited:  // client decided to zone all on its own
                        zp = this.GetNearestZonePoint(player.X, player.Y, player.Z, ZONEPOINT_DETECTION_RANGE);
                        if (zp != null)
                            targetZoneId = zp.TargetZoneID;  // found a zp a reasonable distance away, so use it
                        else
                        {
                            // TODO: start a cheat timer and send a message to the client  Evil thought: zone thier ass to BFE?
                            _log.WarnFormat("Invalid unsolicited zone request for char {0}", player.Name);
                            this.SendZoneCancel(zcPack.PacketStruct, client);
                            return;
                        }
                        break;
                }
            }
            else
            {
                if (player.ZoneMode == ZoneMode.EvacToSafeCoords && player.ZoneSummonID > 0)
                    targetZoneId = player.ZoneSummonID;     // allows for proper zoning for evac & succor
                else
                    targetZoneId = zcPack.PacketStruct.ZoneID;

                // zoning to a specific zone unsolicited means the client must have crossed a zone line
                if (player.ZoneMode == ZoneMode.Unsolicited)
                {
                    zp = this.GetNearestZonePoint(player.X, player.Y, player.Z, targetZoneId, ZONEPOINT_DETECTION_RANGE);

                    if (zp == null || zp.TargetZoneID != targetZoneId)
                    {
                        // if we didn't get a zone point or it's to a diff zone then it's bullshit
                        // TODO: run cheat detection and send a chat message to the client  Evil thought: zone their ass to BFE?
                        _log.WarnFormat("Invalid unsolicited zone request to zone {0} for char {1}", targetZoneId, player.Name);
                        this.SendZoneCancel(zcPack.PacketStruct, client);
                        return;
                    }
                }
            }

            // Attempt to load the target zone's data
            Zone targetZone = null;
            DataLoadOptions dlo = new DataLoadOptions();
            dlo.LoadWith<Zone>(z => z.ZonePoints);

            using (EmuDataContext dbCtx = new EmuDataContext())
            {
                dbCtx.ObjectTrackingEnabled = false;
                dbCtx.LoadOptions = dlo;
                targetZone = dbCtx.Zones.SingleOrDefault<Zone>(z => z.ZoneID == targetZoneId);
            }

            if (targetZone == null)
            {
                // not a good zone
                client.ZonePlayer.MsgMgr.SendSpecialMessage(MessageType.Default, "ERROR: Invalid zone attempt. Attempt logged. If you feel this is an error, please contact a GM.");
                _log.ErrorFormat("Invalid target zone id {0} while {1} was attempting to zone", targetZoneId, player.Name);
                this.SendZoneCancel(zcPack.PacketStruct, client);
                return;
            }
            else if (targetZone.ZonePoints.Count == 0)
            {
                client.ZonePlayer.MsgMgr.SendSpecialMessage(MessageType.Default, "ERROR: Bad zone points for zone attempt, please contact a GM.");
                _log.ErrorFormat("Unable to get zone points for target zone id: {0} while {1} was attempting to zone", targetZoneId, player.Name);
                this.SendZoneCancel(zcPack.PacketStruct, client);
                return;
            }

            // TODO: raise zoning event?

            float destX = 0.0F, destY = 0.0F, destZ = 0.0F;
            float destH = player.Heading;

            // certain special cases have reasons to ignore zoning restrictions
            byte ignoreZoneRestrictions = player.IgnoreZoneRestrictionsReason;  // fetch value for the call to world
            player.IgnoreZoneRestrictionsReason = 0;    // reset the ignoring of zoning restrictions reason

            switch (player.ZoneMode)
            {
                case ZoneMode.ZoneToSafeCoords:
                case ZoneMode.EvacToSafeCoords:
                    _log.DebugFormat("Zoning {0} to safe coords {1},{2},{3} in {4}({5})", player.Name, targetZone.SafeX, targetZone.SafeY,
                        targetZone.SafeZ, targetZone.ShortName, targetZone.ZoneID);
                    destX = targetZone.SafeX;
                    destY = targetZone.SafeY;
                    destZ = targetZone.SafeZ;
                    break;
                case ZoneMode.GMSummon:
                    destX = player.ZoneSummonX;
                    destY = player.ZoneSummonY;
                    destZ = player.ZoneSummonZ;
                    ignoreZoneRestrictions = 1;
                    break;
                case ZoneMode.ZoneToBindPoint:
                case ZoneMode.GateToBindPoint:
                    destX = player.PlayerProfile.Binds[0].X;
                    destY = player.PlayerProfile.Binds[0].Y;
                    destZ = player.PlayerProfile.Binds[0].Z;
                    break;
                case ZoneMode.Solicited:    // we told the client to zone somewhere - we should know where they are going
                    // TODO: start a cheat timer?
                    destX = player.ZoneSummonX;
                    destY = player.ZoneSummonY;
                    destZ = player.ZoneSummonZ;
                    break;
                case ZoneMode.Unsolicited:  // client requested a zoning... when would this happen?  Normal zone line zoning, yes?

                    if (zp != null)
                    {
                        _log.DebugFormat("Detected unsolicited client zoning with a good zone point: target zone id: {0} ({1}x {2}y {3}z {4}heading)",
                            zp.TargetZoneID, zp.TargetX, zp.TargetY, zp.TargetZ, zp.TargetHeading);

                        // zoning using a valid zone point, figure out correct coords - 999999 is placeholder for same as where they were from
                        destX = zp.TargetX == 999999.0F ? client.ZonePlayer.X : zp.TargetX;
                        destY = zp.TargetY == 999999.0F ? client.ZonePlayer.Y : zp.TargetY;
                        destZ = zp.TargetZ == 999999.0F ? client.ZonePlayer.Z : zp.TargetZ;
                        destH = zp.TargetHeading == 999.0F ? client.ZonePlayer.Heading : zp.TargetHeading;
                        break;
                    }
                    else   // should never get here
                    {
                        _log.Error("Somehow an unsolicited zone request made it through the server without a zone cancel.  Please fix.");
                        SendZoneCancel(zcPack.PacketStruct, client);
                        return;
                    }
            }

            // Enforce some rules
            bool zoneProblem = false;   // Some confusion in old emu about when to send certain zone errors... they only currently send one kind
            if (ignoreZoneRestrictions == 0 && (player.AccountStatus < targetZone.MinStatus || player.Level < targetZone.MinLevel))
                zoneProblem = true;

            // TODO: implement zone flags and then check here if char needs to be flagged to zone
            //if (ignoreZoneRestrictions == 0 && targetZone.FlagIDNeeded != null)
            //{
            //    // char needs to be flagged for zone to enter
            //    if (player.AccountStatus < MIN_STATUS_TO_IGNORE_ZONE_FLAGS && )
            //    {

            //    }
            //}

            // TODO: LDON dungeon entrance rules will go here

            if (!zoneProblem)
                this.ZoneClient(zcPack.PacketStruct, targetZone, destX, destY, destZ, destH, ignoreZoneRestrictions, client);
            else
                this.SendZoneError(zcPack.PacketStruct, ZoneError.NoExperience, client);
        }

        /// <summary></summary>
        internal void HandleSaveOnZoneReq(EQRawApplicationPacket packet, Client client)
        {
            client.ZonePlayer.Save();
        }

        /// <summary></summary>
        internal void HandleDeleteSpawn(EQRawApplicationPacket packet, Client client)
        {
            //_log.Debug("Recv DeleteSpawn Packet");

            EQRawApplicationPacket lrPack = new EQRawApplicationPacket(AppOpCode.LogoutReply, client.IPEndPoint, null);
            client.SendApplicationPacket(lrPack);

            SendDespawnPacket(client.ZonePlayer);
            //EQRawApplicationPacket dsPack = new EQRawApplicationPacket(AppOpCode.DeleteSpawn, client.IPEndPoint, BitConverter.GetBytes(client.ZonePlayer.ID));
            //QueuePacketToClients(client.ZonePlayer, dsPack, false);

            client.ZonePlayer.Disconnect();
        }

        internal void HandleMoveItem(EQRawApplicationPacket packet, Client client)
        {
            EQApplicationPacket<MoveItem> miPack = new EQApplicationPacket<MoveItem>(packet);
            MoveItem mi = miPack.PacketStruct;
            client.ZonePlayer.InvMgr.SwapItem(mi.FromSlot, mi.ToSlot, (byte)mi.NumberInStack);
        }

        internal void HandleSpawnAppearance(EQRawApplicationPacket packet, Client client)
        {
            EQApplicationPacket<SpawnAppearance> saPack = new EQApplicationPacket<SpawnAppearance>(packet);
            SpawnAppearance sa = saPack.PacketStruct;

            if (sa.SpawnId != client.ZonePlayer.ID) {
                _log.ErrorFormat("Recv spawn appearance packet with an ID ({0} that doesn't match this client's ({1})", sa.SpawnId, client.ZonePlayer.ID);
                return;
            }

            switch ((SpawnAppearanceType)sa.Type) {
                case SpawnAppearanceType.Invis:
                    client.ZonePlayer.Invisible = (sa.Parameter == 1);
                    QueuePacketToClients(client.ZonePlayer, packet, true);
                    break;
                case SpawnAppearanceType.Light:
                    QueuePacketToClients(client.ZonePlayer, packet, true);  // client is emitting light (torch, shiny shield, etc.)
                    break;
                case SpawnAppearanceType.Animation:
                    if (client.ZonePlayer.IsAIControlled)
                        return;

                    switch ((Stance)sa.Parameter) {
                        case Stance.Standing:
                            client.ZonePlayer.IsFeigning = false;
                            // TODO: bind wound stuff
                            // TODO: disable camp timer
                            break;
                        case Stance.Looting:
                            client.ZonePlayer.IsFeigning = false;
                            break;
                        case Stance.Sitting:
                            client.ZonePlayer.IsFeigning = false;
                            // TODO: some bard spell logic stuff
                            // TODO: bind wound stuff
                            break;
                        case Stance.Crouching:
                            client.ZonePlayer.IsFeigning = false;
                            // TODO: some bard logic stuff
                            break;
                        case Stance.Dead:
                            // TODO: interrupt spell
                            break;
                        default:
                            _log.ErrorFormat("Unsupported Animation stance value: {0}", sa.Parameter);
                            return;
                    }

                    client.ZonePlayer.Stance = (Stance)sa.Parameter;
                    QueuePacketToClients(client.ZonePlayer, packet, true);

                    break;
                case SpawnAppearanceType.Sneak:
                    client.ZonePlayer.Sneaking = (sa.Parameter == 1);
                    QueuePacketToClients(client.ZonePlayer, packet, true);
                    break;
                case SpawnAppearanceType.Anon:
                    if (sa.Parameter == 1)  // Anon
                        client.ZonePlayer.Anonymous = (byte)1;
                    else if (sa.Parameter == 2 || sa.Parameter == 3)    // Roleplay or Anon + Roleplay
                        client.ZonePlayer.Anonymous = (byte)2;
                    else if (sa.Parameter == 0)     // Non-Anon
                        client.ZonePlayer.Anonymous = 0;
                    else {
                        _log.ErrorFormat("Unsupported Anonymous parameter value: {0}", sa.Parameter);
                        return;
                    }

                    QueuePacketToClients(client.ZonePlayer, packet, true);
                    UpdateWorldWho(client);
                    break;
                case SpawnAppearanceType.AFK:
                    client.ZonePlayer.IsAFK = (sa.Parameter == 1);
                    QueuePacketToClients(client.ZonePlayer, packet, true);
                    break;
                case SpawnAppearanceType.Split:
                    client.ZonePlayer.IsAutoSplitting = (sa.Parameter == 1);
                    break;
                case SpawnAppearanceType.Size:
                    QueuePacketToClients(client.ZonePlayer, packet, true);
                    break;
                case SpawnAppearanceType.ShowHelm:
                    client.ZonePlayer.IsShowingHelm = (sa.Parameter == 1);
                    break;
                default:
                    _log.ErrorFormat("Unsupported spawnAppearanceType: {0}", sa.Type);
                    break;
            }
        }

        internal void HandleJump(EQRawApplicationPacket packet, Client client)
        {
            ZonePlayer zp = client.ZonePlayer;
            zp.Endurance = zp.Endurance - (zp.Level < 20u ? (225u * zp.Level / 100u) : 50u);
        }

        internal void HandleReadBook(EQRawApplicationPacket packet, Client client)
        {
            EQApplicationPacket<BookRequest> brPack = new EQApplicationPacket<BookRequest>(packet);
            BookRequest br = brPack.PacketStruct;

            if (br.TextFile[0] == '0' && br.TextFile[1] == '\0') {
                _log.Warn("Invalid book in ReadBook packet.");
                return;
            }

            string txtFile = Encoding.ASCII.GetString(br.TextFile);
            Book book = null;
            using (EmuDataContext dbCtx = new EmuDataContext())
                book = dbCtx.Books.FirstOrDefault(b => b.Name == txtFile);

            if (book != null && book.BookText.Length > 0) {
                byte[] txtBuffer = new byte[Marshal.SizeOf(typeof(BookText)) + book.BookText.Length];
                Buffer.BlockCopy(BitConverter.GetBytes(br.Window), 0, txtBuffer, 0, 1);
                Buffer.BlockCopy(BitConverter.GetBytes(br.Type), 0, txtBuffer, 1, 1);
                Buffer.BlockCopy(Encoding.ASCII.GetBytes(book.BookText), 0, txtBuffer, 6, book.BookText.Length);

                EQRawApplicationPacket btPack = new EQRawApplicationPacket(AppOpCode.ReadBook, client.IPEndPoint, txtBuffer);
                client.SendApplicationPacket(btPack);
            }
        }

        internal void HandleAutoAttack(EQRawApplicationPacket packet, Client client)
        {
            byte[] payload = packet.GetPayload();
            if (payload[0] == 0)    // Auto-Attack turned off?
                client.ZonePlayer.IsAutoAttacking = false;
            else if (payload[0] == 1)   // Auto-Attack turned on?
                client.ZonePlayer.IsAutoAttacking = true;
            else
                throw new Exception(string.Format("Invalid value for Auto Attack packet ({0})", payload[0]));
        }

        internal void HandleTargetCmd(EQRawApplicationPacket packet, Client client)
        {
            ZonePlayer zp = client.ZonePlayer;
            EQApplicationPacket<ClientTarget> ctPack = new EQApplicationPacket<ClientTarget>(packet);
            zp.TargetMob = _mobMgr[(int)ctPack.PacketStruct.TargetID];  // TODO: add check for AI control?

            // TODO: HoTT crap

            // TODO: Group crap

            // TODO: ensure LOS to target?

            // For /target, send reject or success packet
            if (packet.OpCode == AppOpCode.TargetCommand) {
                if (zp.TargetMob != null 
                    && !zp.TargetMob.IsInvisibleTo(zp) 
                    && zp.DistanceNoRoot(zp.TargetMob) <= ZonePlayer.TARGETING_RANGE * ZonePlayer.TARGETING_RANGE) {
                    client.SendApplicationPacket(packet);
                    HPUpdateRatio hpRatio = zp.GetHPUpdateRatio();
                    EQApplicationPacket<HPUpdateRatio> hpRatioPacket = new EQApplicationPacket<HPUpdateRatio>(AppOpCode.MobHealth, hpRatio);
                    client.SendApplicationPacket(hpRatioPacket, false);
                }
                else {
                    zp.TargetMob = null;
                    byte[] rejected = { 0x2f, 0x01, 0x00, 0x00, 0x0d, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
                    EQRawApplicationPacket targetRejPack = new EQRawApplicationPacket(AppOpCode.TargetReject, client.IPEndPoint, rejected);
                    client.SendApplicationPacket(targetRejPack);
                }
            }
        }

        /// <summary></summary>
        /// <remarks>Player is saved twice during the looting process. Once when coin is looted and again when looting is stopped.</remarks>
        internal void HandleLootRequest(EQRawApplicationPacket packet, Client client)
        {
            int entityId = BitConverter.ToInt32(packet.GetPayload(), 0);
            Mob m = _mobMgr[entityId];
            if (m != null) {
                Corpse c = m as Corpse;
                if (c != null) {
                    QueuePacketToClient(client, packet, true, ZoneConnectionState.All); // Client needs orig packet sent back (like an ack)

                    client.ZonePlayer.BreakSneakiness();    // Sneakiness goes away when ya loot

                    uint plat, gold, silver, copper;
                    List<InventoryItem> items;
                    byte lootRes = c.Loot(client.ZonePlayer, out plat, out gold, out silver, out copper, out items);

                    // Things are set here in one go, the response is the response regardless & money amounts will be 0 on disallowed loot reqs
                    MoneyOnCorpse moc = new MoneyOnCorpse()
                    {
                        Response = lootRes,
                        Platinum = plat,
                        Gold = gold,
                        Silver = silver,
                        Copper = copper
                    };

                    if (lootRes == 1) {
                        if (!(c is PlayerCorpse) && client.ZonePlayer.IsGrouped && client.ZonePlayer.IsAutoSplitting) {  // TODO: add check for non-null group
                            moc.Platinum = moc.Gold = moc.Silver = moc.Copper = 0;  // cash will be sent below in other packets

                            // TODO: split the cash
                        }
                        else
                            client.ZonePlayer.GiveMoney(plat, gold, silver, copper);

                        ThreadPool.QueueUserWorkItem(new WaitCallback(ZPSaveCallback), client.ZonePlayer);     // Background the save op
                    }

                    // Send the loot response (either money or a failed message)
                    EQApplicationPacket<MoneyOnCorpse> mocPack = new EQApplicationPacket<MoneyOnCorpse>(AppOpCode.MoneyOnCorpse, moc);
                    QueuePacketToClient(client, mocPack, true, ZoneConnectionState.All);

                    // Now send items if we've been allowed to loot
                    if (lootRes == 1) {
                        
                        //_log.DebugFormat("Corpse {0} has {1} items on it", c.Name, items == null ? 0 : items.Count);
                        if (items != null) {
                            int origSlotId = 0;
                            for (int i = 0; i < items.Count; i++) {
                                if (!(c is PlayerCorpse) || items[i].SlotID < (int)InventorySlot.PersonalEnd) {   // Don't show items in bags
                                    if (i >= 30)
                                        client.ZonePlayer.MsgMgr.SendSpecialMessage(MessageType.Default, "Warning: Too many items to display.  Loot some then re-loot to see the rest.");
                                    else {
                                        origSlotId = items[i].SlotID;   // Save the real slot id
                                        items[i].SlotID = i + 22;       // Temporarily set the slot id to a value for sending to the client
                                        ZoneServer.SendItemPacket(client, items[i], ItemPacketType.Loot);
                                        items[i].SlotID = origSlotId;   // Restore real slot id
                                    }
                                }
                            }
                        }
                    }

                    //QueuePacketToClient(client, packet, true, ZoneConnectionState.All); // Client needs orig packet sent back (like an ack)
                }
                else {
                    _log.ErrorFormat("Entity found for id {0} is not a corpse.", entityId);
                    SendLootRequestErrorPacket(client, 2);
                }
            }
            else {
                _log.ErrorFormat("No entity found for id {0}", entityId);
                SendLootRequestErrorPacket(client, 2);
            }
        }

        internal void HandleEndLootRequest(EQRawApplicationPacket packet, Client client)
        {
            int entityId = BitConverter.ToInt32(packet.GetPayload(), 0);
            Corpse c = _mobMgr.GetCorpse(entityId);

            if (c == null)
                _log.ErrorFormat("Unable to find corpse ID {0}", entityId);
            else {
                SendEndLootComplete(client);
                c.StopLooting();
                ThreadPool.QueueUserWorkItem(new WaitCallback(ZPSaveCallback), client.ZonePlayer);
            }
        }

        internal void HandleLootItem(EQRawApplicationPacket packet, Client client)
        {
            EQApplicationPacket<LootingItem> liPack = new EQApplicationPacket<LootingItem>(packet);
            LootingItem li = liPack.PacketStruct;
            Corpse c = _mobMgr.GetCorpse((int)li.LooteeId);

            if (li.LooterId != client.ZonePlayer.ID) {
                _log.ErrorFormat("ZonePlayer ID != LootItem request id ({0} != {1})", client.ZonePlayer.ID, li.LooterId);
                SendEndLootComplete(client);
                return;
            }

            if (c == null) {
                _log.ErrorFormat("Corpse not found during a LootItem request for entity {0}", li.LooteeId);
                SendEndLootComplete(client);
                return;
            }

            QueuePacketToClient(client, packet, true, ZoneConnectionState.All); // ack back the packet
            InventoryItem invItem = c.LootItem(client.ZonePlayer, (int)li.SlotIdx);
            if (invItem == null) {
                _log.DebugFormat("Problem looting corpse {0}: null item - maybe lore?", c.Name);
                SendEndLootComplete(client);
            }
            else {
                //_log.DebugFormat("Looted item {0} from corpse {1}. Autoloot: {2}", invItem.Item.Name, c.Name, li.AutoLoot);

                if (li.AutoLoot > 0) {
                    if (!client.ZonePlayer.AutoGiveItem(ref invItem))
                        client.ZonePlayer.GiveItem(invItem, (int)InventorySlot.Cursor);
                }
                else
                    client.ZonePlayer.GiveItem(invItem, (int)InventorySlot.Cursor);

                //c.RemoveItem(li.SlotIdx);   // Now remove the item from the corpse

                // TODO: LootItem script event

                // TODO: LDON adventure crap

                // Send msgs to those concerned

                // At this point, invItem has the correct slot info as it was passed ref to the "giver" routines
                //_log.Debug("Raw link bytes: " + BitConverter.ToString(invItem.ToItemLink()));
                //_log.Debug("Printable link bytes: " + Encoding.ASCII.GetString(invItem.ToItemLink()));
                client.ZonePlayer.MsgMgr.SendMessageID((uint)MessageType.LootMessages, MessageStrings.LOOTED_MESSAGE, invItem.ToItemLink());
                if (!(c is PlayerCorpse)) {
                    // TODO: send same msg to group if grouped
                }
            }
        }

        // TODO: add comment about when this packet occurs
        internal void HandleDamage(EQRawApplicationPacket packet, Client client)
        {
            // Re-broadcast to other clients
            EQApplicationPacket<CombatDamage> cdPack = new EQApplicationPacket<CombatDamage>(packet);

            // Don't send falling damage to originator
            QueuePacketToClients(client.ZonePlayer, cdPack, cdPack.PacketStruct.DamageType == (byte)DamageType.Falling);
        }

        /// <summary>From falling, etc.</summary>
        internal void HandleEnvDamage(EQRawApplicationPacket packet, Client client)
        {
            if (this.Zone.ZoneID == 183 || this.Zone.ZoneID == 184)
                return; // No falling damage in tutorial or loading zones

            int damage = 0;
            ZonePlayer zp = client.ZonePlayer;

            if (!zp.IsInZone())
                damage = 1;     // handle damage differently for client which hasn't completed logging in - min of 1 so client acknowledges it

            EQApplicationPacket<EnvDamage> edPack = new EQApplicationPacket<EnvDamage>(packet);
            if (zp.CanGMAvoidFallingDamage()) {
                damage = 1; // min of 1 so client acknowledges it
                zp.MsgMgr.SendSpecialMessage(MessageType.Common13, "Your GM status protects you from {0} points of {1} environmental damage",
                    edPack.PacketStruct.DamageAmount, (DamageType)edPack.PacketStruct.DamageType);
            }
            else if (zp.Invulnerable) {
                damage = 1; // min of 1 so client acknowledges it
                zp.MsgMgr.SendSpecialMessage(MessageType.Common13, "Your GM status protects you from {0} points of {1} environmental damage",
                    edPack.PacketStruct.DamageAmount, (DamageType)edPack.PacketStruct.DamageType);
            }
            else
                damage = (int)edPack.PacketStruct.DamageAmount;

            if (edPack.PacketStruct.DamageType == (byte)DamageType.Falling) {
                // TODO: Lessen damage due to acrobatics AA
            }

            if (damage < 0)
                damage = 31337; // Why 31337?  just a random big number?

            zp.HP -= damage;
        }

        /// <summary>From consuming food, etc.</summary>
        internal void HandleDeleteItem(EQRawApplicationPacket packet, Client client)
        {
            ZonePlayer zp = client.ZonePlayer;
            EQApplicationPacket<DeleteItem> diPack = new EQApplicationPacket<DeleteItem>(packet);
            InventoryItem invItem = zp.InvMgr[(int)diPack.PacketStruct.FromSlotId];

            if (invItem.Item.ItemType == (byte)ItemType.Alcohol) {
                SendMessageIDToNearbyClients(zp, MessageType.Default, MessageStrings.DRINKING_MESSAGE, true, 50.0f, zp.Name, invItem.Item.Name);
                zp.CheckForSkillUp(Skill.AlcoholTolerance, null, 0);    // fucking alcholics
            }

            zp.InvMgr.DeleteItem((int)diPack.PacketStruct.FromSlotId, 1, false);
        }

        /// <summary>From chat messages and commands.</summary>
        internal void HandleChannelMessage(EQRawApplicationPacket packet, Client client)
        {
            ZonePlayer zp = client.ZonePlayer;

            if (zp.IsAIControlled) {
                zp.MsgMgr.SendSpecialMessage(MessageType.Common13, "You try to speak but can't move your mouth!");
                return;
            }

            ChannelMessage cm = ChannelMessage.Deserialize(packet.GetPayload());
            zp.MsgMgr.ReceiveChannelMessage(cm.TargetName, cm.Message.Substring(0, cm.Message.IndexOf('\0')), cm.ChannelId, cm.LanguageId, cm.LanguageSkill);
        }

        /// <summary>From chat messages and commands.</summary>
        internal void HandleConsiderCorpse(EQRawApplicationPacket packet, Client client)
        {
            EQApplicationPacket<Consider> conPack = new EQApplicationPacket<Consider>(packet);
            Consider con = conPack.PacketStruct;

            Corpse c = _mobMgr.GetCorpse((int)con.TargetId);
            if (c == null) {
                _log.ErrorFormat("No corpse found for id {0}!", con.TargetId);
                return;
            }

            if (c is PlayerCorpse) {
                if (c.DecayTime.TotalMilliseconds > 0) {
                    client.ZonePlayer.MsgMgr.SendSpecialMessage(MessageType.Default,
                        "This corpse will decay in {0} days, {1} hours, {2} minutes and {3} seconds.",
                        c.DecayTime.Days, c.DecayTime.Hours, c.DecayTime.Minutes, c.DecayTime.Seconds);
                    client.ZonePlayer.MsgMgr.SendSpecialMessage(MessageType.Default, "This corpse {0} be resurrected.",
                        ((PlayerCorpse)c).Rezzed ? "cannot" : "can");
                }
                else
                    client.ZonePlayer.MsgMgr.SendMessageID(10u, MessageStrings.CORPSE_DECAY_NOW);
            }
            else {
                if (c.DecayTime.TotalMilliseconds > 0)
                    client.ZonePlayer.MsgMgr.SendMessageID(10u, MessageStrings.CORPSE_DECAY1, c.DecayTime.Seconds.ToString() + '\0' + c.DecayTime.Minutes.ToString());
                else
                    client.ZonePlayer.MsgMgr.SendMessageID(10u, MessageStrings.CORPSE_DECAY_NOW);
            }
        }

        internal void HandleMemorizeSpell(EQRawApplicationPacket packet, Client client)
        {
            EQApplicationPacket<MemorizeSpell> msPacket = new EQApplicationPacket<MemorizeSpell>(packet);
            MemorizeSpell ms = msPacket.PacketStruct;

            ZonePlayer zp = client.ZonePlayer;

            // Check for valid spell Id
            if (!IsValidSpell(ms.SpellId)) {
                zp.MsgMgr.SendSpecialMessage(MessageType.Common13, "ERROR: spell id out of range!");
                return;
            }

            // Retrieve the spell from world
            Spell s = ZoneServer.WorldSvc.GetSpellById(ms.SpellId);   // TODO: optimize to cache a certain number of these local to a zone?
            if (s == null) {
                _log.ErrorFormat("No spell found for id {0}!", ms.SpellId);
                return;
            }

            // Check if player can cast the spell
            if (zp.Class > Character.CHARACTER_CLASS_COUNT || s.GetReqLevelForClass((CharClasses)zp.Class) > zp.Level) {
                zp.MsgMgr.SendMessageID(13, MessageStrings.SPELL_LEVEL_TO_LOW, s.GetReqLevelForClass((CharClasses)zp.Class).ToString(), s.SpellName);
                return;
            }

            switch ((SpellMemorize)ms.Scribing) {
                case SpellMemorize.Scribing:
                    InventoryItem invItem = zp.InvMgr[(int)InventorySlot.Cursor];
                    if (invItem != null && invItem.Item != null && invItem.Item.ItemClass == Item.ITEM_CLASS_COMMON) {
                        if (invItem.Item.scrolleffect == ms.SpellId) {  // do spell IDs match?
                            zp.ScribeSpell(ms.SpellId, ms.SlotId, true);
                            zp.InvMgr.DeleteItem((int)InventorySlot.Cursor, 1, true);
                        }
                        else {
                            zp.MsgMgr.SendSpecialMessage(MessageType.Common13, "ERROR: Item not found or spell IDs don't match!");
                            _log.ErrorFormat("Spell Scribing: spell IDs don't match (cursor:{0} != packet:{1}).", invItem.Item.scrolleffect, ms.SpellId);
                        }
                    }
                    else {
                        _log.Error("Spell Scribing: unable to find spell scroll on the cursor.");
                        zp.MsgMgr.SendSpecialMessage(MessageType.Common13, "ERROR: Cannot find scroll on cursor!");
                    }
                    break;
                case SpellMemorize.Memorize:
                    if (zp.HasSpellScribed(ms.SpellId)) // Do we have the spell in our spell book?
                        zp.MemorizeSpell(s, ms.SlotId, true);
                    else
                        _log.WarnFormat("{0} tried to memorize spell {1} that wasn't in thier spell book.", zp.Name, ms.SpellId);   // TODO: log cheaters?
                    break;
                case SpellMemorize.Forget:
                    zp.ForgetSpell(ms.SlotId, true);
                    break;
            }

            ThreadPool.QueueUserWorkItem(o => zp.Save());   // Async save the player info
        }

        internal void HandleCastSpell(EQRawApplicationPacket packet, Client client)
        {
            ZonePlayer zp = client.ZonePlayer;

            EQApplicationPacket<CastSpell> csPacket = new EQApplicationPacket<CastSpell>(packet);
            CastSpell cs = csPacket.PacketStruct;
            _log.DebugFormat("{0} casting spell {1} in spell slot {2} / inv slot {3} at target {4}", zp.Name, cs.SpellId, cs.InventorySlotId, cs.TargetId);

            // Can't cast spells when charmed, etc.
            if (!IsValidSpell(cs.SpellId) || !zp.TrySpellCasting((ushort)cs.SpellId))
                return;

            // Are we using an item or potion (possibly even a disc)?
            if (cs.SpellSlotId == (uint)SpellSlot.UseItem || cs.SpellSlotId == (uint)SpellSlot.PotionBelt) {

                // Using a discipline?
                if (cs.InventorySlotId == (uint)SpellSlot.Discipline) {
                    // TODO: Implement discipline usage
                    zp.InterruptSpell(cs.SpellId);
                    _log.Error("Client attempting discipline use... not implemented!");
                    return;
                }
                else if (cs.InventorySlotId <= (uint)InventorySlot.PersonalSlotsEnd || cs.SpellSlotId == (uint)SpellSlot.PotionBelt) {
                    // TODO: handle item clickies, etc.
                    zp.InterruptSpell(cs.SpellId);
                    _log.Error("Client attempting item click spell use... not implemented!");
                    return;
                }
                else {
                    zp.MsgMgr.SendSpecialMessage(MessageType.Default, "ERROR: CastSpell.InvSlot > 29: " + cs.InventorySlotId);
                    _log.ErrorFormat("{0} tried to use a spell effect from item slot > 29: {1}", zp.Name, cs.InventorySlotId);
                    zp.InterruptSpell(cs.SpellId);
                }
            }
            else    // Ability or regular mem'd spell
                zp.CastSpell((ushort)cs.SpellId, cs.SpellSlotId, cs.TargetId);
        }

        #endregion
    }
}
