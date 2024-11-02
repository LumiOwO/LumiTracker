from .base import TaskBase

from ..enums import EGameEvent, ERegionType, EAnnType
from ..config import cfg, override, LogDebug, LogInfo, LogError
from ..regions import REGIONS
from ..feature import CropBox, ActionCardHandler, CardName
from ..feature import ExtractFeature_Digit_Binalized
from ..stream_filter import StreamFilter

from collections import deque, defaultdict
import cv2
import time
import numpy as np

class CenterCropTask(TaskBase):
    DigitOffsets = [
        (  0.0      ,  0.0      ), # 0
        ( -0.083333 , -0.005464 ), # 1
        (  0.0      ,  0.0      ), # 2
        ( -0.007576 , -0.005495 ), # 3
        ( -0.006536 , -0.005464 ), # 4
        ( -0.007519 ,  0.0      ), # 5
        (  0.0      , -0.010753 ), # 6
        ( -0.007194 ,  0.0      ), # 7
        ( -0.007042 , -0.005464 ), # 8
        ( -0.007299 , -0.016304 ), # 9
    ]

    def __init__(self, frame_manager):
        super().__init__(frame_manager)
        self.center_crop   = None
        self.flow_anchor   = None
        self.KERNEL        = None

    @override
    def OnResize(self, client_width, client_height, ratio_type):
        box    = REGIONS[ratio_type][ERegionType.CENTER]        # left, top, width, height
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])
        self.center_crop = CropBox(left, top, left + width, top + height)

        box    = REGIONS[ratio_type][ERegionType.FLOW_ANCHOR] 
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])
        self.flow_anchor = CropBox(left, top, left + width, top + height)

        size = (5, 3) if client_height < 800 else (7, 5)
        self.KERNEL = np.ones(size, np.uint8)

    def DetectCenterCards(self):
        center_buffer = self.frame_buffer[
            self.center_crop.top  : self.center_crop.bottom, 
            self.center_crop.left : self.center_crop.right
        ]
        if cfg.DEBUG:
            self.center_buffer = center_buffer

        # Convert to grayscale
        gray = cv2.cvtColor(center_buffer, cv2.COLOR_BGR2GRAY)

        # Thresholding
        _, thresh = cv2.threshold(gray, 75, 255, cv2.THRESH_BINARY_INV)

        # Close unconnected border
        thresh = cv2.morphologyEx(thresh, cv2.MORPH_CLOSE, self.KERNEL, iterations=1)

        # Find contours
        contours, _ = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        # Filter, get the possible bboxes of digits
        FILTER_H = self.center_crop.height * 0.5
        FILTER_W = FILTER_H * 2.0
        filtered_bboxes = []
        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)
            if h < FILTER_H or w >= FILTER_W or w <= 1:
                continue
            filtered_bboxes.append(CropBox(x, y, x + w, y + h))
        if not filtered_bboxes:
            return [], []
        
        # sort the bboxes
        if len(filtered_bboxes) > 1:
            filtered_bboxes.sort(key=lambda box: box.left)
        
        # Merge intersect bboxes
        def CanMerge(a, b):
            if a.right < b.left or a.bottom < b.top or a.top > b.bottom:
                return False
            merged_width = max(a.right, b.right) - min(a.left, b.left)
            if merged_width >= FILTER_W:
                return False
            return True

        merged_bboxes = []
        current_bbox = filtered_bboxes[0]
        for bbox in filtered_bboxes[1:]:
            if CanMerge(current_bbox, bbox):
                current_bbox.Merge(bbox)
            else:
                merged_bboxes.append(current_bbox)
                current_bbox = bbox
        merged_bboxes.append(current_bbox)

        # detect digits
        detected = []
        for bbox in merged_bboxes:
            content = gray[bbox.top:bbox.bottom, bbox.left:bbox.right]
            # Find the threshold using the cdf of histogram
            # this is actually a form of local histogram equalization
            hist = np.zeros((256,), dtype=np.float32)
            cv2.calcHist([content], [0], None, [256], [0, 256], hist=hist)
            hist_sum = hist.sum()
            if hist_sum == 0:
                continue
            hist /= hist_sum
            cdf = hist.cumsum()
            thres = min(np.searchsorted(cdf, 0.20), 255)
            # LogDebug(thres=f"{thres}")

            # Thresholding
            _, binary = cv2.threshold(content, thres, 255, cv2.THRESH_BINARY_INV)

            # Get main content, crop margins
            white_y, white_x = np.where(binary == 255)
            if white_y.size == 0 or white_x.size == 0:
                continue
            left    = np.min(white_x)
            right   = np.max(white_x) + 1
            top     = np.min(white_y)
            bottom  = np.max(white_y) + 1
            if bottom - top < FILTER_H or right - left >= FILTER_W:
                continue
            binary  = binary[top:bottom, left:right]
            # cv2.imshow("image", binary)
            # cv2.waitKey(0)

            # Extract feature
            feature = ExtractFeature_Digit_Binalized(binary)
            results, dists = self.db.SearchByFeature(feature, EAnnType.DIGITS)
            # 10 ~ 19 is for card cost's digit, which is outlined
            digit = results[0]
            if digit < 10 or digit > 19 or dists[0] > cfg.strict_threshold:
                digit = -1
            else:
                digit -= 10
                # LogDebug(digit=digit, results=results[:3], dists=dists[:3])

            # Note: Currently, no card costs larger than 5
            # Maybe there will be debuffs that add costs to cards in the future
            if digit >= 0 and digit <= 6:
                # Remove margin from bbox 
                # Order is important here! bbox.width & bbox.height will be changed
                # LogDebug(margin=f"{(left, top, bbox.width - right, bbox.height - bottom)}")
                bbox.right  -= max(bbox.width  - right, 0)
                bbox.bottom -= max(bbox.height - bottom, 0)
                bbox.left   += max(left, 0)
                bbox.top    += max(top, 0)
                detected.append((digit, bbox))

        bboxes = []
        costs  = []
        valid = self.ValidateDetectedBBoxes(detected, bboxes, costs)

        if cfg.DEBUG:
            # if costs:
            #     LogDebug(info="[DetectCenterCards]", costs=costs)
            self.thresh = thresh
            self.bboxes = bboxes
            self.gray = gray

        if valid:
            return bboxes, costs
        else:
            return [], []

    def ValidateDetectedBBoxes(self, detected, bboxes, costs):
        """
            Find card bboxes anchored by the digit's bbox.
            Check if bboxes are valid by these conditions:
            1. Must be center-aligned
            2. Distance between each must be close to the same
        """
        if not detected:
            return False

        frame_width, frame_height = self.frame_buffer.shape[1], self.frame_buffer.shape[0]
        frame_buffer_box = CropBox(0, 0, frame_width, frame_height)

        average_x = 0
        prev_digit_x = -1
        ref_dist = -1
        for cost, bbox in detected:
            offset = CenterCropTask.DigitOffsets[cost]
            dx = round(offset[0] * bbox.width)
            digit_x = bbox.center_x - dx

            bbox.left   = digit_x - self.flow_anchor.left + self.center_crop.left
            bbox.top    = self.flow_anchor.top
            bbox.right  = bbox.left + self.flow_anchor.width
            bbox.bottom = bbox.top  + self.flow_anchor.height

            # Note: Filter out the cards that intersect with the screen boundary.
            # These cards can still be valid if the feature buffer region is within the screen.
            # Currently, just skip them instead of regarding as invalid detection.
            if not bbox.Inside(frame_buffer_box):
                continue
            
            dist = -1 if (prev_digit_x == -1) else (digit_x - prev_digit_x)
            if ref_dist != -1 and abs(dist - ref_dist) > 0.1 * ref_dist:
                return False

            bboxes.append(bbox)
            costs.append(cost)
            if ref_dist == -1 and dist != -1:
                ref_dist = dist
            average_x += bbox.center_x / frame_width
            prev_digit_x = digit_x

        if not bboxes:
            return False

        average_x /= len(bboxes)
        # LogDebug(average_x=average_x, count=len(bboxes))
        if average_x < 0.48 or average_x > 0.52:
            return False
        
        return True

