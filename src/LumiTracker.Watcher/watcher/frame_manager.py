import os
import time
import logging

from types import SimpleNamespace

from PIL import Image

from .config import cfg
from .position import POS
from .database import Database
from .database import ExtractFeature, FeatureDistance, HashToFeature
from .stream_filter import StreamFilter

class FrameManager:
    def __init__(self):
        self.db            = Database()
        self.db.Load()
        self.start_feature = HashToFeature(self.db["controls"]["GameStart"])
        self.round_feature = HashToFeature(self.db["controls"]["GameRound"])
        self.ratio         = "16:9"

        self.prev_log_time   = time.time()
        self.prev_frame_time = self.prev_log_time
        self.frame_count     = 0
        self.min_fps         = 100000
        self.first_log       = True

        self.filters            = SimpleNamespace()
        self.filters.my_event   = StreamFilter(null_val=-1)
        self.filters.op_event   = StreamFilter(null_val=-1)
        self.filters.game_start = StreamFilter(null_val=False)
        self.filters.game_round = StreamFilter(null_val=False)

    def DetectGameStart(self, frame, pos):
        start_w = int(frame.size[0] * pos.start_screen_size[0])
        start_h = int(frame.size[1] * pos.start_screen_size[1])

        start_left = int(frame.size[0] * pos.start_screen_pos[0])
        start_top  = int(frame.size[1] * pos.start_screen_pos[1])
        start_event_frame = frame.crop((start_left, start_top, start_left + start_w, start_top + start_h))

        start_feature = ExtractFeature(start_event_frame)
        dist = FeatureDistance(start_feature, self.start_feature)
        start = (dist <= cfg.threshold)
        start = self.filters.game_start.Filter(start)
        if start:
            logging.debug(f'"info": "Game start, {dist=}"')
            logging.info(f'"type": "game_start"')
            if cfg.DEBUG_SAVE:
                start_event_frame.save(os.path.join(cfg.debug_dir, "save", f"start_event_frame.png"))

    def DetectEvent(self, frame, pos):
        event_w = int(frame.size[0] * pos.event_screen_size[0])
        event_h = int(frame.size[1] * pos.event_screen_size[1])
        inner_crop = (
            cfg.center_cropbox[0] * event_w, 
            cfg.center_cropbox[1] * event_h, 
            cfg.center_cropbox[2] * event_w, 
            cfg.center_cropbox[3] * event_h
        )

        # my event
        my_left = int(frame.size[0] * pos.my_event_pos[0])
        my_top  = int(frame.size[1] * pos.my_event_pos[1])
        my_event_frame = frame.crop((
            my_left + inner_crop[0], 
            my_top  + inner_crop[1], 
            my_left + inner_crop[2], 
            my_top  + inner_crop[3]
        ))

        my_feature = ExtractFeature(my_event_frame)
        my_id, my_dist = self.db.SearchByFeature(my_feature, ann_name="event")
        
        logging.debug(f'"info": "{my_dist=}, my event: {self.db["events"][my_id]["zh-HANS"] if my_id >= 0 else "None"}"')
        if my_dist > cfg.threshold:
            my_id = -1
        my_id = self.filters.my_event.Filter(my_id)

        if my_id >= 0:
            # logging.debug(f'"info": "my event: {self.db["events"][my_id].get("zh-HANS", "None")}, {my_dist=}"')
            logging.info(f'"type": "my_event_card", "card_id": {my_id}')

        # op event
        op_left = int(frame.size[0] * pos.op_event_pos[0])
        op_top  = int(frame.size[1] * pos.op_event_pos[1])
        op_event_frame = frame.crop((
            op_left + inner_crop[0], 
            op_top  + inner_crop[1], 
            op_left + inner_crop[2], 
            op_top  + inner_crop[3]
        ))

        op_feature = ExtractFeature(op_event_frame)
        op_id, op_dist = self.db.SearchByFeature(op_feature, ann_name="event")
        
        if op_dist > cfg.threshold:
            op_id = -1
        op_id = self.filters.op_event.Filter(op_id)

        if op_id >= 0:
            logging.debug(f'"info": "op event: {self.db["events"][op_id].get("zh-HANS", "None")}, {op_dist=}"')
            logging.info(f'"type": "op_event_card", "card_id": {op_id}')

        if cfg.DEBUG_SAVE:
            my_event_frame.save(os.path.join(cfg.debug_dir, "save", f"my_image{self.frame_count}.png"))
            op_event_frame.save(os.path.join(cfg.debug_dir, "save", f"op_image{self.frame_count}.png"))

    def OnFrameArrived(self, frame: Image):
        pos = POS[self.ratio]

        self.DetectGameStart(frame, pos)
        self.DetectRound(frame, pos)
        self.DetectEvent(frame, pos)

        self.frame_count += 1
        cur_time = time.time()

        if cur_time - self.prev_log_time >= cfg.LOG_INTERVAL:
            fps = self.frame_count / (cur_time - self.prev_log_time)
            logging.debug(f'"info": "FPS: {fps}"')
            if (not self.first_log) and (fps < self.min_fps):
                logging.warning(f'"info": "Min FPS = {fps}"')
                self.min_fps = fps

            self.frame_count   = 0
            self.prev_log_time = cur_time
            self.first_log     = False