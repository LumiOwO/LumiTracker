from base import GameState
from ..enums import EGameState

class GameStateActionPhase(GameState):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
    
    def GetState(self):
        return EGameState.ActionPhase
    
    def CollectTasks(self):
        return [
            self.fm.game_start_task, 
            self.fm.game_over_task, 
            self.fm.my_played_task, 
            self.fm.op_played_task, 
            self.fm.round_task, 
            self.fm.card_flow_task
            ]

    def Next(self):
        # TODO: add NatureAndWisdom
        if not self.fm.game_started:
            state = EGameState.GameNotStarted
        else:
            state = self.GetState()
        return state