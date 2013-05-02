using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using EQEmulator.Servers.Internals;
using EQEmulator.Servers.Internals.Data;

namespace EQEmulator.Servers
{
    public partial class ZoneServer
    {
        internal void DispatchServerCommand(ZonePlayer zp, ServerCommand cmd, Dictionary<string, string> args)
        {
            switch (cmd) {
                case ServerCommand.Zone:
                    Zone zone;
                    using (EmuDataContext dbCtx = new EmuDataContext()) {
                        dbCtx.ObjectTrackingEnabled = false;
                        zone = dbCtx.Zones.SingleOrDefault(z => z.ShortName == args["name"]);
                    }

                    if (zone != null)
                        MovePlayer(zp, zone.ZoneID, 0u, zone.SafeX, zone.SafeY, zone.SafeZ, 0.0f, ZoneMode.ZoneToSafeCoords);
                    else
                        zp.MsgMgr.SendSpecialMessage(MessageType.Default, "Unable to locate zone " + args["name"]);

                    break;
                case ServerCommand.GoTo:
                    break;
                default:
                    break;
            }
        }
    }
}
