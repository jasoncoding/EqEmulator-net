using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.Text;
using System.Threading;

using EQEmulator.Servers.Internals;
using EQEmulator.Servers.Internals.Data;
using EQEmulator.Servers.Internals.Entities;
using EQEmulator.Servers.Internals.Packets;
using EQEmulator.Servers.Properties;
using EQEmulator.Servers.ServerTalk;

namespace EQEmulator.Servers
{
    [ServiceBehavior(InstanceContextMode=InstanceContextMode.Single, ConcurrencyMode=ConcurrencyMode.Multiple)]
    public partial class WorldServer : EQServer
    {
        private const short DEFAULT_PORT = 9000;
        private const short COMPRESSION_BUFFER_SIZE = 2048;
        private const int CLIENT_AUTH_WINDOW = 1;
        private const int CLIENT_AUTH_CLEANUP_INTERVAL = 60000;     // how often we clean the auth list (one minute)

        private static ServerConfig _serverCfg = null;

        private int _worldId = 0;
        private Internals.Data.WorldServer _worldServer = null;
        private Dictionary<string, ClientWho> _authClients = new Dictionary<string, ClientWho>(10);    // keyed by ip of expected new clients from LS - doubles as who info
        private ReaderWriterLockSlim _authClientsLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        //private Timer _expClientsStaleRemoval;  // may need a lock for this but probably not that big of a deal
        private Dictionary<int, ZoneProcess> _zoneProcesses = null;     // zone processes indexed by port number
        private ReaderWriterLockSlim _zoneProcListLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private List<SkillCap> _skillCaps = null;
        private List<Spell> _spells = null;

        public WorldServer(int worldId, int port) : base(port)
        {
            _worldId = worldId;
            //_expClientsStaleRemoval = new Timer(new TimerCallback(ExpClientsStaleRemovalTimerCallback), null, Timeout.Infinite, Timeout.Infinite);
        }

        internal override void ServerStarted()
        {
            // Load all data that the world server makes centrally available to the zone processes
            using (EmuDataContext dbCtx = new EmuDataContext())
            {
                dbCtx.ObjectTrackingEnabled = false;
                _worldServer = dbCtx.WorldServers.Single(p => p.WorldConfigID == 1);
                _skillCaps = dbCtx.SkillCaps.Where(sc => sc.Level <= WorldServer.ServerConfig.LevelCap).ToList();
                _spells = dbCtx.Spells.ToList();
            }

            if (_worldServer == null)
                throw new ApplicationException("Server started with an invalid worldId.  Check to make sure the worldId in the app.config is present in the database");

            _log.InfoFormat("Loaded {0} skill caps.", _skillCaps.Count);
            _log.InfoFormat("Loaded {0} spells.", _spells.Count);
            
            _zoneProcesses = new Dictionary<int, ZoneProcess>(ServerConfig.InitNumZoneServers + 5);
            
            // Spawn some zone servers  TODO: crawl zone table and instance any static zones
            SpinUpZoneProcesses(ServerConfig.InitNumZoneServers);

            base.ServerStarted();
        }

        internal override void ServerStopping()
        {
            // release resources for tracked zone processes
            foreach (KeyValuePair<int, ZoneProcess> kvp in _zoneProcesses)
            {
                if (!kvp.Value.Process.HasExited)
                    kvp.Value.Process.Kill();
                kvp.Value.Process.Close();
            }

            base.ServerStopping();
        }

