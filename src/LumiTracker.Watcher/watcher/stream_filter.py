from collections import deque, defaultdict

class SlidingWindow:
    def __init__(self, null_val, window_size):
        self.NULL_VAL    = null_val
        self.WINDOW_SIZE = window_size

        self.window      = deque(maxlen=window_size)
        self.counts      = defaultdict(int)
    
    def UpdateWindow(self, value):
        if len(self.window) == self.WINDOW_SIZE:
            self._PopLeft()
        self.window.append(value)

        if value != self.NULL_VAL:
            self.counts[value] += 1

    def _PopLeft(self):
        removed_value = self.window.popleft()
        if removed_value != self.NULL_VAL:
            self.counts[removed_value] -= 1
            if self.counts[removed_value] == 0:
                del self.counts[removed_value]

    def GetMajority(self):
        if self.counts:
            return max(self.counts, key=self.counts.get)
        else:
            return self.NULL_VAL


class StreamFilter:
    def __init__(self, null_val, window_size=30, valid_count=10):
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

    def Filter(self, value):
        self.window.UpdateWindow(value)
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