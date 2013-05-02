using System;

using log4net;

namespace EQEmulator.Servers.Internals
{
    public abstract class MapBase
    {
        protected const string MAP_DIR = ".\\Maps";
        protected const uint MAP_VERSION = 0x01000000;

        protected static readonly ILog _log = LogManager.GetLogger(typeof(MapBase));

        internal abstract bool LoadMapFromFile(string mapName);     // implement in derived class to process maps
    }
}
