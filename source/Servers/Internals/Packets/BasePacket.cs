using System;
using System.Net;

namespace EQEmulator.Servers.Internals.Packets
{
    internal class BasePacket
    {
        protected IPEndPoint _clientIPE = null;
        protected byte[] _data = null;

        protected BasePacket() { }

        public BasePacket(IPEndPoint remoteIPE, byte[] data)
        {
            _clientIPE = remoteIPE;
            _data = data;
        }

        public IPEndPoint ClientIPE
        {
            get { return _clientIPE; }
            set { _clientIPE = value; }
        }

        /// <summary>This is everything (OpCode, crc, etc.)</summary>
        public byte[] RawPacketData { get { return _data; } }

        public void AppendData(byte[] appData)
        {
            //byte[] newBytes = new byte[_data.Length + appData.Length];
            Array.Resize<byte>(ref _data, _data.Length + appData.Length);
            Buffer.BlockCopy(appData, 0, _data, _data.Length - appData.Length, appData.Length);    // TODO: expensive, refactor into a bytebuffer/memorystream class
        }
    }
}
