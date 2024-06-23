from .base import TaskBase

from ..enums import EAnnType, ECtrlType
from ..config import cfg
from ..position import POS
from ..database import CropBox
from ..database import ExtractFeature, FeatureDistance, HashToFeature
from ..database import SaveImage
from ..stream_filter import StreamFilter

import numpy as np
import logging
import os

class GameStartTask(TaskBase):
    def __init__(self, db, task_type):
        super().__init__(db, task_type)

        self.filter  = StreamFilter(null_val=False)
        self.buffer  = None
    
    def OnResize(self, client_width, client_height, ratio_type):
        pos    = POS[ratio_type]

        left   = round(client_width  * pos[self.task_type][0])
        top    = round(client_height * pos[self.task_type][1])
        width  = round(client_width  * pos[self.task_type][2])
        height = round(client_height * pos[self.task_type][3])

        self.crop_box = CropBox(left, top, left + width, top + height)
        self.buffer   = np.zeros((height, width, 4), dtype=np.uint8)

    def Tick(self, frame_manager):
        if frame_manager.game_started:
            return

        self.buffer[:, :] = self.frame_buffer[
            self.crop_box.top  : self.crop_box.bottom, 
            self.crop_box.left : self.crop_box.right
        ]

        feature = ExtractFeature(self.buffer)
        ctrl_id, dist = self.db.SearchByFeature(feature, EAnnType.CTRLS)
        start = (dist <= cfg.strict_threshold) and (ctrl_id == ECtrlType.GAME_START.value)
        start = self.filter.Filter(start, dist)

        if start:
            frame_manager.game_started = True
            logging.debug(f'"info": "Game start, {dist=}"')
            logging.info(f'"type": "{self.task_type.name}"')
            if cfg.DEBUG_SAVE:
                SaveImage(self.buffer, os.path.join(cfg.debug_dir, "save", f"{self.task_type.name}.png"))