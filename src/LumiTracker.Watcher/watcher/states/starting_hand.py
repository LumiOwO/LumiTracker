from .base import GameState, EGameState, GTasks
from ..config import cfg
from overrides import override

class GameStateStartingHand(GameState):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)

    @override
    def GetState(self):
        return EGameState.StartingHand

    @override
    def CollectTasks(self):
        return [
            GTasks.GameStart, 
            GTasks.GameOver, 
            GTasks.StartingHand,
            GTasks.Round,
            ]

    @override
    def Next(self):
        if not self.fm.game_started:
            state = EGameState.GameNotStarted
        elif self.fm.round > 0:
            state = EGameState.ActionPhase
        else:
            state = self.GetState()
        return state

    @override
    def OnEnter(self, from_state):
        GTasks.Reset()

    @override
    def OnExit(self, to_state):
        if to_state == EGameState.ActionPhase:
            GTasks.StartingHand.Flush()
