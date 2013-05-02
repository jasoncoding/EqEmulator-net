using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Data.Linq.Mapping;

using EQEmulator.Servers.ExtensionMethods;

namespace EQEmulator.Servers.Internals.Data
{
    internal partial class InventoryItem
    {
        private List<InventoryItem> _subItems = new List<InventoryItem> {null, null, null, null, null, null, null, null, null, null};

        internal string Serialize()
        {
            return Serialize(0);
        }

        private string Serialize(int depth)
        {
            string protection = @"\\\\\";

            string instanceData = string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|",
                this.Item.stackable ? this.Charges : 0,
                0,
                this.SlotID,    // TODO: handled differently for merchants, but that will be in a merchantItem class right?
                0,  // TODO: when bazaar is in or for merchants, set the price
                1,  // TODO: for merchants, the amount for sale
                0,
                this.Item.SerialNumber,   // TODO: For merchants, the merchant slot
                0,  // TODO: when attuneable items are in, db will support it and we can place the value here
                (this.Item.stackable ? ((ItemType)this.Item.ItemType == ItemType.Potion ? 1 : 0) : Charges.Value),
                0,
                0);

            // Sub-items
            string[] subItemData = new string[10];
            for (int i = 0; i < _subItems.Count; i++) {
                if (_subItems[i] != null)
                    subItemData[i] = _subItems[i].Serialize(depth + 1);
                else
                    subItemData[i] = string.Empty;
            }

