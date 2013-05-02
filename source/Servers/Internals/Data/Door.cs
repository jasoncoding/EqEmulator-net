using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using EQEmulator.Servers.Internals.Packets;

namespace EQEmulator.Servers.Internals.Data
{
    internal partial class Door
    {
        private const byte OPEN_DOOR = 0x02;
        private const byte CLOSE_DOOR = 0x03;

        private short _entityId = 0;
        private DateTime _openedAt = DateTime.MinValue;

        public short EntityId
        {
            get { return _entityId; }
            set { _entityId = value; }
        }

        public DateTime OpenedAt
        {
            get { return _openedAt; }
            set { _openedAt = value; }
        }

        internal bool IsOpen()
        {
            return OpenedAt > DateTime.MinValue;
        }

        internal ZoneDoor GetDoorStruct()
        {
            ZoneDoor ds = new ZoneDoor();
            ds.Init();

            Buffer.BlockCopy(Encoding.ASCII.GetBytes(this.DoorName), 0, ds.Name, 0, this.DoorName.Length);
            ds.XPos = this.X;
            ds.YPos = this.Y;
            ds.ZPos = this.Z;
            ds.Heading = this.Heading;
            ds.Incline = this.Incline;
            ds.Size = this.Size;
            ds.DoorId = (byte)this.Ordinal;
            ds.OpenType = (byte)this.OpenType;
            ds.StateAtSpawn = (this.InvertState ? !IsOpen() : IsOpen()) ? (byte)1 : (byte)0;
            ds.InvertState = this.InvertState ? (byte)1 : (byte)0;
            ds.DoorParam = this.DoorParam;

            return ds;
        }

        /// <summary>Processes a client clicking this door.</summary>
        /// <param name="clicker">The toon that clicked the door.</param>
        /// <returns>null if the player was unable to open door, else a MoveDoor structure for the clicked door.</returns>
        internal MoveDoor? Click(ZonePlayer clicker)
        {
            MoveDoor md = new MoveDoor();
            md.DoorId = (byte)this.Ordinal;

            if ((this.KeyItemID == null && this.LockPick == 0) || (IsOpen() && this.OpenType == 58)) // TODO: add checks for guild doors
            {
                // not locked
                if (!IsOpen() || this.OpenType == 58)   // wtf is type 58?  Is that a teleporter?
                {
                    md.Action = OPEN_DOOR;
                    this.OpenedAt = DateTime.Now;
                }
                else
                {
                    md.Action = CLOSE_DOOR;
                    this.OpenedAt = DateTime.MinValue;
                }
            }
            else
            {
                // TODO: add checks for lockpicking, keys, flags, etc. on locked doors
            }

            // TODO: add support for teleport doors

            return md;
        }

        internal void Close()
        {
            this.OpenedAt = DateTime.MinValue;
        }
    }
}
