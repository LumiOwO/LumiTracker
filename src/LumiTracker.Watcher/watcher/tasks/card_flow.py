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
        self.center_crop   = None
        self.flow_anchor   = None
        self.my_deck_crop  = None
        self.op_deck_crop  = None

        if cfg.DEBUG:
            self.bboxes = []

    def OnResize(self, client_width, client_height, ratio_type):
        box    = REGIONS[ratio_type][ERegionType.CENTER]        # left, top, width, height
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])
        self.center_crop = CropBox(left, top, left + width, top + height)

        box    = REGIONS[ratio_type][ERegionType.FLOW_ANCHOR] 
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])
        self.flow_anchor = CropBox(left, top, left + width, top + height)

        box    = REGIONS[ratio_type][ERegionType.MY_DECK] 
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])
        self.my_deck_crop = CropBox(left, top, left + width, top + height)

        box    = REGIONS[ratio_type][ERegionType.OP_DECK] 
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])
        self.op_deck_crop = CropBox(left, top, left + width, top + height)

    def _PreTick(self, frame_manager):
        self.valid = frame_manager.game_started

    def _Tick(self, frame_manager):
        bboxes = self.DetectCenterBoundingBoxes()
        debugs = []
        debug_bboxes = []
        for box in bboxes:
            box.left   = box.left + self.center_crop.left
            box.top    = self.flow_anchor.top
            box.right  = box.left + self.flow_anchor.width
            box.bottom = box.top  + self.flow_anchor.height
            debug_bboxes.append(box)

            card_handler = ActionCardHandler()
            card_handler.OnResize(box)
            card_id, dist = card_handler.Update(self.frame_buffer, self.db)
            if dist > cfg.threshold:
                card_id = -1
            debugs.append((self.db["actions"][card_id]["zh-HANS"] if card_id >= 0 else "None", dist))
        if debugs:
            logging.debug(f"{debugs=}")
            self.bboxes = debug_bboxes
        
        my_drawn_detected = self.DetectDeck(is_op=False)
        if my_drawn_detected:
            logging.debug(f"{my_drawn_detected=}")
        
        op_drawn_detected = self.DetectDeck(is_op=True)
        if op_drawn_detected:
            logging.debug(f"{op_drawn_detected=}")
    

    def DetectCenterBoundingBoxes(self):
        center_buffer = self.frame_buffer[
            self.center_crop.top  : self.center_crop.bottom, 
            self.center_crop.left : self.center_crop.right
        ]

        # Convert to grayscale
        gray = cv2.cvtColor(center_buffer, cv2.COLOR_BGR2GRAY)

        # Thresholding
        _, thresh = cv2.threshold(gray, 200, 255, cv2.THRESH_BINARY)
        # cross_kernel = cv2.getStructuringElement(cv2.MORPH_CROSS, (3, 3))
        # thresh = cv2.erode(thresh, None, iterations=1)

        # # Calculate the Euclidean distance from the target color
        # lab = cv2.cvtColor(center_buffer, cv2.COLOR_BGR2Lab).astype(np.int32)
        # # bgr = center_buffer
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
        FILTER_H = self.center_crop.height * 0.5
        filtered_bboxes = []
        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)
            if h < FILTER_H:
                continue
            # ratio = w / h
            # if ratio < 0.55 or ratio > 0.75:
            #     continue
            filtered_bboxes.append(CropBox(x, y, x + w, y + h))
            
            # # use right-bottom as anchor
            # right  = x + w
            # bottom = y + h
            # left   = right  - self.action_card_w
            # top    = bottom - self.action_card_h
            # if left < 0 or top < 0:
            #     continue
            # bboxes.append(CropBox(left, top, right, bottom))
        if not filtered_bboxes:
            return []
        
        # sort the boxes
        if len(filtered_bboxes) > 1:
            filtered_bboxes.sort(key=lambda box: box.left)
        
        # merge bboxes
        MERGED_W_MIN = self.center_crop.width  * 0.1
        MERGED_W_MAX = self.center_crop.width  * 0.2
        MERGED_H_MIN = self.center_crop.height * 0.95
        MERGE_DIST   = self.center_crop.width  * 0.15
        def ValidMergedBox(bbox):
            return bbox.width > MERGED_W_MIN and bbox.width < MERGED_W_MAX and bbox.height > MERGED_H_MIN

        current_bbox = filtered_bboxes[0]
        bboxes = []
        for bbox in filtered_bboxes[1:]:
            dist = bbox.left - current_bbox.left
            if dist < MERGE_DIST:
                current_bbox.Merge(bbox)
            else:
                # filter noise
                if ValidMergedBox(current_bbox):
                    bboxes.append(current_bbox)
                current_bbox = bbox
        if ValidMergedBox(current_bbox):
            bboxes.append(current_bbox)

        def AtCenter(center_x):
            return abs(center_x / self.center_crop.width - 0.5) < 0.1

        num_bboxes = len(bboxes)
        if num_bboxes == 1:
            center_x = bboxes[0].left + bboxes[0].width / 2
            if not AtCenter(center_x):
                bboxes = []
        elif num_bboxes > 1:
            center_sum = 0
            center_sum += bboxes[0].left + bboxes[0].width / 2
            center_sum += bboxes[1].left + bboxes[1].width / 2
            # check distance between cards
            prev_dist = bboxes[1].right - bboxes[0].right
            for i in range(2, num_bboxes):
                dist = bboxes[i].right - bboxes[i - 1].right
                if abs(dist - prev_dist) > 10:
                    # invalid
                    bboxes = []
                    break
                prev_dist = dist
                center_sum += bboxes[i].left + bboxes[i].width / 2
            center_x = center_sum / num_bboxes
            if bboxes and (not AtCenter(center_x)):
                bboxes = []

        if cfg.DEBUG:
            self.center_buffer = center_buffer
            self.thresh = thresh
            self.bboxes = bboxes
            self.gray = gray

        return bboxes
    
    def DetectDeck(self, is_op):
        deck_crop = self.op_deck_crop if is_op else self.my_deck_crop
        deck_buffer = self.frame_buffer[
            deck_crop.top  : deck_crop.bottom, 
            deck_crop.left : deck_crop.right
        ]

        # Convert to grayscale
        gray = cv2.cvtColor(deck_buffer, cv2.COLOR_BGR2GRAY)

        # # Thresholding
        # _, thresh = cv2.threshold(gray, 200, 255, cv2.THRESH_BINARY)
        # # cross_kernel = cv2.getStructuringElement(cv2.MORPH_CROSS, (3, 3))
        # # thresh = cv2.erode(thresh, None, iterations=1)

        # # Calculate the Euclidean distance from the target color
        # lab = cv2.cvtColor(center_buffer, cv2.COLOR_BGR2Lab).astype(np.int32)
        # # bgr = center_buffer
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

        # Apply Gaussian blur to reduce noise
        # blurred = cv2.GaussianBlur(gray, (5, 5), 0)
        blurred = gray
        # Apply Canny edge detection
        edges = cv2.Canny(blurred, 50, 150)
        # Dilate the edges to emphasize thick borders
        # dilated = cv2.dilate(edges, None, iterations=2)
        # thresh = dilated
        thresh = edges

        # Find contours
        contours, _ = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        FILTER_H     = deck_crop.height * 0.5
        FILTER_W_MIN = deck_crop.width  * 0.4
        FILTER_W_MAX = deck_crop.width  * 0.7
        bboxes = []
        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)
            if h < FILTER_H or w < FILTER_W_MIN or w > FILTER_W_MAX:
                continue
            ratio = w / h
            if ratio < 1.5:
                continue
            bboxes.append(CropBox(x, y, x + w, y + h))
            
            # # use right-bottom as anchor
            # right  = x + w
            # bottom = y + h
            # left   = right  - self.action_card_w
            # top    = bottom - self.action_card_h
            # if left < 0 or top < 0:
            #     continue
            # bboxes.append(CropBox(left, top, right, bottom))


        if cfg.DEBUG:
            if not is_op:
                self.my_deck_buffer = deck_buffer
                self.my_deck_thresh = thresh
                self.my_deck_bboxes = bboxes
            else:
                self.op_deck_buffer = deck_buffer
                self.op_deck_thresh = thresh
                self.op_deck_bboxes = bboxes
        
        return True if bboxes else False
