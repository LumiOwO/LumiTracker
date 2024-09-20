from .base import TaskBase

from ..enums import EAnnType, ECtrlType, ETaskType, ERegionType
from ..config import cfg, override, LogDebug, LogInfo
from ..regions import REGIONS
from ..feature import CropBox, ExtractFeature_Control, ExtractFeature_Digit_Binalized
from ..database import SaveImage
from ..stream_filter import StreamFilter

import numpy as np
import logging
import os
import cv2

class RoundTask(TaskBase):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
        self.task_type = ETaskType.ROUND
        self.crop_box  = None  # init when resize
        self.Reset()

    @override
    def Reset(self):
        self.filter = StreamFilter(null_val=-1)

    @override
    def OnResize(self, client_width, client_height, ratio_type):
        box    = REGIONS[ratio_type][ERegionType.ROUND]
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])

        self.crop_box = CropBox(left, top, left + width, top + height)

    @override
    def Tick(self):
        buffer = self.frame_buffer[
            self.crop_box.top  : self.crop_box.bottom, 
            self.crop_box.left : self.crop_box.right
        ]
        self.buffer = buffer

        cur_round = self.DetectCurrentRound(buffer)
        cur_round = self.filter.Filter(cur_round, dist=0)

        if cur_round != -1:
            self.fm.round = cur_round
            LogInfo(
                info=f"Found Round Text",
                type=self.task_type.name, 
                round=self.fm.round,
                )

    def DetectCurrentRound(self, buffer):
        # Convert to grayscale
        gray = cv2.cvtColor(buffer, cv2.COLOR_BGRA2GRAY)
        _, binary = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)

        bboxes = self.GetContentBBoxes(binary)
        hash_size = cfg.hash_size

        # detect round count, from right to left
        cur_round = 0
        base = 1
        index = len(bboxes) - 1
        while index >= 0:
            bbox = bboxes[index]

            # digit should crop from binary image
            content = binary[bbox.top:bbox.bottom, bbox.left:bbox.right]
            feature = ExtractFeature_Digit_Binalized(content)
            results, dists = self.db.SearchByFeature(feature, EAnnType.DIGITS)
            digit = results[0] % 10 if dists[0] <= cfg.threshold else -1
            if digit >= 0:
                # LogDebug(digit=digit, dists=dists[:3])
                cur_round += digit * base
                base *= 10
                index -= 1
            else:
                break

        if cur_round == 0:
            return -1
        
        # merge remain bboxes, detect round text
        if index < 0:
            return -1
        remain_bbox = bboxes[index]
        index -= 1
        while index >= 0:
            remain_bbox.Merge(bboxes[index])
            index -= 1
        
        if remain_bbox.width < hash_size or remain_bbox.height < hash_size:
            return -1

        # round text should crop from colored buffer
        content = buffer[remain_bbox.top:remain_bbox.bottom, remain_bbox.left:remain_bbox.right]
        feature = ExtractFeature_Control(content)
        ctrl_ids, dists = self.db.SearchByFeature(feature, EAnnType.CTRLS)
        found = (dists[0] <= cfg.strict_threshold) and (
            ctrl_ids[0] >= ECtrlType.ROUND_FIRST.value) and (ctrl_ids[0] <= ECtrlType.ROUND_LAST.value)

        # LogDebug(found=found, cur_round=cur_round)

        if cfg.DEBUG_SAVE:
            SaveImage(content, os.path.join(cfg.debug_dir, "save", f"{self.task_type.name}.png"))

        return cur_round if found else -1

    def GetContentBBoxes(self, binary):
        contours, _ = cv2.findContours(binary, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        bboxes = []
        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)
            bboxes.append(CropBox(x, y, x + w, y + h))

        if not bboxes:
            return []

        bboxes.sort(key=lambda box: box.right)

        # Merge the bboxes that overlap along the x-axis
        merged_bboxes = []
        current_bbox = bboxes[0]
        for i in range(1, len(bboxes)):
            bbox = bboxes[i]
            if current_bbox.right >= bbox.left:
                current_bbox.Merge(bbox)
            else:
                merged_bboxes.append(current_bbox)
                current_bbox = bbox
        merged_bboxes.append(current_bbox)

        return merged_bboxes
