from .base import GameState, EGameState, GTasks
from ..config import cfg, override

class GameStateGameNotStarted(GameState):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)

    @override
    def GetState(self):
        return EGameState.GameNotStarted

    @override
    def CollectTasks(self):
        return [GTasks.GameStart]

    @override
    def Next(self):
        if self.fm.game_started:
            state = EGameState.StartingHand
        else:
            state = self.GetState()
        return state