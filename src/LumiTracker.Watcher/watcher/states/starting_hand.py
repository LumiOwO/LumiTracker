from .base import GameState, EGameState, GTasks
from ..config import cfg, override
import numpy as np

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
            GTasks.CardBack,
            ]

    @override
    def Next(self):
        if not self.fm.game_started:
            state = EGameState.GameNotStarted
        elif (self.fm.round > 0) or (self.fm.my_card_back.size > 0 and self.fm.op_card_back.size > 0):
            state = EGameState.ActionPhase
        else:
            state = self.GetState()
        return state

    @override
    def OnEnter(self, from_state):
        GTasks.Reset()
        self.fm.round        = 0
        self.fm.my_card_back = np.zeros((0,))
        self.fm.op_card_back = np.zeros((0,))

    @override
    def OnExit(self, to_state):
        if to_state == EGameState.ActionPhase:
            GTasks.StartingHand.Flush()