        internal static ServerConfig ServerConfig
        {
            get
            {
                if (_serverCfg == null) {
                    using (EmuDataContext dbCtx = new EmuDataContext()) {
                        dbCtx.ObjectTrackingEnabled = false;
                        _serverCfg = dbCtx.ServerConfigs.Single();
                    }
                }

                return _serverCfg;
            }
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

        // Handles application packets specific to the world server
        internal override void ApplicationPacketReceived(EQRawApplicationPacket packet, Client client)
        {
            // TODO: if client does not have an account id (has negotiated a SendLoginInfo) and isn't sending one, return (and set state to closed?)

            try
            {
                ProcessWorldPacket(packet, client);
            }
            catch (Exception ex)
            {
                _log.Error("Error processing an application packet", ex);
            }
        }

        private void ProcessWorldPacket(EQRawApplicationPacket packet, Client client)
        {
            switch (packet.OpCode)
            {
                case AppOpCode.None:
                    _log.Error("Application OpCode found not set during packet processing... please fix.");
                    break;
                case AppOpCode.SendLoginInfo:
                    EQApplicationPacket<LoginInfo> loginInfo = new EQApplicationPacket<LoginInfo>(packet);
                    // TODO: figure out what the deal with these is... and how to send them from the LS
                    string name = Encoding.ASCII.GetString(loginInfo.PacketStruct.LoginInfoData, 0, 18);    // max len for name is 18
                    string password = Encoding.ASCII.GetString(loginInfo.PacketStruct.LoginInfoData, name.Length, 15);  // 15 for password
                    //_log.DebugFormat("Recv SendLoginInfo - name: {0} password: {1}.", name, password);

                    // the following is useless unless login server encryption is grok'd (don't know how the name and password is pushed out from LS)
                    //int id = 0;
                    //if (!int.TryParse(name.Substring(0, name.IndexOf('0')), out id) || id == 0)
                    //    _log.Warn("name couldn't be parsed as an int");

                    if (AuthenticateNewClient(ref client))
                    {
                        if (loginInfo.PacketStruct.zoning == 1)
                            client.WorldPlayer.OnlineStatus = WorldOnlineState.Zoning;

                        if (!(client.WorldPlayer.OnlineStatus == WorldOnlineState.Zoning))
                            SendGuildsList(client);

                        SendLogServer(client);
                        SendApproveWorld(client);
                        SendEnterWorld(client);
                        SendPostEnterWorld(client);

                        if (!(client.WorldPlayer.OnlineStatus == WorldOnlineState.Zoning))
                        {
                            SendExpansionInfo(client);
                            SendCharInfo(client);
                        }
                    }
                    else
                    {
                        _log.InfoFormat("Client at {0} attempted to connect but was not found among expected clients... closing.", client.IPEndPoint.ToString());
                        client.Close();
                    }

                    break;
                case AppOpCode.RandomNameGenerator:
                    NameGeneration ngStruct = new NameGeneration();
                    ngStruct.Name = new byte[64];
                    Buffer.BlockCopy(Encoding.ASCII.GetBytes("SorryPickYerOwn"), 0, ngStruct.Name, 0, 15);
                    EQApplicationPacket<NameGeneration> ngPacket = new EQApplicationPacket<NameGeneration>(AppOpCode.RandomNameGenerator, ngStruct, client.IPEndPoint);
                    client.SendApplicationPacket(ngPacket);
                    break;
                case AppOpCode.ApproveName:
                    string charName = Encoding.ASCII.GetString(packet.GetPayload(), 0, 64).TrimEnd('\0');
                    byte charRace = packet.GetPayload()[64];
                    byte charClass = packet.GetPayload()[68];

                    _log.DebugFormat("Recv name approval request.  Name:{0}, Race:{1}, Class:{2}", charName, (CharRaces)charRace, (CharClasses)charClass);
                    //_log.Debug(BitConverter.ToString(packet.GetPayload()));
			        
                    bool valid;
                    if (char.IsLower(charName, 0))    // must begin with upper case
                        valid = false;
                    else if (!Character.CheckNameFilter(charName))
				        valid = false;
                    else if (!Character.ReserveName(client.WorldPlayer.AccountId, charName))
                        valid = false;
                    else
                    {
                        valid = true;
                        client.WorldPlayer.CharName = charName;
                    }

                    EQRawApplicationPacket anPacket = new EQRawApplicationPacket(AppOpCode.ApproveName, client.IPEndPoint, BitConverter.GetBytes(valid));
                    client.SendApplicationPacket(anPacket);
                    break;
                case AppOpCode.CharacterCreate:
                    EQApplicationPacket<CharacterCreate> charCre = new EQApplicationPacket<CharacterCreate>(packet);

                    bool created = true;
                    try
                    {
                        Character.Create(client.WorldPlayer.CharName, charCre.PacketStruct);
                    }
                    catch (Exception e)
                    {
                        created = false;
                        _log.Error("character creation failed.", e);
                    }
                    
                    if (!created)
                    {
                        Character.Delete(client.WorldPlayer.CharName);
                        client.WorldPlayer.CharName = string.Empty;
                        EQRawApplicationPacket badCreatePacket = new EQRawApplicationPacket(AppOpCode.ApproveName, client.IPEndPoint, BitConverter.GetBytes(false));
                        client.SendApplicationPacket(badCreatePacket);
                    }
                    else
                        _log.InfoFormat("Character created by {0}: Name:{1}, Race:{2}, Class:{3}, Gender:{4}, Deity:{5}, Start Zone:{6}",
                            client.IPEndPoint.ToString(), client.WorldPlayer.CharName, (CharRaces)charCre.PacketStruct.Race, (CharClasses)charCre.PacketStruct.Class,
                            charCre.PacketStruct.Gender, charCre.PacketStruct.Deity, charCre.PacketStruct.StartZone);

                    SendCharInfo(client);

                    break;
                case AppOpCode.EnterWorld:
                    EQApplicationPacket<EnterWorld> ewPacket = new EQApplicationPacket<EnterWorld>(packet);
                    charName = Encoding.ASCII.GetString(ewPacket.PacketStruct.Name).TrimEnd('\0');
                    _log.DebugFormat("Recv EnterWorld request name: {0}, returnHome: {1}, tutorial: {2}", charName,
                        ewPacket.PacketStruct.ReturnHome, ewPacket.PacketStruct.Tutorial);

                    // Get character
                    Character toon = null;
                    using (EmuDataContext dbCtx = new EmuDataContext())
                        toon = dbCtx.Characters.SingleOrDefault(c => c.Name == charName);

                    if (toon == null)
                    {
                        _log.Debug("Couldn't find character in database");
                        client.Close();
                        break;
                    }

                    client.WorldPlayer.ZoneId = toon.ZoneID.Value;
                    client.WorldPlayer.ZoneName = toon.ZoneName;
                    client.WorldPlayer.CharName = toon.Name;

                    if (client.WorldPlayer.OnlineStatus != WorldOnlineState.Zoning)
                        toon.GroupID = 0;
                    else
                    {
                        // TODO: a bunch of shit for setting a char's group
                    }
                    
                    // Send the server's MOTD
                    byte[] motdBuf = Encoding.ASCII.GetBytes(ServerConfig.MOTD ?? "\0");
                    EQRawApplicationPacket motdPacket = new EQRawApplicationPacket(AppOpCode.MOTD, client.IPEndPoint, motdBuf);
                    client.SendApplicationPacket(motdPacket);

                    // Send chat server garbage
                    byte[] chatSvrBuf = new byte[112];
                    StringBuilder sb = new StringBuilder(ServerConfig.ChatHost + '\0', 112);
                    sb.Append(ServerConfig.ChatPort);
                    sb.Append(ServerConfig.ShortName + '\0');
                    sb.Append(client.WorldPlayer.CharName + "067a79d4");
                    //_log.DebugFormat("string: {0}, length: {1}", sb.ToString(), sb.ToString().Length);
                    Buffer.BlockCopy(Encoding.ASCII.GetBytes(sb.ToString()), 0, chatSvrBuf, 0, sb.ToString().Length);

                    EQRawApplicationPacket chat1Packet = new EQRawApplicationPacket(AppOpCode.SetChatServer, client.IPEndPoint, chatSvrBuf);
                    client.SendApplicationPacket(chat1Packet);

                    // Send chat server2 garbage
                    chatSvrBuf = new byte[112];
                    sb = new StringBuilder(ServerConfig.MailHost + '\0', 112);
                    sb.Append(ServerConfig.MailPort);
                    sb.Append(ServerConfig.ShortName + '\0');
                    sb.Append(client.WorldPlayer.CharName + "067a79d4");
                    Buffer.BlockCopy(Encoding.ASCII.GetBytes(sb.ToString()), 0, chatSvrBuf, 0, sb.ToString().Length);

                    EQRawApplicationPacket chat2Packet = new EQRawApplicationPacket(AppOpCode.SetChatServer2, client.IPEndPoint, chatSvrBuf);
                    client.SendApplicationPacket(chat2Packet);

                    EnterWorld(client);
                    break;
                case AppOpCode.WearChange:  // user has selected a different character
                case AppOpCode.CrashDump:
                case AppOpCode.AppAck:
                case AppOpCode.ApproveWorld:
                case AppOpCode.WorldClientCRC1:
                case AppOpCode.WorldClientCRC2:
                case AppOpCode.WorldClientReady:
                    _log.DebugFormat("Received Application OPCode {0}... ignoring.", packet.OpCode);
                    break;
                case AppOpCode.WorldComplete:   // sent when client is done with world and heading to zone (maybe send a disconnect next?)
                    client.Close();
                    break;
                case AppOpCode.DeleteCharacter:
                    string charNameToDel = Encoding.ASCII.GetString(packet.GetPayload()).TrimEnd('\0');
                    _log.InfoFormat("Deleting character {0}", charNameToDel);
                    try
                    {
                        if (Character.Delete(charNameToDel))
                            _log.DebugFormat("{0} was successfully deleted.", charNameToDel);
                        else
                            _log.DebugFormat("{0} was NOT deleted.", charNameToDel);
                    }
                    catch (Exception e)
                    {
                        _log.ErrorFormat("Unable to delete character {0}... reason: {1}", charNameToDel, e.Message);
                    }

                    SendCharInfo(client);

                    break;
                default:
                    _log.Warn("Received Unexpected Application OPCode: " + packet.OpCode);
                    break;
            }
        }

        private void SendGuildsList(Client client)
        {
            EQRawApplicationPacket guildPacket = new EQRawApplicationPacket(AppOpCode.GuildsList, client.IPEndPoint, Guild.ListGuilds());
            //_log.Debug("Sending GuildsList: " + Encoding.ASCII.GetString(guildPacket.GetPayload(), 0, 300));
            lock(client.syncRoot)
                client.SendApplicationPacket(guildPacket);
        }

        private void SendLogServer(Client client)
        {
            LogServer logSvr = new LogServer();
            logSvr.WorldShortName = new byte[32];
            string shortName = ServerConfig.ShortName;
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(shortName), 0, logSvr.WorldShortName, 0, shortName.Length);
            EQApplicationPacket<LogServer> logSvrPacket = new EQApplicationPacket<LogServer>(AppOpCode.LogServer, logSvr, client.IPEndPoint);
            lock (client.syncRoot)
                client.SendApplicationPacket(logSvrPacket);
        }

