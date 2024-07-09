from .base import TaskBase

from ..enums import EAnnType, ECtrlType, ETaskType, ERegionType
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

class GameOverTask(TaskBase):
    def __init__(self, db):
        super().__init__(db)

        self.filter    = StreamFilter(null_val=False)
        self.task_type = ETaskType.GAME_OVER
        self.crop_box  = None  # init when resize
    
    def OnResize(self, client_width, client_height, ratio_type):
        box    = REGIONS[ratio_type][ERegionType.GAME_OVER]
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])

        self.crop_box = CropBox(left, top, left + width, top + height)

    def _PreTick(self, frame_manager):
        self.valid = frame_manager.game_started

    def _Tick(self, frame_manager):
        buffer = self.frame_buffer[
            self.crop_box.top  : self.crop_box.bottom, 
            self.crop_box.left : self.crop_box.right
        ]

        main_content, valid = GameOverTask.CropMainContent(buffer)
        if not valid:
            return

        feature = ExtractFeature(main_content)
        ctrl_id, dist = self.db.SearchByFeature(feature, EAnnType.CTRLS)
        over = (dist <= cfg.strict_threshold) and (
            ctrl_id >= ECtrlType.GAME_OVER_FIRST.value) and (ctrl_id <= ECtrlType.GAME_OVER_LAST.value)
        over = self.filter.Filter(over, dist)

        if over:
            frame_manager.game_started = False

            logging.debug(f'"info": "Game over, last dist in window = {dist}"')
            logging.info(f'"type": "{self.task_type.name}"')
            if cfg.DEBUG_SAVE:
                SaveImage(buffer, os.path.join(cfg.debug_dir, "save", f"{self.task_type.name}.png"))

    def CropMainContent(buffer):
        # Convert to grayscale
        gray = cv2.cvtColor(buffer, cv2.COLOR_BGRA2GRAY)

        # Apply thresholding to create a binary image
        _, binary = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)

        # Identify rows that contain any number of 255s
        foreground_rows = np.any(binary == 255, axis=1).astype(np.uint8)

        # Find boundaries of consecutive ranges
        pivot = np.array([0], dtype=np.uint8)
        foreground_rows = np.concatenate((pivot, foreground_rows, pivot))
        diff = np.diff(foreground_rows)
        start_indices = np.where(diff == 1)[0]
        end_indices   = np.where(diff == 255)[0]
        if start_indices.size == 0:
            return None, False
        
        # Get the maximum length range
        lengths = end_indices - start_indices
        max_index = np.argmax(lengths)
        start = start_indices[max_index] # no need to +1 since we insert a 0 at the beginning
        end   = end_indices[max_index]
        if end - start <= cfg.hash_size:
            return None, False

        return buffer[start:end, :], True