from .base import TaskBase
from ..stream_filter import StreamFilter

from ..enums import EAnnType
from ..config import cfg
from ..position import POS
from ..database import CropBox, ExtractFeature
from ..database import SaveImage
from ..stream_filter import StreamFilter

import numpy as np
import logging
import os

class CardPlayedTask(TaskBase):
    def __init__(self, db, task_type):
        super().__init__(db, task_type)
        
        self.filter         = StreamFilter(null_val=-1)
        self.feature_buffer = None
        self.feature_crops  = []
        self.crop_cfgs      = (cfg.event_crop_box0, cfg.event_crop_box1, cfg.event_crop_box2)
    
    def OnResize(self, client_width, client_height, ratio_type):
        # ////////////////////////////////
        # //    Feature buffer
        # //    Stacked by cropped region
        # //    
        # //    ---------------------
        # //    |         |         |
        # //    |         |         |
        # //    |    0    |    1    |
        # //    |         |         |
        # //    |         |         |
        # //    |         |---------|
        # //    |         |         |
        # //    |         |    2    |
        # //    |         |         |
        # //    |         |         |
        # //    |---------|---------|
        # //
        # ////////////////////////////////

        pos    = POS[ratio_type]

        left   = round(client_width  * pos[self.task_type][0])
        top    = round(client_height * pos[self.task_type][1])
        width  = round(client_width  * pos[self.task_type][2])
        height = round(client_height * pos[self.task_type][3])

        self.crop_box = CropBox(left, top, left + width, top + height)

        self._ResizeFeatureBuffer(width, height)

    def _PreTick(self, frame_manager):
        self.valid = frame_manager.game_started

    def _Tick(self, frame_manager):
        region_buffer = self._UpdateFeatureBuffer()

        # Extract feature
        feature = ExtractFeature(self.feature_buffer)
        card_id, dist = self.db.SearchByFeature(feature, EAnnType.EVENTS)
        
        if dist > cfg.threshold:
            card_id = -1
        if cfg.DEBUG:
            # if (self.task_type.value == 1) and (card_id != -1):
            if (True) and (card_id != -1):
                logging.debug(f'"info": "{dist=}, {self.task_type.name}: {self.db["events"][card_id]["zh-HANS"] if card_id >= 0 else "None"}"')
        card_id = self.filter.Filter(card_id, dist)

        if card_id >= 0:
            logging.debug(f'"type": "{self.task_type.name}", "card_id": {card_id}, "name": {self.db["events"][card_id]["zh-HANS"]}')
            logging.info(f'"type": "{self.task_type.name}", "card_id": {card_id}')
        
        if cfg.DEBUG_SAVE:
            import cv2
            image = cv2.cvtColor(self.feature_buffer, cv2.COLOR_BGRA2BGR)
            SaveImage(image, os.path.join(cfg.debug_dir, "save", f"{self.task_type.name}{frame_manager.frame_count}.png"))

    def _ResizeFeatureBuffer(self, width, height):
        feature_crop_l0 = round(self.crop_cfgs[0][0] * width)
        feature_crop_t0 = round(self.crop_cfgs[0][1] * height)
        feature_crop_w0 = round(self.crop_cfgs[0][2] * width)
        feature_crop_h0 = round(self.crop_cfgs[0][3] * height)
        feature_crop0 = CropBox(
            feature_crop_l0,
            feature_crop_t0,
            feature_crop_l0 + feature_crop_w0,
            feature_crop_t0 + feature_crop_h0,
        )

        feature_crop_l1 = round(self.crop_cfgs[1][0] * width)
        feature_crop_t1 = round(self.crop_cfgs[1][1] * height)
        feature_crop_w1 = round(self.crop_cfgs[1][2] * width)
        feature_crop_h1 = round(self.crop_cfgs[1][3] * height)
        feature_crop1 = CropBox(
            feature_crop_l1,
            feature_crop_t1,
            feature_crop_l1 + feature_crop_w1,
            feature_crop_t1 + feature_crop_h1,
        )

        feature_crop_l2 = round(self.crop_cfgs[2][0] * width)
        feature_crop_t2 = round(self.crop_cfgs[2][1] * height)
        feature_crop_w2 = feature_crop_w1
        feature_crop_h2 = feature_crop_h0 - feature_crop_h1
        feature_crop2 = CropBox(
            feature_crop_l2,
            feature_crop_t2,
            feature_crop_l2 + feature_crop_w2,
            feature_crop_t2 + feature_crop_h2,
        )
        self.feature_crops = [feature_crop0, feature_crop1, feature_crop2]

        feature_buffer_width  = feature_crop0.width + feature_crop1.width
        feature_buffer_height = feature_crop0.height
        self.feature_buffer = np.zeros(
            (feature_buffer_height, feature_buffer_width, 4), dtype=np.uint8)

    def _UpdateFeatureBuffer(self):
        # Get event card region
        region_buffer = self.frame_buffer[
            self.crop_box.top  : self.crop_box.bottom, 
            self.crop_box.left : self.crop_box.right
        ]

        # Crop event card and get feature buffer
        self.feature_buffer[:self.feature_crops[0].height, :self.feature_crops[0].width] = region_buffer[
            self.feature_crops[0].top  : self.feature_crops[0].bottom, 
            self.feature_crops[0].left : self.feature_crops[0].right
        ]

        self.feature_buffer[:self.feature_crops[1].height, self.feature_crops[0].width:] = region_buffer[
            self.feature_crops[1].top  : self.feature_crops[1].bottom, 
            self.feature_crops[1].left : self.feature_crops[1].right
        ]

        self.feature_buffer[self.feature_crops[1].height:, self.feature_crops[0].width:] = region_buffer[
            self.feature_crops[2].top  : self.feature_crops[2].bottom, 
            self.feature_crops[2].left : self.feature_crops[2].right
        ]

        return region_buffer