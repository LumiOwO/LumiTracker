from .base import TaskBase

from ..enums import EAnnType, ECtrlType
from ..config import cfg
from ..position import POS
from ..database import CropBox
from ..database import ExtractFeature
from ..database import SaveImage
from ..stream_filter import StreamFilter

import numpy as np
import logging
import os
import cv2

class RoundTask(TaskBase):
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

    def _PreTick(self, frame_manager):
        self.valid = frame_manager.game_started

    def _Tick(self, frame_manager):
        self.buffer[:, :] = self.frame_buffer[
            self.crop_box.top  : self.crop_box.bottom, 
            self.crop_box.left : self.crop_box.right
        ]

        main_content, valid = RoundTask.CropMainContent(self.buffer)
        if not valid:
            return

        feature = ExtractFeature(main_content)
        ctrl_id, dist = self.db.SearchByFeature(feature, EAnnType.CTRLS)
        found = (dist <= cfg.strict_threshold) and (
            ctrl_id >= ECtrlType.ROUND_FIRST.value) and (ctrl_id <= ECtrlType.ROUND_LAST.value)
        found = self.filter.Filter(found, dist)

        if found:
            logging.debug(f'"info": "Found round text, {dist=}"')
            logging.info(f'"type": "{self.task_type.name}"')
            if cfg.DEBUG_SAVE:
                SaveImage(main_content, os.path.join(cfg.debug_dir, "save", f"{self.task_type.name}.png"))

    def CropMainContent(buffer, threshold=0.2):
        # Convert to grayscale
        gray = cv2.cvtColor(buffer, cv2.COLOR_BGRA2GRAY)

        # Apply thresholding to create a binary image
        _, binary = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)

        # Find contours
        contours, _ = cv2.findContours(binary, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        valid   = False
        thres_w = round(threshold * buffer.shape[1])
        thres_h = round(threshold * buffer.shape[0])
        # Find the bounding box
        x_min, y_min = 20000, 20000
        x_max, y_max = -1, -1
        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)
            if w <= thres_w or h <= thres_h:
                continue

            valid = True
            x_min = min(x_min, x)
            y_min = min(y_min, y)
            x_max = max(x_max, x + w)
            y_max = max(y_max, y + h)
        
        # ignore error box that is too small
        if valid:
            valid = (x_max - x_min >= cfg.hash_size) and (y_max - y_min >= cfg.hash_size)

        if valid:
            return buffer[y_min:y_max, x_min:x_max], True
        else:
            return None, False