class CardFlowTask(CenterCropTask):
    WAIT_TIME        = 1.2  # seconds
    QUEUE_TIME_RANGE = 8.0  # seconds

    class SignalInfo:
        def __init__(self, num_cards, t_begin, t_end, cards, valid):
            self.num_cards = num_cards
            self.t_begin   = t_begin
            self.t_end     = t_end
            self.cards     = cards
            self.valid     = valid

    def __init__(self, frame_manager, need_deck=True, need_dump=True):
        super().__init__(frame_manager)
        self.my_deck_crop  = None
        self.op_deck_crop  = None
        self.deck_dst_size = None
        self.need_deck     = need_deck
        self.need_dump     = need_dump
        self.Reset()
    
    @override
    def Reset(self):
        self.filter        = StreamFilter(null_val=0)
        self.card_recorder = {}
        
        self.signaled_num_cards = 0
        self.signaled_timestamp = 0
        self.signal_queue  = deque()
        self.my_deck_queue = deque()
        self.op_deck_queue = deque()

        self.my_card_back_scaled = np.zeros((0,))
        self.op_card_back_scaled = np.zeros((0,))
        self.invalid_card_back_notified = False

    @override
    def OnResize(self, client_width, client_height, ratio_type):
        super().OnResize(client_width, client_height, ratio_type)

        box    = REGIONS[ratio_type][ERegionType.DECK] 
        box_left, box_top, box_width, box_height = box
        left   = round(client_width  * box_left)
        width  = round(client_width  * box_width)
        height = round(client_height * box_height)
        # my deck
        top    = round(client_height * box_top)
        self.my_deck_crop = CropBox(left, top, left + width, top + height)
        # op deck is at the mirror position
        box_top = 1.0 - (box_top + box_height)
        top    = round(client_height * box_top)
        self.op_deck_crop = CropBox(left, top, left + width, top + height)

        # Dst deck size when resize deck buffer
        dst_height = 100
        aspect_ratio = width / height
        dst_width = int(dst_height * aspect_ratio)
        self.deck_dst_size = (dst_width, dst_height)

        self.scale = dst_height / height
        LogDebug(DeckScale=self.scale)
        self.my_card_back_scaled = np.zeros((0,))
        self.op_card_back_scaled = np.zeros((0,))
        self.invalid_card_back_notified = False

    @override
    def Tick(self):
        self._DetectCards()

        if self.need_deck:
            self._DetectDeck(is_op=False)
            self._DetectDeck(is_op=True)

        if self.need_dump:
            while len(self.signal_queue) > 0:
                dumped = self._DumpDetected(self.signal_queue[0])
                if dumped:
                    self.signal_queue.popleft()
                else:
                    break

    def _DetectCards(self):
        # center
        bboxes, costs = self.DetectCenterCards()
        num_bboxes = len(bboxes)
        # LogDebug(num_bboxes=num_bboxes)

        if cfg.DEBUG:
            detected = []
            debug_bboxes = []
        recorder = self.card_recorder.get(num_bboxes, [])
        if not recorder:
            recorder = [defaultdict(int) for _ in range(num_bboxes)]
            self.card_recorder[num_bboxes] = recorder

        invalid_count = 0
        for i, bbox in enumerate(bboxes):
            card_handler = ActionCardHandler()
            card_handler.OnResize(bbox)
            card_id, dist, dists = card_handler.Update(self.frame_buffer, self.db, threshold=40, check_next_dist=False)

            if card_id >= 0:
                recorder[i][card_id] += 1
            else:
                invalid_count += 1

            if cfg.DEBUG:
                debug_bboxes.append(bbox)
                detected.append((card_id, dists))

        num_cards = num_bboxes if invalid_count != num_bboxes else 0

        if cfg.DEBUG:
            self.bboxes = debug_bboxes
            # if detected:
            #     LogDebug(center=[(CardName(card_id, self.db), dists) for card_id, dists in detected])

        # stream filtering
        num_cards = self.filter.Filter(num_cards, dist=0)
        # LogDebug(num_cards=num_cards)
        if num_cards > 0:
            self.signaled_num_cards = num_cards
            self.signaled_timestamp = time.perf_counter()
            LogDebug(info="[CardFlow]", t_start=self.signaled_timestamp)

        if (self.signaled_num_cards != 0) and self.filter.PrevSignalHasLeft():
            num_cards = self.signaled_num_cards
            cards, valid = self.GetRecordedCards(num_cards)
            info = CardFlowTask.SignalInfo(
                num_cards=num_cards, 
                t_begin=self.signaled_timestamp, 
                t_end=time.perf_counter(),
                cards=cards,
                valid=valid,
                )
            self.signal_queue.append(info)
            self.card_recorder = {}
            self.signaled_num_cards = 0
            LogDebug(info="[CardFlow]", t_end=info.t_end)

    def _IsCardBackValid(self, is_op):
        card_back_scaled = self.op_card_back_scaled if is_op else self.my_card_back_scaled
        if card_back_scaled.size > 0:
            return True

        card_back = self.fm.op_card_back if is_op else self.fm.my_card_back
        if card_back.size == 0:
            if (not self.invalid_card_back_notified):
                LogError(info="[CardFlow] _DetectDeck() called, but card_back is empty!", is_op=is_op)
                self.invalid_card_back_notified = True
            return False

        card_back_scaled = cv2.resize(card_back, None, fx=self.scale, fy=self.scale, interpolation=cv2.INTER_AREA)
        if is_op:
            self.op_card_back_scaled = card_back_scaled
        else:
            self.my_card_back_scaled = card_back_scaled
        return True

    def _DetectDeck(self, is_op):
        if not self._IsCardBackValid(is_op):
            return

        card_back = self.op_card_back_scaled if is_op else self.my_card_back_scaled
        deck_crop = self.op_deck_crop if is_op else self.my_deck_crop
        deck_buffer = self.frame_buffer[
            deck_crop.top  : deck_crop.bottom, 
            deck_crop.left : deck_crop.right
        ]

        # Convert to grayscale
        gray = cv2.cvtColor(deck_buffer, cv2.COLOR_BGR2GRAY)
        # Resize to a relative small size for performance
        gray = cv2.resize(gray, self.deck_dst_size, interpolation=cv2.INTER_AREA)
        # Template matching
        result = cv2.matchTemplate(gray, card_back, cv2.TM_CCOEFF_NORMED)

        # Set threshold
        threshold = 0.8
        found = np.any(result >= threshold)

        if cfg.DEBUG:
            bboxes = []
            if not is_op:
                self.my_deck_buffer = deck_buffer
                self.my_deck_bboxes = bboxes
            else:
                self.op_deck_buffer = deck_buffer
                self.op_deck_bboxes = bboxes

        if found:
            dst_queue = self.op_deck_queue if is_op else self.my_deck_queue
            LogDebug(drawn_detected=True, is_op=is_op)
            timestamp = time.perf_counter()
            while len(dst_queue) > 0 and (timestamp - dst_queue[0] > self.QUEUE_TIME_RANGE):
                dst_queue.popleft()
            dst_queue.append(timestamp)

    '''
        Return (bool): whether this signal is dumped
    '''
    def _DumpDetected(self, info):
        if info.num_cards == 0:
            return True
        # LogDebug(signal_time=t_end)

        # Early return if my deck operation detected
        event_type = EGameEvent.NONE
        idx = 0
        while idx < len(self.my_deck_queue):
            # Get my deck timestamps and remove outdated ones.
            timestamp = self.my_deck_queue[idx]
            if timestamp < info.t_end:
                self.my_deck_queue.popleft()
            else:
                idx += 1
            # LogDebug(my=timestamp)

            if timestamp > info.t_end + self.WAIT_TIME:
                break
            if event_type != EGameEvent.NONE:
                continue
            if timestamp >= info.t_begin and timestamp <= info.t_end:
                continue
            if timestamp < info.t_begin - self.WAIT_TIME:
                continue
            event_type = EGameEvent.MY_DRAWN if timestamp < info.t_begin else EGameEvent.MY_CREATE_DECK

        if event_type != EGameEvent.NONE:
            self._DumpEventType(event_type, info)
            return True

        # op deck can be easily mis-detected, so we need to wait until time up
        if (time.perf_counter() - info.t_end < self.WAIT_TIME):
            return False

        idx = 0
        while idx < len(self.op_deck_queue):
            # Get op deck timestamps and remove outdated ones.
            timestamp = self.op_deck_queue[idx]
            if timestamp < info.t_end:
                self.op_deck_queue.popleft()
            else:
                idx += 1

            if timestamp > info.t_end + self.WAIT_TIME:
                break
            if event_type != EGameEvent.NONE:
                continue
            if timestamp < info.t_end:
                continue
            event_type = EGameEvent.OP_CREATE_DECK

        self._DumpEventType(event_type, info)
        return True

    def _DumpEventType(self, event_type, info):
        if event_type != EGameEvent.NONE:
            cards = info.cards
            if not info.valid:
                LogError(info=f"{event_type.name}: Some cards are not detected.")
            LogInfo(type=event_type.name, 
                    cards=cards,
                    names=[CardName(card, self.db) for card in cards])

    def GetRecordedCards(self, num_cards):
        recorder = self.card_recorder.get(num_cards, [])
        if not recorder:
            return [], False

        valid = True
        cards = [-1] * num_cards
        for i, d in enumerate(recorder):
            card = max(d, key=d.get) if d else -1
            if card == -1:
                valid = False
            cards[i] = card
        return cards, valid
