from .base import TaskBase

from ..enums import EGameEvent, ERegionType, ECtrlType
from ..config import cfg, override, LogDebug, LogInfo
from ..regions import REGIONS
from ..feature import CropBox, ExtractFeature_Control_Single, HashToFeature, FeatureDistance, ImageHash
from ..database import SaveImage
from ..stream_filter import StreamFilter

import numpy as np
import os
import cv2

class CardBackTask(TaskBase):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
        idx = ECtrlType.HISTORY.value - ECtrlType.CTRL_SINGLE_FIRST.value
        self.history_feature = HashToFeature(self.db["ctrls"][idx])

        self.Reset()

    @override
    def Reset(self):
        self.filter = StreamFilter(null_val=False)

    @override
    def OnResize(self, client_width, client_height, ratio_type):
        box    = REGIONS[ratio_type][ERegionType.History]
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])
        self.history_box = CropBox(left, top, left + width, top + height)

        box    = REGIONS[ratio_type][ERegionType.CardBack]
        box_left, box_top, box_width, box_height = box
        left   = round(client_width  * box_left)
        width  = round(client_width  * box_width)
        height = round(client_height * box_height)
        # my card back
        top    = round(client_height * box_top)
        self.my_card_back_box = CropBox(left, top, left + width, top + height)
        # op card back is at the mirror position
        box_top = 1.0 - (box_top + box_height)
        top    = round(client_height * box_top)
        self.op_card_back_box = CropBox(left, top, left + width, top + height)

    @override
    def Tick(self):
        buffer = self.frame_buffer[
            self.history_box.top  : self.history_box.bottom, 
            self.history_box.left : self.history_box.right
        ]

        feature = ExtractFeature_Control_Single(buffer)
        dist = FeatureDistance(feature, self.history_feature)
        detected = (dist <= cfg.strict_threshold)
        # LogDebug(turn=f"{detected}", dist=dist)
        detected = self.filter.Filter(detected, dist)

        if detected:
            my_card_back = self.frame_buffer[
                self.my_card_back_box.top  : self.my_card_back_box.bottom, 
                self.my_card_back_box.left : self.my_card_back_box.right
            ]
            op_card_back = self.frame_buffer[
                self.op_card_back_box.top  : self.op_card_back_box.bottom, 
                self.op_card_back_box.left : self.op_card_back_box.right
            ]

            self.fm.my_card_back = cv2.cvtColor(my_card_back, cv2.COLOR_BGRA2GRAY)
            self.fm.op_card_back = cv2.cvtColor(op_card_back, cv2.COLOR_BGRA2GRAY)

            LogDebug(
                info=f"History button detected and card backs have been stored.",
                feature=f"{ImageHash(feature)}", 
                last_dist=dist,
                )

            if cfg.DEBUG_SAVE:
                SaveImage(my_card_back, os.path.join(cfg.debug_dir, "save", f"my_card_back.png"), remove_alpha=True)
                SaveImage(op_card_back, os.path.join(cfg.debug_dir, "save", f"op_card_back.png"), remove_alpha=True)