from .base import TaskBase

from ..enums import EAnnType, ETaskType, ERegionType
from ..config import cfg, LogDebug, LogInfo
from ..regions import REGIONS
from ..database import CropBox
from ..database import ActionCardHandler
from ..stream_filter import StreamFilter

from types import SimpleNamespace
from collections import deque, defaultdict
import numpy as np
import os
import cv2
import time

class CardFlowTask(TaskBase):
    def __init__(self, db):
        super().__init__(db)
        self.center_crop   = None
        self.flow_anchor   = None
        self.my_deck_crop  = None
        self.op_deck_crop  = None
        self.Reset()
    
    def Reset(self):
        # round 0, five cards to detect
        self.round0 = SimpleNamespace()
        self.round0.cards   = [-1 for _ in range(5)]
        self.round0.filters = [StreamFilter(null_val=-1) for _ in range(5)]

        # round 1 ~ n
        self.MAX_NUM_CARDS = 6
        self.filter        = StreamFilter(null_val=0)
        self.card_recorder = [[]]
        for i in range(self.MAX_NUM_CARDS):
            self.card_recorder.append([defaultdict(int) for _ in range(i + 1)])
        
        self.signaled_num_cards = 0
        self.signaled_timestamp = 0 
        self.my_deck_queue = deque()
        self.op_deck_queue = deque()

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
        if frame_manager.round == 0:
            self._DetectRound0()
        else:
            self._DetectRound()
            self._DumpDetected()

    def _DetectRound0(self):
        bboxes = self.DetectCenterBoundingBoxes()
        num_bboxes = len(bboxes)

        if num_bboxes != 5:
            return

        for i, box in enumerate(bboxes):
            box.left   = box.left + self.center_crop.left
            box.top    = self.flow_anchor.top
            box.right  = box.left + self.flow_anchor.width
            box.bottom = box.top  + self.flow_anchor.height

            card_handler = ActionCardHandler()
            card_handler.OnResize(box)
            card_id, dist = card_handler.Update(self.frame_buffer, self.db)
            if dist > cfg.threshold:
                card_id = -1
            card_id = self.round0.filters[i].Filter(card_id, dist)

            # record last detected card_id
            if card_id >= 0:
                self.round0.cards[i] = card_id

    def _DetectRound(self):
        # center
        bboxes = self.DetectCenterBoundingBoxes()
        num_bboxes = len(bboxes)

        if cfg.DEBUG:
            detected = []
            debug_bboxes = []
        recorder = self.card_recorder[num_bboxes]
        invalid_count = 0
        for i, box in enumerate(bboxes):
            box.left   = box.left + self.center_crop.left
            box.top    = self.flow_anchor.top
            box.right  = box.left + self.flow_anchor.width
            box.bottom = box.top  + self.flow_anchor.height

            card_handler = ActionCardHandler()
            card_handler.OnResize(box)
            card_id, dist = card_handler.Update(self.frame_buffer, self.db)
            if dist > cfg.threshold:
                card_id = -1
                invalid_count += 1

            if card_id >= 0:
                recorder[i][card_id] += 1

            if cfg.DEBUG:
                debug_bboxes.append(box)
                detected.append((card_id, dist))

        if cfg.DEBUG:
            self.bboxes = debug_bboxes
            # if detected:
            #     LogDebug(center=[(self.db["actions"][card[0]]["zh-HANS"] if card[0] >= 0 else "None", card[1]) for card in detected])

        timestamp = time.perf_counter()
        # my deck
        my_drawn_detected = self.DetectDeck(is_op=False)
        if my_drawn_detected:
            # LogDebug(my_drawn_detected=my_drawn_detected)
            self.my_deck_queue.append(timestamp)
        
        # op deck
        op_drawn_detected = self.DetectDeck(is_op=True)
        if op_drawn_detected:
            # LogDebug(op_drawn_detected=op_drawn_detected)
            self.op_deck_queue.append(timestamp)

        # stream filtering
        num_cards = num_bboxes if invalid_count != num_bboxes else 0
        num_cards = self.filter.Filter(num_cards, dist=0)
        if num_cards > 0:
            self.signaled_num_cards = num_cards
            self.signaled_timestamp = timestamp
    
    def _DumpDetected(self):
        # flush round 0 result
        if self.round0.cards:
            cards = self.round0.cards
            LogInfo(type=ETaskType.MY_DRAWN.name, cards=cards,
                    names=[(self.db["actions"][card]["zh-HANS"]) for card in cards])
            self.round0.cards = []
        
        # dump if signaled
        if self.signaled_num_cards == 0:
            return
        WAIT_TIME = 1.2  # seconds
        if time.perf_counter() - self.signaled_timestamp < WAIT_TIME:
            return
        
        task_type = ETaskType.NONE
        while len(self.my_deck_queue) > 0:
            timestamp = self.my_deck_queue[0]
            if timestamp > self.signaled_timestamp + WAIT_TIME:
                break
            self.my_deck_queue.popleft()
            if task_type != ETaskType.NONE:
                continue

            if timestamp < self.signaled_timestamp - WAIT_TIME:
                continue
            task_type = ETaskType.MY_DRAWN if timestamp < self.signaled_timestamp else ETaskType.MY_CREATE_DECK
        
        while len(self.op_deck_queue) > 0:
            timestamp = self.op_deck_queue[0]
            if timestamp > self.signaled_timestamp + WAIT_TIME:
                break
            self.op_deck_queue.popleft()
            if task_type != ETaskType.NONE:
                continue

            if timestamp < self.signaled_timestamp:
                continue
            task_type = ETaskType.OP_CREATE_DECK

        if task_type != ETaskType.NONE:
            recorder = self.card_recorder[self.signaled_num_cards]
            cards = [max(d, key=d.get) for d in recorder]
            LogInfo(type=task_type.name, cards=cards,
                    names=[(self.db["actions"][card]["zh-HANS"]) for card in cards])
        
        # reset
        self.card_recorder[self.signaled_num_cards] = [defaultdict(int) for _ in range(self.signaled_num_cards)]
        self.signaled_num_cards = 0
        self.signaled_timestamp = 0 


    def DetectCenterBoundingBoxes(self):
        center_buffer = self.frame_buffer[
            self.center_crop.top  : self.center_crop.bottom, 
            self.center_crop.left : self.center_crop.right
        ]

        # Convert to grayscale
        gray = cv2.cvtColor(center_buffer, cv2.COLOR_BGR2GRAY)

        # Thresholding
        _, thresh = cv2.threshold(gray, 200, 255, cv2.THRESH_BINARY)

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

        # Apply Canny edge detection
        edges = cv2.Canny(gray, 50, 150)

        # Find contours
        contours, _ = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

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

        if cfg.DEBUG:
            if not is_op:
                self.my_deck_buffer = deck_buffer
                self.my_deck_edges  = edges
                self.my_deck_bboxes = bboxes
            else:
                self.op_deck_buffer = deck_buffer
                self.op_deck_edges  = edges
                self.op_deck_bboxes = bboxes
        
        return True if bboxes else False
