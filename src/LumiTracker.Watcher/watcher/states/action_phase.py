from .base import GameState, EGameState, GTasks
from ..config import cfg
from overrides import override

class GameStateActionPhase(GameState):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)

    @override
    def GetState(self):
        return EGameState.ActionPhase

    @override
    def CollectTasks(self):
        return [
            GTasks.GameStart, 
            GTasks.GameOver, 
            GTasks.MyPlayed, 
            GTasks.OpPlayed, 
            GTasks.Round, 
            GTasks.CardFlow
            ]

    @override
    def Next(self):
        # TODO: add NatureAndWisdom
        if not self.fm.game_started:
            state = EGameState.GameNotStarted
        else:
            state = self.GetState()
        return state