from .base import GameState, EGameState, GTasks

class GameStateActionPhase(GameState):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
    
    def GetState(self):
        return EGameState.ActionPhase
    
    def CollectTasks(self):
        return [
            GTasks.GameStart, 
            GTasks.GameOver, 
            GTasks.MyPlayed, 
            GTasks.OpPlayed, 
            GTasks.Round, 
            GTasks.CardFlow
            ]

    def Next(self):
        # TODO: add NatureAndWisdom
        if not self.fm.game_started:
            state = EGameState.GameNotStarted
        else:
            state = self.GetState()
        return state