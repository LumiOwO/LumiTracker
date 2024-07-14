import time
import logging

from .config import cfg
from .database import Database
from .enums import ETaskType, ERatioType
from .tasks import GameStartTask, CardPlayedTask, GameOverTask, RoundTask, CardFlowTask

class FrameManager:
    def __init__(self):
        # database
        db = Database()
        db.Load()
        self.db = db

        # tasks
        self.game_start_task = GameStartTask(db)
        self.my_played_task  = CardPlayedTask(db, is_op=False)
        self.op_played_task  = CardPlayedTask(db, is_op=True)
        self.game_over_task  = GameOverTask(db)
        self.round_task      = RoundTask(db)
        self.card_flow_task  = CardFlowTask(db)
        self.tasks = [self.game_start_task, self.my_played_task, self.op_played_task, self.game_over_task, self.round_task, self.card_flow_task]

        # controls
        self.game_started    = False
        self.round           = 0

        # logs
        self.prev_log_time   = time.perf_counter()
        self.prev_frame_time = self.prev_log_time
        self.frame_count     = 0
        self.min_fps         = 100000
        self.first_log       = True

    def Resize(self, client_width, client_height):
        if client_height == 0:
            return

        # Update ratio
        ratio = client_width / client_height
        EPSILON = 0.005
        if   abs( ratio - 16 / 9 ) < EPSILON:
            ratio_type = ERatioType.E16_9
        elif abs( ratio - 16 / 10) < EPSILON:
            ratio_type = ERatioType.E16_10
        elif abs( ratio - 64 / 27) < EPSILON:
            ratio_type = ERatioType.E64_27
        elif abs( ratio - 43 / 18) < EPSILON:
            ratio_type = ERatioType.E43_18
        elif abs( ratio - 12 / 5 ) < EPSILON:
            ratio_type = ERatioType.E12_5
        else:
            logging.info(f'"type": "{ETaskType.UNSUPPORTED_RATIO}", "client_width": {client_width}, "client_height": {client_height}')
            logging.warning(f'"info": "Current resolution is {client_width}x{client_height} with {ratio=}, which is not supported now."')
            ratio_type = ERatioType.E16_9 # default
        
        for task in self.tasks:
            task.OnResize(client_width, client_height, ratio_type)

    def OnFrameArrived(self, frame_buffer):
        for task in self.tasks:
            task.PreTick(self, frame_buffer)

        for task in self.tasks:
            if task.valid:
                task.Tick(self)

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