        private void SendApproveWorld(Client client)
        {
            // send some hardcoded bytes, no one has specifically laid out exactly what the parts are (nor does it matter, apparently)
            byte[] buffer = {
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x37,0x87,0x13,0xbe,0xc8,0xa7,0x77,0xcb,
                0x27,0xed,0xe1,0xe6,0x5d,0x1c,0xaa,0xd3,0x3c,0x26,0x3b,0x6d,0x8c,0xdb,0x36,0x8d,
                0x91,0x72,0xf5,0xbb,0xe0,0x5c,0x50,0x6f,0x09,0x6d,0xc9,0x1e,0xe7,0x2e,0xf4,0x38,
                0x1b,0x5e,0xa8,0xc2,0xfe,0xb4,0x18,0x4a,0xf7,0x72,0x85,0x13,0xf5,0x63,0x6c,0x16,
                0x69,0xf4,0xe0,0x17,0xff,0x87,0x11,0xf3,0x2b,0xb7,0x73,0x04,0x37,0xca,0xd5,0x77,
                0xf8,0x03,0x20,0x0a,0x56,0x8b,0xfb,0x35,0xff,0x59,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x15,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x53,0xC3,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x01,0x00,0x00,0x00
                };

            EQRawApplicationPacket awPacket = new EQRawApplicationPacket(AppOpCode.ApproveWorld, client.IPEndPoint, buffer);
            lock (client.syncRoot)
                client.SendApplicationPacket(awPacket);
        }

