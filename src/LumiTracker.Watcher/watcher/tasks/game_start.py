from .base import TaskBase

from ..enums import EAnnType, ECtrlType, EGameEvent, ERegionType
from ..config import cfg, override, LogDebug, LogInfo
from ..regions import REGIONS
from ..feature import CropBox, ExtractFeature_Control
from ..database import SaveImage
from ..stream_filter import StreamFilter

import numpy as np
import logging
import os

class GameStartTask(TaskBase):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
        self.event_type = EGameEvent.GAME_START
        self.crop_box   = None  # init when resize
        self.Reset()

        # update every frame
        self.detected = False

    @override
    def Reset(self):
        self.filter = StreamFilter(null_val=False)

    @override
    def OnResize(self, client_width, client_height, ratio_type):
        box    = REGIONS[ratio_type][ERegionType.GAME_START]
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])

        self.crop_box = CropBox(left, top, left + width, top + height)

    @override
    def Tick(self):
        buffer = self.frame_buffer[
            self.crop_box.top  : self.crop_box.bottom, 
            self.crop_box.left : self.crop_box.right
        ]

        feature = ExtractFeature_Control(buffer)
        ctrl_ids, dists = self.db.SearchByFeature(feature, EAnnType.CTRLS)
        start = (dists[0] <= cfg.strict_threshold) and (ctrl_ids[0] == ECtrlType.GAME_START.value)
        start = self.filter.Filter(start, dists[0])

        self.detected = start

        if start:
            self.fm.game_started = True
            self.fm.round        = 0

            LogInfo(
                info=f"Game Started, last dist in window = {dists[0]}",
                type=self.event_type.name,
                )
            if cfg.DEBUG_SAVE:
                SaveImage(buffer, os.path.join(cfg.debug_dir, "save", f"{self.event_type.name}.png"))