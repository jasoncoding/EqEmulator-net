using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using EQEmulator.Servers.Internals.Packets;
using log4net;

namespace EQEmulator.Servers.Internals
{
    public enum ConnectionState
    {
        Established,    // normal condition prior to either a client disconnect or a server shutdown
        Disconnecting,  // disconnect sent, waiting for disconnect reply
        Closing,        // we want to close, waiting for outgoing packets to flush?
        Closed          // fully closed
    }

    internal class Client
    {
        private const int RATE_BASE = 1048576;  // 1mb
        private const int DECAY_BASE = 78642;   // ??

        public object syncRoot = new object();
        protected static readonly ILog _log = LogManager.GetLogger(typeof(Client));

        private IPEndPoint _ipe = null;
        private uint _sessionId = 0;
        private uint _key = 0;
        private uint _maxLen = 0;
        private ConnectionState _connState;
        
        private ushort _nextInSeq = 0;      // the next expected seq
        private int _lastAckSent = -1;      // the last actual ack sent
        private int _nextAckToSend = -1;    // next ack that needs sent
        private ushort _nextOutSeq = 0;     // next seq to be used on outgoing app packet
        private ushort _sequencedBase = 0;  // seq num of _seqQueue[0]
        private int _nextSequencedSend = 0; // index into _seqQueue when checking for packets that need sending (packets below this # have been sent but not yet ack'd)
        private int _oversizedOffset = 0;   // offset into oversizedPacket for next fragment
        private int _rateThreshold = RATE_BASE / 250;
        private int _decayRate = DECAY_BASE / 250;
        private int _bytesWritten = 0;

        private Queue<Packets.RawEQPacket> _seqQueue, _nonSeqQueue;     // Send queues
        private Dictionary<ushort, RawEQPacket> _dataPackets;   // Stored data packets - indexed via seq num
        private byte[] _oversizedBuffer = null;     // for handling large packets composed from fragments

        private object _player = null;      // World & Zone specific member

        public Client(IPEndPoint ipe)
        {
            _ipe = ipe;
            _connState = ConnectionState.Established;
            _seqQueue = new Queue<RawEQPacket>(50);
            _nonSeqQueue = new Queue<RawEQPacket>(50);
            _dataPackets = new Dictionary<ushort, RawEQPacket>(50);
        }

        #region Properties
        public IPEndPoint IPEndPoint
        {
            get { return _ipe; }
        }

        public uint SessionId
        {
            get { return _sessionId; }
            set { _sessionId = value; }
        }

        public uint Key
        {
            get { return _key; }
            set { _key = value; }
        }

        public uint MaxLength
        {
            get { return _maxLen; }
            set { _maxLen = value; }
        }

        public ConnectionState ConnectionState
        {
            get { return _connState; }
            set { _connState = value; }
        }

        public ushort NextInSeq
        {
            get { return _nextInSeq; }
            set { _nextInSeq = value; }
        }

        public int LastAckSent
        {
            get { return _lastAckSent; }
            set { _lastAckSent = value; }
        }

        public int NextAckToSend
        {
            get { return _nextAckToSend; }
            set { _nextAckToSend = value; }
        }

        public Queue<RawEQPacket> NonSequencedQueue
        {
            get { return _nonSeqQueue; }
        }

        public Dictionary<ushort, RawEQPacket> DataPackets
        {
            get { return _dataPackets; }
        }

        public byte[] OversizedBuffer
        {
            get { return _oversizedBuffer; }
            set { _oversizedBuffer = value; }
        }

        public int OversizedOffset
        {
            get { return _oversizedOffset; }
            set { _oversizedOffset = value; }
        }

        public WorldPlayer WorldPlayer
        {
            get { return _player as WorldPlayer; }
            set { _player = value; }
        }

        public ZonePlayer ZonePlayer
        {
            get { return _player as ZonePlayer; }
            set { _player = value; }
        }
        #endregion

        public bool HasOutgoingPackets()
        {
            return (_seqQueue.Count > 0 || (_nonSeqQueue.Count - _nextSequencedSend) > 0);
        }

