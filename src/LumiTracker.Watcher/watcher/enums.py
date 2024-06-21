import enum

class ETaskType(enum.Enum):
    GAME_START        = 0
    MY_EVENT          = enum.auto()
    OP_EVENT          = enum.auto()

    # types for exception
    UNSUPPORTED_RATIO = enum.auto()

class ERatioType(enum.Enum):
    E16_9  = 0
    E16_10 = enum.auto()
    E64_27 = enum.auto()
    E43_18 = enum.auto()
    E12_5  = enum.auto()