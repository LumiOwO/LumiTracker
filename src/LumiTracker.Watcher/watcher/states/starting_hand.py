from base import GameState
from ..enums import EGameState

class GameStateStartingHand(GameState):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
    
    def GetState(self):
        return EGameState.StartingHand
    
    def CollectTasks(self):
        return [
            self.fm.game_start_task, 
            self.fm.game_over_task, 
            self.fm.starting_hand_task,
            self.fm.round_task,
            ]
    
    def Next(self):
        if not self.fm.game_started:
            state = EGameState.GameNotStarted
        elif self.fm.round > 0:
            state = EGameState.ActionPhase
        else:
            state = self.GetState()
        return state
