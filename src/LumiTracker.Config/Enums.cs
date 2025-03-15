using System.Globalization;

namespace LumiTracker.Config
{
    public enum ELanguage : int
    {
        FollowSystem,
        zh_HANS,
        en_US,
        ja_JP,

        NumELanguages,
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

    public enum EInputType : int
    {
        NONE = 0,

        CAPTURE_TEST,
    }

    public enum EGameEvent : int
    {
        NONE = 0,

        // Events for duel
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

        GAME_EVENT_FIRST = GAME_START,
        GAME_EVENT_LAST  = OP_CREATE_HAND,

        // Events for client
        UNSUPPORTED_RATIO,
        CAPTURE_TEST,
        LOG_FPS,
        MY_CHARACTERS,
        OP_CHARACTERS,

        // Events for server
        INITIAL_DECK,
    }

    // default: (left, top, width, height)
    public enum ERegionType : int
    {
        GAME_START = 0,
        MY_PLAYED,
        OP_PLAYED,
        GAME_OVER,
        PHASE,
        ROUND,
        CENTER,
        FLOW_ANCHOR,    // (margin to digit center, margin to card top, card width, card height)
        DECK,
        VS_ANCHOR,      // (left, top, width, height, margin)
        SETTINGS,
        CARD_BACK,
        TURN,
        HISTORY,
    }

    public enum ERatioType : int
    {
        E16_9 = 0,  // 1920 x 1080, 2560 x 1440
        E16_10,     // 1920 x 1200, 1680 x 1050
        E64_27,     // 2560 × 1080, 2048 x 864
        E43_18,     // 3440 × 1440, 2150 x 900
        E12_5,      // 3840 x 1600, 1920 x 800
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
                // Extra types, not displayed in the app
                EClientType.Video    => ["PotPlayerMini64.exe", "Honeyview.exe"],
                EClientType.WeMeet   => ["wemeetapp.exe"],
                _ => ["YuanShen.exe"] // the default case
            };
        }

        public static bool BitBltUnavailable(EClientType clientType)
        {
            return (clientType == EClientType.CloudPC) || (clientType == EClientType.CloudWeb) || (clientType == EClientType.Video);
        }

        public static bool ShouldShowUnsupportedRatioWarning(EClientType clientType)
        {
            return (clientType == EClientType.YuanShen) || (clientType == EClientType.Global) || (clientType == EClientType.CloudPC);
        }

        public static string ToLanguageName(this ELanguage lang)
        {
            return lang.ToString().Replace('_', '-');
        }

        public static ELanguage ToELanguage(this string lang)
        {
            ELanguage curLang;
            if (Enum.TryParse(lang.Replace('-', '_'), out curLang) && curLang < ELanguage.NumELanguages)
            {
                return curLang;
            }
            else
            {
                return ELanguage.FollowSystem;
            }
        }

        public static string GetLanguageUtf8Name(ELanguage lang)
        {
            return lang switch
            {
                ELanguage.zh_HANS => "简体中文",
                ELanguage.en_US   => "English",
                ELanguage.ja_JP   => "日本語",
                _ => Lang.FollowSystem,
            };
        }

        public static string LanguageNameShortToFull(string name)
        {
            return name switch
            {
                "zh" => "zh-HANS",
                "en" => "en-US",
                "ja" => "ja-JP",
                _ => "en-US",
            };
        }

        // This function always return an valid language name
        public static string ParseLanguageName(string? lang)
        {
            if (lang != null && lang.Length == 2)
            {
                lang = LanguageNameShortToFull(lang);
            }
            ELanguage curLang = lang?.ToELanguage() ?? ELanguage.FollowSystem;
            if (curLang != ELanguage.FollowSystem)
            {
                return curLang.ToLanguageName();
            }
            // Follow system
            lang = LanguageNameShortToFull(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            curLang = lang.ToELanguage();
            if (curLang != ELanguage.FollowSystem)
            {
                return curLang.ToLanguageName();
            }
            // Default to english
            return "en-US";
        }
    }
}
