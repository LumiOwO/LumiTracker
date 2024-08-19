from .base import TaskBase

from ..enums import EAnnType, ECtrlType, ETaskType, ERegionType
from ..config import cfg, LogDebug, LogInfo
from ..regions import REGIONS
from ..feature import CropBox, ExtractFeature_Control
from ..database import SaveImage
from ..stream_filter import StreamFilter

import numpy as np
import logging
import os

class GameStartTask(TaskBase):
    def __init__(self, db):
        super().__init__(db)
        self.task_type = ETaskType.GAME_START
        self.crop_box  = None  # init when resize
        self.Reset()
    
    def Reset(self):
        self.filter    = StreamFilter(null_val=False)

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

        feature = ExtractFeature_Control(buffer)
        ctrl_ids, dists = self.db.SearchByFeature(feature, EAnnType.CTRLS)
        start = (dists[0] <= cfg.strict_threshold) and (ctrl_ids[0] == ECtrlType.GAME_START.value)
        start = self.filter.Filter(start, dists[0])

        frame_manager.reset_tasks = start
        if start:
            frame_manager.game_started = True
            frame_manager.round        = 0

            LogInfo(
                info=f"Game start, last dist in window = {dists[0]}",
                type=self.task_type.name,
                )
            if cfg.DEBUG_SAVE:
                SaveImage(buffer, os.path.join(cfg.debug_dir, "save", f"{self.task_type.name}.png"))