from __future__ import annotations
from .base import TaskBase

from ..enums import EAnnType, ECtrlType, EGameEvent, ERegionType
from ..config import cfg, override, LogDebug, LogInfo
from ..regions import REGIONS, GetRatioType
from ..feature import CropBox, CharacterCardHandler, ChracterName
from ..feature import FeatureDistance, ExtractFeature_Control_Single
from ..database import SaveImage
from ..stream_filter import StreamFilter

import numpy as np
import logging
import os
import enum

class SingleCharacterTask(TaskBase):
    def __init__(self, frame_manager, parent : AllCharactersTask, index):
        super().__init__(frame_manager)
        self.parent   = parent
        self.index    = index
        self.handler  = CharacterCardHandler()
        # init when parent resize
        self.crop_box      = None   
        self.corner_box    = None
        self.active_deltaY = 0 # init when parent resize
        self.Reset()

    @override
    def Reset(self):
        # VS
        self.card_id = -1
        self.filter  = StreamFilter(null_val=-1)
        self.corner_feature = np.array([])
        # InGame
        self.isActiveCharacter = False

    @override
    def OnResize(self, client_width, client_height, ratio_type):
        # Should resized by parent
        raise NotImplementedError()

    @override
    def Tick(self):
        # Should Tick by parent
        raise NotImplementedError()

    def TickVS(self):
        buffer = self.frame_buffer[
            self.crop_box.top  : self.crop_box.bottom, 
            self.crop_box.left : self.crop_box.right
        ]
        card_id, dist, dists = self.handler.Update(buffer, self.db)
        card_id = self.filter.Filter(card_id, dist=dist)
        if card_id >= 0:
            self.card_id = card_id
            corner = self.frame_buffer[
                self.corner_box.top  : self.corner_box.bottom, 
                self.corner_box.left : self.corner_box.right
            ]
            self.corner_feature = ExtractFeature_Control_Single(corner)
            # SaveImage(corner, os.path.join(cfg.debug_dir, "save", f"Corner{self.index}_VS.png"))
            # SaveImage(buffer, os.path.join(cfg.debug_dir, "save", f"Char{self.index}_VS.png"))

    def TickInGame(self):
        if len(self.corner_feature) == 0:
            # TODO: LogError
            return

        corner = self.frame_buffer[
            self.corner_box.top  : self.corner_box.bottom, 
            self.corner_box.left : self.corner_box.right
        ]
        feature = ExtractFeature_Control_Single(corner)
        dist = FeatureDistance(feature, self.corner_feature)
        detected = (dist <= cfg.strict_threshold)
        if detected:
            LogDebug(index=self.index, isActive=f"{detected}", dist=dist)
            # SaveImage(corner, os.path.join(cfg.debug_dir, "save", f"Corner{self.index}_InGame.png"))

            buffer = self.frame_buffer[
                self.crop_box.top + self.active_deltaY  : self.crop_box.bottom + self.active_deltaY, 
                self.crop_box.left : self.crop_box.right
            ]
            # SaveImage(buffer, os.path.join(cfg.debug_dir, "save", f"Char{self.index}_InGame.png"))