        private void SendEnterWorld(Client client)
        {
            byte[] nameBuf = new byte[32];
            
            // if zoning, old emu checks that current client's account and character_ tables matched on IDs (not sure if this is necessary)
            if (client.WorldPlayer.OnlineStatus == WorldOnlineState.Zoning)
            {
                byte[] charBuf = Encoding.ASCII.GetBytes(client.WorldPlayer.CharName);
                Buffer.BlockCopy(charBuf, 0, nameBuf, 0, charBuf.Length);
                _log.InfoFormat("Telling client ({0}) to continue session.", client.WorldPlayer.CharName);
            }

            EQRawApplicationPacket entWorldPacket = new EQRawApplicationPacket(AppOpCode.EnterWorld, client.IPEndPoint, nameBuf);
            lock (client.syncRoot)
                client.SendApplicationPacket(entWorldPacket);
        }

        private void SendPostEnterWorld(Client client)
        {
            EQRawApplicationPacket pewPacket = new EQRawApplicationPacket(AppOpCode.PostEnterWorld, client.IPEndPoint, new byte[] {0x00});
            //_log.DebugFormat("PostEnterWorld Bytes Sent Dump: {0}", BitConverter.ToString(pewPacket.RawPacketData));
            lock (client.syncRoot)
                client.SendApplicationPacket(pewPacket);
        }

