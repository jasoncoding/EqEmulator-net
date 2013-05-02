using System;
using System.Runtime.InteropServices;

namespace EQEmulator.Servers.Internals.Packets
{
    //[StructLayout(LayoutKind.Sequential, Pack = 1)]
    //struct SessionRequest
    //{
    //    public UInt32 UnknownA;
    //    public UInt32 Session;
    //    public UInt32 MaxLength;
    //}

    internal class SessionRequestPacket : RawEQPacket
    {
        private SessionRequest _sessionRequest;

        internal SessionRequestPacket(RawEQPacket rawPacket)
        {
            this._clientIPE = rawPacket.ClientIPE;
            this._data = rawPacket.RawPacketData;
            this._SentCRC = rawPacket.SentCRC;
            this._OpCode = rawPacket.OpCode;

            GCHandle pinnedBytes = GCHandle.Alloc(GetPayload(), GCHandleType.Pinned);
            _sessionRequest = (SessionRequest)Marshal.PtrToStructure(pinnedBytes.AddrOfPinnedObject(), typeof(SessionRequest));
            pinnedBytes.Free();
        }

        public SessionRequest SessionRequest { get { return _sessionRequest; } }
    }
}
