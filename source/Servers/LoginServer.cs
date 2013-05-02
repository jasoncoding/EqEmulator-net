using System;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.Text;
using System.Threading;

using EQEmulator.Servers.Internals;
using EQEmulator.Servers.Internals.Data;
using EQEmulator.Servers.Internals.Packets;
using EQEmulator.Servers.ServerTalk;

namespace EQEmulator.Servers
{
    public class LoginServer : EQServer
    {
        private const short DEFAULT_PORT = 5998;
        private readonly byte[] CHAT_MSG = new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x65, 0x00, 0x00, 0x00, 0x00, 0x43, 0x68, 0x61, 0x74, 0x4d, 0x65, 0x73, 0x73, 0x61, 0x67, 0x65, 0x00 };
        private readonly byte[] LOGIN_ACCEPTED_MSG = new byte[] { 0x03, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x0d, 0x9f, 0x27, 0x9b, 0xa5, 0xd8, 0x72, 0x60, 0xa8, 0x03, 0x90, 0xd3,
            0xfe, 0xb1, 0xcf, 0xbb, 0x22, 0xc2, 0x35, 0x17, 0xbe, 0xfa, 0xfe, 0xed, 0xee, 0xea, 0x64, 0x3f, 0xfe, 0x9a, 0x2c, 0x4c, 0x4d, 0xa8, 0x94, 0x8b, 0x9f, 0x5b, 0x76, 0x04, 0xfd, 0x16, 0xc3,
            0x2c, 0x88, 0x78, 0xc0, 0x22, 0xd0, 0x4f, 0xc1, 0x0d, 0xb8, 0xfe, 0xf6, 0x98, 0x55, 0x61, 0xb1, 0x3a, 0x60, 0x4b, 0x70, 0xf3, 0x6b, 0x13, 0xd8, 0x7b, 0x82, 0xad, 0x76, 0xfd };
        private readonly byte[] SERVER_LIST_BEG_MSG = { 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x65, 0x01, 0x00, 0x00, 0x00, 0x00 };
        private readonly byte[] PLAY_EQ_RESP_MSG = { 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x65, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        private Internals.Data.WorldServer _worldServer = null;
        private ChannelFactory<IWorldService> _worldSvcClientChannel = null;
        private IWorldService _worldSvcClient = null;
        private EndpointAddress _worldEA;
        private int _clientCount = 0;
        private object _worldSvcLock = new object();

        public LoginServer() : this(DEFAULT_PORT) { }

        public LoginServer(int port) : base(port) { }

        internal override void ServerStarted()
        {
            // Connect to World (really just setting up proxy)
            _worldEA = new EndpointAddress("net.tcp://localhost:8000/WorldService");    // TODO: change to pick up an endpoint in the config file
            _worldSvcClientChannel = new ChannelFactory<IWorldService>(new NetTcpBinding(), _worldEA);
            //_worldSvcClient = new WorldService.WorldServiceClient(new NetTcpBinding(), worldEA);

            using (EmuDataContext dbCtx = new EmuDataContext())
            {
                dbCtx.ObjectTrackingEnabled = false;
                _worldServer = dbCtx.WorldServers.Single(p => p.WorldConfigID == 1);
            }
            
            base.ServerStarted();
        }

        internal override void ServerStopping()
        {
            base.ServerStopping();
        }

        private IWorldService WorldSvcClient
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
                    }
                }

                return _worldSvcClient;
            }
        }

        // Handles application packets specific to the login server
        internal override void ApplicationPacketReceived(EQRawApplicationPacket packet, Client client)
        {
            try
            {
                ProcessApplicationPacket(packet, client);
            }
            catch (Exception ex)
            {
                _log.Error("Error processing an application packet", ex);
            }
        }

        private void ProcessApplicationPacket(EQRawApplicationPacket packet, Client client)
        {
            switch (packet.OpCode)
            {
                case AppOpCode.None:
                    _log.Error("Application OpCode found not set during packet processing... please fix.");
                    break;
                case AppOpCode.SessionReady:
                    //_log.Debug("Received SessionReady OPCode");

                    // Send a chat message - why? I have no idea
                    EQRawApplicationPacket chatPacket = new EQRawApplicationPacket(AppOpCode.ChatMessage, packet.ClientIPE, CHAT_MSG);
                    lock (client.syncRoot)
                        client.SendApplicationPacket(chatPacket);
                    break;
                case AppOpCode.Login:
                    //_log.Debug("Received Login OPCode");

                    // Authenticate - either with IPAddress or netbios name (if local)
                    string hostName = null;
                    if (Utility.IsIpInNetwork(IPAddress.Parse(_worldServer.IPAddress), client.IPEndPoint.Address, IPAddress.Parse("255.255.255.0")))
                        hostName = Dns.GetHostEntry(client.IPEndPoint.Address).HostName;
                    else
                        hostName = client.IPEndPoint.Address.ToString();

                    LoginAccount logAcct = null;
                    using (EmuDataContext dbCtx = new EmuDataContext())
                    {
                        logAcct = dbCtx.LoginAccounts.SingleOrDefault(la => la.IPAddress == hostName);
                    }

                    if (logAcct == null)
                    {
                        _log.InfoFormat("Client ({0}) attempted login but no matching Login Account was found.", hostName);
                        client.Close();
                        return;
                    }

                    _log.InfoFormat("Client ({0}) login successful.", hostName);
                    // TODO: set last login date?

                    // Send a login accepted
                    EQRawApplicationPacket laPacket = new EQRawApplicationPacket(AppOpCode.LoginAccepted, packet.ClientIPE, LOGIN_ACCEPTED_MSG);
                    lock (client.syncRoot)
                        client.SendApplicationPacket(laPacket);
                    break;
                case AppOpCode.ServerListRequest:
                    //_log.Debug("Received ServerListRequest OPCode");
                    SendServerList(client);
                    break;
                case AppOpCode.PlayEverquestRequest:
                    //_log.Debug("Received PlayEverquestRequest OPCode");

                    // TODO: check for locked and admin level

                    // TODO: check for max players and admin level

                    SendPlayResponse(client);
                    break;
                case AppOpCode.EnterChat:
                case AppOpCode.ChatMessage:
                case AppOpCode.Poll:
                default:
                    _log.Warn("Received Unexpected Application OPCode: " + packet.OpCode);
                    break;
            }
        }

        private void SendServerList(Client client)
        {
            // Send the list of servers
            byte[] prefIpBuf = Encoding.ASCII.GetBytes(_worldServer.IPAddress);
            byte[] prefServerBuf = Encoding.ASCII.GetBytes(_worldServer.LongName);
            int offSet = 20;
            byte[] slBuffer = new byte[512];

            // clearly hackish, but it gets the job done
            Buffer.BlockCopy(SERVER_LIST_BEG_MSG, 0, slBuffer, 0, 16);
            Buffer.SetByte(slBuffer, 16, 0x01);     // server count (we're starting at 1)
            Buffer.BlockCopy(new byte[] { 0x00, 0x00, 0x00 }, 0, slBuffer, 17, 3);
            Buffer.BlockCopy(prefIpBuf, 0, slBuffer, 20, prefIpBuf.Length);     // begin preferred server info
            offSet += prefIpBuf.Length;
            slBuffer[offSet] = 0x00;
            slBuffer[offSet + 1] = _worldServer.ServerType;   // 0x09=preferred,0x01=regular?,0x11=legends?
            offSet += 2;
            Buffer.BlockCopy(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00 }, 0, slBuffer, offSet, 7);  // sever index
            offSet += 7;
            Buffer.BlockCopy(prefServerBuf, 0, slBuffer, offSet, prefServerBuf.Length);
            offSet += prefServerBuf.Length;
            Buffer.BlockCopy(new byte[] { 0x00, 0x45, 0x4e, 0x00, 0x55, 0x53, 0x00 }, 0, slBuffer, offSet, 7);
            offSet += 7;
            Buffer.SetByte(slBuffer, offSet, _worldServer.Status);    // status: 0x00=open, 0xfe=locked
            offSet++;
            Buffer.BlockCopy(new byte[] { 0x00, 0x00 }, 0, slBuffer, offSet, 2);
            offSet += 2;
            Buffer.BlockCopy(BitConverter.GetBytes((short)_clientCount), 0, slBuffer, offSet, 2);
            offSet += 2;
            Buffer.BlockCopy(new byte[] { 0x00, 0x00, 0x00 }, 0, slBuffer, offSet, 3);
            offSet += 3;
            Array.Resize<byte>(ref slBuffer, offSet);

            EQRawApplicationPacket slPacket = new EQRawApplicationPacket(AppOpCode.ServerListResponse, client.IPEndPoint, slBuffer);
            lock (client.syncRoot)
                client.SendApplicationPacket(slPacket);
        }

        // Only one server could've been selected, so just roll with that.  TODO: Need to implement more code if we have more than one server
        private void SendPlayResponse(Client client)
        {
            bool worldUp = false;

            // Tell world there will be a client coming its way
            try
            {
                bool isLocalNet = Utility.IsIpInNetwork(IPAddress.Parse(_worldServer.IPAddress), client.IPEndPoint.Address, IPAddress.Parse("255.255.255.0"));
                WorldSvcClient.ExpectNewClient(client.IPEndPoint.Address.ToString(), isLocalNet);   // always authenticate by IP
                worldUp = true;
            }
            catch (CommunicationException ce)    // Specific fault handlers go before the CommunicationException handler
            {
                //_log.Error("Attempt to tell world to expect a new client encountered a Communication Exception.", ce);
                _log.Error("Attempt to tell world to expect a new client encountered a Communication Exception... World probably not up or has an issue with the WorldService ServiceHost.");
            }
            catch (TimeoutException te)
            {
                _log.Error("Attempt to tell world to expect a new client has timed out.", te);
            }
            catch (Exception e)
            {
                _log.Error("Attempt to tell world to expect a new client has errored out.", e);
            }

            if (worldUp)
            {
                // Give the client the ok
                Buffer.SetByte(PLAY_EQ_RESP_MSG, 16, 0x01);     // sets the index of the server to go to (evidently one based)
                EQRawApplicationPacket perPacket = new EQRawApplicationPacket(AppOpCode.PlayEverquestResponse, client.IPEndPoint, PLAY_EQ_RESP_MSG);
                lock (client.syncRoot)
                    client.SendApplicationPacket(perPacket);

                Interlocked.Increment(ref _clientCount);    // bump the client count
            }
            else
            {
                _worldSvcClientChannel.Abort();
                client.Close();
            }
        }
    }
}