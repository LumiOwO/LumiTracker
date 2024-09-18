import enum

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