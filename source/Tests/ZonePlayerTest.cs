using System.Data.Linq;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using EQEmulator.Servers.Internals;
using EQEmulator.Servers.Internals.Data;
using System.Collections.Generic;
using EQEmulator.Servers.Internals.Packets;
using EQEmulator.Servers;
using System.Diagnostics;
using System;
using EQEmulator.Servers.Internals.Entities;

namespace Tests
{
    /// <summary>
    ///This is a test class for ZonePlayerTest and is intended to contain all ZonePlayerTest Unit Tests
    ///</summary>
    [TestClass()]
    public class ZonePlayerTest
    {
        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        // 
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        //[ClassInitialize()]
        //public static void MyClassInitialize(TestContext testContext)
        //{
        //}
        //
        //Use ClassCleanup to run code after all tests in a class have run
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        //Use TestInitialize to run code before running each test
        //[TestInitialize()]
        //public void MyTestInitialize()
        //{
        //}
        //
        //Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //
        #endregion

        //[TestMethod()]
        public void Inventory_Should_Have_Item_In_Correct_Spot_After_Moving_A_Container()
        {
            // ARRANGE
            // Get the client's character info
            string charName = "Littlebadwiz";
            Character toon = null;
            DataLoadOptions dlo = new DataLoadOptions();
            dlo.LoadWith<Character>(c => c.Account);
            dlo.LoadWith<Character>(c => c.Zone);
            dlo.LoadWith<Character>(c => c.InventoryItems);
            dlo.LoadWith<InventoryItem>(ii => ii.Item);

            using (EmuDataContext dbCtx = new EmuDataContext()) {
                dbCtx.ObjectTrackingEnabled = false;
                dbCtx.LoadOptions = dlo;
                toon = dbCtx.Characters.SingleOrDefault(c => c.Name == charName);
            }

            ZonePlayer zp = new ZonePlayer(1, toon, 1, new Client(new System.Net.IPEndPoint(0x2414188f, 123)));
            List<uint?> itemsBefore = new List<uint?> { null, null, null, null, null, null, null, null, null, null };
            List<uint?> itemsAfter = new List<uint?> { null, null, null, null, null, null, null, null, null, null };

            for (int i = 321; i < 331; i++) {   // track the original item ids
                if (zp.InvMgr[i] != null)
                    itemsBefore[i - 321] = zp.InvMgr[i].ItemID;
            }

            // ACT
            zp.InvMgr.SwapItem(29, 30, (byte)0);    // swap a container in the 8th inv slot to the cursor
            zp.InvMgr.SwapItem(30, 26, (byte)0);    // then swap the container from the cursor to the 5th inv slot

            for (int i = 291; i < 301; i++) {   // now track the current item ids
                if (zp.InvMgr[i] != null)
                    itemsAfter[i - 291] = zp.InvMgr[i].ItemID;
            }

            // ASSERT
            for (int i = 0; i < 10; i++)
                Assert.IsTrue(itemsBefore[i] == itemsAfter[i]);
        }

        //[TestMethod()]
        public void Message_Should_Send_Correctly()
        {
            // ARRANGE
            //SpecialMessage msg = new SpecialMessage(0, "This is a sample message.");
            
            // ACT
            //Debug.WriteLine(BitConverter.ToString(msg.Serialize()));

            // ASSERT
            
        }

        //[TestMethod()]
        public void Weapon_Should_Equip_Correctly()
        {
            // ARRANGE
            Character toon = null;
            InventoryItem invItem = new InventoryItem();
            Item item = null;

            // Get the client's character info
            string charName = "Badass";
            DataLoadOptions dlo = new DataLoadOptions();
            dlo.LoadWith<Character>(c => c.Account);
            dlo.LoadWith<Character>(c => c.Zone);
            dlo.LoadWith<Character>(c => c.InventoryItems);
            dlo.LoadWith<InventoryItem>(ii => ii.Item);
            using (EmuDataContext dbCtx = new EmuDataContext()) {
                dbCtx.ObjectTrackingEnabled = false;
                dbCtx.LoadOptions = dlo;
                toon = dbCtx.Characters.SingleOrDefault(c => c.Name == charName);
            }

            ZonePlayer zp = new ZonePlayer(1, toon, 1, new Client(new System.Net.IPEndPoint(0x2414188f, 123)));

            // Get the inventory item we're giving to the char
            using (EmuDataContext dbCtx = new EmuDataContext()) {
                dbCtx.ObjectTrackingEnabled = false;
                item = dbCtx.Items.SingleOrDefault(i => i.ItemID == 5023);
            }
            invItem.Item = item;

            // ACT
            zp.AutoGiveItem(ref invItem);

            // ASSERT

        }

