from .base import TaskBase

from ..enums import EAnnType, ECtrlType, ERegionType
from ..config import cfg
from ..regions import REGIONS
from ..database import CropBox
from ..database import ExtractFeature
from ..database import ActionCardHandler
from ..database import SaveImage
from ..stream_filter import StreamFilter

import numpy as np
import logging
import os
import cv2

class CardFlowTask(TaskBase):
    def __init__(self, db):
        super().__init__(db)

        # self.filter        = StreamFilter(null_val=False)
        self.client_width  = 0
        self.client_height = 0
        self.center_crop   = None
        self.center_buffer = None

        # self.MAX_CARDS     = 6
        # self.card_handlers = []
        # for _ in range(self.MAX_CARDS):
        #     self.card_handlers.append(ActionCardHandler())
    
    def OnResize(self, client_width, client_height, ratio_type):
        box    = REGIONS[ratio_type][ERegionType.CENTER]
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])

        self.client_width  = client_width
        self.client_height = client_height
        self.center_crop   = CropBox(left, top, left + width, top + height)

    def _PreTick(self, frame_manager):
        self.valid = frame_manager.game_started

    def _Tick(self, frame_manager):
        # ============ center ==============
        self.center_buffer = self.frame_buffer[
            self.center_crop.top  : self.center_crop.bottom, 
            self.center_crop.left : self.center_crop.right
        ]

        bounding_boxes = self.DetectCenterBoundingBoxes()
        debugs = []
        for box in bounding_boxes:
            card_handler = ActionCardHandler()
            card_handler.OnResize(box)
            card_id, dist = card_handler.Update(self.center_buffer, self.db)
            if dist > cfg.threshold:
                card_id = -1
            debugs.append((self.db["actions"][card_id]["zh-HANS"] if card_id >= 0 else "None", dist))
        if debugs:
            logging.debug(f"{debugs=}")
    

    def DetectCenterBoundingBoxes(self):
        # Convert to grayscale
        gray = cv2.cvtColor(self.center_buffer, cv2.COLOR_BGR2GRAY)

        # Thresholding
        _, thresh = cv2.threshold(gray, 180, 255, cv2.THRESH_BINARY)

        # Find contours
        contours, _ = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        # Filter and draw bounding boxes around the detected cards
        bounding_boxes = []
        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)
            if h < self.center_crop.height * 0.65 or h > self.center_crop.height * 0.7:
                continue
            if abs(w / h - 420 / 720) > 0.1:
                continue

            bounding_boxes.append((x, y, w, h))
        
        if cfg.DEBUG:
            self.thresh = thresh
            self.bounding_boxes = bounding_boxes

        return bounding_boxes