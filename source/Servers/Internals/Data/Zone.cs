using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using EQEmulator.Servers.Internals.Packets;

namespace EQEmulator.Servers.Internals.Data
{
    internal partial class Zone
    {
        private NewZone _newZone;

        public NewZone NewZoneStruct
        {
            get { return _newZone; }
            set { _newZone = value; }
        }

        internal void InitNewZoneStruct()
        {
            _newZone = new NewZone();
            _newZone.Init();
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(this.ShortName), 0, _newZone.ZoneShortName, 0, this.ShortName.Length);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(this.LongName), 0, _newZone.ZoneLongName, 0, this.LongName.Length);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(this.ShortName), 0, _newZone.ZoneShortName2, 0, this.ShortName.Length);
            _newZone.ZoneType = this.ZType;
            _newZone.FogRed[0] = this.FogRed;
            _newZone.FogRed[1] = this.FogRed2;
            _newZone.FogRed[2] = this.FogRed3;
            _newZone.FogRed[3] = this.FogRed4;
            _newZone.FogGreen[0] = this.FogGreen;
            _newZone.FogGreen[1] = this.FogGreen2;
            _newZone.FogGreen[2] = this.FogGreen3;
            _newZone.FogGreen[3] = this.FogGreen4;
            _newZone.FogBlue[0] = this.FogBlue;
            _newZone.FogBlue[1] = this.FogBlue2;
            _newZone.FogBlue[2] = this.FogBlue3;
            _newZone.FogBlue[3] = this.FogBlue4;
            _newZone.FogMinClip[0] = this.FogMinClip;
            _newZone.FogMinClip[1] = this.FogMinClip2;
            _newZone.FogMinClip[2] = this.FogMinClip3;
            _newZone.FogMinClip[3] = this.FogMinClip4;
            _newZone.FogMaxClip[0] = this.FogMaxClip;
            _newZone.FogMaxClip[1] = this.FogMaxClip2;
            _newZone.FogMaxClip[2] = this.FogMaxClip3;
            _newZone.FogMaxClip[3] = this.FogMaxClip4;
            _newZone.Gravity = 0.4F;    // TODO: source in DB?
            _newZone.TimeType = this.TimeType;
            _newZone.Sky = this.Sky;
            _newZone.ZoneExpMultiplier = this.XPMultiplier;
            _newZone.SafeX = this.SafeX;
            _newZone.SafeY = this.SafeY;
            _newZone.SafeZ = this.SafeZ;
            _newZone.Underworld = this.UnderWorld;
            _newZone.MinClip = this.MinClip;
            _newZone.MaxClip = this.MaxClip;
            _newZone.ZoneId = (ushort)_ZoneID;
        }
    }
}
