from .base import GameState, EGameState, GTasks

class GameStateGameNotStarted(GameState):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
    
    def GetState(self):
        return EGameState.GameNotStarted
    
    def CollectTasks(self):
        return [GTasks.GameStart]
    
    def Next(self):
        if self.fm.game_started:
            state = EGameState.StartingHand
        else:
            state = self.GetState()
        return state