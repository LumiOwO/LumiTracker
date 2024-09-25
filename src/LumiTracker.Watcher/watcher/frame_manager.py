import time
import logging

from .config import cfg, LogDebug, LogInfo, LogWarning
from .enums import ETaskType
from .database import Database

from .states import *

class FrameManager:
    def __init__(self):
        # database
        db = Database()
        db.Load()
        self.db = db

        # tasks
        GTasks.Init(self)

        # game states for state machine
        self.states = [
            GameStateGameNotStarted(self),  
            GameStateStartingHand(self),    
            GameStateActionPhase(self),     
            GameStateNatureAndWisdom(self), 
        ]
        self.state = self.states[EGameState.GameNotStarted.value]
        self.tasks = self.state.CollectTasks()

        # control signals
        self.game_started    = False
        self.round           = 0

        # logs
        self.prev_log_time   = time.perf_counter()
        self.prev_frame_time = self.prev_log_time
        self.frame_count     = 0
        self.fps_interval    = cfg.LOG_INTERVAL / 10

    def Resize(self, client_width, client_height):
        if client_height == 0:
            return

        GTasks.OnResize(client_width, client_height)

    def OnFrameArrived(self, frame_buffer):
        # Note: No PreTick & PostTick is needed right now. 
        for task in self.tasks:
            task.SetFrameBuffer(frame_buffer)
            task.Tick()

        # State transfer
        old_state = self.state.GetState()
        if GTasks.GameStart.detected:
            new_state = EGameState.StartingHand
        else:
            new_state = self.state.Next()
        transfer = (GTasks.GameStart.detected) or (new_state != old_state)

        if transfer:
            LogDebug(info=f"[GameState] {old_state.name} ---> {new_state.name}")
            
            self.state.OnExit(to_state=new_state)
            self.state = self.states[new_state.value]
            self.state.OnEnter(from_state=old_state)

        self.tasks = self.state.CollectTasks()

        # Logs
        self.frame_count += 1
        cur_time = time.perf_counter()

        if cur_time - self.prev_log_time >= self.fps_interval:
            fps = self.frame_count / (cur_time - self.prev_log_time)
            LogInfo(
                type=f"{ETaskType.LOG_FPS.name}",
                fps=f"{fps}"
                )

            self.frame_count   = 0
            self.prev_log_time = cur_time