        public void ClearQueues()
        {
            _nonSeqQueue.Clear();
            _seqQueue.Clear();
            _dataPackets.Clear();

            _nextInSeq = 0;
            _lastAckSent = -1;
            _nextAckToSend = -1;
            _nextOutSeq = 0;
            _sequencedBase = 0;
            _nextSequencedSend = 0;
        }

        public void SetNextAckToSend(ushort seqNum)
        {
            //_log.DebugFormat("cur ack = {0}, new ack = {1}", _nextAckToSend, seqNum);
            _nextAckToSend = seqNum;
            _nextInSeq++;   // we're setting next ack because we just got a packet in, so increment the next expected in seq
        }

        public List<RawEQPacket> GetPacketsToSend(bool decay)
        {
            if (decay)
                Decay();    // decay PRIOR to getting send packets

            // check rate to ensure we can send more
            if (_bytesWritten > _rateThreshold)
            {
                //_log.DebugFormat("Over threshold: {0} > {1}", _bytesWritten, _rateThreshold);
                return null;
            }

            List<RawEQPacket> sendPackets = new List<RawEQPacket>(10);
            bool nonSeqEmpty = false, seqEmpty = false;
            
            // Copy packets from seq queue
            IEnumerable<RawEQPacket> seqPackets = null;
            lock (((ICollection)_seqQueue).SyncRoot)
            {
                seqPackets = new List<RawEQPacket>(_seqQueue.Skip(_nextSequencedSend).ToArray());
            }

            IEnumerator<RawEQPacket> seqEnum = seqPackets.GetEnumerator();
            RawEQPacket packet = null;

            while (!nonSeqEmpty || !seqEmpty)   // Loop the send queues
            {
                // TODO: add packet combining
                if (_nonSeqQueue.Count > 0) {
                    packet = _nonSeqQueue.Dequeue();
                    sendPackets.Add(packet);
                    _bytesWritten += packet.RawPacketData.Length;   // adding the total raw length, old emu didn't include opcode len

                    if (_bytesWritten > _rateThreshold) {
                        // Sent enough for now
                        //_log.DebugFormat("Exceeded write threshold in non-seq with: {0}/{1} bytes", _bytesWritten, _rateThreshold);
                        break;
                    }
                }
                else
                    nonSeqEmpty = true;

                if (seqEnum.MoveNext()) {
                    packet = seqEnum.Current;
                    sendPackets.Add(packet);   // don't dequeue yet... do that when ack'd
                    _bytesWritten += packet.RawPacketData.Length;   // adding the total raw length, old emu didn't include opcode len
                    _nextSequencedSend++;   // increment the queue index past the now sent packet

                    if (_bytesWritten > _rateThreshold) {
                        // Sent enough for now
                        //_log.DebugFormat("Exceeded write threshold in seq with: {0}/{1} bytes", _bytesWritten, _rateThreshold);
                        break;
                    }
                }
                else
                    seqEmpty = true;
            }

            return sendPackets;
        }

        private void Decay()
        {
            if (_bytesWritten > 0)
            {
                _bytesWritten -= _decayRate;
                if (_bytesWritten < 0)
                    _bytesWritten = 0;
            }
        }

        /// <summary>Removes sent packets from the sequenced queue up to the specified sequence.</summary>
        public void AckPackets(ushort seqNum)
        {
            if (_sequencedBase + _seqQueue.Count != _nextOutSeq)
                _log.ErrorFormat("Invalid Sequence Queue: base({0}) + queue size({1}) != nextOutSeq({2})", _sequencedBase, _seqQueue.Count, _nextOutSeq);

            if (seqNum == _sequencedBase)
                return; //_log.DebugFormat("Recv ack with no window advancement (seq {0})", seqNum);     // client acking nothing new
            else if (seqNum < _sequencedBase) {
                // odd, client wants to ack packets earlier than the window... sup with that?
                _log.WarnFormat("Recv ack with backward window advancement (recv seq {0}, window starts at {1})", seqNum, _sequencedBase);
            }
            else {
                //_log.DebugFormat("Recv ack through seq {0}, our base is {1}", seqNum, _sequencedBase);
                // Let's ack some packets
                while (_sequencedBase != seqNum + 1) {
                    if (_seqQueue.Count == 0) {
                        _log.ErrorFormat("SEQUENCED QUEUE OUT OF PACKETS. Base: {0}, Next send is {1}", _sequencedBase, _nextSequencedSend);
                        _sequencedBase = _nextOutSeq;   // make sure our base is corrected
                        _nextSequencedSend = 0;     // reset index at which we start sending packets from seq queue
                    }

                    // remove packet from seq queue
                    //_log.DebugFormat("Removing acked packet (seq {0}).  Next send is {1}", _sequencedBase, _nextSequencedSend);
                    lock (((ICollection)_seqQueue).SyncRoot)
                        _seqQueue.Dequeue();

                    if (_nextSequencedSend > 0)
                        _nextSequencedSend--;   // adjust the threshold at which we look for packets that need sent

                    _sequencedBase++;   // increment to the index past the one we just removed
                }
            }
        }

