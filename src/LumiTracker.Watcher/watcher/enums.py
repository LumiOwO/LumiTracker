import enum

class ETaskType(enum.Enum):
    GAME_START        = 0
    MY_EVENT          = enum.auto()
    OP_EVENT          = enum.auto()
    GAME_OVER         = enum.auto()

    # types for exception
    UNSUPPORTED_RATIO = enum.auto()

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

    CTRL_FEATURES_COUNT = enum.auto()

class EAnnType(enum.Enum):
    EVENTS    = 0
    CTRLS     = enum.auto()

    ANN_COUNT = enum.auto()
