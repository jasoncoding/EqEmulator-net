using System;

namespace EQEmulator.Servers
{
    public enum ProtocolOpCode : ushort
    {
        None                = 0x0000,
        SessionRequest      = 0x0001,
        SessionResponse     = 0x0002,
        Combined            = 0x0003,
        SessionDisconnect   = 0x0005,
        KeepAlive           = 0x0006,
        SessionStatRequest  = 0x0007,
        SessionStatResponse = 0x0008,
        Packet              = 0x0009,
        Fragment            = 0x000d,
        OutOfOrderAck       = 0x0011,
        Ack                 = 0x0015,
        AppCombined         = 0x0019,
        OutOfSession        = 0x001d
    }

    public enum AppOpCode : ushort
    {
        None                    = 0x0000,
        // Login OpCodes
        SessionReady            = 0x0001,
        Login                   = 0x0002,
        ServerListRequest       = 0x0004,
        PlayEverquestRequest    = 0x000d,
        EnterChat               = 0x000f,
        PollResponse            = 0x0011,
        ChatMessage             = 0x0016,
        LoginAccepted           = 0x0017,
        ServerListResponse      = 0x0018,
        PlayEverquestResponse   = 0x0021,
        Poll                    = 0x0029,
        
        // World OpCodes
        ApproveWorld            = 0x3c25,
        LogServer               = 0x0fa6,
        MOTD                    = 0x024d,
        SendLoginInfo           = 0x4dd0,
        DeleteCharacter         = 0x26c9,
        SendCharInfo            = 0x4513,
        ExpansionInfo           = 0x04ec,
        CharacterCreate         = 0x10b2,
        RandomNameGenerator     = 0x23d4,
        GuildsList              = 0x6957,   // same as zone guild list afaik
        ApproveName             = 0x3ea6,
        EnterWorld              = 0x7cba,
        PostEnterWorld          = 0x52A4,
        WorldClientCRC1         = 0x5072,
        WorldClientCRC2         = 0x5b18,
        SetChatServer           = 0x00d7,
        SetChatServer2          = 0x6536,
        ZoneServerInfo          = 0x61b6,
        WorldComplete           = 0x509d,
        ZoneUnavail             = 0x407C,
        WorldClientReady        = 0x5e99,
        CharacterStillInZone    = 0x60fa,   // world->client. reject.
        WorldChecksumFailure    = 0x7D37,	// world->client. reject.
        WorldLoginFailed        = 0x8DA7,	// world->client. reject.
        WorldLogout             = 0x7718,	// client->world
        WorldLevelTooHigh       = 0x583b,	// world->client. Cancels zone in.
        CharInacessable         = 0x436A,   // world->client. Cancels zone in.

        CrashDump               = 0x7825,
        WearChange              = 0x7441,
        AppAck                  = 0x7752,   // used by world & zone both

        // Zone in opcodes
        ZoneEntry       = 0x7213,
        NewZone         = 0x0920,
        ReqClientSpawn  = 0x0322,
        ZoneSpawns      = 0x2e78,
        CharInventory   = 0x5394,
        SetServerFilter = 0x6563,
        LockoutTimerInfo= 0x7f63,
        SendZonepoints  = 0x3eba,
        SpawnDoor       = 0x4c24,
        ReqNewZone      = 0x7ac5,
        PlayerProfile   = 0x75df,
        TimeOfDay       = 0x1580,

        Logout          = 0x61ff,
        LogoutReply     = 0x3cdc,		// 0x48c2		0x0F66	is not quite right.. this causes disconnect error...
        PreLogoutReply  = 0x711e,   // 0 len packet sent during logout/zoning
        LevelUpdate     = 0x6d44,			
        Stamina         = 0x7a83,
        
