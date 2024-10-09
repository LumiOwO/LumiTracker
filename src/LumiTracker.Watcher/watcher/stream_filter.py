from collections import deque, defaultdict
from .config import cfg
import time

class SlidingWindow:
    class Record:
        def __init__(self):
            self.count     = 0
            self.is_strict = False
            # Note: maybe use time range later
            # self.t_first   = time.perf_counter()
            # self.t_last    = self.t_first

    def __init__(self, null_val, window_size, min_count):
        self.NULL_VAL    = null_val
        self.WINDOW_SIZE = window_size
        self.MIN_COUNT   = min_count

        self.window      = deque(maxlen=window_size)
        self.records     = defaultdict(lambda: SlidingWindow.Record())
    
    def UpdateWindow(self, value, dist):
        if len(self.window) == self.WINDOW_SIZE:
            self._PopLeft()
        self.window.append(value)

        if value != self.NULL_VAL:
            record = self.records[value]
            record.count += 1
            record.is_strict = (dist <= cfg.strict_threshold) or (record.is_strict)
            self.records[value] = record

    def _PopLeft(self):
        value = self.window.popleft()
        if value != self.NULL_VAL:
            self.records[value].count -= 1
            if self.records[value].count == 0:
                del self.records[value]

    def GetMajority(self):
        if not self.records:
            return self.NULL_VAL

        majority = max(self.records, key=lambda k: self.records[k].count)
        record = self.records[majority]
        if record.count >= self.MIN_COUNT:
            return majority
        if (record.is_strict) and (record.count >= (self.MIN_COUNT // 2)): # maybe tune this threshold
            return majority

        return self.NULL_VAL
    
    def Reset(self):
        self.window    = deque(maxlen=self.WINDOW_SIZE)
        self.records   = defaultdict(lambda: SlidingWindow.Record())


class StreamFilter:
    def __init__(self, null_val, window_size=40, valid_count=15, cooldown=10, window_min_count=10):
        self.NULL_VAL    = null_val
        self.VALID_COUNT = valid_count
        self.COOLDOWN    = cooldown

        self.window      = SlidingWindow(null_val, window_size, window_min_count)
        self.value       = null_val
        self.count       = 0
        self.signaled    = False
        self.cooldown    = 0

    def ReadSameValue(self):
        if self.value == self.NULL_VAL:
            return
        
        if self.count < self.VALID_COUNT:
            self.count += 1
        elif not self.signaled:
            self.signaled = True

    def ReadDifferentValue(self, value):
        self.value    = value
        self.count    = 0 if value == self.NULL_VAL else 1
        self.signaled = False
    
    def Cooldown(self, value):
        if value == self.value:
            self.cooldown = self.COOLDOWN
            return

        self.cooldown -= 1
        if self.cooldown == 0:
            # reset
            self.value       = self.NULL_VAL
            self.count       = 0
            self.signaled    = False
            self.window.Reset()

    def Filter(self, value, dist):
        if self.cooldown > 0:
            self.Cooldown(value)
            return self.NULL_VAL

        self.window.UpdateWindow(value, dist)
        value = self.window.GetMajority()

        prev_signaled = self.signaled

        # push
        if value == self.value:
            self.ReadSameValue()
        else:
            self.ReadDifferentValue(value)

        # read
        if (not prev_signaled) and (self.signaled):
            self.cooldown = self.COOLDOWN
            return self.value
        else:
            return self.NULL_VAL

    def PrevSignalHasLeft(self):
        return self.cooldown == 0