        /// <summary>Sets the client connection state to Closing.  This signals the server to begin closing the client.</summary>
        public void Close()
        {
            lock (this.syncRoot) {
                if (_connState == ConnectionState.Established)  // Prevent from further closing an already closing client
                    _connState = ConnectionState.Closing;
            }
        }

        public void SendSessionResponse(SessionFormat format)
        {
            // re-init the client's queues (might have recv this packet again before the writer had a chance to send the session response (eliminates a race))
            ClearQueues();

            SessionResponse sessResp = new SessionResponse();
            sessResp.SessionId = (UInt32)IPAddress.HostToNetworkOrder((int)_sessionId);
            sessResp.MaxLength = (UInt32)IPAddress.HostToNetworkOrder((int)_maxLen);
            sessResp.CRCLength = 2;
            sessResp.Format = (byte)format;
            sessResp.Key = (UInt32)IPAddress.HostToNetworkOrder((int)_key);

            EQPacket<SessionResponse> sessRespPacket = new EQPacket<SessionResponse>(ProtocolOpCode.SessionResponse, sessResp, _ipe);
            //_log.DebugFormat("Sending SessionResponse to client: session: {0}, maxlen: {1}, key: {2}", _sessionId, _maxLen, _key);
            _nonSeqQueue.Enqueue(sessRespPacket);
        }

        public void SendAck(ushort seqNum)
        {
            _lastAckSent = seqNum;
            ushort seq_no = (ushort)IPAddress.HostToNetworkOrder((short)seqNum);

            RawEQPacket ackPacket = new RawEQPacket(ProtocolOpCode.Ack, BitConverter.GetBytes(seq_no), _ipe);
            //_log.Debug(string.Format("Sending Ack with sequence: {0}", seqNum));
            _nonSeqQueue.Enqueue(ackPacket);
        }

        public void SendDisconnect(ConnectionState connState)
        {
            _connState = connState;
            uint session_no = (uint)IPAddress.HostToNetworkOrder((int)_sessionId);
            RawEQPacket discPacket = new RawEQPacket(ProtocolOpCode.SessionDisconnect, BitConverter.GetBytes(session_no), _ipe);
            _log.DebugFormat("Sending Disconnect for session {0}", _sessionId);
            _nonSeqQueue.Enqueue(discPacket);
        }

        public void SendOutOfOrderAck(ushort seqNum)
        {
            _log.Debug("Sending out of order ack with sequence " + seqNum.ToString());
            _nonSeqQueue.Enqueue(new RawEQPacket(ProtocolOpCode.OutOfOrderAck, BitConverter.GetBytes(seqNum), _ipe));
        }

        public void SendSessionStats(EQPacket<SessionStats> statsPacket)
        {
            SessionStats sessStats = statsPacket.PacketStruct;  // new SessionStats();
            ulong packsRecv = statsPacket.PacketStruct.PacketsRecieved;
            sessStats.PacketsRecieved = statsPacket.PacketStruct.PacketsSent;
            sessStats.PacketsSent = packsRecv;

            EQPacket<SessionStats> sessStatsPacket = new EQPacket<SessionStats>(ProtocolOpCode.SessionStatResponse, sessStats, _ipe);
            _nonSeqQueue.Enqueue(sessStatsPacket);

            uint avgDelta = RawEQPacket.NetToHostOrder(sessStats.AverageDelta);
            if (avgDelta > 0)
            {
                _rateThreshold = RATE_BASE / (int)avgDelta;
                _decayRate = DECAY_BASE / (int)avgDelta;
                //_log.DebugFormat("Adjusting data rate to thresh {0}, decay {1} based on avg delta {2}", _rateThreshold, _decayRate, avgDelta);
            }
        }

