using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

using EQEmulator.Servers.Internals;
using EQEmulator.Servers.Internals.Packets;

namespace EQEmulator.Servers
{
    public class EQServer : UDPServer
    {
        private Queue<RawEQPacket> _sendQueue;  // queue for raw sending
        private Queue<BasePacket> _recvQueue;   // & receiving with UDPServer
        private Thread _readerThread, /*_writerThread,*/ _clientThread;
        internal Dictionary<string, Client> _clients;   // keyed by client's IP
        private AutoResetEvent _readerWaitHandle, _writerWaitHandle;
        protected ReaderWriterLockSlim _clientListLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private Timer _decayTimer;
        private bool _decayed = false;
        private object _decayedLockObj = new object();

        /// <summary>Implemented by derived class to perform handle application level packets.</summary>
        internal virtual void ApplicationPacketReceived(EQRawApplicationPacket packet, Client client)
        {
            throw new NotImplementedException("Must override ApplicationPacketReceived virtual method in the specialized server class.");
        }

        /// <summary>Implemented by derived class to set the session response's compression and/or encoding.</summary>
        internal virtual void PreSendSessionResponse(Client client)
        {
            client.SendSessionResponse(SessionFormat.Normal);
        }

        /// <summary>Implemented by derived class to perform any necessary packet decompression and/or decryption.</summary>
        internal virtual void PreProcessInPacket(ref BasePacket packet) { }

        /// <summary>Implemented by derived class to perform any necessary packet decompression and/or decryption.</summary>
        internal virtual void PreProcessOutPacket(ref RawEQPacket packet) { }

        /// <summary>Implemented by derived class to be notified of a client disconnecting from the server.</summary>
        internal virtual void ClientDisconnected(Client client) { }

        public EQServer(int port) : base(port) { }

        internal override void PacketReceived(BasePacket packet)
        {
            // fast method of enqueueing so we don't drop any packets by doing a lot of work in this method
            ThreadPool.QueueUserWorkItem(new WaitCallback(EnqueueRecvPacket), packet);
        }

        internal override void ServerStarting()
        {
            _recvQueue = new Queue<BasePacket>(50);
            //_sendQueue = new Queue<RawEQPacket>(50);
            _readerThread = new Thread(new ThreadStart(ReaderProc));
            //_writerThread = new Thread(new ThreadStart(WriterProc));
            //_writerThread.Priority = ThreadPriority.AboveNormal;    // see if this gets our writes out faster
            _clientThread = new Thread(new ThreadStart(ClientProc));
            _clients = new Dictionary<string, Client>(50);

            _readerWaitHandle = new AutoResetEvent(false);
            //_writerWaitHandle = new AutoResetEvent(false);

            _decayTimer = new Timer(new TimerCallback(DecayTimerCallback), null, Timeout.Infinite, 20);
        }

        internal override void ServerStarted()
        {
            _readerThread.Start();
            //_writerThread.Start();
            _clientThread.Start();
        }

        internal override void ServerStopping()
        {
            _clientListLock.EnterReadLock();
            foreach (KeyValuePair<string, Client> kvp in _clients)
            {
                if (kvp.Value.ConnectionState == ConnectionState.Established)
                    kvp.Value.Close();     // No need to lock the client... right?
            }
            _clientListLock.ExitReadLock();

            Thread.Sleep(2000);     // Creates a minor race, but should be enough (esp. combined with the following write lock) to iterate clients and send the disconnects

            _clientListLock.EnterWriteLock();
            _clients.Add(string.Empty, null);   // signals shutdown
            _clientListLock.ExitWriteLock();
            _clientThread.Join();
            _log.Debug("Client thread joined.");

            EnqueueRecvPacket(null);    // signals shutdown
            _readerThread.Join();
            _log.Debug("Reader thread joined.");

            //EnqueueSendPacket(null);    // signals shutdown
            //_writerThread.Join();
            //_log.Debug("Writer thread joined.");
        }

        internal override void ServerStopped()
        {
            // cleanup
            _readerWaitHandle.Close();
            //_writerWaitHandle.Close();

            _decayTimer.Dispose();
        }

