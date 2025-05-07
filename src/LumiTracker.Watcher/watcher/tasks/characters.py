from __future__ import annotations
from .base import TaskBase

from ..enums import EAnnType, ECtrlType, EGameEvent, ERegionType
from ..config import cfg, override, LogDebug, LogInfo
from ..regions import REGIONS, GetRatioType
from ..feature import CropBox, CharacterCardHandler, ChracterName
from ..database import SaveImage
from ..stream_filter import StreamFilter

import numpy as np
import os
import cv2

class SingleCharacterTask(TaskBase):
    def __init__(self, frame_manager, parent : AllCharactersTask, index):
        super().__init__(frame_manager)
        self.parent        = parent
        self.index         = index
        self.handler       = CharacterCardHandler()
        # init when parent resize
        self.crop_box      = None   
        self.border_box    = None
        self.active_deltaY = 0
        self.Reset()

    @override
    def Reset(self):
        # VS
        self.card_id = -1
        self.filter  = StreamFilter(null_val=-1)

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

    def TickInGame(self):
        self.active_dist = 1000 # should be updated every frame

        border = self.frame_buffer[
            self.border_box.top  : self.border_box.bottom, 
            self.border_box.left : self.border_box.right
        ]
        if len(border) > 0:
            gray = cv2.cvtColor(border, cv2.COLOR_BGRA2GRAY)
            pixels_gray = gray.ravel() # Flatten the grayscale image into a 1D array of pixel intensities
            mean = np.mean(pixels_gray)
            variance = np.var(pixels_gray)
            if mean > 180 and variance <= 600:
                self.active_dist = cfg.threshold
            # if self.index == 3:
            #     LogDebug(index=self.index, mean=mean, variance=variance)
                # SaveImage(border, os.path.join(cfg.debug_dir, "save", f"border{self.index}_InGame.png"))

                # buffer = self.frame_buffer[
                #     self.border_box.top - self.active_deltaY  : self.border_box.bottom - self.active_deltaY, 
                #     self.border_box.left : self.border_box.right
                # ]
                # SaveImage(buffer, os.path.join(cfg.debug_dir, "save", f"Char{self.index}_InGame.png"))


class AllCharactersTask(TaskBase):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
        self.tasks = [SingleCharacterTask(frame_manager, self, i) for i in range(6)]
        self.Reset()

    @override
    def Reset(self):
        self.my_active = -1
        self.op_active = -1
        # Tune the parameters to make it trigger faster
        self.my_active_filter = StreamFilter(null_val=-1, window_size=10, valid_count=1, window_min_count=6)
        self.op_active_filter = StreamFilter(null_val=-1, window_size=10, valid_count=1, window_min_count=6)
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

        box          = REGIONS[ratio_type][ERegionType.CharMargin]
        marginVS     = round(client_width  * box[0])
        marginInGame = round(client_width  * box[1])
        deltaY       = round(client_height * box[2])
        for i in range(6):
            self.tasks[i].handler.OnResize(CropBox(0, 0, width, height))

            offsetX = 0
            offsetY = 0
            border_offsetY = 0
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
                border_offsetY = -deltaY if i < 3 else deltaY
                self.tasks[i].active_deltaY = border_offsetY
            else:
                raise NotImplementedError()

            crop_box = CropBox(
                left + offsetX, 
                top  + offsetY, 
                left + offsetX + width, 
                top  + offsetY + height
                )

            box           = REGIONS[ratio_type][ERegionType.CharBorder]
            border_left   = round(crop_box.width  * (1.0 - box[0]) * 0.5)
            border_width  = round(crop_box.width  * box[0])
            border_height = round(crop_box.height * box[1])
            border_box = CropBox(
                crop_box.left  + border_left, 
                crop_box.top   + border_offsetY,
                crop_box.left  + border_left   + border_width, 
                crop_box.top   + border_height + border_offsetY,
                )

            self.tasks[i].crop_box = crop_box
            self.tasks[i].border_box = border_box

    @override
    def Tick(self):
        if self.region == ERegionType.CharVS:
            self.TickVS()
        elif self.region == ERegionType.CharInGame:
            pass
            # TODO: temporarily disable this
            # self.TickInGame()
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

    def TickInGameForPlayer(self, active_filter : StreamFilter):
        is_op = (active_filter == self.op_active_filter)
        tasks = self.tasks[3:] if is_op else self.tasks[:3]

        flag = 0
        dist = 1000
        for i in range(3):
            task = tasks[i]
            task.TickInGame()
            if task.active_dist <= cfg.threshold:
                dist = min(dist, task.active_dist)
                flag |= (1 << i)

        active = -1
        for i in range(3):
            if flag == (1 << i):
                active = i
                break
        if is_op and active >= 0:
            active += 3

        active = active_filter.Filter(active, dist=dist)
        return active

    def TickInGame(self):
        should_trigger = False
        my_active = self.TickInGameForPlayer(self.my_active_filter)
        if my_active >= 0 and self.my_active != my_active:
            self.my_active = my_active
            should_trigger = True
        op_active = self.TickInGameForPlayer(self.op_active_filter)
        if op_active >= 0 and self.op_active != op_active:
            self.op_active = op_active
            should_trigger = True

        if should_trigger:
            my_card_id = self.tasks[self.my_active].card_id if self.my_active >= 0 else -1
            op_card_id = self.tasks[self.op_active].card_id if self.op_active >= 0 else -1
            LogInfo(
                type=f"{EGameEvent.ActiveIndices.name}",
                my=self.my_active,
                op=self.op_active,
                names=[ChracterName(my_card_id, self.db), ChracterName(op_card_id, self.db)]
                )