        private void SendExpansionInfo(Client client)
        {
            byte[] expInfoBuf = BitConverter.GetBytes(ServerConfig.Expansions);     // old default = 511 (0x1FF - all expansions)
            EQRawApplicationPacket seiPacket = new EQRawApplicationPacket(AppOpCode.ExpansionInfo, client.IPEndPoint, expInfoBuf);
            //_log.DebugFormat("ExpansionInfo Bytes Sent Dump: {0}", BitConverter.ToString(seiPacket.RawPacketData));
            lock (client.syncRoot)
                client.SendApplicationPacket(seiPacket);
        }

        private void SendCharInfo(Client client)
        {
            CharacterSelect charSel = Character.GetCharSelectData(client.WorldPlayer.AccountId);
            EQApplicationPacket<CharacterSelect> csPacket = new EQApplicationPacket<CharacterSelect>(AppOpCode.SendCharInfo, charSel, client.IPEndPoint);

            //_log.DebugFormat("CharacterSelect Bytes Sent Dump: {0}", BitConverter.ToString(csPacket.RawPacketData));
            lock (client)
            {
                client.WorldPlayer.OnlineStatus = WorldOnlineState.CharSelect;
                client.SendApplicationPacket(csPacket);
            }
        }

        /// <summary>Queries the expected clients collection for the client that wants to connect.</summary>
        private bool AuthenticateNewClient(ref Client client)
        {
            _authClientsLock.EnterReadLock();    // kind of a messy lock but really shouldn't matter unless there is an onslaught of account creations
            try
            {
                if (_authClients.ContainsKey(client.IPEndPoint.Address.ToString()))
                {
                    // Good, now check for an existing account
                    using (EmuDataContext dbCtx = new EmuDataContext())
                    {
                        string hostName = this.GetClientAddress(client, _worldServer.IPAddress);    // conveniently gets a netbios name for locals
                        Account acct = dbCtx.Accounts.SingleOrDefault(a => a.IPAddress == hostName);

                        if (acct == null)
                        {
                            // Create the initial account details
                            acct = new Account();
                            acct.IPAddress = hostName;
                            acct.Status = ServerConfig.NewUserDefaultStatus;
                            dbCtx.Accounts.InsertOnSubmit(acct);
                            dbCtx.SubmitChanges();

                            // Prime the client's player info (use IP for address, not hostName which may have the netbios name)
                            client.WorldPlayer = new WorldPlayer(acct.AccountID, acct.Status, string.Empty, _authClients[client.IPEndPoint.Address.ToString()].IsLocal);
                        }
                        else
                            client.WorldPlayer = new WorldPlayer(acct.AccountID, acct.Status, acct.CharName, _authClients[client.IPEndPoint.Address.ToString()].IsLocal);
                    }

                    return true;
                }
                else
                    return false;
            }
            finally
            {
                _authClientsLock.ExitReadLock();
            }
        }

