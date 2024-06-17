import os
import time
import logging

from types import SimpleNamespace

from PIL import Image
import numpy as np

from .config import cfg
from .position import POS
from .database import ExtractFeatureFromBuffer, CopyEventFeatureRegion
from .database import FeatureDistance, HashToFeature
from .database import Database, CropBox
from .stream_filter import StreamFilter

class FrameManager:
    def __init__(self):
        self.db                 = Database()
        self.db.Load()
        self.start_feature      = HashToFeature(self.db["controls"]["GameStart"])
        self.round_feature      = HashToFeature(self.db["controls"]["GameRound"])

        self.ratio              = "16:9"
        self.start_crop         = None
        self.my_crop            = None
        self.op_crop            = None
        self.start_buffer       = None
        self.my_feature_buffer  = None
        self.op_feature_buffer  = None

        self.prev_log_time      = time.perf_counter()
        self.prev_frame_time    = self.prev_log_time
        self.frame_count        = 0
        self.min_fps            = 100000
        self.first_log          = True

        self.filters            = SimpleNamespace()
        self.filters.my_event   = StreamFilter(null_val=-1)
        self.filters.op_event   = StreamFilter(null_val=-1)
        self.filters.game_start = StreamFilter(null_val=False)
        self.filters.game_round = StreamFilter(null_val=False)

    def Resize(self, client_width, client_height):
        if client_height == 0:
            return

        # Update ratio
        ratio = client_width / client_height
        EPSILON = 0.005
        if   abs( ratio - 16 / 9 ) < EPSILON:
            self.ratio = "16:9"
        elif abs( ratio - 16 / 10) < EPSILON:
            self.ratio = "16:10"
        elif abs( ratio - 64 / 27) < EPSILON:
            self.ratio = "64:27"
        elif abs( ratio - 43 / 18) < EPSILON:
            self.ratio = "43:18"
        elif abs( ratio - 12 / 5 ) < EPSILON:
            self.ratio = "12:5"
        else:
            logging.info(f'"type": "unsupported_ratio"')
            logging.warning(f'"info": "Current resolution is {client_width}x{client_height} with {ratio=}, which is not supported now."')
            self.ratio = "16:9" # default
        
        # Update crop boxes
        pos = POS[self.ratio]

        # game start
        start_w = int(client_width * pos.start_screen_size[0])
        start_h = int(client_height * pos.start_screen_size[1])
        start_left = int(client_width * pos.start_screen_pos[0])
        start_top  = int(client_height * pos.start_screen_pos[1])
        self.start_crop = CropBox(start_left, start_top, start_left + start_w, start_top + start_h)
        self.start_buffer = np.zeros((self.start_crop.height, self.start_crop.width, 4), dtype=np.uint8)

        # event played
        event_w = int(client_width * pos.event_screen_size[0])
        event_h = int(client_height * pos.event_screen_size[1])
        feature_crop1 = CropBox(
            int(cfg.event_crop_box1[0] * event_w),
            int(cfg.event_crop_box1[1] * event_h),
            int(cfg.event_crop_box1[2] * event_w),
            int(cfg.event_crop_box1[3] * event_h),
        )
        feature_crop2 = CropBox(
            int(cfg.event_crop_box2[0] * event_w),
            int(cfg.event_crop_box2[1] * event_h),
            int(cfg.event_crop_box2[2] * event_w),
            int(cfg.event_crop_box2[3] * event_h),
        )

        my_left = int(client_width * pos.my_event_pos[0])
        my_top  = int(client_height * pos.my_event_pos[1])
        self.my_crop = CropBox(my_left, my_top, my_left + event_w, my_top + event_h)
        self.my_feature_buffer = np.zeros(
            (feature_crop1.height + feature_crop2.height, feature_crop1.width, 4), dtype=np.uint8)

        op_left = int(client_width * pos.op_event_pos[0])
        op_top  = int(client_height * pos.op_event_pos[1])
        self.op_crop = CropBox(op_left, op_top, op_left + event_w, op_top + event_h)
        self.op_feature_buffer = np.zeros(
            (feature_crop1.height + feature_crop2.height, feature_crop1.width, 4), dtype=np.uint8)

    def DetectGameStart(self, frame_buffer):
        self.start_buffer[:, :] = frame_buffer[
            self.start_crop.top  : self.start_crop.bottom, 
            self.start_crop.left : self.start_crop.right
        ]

        start_feature = ExtractFeatureFromBuffer(self.start_buffer)
        dist = FeatureDistance(start_feature, self.start_feature)
        start = (dist <= cfg.threshold)
        start = self.filters.game_start.Filter(start)

        if start:
            logging.debug(f'"info": "Game start, {dist=}"')
            logging.info(f'"type": "game_start"')
            if cfg.DEBUG_SAVE:
                start_image = Image.fromarray(self.start_buffer[:, :, 2::-1])
                start_image.save(os.path.join(cfg.debug_dir, "save", f"start_event_frame.png"))

    def DetectEvent(self, frame_buffer):
        # my event
        my_buffer = frame_buffer[
            self.my_crop.top  : self.my_crop.bottom, 
            self.my_crop.left : self.my_crop.right
        ]
        CopyEventFeatureRegion(self.my_feature_buffer, my_buffer)
        my_feature = ExtractFeatureFromBuffer(self.my_feature_buffer)
        my_id, my_dist = self.db.SearchByFeature(my_feature, ann_name="event")
        
        logging.debug(f'"info": "{my_dist=}, my event: {self.db["events"][my_id]["zh-HANS"] if my_id >= 0 else "None"}"')
        if my_dist > cfg.threshold:
            my_id = -1
        my_id = self.filters.my_event.Filter(my_id)

        if my_id >= 0:
            # logging.debug(f'"info": "my event: {self.db["events"][my_id].get("zh-HANS", "None")}, {my_dist=}"')
            logging.info(f'"type": "my_event_card", "card_id": {my_id}')

        # op event
        op_buffer = frame_buffer[
            self.op_crop.top  : self.op_crop.bottom, 
            self.op_crop.left : self.op_crop.right
        ]
        CopyEventFeatureRegion(self.op_feature_buffer, op_buffer)
        op_feature = ExtractFeatureFromBuffer(self.op_feature_buffer)
        op_id, op_dist = self.db.SearchByFeature(op_feature, ann_name="event")
        
        if op_dist > cfg.threshold:
            op_id = -1
        op_id = self.filters.op_event.Filter(op_id)

        if op_id >= 0:
            logging.debug(f'"info": "op event: {self.db["events"][op_id].get("zh-HANS", "None")}, {op_dist=}"')
            logging.info(f'"type": "op_event_card", "card_id": {op_id}')

        if cfg.DEBUG_SAVE:
            my_image = Image.fromarray(my_buffer[:, :, 2::-1])
            my_image.save(os.path.join(cfg.debug_dir, "save", f"my_image{self.frame_count}.png"))
            op_image = Image.fromarray(op_buffer[:, :, 2::-1])
            op_image.save(os.path.join(cfg.debug_dir, "save", f"op_image{self.frame_count}.png"))

    def OnFrameArrived(self, frame_buffer: np.ndarray):
        self.DetectGameStart(frame_buffer)
        self.DetectEvent(frame_buffer)

        self.frame_count += 1
        cur_time = time.perf_counter()

        if cur_time - self.prev_log_time >= cfg.LOG_INTERVAL:
            fps = self.frame_count / (cur_time - self.prev_log_time)
            logging.debug(f'"info": "FPS: {fps}"')
            if (not self.first_log) and (fps < self.min_fps):
                logging.warning(f'"info": "Min FPS = {fps}"')
                self.min_fps = fps

            self.frame_count   = 0
            self.prev_log_time = cur_time
            self.first_log     = False