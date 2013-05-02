using System;
using System.Runtime.InteropServices;
using System.Net;

namespace EQEmulator.Servers.Internals.Packets
{
    internal class EQPacket<TPacketStruct> : RawEQPacket
        where TPacketStruct : struct
    {
        private TPacketStruct _packetStruct;

        internal EQPacket(RawEQPacket rawPacket)
        {
            _clientIPE = rawPacket.ClientIPE;
            _data = rawPacket.RawPacketData;
            _OpCode = rawPacket.OpCode;

            GCHandle pinnedBytes = GCHandle.Alloc(GetPayload(), GCHandleType.Pinned);
            _packetStruct = (TPacketStruct)Marshal.PtrToStructure(pinnedBytes.AddrOfPinnedObject(), typeof(TPacketStruct));
            pinnedBytes.Free();
        }

        internal EQPacket(ProtocolOpCode opCode, TPacketStruct packetStruct, IPEndPoint clientIPE)
        {
            _OpCode = opCode;
            _packetStruct = packetStruct;
            _clientIPE = clientIPE;

            // serialize to raw byte stream from opcode & struct... byte stream = opcode + structure + optional crc
            int dataSize = Marshal.SizeOf(_packetStruct);
            byte[] _structData = new byte[dataSize];
            GCHandle handle = GCHandle.Alloc(_structData, GCHandleType.Pinned);
            IntPtr buffer = handle.AddrOfPinnedObject();
            Marshal.StructureToPtr(_packetStruct, buffer, false);
            handle.Free();

            dataSize += 2;   // opcode
            _data = new byte[dataSize];
            Buffer.BlockCopy(_structData, 0, _data, 2, _structData.Length);
            ushort opCodeTmp = (ushort)IPAddress.HostToNetworkOrder((short)_OpCode);
            Buffer.BlockCopy(BitConverter.GetBytes(opCodeTmp), 0, _data, 0, 2);
        }

        internal TPacketStruct PacketStruct
        {
            get { return _packetStruct; }
            set { _packetStruct = value; }
        }
    }
}