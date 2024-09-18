from .card_flow import CenterCropTask

from ..enums import ETaskType
from ..config import cfg, LogDebug, LogInfo, LogError
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
    
    def Flush(self):
        res = []
        for card in self.cards:
            if card != -1:
                res.append(card)
        
        if len(res) < 5:
            LogError(
                info="Not all starting hand cards are detected!", 
                detected=self.cards)
        if len(res) > 0:
            LogInfo(
                type=ETaskType.MY_DRAWN.name, 
                cards=res,
                names=[CardName(card, self.db) for card in res])

        self.Reset()
