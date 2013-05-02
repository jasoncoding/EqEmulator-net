using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using log4net;

namespace EQEmulator.Servers.Internals
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ZBSPNode
    {
        public int NodeNumber;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.R4, SizeConst = 3)]
        public float[] Normal;
        public float SplitDistance;
        public int Region;
        public int Special;
        public int Left;
        public int Right;
    }

    public class WaterMap : MapBase
    {
        private const int WATERMAP_VERSION = 1;

        private ZBSPNode[] _bspRoot;

        internal override bool LoadMapFromFile(string mapName)
        {
            string mapFilePath = Path.Combine(MAP_DIR, mapName + ".wtr");

            if (!File.Exists(mapFilePath))
            {
                _log.ErrorFormat("Unable to find map file {0}", mapFilePath);
                return false;
            }

            byte[] EQWMagicBuf = new byte[10];
            uint BSPTreeSize, EQWVersion;

            using (BinaryReader binRdr = new BinaryReader(File.OpenRead(mapFilePath), Encoding.ASCII))
            {
                EQWMagicBuf = binRdr.ReadBytes(10);
                if (string.Compare(Encoding.ASCII.GetString(EQWMagicBuf), "EQEMUWATER", true) != 0)
                {
                    _log.ErrorFormat("Bad header in water region map {0}.", Encoding.ASCII.GetString(EQWMagicBuf));
                    return false;
                }

                EQWVersion = binRdr.ReadUInt32();
                if (EQWVersion != 1)
                {
                    _log.Error("Incompatible water region map version.");
                    return false;
                }

                BSPTreeSize = binRdr.ReadUInt32();
                _bspRoot = new ZBSPNode[BSPTreeSize];

                byte[] buffer = null;
                GCHandle handle;
                for (int i = 0; i < BSPTreeSize; i++)
                {
                    buffer = binRdr.ReadBytes(Marshal.SizeOf(typeof(ZBSPNode)));
                    handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                    _bspRoot[i] = (ZBSPNode)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(ZBSPNode));
                    handle.Free();
                }
            }

            _log.DebugFormat("Water region map has {0} nodes.", BSPTreeSize);
            return true;
        }
    }
}
