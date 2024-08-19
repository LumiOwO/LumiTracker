from .card_flow import CenterCropTask

from ..enums import EGameState, ETaskType
from ..config import cfg, LogDebug, LogInfo
from ..feature import ActionCardHandler, CardName
from ..stream_filter import StreamFilter

class StartingHandTask(CenterCropTask):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
        self.cards   = []
        self.filters = []

        self.Reset()
    
    def Reset(self):
        # round 0, five cards to detect
        self.cards   = [-1 for _ in range(5)]
        self.filters = [StreamFilter(null_val=-1) for _ in range(5)]

    def OnStateTransfer(self, old_state, new_state):
        # TODO: change to RollPhase
        if new_state == EGameState.ActionPhase:
            LogInfo(
                type=ETaskType.MY_DRAWN.name, 
                cards=self.cards,
                names=[CardName(card, self.db) for card in self.cards])

        self.Reset()

    def Tick(self):
        bboxes = self.DetectCenterBoundingBoxes()
        num_bboxes = len(bboxes)

        if num_bboxes != 5:
            return

        for i, box in enumerate(bboxes):
            box.left   = box.left + self.center_crop.left
            box.top    = self.flow_anchor.top
            box.right  = box.left + self.flow_anchor.width
            box.bottom = box.top  + self.flow_anchor.height

            card_handler = ActionCardHandler()
            card_handler.OnResize(box)
            card_id, dist, dists = card_handler.Update(self.frame_buffer, self.db)
            card_id = self.filters[i].Filter(card_id, dist=dist)

            # record last detected card_id
            if card_id >= 0:
                self.cards[i] = card_id
