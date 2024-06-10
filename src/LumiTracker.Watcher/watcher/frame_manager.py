import os
import time
import logging

from types import SimpleNamespace

from PIL import Image

from .config import cfg
from .database import Database
from .database import ExtractFeature, FeatureDistance, HashToFeature

class StreamFilter:
    def __init__(self, null_val, valid_count=5):
        self.value        = null_val
        self.count        = 0
        self.valid_count  = valid_count
        self.null_val     = null_val
    
    def Filter(self, value):
        # push
        if value == self.null_val:
            self.value = value
            self.count = 0
        elif self.value == value:
            self.count += 1
        else:
            self.value = value
            self.count = 1

        # logging.debug(self.count, self.value)

        # read
        if self.value != self.null_val and self.count == self.valid_count:
            return self.value
        else:
            return self.null_val

class FrameManager:
    def __init__(self):
        self.db            = Database()
        self.db.Load()
        self.start_feature = HashToFeature(self.db["controls"]["start_hash"])

        self.prev_log_time   = time.time()
        self.prev_frame_time = self.prev_log_time
        self.frame_count     = 0

        self.filters            = SimpleNamespace()
        self.filters.my_event   = StreamFilter(null_val=-1)
        self.filters.op_event   = StreamFilter(null_val=-1)
        self.filters.game_start = StreamFilter(null_val=False)

    def DetectGameStart(self, frame):
        start_w = int(frame.size[0] * cfg.start_screen_size[0])
        start_h = int(frame.size[1] * cfg.start_screen_size[1])

        start_left = (1.0 - cfg.start_screen_size[0]) / 2
        start_left = int(frame.size[0] * start_left)
        start_top  = (1.0 - cfg.start_screen_size[1]) / 2
        start_top  = int(frame.size[1] * start_top )
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

    def DetectEvent(self, frame):
        event_w = int(frame.size[0] * cfg.event_screen_size[0])
        event_h = int(frame.size[1] * cfg.event_screen_size[1])
        
        # my event
        my_left = int(frame.size[0] * cfg.my_event_pos[0])
        my_top  = int(frame.size[1] * cfg.my_event_pos[1])
        my_event_frame = frame.crop((my_left, my_top, my_left + event_w, my_top + event_h))

        my_feature = ExtractFeature(my_event_frame)
        my_id, my_dist = self.db.SearchByFeature(my_feature, card_type="event")
        
        if my_dist > cfg.threshold:
            my_id = -1
        my_id = self.filters.my_event.Filter(my_id)

        if my_id >= 0:
            logging.debug(f'"info": "my event: {self.db["events"][my_id].get("name_CN", "None")}, {my_dist=}"')
            logging.info(f'"type": "my_event_card", "card_id": {my_id}')

        # op event
        op_left = int(frame.size[0] * cfg.op_event_pos[0])
        op_top  = int(frame.size[1] * cfg.op_event_pos[1])
        op_event_frame = frame.crop((op_left, op_top, op_left + event_w, op_top + event_h))

        op_feature = ExtractFeature(op_event_frame)
        op_id, op_dist = self.db.SearchByFeature(op_feature, card_type="event")
        
        if op_dist > cfg.threshold:
            op_id = -1
        op_id = self.filters.op_event.Filter(op_id)

        if op_id >= 0:
            logging.debug(f'"info": "op event: {self.db["events"][op_id].get("name_CN", "None")}, {op_dist=}"')
            logging.info(f'"type": "op_event_card", "card_id": {op_id}')

        if cfg.DEBUG_SAVE:
            my_event_frame.save(os.path.join(cfg.debug_dir, "save", f"my_image{self.frame_count}.png"))
            op_event_frame.save(os.path.join(cfg.debug_dir, "save", f"op_image{self.frame_count}.png"))

    def OnFrameArrived(self, frame: Image):
        self.DetectGameStart(frame)
        self.DetectEvent(frame)

        self.frame_count += 1
        cur_time = time.time()

        if cur_time - self.prev_log_time >= cfg.LOG_INTERVAL:
            logging.debug(f'"info": "FPS: {self.frame_count / (cur_time - self.prev_log_time)}"')
            self.frame_count   = 0
            self.prev_log_time = cur_time