from .base import TaskBase

from ..enums import EAnnType, ECtrlType, EGameEvent, ERegionType
from ..config import cfg, override, LogDebug, LogInfo
from ..regions import REGIONS
from ..feature import CropBox, ExtractFeature_Control, CharacterCardHandler, ChracterName
from ..database import SaveImage
from ..stream_filter import StreamFilter

import numpy as np
import logging
import os

class GameStartTask(TaskBase):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
        self.event_type = EGameEvent.GAME_START
        self.crop_box   = None  # init when resize
        self.handlers = [CharacterCardHandler() for _ in range(6)]
        self.Reset()

        # update every frame
        self.detected = False

    @override
    def Reset(self):
        self.filter = StreamFilter(null_val=False)
        self.detect_characters = False
        self.cards  = [-1 for _ in range(6)]
        self.card_filters = [StreamFilter(null_val=-1) for _ in range(6)]

    @override
    def OnResize(self, client_width, client_height, ratio_type):
        box    = REGIONS[ratio_type][ERegionType.GAME_START]
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])

        self.crop_box = CropBox(left, top, left + width, top + height)

        box    = REGIONS[ratio_type][ERegionType.VS_ANCHOR]
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])
        margin = round(client_width  * box[4])

        for i in range(6):
            self.handlers[i].OnResize(CropBox(0, 0, width, height))

        self.vs_anchor_box = CropBox(left, top, left + width, top + height)
        self.vs_left_offsets = []
        # my
        for i in range(3):
            self.vs_left_offsets.append(i * (width + margin))
        # op
        op_left = client_width - (left + self.vs_left_offsets[-1] + width)
        op_offset = op_left - left
        for i in range(3):
            self.vs_left_offsets.append(op_offset + i * (width + margin))

    @override
    def Tick(self):
        buffer = self.frame_buffer[
            self.crop_box.top  : self.crop_box.bottom, 
            self.crop_box.left : self.crop_box.right
        ]

        feature = ExtractFeature_Control(buffer)
        ctrl_ids, dists = self.db.SearchByFeature(feature, EAnnType.CTRLS)
        start = (dists[0] <= cfg.strict_threshold) and (ctrl_ids[0] == ECtrlType.GAME_START.value)
        start = self.filter.Filter(start, dists[0])

        self.detected = start

        if start:
            self.fm.game_started = True
            self.detect_characters = True

            LogInfo(
                info=f"Game Started, last dist in window = {dists[0]}",
                type=self.event_type.name,
                )
            if cfg.DEBUG_SAVE:
                SaveImage(buffer, os.path.join(cfg.debug_dir, "save", f"{self.event_type.name}.png"))

        if self.detect_characters:
            if not self.filter.PrevSignalHasLeft():
                self.DetectCharacters()
            else:
                self.Reset()

    def DetectCharacters(self):
        my_ctx = [0, 0] # prev, cur
        op_ctx = [0, 0]

        for i in range(6):
            ctx = my_ctx if i < 3 else op_ctx
            if self.cards[i] >= 0:
                ctx[0] += 1
                continue

            offset = self.vs_left_offsets[i]
            buffer = self.frame_buffer[
                self.vs_anchor_box.top  : self.vs_anchor_box.bottom, 
                self.vs_anchor_box.left + offset : self.vs_anchor_box.right + offset
            ]
            card_id, dist, dists = self.handlers[i].Update(buffer, self.db)

            card_id = self.card_filters[i].Filter(card_id, dist=dist)

            # record last detected card_id
            if card_id >= 0:
                self.cards[i] = card_id
                ctx[1] += 1

        my_sum = sum(my_ctx)
        if my_ctx[1] > 0 and my_sum == 3:
            LogInfo(
                type=EGameEvent.MY_CHARACTERS.name,
                cards=self.cards[:3],
                names=[ChracterName(card_id, self.db) for card_id in self.cards[:3]],
                )

        op_sum = sum(op_ctx)
        if my_sum == 3 and op_ctx[1] > 0 and op_sum == 3:
            LogInfo(
                type=EGameEvent.OP_CHARACTERS.name,
                cards=self.cards,
                names=[ChracterName(card_id, self.db) for card_id in self.cards[3:]],
                )
