import time
import logging

from .config import cfg, LogDebug, LogWarning
from .regions import GetRatioType
from .database import Database
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
        self.reset_tasks     = False

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
        ratio_type = GetRatioType(client_width, client_height)
        
        for task in self.tasks:
            task.OnResize(client_width, client_height, ratio_type)

    def OnFrameArrived(self, frame_buffer):
        # always tick game start task
        self.game_start_task.PreTick(self, frame_buffer)
        if self.game_start_task.valid:
            self.game_start_task.Tick(self)
        
        # tick others
        for task in self.tasks[1:]:
            if self.reset_tasks:
                task.Reset()
            task.PreTick(self, frame_buffer)

        for task in self.tasks[1:]:
            if task.valid:
                task.Tick(self)

        self.frame_count += 1
        cur_time = time.perf_counter()

        if cur_time - self.prev_log_time >= cfg.LOG_INTERVAL:
            fps = self.frame_count / (cur_time - self.prev_log_time)
            LogDebug(info=f"FPS: {fps}")
            if (not self.first_log) and (self.min_fps - fps >= 5):
                LogWarning(info=f"Min FPS = {fps}")
                self.min_fps = fps

            self.frame_count   = 0
            self.prev_log_time = cur_time
            self.first_log     = False