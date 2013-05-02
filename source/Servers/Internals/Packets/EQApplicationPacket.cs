using System;
using System.Runtime.InteropServices;
using System.Net;

namespace EQEmulator.Servers.Internals.Packets
{
    internal class EQApplicationPacket<TPacketStruct> : EQRawApplicationPacket
        // TODO: remove comment  where TPacketStruct : class
    {
        private TPacketStruct _packetStruct;

        internal EQApplicationPacket(EQRawApplicationPacket rawPacket)
        {
            _clientIPE = rawPacket.ClientIPE;
            _data = rawPacket.RawPacketData;
            _OpCode = rawPacket.OpCode;

            // TODO: improve perf by using an unsafe pinned pointer instead of GCHandle
            GCHandle pinnedBytes = GCHandle.Alloc(GetPayload(), GCHandleType.Pinned);
            _packetStruct = (TPacketStruct)Marshal.PtrToStructure(pinnedBytes.AddrOfPinnedObject(), typeof(TPacketStruct));
            pinnedBytes.Free();
        }

        internal EQApplicationPacket(AppOpCode opCode, TPacketStruct packetStruct)
            : this(opCode, packetStruct, null)
        { }

        internal EQApplicationPacket(AppOpCode opCode, TPacketStruct packetStruct, IPEndPoint clientIPE)
        {
            _OpCode = opCode;
            _packetStruct = packetStruct;
            _clientIPE = clientIPE;

            // serialize to raw byte stream from opcode & struct... byte stream = opcode + structure
            int dataSize = Marshal.SizeOf(_packetStruct);
            byte[] structData = new byte[dataSize];
            GCHandle handle = GCHandle.Alloc(structData, GCHandleType.Pinned);
            IntPtr buffer = handle.AddrOfPinnedObject();
            Marshal.StructureToPtr(_packetStruct, buffer, false);
            handle.Free();

            dataSize += 2;   // opcode
            _data = new byte[dataSize];
            Buffer.BlockCopy(structData, 0, _data, 2, structData.Length);
            //ushort opCodeNO = (ushort)IPAddress.HostToNetworkOrder((short)_OpCode);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)_OpCode), 0, _data, 0, 2);  // Don't put in network order
        }

        internal TPacketStruct PacketStruct
        {
            get { return _packetStruct; }
            set { _packetStruct = value; }
        }

        internal string DumpStruct()
        {
            return _packetStruct.ToString() + " Dump... \n" + Utility.DumpStruct(this.PacketStruct);
        }
    }
}
