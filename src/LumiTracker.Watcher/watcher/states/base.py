import enum
from abc import ABC, abstractmethod

from ..regions import GetRatioType
from ..tasks import *
from ..config import LogDebug

class EGameState(enum.Enum):
    GameNotStarted  = 0
    StartingHand    = enum.auto()
    ActionPhase     = enum.auto()
    NatureAndWisdom = enum.auto()

class GameState(ABC):
    def __init__(self, frame_manager):
        self.fm = frame_manager
    
    @abstractmethod
    def GetState(self):
        raise NotImplementedError()
    
    @abstractmethod
    def CollectTasks(self):
        raise NotImplementedError()
    
    @abstractmethod
    def Next(self):
        raise NotImplementedError()
    
    def OnEnter(self, from_state):
        pass

    def OnExit(self, to_state):
        pass

class GTasks():
    @classmethod
    def Init(cls, frame_manager):
        cls.GameStart     = GameStartTask(frame_manager)
        cls.GameOver      = GameOverTask(frame_manager)
        cls.Round         = RoundTask(frame_manager)
        cls.StartingHand  = CardSelectTask(frame_manager, 5, is_starting_hand=True)
        cls.MyPlayed      = CardPlayedTask(frame_manager, is_op=False)
        cls.OpPlayed      = CardPlayedTask(frame_manager, is_op=True)
        cls.CardFlow      = CardFlowTask(frame_manager)
        cls.GamePhase     = GamePhaseTask(frame_manager)
        cls.CardBack      = CardBackTask(frame_manager)
        cls.AllCharacters = AllCharactersTask(frame_manager)

        cls.NatureAndWisdom_Draw   = CardSelectTask(frame_manager, 1)
        cls.NatureAndWisdom_Count  = CardFlowTask(frame_manager, need_deck=False, need_dump=False)
        cls.NatureAndWisdom_Select = CardSelectTask(frame_manager)

    @classmethod
    def ForEach(cls, func):
        for var_name, value in cls.__dict__.items():
            if (not var_name.startswith('__')) and (not callable(value)) and isinstance(value, TaskBase):
                # LogDebug(info=f"{var_name=}")
                task = value
                func(task)

    @classmethod
    def OnResize(cls, client_width, client_height, ratio_type):
        # LogDebug(info=f"****** OnResize All Tasks ******")
        GTasks.ForEach(lambda task: task.OnResize(client_width, client_height, ratio_type))
    
    @classmethod
    def Reset(cls):
        # LogDebug(info=f"****** Reset All Tasks ******")
        GTasks.ForEach(lambda task: task.Reset() if task != cls.GameStart else None)