        // Guild OpCodes
        ZoneGuildList       = 0x6957,     // (one entry too long)
        GetGuildMOTD        = 0x7fec,
        GuildMemberList     = 0x147d,
        GuildMemberUpdate   = 0x0f4d,
        GuildRemove         = 0x0179,
        GuildPeace          = 0x215a,
        GuildWar            = 0x0c81,
        GuildLeader         = 0x12b1,
        GuildDemote         = 0x4eb9,
        GuildMOTD           = 0x475a,
        SetGuildMOTD        = 0x591c,
        GetGuildsList       = None,
        GuildInvite         = 0x18b7,
        GuildPublicNote     = 0x17a2,
        GuildDelete         = 0x6cce,
        GuildInviteAccept   = 0x61d0,
        GuildManageBanker   = 0x3d1e,
        GuildBank           = None,

        // GM & guide opcodes
        GMServers       = 0x3387,        // /servers
        GMBecomeNPC     = 0x7864,		// /becomenpc
        GMZoneRequest   = 0x1306,	// /zone
        GMSearchCorpse  = 0x3c32,	// /searchcorpse
        GMHideMe        = 0x15b2,		    // /hideme
        GMGoto          = 0x1cee,		    // /goto
        GMDelCorpse     = 0x0b2f,		// /delcorpse
        GMApproval      = 0x0c0f,		// /approval
        GMToggle        = 0x7fea,		    // /toggletell
        GMZoneRequest2  = None,
        GMSummon        = 0x1edc,		    // /summon
        GMEmoteZone     = 0x39f2,		// /emotezone
        GMEmoteWorld    = 0x3383,		// /emoteworld (not implemented)
        GMFind          = 0x5930,		    // /find		
        GMKick          = 0x692c,		    // /kick
        GMNameChange    = None,

        SafePoint       = None,
        BindWound       = 0x601d,
        GMTraining      = 0x238f,
        GMEndTraining   = 0x613d,
        GMTrainSkill    = 0x11d2,
        Animation       = 0x2acf,
        Stun            = 0x1E51,
        MoneyUpdate     = 0x267c,
        SendExpZonein   = 0x0587,
        IncreaseStats   = None,
        ReadBook        = 0x1496,
        Dye             = 0x00dd,
        Consume         = 0x77d6,
        Begging         = 0x13e7,
        InspectRequest  = 0x775d,
        InspectAnswer   = 0x2403,
        Action2         = None,
        BeginCast       = 0x3990,
        BuffFadeMsg     = 0x0b2d,
        Consent         = 0x1081,
        ConsentDeny     = 0x4e8c,
        ConsentResponse = 0x6380,
        LFGCommand      = 0x68ac,
        LFGGetMatchesRequest    = 0x022f,
        LFGAppearance   = None,
        LFGResponse     = None,		
        LFGGetMatchesResponse   = 0x45d0,
        LootItem        = 0x7081,
        Bug             = 0x7ac2,
        BoardBoat       = 0x4298,
        Save            = 0x736b,
        Camp            = 0x78c1,
        EndLootRequest  = 0x2316,
        MemorizeSpell   = 0x308e,
        SwapSpell       = 0x2126,
        CastSpell       = 0x304b,
        DeleteSpell     = 0x4f37,
        LoadSpellSet    = 0x403e,
        AutoAttack      = 0x5e55,
        AutoFire        = 0x6c53,
        Consider        = 0x65ca,
        Emote           = 0x547a,
        PetCommands     = 0x10a1,
        SpawnAppearance = 0x7c32,
        DeleteSpawn     = 0x55bc,
        FormattedMessage= 0x5a48,
        WhoAllRequest   = 0x5cdd,
        WhoAllResponse  = 0x757b,
        AutoAttack2     = 0x0701,
        SetRunMode      = 0x4aba,
        SimpleMessage   = 0x673c,
        SaveOnZoneReq   = 0x1540,
        SenseHeading    = 0x05ac,
        Buff            = 0x6a53,
        LootComplete    = 0x0a94,
        EnvDamage       = 0x31b3,
        Split           = 0x4848,
        Surname         = 0x4668,
        MoveItem        = 0x420f,
        DeleteCharge    = 0x1c4a,
        FaceChange      = 0x0f8e,
        ItemPacket      = 0x3397,
        ItemLinkResponse= 0x667c,
        ClientReady     = 0x5e20,
        ZoneChange      = 0x5dd8,
        ItemLinkClick   = 0x53e5,
        Forage          = 0x4796,
        BazaarSearch    = 0x1ee9,
        NewSpawn        = 0x1860,			// a similar unknown packet to NewSpawn: 0x12b2
        Action          = 0x497c,
        SpecialMesg     = 0x2372,
        Bazaar          = None,
        LeaveBoat       = 0x67c9,
        Weather         = 0x254d,
        LFPGetMatchesRequest= 0x35a6,
        Illusion        = 0x448d,
        TargetReject    = None,
        TargetCommand   = 0x1477,
        TargetMouse     = 0x6c47,
        TargetHoTT      = 0x6a12,
        GMKill          = 0x6980,
        MoneyOnCorpse   = 0x7fe4,
        ClickDoor       = 0x043b,
        MoveDoor        = 0x700d,
        LootRequest     = 0x6f90,
        YellForHelp     = 0x61ef,
        ManaChange      = 0x4839,
        LFPCommand      = 0x6f82,
        RandomReply     = 0x6cd5,
        DenyResponse    = 0x7c66,
        ConsiderCorpse  = 0x773f,
        ConfirmDelete   = 0x3838,
        MobHealth       = 0x0695,
        HPUpdate        = 0x3bcf,
        SkillUpdate     = 0x6a93,
        RandomReq       = 0x5534,
        ClientUpdate    = 0x14cb,
        Report          = 0x7f9d,		
        GroundSpawn     = 0x0f47,
        LFPGetMatchesResponse= 0x45d0,
        Jump            = 0x0797,
        ExpUpdate       = 0x5ecd,
        Death           = 0x6160,
        BecomeCorpse    = 0x4DBC,
        GMLastName      = 0x23a1,
        InitialMobHealth= 0x3d2d,
        Mend            = 0x14ef,
        MendHPUpdate    = None,
        Feedback        = 0x5306,
        TGB             = 0x0c11,
        InterruptCast   = 0x0b97,
        Damage          = 0x5c78,
        ChannelMessage  = 0x1004,
        LevelAppearance = None,
        MultiLineMsg    = None,
        Charm           = None,
        ApproveZone     = None,
        Assist          = 0x7709,
        AugmentItem     = 0x539b,
        BazaarInspect   = None,
        ClientError     = None,
        DeleteItem      = 0x4d81,
        ControlBoat= None,
        DumpName= None,
        FeignDeath= 0x7489,
        Heartbeat= None,
        ItemName= None,
        LDoNButton= None,
        MoveCoin= 0x7657,
        ReloadUI= None,
        ZonePlayerToBind= 0x385e,