        private void EnterWorld(Client client)
        {
            ZoneProcess zp = GetZoneProcess(client.WorldPlayer.ZoneId);
            if (zp == null)
            {
                SendZoneUnavail(client);
                return;
            }

            // TODO: wrap in a 5-10 sec try for client inc?
            int retCode = 0;
            if (client.WorldPlayer.OnlineStatus != WorldOnlineState.Zoning) // No need to call again if zoning - ENC was called in the ZoneToZone
                retCode = zp.ZoneService.ExpectNewClient(client.WorldPlayer.CharId, client.IPEndPoint.Address.ToString(), client.WorldPlayer.IsLocalNet);

            if (retCode == 0)
                _log.InfoFormat("{0} {1} {2}", client.WorldPlayer.CharName, 
                    client.WorldPlayer.OnlineStatus == WorldOnlineState.CharSelect ? "entering zone" : "zoning to", client.WorldPlayer.ZoneName);
            else
            {
                _log.ErrorFormat("Attempt to enter zone {0} failed with error code {1}", client.WorldPlayer.ZoneName, retCode);
                SendZoneUnavail(client);    // This means the client couldn't log in... TODO: should we move the player to some default zone?
                return;
            }

            // Send a zoneServerInfo packet
            ZoneServerInfo zsiStruct = new ZoneServerInfo();
            zsiStruct.IP = new byte[128];
            byte[] zoneAddrBuf = null;
            if (client.WorldPlayer.IsLocalNet)
                zoneAddrBuf = Encoding.ASCII.GetBytes(ServerConfig.ZoneLocalAddress);
            else
                zoneAddrBuf = Encoding.ASCII.GetBytes(ServerConfig.ZoneAddress);

            Buffer.BlockCopy(zoneAddrBuf, 0, zsiStruct.IP, 0, zoneAddrBuf.Length);
            zsiStruct.Port = (ushort)zp.Port;
            EQApplicationPacket<ZoneServerInfo> zsiPacket = new EQApplicationPacket<ZoneServerInfo>(AppOpCode.ZoneServerInfo, zsiStruct);
            client.SendApplicationPacket(zsiPacket);
            client.WorldPlayer.OnlineStatus = WorldOnlineState.Zoning;   // TODO: should zone send back something to change this to InZone?

            // Set the account's current character
            using (EmuDataContext dbCtx = new EmuDataContext())
            {
                Account acct = dbCtx.Accounts.SingleOrDefault(a => a.AccountID == client.WorldPlayer.AccountId);

                if (acct == null)
                    _log.ErrorFormat("Unable to locate {0}'s account information (acctId {1}) in EnterWorld.  WTF.",
                        client.WorldPlayer.CharName, client.WorldPlayer.AccountId);
                else
                {
                    // TODO: optimize into a single sproc call
                    acct.CharName = client.WorldPlayer.CharName;
                    dbCtx.SubmitChanges();
                }
            }
        }

        private void SendZoneUnavail(Client client)
        {
            ZoneUnavailable zuStruct = new ZoneUnavailable();
            zuStruct.ZoneName = new byte[16];
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(client.WorldPlayer.ZoneName), 0, zuStruct.ZoneName, 0, client.WorldPlayer.ZoneName.Length);
            EQApplicationPacket<ZoneUnavailable> zuPacket = new EQApplicationPacket<ZoneUnavailable>(AppOpCode.ZoneUnavail, zuStruct, client.IPEndPoint);
            client.SendApplicationPacket(zuPacket);

            client.WorldPlayer.ZoneId = 0;
        }

        /// <summary>Retrieves the matching zone process from the list of running zones or spins up a new zone on demand.</summary>
        /// <returns>Null if there was a problem booting the zone.</returns>
        private ZoneProcess GetZoneProcess(ushort zoneId)
        {
            if (zoneId < 1)
                throw new ArgumentException(string.Format("invalid zoneId parameter {0}", zoneId), "zoneId");

            ZoneProcess zp = null;
            Zone zone = null;
            using (EmuDataContext dbCtx = new EmuDataContext())
            {
                dbCtx.ObjectTrackingEnabled = false;    // only loading referential data
                zone = dbCtx.Zones.SingleOrDefault(z => z.ZoneID == zoneId);
            }

            if (zone == null)
                throw new ApplicationException("Unable to find zone {0}" + zoneId.ToString());

            // see if we already have this zone spun up.  TODO: This logic will have to change for instanced zones.
            _zoneProcListLock.EnterReadLock();
            try
            {
                foreach (KeyValuePair<int, ZoneProcess> kvp in _zoneProcesses)
                {
                    if (kvp.Value.ZoneId == zoneId)
                    {
                        zp = kvp.Value;     // found the zone currently running
                        break;
                    }
                    else if (kvp.Value.ZoneId == null)
                    {
                        if (zp == null)
                            zp = kvp.Value;   // empty zone process ready to go (if we don't locate the zone) - should ALWAYS at least find one
                        // Don't break here... keep looking for a running zone
                    }
                }

                if (zp == null)
                    _log.WarnFormat("Unable to find a loaded zone matching zoneId {0} or empty zone process.  Try increasing the port ranges in the server config to allow for more zone server processes", zoneId);
                else
                {
                    if (zp.ZoneId == null)
                    {
                        // Don't have this zone spun up yet, so spin it up
                        _log.InfoFormat("Booting up zoneId {0} ({1})", zoneId, zone.ShortName);
                        // TODO: may want to make this async to exit lock sooner, or not?
                        if (!zp.ZoneService.BootUp(zoneId, zone.ShortName, ZoneInstanceType.Dynamic))
                        {
                            _log.Error("Bad response from zone on a bootup call.");   // TODO: would we rather catch an exception w/ richer info?
                            return null;
                        }

                        zp.ZoneId = zoneId;     // set the zoneId

                        // see if we've exhausted our "empty" zone process and spin some new ones up if needed
                        ThreadPool.QueueUserWorkItem(new WaitCallback(AdjustZoneProcesses));    // not a recursive lock, thread will just block until we exit lock - which is next
                    }
                }
            }
            finally
            {
                _zoneProcListLock.ExitReadLock();
            }

            return zp;
        }

