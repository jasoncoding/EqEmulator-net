using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EQEmulator.Servers.Internals.Entities
{
    internal class LootItem
    {
        public uint ItemID { get; set; }
        public short EquipSlot { get; set; }
        public byte Charges { get; set; }
        public short LootSlot { get; set; }
        public string IDFile { get; set; }
        public int Material { get; set; }
        public byte ItemType { get; set; }
        public short Damage { get; set; }
        public uint Color { get; set; }
        public uint Augmentation1 { get; set; }
        public uint Augmentation2 { get; set; }
        public uint Augmentation3 { get; set; }
        public uint Augmentation4 { get; set; }
        public uint Augmentation5 { get; set; }

        public LootItem()
        {
            ItemID = 0;
            EquipSlot = 0;
            Charges = 0;
            LootSlot = 0;
            IDFile = string.Empty;
            Material = 0;
            ItemType = 0;
            Damage = 0;
            Augmentation1 = 0;
            Augmentation2 = 0;
            Augmentation3 = 0;
            Augmentation4 = 0;
            Augmentation5 = 0;
        }
    }
}
