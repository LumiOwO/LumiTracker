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
import cv2

class CardFlowTask(TaskBase):
    def __init__(self, db):
        super().__init__(db)

        self.filter   = StreamFilter(null_val=False)
        self.crop_box = None
    
    def OnResize(self, client_width, client_height, ratio_type):
        box    = REGIONS[ratio_type][ERegionType.CENTER]
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])

        self.crop_box = CropBox(left, top, left + width, top + height)

    def _PreTick(self, frame_manager):
        self.valid = frame_manager.game_started

    def _Tick(self, frame_manager):
        self.buffer = self.frame_buffer[
            self.crop_box.top  : self.crop_box.bottom, 
            self.crop_box.left : self.crop_box.right
        ]

        feature = ExtractFeature(self.buffer)
        ctrl_id, dist = self.db.SearchByFeature(feature, EAnnType.CTRLS)
        start = (dist <= cfg.strict_threshold) and (ctrl_id == ECtrlType.GAME_START.value)
        start = self.filter.Filter(start, dist)

        # Convert to grayscale
        gray = cv2.cvtColor(self.buffer, cv2.COLOR_BGR2GRAY)

        # Thresholding
        _, thresh = cv2.threshold(gray, 180, 255, cv2.THRESH_BINARY)
        self.thresh = thresh

        # Find contours
        contours, _ = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        # Filter and draw bounding boxes around the detected cards
        self.bounding_boxes = []
        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)
            if h < self.crop_box.height * 0.65 or h > self.crop_box.height * 0.7:
                continue
            if abs(w / h - 420 / 720) > 0.1:
                continue

            self.bounding_boxes.append((x, y, w, h))
            