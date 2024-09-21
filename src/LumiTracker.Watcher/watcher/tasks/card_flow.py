from .base import TaskBase

from ..enums import ETaskType, ERegionType, EAnnType
from ..config import cfg, override, LogDebug, LogInfo, LogError
from ..regions import REGIONS
from ..feature import CropBox, ActionCardHandler, CardName
from ..feature import ExtractFeature_Digit_Binalized
from ..stream_filter import StreamFilter

from collections import deque, defaultdict
import cv2
import time

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
        _, thresh = cv2.threshold(gray, 65, 255, cv2.THRESH_BINARY_INV)

        # Find contours
        contours, _ = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        # Filter and draw bounding boxes around the detected cards
        FILTER_H = self.center_crop.height * 0.6
        FILTER_W = FILTER_H * 1.5
        filtered_bboxes = []
        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)
            if h < FILTER_H or w >= FILTER_W:
                continue
            filtered_bboxes.append(CropBox(x, y, x + w, y + h))
        if not filtered_bboxes:
            return [], []
        
        # sort the boxes
        if len(filtered_bboxes) > 1:
            filtered_bboxes.sort(key=lambda box: box.left)

        # detect digits
        detected = []
        for bbox in filtered_bboxes:
            # digit should crop from binary image
            binary = thresh[bbox.top:bbox.bottom, bbox.left:bbox.right]
            feature = ExtractFeature_Digit_Binalized(binary)
            results, dists = self.db.SearchByFeature(feature, EAnnType.DIGITS)
            digit = results[0] % 10 if dists[0] <= cfg.threshold else -1
            # Note: Currently, no card costs larger than 5
            # Maybe there will be debuffs that add costs to cards in the future
            if digit >= 0 and digit <= 6:
                # LogDebug(digit=digit, results=results[:3], dists=dists[:3])
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
    def __init__(self, frame_manager, need_dump=True):
        super().__init__(frame_manager)
        self.my_deck_crop  = None
        self.op_deck_crop  = None
        self.need_dump     = need_dump
        self.Reset()
    
    @override
    def Reset(self):
        self.filter        = StreamFilter(null_val=0)
        self.card_recorder = {}
        
        self.signaled_num_cards = 0
        self.signaled_timestamp = 0 
        self.my_deck_queue = deque()
        self.op_deck_queue = deque()

    @override
    def OnResize(self, client_width, client_height, ratio_type):
        super().OnResize(client_width, client_height, ratio_type)

        box    = REGIONS[ratio_type][ERegionType.MY_DECK] 
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])
        self.my_deck_crop = CropBox(left, top, left + width, top + height)

        box    = REGIONS[ratio_type][ERegionType.OP_DECK] 
        left   = round(client_width  * box[0])
        top    = round(client_height * box[1])
        width  = round(client_width  * box[2])
        height = round(client_height * box[3])
        self.op_deck_crop = CropBox(left, top, left + width, top + height)

    @override
    def Tick(self):
        self._DetectCards()
        self._DetectDeck(is_op=False)
        self._DetectDeck(is_op=True)

        if self.need_dump:
            self._DumpDetected()

    def _DetectCards(self):
        # center
        bboxes, costs = self.DetectCenterCards()
        num_bboxes = len(bboxes)

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
            card_id, dist, dists = card_handler.Update(self.frame_buffer, self.db)

            if card_id >= 0:
                recorder[i][card_id] += 1
            else:
                invalid_count += 1

            if cfg.DEBUG:
                debug_bboxes.append(bbox)
                detected.append((card_id, dists))

        if cfg.DEBUG:
            self.bboxes = debug_bboxes
            # if detected:
            #     LogDebug(center=[(CardName(card_id, self.db), dists) for card_id, dists in detected])

        # stream filtering
        num_cards = num_bboxes if invalid_count != num_bboxes else 0
        num_cards = self.filter.Filter(num_cards, dist=0)
        if num_cards > 0:
            self.signaled_num_cards = num_cards
            self.signaled_timestamp = time.perf_counter()

    def _DetectDeck(self, is_op):
        deck_crop = self.op_deck_crop if is_op else self.my_deck_crop
        deck_buffer = self.frame_buffer[
            deck_crop.top  : deck_crop.bottom, 
            deck_crop.left : deck_crop.right
        ]

        # Convert to grayscale
        gray = cv2.cvtColor(deck_buffer, cv2.COLOR_BGR2GRAY)

        # Apply Canny edge detection
        edges = cv2.Canny(gray, 50, 150)

        # Find contours
        contours, _ = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

        FILTER_H     = deck_crop.height * 0.5
        FILTER_W_MIN = deck_crop.width  * 0.4
        FILTER_W_MAX = deck_crop.width  * 0.7
        bboxes = []
        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)
            if h < FILTER_H or w < FILTER_W_MIN or w > FILTER_W_MAX:
                continue
            ratio = w / h
            if ratio < 1.5:
                continue
            bboxes.append(CropBox(x, y, x + w, y + h))

        if cfg.DEBUG:
            if not is_op:
                self.my_deck_buffer = deck_buffer
                self.my_deck_edges  = edges
                self.my_deck_bboxes = bboxes
            else:
                self.op_deck_buffer = deck_buffer
                self.op_deck_edges  = edges
                self.op_deck_bboxes = bboxes
        
        if bboxes:
            dst_queue = self.op_deck_queue if is_op else self.my_deck_queue
            # LogDebug(drawn_detected=True, is_op=is_op)
            dst_queue.append(time.perf_counter())

    def _DumpDetected(self):
        # dump if signaled
        if self.signaled_num_cards == 0:
            return
        WAIT_TIME = 1.2  # seconds
        if time.perf_counter() - self.signaled_timestamp < WAIT_TIME:
            return
        
        task_type = ETaskType.NONE
        while len(self.my_deck_queue) > 0:
            timestamp = self.my_deck_queue[0]
            if timestamp > self.signaled_timestamp + WAIT_TIME:
                break
            self.my_deck_queue.popleft()
            if task_type != ETaskType.NONE:
                continue

            if timestamp < self.signaled_timestamp - WAIT_TIME:
                continue
            task_type = ETaskType.MY_DRAWN if timestamp < self.signaled_timestamp else ETaskType.MY_CREATE_DECK
        
        while len(self.op_deck_queue) > 0:
            timestamp = self.op_deck_queue[0]
            if timestamp > self.signaled_timestamp + WAIT_TIME:
                break
            self.op_deck_queue.popleft()
            if task_type != ETaskType.NONE:
                continue

            if timestamp < self.signaled_timestamp:
                continue
            task_type = ETaskType.OP_CREATE_DECK

        if task_type != ETaskType.NONE:
            num_cards = self.signaled_num_cards
            cards, valid = self.GetRecordedCards(num_cards)
            if valid:
                LogInfo(type=task_type.name, 
                        cards=cards,
                        names=[CardName(card, self.db) for card in cards])
            else:
                LogError(info=f"{task_type.name}: Some cards are not detected.", 
                        cards=cards,
                        names=[CardName(card, self.db) for card in cards])

        # Reset
        # Assume that within the time range of 1 operation, there will be only 1 count detected.
        # Therefore, when the dump ends, all recorded cards should be reset.
        self.card_recorder = {}
        self.signaled_num_cards = 0
        self.signaled_timestamp = 0 

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
