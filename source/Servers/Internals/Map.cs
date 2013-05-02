using System;
using System.IO;
using System.Runtime.InteropServices;

namespace EQEmulator.Servers.Internals
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct MapHeader
    {
        public uint Version;
        public uint FaceCount;
        public ushort NodeCount;
        public uint FaceListCount;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct Node
    {
        public ushort MinX;
        public ushort MinY;
        public ushort MaxX;
        public ushort MaxY;
        public byte Flags;
        [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 8)]
        public byte[] NodesFaces;   // TODO: perhaps implement an indexer to match the union in the original (see below)
    }

    // Original NodesFaces union:
    //union {
    //    unsigned short nodes[4];	//index 0 means NULL, not root
    //    struct {
    //        unsigned long count;
    //        unsigned long offset;
    //    } faces;
    //};

    public struct Vertex
    {
        public float X;
        public float Y;
        public float Z;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Face
    {
        public Vertex A;
        public Vertex B;
        public Vertex C;
        public float NX;
        public float NY;
        public float NZ;
        public float ND;
    }

    public class Map : MapBase
    {
        private float _minZ, _maxZ, _minX, _maxX, _minY, _maxY;
        private uint _facesCnt, _nodesCnt, _faceListsCnt;
        private Face[] _finalFaces;
        private Node[] _nodes;
        private uint[] _faceLists;

        public Map()
        {
            _minZ = float.MaxValue;
            _minX = float.MaxValue;
            _minY = float.MaxValue;
            _maxZ = float.MinValue;
            _maxX = float.MinValue;
            _maxY = float.MinValue;
        }

        public Face[] Faces
        {
            get { return _finalFaces; }
        }

        public float MinX
        {
            get { return _minX; }
        }

        public float MaxX
        {
            get { return _maxX; }
        }

        public float MinY
        {
            get { return _minY; }
        }

        public float MaxY
        {
            get { return _maxY; }
        }

        public float MinZ
        {
            get { return _minZ; }
        }

        public float MaxZ
        {
            get { return _maxZ; }
        }

        internal override bool LoadMapFromFile(string mapName)
        {
            string mapFilePath = Path.Combine(MAP_DIR, mapName + ".map");
            
            if (!File.Exists(mapFilePath))
            {
                _log.ErrorFormat("Unable to find map file {0}", mapFilePath);
                return false;
            }

            MapHeader mapHdr = new MapHeader();
            using (BinaryReader binRdr = new BinaryReader(File.OpenRead(mapFilePath)))
            {
                byte[] buffer = binRdr.ReadBytes(Marshal.SizeOf(mapHdr));
                GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                mapHdr = (MapHeader)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(MapHeader));
                handle.Free();

                if (mapHdr.Version != MAP_VERSION)
                {
                    _log.ErrorFormat("Invalid map version detected: {0}", mapHdr.Version);
                    return false;
                }

                _log.DebugFormat("Map header: {0} faces, {1} nodes, {2} facelists", mapHdr.FaceCount, mapHdr.NodeCount, mapHdr.FaceListCount);
                _facesCnt = mapHdr.FaceCount;
                _nodesCnt = mapHdr.NodeCount;
                _faceListsCnt = mapHdr.FaceListCount;
                _finalFaces = new Face[_facesCnt];
                _nodes = new Node[_nodesCnt];
                _faceLists = new uint[_faceListsCnt];

                // read faces
                for (int i = 0; i < _facesCnt; i++)
                {
                    buffer = binRdr.ReadBytes(Marshal.SizeOf(typeof(Face)));
                    handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                    _finalFaces[i] = (Face)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(Face));
                    handle.Free();
                }

                // read nodes
                for (int i = 0; i < _nodesCnt; i++)
                {
                    buffer = binRdr.ReadBytes(Marshal.SizeOf(typeof(Node)));
                    handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                    _nodes[i] = (Node)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(Node));
                    handle.Free();
                }

                // read face lists
                for (int i = 0; i < _faceListsCnt; i++)
                    _faceLists[i] = binRdr.ReadUInt32();

                float v = 0.0F;
                for (int i = 0; i < _facesCnt; i++)
                {
                    v = VMax3(_finalFaces[i].A.X, _finalFaces[i].B.X, _finalFaces[i].C.X);
                    if (v > _maxX)
                        _maxX = v;
                    v = VMin3(_finalFaces[i].A.X, _finalFaces[i].B.X, _finalFaces[i].C.X);
                    if (v < _minX)
                        _minX = v;
                    v = VMax3(_finalFaces[i].A.Y, _finalFaces[i].B.Y, _finalFaces[i].C.Y);
                    if (v > _maxY)
                        _maxY = v;
                    v = VMin3(_finalFaces[i].A.Y, _finalFaces[i].B.Y, _finalFaces[i].C.Y);
                    if (v < _minY)
                        _minY = v;
                    v = VMax3(_finalFaces[i].A.Z, _finalFaces[i].B.Z, _finalFaces[i].C.Z);
                    if (v > _maxZ)
                        _maxZ = v;
                    v = VMin3(_finalFaces[i].A.Z, _finalFaces[i].B.Z, _finalFaces[i].C.Z);
                    if (v < _minZ)
                        _minZ = v;
                }

                _log.DebugFormat("Loaded map: {0} vertices, {1} faces", _facesCnt * 3, _facesCnt);
                _log.DebugFormat("Map BB: ({0:F2} -> {1:F2}, {2:F2} -> {3:F2}, {4:F2} -> {5:F2})", _minX, _maxX, _minY, _maxY, _minZ, _maxZ);

                return true;
            }
        }

        // vertex clean up functions
        private float VMin3(float a, float b, float c)
        {
            return (a < b) ? (a < c ? a : c) : (b < c ? b : c);
        }

        private float VMax3(float a, float b, float c)
        {
            return (a > b) ? (a > c ? a : c) : (b > c ? b : c);
        }
    }
}