        /// <summary>Makes sure the zone processes list has at least one empty and ready to go ZoneProcess.  Respects limits in server config.</summary>
        private void AdjustZoneProcesses(Object stateInfo)
        {
            bool hasFreeZP = false;

            _zoneProcListLock.EnterReadLock();
            try
            {
                foreach (KeyValuePair<int, ZoneProcess> kvp in _zoneProcesses)
                {
                    if (kvp.Value.ZoneId == null)
                    {
                        hasFreeZP = true;
                        break;
                    }
                }
            }
            catch(Exception e)
            {
                _log.Error("Error looking for empty zone processes.", e);
            }
            finally
            {
                _zoneProcListLock.ExitReadLock();
            }

            if (!hasFreeZP)
                SpinUpZoneProcesses(1);
        }

        /// <summary>Spins up the specified amount of zone processes.  Reuses port numbers.</summary>
        /// <remarks>Small chance that we could end up spinning up two at once (not so big of a deal).</remarks>
        private void SpinUpZoneProcesses(int num)
        {
            _log.DebugFormat("Spinning up {0} new zone processes.", num);

            Process process = null;
            short port = ServerConfig.ZonePortLow;  // start searching for free port numbers at the low end of the config range
            ChannelFactory<IZoneService> zoneSvcChanFact = null;
            EndpointAddress zoneSvcEA = null;

            for (short i = 0; i < num; i++)
            {
                // search for a free port
                _zoneProcListLock.EnterWriteLock();
                try
                {
                    for (int portIdx = 0; portIdx < ServerConfig.ZonePortHigh; portIdx++)
                    {
                        if (!_zoneProcesses.ContainsKey(port + portIdx))
                        {
                            // found a free port we can use
                            process = new Process();
                            process.StartInfo.FileName = System.IO.Path.Combine(Environment.CurrentDirectory, "ZoneServerLauncher.exe");
                            process.StartInfo.Arguments = (port + portIdx).ToString();
                            //TODO: turn on process.StartInfo.CreateNoWindow = true;
                            if (process.Start())
                            {
                                // Gen the WCF client proxy for the zone
                                zoneSvcEA = new EndpointAddress("net.tcp://localhost:" + (port + portIdx).ToString() + "/ZoneService/");
                                zoneSvcChanFact = new ChannelFactory<IZoneService>(new NetTcpBinding(), zoneSvcEA);
                                _zoneProcesses.Add(port + portIdx, new ZoneProcess((short)(port + portIdx), process, zoneSvcChanFact));

                                _log.InfoFormat("Tracking new zone process started on port {0}.", port + portIdx);
                                Thread.Sleep(500);
                                break;
                            }
                            else
                                _log.Fatal("Error starting zone process - process not started or reused");
                        }
                        else
                        {
                            if (portIdx == ServerConfig.ZonePortHigh - 1)
                                _log.WarnFormat("Running at max amount of zone processes ({0}). Consider increasing the port range in the server config.",
                                    _zoneProcesses.Count);
                        }
                    }
                }
                catch(Exception e)
                {
                    _log.Error("Error during the spin up of zone processes.", e);
                }
                finally
                {
                    _zoneProcListLock.ExitWriteLock();
                }
            }
        }
    }
}