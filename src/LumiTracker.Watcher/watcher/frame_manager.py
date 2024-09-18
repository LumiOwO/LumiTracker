import time
import logging

from .config import cfg, LogDebug, LogInfo, LogWarning
from .enums import EGameState, ETaskType
from .regions import GetRatioType
from .database import Database

from .tasks import *
from .states import *

class FrameManager:
    def __init__(self):
        # database
        db = Database()
        db.Load()
        self.db = db

        # tasks
        self.game_start_task    = GameStartTask(self)
        self.my_played_task     = CardPlayedTask(self, is_op=False)
        self.op_played_task     = CardPlayedTask(self, is_op=True)
        self.game_over_task     = GameOverTask(self)
        self.round_task         = RoundTask(self)
        self.card_flow_task     = CardFlowTask(self)
        self.starting_hand_task = StartingHandTask(self)
        self.all_tasks = [
            self.game_start_task,   
            self.my_played_task,    
            self.op_played_task,    
            self.game_over_task,    
            self.round_task,        
            self.card_flow_task,    
            self.starting_hand_task,
        ]

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

    def Resize(self, client_width, client_height):
        if client_height == 0:
            return

        # Update ratio
        ratio_type = GetRatioType(client_width, client_height)
        
        for task in self.all_tasks:
            task.OnResize(client_width, client_height, ratio_type)

    def OnFrameArrived(self, frame_buffer):
        # Note: No PreTick & PostTick is needed right now. 
        for task in self.tasks:
            task.SetFrameBuffer(frame_buffer)
            task.Tick()

        # State transfer
        old_state = self.state.GetState()
        if self.game_start_task.detected:
            new_state = EGameState.StartingHand
        else:
            new_state = self.state.Next()
        transfer = (self.game_start_task.detected) or (new_state != old_state)

        if transfer:
            LogDebug(info=f"GameState: {old_state.name} ---> {new_state.name}")
            for task in self.tasks:
                task.OnStateTransfer(old_state, new_state)
            self.state = self.states[new_state.value]
            self.tasks = self.state.CollectTasks()

        # Logs
        self.frame_count += 1
        cur_time = time.perf_counter()

        if cur_time - self.prev_log_time >= cfg.LOG_INTERVAL:
            fps = self.frame_count / (cur_time - self.prev_log_time)
            LogInfo(
                type=f"{ETaskType.LOG_FPS.name}",
                fps=f"{fps}"
                )

            self.frame_count   = 0
            self.prev_log_time = cur_time