            string staticData = string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}|{15}|{16}|{17}|{18}|{19}|" +
                "{20}|{21}|{22}|{23}|{24}|{25}|{26}|{27}|{28}|{29}|{30}|{31}|{32}|{33}|{34}|{35}|{36}|{37}|{38}|{39}|{40}|{41}|{42}|{43}|" +
                "{44}|{45}|{46}|{47}|{48}|{49}|{50}|{51}|{52}|{53}|{54}|{55}|{56}|{57}|{58:f6}|{59}|{60}|{61}|{62}|{63}|{64}|{65}|{66}|{67}|" +
                "{68}|{69}|{70}|{71}|{72}|{73}|{74}|{75}|{76}|{77}|{78}|{79}|{80}|{81}|{82}|{83}|{84}|{85}|{86}|{87}|{88}|{89}|{90}|{91}|" +
                "{92}|{93}|{94}|{95}|{96}|{97}|{98}|{99}|{100}|{101}|{102}|{103}|{104}|{105}|{106}|{107}|{108}|{109}|{110}|{111}|{112}|" +
                "{113}|{114}|{115}|{116}|{117}|{118}|{119}|{120}|{121}|{122}|{123}|{124}|{125}|{126}|{127}|{128}|{129}|{130}|{131}|{132}|" +
                "{133}|{134}|{135}|{136}|{137}|{138}|{139}|{140}|{141}|{142}|{143}|{144}|{145}|{146}|{147}|{148}|{149}|{150}|{151}|{152}|{153}|{154}|{155}|{156}|{157}|{158}",
                this.Item.ItemClass,        // 0
                this.Item.Name,             // 1
                string.IsNullOrEmpty(this.Item.Lore) ? "0" : this.Item.Lore,    // 2
                this.Item.IDFile,           // 3
                this.Item.ItemID,           // 4
                this.Item.weight,           // 5
                this.Item.NoRent,           // 6
                this.Item.NoDrop,           // 7
                this.Item.Size,             // 8
                this.Item.slots,            // 9
                this.Item.Price,            // 10
                this.Item.Icon,             // 11
                0, 0,                       // 12 & 13
                this.Item.Benefit,          // 14
                this.Item.tradeskills,      // 15
                this.Item.CRBonus,          // 16
                this.Item.DRBonus,          // 17
                this.Item.PRBonus,          // 18
                this.Item.MRBonus,          // 19
                this.Item.FRBonus,          // 20
                this.Item.StrBonus,         // 21
                this.Item.StaBonus,         // 22
                this.Item.AgiBonus,         // 23
                this.Item.DexBonus,         // 24
                this.Item.ChaBonus,         // 25
                this.Item.IntBonus,         // 26
                this.Item.WisBonus,         // 27
                this.Item.HPBonus,          // 28
                this.Item.ManaBonus,        // 29
                this.Item.ACBonus,          // 30
                this.Item.Deity,            // 31
                this.Item.skillmodvalue,    // 32
                0,                          // 33
                this.Item.skillmodtype,     // 34
                this.Item.BaneDmgRace,      // 35
                this.Item.BaneDmgBodyAmt,   // 36
                this.Item.BaneDmgBody,      // 37
                this.Item.IsMagic ? 1 : 0,  //38
                this.Item.ClickEffectCastTime,// 39
                this.Item.ReqLevel,         // 40
                this.Item.BardType,         // 41
                this.Item.BardValue,        // 42
                this.Item.Light,            // 43
                this.Item.Delay,            // 44
                this.Item.RecLevel,         // 45
                this.Item.RecSkill,         // 46
                this.Item.ElemDmgType,      // 47
                this.Item.ElemDmgAmt,       // 48
                this.Item.Range,            // 49
                this.Item.Damage,
                this.Item.Color,
                this.Item.Classes,
                this.Item.Races,
                0,
                this.Item.MaxCharges,
                this.Item.ItemType,
                this.Item.Material,
                this.Item.SellRate,         // 58
                0,
                this.Item.ClickEffectCastTime,
                0,
                this.Item.ProcRate,
                this.Item.CombatEffects,
                this.Item.Shielding,
                this.Item.stunresist,
                this.Item.strikethrough,
                this.Item.ExtraDmgSkill,
                this.Item.ExtraDmgAmt,
                this.Item.spellshield,
                this.Item.Avoidance,
                this.Item.Accuracy,
                this.Item.CharmFileID,
                this.Item.FactionMod1,
                this.Item.FactionMod2,
                this.Item.FactionMod3,
                this.Item.FactionMod4,
                this.Item.FactionAmt1,
                this.Item.FactionAmt2,
                this.Item.FactionAmt3,
                this.Item.FactionAmt4,
                this.Item.CharmFile,
                this.Item.AugType,
                this.Item.AugSlot1Type,
                this.Item.AugSlot1Vis,
                this.Item.AugSlot2Type,
                this.Item.AugSlot2Vis,
                this.Item.AugSlot3Type,
                this.Item.AugSlot3Vis,
                this.Item.AugSlot4Type,
                this.Item.AugSlot4Vis,
                this.Item.AugSlot5Type,
                this.Item.AugSlot5Vis,
                this.Item.LdonTheme,
                this.Item.LdonPrice,
                this.Item.Ldonsold,
                this.Item.BagType,
                this.Item.BagSlots,
                this.Item.BagSize,
                this.Item.BagWeightReduc,
                this.Item.Book,
                this.Item.booktype,
                this.Item.BookFileName,
                this.Item.BaneDmgRaceAmt,
                this.Item.AugRestrict,
                this.Item.LoreGroup,
                this.Item.HasPendingLoreFlag ? 1 : 0,
                this.Item.IsArtifact ? 1 : 0,
                this.Item.IsSummoned ? 1 : 0,
                this.Item.favor,
                this.Item.FVNoDrop ? 1 : 0,
                this.Item.EnduranceBonus,
                this.Item.DotShielding,
                this.Item.AttackBonus,
                this.Item.Regen,
                this.Item.ManaRegen,
                this.Item.EnduranceRegen,
                this.Item.HastePct,
                this.Item.DamageShield,
                this.Item.recastdelay,
                this.Item.recasttype,
                this.Item.guildfavor,
                this.Item.AugDistiller,
                0, 0,
                this.Item.IsAttuneable ? 1 : 0,
                this.Item.nopet ? 1 : 0,
                0,
                this.Item.pointtype,
                this.Item.potionbelt,
                0,
                this.Item.stacksize,
                this.Item.notransfer ? 1 : 0,
                this.Item.stackable ? 1 : 0,
                this.Item.clickeffect,
                this.Item.ClickType,
                this.Item.ClickLevelMax,
                this.Item.ClickLevelReq,
                0,
                this.Item.proceffect,
                this.Item.proctype,
                this.Item.proclevel2,
                this.Item.proclevel,
                0,
                this.Item.worneffect,
                this.Item.worntype,
                this.Item.wornlevel2,
                this.Item.wornlevel,
                0,
                this.Item.FocusEffectSpellID,
                this.Item.focustype,
                this.Item.focuslevel2,
                this.Item.focuslevel,
                0,
                this.Item.scrolleffect,
                this.Item.scrolltype,
                this.Item.scrolllevel2,
                this.Item.scrolllevel,
                0);

            string serializedData = string.Format("{0}{1}" +    // Leading quotes (and protection) if a sub-items
                                                  "{2}" +       // Instance data
                                                  "{3}\"" +     // Quotes (and protection - if needed) around static data
                                                  "{4}" +       // Static data
                                                  "{5}\"" +     // Quotes (and protection - if needed) around static data
                                                  "|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}|{15}" +    // sub-items
                                                  "{16}{17}",   // Trailing quotes (and protection) if a sub-item
                depth > 0 ? protection.Substring(0, depth - 1) : string.Empty,
                depth > 0 ? "\"" : string.Empty,
                instanceData,
                protection.Substring(0, depth),
                staticData,
                protection.Substring(0, depth),
                subItemData[0],
                subItemData[1],
                subItemData[2],
                subItemData[3],
                subItemData[4],
                subItemData[5],
                subItemData[6],
                subItemData[7],
                subItemData[8],
                subItemData[9],
                depth > 0 ? protection.Substring(0, depth - 1) : string.Empty,
                depth > 0 ? "\"" : string.Empty);

            return serializedData;
        }

        internal void ClearSubItems()
        {
            _subItems = new List<InventoryItem> { null, null, null, null, null, null, null, null, null, null };
        }

        internal void AddSubItem(int containerIdx, InventoryItem invItem)
        {
            _subItems[containerIdx] = invItem;
        }

        internal InventoryItem ShallowCopy()
        {
            PropertyInfo[] sourcePropInfos = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo[] destinationPropInfos = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            // create an object to copy values into
            Type entityType = GetType();
            InventoryItem destination;
            destination = Activator.CreateInstance(entityType) as InventoryItem;

            foreach (PropertyInfo sourcePropInfo in sourcePropInfos)
            {
                if (Attribute.GetCustomAttribute(sourcePropInfo, typeof(ColumnAttribute), false) != null)
                {
                    PropertyInfo destPropInfo = destinationPropInfos.Where(pi => pi.Name == sourcePropInfo.Name).First();
                    destPropInfo.SetValue(destination, sourcePropInfo.GetValue(this, null), null);
                }
            }

            return destination;

            //return this.MemberwiseClone() as InventoryItem;
        }

        /// <summary>Iterates non-null sub-items.</summary>
        internal IEnumerable<InventoryItem> SubItems()
        {
            for (int i = 0; i < this.Item.BagSlots; i++) {
                if (_subItems[i] != null)
                    yield return _subItems[i];
            }
        }

        internal InventoryItem ToNewInventoryItem()
        {
            InventoryItem newItem = new InventoryItem()   // TODO: copy augments also
            {
                ItemID = this.ItemID,
                Item = this.Item,
                Charges = this.Charges,
                Color = this.Color
            };

            return newItem;
        }

        /// <summary>Generates an item link for this item.  Used in several operations.</summary>
        /// <returns>Item link representation of this item.</returns>
        /// <remarks>usage: http://eqitems.13th-floor.org/phpBB2/viewtopic.php?p=510#510
        /// evolving item info: http://eqitems.13th-floor.org/phpBB2/viewtopic.php?t=145#558</remarks>
        internal byte[] ToItemLink()
        {
            byte evolving = 0, evolvedLevel = 0;    // TODO: evolving items (DoD era)
            ushort loreGroup = 0;
            int hash = 0;
            string link = string.Format("0{0:X5}{1:X5}{2:X5}{3:X5}{4:X5}{5:X5}{6}{7:X4}{8}{9:X8}",
                this.ItemID,
                0,          // TODO: Augmentations (LDoN era)
                0,
                0,
                0,
                0,
                evolving,
                loreGroup,
                evolvedLevel,
                hash);

            byte[] linkBytes = new byte[2 + link.Length + this.Item.Name.Length];   // 2 for the bracketing 0x12 bytes
            linkBytes[0] = 0x12;
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(link), 0, linkBytes, 1, link.Length);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(this.Item.Name), 0, linkBytes, 1 + link.Length, this.Item.Name.Length);
            linkBytes[linkBytes.Length - 1] = 0x12;

            return linkBytes;
        }

        public override string ToString()
        {
            string ret = this.InventoryItemID.ToString();
            ret += this.Item != null ? "|" + this.Item.Name + "|" + this.ItemID : string.Empty;

            return ret;
        }
    }
}