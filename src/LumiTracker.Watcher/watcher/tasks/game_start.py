from .base import TaskBase

from ..enums import EAnnType, ECtrlType, ETaskType, ERegionType
from ..config import cfg, LogDebug, LogInfo
from ..regions import REGIONS
from ..database import CropBox
from ..database import ExtractFeature
from ..database import SaveImage
from ..stream_filter import StreamFilter

import numpy as np
import logging
import os

class GameStartTask(TaskBase):
    def __init__(self, db):
        super().__init__(db)

        self.filter    = StreamFilter(null_val=False)
        self.task_type = ETaskType.GAME_START
        self.crop_box  = None  # init when resize
    
    def OnResize(self, client_width, client_height, ratio_type):
        box    = REGIONS[ratio_type][ERegionType.GAME_START]
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])

        self.crop_box = CropBox(left, top, left + width, top + height)

    def _PreTick(self, frame_manager):
        self.valid = True

    def _Tick(self, frame_manager):
        buffer = self.frame_buffer[
            self.crop_box.top  : self.crop_box.bottom, 
            self.crop_box.left : self.crop_box.right
        ]

        feature = ExtractFeature(buffer)
        ctrl_id, dist = self.db.SearchByFeature(feature, EAnnType.CTRLS)
        start = (dist <= cfg.strict_threshold) and (ctrl_id == ECtrlType.GAME_START.value)
        start = self.filter.Filter(start, dist)

        if start:
            frame_manager.game_started = True
            frame_manager.round        = 0

            LogInfo(
                info=f"Game start, last dist in window = {dist}",
                type=self.task_type.name,
                )
            if cfg.DEBUG_SAVE:
                SaveImage(buffer, os.path.join(cfg.debug_dir, "save", f"{self.task_type.name}.png"))