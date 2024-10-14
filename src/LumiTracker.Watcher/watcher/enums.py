import enum
from ._enums_gen import EActionCard, ECharacterCard

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

class EInputType(enum.Enum):
    NONE              = 0
    CAPTURE_TEST      = enum.auto()

class ETaskType(enum.Enum):
    NONE              = 0

    GAME_START        = enum.auto()
    MY_PLAYED         = enum.auto()
    OP_PLAYED         = enum.auto()
    GAME_OVER         = enum.auto()
    ROUND             = enum.auto()
    MY_DRAWN          = enum.auto()
    OP_DRAWN          = enum.auto() # placeholder, not used yet
    MY_DISCARD        = enum.auto()
    OP_DISCARD        = enum.auto()
    MY_CREATE_DECK    = enum.auto()
    OP_CREATE_DECK    = enum.auto()
    MY_CREATE_HAND    = enum.auto() # eg. Furina
    OP_CREATE_HAND    = enum.auto()

    GAME_EVENT_FIRST  = GAME_START
    GAME_EVENT_LAST   = OP_CREATE_HAND

    UNSUPPORTED_RATIO = enum.auto()
    CAPTURE_TEST      = enum.auto()
    LOG_FPS           = enum.auto()

class ERegionType(enum.Enum):
    GAME_START = 0
    MY_PLAYED  = enum.auto()
    OP_PLAYED  = enum.auto()
    GAME_OVER  = enum.auto()
    PHASE      = enum.auto()
    ROUND      = enum.auto()
    CENTER     = enum.auto()
    FLOW_ANCHOR= enum.auto()
    MY_DECK    = enum.auto()

class ERatioType(enum.Enum):
    E16_9  = 0
    E16_10 = enum.auto()
    E64_27 = enum.auto()
    E43_18 = enum.auto()
    E12_5  = enum.auto()

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

    CTRL_FEATURES_COUNT = enum.auto()

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