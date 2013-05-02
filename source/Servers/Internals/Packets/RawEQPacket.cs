using System;
using System.Net;

using log4net;

namespace EQEmulator.Servers.Internals.Packets
{
    internal class RawEQPacket : BasePacket
    {
        protected static readonly ILog _log = LogManager.GetLogger(typeof(RawEQPacket));
        protected ProtocolOpCode _OpCode = ProtocolOpCode.None;
        protected ushort _rawOpCode = 0;
        private byte[] _payload = null;

        protected RawEQPacket() { }

        /// <summary>This ctor serializes to the internal buffer with the given opCode and data</summary>
        public RawEQPacket(ProtocolOpCode opCode, byte[] data, IPEndPoint clientIPE)
        {
            _OpCode = opCode;
            _rawOpCode = (ushort)opCode;
            _clientIPE = clientIPE;

            // serialize to buffer
            int dataSize = data.Length + 2;   // opcode
            _data = new byte[dataSize];
            Buffer.BlockCopy(data, 0, _data, 2, data.Length);
            ushort opCodeNO = (ushort)IPAddress.HostToNetworkOrder((short)_OpCode);
            Buffer.BlockCopy(BitConverter.GetBytes(opCodeNO), 0, _data, 0, 2);
        }

        /// <summary>This ctor serializes to the internal buffer with the given opCode, sequence, and data</summary>
        public RawEQPacket(ProtocolOpCode opCode, ushort seqNum, byte[] data, IPEndPoint clientIPE)
        {
            _OpCode = opCode;
            _rawOpCode = (ushort)opCode;
            _clientIPE = clientIPE;

            // serialize to buffer
            int dataSize = data.Length + 4;   // opcode(2) + sequence(2)
            _data = new byte[dataSize];
            Buffer.BlockCopy(BitConverter.GetBytes(seqNum), 0, _data, 2, 2);
            ushort opCodeNO = (ushort)IPAddress.HostToNetworkOrder((short)_OpCode);
            Buffer.BlockCopy(BitConverter.GetBytes(opCodeNO), 0, _data, 0, 2);
            Buffer.BlockCopy(data, 0, _data, 4, data.Length);
        }

        /// <summary>This ctor only parses and deserializes the OpCode internally.</summary>
        public RawEQPacket(IPEndPoint clientIPE, byte[] data) : base(clientIPE, data)
        {
            _rawOpCode = (ushort)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(data, 0));
            if (_rawOpCode < 0x00ff)
                _OpCode = (ProtocolOpCode)_rawOpCode;
        }

        /// <summary>This ctor upscales a BasePacket.  Internally parses OpCode.</summary>
        public RawEQPacket(BasePacket packet, bool hasCrc)
        {
            _rawOpCode = (ushort)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(packet.RawPacketData, 0));
            if (_rawOpCode < 0x00ff)
                _OpCode = (ProtocolOpCode)_rawOpCode;

            _clientIPE = packet.ClientIPE;

            if (hasCrc)
            {
                _data = new byte[packet.RawPacketData.Length - 2];
                Buffer.BlockCopy(packet.RawPacketData, 0, _data, 0, packet.RawPacketData.Length - 2);
            }
            else
            {
                _data = new byte[packet.RawPacketData.Length];
                Buffer.BlockCopy(packet.RawPacketData, 0, _data, 0, packet.RawPacketData.Length);
            }
        }

        public ProtocolOpCode OpCode
        {
            get { return _OpCode; }
            set { _OpCode = value; }
        }

        public ushort RawOpCode
        {
            get { return _rawOpCode; }
            set { _rawOpCode = value; }
        }

        public byte[] GetPayload()
        {
            if (_payload == null)
            {
                _payload = new byte[_data.Length - 2];
                Buffer.BlockCopy(_data, 2, _payload, 0, _data.Length - 2);  // snip the protocol opcode
            }

            return _payload;
        }

        public static ulong NetToHostOrder(ulong val)
        {
            return (ulong)IPAddress.NetworkToHostOrder((long)val);
        }

        public static uint NetToHostOrder(uint val)
        {
            return (uint)IPAddress.NetworkToHostOrder((int)val);
        }

        public static ushort NetToHostOrder(ushort val)
        {
            return (ushort)IPAddress.NetworkToHostOrder((short)val);
        }

        public static ulong HostToNetOrder(ulong val)
        {
            return (ulong)IPAddress.HostToNetworkOrder((long)val);
        }

        public static uint HostToNetOrder(uint val)
        {
            return (uint)IPAddress.HostToNetworkOrder((int)val);
        }

        public static ushort HostToNetOrder(ushort val)
        {
            return (ushort)IPAddress.HostToNetworkOrder((short)val);
        }
    }
}
