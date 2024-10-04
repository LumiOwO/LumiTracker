from .base import TaskBase

from ..enums import EAnnType, ECtrlType, ETaskType, ERegionType
from ..config import cfg, override, LogDebug, LogInfo
from ..regions import REGIONS
from ..feature import CropBox, ExtractFeature_Control
from ..database import SaveImage
from ..stream_filter import StreamFilter

import numpy as np
import logging
import os
import cv2
import enum

class EGameResult(enum.Enum):
    Null = 0
    Win  = enum.auto()
    Lose = enum.auto()

class GameOverTask(TaskBase):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
        self.task_type = ETaskType.GAME_OVER
        self.crop_box  = None  # init when resize
        self.Reset()

    @override
    def Reset(self):
        self.filter = StreamFilter(null_val=EGameResult.Null)
    
    @override
    def OnResize(self, client_width, client_height, ratio_type):
        box    = REGIONS[ratio_type][ERegionType.GAME_OVER]
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])

        self.crop_box = CropBox(left, top, left + width, top + height)

    @override
    def Tick(self):
        res, dist = self.DetectGameResult()
        res = self.filter.Filter(res, dist)

        if res != EGameResult.Null:
            self.fm.game_started = False

            LogInfo(
                info=f"Game Over, last dist in window = {dist}",
                type=f"{self.task_type.name}",
                is_win=(res == EGameResult.Win),
                )

    def DetectGameResult(self):
        buffer = self.frame_buffer[
            self.crop_box.top  : self.crop_box.bottom, 
            self.crop_box.left : self.crop_box.right
        ]
        # self.buffer = buffer

        main_content, valid = self.CropMainContent(buffer)
        if not valid:
            return EGameResult.Null, 100

        feature = ExtractFeature_Control(main_content)
        ctrl_ids, dists = self.db.SearchByFeature(feature, EAnnType.CTRLS)
        ctrl_id, dist = ctrl_ids[0], dists[0]
        if dist > cfg.strict_threshold:
            res = EGameResult.Null
        elif ECtrlType.IsGameWin(ctrl_id):
            res = EGameResult.Win
        elif ECtrlType.IsGameLose(ctrl_id):
            res = EGameResult.Lose
        else:
            res = EGameResult.Null

        # LogDebug(res=f"{res}", dists=dists[:3])

        if cfg.DEBUG_SAVE:
            SaveImage(main_content, os.path.join(cfg.debug_dir, "save", f"{self.task_type.name}.png"))
        return res, dist

    @staticmethod
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