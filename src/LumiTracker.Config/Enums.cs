namespace LumiTracker.Config
{
    public enum ELanguage : int
    {
        zh_HANS,
        en_US,
    }

    public enum EClosingBehavior : int
    {
        Quit,
        Minimize,
    }

    public enum EClientType : int
    {
        YuanShen,
        Global,
        CloudPC,
        CloudWeb,
        NumClientTypes,

        // Extra types, not displayed in the app
        Video,
        WeMeet,
    }

    public enum ECaptureType : int
    {
        BitBlt,
        WindowsCapture,

        NumCaptureTypes
    }

    public enum EActionCardType : int
    {
        Talent,
        Token,
        Catalyst,
        Bow,
        Claymore,
        Polearm,
        Sword,
        Artifact,
        Technique,
        Location,
        Companion,
        Item,
        ArcaneLegend,
        Resonance,
        Event,
        Food,
    }

    public enum EElementType : int
    {
        Cryo,
        Hydro,
        Pyro,
        Electro,
        Anemo,
        Geo,
        Dendro,
    }

    public enum ECostType : int
    {
        Cryo    = EElementType.Cryo,
        Hydro   = EElementType.Hydro,
        Pyro    = EElementType.Pyro,
        Electro = EElementType.Electro,
        Anemo   = EElementType.Anemo,
        Geo     = EElementType.Geo,
        Dendro  = EElementType.Dendro,
        Same,
        Any,
        CryoAttack,
        HydroAttack,
        PyroAttack,
        ElectroAttack,
        AnemoAttack,
        GeoAttack,
        DendroAttack,
    }

    public enum ETaskType : int
    {
        NONE = 0,

        GAME_START,
        MY_PLAYED,
        OP_PLAYED,
        GAME_OVER,
        ROUND,
        MY_DRAWN,
        OP_DRAWN,       // placeholder, not used yet
        MY_DISCARD,
        OP_DISCARD,
        MY_CREATE_DECK,
        OP_CREATE_DECK,
        MY_CREATE_HAND, // eg. Furina
        OP_CREATE_HAND,

        UNSUPPORTED_RATIO,
        CAPTURE_TEST,
        LOG_FPS,
    }

    public static class EnumHelpers
    {
        public static string[] GetClientProcessList(EClientType clientType)
        {
            return clientType switch
            {
                EClientType.YuanShen => ["YuanShen.exe"],
                EClientType.Global   => ["GenshinImpact.exe"],
                EClientType.CloudPC  => ["Genshin Impact Cloud Game.exe"],
                EClientType.CloudWeb => ["chrome.exe", "firefox.exe", "msedge.exe"],
                EClientType.Video    => ["PotPlayerMini64.exe"],
                EClientType.WeMeet   => ["wemeetapp.exe"],
                _ => ["YuanShen.exe"] // the default case
            };
        }

        public static bool IsCloudGame(EClientType clientType)
        {
            return (clientType == EClientType.CloudPC) || (clientType == EClientType.CloudWeb);
        }
    }
}