        // Read the received queue
        internal void ReaderProc()
        {
            BasePacket packet = null;
            
            while (true)
            {
                packet = null;
                try
                {
                    lock (((ICollection)_recvQueue).SyncRoot)
                    {
                        if (_recvQueue.Count > 0)
                        {
                            packet = _recvQueue.Dequeue();
                            if (packet == null)
                            {
                                _log.Debug("ReaderProc detected shutdown, shutting down");
                                return;     // null packet means shutdown
                            }
                        }
                    }

                    if (packet != null)
                        PreProcessRecvPacket(packet);   // start processing the packet
                    else
                        _readerWaitHandle.WaitOne();
                }
                catch (Exception e)
                {
                    _log.Error("ReaderProc error", e);
                    _readerWaitHandle.WaitOne();
                }
            }
        }

        // Read the send queue and write packets out
        internal void WriterProc()
        {
            RawEQPacket packet = null;

            while (true)
            {
                packet = null;
                try
                {
                    if (_sendQueue.Count > 0)
                    {
                        lock (((ICollection)_sendQueue).SyncRoot)
                        {
                            if (_sendQueue.Count > 0)
                            {
                                packet = _sendQueue.Dequeue();
                                if (packet == null)
                                {
                                    _log.Debug("WriterProc detected shutdown, shutting down");
                                    return;     // null packet means shutdown
                                }
                            }
                        }
                    }

                    if (packet != null)
                        this.AsyncBeginSend(packet);
                    else
                        _writerWaitHandle.WaitOne();
                }
                catch (Exception ex)
                {
                    _log.Error("WriterProc error", ex);
                    _writerWaitHandle.WaitOne();
                }
            }
        }

