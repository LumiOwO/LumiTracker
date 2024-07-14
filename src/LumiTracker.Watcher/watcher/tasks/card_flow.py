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
        self.center_buffer = None
        self.center_crop   = None
        self.action_card_w = 0
        self.action_card_h = 0

        # self.MAX_CARDS     = 6
        # self.card_handlers = []
        # for _ in range(self.MAX_CARDS):
        #     self.card_handlers.append(ActionCardHandler())
    
    def OnResize(self, client_width, client_height, ratio_type):
        box    = REGIONS[ratio_type][ERegionType.CENTER]        # left, top, width, height
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])
        self.center_crop   = CropBox(left, top, left + width, top + height)

        sizes  = REGIONS[ratio_type][ERegionType.FLOW_CARDS]    # center_card_w, center_card_h, ___, ___ 
        self.action_card_w = round(client_width  * sizes[0])
        self.action_card_h = round(client_height * sizes[1])
        ___  = round(client_width  * sizes[2])
        ___  = round(client_height * sizes[3])

    def _PreTick(self, frame_manager):
        self.valid = frame_manager.game_started

    def _Tick(self, frame_manager):
        # ============ center ==============
        self.center_buffer = self.frame_buffer[
            self.center_crop.top  : self.center_crop.bottom, 
            self.center_crop.left : self.center_crop.right
        ]

        bboxes = self.DetectCenterBoundingBoxes()
        debugs = []
        for box in bboxes:
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
        bboxes = []
        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)
            if h < self.center_crop.height * 0.65 or h > self.center_crop.height * 0.7:
                continue
            if abs(w / h - 420 / 720) > 0.1:
                continue
            
            # use right-bottom as anchor
            right  = x + w
            bottom = y + h
            left   = right  - self.action_card_w
            top    = bottom - self.action_card_h
            if left < 0 or top < 0:
                continue
            bboxes.append(CropBox(left, top, right, bottom))
        
        if cfg.DEBUG:
            self.thresh = thresh
            self.bboxes = bboxes

        num_bboxes = len(bboxes)
        if num_bboxes > 1:
            # sort by right
            bboxes.sort(key=lambda box: box.right)

            # check distance between cards
            prev_dist = bboxes[1].right - bboxes[0].right
            for i in range(2, num_bboxes):
                dist = bboxes[i].right - bboxes[i - 1].right
                if abs(dist - prev_dist) > 10:
                    # invalid
                    bboxes = []
                    break
                prev_dist = dist

        return bboxes