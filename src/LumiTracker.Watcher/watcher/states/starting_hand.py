from .base import GameState, EGameState, GTasks

class GameStateStartingHand(GameState):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
    
    def GetState(self):
        return EGameState.StartingHand
    
    def CollectTasks(self):
        return [
            GTasks.GameStart, 
            GTasks.GameOver, 
            GTasks.StartingHand,
            GTasks.Round,
            ]
    
    def Next(self):
        if not self.fm.game_started:
            state = EGameState.GameNotStarted
        elif self.fm.round > 0:
            state = EGameState.ActionPhase
        else:
            state = self.GetState()
        return state

    def OnExit(self, to_state):
        if to_state == EGameState.ActionPhase:
            GTasks.StartingHand.Flush()
