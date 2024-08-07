import enum

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

    # types for exception
    UNSUPPORTED_RATIO = enum.auto()

class ERegionType(enum.Enum):
    GAME_START = 0
    MY_PLAYED  = enum.auto()
    OP_PLAYED  = enum.auto()
    GAME_OVER  = enum.auto()
    ROUND      = enum.auto()
    CENTER     = enum.auto()
    FLOW_ANCHOR= enum.auto()
    MY_DECK    = enum.auto()
    OP_DECK    = enum.auto()

class ERatioType(enum.Enum):
    E16_9  = 0
    E16_10 = enum.auto()
    E64_27 = enum.auto()
    E43_18 = enum.auto()
    E12_5  = enum.auto()

class ECtrlType(enum.Enum):
    GAME_START              = 0

    GAME_OVER_WIN_ZH_HANS   = enum.auto()
    GAME_OVER_LOSE_ZH_HANS  = enum.auto()

    GAME_OVER_FIRST = GAME_OVER_WIN_ZH_HANS
    GAME_OVER_LAST  = GAME_OVER_LOSE_ZH_HANS

    ROUND_ZH_HANS   = enum.auto()

    ROUND_FIRST     = ROUND_ZH_HANS
    ROUND_LAST      = ROUND_ZH_HANS

    CTRL_FEATURES_COUNT = enum.auto()

class EAnnType(enum.Enum):
    ACTIONS   = 0
    CTRLS     = enum.auto()

    ANN_COUNT = enum.auto()

class EActionCardType(enum.Enum):
    Talent       = 0
    Token        = enum.auto()
    Catalyst     = enum.auto()
    Bow          = enum.auto()
    Claymore     = enum.auto()
    Polearm      = enum.auto()
    Sword        = enum.auto()
    Artifact     = enum.auto()
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