        // Loop the clients
        internal void ClientProc()
        {
            List<RawEQPacket> dataPackets = null, sendPackets = null;
            List<string> clientsToRemove = new List<string>(10);  // keys to remove
            List<Client> clientsWithPackets = new List<Client>(20);
            bool decayed = false;
            Client cli = null;

            _decayTimer.Change(0, 20);  // fire up the decay timer
            while (true)
            {
                decayed = _decayed;
                lock (_decayedLockObj)
                    _decayed = false;   // reset

                // loop clients copying those that need to send packets or that need removed (disconnected)
                _clientListLock.EnterReadLock();
                try
                {
                    if (_clients.ContainsValue(null))    // null signals shutdown
                    {
                        _log.Debug("ClientProc detected shutdown, shutting down");
                        _decayTimer.Change(Timeout.Infinite, Timeout.Infinite);
                        return;
                    }

                    clientsWithPackets.Clear();

                    foreach (KeyValuePair<string, Client> kvp in _clients)
                    {
                        // is locking truly necessary here?  it seems not, right?
                        cli = kvp.Value;
                        if (cli.ConnectionState == ConnectionState.Closed)
                        {
                            clientsToRemove.Add(kvp.Key);
                            continue;   // if closed, we don't want to send anything so let's move on
                        }

                        if (cli.LastAckSent < cli.NextAckToSend)  // only ack the highest ack
                            cli.SendAck((ushort)cli.NextAckToSend);

                        if (cli.HasOutgoingPackets())
                            clientsWithPackets.Add(cli);

                        if (cli.ZonePlayer != null)     // ZonePlayer's process is the spot for client processing
                            if (!cli.ZonePlayer.Process())
                                cli.Close();    // player must be LD or something, so let's close
                    }
                }
                catch (Exception ex)
                {
                    _log.Error("ClientProc error", ex);
                }
                finally
                {
                    _clientListLock.ExitReadLock();
                }

                try
                {
                    foreach (Client client in clientsWithPackets)
                    {
                        lock (client.syncRoot)
                        {
                            if (client.HasOutgoingPackets())    // Get any packets that need sent
                                sendPackets = client.GetPacketsToSend(decayed);

                            // Process any previously stored packets (future sequenced, etc.)
                            RawEQPacket packet = null;
                            ushort seq = client.NextInSeq;
                            dataPackets = new List<RawEQPacket>(10);
                            while (client.DataPackets.TryGetValue(seq, out packet))
                            {
                                _log.Debug("Processing queued data packet with Seq: " + client.NextInSeq);
                                dataPackets.Add(packet);
                                client.DataPackets.Remove(seq);
                                seq++;
                            }
                        }

                        if (sendPackets != null)    // Send the packets built from the client's queues
                        {
                            for (int i = 0; i < sendPackets.Count; i++)
                            {
                                RawEQPacket packet = sendPackets[i];
                                if (packet.OpCode != ProtocolOpCode.SessionRequest && packet.OpCode != ProtocolOpCode.SessionResponse)
                                {
                                    PreProcessOutPacket(ref packet);     // Give the app level server a shot at the outgoing packet

                                    // perform a crc and append last two bytes to packet - note intentional truncation
                                    ushort crc_no = (ushort)CRC.ComputeChecksum(packet.RawPacketData, packet.RawPacketData.Length, client.Key);
                                    crc_no = (ushort)IPAddress.HostToNetworkOrder((short)crc_no);
                                    packet.AppendData(BitConverter.GetBytes(crc_no));
                                }

                                sendPackets[i] = packet;
                            }

                            //EnqueueSendPackets(sendPackets);
                            foreach (RawEQPacket packet in sendPackets)
                            {
                                this.AsyncBeginSend(packet);
                                //Thread.Sleep(250); // test
                            }

                            sendPackets = null;
                        }

                        // Now that all pending packets have been sent, see if we need to close
                        if (client.ConnectionState == ConnectionState.Closing)
                        {
                            lock (client.syncRoot)
                                client.SendDisconnect(ConnectionState.Disconnecting);   // Doesn't get sent until next client loop
                        }
                        else if (client.ConnectionState == ConnectionState.Disconnecting)
                        {
                            // Ok we've sent the disconnect, time to shut er down
                            lock (client.syncRoot)
                                client.ConnectionState = ConnectionState.Closed;
                        }

                        for (int i = 0; i < dataPackets.Count; i++)     // TODO: not sure I like this here - actually it's not in the lock so might be ok
                            ProcessRecvPacket(dataPackets[i], client);
                    }

                    // Remove disconnected clients
                    if (clientsToRemove.Count > 0)
                    {
                        RemoveDisconnectedClients(clientsToRemove);
                        clientsToRemove.Clear();
                    }
                }
                catch (Exception ex)
                {
                    _log.Error("ClientProc error", ex);
                }

                if (_clients.Count == 0)
                    Thread.Sleep(1000); // Don't just spin on and on forever with no clients (empty static zone maybe)
                else
                    Thread.Sleep(10);   // Let's try this to see if yielding here can be good
            }
        }

        private void EnqueueRecvPacket(BasePacket packet)
        {
            lock (((ICollection)_recvQueue).SyncRoot)
                _recvQueue.Enqueue(packet);

            _readerWaitHandle.Set();  // Signal reader proc
        }

        private void EnqueueRecvPacket(object state)
        {
            try
            {
                BasePacket packet = (BasePacket)state;
                EnqueueRecvPacket(packet);
            }
            catch (Exception e)
            {
                _log.Error("Error during attempt to use QueueUserWorkItem to fast queue a packet", e);
            }
        }

        private void EnqueueSendPacket(RawEQPacket packet)
        {
            lock (((ICollection)_sendQueue).SyncRoot)
                _sendQueue.Enqueue(packet);

            _writerWaitHandle.Set();  // Signal writer proc
        }

        private void EnqueueSendPackets(List<RawEQPacket> packets)
        {
            lock (((ICollection)_sendQueue).SyncRoot)
            {
                foreach (RawEQPacket packet in packets)
                    _sendQueue.Enqueue(packet);
            }

            _writerWaitHandle.Set();  // Signal writer proc
        }

        // Do any basic processing common to all packets
        private void PreProcessRecvPacket(BasePacket packet)
        {
            Client client;
            _clientListLock.EnterReadLock();
            _clients.TryGetValue(packet.ClientIPE.ToString(), out client);
            _clientListLock.ExitReadLock();

            ProtocolOpCode opCode = (ProtocolOpCode)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(packet.RawPacketData, 0));