class AllCharactersTask(TaskBase):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
        self.tasks = [SingleCharacterTask(frame_manager, self, i) for i in range(6)]
        self.Reset()

    @override
    def Reset(self):
        for i in range(6):
            self.tasks[i].Reset()
        self.SetRegionType(ERegionType.CharVS)

    def SetRegionType(self, region):
        self.region = region
        if self.frame_buffer is not None:
            height = self.frame_buffer.shape[0]
            width  = self.frame_buffer.shape[1]
            self.OnResize(width, height, GetRatioType(width, height))

    @override
    def SetFrameBuffer(self, frame_buffer):
        super().SetFrameBuffer(frame_buffer)
        for i in range(6):
            self.tasks[i].SetFrameBuffer(frame_buffer)

    @override
    def OnResize(self, client_width, client_height, ratio_type):
        box          = REGIONS[ratio_type][self.region]
        left         = round(client_width  * box[0])
        top          = round(client_height * box[1])
        width        = round(client_width  * box[2])
        height       = round(client_height * box[3])

        box          = REGIONS[ratio_type][ERegionType.CharOffset]
        marginVS     = round(client_width  * box[0])
        marginInGame = round(client_width  * box[1])
        deltaY       = round(client_height * box[2])
        for i in range(6):
            self.tasks[i].handler.OnResize(CropBox(0, 0, width, height))

            offsetX = 0
            offsetY = 0
            corner_offsetY = 0
            if self.region == ERegionType.CharVS:
                if i < 3:
                    offsetX = i * (width + marginVS)
                else:
                    op_left = client_width  - (left + width + 2 * (width + marginVS))
                    offsetX = op_left - left
                    offsetX += (i - 3) * (width + marginVS)

            elif self.region == ERegionType.CharInGame:
                offsetX = (i % 3) * (width + marginInGame)
                if i >= 3:
                    op_top = client_height - (top + height)
                    offsetY = op_top - top
                corner_offsetY = -deltaY if i < 3 else deltaY
                self.tasks[i].active_deltaY = corner_offsetY
            else:
                raise NotImplementedError()

            crop_box = CropBox(
                left + offsetX, 
                top  + offsetY, 
                left + offsetX + width, 
                top  + offsetY + height
                )

            box           = REGIONS[ratio_type][ERegionType.CharCorner]
            corner_left   = round(crop_box.width  * box[0])
            corner_top    = round(crop_box.height * box[1])
            corner_width  = round(crop_box.width  * box[2])
            corner_height = round(crop_box.height * box[3])
            if self.region == ERegionType.CharVS:
                # TODO: change CharCorner fields to (scalex, scaley, width, height)
                corner_width  = round(corner_width  * 1.1 * 1.0)
                corner_height = round(corner_height * 1.1 * 1.0330)
                corner_box = CropBox(
                    crop_box.right - corner_width, 
                    crop_box.top   + corner_offsetY,
                    crop_box.right, 
                    crop_box.top   + corner_height + corner_offsetY,
                    )
            else:
                corner_box = CropBox(
                    crop_box.left + corner_left, 
                    crop_box.top  + corner_top  + corner_offsetY,
                    crop_box.left + corner_left + corner_width, 
                    crop_box.top  + corner_top  + corner_height + corner_offsetY,
                    )
            LogDebug(corner_box=f"{corner_box}")

            self.tasks[i].crop_box = crop_box
            self.tasks[i].corner_box = corner_box

    @override
    def Tick(self):
        if self.region == ERegionType.CharVS:
            self.TickVS()
        elif self.region == ERegionType.CharInGame:
            self.TickInGame()
        else:
            raise NotImplementedError()

    def TickVS(self):
        my_ctx = [0, 0] # prev, cur
        op_ctx = [0, 0]
        for i in range(6):
            task = self.tasks[i]
            ctx = my_ctx if i < 3 else op_ctx
            if task.card_id >= 0:
                ctx[0] += 1
                continue

            task.TickVS()

            if task.card_id >= 0:
                ctx[1] += 1

        cards = [task.card_id for task in self.tasks]
        my_sum = sum(my_ctx)
        if my_ctx[1] > 0 and my_sum == 3:
            LogInfo(
                type=EGameEvent.MyCharacters.name,
                cards=cards[:3],
                names=[ChracterName(card_id, self.db) for card_id in cards[:3]],
                )

        op_sum = sum(op_ctx)
        if my_sum == 3 and op_ctx[1] > 0 and op_sum == 3:
            LogInfo(
                type=EGameEvent.OpCharacters.name,
                cards=cards,
                names=[ChracterName(card_id, self.db) for card_id in cards[3:]],
                )
            self.fm.op_characters = cards[3:]

    def TickInGame(self):
        for i in range(6):
            self.tasks[i].TickInGame()
