from .base import GameState, EGameState, GTasks

class GameStateNatureAndWisdom(GameState):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
    
    def GetState(self):
        return EGameState.NatureAndWisdom
    
    def CollectTasks(self):
        return [
            GTasks.GameStart,
            GTasks.GameOver,
            ]
    
    def Next(self):
        if not self.fm.game_started:
            state = EGameState.GameNotStarted
        else:
            state = self.GetState()
        return state

