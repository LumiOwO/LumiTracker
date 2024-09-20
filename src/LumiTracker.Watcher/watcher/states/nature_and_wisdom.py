from .base import GameState, EGameState, GTasks
from ..config import cfg
from overrides import override

class GameStateNatureAndWisdom(GameState):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)

    @override
    def GetState(self):
        return EGameState.NatureAndWisdom

    @override
    def CollectTasks(self):
        return [
            GTasks.GameStart,
            GTasks.GameOver,
            ]

    @override
    def Next(self):
        if not self.fm.game_started:
            state = EGameState.GameNotStarted
        else:
            state = self.GetState()
        return state

