import enum

class ETaskType(enum.Enum):
    GAME_START        = 0
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
    MY_CREATE_HAND    = enum.auto() # Furina
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
