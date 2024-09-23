from .base import TaskBase

from ..enums import EAnnType, ECtrlType, EGamePhase, ERegionType
from ..config import cfg, override, LogDebug, LogInfo
from ..regions import REGIONS
from ..feature import CropBox, ExtractFeature_Control_Grayed
from ..database import SaveImage
from ..stream_filter import StreamFilter

import numpy as np
import cv2
import os

class GamePhaseTask(TaskBase):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
        self.crop_box  = None  # init when resize
        self.Reset()

    @override
    def Reset(self):
        self.filter = StreamFilter(null_val=EGamePhase.Null)

    @override
    def OnResize(self, client_width, client_height, ratio_type):
        box    = REGIONS[ratio_type][ERegionType.PHASE]
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])

        self.crop_box = CropBox(left, top, left + width, top + height)

    @override
    def Tick(self):
        res, dist = self.DetectGamePhase()
        res = self.filter.Filter(res, dist)

        self.phase_signal = res

        # LogDebug(
        #     info=f"Game Phase detected: {res}",
        #     )

    def DetectGamePhase(self):
        buffer = self.frame_buffer[
            self.crop_box.top  : self.crop_box.bottom, 
            self.crop_box.left : self.crop_box.right
        ]

        gray = cv2.cvtColor(buffer, cv2.COLOR_BGRA2GRAY)

        _, binary = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)

        # get (y, x) of white pixel
        white_y, white_x = np.where(binary == 255)
        if white_y.size == 0 or white_x.size == 0:
            return EGamePhase.Null, 100
        left    = np.min(white_x)
        right   = np.max(white_x) + 1
        top     = np.min(white_y)
        bottom  = np.max(white_y) + 1
        if (right - left <= cfg.hash_size) or (bottom - top <= cfg.hash_size):
            return EGamePhase.Null, 100
        content = gray[top:bottom, left:right]

        feature = ExtractFeature_Control_Grayed(content)
        ctrl_ids, dists = self.db.SearchByFeature(feature, EAnnType.CTRLS)
        ctrl_id, dist = ctrl_ids[0], dists[0]
        if dist > cfg.strict_threshold:
            res = EGamePhase.Null
        elif (ctrl_id >= ECtrlType.PHASE_ACTION_FIRST.value) and (ctrl_id <= ECtrlType.PHASE_ACTION_LAST.value):
            res = EGamePhase.Action
        else:
            res = EGamePhase.Null

        if cfg.DEBUG_SAVE:
            SaveImage(buffer[top:bottom, left:right], os.path.join(cfg.debug_dir, "save", f"{res.name}.png"))
        return res, dist