        /// <summary>Send the app packet with ackReq = true.</summary>
        public void SendApplicationPacket(EQRawApplicationPacket appPacket)
        {
            SendApplicationPacket(appPacket, true);
        }
        
        /// <summary>Sends an application packet as one or more raw packets.</summary>
        public void SendApplicationPacket(EQRawApplicationPacket appPacket, bool ackReq)
        {
            //_log.Debug("Sending " + appPacket.OpCode.ToString() + " application packet");

            if (appPacket.ClientIPE == null)
                appPacket.ClientIPE = this.IPEndPoint;

            if (ackReq)     // ack required = seq queue, no ack req = non-seq
            {
                // If the app packet can fit in one raw packet then send an OP_Packet packet, otherwise frament and send multiple OP_Fragment packets
                if (appPacket.GetPayload().Length > _maxLen - 8)    // proto-op(2), seq(2), app-op(2)... data ...crc(2)
                {
                    int chunksize, used;
                    byte[] tmpBuffer;
                    //_log.DebugFormat("Making oversized packet for a length of {0}", appPacket.GetPayload().Length);

                    // Build the raw bytes
                    int len = appPacket.RawPacketData.Length;
                    //uint lenNO = RawEQPacket.HostToNetOrder(len);
                    int lenNO = IPAddress.HostToNetworkOrder(appPacket.RawPacketData.Length);
                    tmpBuffer = new byte[_maxLen - 6];  // the fragment opCode, seq# and crc get thrown on later
                    used = (int)_maxLen - 10;           // track how many bytes we've copied, allowing 10 for opCode, seq#, length and crc
                    Buffer.BlockCopy(BitConverter.GetBytes(lenNO), 0, tmpBuffer, 0, 4);     // first packet gets the total length
                    Buffer.BlockCopy(appPacket.RawPacketData, 0, tmpBuffer, 4, used);
                    //_log.DebugFormat("First fragment: used {0}/{1} - Put size {2} in the packet", used, appPacket.RawPacketData.Length, len);
                    SendSequencedPacket(new BasePacket(appPacket.ClientIPE, tmpBuffer), ProtocolOpCode.Fragment);

                    while (used < appPacket.RawPacketData.Length)
                    {
                        chunksize = (int)Math.Min(appPacket.RawPacketData.Length - used, _maxLen - 6);
                        tmpBuffer = new byte[chunksize];
                        Buffer.BlockCopy(appPacket.RawPacketData, used, tmpBuffer, 0, chunksize);
                        used += chunksize;
                        //_log.DebugFormat("Next fragment: used {0}/{1}, len: {2}", used, appPacket.RawPacketData.Length, chunksize);
                        SendSequencedPacket(new BasePacket(appPacket.ClientIPE, tmpBuffer), ProtocolOpCode.Fragment);
                    }
                }
                else
                    SendSequencedPacket(appPacket, ProtocolOpCode.Packet);
            }
            else
                _nonSeqQueue.Enqueue(new RawEQPacket(ProtocolOpCode.Packet, appPacket.RawPacketData, appPacket.ClientIPE));
        }

        public void SendSequencedPacket(BasePacket appPacket, ProtocolOpCode opCode)
        {
            // Wrap in a raw packet and throw on sequenced queue
            ushort nextOutSeq_no = (ushort)IPAddress.HostToNetworkOrder((short)_nextOutSeq);
            RawEQPacket packet = new RawEQPacket(opCode, nextOutSeq_no, appPacket.RawPacketData, appPacket.ClientIPE);
            _nextOutSeq++;

            lock (((ICollection)_seqQueue).SyncRoot)
                _seqQueue.Enqueue(packet);
        }
    }
}
