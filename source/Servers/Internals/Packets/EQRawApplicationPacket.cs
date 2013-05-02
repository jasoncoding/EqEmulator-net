using System;
using System.IO;
using System.Net;

using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace EQEmulator.Servers.Internals.Packets
{
    internal class EQRawApplicationPacket : BasePacket
    {
        protected AppOpCode _OpCode = AppOpCode.None;
        private byte[] _payload = null;

        protected EQRawApplicationPacket() { }

        /// <summary>This ctor only parses and deserializes the opCode internally.</summary>
        internal EQRawApplicationPacket(IPEndPoint ipe, byte[] data)
            : base(ipe, data)
        {
            // OpCode is assumed sent backwards, so no need for endian switching
            _OpCode = (AppOpCode)BitConverter.ToUInt16(data, 0);
        }

        internal EQRawApplicationPacket(RawEQPacket rawPacket)
            : base(rawPacket.ClientIPE, rawPacket.RawPacketData)
        {
            _OpCode = (AppOpCode)rawPacket.RawOpCode;
        }

        /// <summary>This ctor serializes to the internal buffer with the given opCode and data</summary>
        internal EQRawApplicationPacket(AppOpCode OpCode, IPEndPoint ipe, byte[] data)
        {
            _OpCode = OpCode;
            _clientIPE = ipe;

            // serialize to raw byte stream from opcode & passed data
            int dataSize = (data != null) ? 2 + data.Length : 2;   // allow for opcode
            _data = new byte[dataSize];
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)_OpCode), 0, _data, 0, 2);   // Don't put in network order?
            if (data != null)
                Buffer.BlockCopy(data, 0, _data, 2, data.Length);
        }

        public AppOpCode OpCode
        {
            get { return _OpCode; }
        }

        public byte[] GetPayload()
        {
            if (_payload == null)
            {
                _payload = new byte[_data.Length - 2];
                Buffer.BlockCopy(_data, 2, _payload, 0, _data.Length - 2);  // snip the application opcode
            }

            return _payload;
        }

        public static int Deflate(byte[] inBuffer, ref byte[] outBuffer)
        {
            int newLen = 0, flagOffset = 1;

            outBuffer[0] = inBuffer[0];
            if (inBuffer[0] == 0)
            {
                flagOffset = 2;
                outBuffer[1] = inBuffer[1];
            }

            if (inBuffer.Length > 30)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    Deflater deflater = new Deflater(Deflater.DEFAULT_COMPRESSION, false);
                    using (DeflaterOutputStream outStream = new DeflaterOutputStream(ms, deflater))
                    {
                        outStream.IsStreamOwner = false;
                        outStream.Write(inBuffer, flagOffset, inBuffer.Length - flagOffset);
                        outStream.Flush();
                        outStream.Finish();
                    }

                    ms.Position = 0;
                    ms.Read(outBuffer, flagOffset + 1, (int)ms.Length);
                    newLen = (int)ms.Length + flagOffset + 1;
                    outBuffer[flagOffset] = 0x5a;
                }
            }
            else
            {
                Buffer.BlockCopy(inBuffer, flagOffset, outBuffer, flagOffset + 1, inBuffer.Length - flagOffset);
                outBuffer[flagOffset] = 0xa5;
                newLen = inBuffer.Length + 1;
            }

            return newLen;
        }

        public static int Inflate(byte[] inBuffer, ref byte[] outBuffer)
        {
            int newLen = 0, flagOffset = 0;

            outBuffer[0] = inBuffer[0];
            if (inBuffer[0] == 0x00)
            {
                flagOffset = 2;
                outBuffer[1] = inBuffer[1];
            }
            else
                flagOffset = 1;

            if (inBuffer.Length > 2 && inBuffer[flagOffset] == 0x5a)
            {
                // TODO: move streams from instance to private statics for perf
                using (MemoryStream ms = new MemoryStream(inBuffer, flagOffset + 1, inBuffer.Length - (flagOffset + 1) - 2))
                {
                    Inflater inflater = new Inflater(false);
                    InflaterInputStream inStream = new InflaterInputStream(ms, inflater);
                    newLen = inStream.Read(outBuffer, flagOffset, outBuffer.Length - flagOffset) + 2;

                    outBuffer[newLen++] = inBuffer[inBuffer.Length - 2];
                    outBuffer[newLen++] = inBuffer[inBuffer.Length - 1];
                }
            }
            else if (inBuffer.Length > 2 && inBuffer[flagOffset] == 0xa5)
            {
                Buffer.BlockCopy(inBuffer, flagOffset + 1, outBuffer, flagOffset, inBuffer.Length - (flagOffset + 1));
                newLen = inBuffer.Length - 1;
            }
            else
            {
                Buffer.BlockCopy(inBuffer, 0, outBuffer, 0, inBuffer.Length);
                newLen = inBuffer.Length;
            }

            return newLen;
        }
    }
}
