from .base import GameState, EGameState, GTasks
from ..config import cfg, override
from ..enums import EActionCard, ERegionType

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
            GTasks.CardFlow,
            GTasks.AllCharacters,
            ]

    @override
    def Next(self):
        if not self.fm.game_started:
            state = EGameState.GameNotStarted
        elif GTasks.MyPlayed.card_id_signal == EActionCard.NatureAndWisdom.value:
            state = EGameState.NatureAndWisdom
        else:
            state = self.GetState()
        return state

    @override
    def OnEnter(self, from_state):
        GTasks.AllCharacters.SetRegionType(ERegionType.CharInGame)