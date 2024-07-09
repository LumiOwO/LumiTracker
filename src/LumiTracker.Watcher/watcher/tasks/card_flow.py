from .base import TaskBase

from ..enums import EAnnType, ECtrlType, ERegionType
from ..config import cfg
from ..regions import REGIONS
from ..database import CropBox
from ..database import ExtractFeature
from ..database import SaveImage
from ..stream_filter import StreamFilter

import numpy as np
import logging
import os

class CardFlowTask(TaskBase):
    def __init__(self, db):
        super().__init__(db)

        self.filter  = StreamFilter(null_val=False)
    
    def OnResize(self, client_width, client_height, ratio_type):
        # box    = REGIONS[ratio_type][self.task_type]
        # left   = round(client_width  * box[0])
        # top    = round(client_height * box[1])
        # width  = round(client_width  * box[2])
        # height = round(client_height * box[3])

        # self.crop_box = CropBox(left, top, left + width, top + height)
        pass

    def _PreTick(self, frame_manager):
        self.valid = frame_manager.game_started

    def _Tick(self, frame_manager):
        pass