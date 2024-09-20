from .card_flow import CenterCropTask

from ..enums import ETaskType
from ..config import cfg, override, LogDebug, LogInfo, LogError
from ..feature import ActionCardHandler, CardName, CardCost
from ..stream_filter import StreamFilter

from collections import Counter

class CardSelectTask(CenterCropTask):
    def __init__(self, frame_manager, n_cards=1, prev_cards=None):
        super().__init__(frame_manager)
        self.n_cards     = n_cards
        self.prev_counts = None
        self.cards       = []
        self.filters     = []
        
        prev_cards = [] if prev_cards is None else prev_cards
        self._Reset(n_cards, prev_cards)

    def _Reset(self, n_cards, prev_cards):
        self.n_cards     = n_cards
        self.prev_counts = Counter(prev_cards)
        self.cards   = [-1 for _ in range(self.n_cards)]
        self.filters = [StreamFilter(null_val=-1) for _ in range(self.n_cards)]

    @override
    def Reset(self):
        self._Reset(self.n_cards, [])

    @override
    def Tick(self):
        bboxes, costs = self.DetectCenterCards()
        num_bboxes = len(bboxes)

        if num_bboxes != self.n_cards:
            return

        for i, bbox in enumerate(bboxes):
            card_handler = ActionCardHandler()
            card_handler.OnResize(bbox)
            card_id, dist, dists = card_handler.Update(self.frame_buffer, self.db)
            if card_id >= 0 and costs[i] != CardCost(card_id, self.db):
                card_id = -1
            card_id = self.filters[i].Filter(card_id, dist=dist)

            # record last detected card_id
            if card_id >= 0:
                self.cards[i] = card_id
    
    def Flush(self):
        cur_counts = Counter(self.cards)
        if -1 in cur_counts:
            del cur_counts[-1]
            LogError(
                info="[CardSelect] Some cards are not detected!", 
                detected=self.cards)

        diff   = cur_counts - self.prev_counts
        drawn  = []
        create = []
        for card_id, count in diff.items():
            if count > 0:
                drawn  += [card_id] * count
            elif count < 0:
                create += [card_id] * (-count)

        if drawn:
            LogInfo(
                type=ETaskType.MY_DRAWN.name,
                cards=drawn,
                names=[CardName(card, self.db) for card in drawn])
        if create:
            LogInfo(
                type=ETaskType.MY_CREATE_DECK.name,
                cards=create,
                names=[CardName(card, self.db) for card in create])

        self.Reset()
