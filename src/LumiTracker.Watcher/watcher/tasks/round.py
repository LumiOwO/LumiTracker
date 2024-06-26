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
    
    def OnResize(self, client_width, client_height, ratio_type):
        pos    = POS[ratio_type]

        left   = round(client_width  * pos[self.task_type][0])
        top    = round(client_height * pos[self.task_type][1])
        width  = round(client_width  * pos[self.task_type][2])
        height = round(client_height * pos[self.task_type][3])

        self.crop_box = CropBox(left, top, left + width, top + height)

    def _PreTick(self, frame_manager):
        self.valid = frame_manager.game_started

    def _Tick(self, frame_manager):
        buffer = self.frame_buffer[
            self.crop_box.top  : self.crop_box.bottom, 
            self.crop_box.left : self.crop_box.right
        ]

        main_content, valid = RoundTask.CropMainContent(buffer)
        if not valid:
            return

        feature = ExtractFeature(main_content)
        ctrl_id, dist = self.db.SearchByFeature(feature, EAnnType.CTRLS)
        found = (dist <= cfg.strict_threshold) and (
            ctrl_id >= ECtrlType.ROUND_FIRST.value) and (ctrl_id <= ECtrlType.ROUND_LAST.value)
        found = self.filter.Filter(found, dist)

        if found:
            frame_manager.round += 1

            logging.debug(f'"info": "Found round text, last dist in window = {dist}"')
            logging.info(f'"type": "{self.task_type.name}", "round": "{frame_manager.round}"')
            if cfg.DEBUG_SAVE:
                SaveImage(main_content, os.path.join(cfg.debug_dir, "save", f"{self.task_type.name}.png"))

    def CropMainContent(buffer):
        # Convert to grayscale
        gray = cv2.cvtColor(buffer, cv2.COLOR_BGRA2GRAY)

        # Apply thresholding to create a binary image
        _, binary = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)

        # Identify columns that contain any number of 255s
        foreground_cols = np.any(binary == 255, axis=0).astype(np.uint8)

        # Find boundaries of consecutive ranges
        pivot = np.array([0], dtype=np.uint8)
        foreground_cols = np.concatenate((pivot, foreground_cols, pivot))
        diff = np.diff(foreground_cols)
        start_indices = np.where(diff == 1)[0]
        end_indices   = np.where(diff == 255)[0]
        if start_indices.size == 0:
            return None, False
        
        # Merge text ranges
        threshold = 0.2
        thres_w = max(round(threshold * buffer.shape[1]), cfg.hash_size)
        x_min, x_max = 20000, -1
        for i in range(start_indices.size):
            start = start_indices[i] # no need to +1 since we insert a 0 at the beginning
            end   = end_indices[i]
            if end - start <= thres_w:
                continue

            x_min = min(x_min, start)
            x_max = max(x_max, end)

        if x_max > -1:
            return buffer[:, x_min:x_max], True
        else:
            return None, False
