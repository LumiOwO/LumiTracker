import enum
from ..regions import GetRatioType
from ..tasks import *

class EGameState(enum.Enum):
    GameNotStarted  = 0
    StartingHand    = enum.auto()
    ActionPhase     = enum.auto()
    NatureAndWisdom = enum.auto()

class GameState():
    def __init__(self, frame_manager):
        self.fm = frame_manager
    
    def GetState(self):
        raise NotImplementedError()
    
    def CollectTasks(self):
        raise NotImplementedError()
    
    def Next(self):
        raise NotImplementedError()
    
    def OnEnter(self, from_state):
        pass

    def OnExit(self, to_state):
        pass

class GTasks():
    @classmethod
    def Init(cls, frame_manager):
        cls.GameStart    = GameStartTask(frame_manager)
        cls.GameOver     = GameOverTask(frame_manager)
        cls.Round        = RoundTask(frame_manager)
        cls.StartingHand = CardSelectTask(frame_manager, 5)
        cls.MyPlayed     = CardPlayedTask(frame_manager, is_op=False)
        cls.OpPlayed     = CardPlayedTask(frame_manager, is_op=True)
        cls.CardFlow     = CardFlowTask(frame_manager)

    @classmethod
    def OnResize(cls, client_width, client_height):
        # Update ratio
        ratio_type = GetRatioType(client_width, client_height)
        
        for var_name, value in cls.__dict__.items():
            if (not var_name.startswith('__')) and (not callable(value)) and isinstance(value, TaskBase):
                task = value
                task.OnResize(client_width, client_height, ratio_type)