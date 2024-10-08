from collections import deque, defaultdict
from .config import cfg

class SlidingWindow:
    def __init__(self, null_val, window_size):
        self.NULL_VAL    = null_val
        self.WINDOW_SIZE = window_size

        self.window      = deque(maxlen=window_size)
        self.counts      = defaultdict(int)
        self.is_strict   = defaultdict(bool)
    
    def UpdateWindow(self, value, dist):
        if len(self.window) == self.WINDOW_SIZE:
            self._PopLeft()
        self.window.append(value)

        if value != self.NULL_VAL:
            self.counts[value] += 1
            self.is_strict[value] = (dist <= cfg.strict_threshold) or (self.is_strict[value])

    def _PopLeft(self):
        value = self.window.popleft()
        if value != self.NULL_VAL:
            self.counts[value]   -= 1
            if self.counts[value] == 0:
                del self.counts[value]
                del self.is_strict[value]

    def GetMajority(self):
        if not self.counts:
            return self.NULL_VAL

        majority = max(self.counts, key=self.counts.get)
        count = self.counts[majority]
        if count > 6:
            return majority
        if (self.is_strict[majority]) and (count > 0): # maybe tune this threshold
            return majority

        return self.NULL_VAL
    
    def Reset(self):
        self.window    = deque(maxlen=self.WINDOW_SIZE)
        self.counts    = defaultdict(int)
        self.is_strict = defaultdict(bool)


class StreamFilter:
    def __init__(self, null_val, window_size=40, valid_count=15, cooldown=10):
        self.NULL_VAL    = null_val
        self.VALID_COUNT = valid_count
        self.COOLDOWN    = cooldown

        self.window      = SlidingWindow(null_val, window_size)
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