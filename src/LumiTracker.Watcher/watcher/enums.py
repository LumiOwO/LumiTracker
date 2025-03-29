import enum
from ._enums_gen import EActionCard, ECharacterCard

def _IsExtra(card_id):
    return card_id >= EActionCard.NumActions.value

def _IsExtraGolden(card_id):
    start = EActionCard.NumActions.value
    end   = start + EActionCard.NumExtraGoldens.value
    return (card_id >= start) and (card_id < end)

def _IsExtraMyArcaneLegend(card_id):
    start = EActionCard.NumActions.value + EActionCard.NumExtraGoldens.value
    end   = start + EActionCard.NumArcaneLegends.value
    return (card_id >= start) and (card_id < end)

def _IsExtraOpArcaneLegend(card_id):
    num_arcanes = EActionCard.NumArcaneLegends.value
    start = EActionCard.NumActions.value + EActionCard.NumExtraGoldens.value + num_arcanes
    end   = start + num_arcanes
    return (card_id >= start) and (card_id < end)

EActionCard.IsExtra               = staticmethod(_IsExtra)
EActionCard.IsExtraGolden         = staticmethod(_IsExtraGolden)
EActionCard.IsExtraMyArcaneLegend = staticmethod(_IsExtraMyArcaneLegend)
EActionCard.IsExtraOpArcaneLegend = staticmethod(_IsExtraOpArcaneLegend)

class ELanguage(enum.Enum):
    FollowSystem = 0
    zh_HANS      = enum.auto()
    en_US        = enum.auto()
    ja_JP        = enum.auto()

    NumELanguages = enum.auto()

class EClientType(enum.Enum):
    YuanShen       = 0
    Global         = enum.auto()
    CloudPC        = enum.auto()
    CloudWeb       = enum.auto()
    NumClientTypes = enum.auto()

    # Extra types, not displayed in the app
    Video          = enum.auto()
    WeMeet         = enum.auto()

class ECaptureType(enum.Enum):
    BitBlt          = 0
    WindowsCapture  = enum.auto()

    NumCaptureTypes = enum.auto()

class EGamePhase(enum.Enum):
    Null   = 0
    Action = enum.auto()

class ETurn(enum.Enum):
    Null = 0
    My   = enum.auto()
    Op   = enum.auto()

class EInputType(enum.Enum):
    CaptureTest       = 0

    NumInputTypes     = enum.auto()
    Invalid           = NumInputTypes

class EGameEvent(enum.Enum):
    # Events for duel
    GameStart         = 0
    MyPlayed          = enum.auto()
    OpPlayed          = enum.auto()
    GameOver          = enum.auto()
    Round             = enum.auto()
    MyDrawn           = enum.auto()
    OpDrawn           = enum.auto() # placeholder, not used yet
    MyDiscard         = enum.auto()
    OpDiscard         = enum.auto()
    MyCreateDeck      = enum.auto()
    OpCreateDeck      = enum.auto()
    MyCreateHand      = enum.auto() # eg. Furina
    OpCreateHand      = enum.auto()

    GameEventFirst    = GameStart
    GameEventLast     = OpCreateHand

    # events for client
    UnsupportedRatio  = enum.auto()
    CaptureTest       = enum.auto()
    LogFps            = enum.auto()
    MyCharacters      = enum.auto()
    OpCharacters      = enum.auto()

    # events for server
    InitialDeck       = enum.auto()

    NumGameEvents     = enum.auto()
    Invalid           = NumGameEvents

class ERegionType(enum.Enum):
    # default: (left, top, width, height)
    GameStart       = 0
    MyPlayed        = enum.auto()
    OpPlayed        = enum.auto()
    GameOver        = enum.auto()
    Phase           = enum.auto()
    Round           = enum.auto()
    Center          = enum.auto()
    FlowAnchor      = enum.auto()  # (margin to digit center, margin to card top, card width, card height)
    Deck            = enum.auto()
    Settings        = enum.auto()
    CardBack        = enum.auto()
    Turn            = enum.auto()
    History         = enum.auto()
    CharVS          = enum.auto()
    CharInGame      = enum.auto()
    CharCorner      = enum.auto()
    CharOffset      = enum.auto()  # (VS margin, in-game margin, active character height offset, equipment margin)

    NumRegionTypes  = enum.auto()

class ERatioType(enum.Enum):
    E16_9           = 0            # 1920 x 1080, 2560 x 1440
    E16_10          = enum.auto()  # 1920 x 1200, 1680 x 1050
    E64_27          = enum.auto()  # 2560 × 1080, 2048 x 864
    E43_18          = enum.auto()  # 3440 × 1440, 2150 x 900
    E12_5           = enum.auto()  # 3840 x 1600, 1920 x 800

    NumRatioTypes   = enum.auto()

