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


class StreamFilter:
    def __init__(self, null_val, window_size=40, valid_count=15):
        self.NULL_VAL    = null_val
        self.VALID_COUNT = valid_count

        self.window      = SlidingWindow(null_val, window_size)
        self.value       = null_val
        self.count       = 0
        self.signaled    = False

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

    def Filter(self, value, dist):
        self.window.UpdateWindow(value, dist)
        value = self.window.GetMajority()

        prev_signaled = self.signaled

        # push
        if value == self.value:
            self.ReadSameValue()
        else:
            self.ReadDifferentValue(value)

        # logging.debug(self.count, self.value)

        # read
        if (not prev_signaled) and (self.signaled):
            return self.value
        else:
            return self.NULL_VAL