        // pc & npc trading
        TradeRequest= 0x372f,
        TradeAcceptClick= 0x0065,
        TradeRequestAck= 0x4048,
        TradeCoins= 0x34c1,		// guess...
        FinishTrade= 0x6014,
        CancelTrade= 0x2dc1,
        TradeMoneyUpdate= None,		// not sure

        // merchant crap
        ShopPlayerSell= 0x0e13,
        ShopEnd= 0x7e03,
        ShopEndConfirm= 0x20b2,
        ShopPlayerBuy= 0x221e,		
        ShopRequest= 0x45f9,
        ShopDelItem= None,  // None maybe, 16 bytes though

        // tradeskill stuff:
        // something 0x21ed (8)
        // something post combine 0x5f4e (8)
        ClickObject= 0x3bc2,
        ClickObjectAction= 0x6937,
        ClearObject= 0x21ed,  // was 0x711e, 0x711e of len 0 comes right after ClickObjectAck from server
        RecipeDetails= 0x4ea2,
        RecipesFavorite= 0x23f0,
        RecipesSearch= 0x164d,
        RecipeReply= 0x31f8,
        RecipeAutoCombine= 0x0353,
        TradeSkillCombine= 0x0b40,

        RequestDuel     = 0x28e1,
        DuelResponse    = 0x1b09,
        DuelResponse2   = None,    // when accepted

