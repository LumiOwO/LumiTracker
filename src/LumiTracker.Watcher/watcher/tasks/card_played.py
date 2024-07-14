from .base import TaskBase
from ..stream_filter import StreamFilter

from ..enums import EAnnType, ETaskType, ERegionType
from ..config import cfg
from ..regions import REGIONS
from ..database import ActionCardHandler
from ..database import SaveImage
from ..stream_filter import StreamFilter

import numpy as np
import logging
import os

class CardPlayedTask(TaskBase):
    def __init__(self, db, is_op):
        super().__init__(db)
        
        self.filter         = StreamFilter(null_val=-1)
        self.task_type      = ETaskType.OP_PLAYED if is_op else ETaskType.MY_PLAYED
        self.card_handler   = ActionCardHandler()
    
    def OnResize(self, client_width, client_height, ratio_type):
        region_type = ERegionType.OP_PLAYED if self.task_type == ETaskType.OP_PLAYED else ERegionType.MY_PLAYED
        box = REGIONS[ratio_type][region_type]
        self.card_handler.OnResize(client_width, client_height, box)

    def _PreTick(self, frame_manager):
        self.valid = frame_manager.game_started

    def _Tick(self, frame_manager):
        card_id, dist = self.card_handler.Update(self.frame_buffer, self.db)
        if cfg.DEBUG:
            # if (self.task_type.value == 1) and True: #(card_id != -1):
            if (True) and (card_id != -1):
                logging.debug(f'"info": "{dist=}, {self.task_type.name}: {self.db["actions"][card_id]["zh-HANS"] if card_id >= 0 else "None"}"')
        card_id = self.filter.Filter(card_id, dist)

        if card_id >= 0:
            logging.debug(f'"type": "{self.task_type.name}", "card_id": {card_id}, "name": {self.db["actions"][card_id]["zh-HANS"]}')
            logging.info(f'"type": "{self.task_type.name}", "card_id": {card_id}')
        
        if cfg.DEBUG_SAVE:
            import cv2
            image = cv2.cvtColor(self.feature_buffer, cv2.COLOR_BGRA2BGR)
            SaveImage(image, os.path.join(cfg.debug_dir, "save", f"{self.task_type.name}{frame_manager.frame_count}.png"))