class ECtrlType(enum.Enum):
    # game start
    GAME_START = 0

    # game over
    GAME_OVER_WIN_ZH     = enum.auto()
    GAME_OVER_WIN_EN     = enum.auto()
    GAME_OVER_WIN_JA     = enum.auto()
    GAME_OVER_LOSE_ZH    = enum.auto()
    GAME_OVER_LOSE_EN    = enum.auto()
    GAME_OVER_LOSE_JA    = enum.auto()

    GAME_OVER_WIN_FIRST  = GAME_OVER_WIN_ZH
    GAME_OVER_WIN_LAST   = GAME_OVER_WIN_JA
    GAME_OVER_LOSE_FIRST = GAME_OVER_LOSE_ZH
    GAME_OVER_LOSE_LAST  = GAME_OVER_LOSE_JA
    GAME_OVER_FIRST      = GAME_OVER_WIN_FIRST
    GAME_OVER_LAST       = GAME_OVER_LOSE_LAST

    # round
    ROUND_ZH    = enum.auto()
    ROUND_EN    = enum.auto()
    ROUND_JA    = enum.auto()

    ROUND_FIRST = ROUND_ZH
    ROUND_LAST  = ROUND_JA

    # action phase
    PHASE_ACTION_ZH    = enum.auto()
    PHASE_ACTION_EN    = enum.auto()
    PHASE_ACTION_JA    = enum.auto()

    PHASE_ACTION_FIRST = PHASE_ACTION_ZH
    PHASE_ACTION_LAST  = PHASE_ACTION_JA

    NUM_CTRLS_ANN = enum.auto()

    # Single ctrls that are not included in the ann file
    SETTINGS = enum.auto()
    HISTORY  = enum.auto()
    MY_TURN  = enum.auto()
    OP_TURN  = enum.auto()

    CTRL_SINGLE_FIRST = SETTINGS
    CTRL_SINGLE_LAST  = OP_TURN
    NUM_CTRLS_SINGLE  = CTRL_SINGLE_LAST - CTRL_SINGLE_FIRST + 1

    @staticmethod
    def IsGameStart(ctrl_id):
        return ctrl_id == ECtrlType.GAME_START.value

    @staticmethod
    def IsGameWin(ctrl_id):
        return (ctrl_id >= ECtrlType.GAME_OVER_WIN_FIRST.value) and (ctrl_id <= ECtrlType.GAME_OVER_WIN_LAST.value)
    
    @staticmethod
    def IsGameLose(ctrl_id):
        return (ctrl_id >= ECtrlType.GAME_OVER_LOSE_FIRST.value) and (ctrl_id <= ECtrlType.GAME_OVER_LOSE_LAST.value)
    
    @staticmethod
    def IsGameOver(ctrl_id):
        return (ctrl_id >= ECtrlType.GAME_OVER_FIRST.value) and (ctrl_id <= ECtrlType.GAME_OVER_LAST.value)
    
    @staticmethod
    def IsRound(ctrl_id):
        return (ctrl_id >= ECtrlType.ROUND_FIRST.value) and (ctrl_id <= ECtrlType.ROUND_LAST.value)

    @staticmethod
    def IsPhaseAction(ctrl_id):
        return (ctrl_id >= ECtrlType.PHASE_ACTION_FIRST.value) and (ctrl_id <= ECtrlType.PHASE_ACTION_LAST.value)


class EAnnType(enum.Enum):
    ACTIONS_A    = 0
    ACTIONS_D    = enum.auto()
    CTRLS        = enum.auto()
    DIGITS       = enum.auto()
    CHARACTERS_A = enum.auto()
    CHARACTERS_D = enum.auto()

    ANN_COUNT    = enum.auto()

class EActionCardType(enum.Enum):
    Talent       = 0
    Token        = enum.auto()
    Catalyst     = enum.auto()
    Bow          = enum.auto()
    Claymore     = enum.auto()
    Polearm      = enum.auto()
    Sword        = enum.auto()
    Artifact     = enum.auto()
    Technique    = enum.auto()
    Location     = enum.auto()
    Companion    = enum.auto()
    Item         = enum.auto()
    ArcaneLegend = enum.auto()
    Resonance    = enum.auto()
    Event        = enum.auto()
    Food         = enum.auto()

class EElementType(enum.Enum):
    Cryo    = 0
    Hydro   = enum.auto()
    Pyro    = enum.auto()
    Electro = enum.auto()
    Anemo   = enum.auto()
    Geo     = enum.auto()
    Dendro  = enum.auto()

class ECostType(enum.Enum):
    Cryo          = EElementType.Cryo.value
    Hydro         = EElementType.Hydro.value
    Pyro          = EElementType.Pyro.value
    Electro       = EElementType.Electro.value
    Anemo         = EElementType.Anemo.value
    Geo           = EElementType.Geo.value
    Dendro        = EElementType.Dendro.value
    Same          = enum.auto()
    Any           = enum.auto()
    CryoAttack    = enum.auto()
    HydroAttack   = enum.auto()
    PyroAttack    = enum.auto()
    ElectroAttack = enum.auto()
    AnemoAttack   = enum.auto()
    GeoAttack     = enum.auto()
    DendroAttack  = enum.auto()