        //[TestMethod()]
        public void Command_Should_Parse_Correctly()
        {
            // ARRANGE
            Character toon = null;
            InventoryItem invItem = new InventoryItem();

            // Get the client's character info
            string charName = "Badass";
            DataLoadOptions dlo = new DataLoadOptions();
            dlo.LoadWith<Character>(c => c.Account);
            dlo.LoadWith<Character>(c => c.Zone);
            dlo.LoadWith<Character>(c => c.InventoryItems);
            dlo.LoadWith<InventoryItem>(ii => ii.Item);
            using (EmuDataContext dbCtx = new EmuDataContext()) {
                dbCtx.ObjectTrackingEnabled = false;
                dbCtx.LoadOptions = dlo;
                toon = dbCtx.Characters.SingleOrDefault(c => c.Name == charName);
            }

            ZonePlayer zp = new ZonePlayer(1, toon, 1, new Client(new System.Net.IPEndPoint(0x2414188f, 123)));

            // ACT
            //zp.MsgMgr.ReceiveChannelMessage("someTarget", "!damage /amount:200 /type:3", 8, 0, 100);
            zp.MsgMgr.ReceiveChannelMessage("someTarget", "!damage", 8, 0, 100);

            // ASSERT

        }

        //[TestMethod()]
        public void Command_Should_Recognize_Bad_Command()
        {
            // ARRANGE
            Character toon = null;
            InventoryItem invItem = new InventoryItem();

            // Get the client's character info
            string charName = "Badass";
            DataLoadOptions dlo = new DataLoadOptions();
            dlo.LoadWith<Character>(c => c.Account);
            dlo.LoadWith<Character>(c => c.Zone);
            dlo.LoadWith<Character>(c => c.InventoryItems);
            dlo.LoadWith<InventoryItem>(ii => ii.Item);
            using (EmuDataContext dbCtx = new EmuDataContext()) {
                dbCtx.ObjectTrackingEnabled = false;
                dbCtx.LoadOptions = dlo;
                toon = dbCtx.Characters.SingleOrDefault(c => c.Name == charName);
            }

            ZonePlayer zp = new ZonePlayer(1, toon, 1, new Client(new System.Net.IPEndPoint(0x2414188f, 123)));

            // ACT
            zp.MsgMgr.ReceiveChannelMessage("someTarget", "!shitCMd /badArg:200", 8, 0, 100);

            // ASSERT

        }

        [TestMethod()]
        public void Item_Should_Stack_Correctly_In_A_Container()
        {
            // ARRANGE
            Character toon = null;
            InventoryItem invItem = new InventoryItem();
            Item item = null;

            // Get the client's character info
            string charName = "Badass";
            DataLoadOptions dlo = new DataLoadOptions();
            dlo.LoadWith<Character>(c => c.Account);
            dlo.LoadWith<Character>(c => c.Zone);
            dlo.LoadWith<Character>(c => c.InventoryItems);
            dlo.LoadWith<InventoryItem>(ii => ii.Item);
            using (EmuDataContext dbCtx = new EmuDataContext()) {
                dbCtx.ObjectTrackingEnabled = false;
                dbCtx.LoadOptions = dlo;
                toon = dbCtx.Characters.SingleOrDefault(c => c.Name == charName);
            }

            ZonePlayer zp = new ZonePlayer(1, toon, 1, new Client(new System.Net.IPEndPoint(0x2414188f, 123)));

            // Get the inventory item (bone chips) we're giving to the char
            using (EmuDataContext dbCtx = new EmuDataContext()) {
                dbCtx.ObjectTrackingEnabled = false;
                item = dbCtx.Items.SingleOrDefault(i => i.ItemID == 13073);
            }
            invItem.Item = item;
            invItem.Charges = 1;    // Giving one bone chip

            // ACT
            if (!zp.AutoGiveItem(ref invItem))
                zp.GiveItem(invItem, (int)InventorySlot.Cursor);
        }
    }
}
