using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Linq;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceModel;
using System.Text;
using System.Threading;

using EQEmulator.Servers.ExtensionMethods;
using EQEmulator.Servers.Internals;
using EQEmulator.Servers.Internals.Data;
using EQEmulator.Servers.Internals.Entities;
using EQEmulator.Servers.Internals.Packets;
using EQEmulator.Servers.ServerTalk;

namespace EQEmulator.Servers
{
    public enum ZoneInstanceType
    {
        Dynamic,
        Static,
        Instanced
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, IncludeExceptionDetailInFaults = true)]
    public partial class ZoneServer : EQServer
    {
        private const short COMPRESSION_BUFFER_SIZE = 2048;
        private const int CLIENT_AUTH_WINDOW = 1;
        private const int CLIENT_AUTH_CLEANUP_INTERVAL = 60000;     // one minute
        private const int DYNAMIC_SHUTDOWN_TIMEOUT = 60000;         // one minute
        private const int SPAWN_TIMER_INTERVAL = 5000;
        private const int SPAWN_QUEUE_TIMER_INTERVAL = 5000;
        private const int EMPTY_ZONE_SLEEP_INTERVAL = 1000;
        private const int DOOR_TIMER_INTERVAL = 2500;
        private const int DOOR_CLOSED_INTERVAL = 5;     // in seconds
        private const float ZONEPOINT_DETECTION_RANGE = 40000.0F;  // Max distance from a zone point for finding another zone point

        private delegate void OpCodeHandler(EQRawApplicationPacket packet, Client client);

        private static ChannelFactory<IWorldService> _worldSvcClientChannel = null;
        private static EndpointAddress _worldEA;
        private static IWorldService _worldSvcClient = null;
        private static object _worldSvcLock = new object();
        private Dictionary<string, ClientAuth> _authClients = new Dictionary<string, ClientAuth>(10);    // keyed by ip of expected new clients from World
        private ReaderWriterLockSlim _authClientsLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        
        private Timer _staleAuthRemovalTimer, _shutdownTimer, _spawnTimer, _weatherTimer, _spawnQueueTimer, _doorTimer;
        private Dictionary<AppOpCode, OpCodeHandler> _connectingOpCodeHandlers, _connectedOpCodeHandlers;
        private Thread _zoneThread;
        private short _lastEntityId = 0;
        private object _entityIdLock = new object();
        private Zone _zone = null;
        private ZoneInstanceType _instanceType;
        private int _maxSpellId = 0;
        private int _mobsAggroed = 0;
        private int _graveyardId = 0, _graveyardZoneId = 0;
        private float _graveyardX = 0.0F, _graveyardY = 0.0F, _graveyardZ = 0.0F, graveyardHeading = 0.0F;
        private byte _zoneWeather = 0;
        private bool _loaded = false, _clientInc = false, _locked = false, _shutdown = false;
        private object _loadedLock = new object();  // syncs _loaded and _clientInc
        private Map _map;
        private WaterMap _waterMap;
        private Queue<Internals.Packets.Spawn> _spawnQueue = new Queue<EQEmulator.Servers.Internals.Packets.Spawn>(150);
        private MobManager _mobMgr = null;
        // TODO: object list
        // TODO: blocked spells list?

        /// <summary>Static ctor.  Initializes world comm. channel.</summary>
        static ZoneServer()
        {
            // Connect to World (really just setting up proxy)
            _worldEA = new EndpointAddress("net.tcp://localhost:8000/WorldService");    // TODO: change to pick up an endpoint in the config file
            _worldSvcClientChannel = new ChannelFactory<IWorldService>(new NetTcpBinding(), _worldEA);
        }

        public ZoneServer(int port) : base(port)
        {
            LoadOpCodeHandlers();   // maps incomming opCodes to their handler routines
            _mobMgr = new MobManager(this);
        }

        internal static IWorldService WorldSvc
        {
            get
            {
                lock (_worldSvcLock)    // prevents clients from hitting the client in an "Opening" or "Closing" state
                {
                    if (_worldSvcClientChannel.State != CommunicationState.Opened)
                    {
                        _worldSvcClientChannel = new ChannelFactory<IWorldService>(new NetTcpBinding(), _worldEA);
                        _worldSvcClientChannel.Open();
                        _worldSvcClient = _worldSvcClientChannel.CreateChannel();
                        ((IContextChannel)_worldSvcClient).OperationTimeout = new TimeSpan(0, 5, 0);
                    }
                }

                return _worldSvcClient;
            }
        }

        internal Zone Zone
        {
            get { return _zone; }
            set { _zone = value; }
        }

        internal override void ServerStarting()
        {
            _zoneThread = new Thread(new ThreadStart(ZoneProc));

            base.ServerStarting();
        }

        internal override void ServerStarted()
        {
            // Init timers
            _staleAuthRemovalTimer = new Timer(new TimerCallback(StaleAuthRemovalTimerCallback), null, Timeout.Infinite, Timeout.Infinite);
            _shutdownTimer = new Timer(new TimerCallback(ShutdownTimerCallback), null, Timeout.Infinite, 60000);    // allow a minute to shutdown dynamic zones
            _spawnTimer = new Timer(new TimerCallback(SpawnTimerCallback), null, Timeout.Infinite, Timeout.Infinite);
            _spawnQueueTimer = new Timer(new TimerCallback(SpawnQueueTimerCallback), null, Timeout.Infinite, Timeout.Infinite);
            _weatherTimer = new Timer(new TimerCallback(WeatherTimerCallback), null, Timeout.Infinite, Timeout.Infinite);
            _doorTimer = new Timer(new TimerCallback(DoorTimerCallback), null, Timeout.Infinite, Timeout.Infinite);

            _zoneThread.Start();

            base.ServerStarted();
        }

        internal override void ServerStopping()
        {
            _shutdown = true;
            _zoneThread.Join();
            _log.Debug("Zone thread joined.");

            base.ServerStopping();
        }

        internal override void ServerStopped()
        {
            _shutdownTimer.Dispose();
            _spawnTimer.Dispose();
            _spawnQueueTimer.Dispose();
            _weatherTimer.Dispose();
            _doorTimer.Dispose();
            _mobMgr.Dispose();

            base.ServerStopped();

            // TODO: do we get a perf benefit from explicitly clearing any data ourselves?
        }

        internal override void PreSendSessionResponse(Client client)
        {
            client.SendSessionResponse(SessionFormat.Compressed);
            //client.SendSessionResponse(SessionFormat.Normal);
        }

        internal override void PreProcessInPacket(ref BasePacket packet)
        {
            // decompress the packet
            byte[] unCompBuffer = new byte[COMPRESSION_BUFFER_SIZE];
            int unCompLen = EQRawApplicationPacket.Inflate(packet.RawPacketData, ref unCompBuffer);
            Array.Resize<byte>(ref unCompBuffer, unCompLen);
            packet = new BasePacket(packet.ClientIPE, unCompBuffer);
            //_log.Debug(string.Format("inflated packet data: {0}.", BitConverter.ToString(unCompBuffer)));
        }

        internal override void PreProcessOutPacket(ref RawEQPacket packet)
        {
            // compress the packet
            byte[] compBuffer = new byte[COMPRESSION_BUFFER_SIZE];
            int compLen = EQRawApplicationPacket.Deflate(packet.RawPacketData, ref compBuffer);
            Array.Resize<byte>(ref compBuffer, compLen);
            packet = new RawEQPacket(packet.ClientIPE, compBuffer);
        }

        /// <summary>Be careful, the _clients collection is locked when this fires.</summary>
        internal override void ClientDisconnected(Client client)
        {
            if (client.ZonePlayer != null)  // Could be null if we got kicked before the player obj was init
                client.ZonePlayer.Dispose();
            
            _log.DebugFormat("Detected a client disconnect. Total clients now connected: {0}", _clients.Count);

            if (_clients.Count == 0)    // TODO: modify for static and instanced zones
            {
                _shutdownTimer.Change(DYNAMIC_SHUTDOWN_TIMEOUT, Timeout.Infinite);  // if it's the last client to leave start the dynamic unload countdown
                _log.InfoFormat("Shutdown timer started.  Zone will unload in {0} ms", DYNAMIC_SHUTDOWN_TIMEOUT);
            }
        }

        private void LoadOpCodeHandlers()
        {
            // Load "ConnectING" OpCode handler array
            _connectingOpCodeHandlers = new Dictionary<AppOpCode, OpCodeHandler>(15);
            _connectingOpCodeHandlers.Add(AppOpCode.ZoneEntry, new OpCodeHandler(HandleZoneEntry));
            _connectingOpCodeHandlers.Add(AppOpCode.ReqNewZone, new OpCodeHandler(HandleReqNewZone));
            _connectingOpCodeHandlers.Add(AppOpCode.SendAATable, new OpCodeHandler(HandleSendAATable));
            _connectingOpCodeHandlers.Add(AppOpCode.UpdateAA, new OpCodeHandler(HandleUpdateAA));
            _connectingOpCodeHandlers.Add(AppOpCode.SendTributes, new OpCodeHandler(HandleSendTributes));
            _connectingOpCodeHandlers.Add(AppOpCode.SendGuildTributes, new OpCodeHandler(HandleSendGuildTributes));
            _connectingOpCodeHandlers.Add(AppOpCode.ReqClientSpawn, new OpCodeHandler(HandleReqClientSpawn));
            _connectingOpCodeHandlers.Add(AppOpCode.SendExpZonein, new OpCodeHandler(HandleSendExpZoneIn));
            _connectingOpCodeHandlers.Add(AppOpCode.SetServerFilter, new OpCodeHandler(HandleSetServerFilter));
            _connectingOpCodeHandlers.Add(AppOpCode.ClientReady, new OpCodeHandler(HandleClientReady));
            _connectingOpCodeHandlers.Add(AppOpCode.WearChange, new OpCodeHandler(BlackHoleHandler));
            _connectingOpCodeHandlers.Add(AppOpCode.SpawnAppearance, new OpCodeHandler(BlackHoleHandler));
            
            // Load "ConnectED" OpCode handler array
            _connectedOpCodeHandlers = new Dictionary<AppOpCode, OpCodeHandler>(200);
            _connectedOpCodeHandlers.Add(AppOpCode.ClientUpdate, new OpCodeHandler(HandleClientUpdate));
            _connectedOpCodeHandlers.Add(AppOpCode.WearChange, new OpCodeHandler(HandleWearChange));
            _connectedOpCodeHandlers.Add(AppOpCode.FloatListThing, new OpCodeHandler(BlackHoleHandler));
            _connectedOpCodeHandlers.Add(AppOpCode.ClickDoor, new OpCodeHandler(HandleClickDoor));
            _connectedOpCodeHandlers.Add(AppOpCode.ZoneChange, new OpCodeHandler(HandleZoneChange));
            _connectedOpCodeHandlers.Add(AppOpCode.SaveOnZoneReq, new OpCodeHandler(HandleSaveOnZoneReq));
            _connectedOpCodeHandlers.Add(AppOpCode.DeleteSpawn, new OpCodeHandler(HandleDeleteSpawn));
            _connectedOpCodeHandlers.Add(AppOpCode.WeaponEquip1, new OpCodeHandler(BlackHoleHandler));
            _connectedOpCodeHandlers.Add(AppOpCode.WeaponEquip2, new OpCodeHandler(BlackHoleHandler));
            _connectedOpCodeHandlers.Add(AppOpCode.WeaponUnequip2, new OpCodeHandler(BlackHoleHandler));
            _connectedOpCodeHandlers.Add(AppOpCode.MoveItem, new OpCodeHandler(HandleMoveItem));
            _connectedOpCodeHandlers.Add(AppOpCode.SpawnAppearance, new OpCodeHandler(HandleSpawnAppearance));
            _connectedOpCodeHandlers.Add(AppOpCode.Jump, new OpCodeHandler(HandleJump));
            _connectedOpCodeHandlers.Add(AppOpCode.ReadBook, new OpCodeHandler(HandleReadBook));
            _connectedOpCodeHandlers.Add(AppOpCode.AutoAttack, new OpCodeHandler(HandleAutoAttack));
            _connectedOpCodeHandlers.Add(AppOpCode.AutoAttack2, new OpCodeHandler(BlackHoleHandler));
            _connectedOpCodeHandlers.Add(AppOpCode.TargetMouse, new OpCodeHandler(HandleTargetCmd));
            _connectedOpCodeHandlers.Add(AppOpCode.TargetCommand, new OpCodeHandler(HandleTargetCmd));
            _connectedOpCodeHandlers.Add(AppOpCode.LootRequest, new OpCodeHandler(HandleLootRequest));
            _connectedOpCodeHandlers.Add(AppOpCode.EndLootRequest, new OpCodeHandler(HandleEndLootRequest));
            _connectedOpCodeHandlers.Add(AppOpCode.LootItem, new OpCodeHandler(HandleLootItem));
            _connectedOpCodeHandlers.Add(AppOpCode.Damage, new OpCodeHandler(HandleDamage));
            _connectedOpCodeHandlers.Add(AppOpCode.EnvDamage, new OpCodeHandler(HandleEnvDamage));
            _connectedOpCodeHandlers.Add(AppOpCode.DeleteItem, new OpCodeHandler(HandleDeleteItem));
            _connectedOpCodeHandlers.Add(AppOpCode.ChannelMessage, new OpCodeHandler(HandleChannelMessage));
            _connectedOpCodeHandlers.Add(AppOpCode.ConsiderCorpse, new OpCodeHandler(HandleConsiderCorpse));
            _connectedOpCodeHandlers.Add(AppOpCode.MemorizeSpell, new OpCodeHandler(HandleMemorizeSpell));
            _connectedOpCodeHandlers.Add(AppOpCode.CastSpell, new OpCodeHandler(HandleCastSpell));
        }

        // Handles application packets specific to the zone server
        internal override void ApplicationPacketReceived(EQRawApplicationPacket packet, Client client)
        {
            try
            {
                if (packet.OpCode == AppOpCode.AppAck)
                {
                    //_log.Debug("Recv AppAck... ignoring.");
                    return;
                }

                if (client.ZonePlayer == null)
                {
                    // Assume this is a zone entry, if not we have issues
                    if (packet.OpCode == AppOpCode.ZoneEntry)
                        HandleZoneEntry(packet, client);
                    else
                        _log.Error("Expected ZoneEntry OpCdode... what's up with that?");
                }
                else
                {
                    switch (client.ZonePlayer.ConnectionState)
                    {
                        case ZoneConnectionState.Connecting:
                            if (_connectingOpCodeHandlers.ContainsKey(packet.OpCode))
                                _connectingOpCodeHandlers[packet.OpCode](packet, client);
                            else
                                _log.Warn("Received Unexpected Connecting Application OPCode: " + packet.OpCode);
                            break;
                        case ZoneConnectionState.Connected:
                            if (_connectedOpCodeHandlers.ContainsKey(packet.OpCode))
                                _connectedOpCodeHandlers[packet.OpCode](packet, client);
                            else
                                _log.Warn("Received Unexpected Connected Application OPCode: " + packet.OpCode);
                            break;
                        case ZoneConnectionState.LinkDead:
                        case ZoneConnectionState.Kicked:
                        case ZoneConnectionState.Disconnected:
                            break;
                        case ZoneConnectionState.ClientError:
                        case ZoneConnectionState.All:
                            _log.WarnFormat("Unhandled client connection state {0} when recv OPCode {1}", client.ZonePlayer.ConnectionState, packet.OpCode);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error("Error processing an application packet", ex);
            }
        }

        /// <summary>Initializes all data for a zone - spawns, zone points, AA, etc.</summary>
        private bool Init(ushort zoneId, ZoneInstanceType ziType)
        {
            // Load the zone along with associated zone points, spawns, spawn groups, spawn group entries and NPCs.
            // TODO: Probably will want to load this differently when zone state persistance is in (join to a state table for spawns?)
            DataLoadOptions dlo = new DataLoadOptions();
            dlo.LoadWith<Zone>(z => z.ZonePoints);
            dlo.LoadWith<Zone>(z => z.Doors);
            dlo.LoadWith<Zone>(z => z.Spawns);
            dlo.LoadWith<Internals.Data.Spawn>(s => s.SpawnGroup);
            dlo.LoadWith<SpawnGroup>(sg => sg.SpawnGroupEntries);
            dlo.AssociateWith<SpawnGroup>(sg => sg.SpawnGroupEntries.OrderBy(sge => sge.Chance));   // sort the spawnGroupEntries by chance
            dlo.LoadWith<SpawnGroupEntry>(sge => sge.Npc);
            dlo.LoadWith<Npc>(npc => npc.Loot);
            dlo.LoadWith<Loot>(l => l.LootEntries);
            dlo.LoadWith<LootEntry>(le => le.LootDrops);
            dlo.LoadWith<LootDrop>(ld => ld.Item);  // TODO: can we trim how much of an item comes back?

            using (EmuDataContext dbCtx = new EmuDataContext())
            {
                dbCtx.ObjectTrackingEnabled = false;    // only loading referential data
                dbCtx.LoadOptions = dlo;    // TODO: use profiler to make sure we get it all in one call
                //dbCtx.Log = new Log4NetWriter(typeof(ZoneServer));
                _zone = dbCtx.Zones.Single(z => z.ZoneID == zoneId);
            }
            
            this.Zone.InitNewZoneStruct();
            _instanceType = ziType;

            // Load map & water map
            _map = new Map();
            _waterMap = new WaterMap();
            try
            {
                if (!_map.LoadMapFromFile(_zone.ShortName))
                    return false;

                if (!_waterMap.LoadMapFromFile(_zone.ShortName))
                    _waterMap = null;
            }
            catch (Exception e)
            {
                _log.Error("Error during map loading", e);
            }

            _mobMgr.Init();     // Must be done prior to any doors, mobs or player init

            if (_zone.Doors.Count > 0)
            {
                foreach (Door d in _zone.Doors)
                    d.EntityId = GetNewEntityId();  // Assign each door an entity Id

                _log.InfoFormat("Loaded {0} doors", _zone.Doors.Count);
            }
            else
                _log.Warn("No doors loaded");
            

            // TODO: Load Spawn Conditions

            SpawnTimerCallback(null);   // Load spawns NOW
            _log.InfoFormat("Loaded {0} Spawn", _mobMgr.MobCount);

            // TODO: Implement zone state persistance

            // TODO: Load corpses

            // TODO: Load traps

            // TODO: Load ground spawns

            // TODO: Load objects (tradeskill containers, maybe books, etc.)

            _maxSpellId = WorldSvc.GetMaxSpellId();
            if (_maxSpellId == 0)
                _log.Error("Max SpellId equals zero.  Problem with spell loading in world?");

            // TODO: Load blocked spells

            // TODO: Load guilds, factions, titles, AA, Tributes, corpse timers?

            // TODO: Load merchant data

            // TODO: Load petition data

            // TODO: Load graveyard

            // TODO: Load timezone data

            // TODO: Implement server time keeping in world and then sync up here (see end of orig Init and bootup routines)

            // Just before we finish, start up the various timers
            _spawnTimer.Change(30000, SPAWN_TIMER_INTERVAL);    // We've already populated the spawn list, so wait a bit
            _spawnQueueTimer.Change(5000, SPAWN_QUEUE_TIMER_INTERVAL);  // wait 5 sec before worrying about queued spawn packets
            _doorTimer.Change(10000, DOOR_TIMER_INTERVAL);  // wait 10 sec before worrying about doors

            // Initialize weather timer
            if (_zone.Weather <= 3 && _zone.Weather > 0)
            {
                Random rand = new Random();
                _weatherTimer.Change(0, (rand.Next(1800, 7200) + 30) * 2000);
            }

            return true;
        }

        /// <summary>Unloads all data associated with the zone. Used when dynamic zone is shut down.</summary>
        private void Unload()
        {
            _staleAuthRemovalTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _shutdownTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _spawnTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _spawnQueueTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _weatherTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _doorTimer.Change(Timeout.Infinite, Timeout.Infinite);

            _zone = null;
            _map = null;
            _waterMap = null;
            _mobMgr.Clear();
        }

        /// <summary>Routine to process entities and such for the zone.  Shutdown signaled by null mob entity.</summary>
        /// <remarks>Some entities are processed via timers, others are processed here.</remarks>
        private void ZoneProc()
        {
            while (true)
            {
                if (_clients.Count == 0) {
                    if (_loaded)
                        Thread.Sleep(2000);
                    else
                        Thread.Sleep(5000);

                    continue;
                }

                try
                {
                    if (_shutdown)
                    {
                        _log.Debug("ZoneProc detected shutdown, exiting");
                        return;
                    }

                    _mobMgr.Process();

                    // TODO: Process Beacons, Groups, etc.
                }
                catch (Exception ex)
                {
                    _log.Error("ZoneProc error", ex);
                }

                Thread.Sleep(1);    // May help to yield a bit here
            }
        }

        /// <summary>Gets an identifier for new entities.  Thread safe.</summary>
        /// <remarks>Don't make this static.</remarks>
        internal short GetNewEntityId()
        {
            lock (_entityIdLock)
            {
                if (_lastEntityId > 1500)   // let's hope 1500 is a high enough number (yeah, it could hit higher than 1500 - but only once)
                {
                    _log.Debug("Entity IDs passed 1500... restarting from zero.");
                    _lastEntityId = 0;
                }

                _lastEntityId++;    // The initial bump over the last id (or zero) that was handed out

                // Make sure no entities have this id and cycle through ids
                foreach (Door d in this.Zone.Doors)
                    if (_lastEntityId == d.EntityId)
                        _lastEntityId++;

                if (_mobMgr[_lastEntityId] != null)
                    _lastEntityId++;
            }

            return _lastEntityId;
        }

        /// <summary>Queues the packet to all clients in zone with ackReq = true and reqConnState = Connected.</summary>
        /// <param name="skipSender">True is packet should NOT be queued to the sender.</param>
        internal void QueuePacketToClients(Mob sender, EQRawApplicationPacket packet, bool skipSender)
        {
            QueuePacketToClients(sender, packet, ZoneConnectionState.Connected, skipSender);
        }

        /// <summary>Sends the provided packet to all clients that meet the specified zone connection state.</summary>
        internal void QueuePacketToClients(Mob sender, EQRawApplicationPacket packet, ZoneConnectionState reqConnState, bool skipSender)
        {
            QueuePacketToClients(sender, packet, reqConnState, skipSender, true);
        }

        internal void QueuePacketToClients(Mob sender, EQRawApplicationPacket packet, ZoneConnectionState reqConnState, bool skipSender, bool ackReq)
        {
            foreach (Client client in this.GetConnectedClients())
            {
                if (client.ZonePlayer == null || (client.ZonePlayer == sender && skipSender))
                    continue;   // Don't send to a client before its zonePlayer is set or to self if skip set

                QueuePacketToClient(client, packet, ackReq, reqConnState);
            }
        }

        /// <summary>Sends an application packet to nearby clients.</summary>
        internal void QueuePacketToNearbyClients(Mob sender, EQRawApplicationPacket packet, float distance, bool skipSender)
        {
            QueuePacketToNearbyClients(sender, packet, distance, skipSender, true);
        }

        internal void QueuePacketToNearbyClients(Mob sender, EQRawApplicationPacket packet, float distance, bool skipSender, bool ackReq)
        {
            QueuePacketToNearbyClients(sender, packet, distance, skipSender, true, null);
        }

        internal void QueuePacketToNearbyClients(Mob sender, EQRawApplicationPacket packet, float distance, bool skipSender, bool ackReq, int[] skipIDs)
        {
            if (distance <= 0)
                distance = 600;

            float range = distance * distance;

            foreach (Client client in this.GetConnectedClients()) {
                if (client.ZonePlayer == null 
                    || (client.ZonePlayer == sender && skipSender) 
                    || (skipIDs != null && skipIDs.Contains(client.ZonePlayer.ID)))
                    continue;   // Don't send to a client before its zonePlayer is set or to self if skip set or if client is on the skip list

                // TODO: when messaging filters are in, add support here
                if (client.ZonePlayer.ConnectionState == ZoneConnectionState.Connected && sender.DistanceNoRoot(client.ZonePlayer) <= range)
                    QueuePacketToClient(client, packet, ackReq, ZoneConnectionState.Connected);
                //else
                //    _log.DebugFormat("{0} is too far from {1}, not sending {2}", client.ZonePlayer.Name, sender.Name, packet.OpCode);
            }
        }

        internal void QueuePacketToClientsByTarget(Mob sender, EQRawApplicationPacket packet, bool skipSender, bool ackReq)
        {
            foreach (Client client in this.GetConnectedClients()) {
                if (!skipSender || client.ZonePlayer != sender) {
                    // Not skipping the sender or the client isn't the sender anyways
                    if (client.ZonePlayer.TargetMob != null && client.ZonePlayer.TargetMob == sender) {
                        // this client has the sender targeted, so send the packet to them
                        QueuePacketToClient(client, packet, ackReq, ZoneConnectionState.Connected);
                        //_log.DebugFormat("Sent {0} by target to {1}.", packet.OpCode, client.ZonePlayer.Name);
                    }
                }
            }
        }

        internal void QueuePacketToClient(Client client, EQRawApplicationPacket appPacket, bool ackReq, ZoneConnectionState reqConnState)
        {
            // TODO: when messaging filters are in, add support here

            // See if the packet should be deferred for later sending when the client is "connected"
            if (reqConnState == ZoneConnectionState.All || client.ZonePlayer.ConnectionState == reqConnState)
                client.SendApplicationPacket(appPacket);
            else
                client.ZonePlayer.AddDeferredPacket(new DeferredPacket(appPacket, true));
        }

        internal void SendMessageIDToNearbyClients(Mob sender, MessageType mt, MessageStrings ms, bool skipSender, float distance, params string[] messages)
        {
            float range = distance * distance;
            foreach (Client c in this.GetConnectedClients()) {
                if ((!skipSender || sender != c.ZonePlayer) && c.ZonePlayer.DistanceNoRoot(sender) <= range) {
                    if (messages != null)
                        c.ZonePlayer.MsgMgr.SendMessageID((uint)mt, ms, string.Join("\0", messages));
                    else
                        c.ZonePlayer.MsgMgr.SendMessageID((uint)mt, ms);
                }
            }
        }

        private void AddMob(Mob mob)
        {
            if (mob.X > _map.MaxX || mob.X < _map.MinX || mob.Y > _map.MaxY || mob.Y < _map.MinY || mob.Z > _map.MaxZ || mob.Z < _map.MinZ)
            {
                _log.ErrorFormat("Tried to add mob outside the bounds of the map ({0}, {1}, {2}, {3})", mob.Name, mob.X, mob.Y, mob.Z);
                return;
            }

            _mobMgr.AddMob(mob);
        }

        private void AddNpc(NpcMob npcMob, bool sendSpawnPacket, bool dontQueue)
        {
            // TODO: try to see if we can get away with only one list for mobs & npcs... npcs are in the mob list, so hey, it might work
            //_npcMobs.Add(npcMob.NpcID, npcMob); // Added by id of the npc db entity
            AddMob(npcMob);
            
            if (sendSpawnPacket)
            {
                if (dontQueue)
                {
                    EQApplicationPacket<Internals.Packets.Spawn> spawnPack = new EQApplicationPacket<Internals.Packets.Spawn>(AppOpCode.NewSpawn, npcMob.GetSpawn());
                    QueuePacketToClients(null, spawnPack, true);
                    // TODO: raise spawn event
                }
                else
                    if (_clients.Count > 0) // no clients, no send anything
                    {
                        lock (((ICollection)_spawnQueue).SyncRoot)
                        {
                            _spawnQueue.Enqueue(npcMob.GetSpawn());
                        }
                    }

                // TODO: raise spawn event - nah, better place is where it actually spawns, yea?
            }
        }

        internal string GetUniqueMobName(string name)
        {
            bool[] used = new bool[300];
            int idx = 0;

            _mobMgr.MobListLock.EnterReadLock();
            try {
                foreach (Mob m in _mobMgr.AllMobs) {
                    if (m.Name.StartsWith(name, true, System.Globalization.CultureInfo.InvariantCulture)) {
                        idx = int.Parse(m.Name.Substring(m.Name.Length - 3, 3));
                        used[idx] = true;
                    }
                }
            }
            catch (Exception ex) {
                _log.Error("Error while searching the mobs list for unique names... ", ex);
            }
            finally {
                _mobMgr.MobListLock.ExitReadLock();
            }

            for (int i = 0; i < used.Length; i++)
            {
                if (!used[i])
                    return name + i.ToString("D3");
            }

            _log.ErrorFormat("Unable to find a unique name for {0}", name);
            return GetUniqueMobName(name + "!");
        }

        internal Door FindDoor(short ordinal)
        {
            foreach (Door d in this.Zone.Doors)
                if (d.Ordinal == ordinal)
                    return d;

            return null;
        }

        /// <summary>Finds nearest zone point without a specified zone.</summary>
        /// <returns>ZonePoint object nearest to the client's position.</returns>
        internal ZonePoint GetNearestZonePoint(float x, float y, float z, float maxDistance)
        {
            ZonePoint nearestZp = null;
            float nearestDist = float.MaxValue;
            float maxDist2 = maxDistance * maxDistance;
            float deltaX, deltaY, dist;

            foreach (ZonePoint zp in this.Zone.ZonePoints)
	        {
                deltaX = zp.X - x;
                deltaY = zp.Y - y;
                if (zp.X  == 999999 || zp.X == -999999)
                    deltaX = 0;
                if (zp.Y == 999999 || zp.Y == -999999)
                    deltaY = 0;

                dist = deltaX * deltaX + deltaY * deltaY;
                if (dist < nearestDist)
                {
                    nearestZp = zp;
                    nearestDist = dist;
                }
	        }

            if(nearestDist > maxDist2)
		        nearestZp = null;

            return nearestZp;
        }

        /// <summary>Finds nearest zone point with a specified zone with which to narrow the zone's zone points.</summary>
        /// <returns>ZonePoint object nearest to the client's position.</returns>
        internal ZonePoint GetNearestZonePoint(float x, float y, float z, int targetZoneId, float maxDistance)
        {
            ZonePoint nearestZp = null;
            float nearestDist = float.MaxValue;
            float maxDist2 = maxDistance * maxDistance;
            float deltaX, deltaY, dist;

            foreach (ZonePoint zp in this.Zone.ZonePoints)
            {
                if (zp.TargetZoneID == targetZoneId)
                {
                    deltaX = zp.X - x;
                    deltaY = zp.Y - y;
                    if (zp.X == 999999.0F || zp.X == -999999.0F)
                        deltaX = 0;
                    if (zp.Y == 999999.0F || zp.Y == -999999.0F)
                        deltaY = 0;

                    dist = deltaX * deltaX + deltaY * deltaY;
                    if (dist < nearestDist)
                    {
                        nearestZp = zp;
                        nearestDist = dist;
                    }
                }
            }

            if (nearestDist > 40000.0F && nearestDist < maxDist2)
            {
                // Someone is probably cheating
                _log.WarnFormat("Closest zone point for zone {0} using zone points {1}x {2}y {3}z is {4}. May need to update zone points table if arrival at correct spot fails.",
                    targetZoneId, x, y, z, nearestDist);
            }

            if (nearestDist > maxDist2)
                nearestZp = GetNearestZonePoint(x, y, z, ZONEPOINT_DETECTION_RANGE);   // Try without a zone

            return nearestZp;
        }

        /// <summary>Called when the client is allowed to zone.  Handles all which must happen when a client zones out.</summary>
        internal void ZoneClient(ZoneChange zc, Zone targetZone, float destX, float destY, float destZ, float destH, byte ignoreRestrictions, Client client)
        {
            this.SendLogoutPackets(client);

            _mobMgr.RemoveFromAllHateLists(client.ZonePlayer);

            // TODO: clear aggro for pet, if present

            _log.DebugFormat("{0} is ATTEMPTING to zone to {1} ({2}) {3}x {4}y {5}z", client.ZonePlayer.Name, targetZone.ShortName, targetZone.ZoneID,
                destX, destY, destZ);

            ZoneChange zcOut = new ZoneChange();
            if (targetZone.ZoneID == this.Zone.ZoneID)
            {
                // Zoning to same zone (maybe a bind point, etc.)
                zcOut.ZoneID = this.Zone.ZoneID;
                zcOut.Success = (int)ZoneError.Success;

                client.ZonePlayer.X = destX;
                client.ZonePlayer.Y = destY;
                client.ZonePlayer.Z = destZ;
                client.ZonePlayer.Heading = destH;

                _log.InfoFormat("{0} is zoning to same zone {1}x {2}y {3}z (no error)", client.ZonePlayer.Name, destX, destY, destZ);
                AddClientAuth(client.IPEndPoint.Address.ToString(), true);   // add to expected clients list
            }
            else
            {
                // Send a ZTZ to World
                ZoneToZone ztz = new ZoneToZone();
                ztz.CharName = client.ZonePlayer.Name;
                ztz.CharId = client.ZonePlayer.ID;
                ztz.ClientIp = client.IPEndPoint.Address.ToString();
                ztz.CurrentZoneId = this.Zone.ZoneID;
                ztz.RequestedZoneId = targetZone.ZoneID;
                ztz.AccountStatus = client.ZonePlayer.AccountStatus;
                ztz.IgnoreRestrictions = ignoreRestrictions;
                int ztzResult = WorldSvc.ZoneToZone(ztz);

                if (ztzResult > 0)
                {
                    // problems
                    zcOut.Success = (int)ZoneError.NotReady;
                    client.ZonePlayer.ZoneId = this.Zone.ZoneID;    // client isn't zoning after all, so set the id back to this zone

                    _log.InfoFormat("{0} is zoning to same zone {1}x {2}y {3}z due to error code {4} when asking world to zone",
                        client.ZonePlayer.Name, destX, destY, destZ, ztzResult);

                    client.ZonePlayer.MsgMgr.SendSpecialMessage(MessageType.Default, string.Format("There was a problem zoning.  Code {0}.", ztzResult));
                }
                else
                {
                    zcOut.Init();
                    Buffer.BlockCopy(Encoding.ASCII.GetBytes(client.ZonePlayer.Name), 0, zcOut.CharName, 0, client.ZonePlayer.Name.Length);
                    zcOut.ZoneID = targetZone.ZoneID;
                    zcOut.Success = (int)ZoneError.Success;

                    client.ZonePlayer.X = destX;
                    client.ZonePlayer.Y = destY;
                    client.ZonePlayer.Z = destZ;
                    client.ZonePlayer.Heading = destH;
                    client.ZonePlayer.ZoneId = targetZone.ZoneID;

                    _log.InfoFormat("{0} is zoning to {1} ({2}) {3}x {4}y {5}z", client.ZonePlayer.Name, targetZone.ShortName, targetZone.ZoneID,
                        destX, destY, destZ);

                    // TODO: for ignoreRestrictions of 3, get safe coords for target zone
                }
            }

            client.ZonePlayer.ZoneMode = ZoneMode.Unsolicited;  // reset the zoneMode
            client.ZonePlayer.Save();   // this forced save ensures the correct zone info is available to world when zoning the client

            EQApplicationPacket<ZoneChange> zcPack = new EQApplicationPacket<ZoneChange>(AppOpCode.ZoneChange, zcOut);
            client.SendApplicationPacket(zcPack);
        }

        internal void MovePlayer(ZonePlayer zp, uint zoneId, uint instanceId, float x, float y, float z, float heading, ZoneMode zm)
        {
            if (zoneId == this.Zone.ZoneID) {   // TODO: also test if the instance id is equal to this zone's
                if (zp.IsAIControlled) {
                    // TODO: quick move the pc
                    return;
                }

                // TODO: if they have a pet, quick move the pet to the new spot as well
            }

            zp.ZoneMode = zm;

            switch (zm) {
                case ZoneMode.EvacToSafeCoords:
                case ZoneMode.ZoneToSafeCoords:
                    // TODO: start cheat timer?
                    zp.ZoneSummonX = x;
                    zp.ZoneSummonY = y;
                    zp.ZoneSummonZ = z;
                    zp.Heading = heading;
                    break;
                case ZoneMode.GMSummon:
                    zp.MsgMgr.SendSpecialMessage(MessageType.Default, "You have been summoned by a GM!");
                    zp.ZoneSummonX = x;
                    zp.ZoneSummonY = y;
                    zp.ZoneSummonZ = z;
                    zp.Heading = heading;
                    zp.ZoneSummonID = (ushort)zoneId;
                    break;
                case ZoneMode.Solicited:
                    zp.ZoneSummonX = x;
                    zp.ZoneSummonY = y;
                    zp.ZoneSummonZ = z;
                    zp.Heading = heading;
                    zp.ZoneSummonID = (ushort)zoneId;
                    break;
                case ZoneMode.ZoneToBindPoint:
                    zp.ZoneId = (ushort)zoneId;
                    zp.X = x;
                    zp.Y = y;
                    zp.Z = z;
                    zp.Heading = heading;
                    _log.InfoFormat("Player {0} has died and will be zoned to bind point in zone {1} at LOC x={2}, y={3}, z={4}", zp.Name,
                        zoneId, x, y, z);

                    ZonePlayerToBind zptb = new ZonePlayerToBind(zoneId, x, y, z, heading, "Bind Location");
                    EQRawApplicationPacket zptbPack = new EQRawApplicationPacket(AppOpCode.ZonePlayerToBind, zp.Client.IPEndPoint, zptb.Serialize());
                    QueuePacketToClient(zp.Client, zptbPack, true, ZoneConnectionState.All);
                    return;
                case ZoneMode.Unsolicited:
                    throw new NotSupportedException("This type of player moving not supported yet.  Implement it!");
                    //break;
                case ZoneMode.GateToBindPoint:
                    throw new NotSupportedException("This type of player moving not supported yet.  Implement it!");
                    //break;
                case ZoneMode.SummonPC:
                    zp.MsgMgr.SendSpecialMessage(MessageType.Default, "You have been summoned!");
                    throw new NotSupportedException("This type of player moving not supported yet.  Implement it!");
                    //break;
            }

            // Handle Packet sending wasn't handled yet if we've gotten this far, so handle it now
            if (zm == ZoneMode.Solicited || zm == ZoneMode.ZoneToSafeCoords) {
                RequestClientZoneChange rczc = new RequestClientZoneChange()
                {
                    ZoneId = (ushort)zoneId,
                    X = x,
                    Y = y,
                    Z = z,
                    Heading = heading,
                    InstanceId = (ushort)instanceId,
                    Type = 0x01     // Might be meaningless... noted as an "observed value"
                };

                EQApplicationPacket<RequestClientZoneChange> rczcPack = new EQApplicationPacket<RequestClientZoneChange>(AppOpCode.RequestClientZoneChange, rczc);
                QueuePacketToClient(zp.Client, rczcPack, true, ZoneConnectionState.Connected);
            }
        }

        private void AddClientAuth(string clientIp, bool isLocal)
        {
            _authClientsLock.EnterWriteLock();
            try {
                // Don't add if already in auth list (might have crashed out and coming back in)
                if (!_authClients.ContainsKey(clientIp))
                    _authClients.Add(clientIp, new ClientAuth(clientIp, isLocal, DateTime.Now));
                else {
                    _authClients[clientIp].TimeAdded.AddMinutes(1);  // bump thier auth window
                    _log.WarnFormat("Client {0} already found in auth list during ExpectNewClient inter-server call.  Perhaps client crashed out, etc. the first time.",
                        clientIp);
                }

                _shutdownTimer.Change(Timeout.Infinite, Timeout.Infinite);  // disable shut down timer TODO: add logic for static and instanced zones
                _log.DebugFormat("Shutdown timer disabled - new client coming in.");

                if (_authClients.Count == 1)     // only start the timer if this is the first one we've added
                    _staleAuthRemovalTimer.Change(CLIENT_AUTH_CLEANUP_INTERVAL, CLIENT_AUTH_CLEANUP_INTERVAL);
            }
            catch (Exception e) {
                _log.Error("Error trying to add an expected client.", e);
            }
            finally {
                _authClientsLock.ExitWriteLock();
                _clientInc = false;
            }
        }

        internal bool HasGraveyard()
        {
            return false;   // TODO: implement graveyard
        }

        /// <summary>Preliminary spell id validity check before making a world call.</summary>
        internal bool IsValidSpell(uint spellId)
        {
            return spellId > 1 && spellId != 0xFFFF && spellId <= _maxSpellId;
        }

        #region Callbacks
        private void StaleAuthRemovalTimerCallback(object state)
        {
            // remove stale expected clients list
            _authClientsLock.EnterWriteLock();
            try
            {
                List<ClientAuth> staleClients = _authClients.Values.Where(c => c.TimeAdded < DateTime.Now.AddMinutes(1)).ToList();
                foreach (ClientAuth ca in staleClients)
                {
                    _authClients.Remove(ca.ClientIp);
                    _log.DebugFormat("removed stale client {0} from auth list.", ca.ClientIp);
                }
            }
            catch (Exception e)
            {
                _log.Error("Error trying to remove a stale expected client.", e);
            }
            finally
            {
                if (_authClients.Count == 0)     // Stop timer til more clients come in
                {
                    _staleAuthRemovalTimer.Change(Timeout.Infinite, Timeout.Infinite);

                    if (_clients.Count == 0)
                        _shutdownTimer.Change(DYNAMIC_SHUTDOWN_TIMEOUT, Timeout.Infinite);  // a client may have bombed out when logging in
                }
                _authClientsLock.ExitWriteLock();
            }
        }

        private void ShutdownTimerCallback(object state)
        {
            lock (_loadedLock)
            {
                if (_clientInc)
                {
                    _log.Info("During the unload of a dynamic zone we found inbound clients.  Aborting shutdown.");
                    _shutdownTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    return;
                }

                _log.Info("Unloading dynamic zone.");
                Unload();
                _loaded = false;
            }

            TellWorldWeUnloaded(_port);
        }

        /// <summary>Processes the (re)spawning of entities in the zone.  Iterates the spawns for this zone and then adds a corresponding
        /// NPC to the mob list if various spawn conditions are met.</summary>
        private void SpawnTimerCallback(object state)
        {
            NpcMob npcMob = null;
            using (EmuDataContext dbCtx = new EmuDataContext())
            {
                dbCtx.ObjectTrackingEnabled = false;

                List<SkillCap> skillCaps = dbCtx.SkillCaps.OrderBy(sc => sc.SkillID).ThenBy(sc => sc.Class).ThenBy(sc => sc.Level).ToList();

                foreach (Internals.Data.Spawn s in this.Zone.Spawns)
                {
                    // TODO: Handle timeleft on spawn entity (may be able to wait until persistant zone state is in)

                    // TODO: Process spawn conditions

                    if (!s.ReadyForRespawn())
                        continue;

                    try
                    {
                        Npc npc = s.SpawnGroup.PickNPC();   // Get an Npc
                        if (npc != null)
                        {
                            npcMob = new NpcMob(GetNewEntityId(), npc, s.PathGrid, s.X, s.Y, s.Z, s.Heading);

                            // Clean up the npc's name and make it unique (def. before we add to list)
                            npcMob.Name = npcMob.Name.RemoveDigits();
                            npcMob.Name = GetUniqueMobName(npcMob.Name);

                            var npcSkillCaps = skillCaps.Where(sc => sc.Class == npcMob.Class && sc.Level == npcMob.Level);

                            foreach (SkillCap cap in npcSkillCaps)
                                npcMob.SetSkillLevel((Skill)cap.SkillID, cap.Level);

                            //_log.DebugFormat("Trying to add npc with entity id of {0}", npcMob.ID);
                            AddNpc(npcMob, true, false);
                            // TODO: add to npc limit list

                            s.LastSpawned = DateTime.Now;
                            // TODO: set roambox?
                            npcMob.LoadGrid(this.Zone.ZoneID, dbCtx);
                        }
                        else
                        {
                            s.LastSpawned = DateTime.MaxValue;  // better than tracking a separate list of deletes and delete routine?
                            _log.DebugFormat("Spawn {0} removed due to lack of NPCs, spawn groups or cumulative spawn chance of zero.", s.SpawnID);
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error("Error in SpawnTimerCallback", ex);
                    }
                }
            }
        }

        private void WeatherTimerCallback(object state)
        {
            Random rand = new Random();
            int changePct = rand.Next(0, 100);

            if (changePct > 80)
            {
                // change weather
                byte oldWeather = _zoneWeather;
                _zoneWeather = _zoneWeather == 0 ? (byte)_zone.Weather : (byte)0;
                _log.InfoFormat("weather for {0} has changed from {1} to {2}", _zone.ShortName, oldWeather, _zoneWeather);
                SendWeather();
            }
        }

        private void SpawnQueueTimerCallback(object state)
        {
            EQRawApplicationPacket packet = null;
            Internals.Packets.Spawn spawn;

            while (_spawnQueue.Count > 0)
            {
                try
                {
                    lock (((ICollection)_spawnQueue).SyncRoot)
                    {
                        spawn = _spawnQueue.Dequeue();
                    }

                    // send the packet
                    packet = new EQApplicationPacket<Internals.Packets.Spawn>(AppOpCode.NewSpawn, spawn);
                    QueuePacketToClients(null, packet, true);
                }
                catch (Exception e)
                {
                    _log.Error("SpawnQueueTimerCallback error", e);
                }
            }
        }

        private void DoorTimerCallback(object state)
        {
            // Set opened at to DateTime.MinValue
            foreach (Door d in this.Zone.Doors)
                if (DateTime.Now.Subtract(d.OpenedAt).TotalSeconds > DOOR_CLOSED_INTERVAL)
                    d.Close();
        }

        private void ZPSaveCallback(object state)
        {
            try {
                ZonePlayer zp = state as ZonePlayer;
                zp.Save();
            }
            catch (Exception e) {
                _log.Error("Save error on zone player object.", e);
            }
        }
        #endregion

        #region Event Handlers

        #endregion
    }
}