            if (client != null)
            {
                // Found an existing client
                if (client.ConnectionState == ConnectionState.Closed)
                    return;     // ignore packets for closed connections
            }
            else
            {
                if (opCode != ProtocolOpCode.SessionRequest)
                    return;     // no client and not a session request - so ignore it
                else
                    client = new Client(packet.ClientIPE);
            }

            // CRC
            bool hasCrc = false;
            if (opCode != ProtocolOpCode.SessionRequest && opCode != ProtocolOpCode.SessionResponse && opCode != ProtocolOpCode.OutOfSession)
            {
                hasCrc = true;
                ushort sentCRC = (ushort)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(packet.RawPacketData, packet.RawPacketData.Length - 2));
                if (sentCRC != (ushort)CRC.ComputeChecksum(packet.RawPacketData, packet.RawPacketData.Length - 2, client.Key))
                {
                    _log.Error("Packet failed checksum.  Client key: " + client.Key);
                    return;
                }

                PreProcessInPacket(ref packet);   // Let the app level server get a crack at the packet
            }

            ProcessRecvPacket(new RawEQPacket(packet, hasCrc), client);
        }

        // Process the different protocol packet types
        private void ProcessRecvPacket(RawEQPacket packet, Client client)
        {
            if (packet.RawOpCode > 0x00ff)      // Check for application level packet
            {
                //_log.Debug("OPCode above 0x00ff.");
                packet.RawOpCode = (ushort)IPAddress.NetworkToHostOrder((short)packet.RawOpCode);   // orig code says opcode byte order is backwards in this case
                ApplicationPacketReceived(new EQRawApplicationPacket(packet), client);  // Handled by specialized server type
                return;
            }

            int subPacketLen = 0, processed = 0;

            switch (packet.OpCode)
            {
                case ProtocolOpCode.None:
                    _log.Error("Protocol OpCode found not set during packet processing... please fix.");
                    break;
                case ProtocolOpCode.SessionRequest:
                    // Check for existing client - may be getting blitzed w/ session requests
                    if (client.SessionId != 0 && client.ConnectionState == ConnectionState.Established)
                    {
                        _log.Warn("Recv a sessionRequest for an existing open client.");
                        break;
                    }

                    bool add = (client.SessionId == 0);   // handles case of existing clients that aren't connected (no need to re-add)

                    EQPacket<SessionRequest> sessReqPacket = new EQPacket<SessionRequest>(packet);
                    lock (client.syncRoot)
                    {
                        client.SessionId = (UInt32)IPAddress.NetworkToHostOrder((int)sessReqPacket.PacketStruct.SessionId);
                        client.MaxLength = (UInt32)IPAddress.NetworkToHostOrder((int)sessReqPacket.PacketStruct.MaxLength);
                        client.Key = 0x11223344;
                        client.ConnectionState = ConnectionState.Established;
                        //_log.Debug(string.Format("Received Session Request: session {0} maxlength {1}", client.SessionId, client.MaxLength));
                        PreSendSessionResponse(client);
                    }

                    if (add)
                    {
                        _clientListLock.EnterWriteLock();
                        try
                        {
                            _clients.Add(client.IPEndPoint.ToString(), client);
                            _log.InfoFormat("New client connecting from {0}", client.IPEndPoint.ToString());
                        }
                        finally
                        {
                            _clientListLock.ExitWriteLock();
                        }
                    }

                    break;
                case ProtocolOpCode.SessionResponse:
                    _log.Warn("Received unhandled SessionResponse OPCode");
                    break;
                case ProtocolOpCode.Combined:
                    
                    while (processed < packet.GetPayload().Length)
                    {
                        subPacketLen = Buffer.GetByte(packet.GetPayload(), processed);
                        //_log.Debug("Extracting combined packet of length " + subPacketLen);
                        byte[] embBytes = new byte[subPacketLen];
                        Buffer.BlockCopy(packet.GetPayload(), processed + 1, embBytes, 0, subPacketLen);
                        RawEQPacket embPacket = new RawEQPacket(client.IPEndPoint, embBytes);
                        ProcessRecvPacket(embPacket, client);
                        processed += subPacketLen + 1;
                    }
                    break;
                case ProtocolOpCode.SessionDisconnect:
                    lock (client.syncRoot)
                    {
                        switch (client.ConnectionState)
                        {
                            case ConnectionState.Established:
                                _log.Debug("Received client initiated disconnect");
                                lock (client.syncRoot)
                                    client.ConnectionState = ConnectionState.Closed;
                                    //client.SendDisconnect(ConnectionState.Closed);
                                break;
                            case ConnectionState.Closing:
                                _log.Debug("Received disconnect during a pending close");
                                lock (client.syncRoot)
                                    client.SendDisconnect(ConnectionState.Closed);
                                break;
                            case ConnectionState.Closed:
                            case ConnectionState.Disconnecting:     // This is never sent back... handling a different way
                                _log.Debug("Received expected disconnect");
                                lock (client.syncRoot)
                                    client.ConnectionState = ConnectionState.Closed;
                                break;
                        }
                    }
                    break;
                case ProtocolOpCode.KeepAlive:
                    lock (client.syncRoot)
                        client.NonSequencedQueue.Enqueue(new RawEQPacket(client.IPEndPoint, packet.RawPacketData));

                    _log.Debug("Received and replied to a KeepAlive");
                    break;
                case ProtocolOpCode.SessionStatRequest:
                    EQPacket<SessionStats> statsPacket = new EQPacket<SessionStats>(packet);
                    //_log.DebugFormat("Received Stats: {0} packets recv, {1} packets sent, Deltas: local {2}, ({3} <- {4} -> {5}) remote {6}",
                    //    RawEQPacket.NetToHostOrder(statsPacket.PacketStruct.PacketsRecieved), RawEQPacket.NetToHostOrder(statsPacket.PacketStruct.PacketsRecieved), RawEQPacket.NetToHostOrder(statsPacket.PacketStruct.LastLocalDelta),
                    //    RawEQPacket.NetToHostOrder(statsPacket.PacketStruct.LowDelta), RawEQPacket.NetToHostOrder(statsPacket.PacketStruct.AverageDelta),
                    //    RawEQPacket.NetToHostOrder(statsPacket.PacketStruct.HighDelta), RawEQPacket.NetToHostOrder(statsPacket.PacketStruct.LastRemoteDelta));
                    
                    lock (client.syncRoot)
                        client.SendSessionStats(statsPacket);
                    break;
                case ProtocolOpCode.SessionStatResponse:
                    _log.Debug("Received SessionStatResponse OPCode, ignoring");
                    break;
                case ProtocolOpCode.Packet:
                    ushort seqNum = (ushort)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(packet.GetPayload(), 0));   // get seq num
                    //_log.Debug(string.Format("Received Data Packet: session {0} sequence {1}", client.SessionId, seqNum));

                    // TODO: figure out the locking strategy here
                    // determine the packet sequence
                    if (seqNum > client.NextInSeq)
                    {
                        _log.DebugFormat("Recv future data packet - expected {0} but got {1}", client.NextInSeq, seqNum);
                        lock (client.syncRoot)
                        {
                            client.DataPackets.Remove(seqNum);
                            client.DataPackets.Add(seqNum, packet);     // shove into the deferred packet list
                        }
                    }
                    else if (seqNum < client.NextInSeq)
                    {
                        //_log.DebugFormat("Recv duplicate data packet - expected {0} but got {1}", client.NextInSeq, seqNum);
                        client.SendOutOfOrderAck(seqNum);
                    }
                    else
                    {
                        // Received packet w/ expected seq
                        lock (client.syncRoot)
                        {
                            client.DataPackets.Remove(seqNum);  // Remove if it was previously queued as a future packet
                            client.SetNextAckToSend(seqNum);    // sequenced packets must be ack'd
                        }

                        // check for embedded OP_AppCombined (0x19)
                        ProtocolOpCode embOp = (ProtocolOpCode)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(packet.GetPayload(), 2));
                        if (embOp == ProtocolOpCode.AppCombined)
                        {
                            //_log.Debug("Found and extracted an embedded packet in a data packet");
                            byte[] embBytes = new byte[packet.GetPayload().Length - 4]; // snip the data packet sequence num & AppCombined OpCode
                            Buffer.BlockCopy(packet.GetPayload(), 4, embBytes, 0, packet.GetPayload().Length - 4);
                            RawEQPacket embPacket = new RawEQPacket(ProtocolOpCode.AppCombined, embBytes, client.IPEndPoint);
                            ProcessRecvPacket(embPacket, client);
                        }
                        else
                        {
                            // Needs to be handled by specialized server, let's get us an app packet going
                            byte[] appBytes = new byte[packet.GetPayload().Length - 2]; // snip the data packet sequence num
                            Buffer.BlockCopy(packet.GetPayload(), 2, appBytes, 0, packet.GetPayload().Length - 2);
                            ApplicationPacketReceived(new EQRawApplicationPacket(client.IPEndPoint, appBytes), client);
                        }
                    }
                    break;
                case ProtocolOpCode.Fragment:
                    ushort fragSeqNum = (ushort)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(packet.GetPayload(), 0));

                    if (fragSeqNum > client.NextInSeq)
                    {
                        _log.DebugFormat("Recv future fragment - expected {0} but got {1}", client.NextInSeq, fragSeqNum);
                        lock (client.syncRoot)
                            client.DataPackets.Add(fragSeqNum, packet);     // shove into the deferred data packet list
                    }
                    else if (fragSeqNum < client.NextInSeq)
                    {
                        //_log.DebugFormat("Recv duplicate data fragment - expected {0} but got {1}", client.NextInSeq, fragSeqNum);
                        client.SendOutOfOrderAck(fragSeqNum);
                    }
                    else
                    {
                        // Received packet w/ expected seq
                        BasePacket bigPacket = null;
                        lock (client.syncRoot)
                        {
                            client.DataPackets.Remove(fragSeqNum);  // Remove if it was previously queued as a future packet
                            client.SetNextAckToSend(fragSeqNum);

                            if (client.OversizedBuffer != null)
                            {
                                // copy this round's fragment into the oversized buffer
                                Buffer.BlockCopy(packet.GetPayload(), 2, client.OversizedBuffer, client.OversizedOffset, packet.GetPayload().Length - 2);
                                client.OversizedOffset += packet.GetPayload().Length - 2;
                                //_log.DebugFormat("Recv fragment - seq {0} now at {1}", fragSeqNum, client.OversizedBuffer.Length / client.OversizedOffset);

                                if (client.OversizedOffset == client.OversizedBuffer.Length)
                                {
                                    // I totally don't get this first comparison (shouldn't we be looking in the oversized buffer), but ok...
                                    if (Buffer.GetByte(packet.GetPayload(), 2) == 0x00 && Buffer.GetByte(packet.GetPayload(), 3) == 0x19)
                                        bigPacket = new RawEQPacket(packet.ClientIPE, client.OversizedBuffer);
                                    else
                                        bigPacket = new EQRawApplicationPacket(client.IPEndPoint, client.OversizedBuffer);

                                    client.OversizedBuffer = null;
                                    client.OversizedOffset = 0;
                                    //_log.Debug("Completed combined oversized packet.");
                                }
                            }
                            else
                            {
                                uint oversizedLen = (uint)IPAddress.NetworkToHostOrder((int)BitConverter.ToUInt32(packet.GetPayload(), 2));
                                client.OversizedBuffer = new byte[oversizedLen];    // initialize the oversized packet buffer
                                Buffer.BlockCopy(packet.GetPayload(), 6, client.OversizedBuffer, 0, packet.GetPayload().Length - 6);
                                client.OversizedOffset = packet.GetPayload().Length - 6;
                                //_log.DebugFormat("Recv initial fragment packet - total size: {0} fragment len: {1}", oversizedLen, packet.GetPayload().Length - 6);
                            }
                        }

                        if (bigPacket is RawEQPacket)
                            ProcessRecvPacket(bigPacket as RawEQPacket, client);
                        else if(bigPacket is EQRawApplicationPacket)
                            ApplicationPacketReceived(bigPacket as EQRawApplicationPacket, client);
                    }
                    break;
                case ProtocolOpCode.OutOfOrderAck:
                    _log.Debug("Received OutOfOrderAck OPCode");
                    break;
                case ProtocolOpCode.Ack:
                    ushort ackSeqNum = (ushort)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(packet.GetPayload(), 0));   // get seq num
                    lock (client.syncRoot)
                        client.AckPackets(ackSeqNum);
                    break;
                case ProtocolOpCode.AppCombined:
                    //_log.Debug("Processing App Combined packet: " + BitConverter.ToString(packet.RawPacketData));
                    processed = 0;
                    EQRawApplicationPacket appPacket = null;

                    while (processed < packet.GetPayload().Length)
                    {
                        appPacket = null;
                        subPacketLen = Buffer.GetByte(packet.GetPayload(), processed);
                        
                        if (subPacketLen != 0xff)
                        {
                            //_log.Debug("Extracting App Combined packet of length " + subPacketLen);
                            byte[] appBytes = new byte[subPacketLen];
                            Buffer.BlockCopy(packet.GetPayload(), processed + 1, appBytes, 0, subPacketLen);
                            appPacket = new EQRawApplicationPacket(client.IPEndPoint, appBytes);
                            processed += (subPacketLen + 1);
                        }
                        else
                        {
                            subPacketLen = IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(packet.GetPayload(), processed + 1));
                            //_log.Debug("Extracting App Combined packet of length " + subPacketLen);
                            byte[] appBytes = new byte[subPacketLen];
                            Buffer.BlockCopy(packet.GetPayload(), processed + 3, appBytes, 0, subPacketLen);
                            appPacket = new EQRawApplicationPacket(client.IPEndPoint, appBytes);
                            processed += (subPacketLen + 3);
                        }

                        ApplicationPacketReceived(appPacket, client);
                    }
                    break;
                case ProtocolOpCode.OutOfSession:
                    _log.Debug("Received OutOfSession OPCode, ignoring");
                    break;
                default:
                    _log.Warn("Received Unknown Protocol OPCode: " + (ushort)packet.OpCode);
                    break;
            }
        }

        private void RemoveDisconnectedClients(List<string> clients)
        {
            //foreach (string ip in clients)  // do notifies here so it's outside of lock
            //    ClientDisconnected(_clients[ip]);
            
            Client client = null;

            try
            {
                _clientListLock.EnterWriteLock();
                foreach (string ip in clients)
                {
                    _log.Debug("Removing disconnected client " + ip);
                    client = _clients[ip];
                    _clients.Remove(ip);
                    ClientDisconnected(client);
                }
            }
            catch (Exception ex)
            {
                _log.Error("Error removing disconnected clients... ", ex);
            }
            finally
            {
                _clientListLock.ExitWriteLock();
            }
        }

        /// <summary>Retrieve active clients.</summary>
        internal List<Client> GetConnectedClients()
        {
            List<Client> clients = new List<Client>();
            Client cli = null;
            _clientListLock.EnterReadLock();
            try
            {
                foreach (KeyValuePair<string, Client> kvp in _clients)
                {
                    cli = kvp.Value;
                    if (cli.ConnectionState != ConnectionState.Established)
                        continue;   // if closed, we don't want to send anything so let's move on
                    else
                        clients.Add(cli);
                }
            }
            catch (Exception ex)
            {
                _log.Error("GetConnectedClients error", ex);
            }
            finally
            {
                _clientListLock.ExitReadLock();
            }

            return clients;
        }

        private void DecayTimerCallback(object state)
        {
            lock (_decayedLockObj)
                _decayed = true;
        }

        /// <summary>Returns a NetBios name if client is on local net or an IP address if remote.</summary>
        internal string GetClientAddress(Client client, string networkAddr)
        {
            IPAddress serverIp = IPAddress.Parse(networkAddr);
            if (Utility.IsIpInNetwork(serverIp, client.IPEndPoint.Address, IPAddress.Parse("255.255.255.0")))   // TODO: pick up local net mask from config or something
                return Dns.GetHostEntry(client.IPEndPoint.Address).HostName;
            else
                return client.IPEndPoint.Address.ToString();
        }
    }
}
