from .base import GameState, EGameState, GTasks
from ..config import cfg, override, LogDebug
from ..enums import EGamePhase
from ..feature import CardName

import enum
import time

class GameStateNatureAndWisdom(GameState):
    class EStage(enum.Enum):
        Draw   = 0
        Count  = enum.auto()
        Select = enum.auto()


    def __init__(self, frame_manager):
        super().__init__(frame_manager)
        self.stage      = self.EStage.Draw
        self.drawn_card = -1
        self.drawn_time = None
        self.num_select = 0

        self.Reset()

    @override
    def GetState(self):
        return EGameState.NatureAndWisdom

    @override
    def CollectTasks(self):
        # stage transition
        if self.stage == self.EStage.Draw:
            self.HandleDrawStage()
        elif self.stage == self.EStage.Count:
            self.HandleCountStage()
        elif self.stage == self.EStage.Select:
            self.HandleSelectStage()
        else:
            raise NotImplementedError()

        tasks = [
            GTasks.GameStart,
            GTasks.GameOver,
            GTasks.GamePhase,
            ]
        if self.stage == self.EStage.Draw:
            tasks.append(GTasks.NatureAndWisdom_Draw)
        elif self.stage == self.EStage.Count:
            tasks.append(GTasks.NatureAndWisdom_Count)
        elif self.stage == self.EStage.Select:
            tasks.append(GTasks.NatureAndWisdom_Select)
        else:
            raise NotImplementedError()

        return tasks

    @override
    def Next(self):
        if not self.fm.game_started:
            state = EGameState.GameNotStarted
        elif GTasks.GamePhase.phase_signal == EGamePhase.Action:
            state = EGameState.ActionPhase
        else:
            state = self.GetState()
        return state

    @override
    def OnEnter(self, from_state):
        self.Reset()
        LogDebug(info=f"[NatureAndWisdom] Stage: {self.EStage.Draw.name}")
    
    @override
    def OnExit(self, to_state):
        if to_state == EGameState.ActionPhase:
            GTasks.NatureAndWisdom_Select.Flush()

    def Reset(self):
        self.stage       = self.EStage.Draw
        self.drawn_card  = -1
        self.drawn_end_t = None
        self.num_select  = 0
        GTasks.NatureAndWisdom_Draw.Reset()

    def HandleDrawStage(self):
        task = GTasks.NatureAndWisdom_Draw
        if self.drawn_card == -1:
            card_id = task.cards[0]
            if card_id != -1:
                task.Flush(need_reset=False)
                self.drawn_card = card_id
                self.drawn_end_t = time.perf_counter()
            return

        # wait until previous signal has left
        if not task.filters[0].PrevSignalHasLeft():
            self.drawn_end_t = time.perf_counter()
            return
        # wait for the drawn card to leave the center region
        WAIT_TIME = 1.2 # seconds
        if time.perf_counter() - self.drawn_end_t < WAIT_TIME:
            return

        # To next stage
        self.stage = self.EStage.Count
        GTasks.NatureAndWisdom_Count.Reset()
        LogDebug(
            info=f"[NatureAndWisdom] Stage: {self.EStage.Draw.name} ---> {self.EStage.Count.name}",
            drawn=CardName(self.drawn_card, self.fm.db)
            )

    def HandleCountStage(self):
        task = GTasks.NatureAndWisdom_Count
        num_cards = task.signaled_num_cards
        if num_cards <= 0:
            return

        cards, valid = task.GetRecordedCards(num_cards)
        if not valid:
            return
        # ENSURE: drawn_card is in this list
        if self.drawn_card not in cards:
            return

        # To next stage
        self.num_select = num_cards
        self.stage = self.EStage.Select
        GTasks.NatureAndWisdom_Select._Reset(num_cards, cards)
        LogDebug(
            info=f"[NatureAndWisdom] Stage: {self.EStage.Count.name} ---> {self.EStage.Select.name}",
            num_cards=num_cards, 
            initial=[CardName(card, self.fm.db) for card in cards]
            )

    def HandleSelectStage(self):
        pass