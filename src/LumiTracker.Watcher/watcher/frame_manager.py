import time
import logging

from .config import cfg, LogDebug, LogWarning
from .enums import EGameState
from .regions import GetRatioType
from .database import Database
from .tasks import GameStartTask, CardPlayedTask, GameOverTask, RoundTask, CardFlowTask, StartingHandTask

class GameState():
    def __init__(self, frame_manager):
        self.fm = frame_manager
    
    def GetState(self):
        raise NotImplementedError()
    
    def CollectTasks(self):
        raise NotImplementedError()
    
    def Next(self):
        raise NotImplementedError()

class GameStateGameNotStarted(GameState):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
    
    def GetState(self):
        return EGameState.GameNotStarted
    
    def CollectTasks(self):
        return [self.fm.game_start_task]
    
    def Next(self):
        if self.fm.game_started:
            state = EGameState.StartingHand
        else:
            state = self.GetState()
        return state

class GameStateStartingHand(GameState):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
    
    def GetState(self):
        return EGameState.StartingHand
    
    def CollectTasks(self):
        return [
            self.fm.game_start_task, 
            self.fm.game_over_task, 
            self.fm.starting_hand_task,
            self.fm.round_task,
            ]
    
    def Next(self):
        if not self.fm.game_started:
            state = EGameState.GameNotStarted
        elif self.fm.round > 0:
            state = EGameState.ActionPhase
        else:
            state = self.GetState()
        return state

class GameStateActionPhase(GameState):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
    
    def GetState(self):
        return EGameState.ActionPhase
    
    def CollectTasks(self):
        return [
            self.fm.game_start_task, 
            self.fm.game_over_task, 
            self.fm.my_played_task, 
            self.fm.op_played_task, 
            self.fm.round_task, 
            self.fm.card_flow_task
            ]

    def Next(self):
        # TODO: add NatureAndWisdom
        if not self.fm.game_started:
            state = EGameState.GameNotStarted
        else:
            state = self.GetState()
        return state

class GameStateNatureAndWisdom(GameState):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
    
    def GetState(self):
        return EGameState.NatureAndWisdom
    
    def CollectTasks(self):
        return [
            self.fm.game_start_task,
            self.fm.game_over_task,
            ]
    
    def Next(self):
        if not self.fm.game_started:
            state = EGameState.GameNotStarted
        else:
            state = self.GetState()
        return state


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
        self.min_fps         = 100000
        self.first_log       = True

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
            LogDebug(info=f"FPS: {fps}")
            if (not self.first_log) and (self.min_fps - fps >= 5):
                LogWarning(info=f"Min FPS = {fps}")
                self.min_fps = fps

            self.frame_count   = 0
            self.prev_log_time = cur_time
            self.first_log     = False