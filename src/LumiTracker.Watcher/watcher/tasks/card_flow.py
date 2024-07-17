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
        # cross_kernel = cv2.getStructuringElement(cv2.MORPH_CROSS, (3, 3))
        # thresh = cv2.erode(thresh, None, iterations=1)

        # # Calculate the Euclidean distance from the target color
        # lab = cv2.cvtColor(self.center_buffer, cv2.COLOR_BGR2Lab).astype(np.int32)
        # # bgr = self.center_buffer
        # threshold = 50
        # target_color1 = [232, 222, 197]
        # target_color2 = [240, 224, 146]
        # target_color1 = cv2.cvtColor(np.uint8([[target_color1]]), cv2.COLOR_RGB2Lab)[0][0]
        # target_color2 = cv2.cvtColor(np.uint8([[target_color2]]), cv2.COLOR_RGB2Lab)[0][0]
        # distance1 = np.sum(np.abs(lab[..., :3] - target_color1[:3]), axis=2)
        # mask1 = np.uint8(distance1 < threshold) * 255
        # distance2 = np.sum(np.abs(lab[..., :3] - target_color2[:3]), axis=2)
        # mask2 = np.uint8(distance2 < threshold) * 255
        # thresh = cv2.bitwise_or(mask1, mask2)
        # # thresh = mask2

        # # Apply Gaussian blur to reduce noise
        # blurred = cv2.GaussianBlur(gray, (5, 5), 0)
        # # Apply Canny edge detection
        # edges = cv2.Canny(blurred, 50, 300)
        # # Dilate the edges to emphasize thick borders
        # # dilated = cv2.dilate(edges, None, iterations=2)
        # # thresh = dilated
        # thresh = edges

        # Find contours
        contours, _ = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        # Filter and draw bounding boxes around the detected cards
        bboxes = []
        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)
            if h < self.center_crop.height * 0.65:
                continue
            # ratio = w / h
            # if ratio < 0.55 or ratio > 0.75:
            #     continue
            bboxes.append(CropBox(x, y, x + w, y + h))
            
            # use right-bottom as anchor
            right  = x + w
            bottom = y + h
            left   = right  - self.action_card_w
            top    = bottom - self.action_card_h
            if left < 0 or top < 0:
                continue
            # bboxes.append(CropBox(left, top, right, bottom))
        
        if cfg.DEBUG:
            self.thresh = thresh
            self.bboxes = bboxes
            self.gray = gray

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