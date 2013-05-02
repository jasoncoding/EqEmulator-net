using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using EQEmulator.Servers.Internals.Packets;
using log4net;

namespace EQEmulator.Servers.Internals
{
    internal class Guild    // TODO: candidate for future shared cache?
    {
        protected static readonly ILog _log = LogManager.GetLogger(typeof(Guild));

        public const int MAX_NUMBER_GUILDS = 1500;
        public static uint GUILD_NONE = 0xffffffff;   // user has no guild

        public enum GuildRank
        {
            Member  = 0,
            Officer = 1,
            Leader  = 2,
            None    = 3
        }

        public enum GuildAction
        {
            Hear    = 0,
            Speak   = 1,
            Invite  = 2,
            Remove  = 3,
            Promote = 4,
            Demote  = 5,
            MOTD    = 6,    // should this be "SetMOTD"?
            WarPeace= 7     // this necessary?
        }

        internal static byte[] ListGuilds()
        {
            int len = 64 + (MAX_NUMBER_GUILDS * 64);    // Could use Marshal.SizeOf() but why
            byte[] buffer = new byte[len];

            int r, pos;
	        for(r = 0, pos = 0; r <= MAX_NUMBER_GUILDS; r++, pos += 64)
		        Buffer.BlockCopy(Encoding.ASCII.GetBytes("BAD GUILD"), 0, buffer, pos, 9);

            Utility.NullPadBuffer(ref buffer, 0, string.Empty, 64, true);

            // TODO: instead of adding a couple bullshit guilds, add database calls to retrieve guild list from db
            //Utility.NullPadBuffer(ref buffer, 64, "Warriors of the Greenmist", 64);
            //Utility.NullPadBuffer(ref buffer, 128, "Flames of Freedom", 64);

            return buffer;
        }
    }
}