        RezzComplete    = None,		    // packet wrong on this
        RezzRequest     = None,		    // packet wrong on this
        RezzAnswer      = None,		    // packet wrong on this
        SafeFallSuccess = None,
        Shielding       = None,
        TestBuff        = 0x6ab0,		// /testbuff
        Track           = 0x5d11,		//  ShowEQ 10/27/05
        TrackTarget     = None,
        TrackUnknown    = 0x6177,		// size 0 right after Track
        
        // Group OpCodes
        GroupDisband            = 0x0e76,
        GroupInvite             = 0x1b48,
        GroupFollow             = 0x7bc7,
        GroupUpdate             = 0x2dd6,
        GroupAcknowledge        = None,
        GroupCancelInvite       = 0x1f27,
        GroupDelete             = None,
        GroupFollow2            = None,     // used in conjunction with GroupInvite2
        GroupInvite2            = 0x12d6,   // sometimes sent instead of GroupInvite
        CancelInvite            = None,

        RaidJoin= 0x1f21,
        RaidInvite= 0x5891,
        RaidUpdate= 0x1f21,

        ZoneComplete= None,
        ItemLinkText= None,
        DisciplineUpdate= 0x7180,
        LocInfo= None,
        FindPersonRequest= 0x3c41,
        FindPersonReply= 0x5711,
        ForceFindPerson= None,
        LoginComplete= None,
        Sound= None,
        MobRename= 0x0498,
        BankerChange= 0x6a5b,

        // Button-push commands
        Taunt= 0x5e48,
        CombatAbility= 0x5ee8,
        SenseTraps= 0x5666,
        PickPocket= 0x2ad8,
        DisarmTraps= 0x1214,
        Disarm= 0x17d9,
        Hide= 0x4312,		
        Sneak= 0x74e1	,		
        Fishing= 0x0b36,
        InstillDoubt= 0x389e,		// intimidation
        LDoNOpen= 0x083b,

        RequestClientZoneChange= 0x7834,

        PickLockSuccess     = 0x40E7,
        WeaponEquip1        = 0x6c5e,
        WeaponEquip2        = 0x63da,
        WeaponUnequip2      = 0x381d,

        // Tribute Packets
        OpenGuildTributeMaster  = None,
        OpenTributeMaster       = 0x512e,   // open tribute master window
        OpenTributeReply        = 0x27B3,	// reply to open request
        SelectTribute           = 0x625d,   // clicking on a tribute, and text reply
        TributeItem             = 0x6f6c,	// donating an item
        TributeMoney            = 0x27b3,	// donating money
        TributeNPC              = 0x7f25,	// seems to be missing now
        TributeToggle           = 0x2688,	// activating/deactivating tribute
        TributeTimer            = 0x4665,   // a 4 byte tier update, 10 minutes for seconds
        TributePointUpdate      = 0x6463,   // 16 byte point packet
        TributeUpdate           = 0x5639,
        GuildTributeInfo        = 0x5e3d,
        TributeInfo             = 0x152d,
        SendGuildTributes       = 0x5e3a, 	// request packet, 4 bytes
        SendTributes            = 0x067a,   // request packet, 4 bytes, migth be backwards

        // Task packets
        CompletedTasks          = 0x76a2,
        TaskDescription         = 0x5ef7,
        TaskActivity            = 0x682d,
        TaskMemberList          = None,	    // not sure
        OpenNewTasksWindow      = None,	    // combined with AvaliableTask I think
        AvaliableTask           = None,
        AcceptNewTask           = None,
        CancelTask              = None,
        DeclineAllTasks         = None,	    // not sure, 12 bytes
        // task complete related: 0x0000 (24 bytes), 0x0000 (8 bytes), 0x0000 (4 bytes)

        // AA stuff
        SendAATable         = 0x367d,
        UpdateAA            = 0x5966,
        RespondAA           = 0x3af4,
        SendAAStats         = 0x5996,
        AAAction            = 0x0681,
        AAExpUpdate         = 0x5f58,

        PurchaseLeadershipAA    = None,
        UpdateLeadershipAA      = None,
        LeadershipExpUpdate     = None,
        LeadershipExpToggle     = 0x5b37,

        // Misc OpCodes
        CustomTitles        = 0x2a28,
        FloatListThing      = 0x6a1b
    }
}