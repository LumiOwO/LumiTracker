from .base import TaskBase

from ..enums import ETaskType, ERegionType, EAnnType
from ..config import cfg, LogDebug, LogInfo, LogError
from ..regions import REGIONS
from ..feature import CropBox, ActionCardHandler, CardName, CardCost
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
        costs = []
        for bbox in filtered_bboxes:
            # digit should crop from binary image
            binary = thresh[bbox.top:bbox.bottom, bbox.left:bbox.right]
            feature = ExtractFeature_Digit_Binalized(binary)
            results, dists = self.db.SearchByFeature(feature, EAnnType.DIGITS)
            digit = results[0] % 10 if dists[0] <= cfg.threshold else -1
            # Note: Currently, no card costs larger than 5
            if digit >= 0 and digit <= 5:
                # LogDebug(digit=digit, results=results[:3], dists=dists[:3])
                detected.append(bbox)
                costs.append(digit)
        
        # find card bbox anchored by the digit's bbox
        bboxes = []
        for i in range(len(costs)):
            cost = costs[i]
            bbox = detected[i]

            offset = CenterCropTask.DigitOffsets[cost]
            dx = round(offset[0] * bbox.width)
            center_x = bbox.left + bbox.width // 2 - dx

            bbox.left   = center_x  - self.flow_anchor.left + self.center_crop.left
            bbox.top    = self.flow_anchor.top
            bbox.right  = bbox.left + self.flow_anchor.width
            bbox.bottom = bbox.top  + self.flow_anchor.height
            bboxes.append(bbox)

        if cfg.DEBUG:
            # if costs:
            #     LogDebug(info="[DetectCenterCards]", costs=costs)
            self.thresh = thresh
            self.bboxes = bboxes
            self.gray = gray

        return bboxes, costs

class CardFlowTask(CenterCropTask):
    def __init__(self, frame_manager):
        super().__init__(frame_manager)
        self.my_deck_crop  = None
        self.op_deck_crop  = None
        self.Reset()
    
    def Reset(self):
        # round 1 ~ n
        self.MAX_NUM_CARDS = 10
        self.filter        = StreamFilter(null_val=0)
        self.card_recorder = [[]]
        for i in range(self.MAX_NUM_CARDS):
            self.card_recorder.append([defaultdict(int) for _ in range(i + 1)])
        
        self.signaled_num_cards = 0
        self.signaled_timestamp = 0 
        self.my_deck_queue = deque()
        self.op_deck_queue = deque()

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

    def Tick(self):
        self._DetectCards()
        self._DumpDetected()

    def _DetectCards(self):
        # center
        bboxes, costs = self.DetectCenterCards()
        num_bboxes = len(bboxes)

        if cfg.DEBUG:
            detected = []
            debug_bboxes = []
        recorder = self.card_recorder[num_bboxes]
        invalid_count = 0
        for i, bbox in enumerate(bboxes):
            card_handler = ActionCardHandler()
            card_handler.OnResize(bbox)
            card_id, dist, dists = card_handler.Update(self.frame_buffer, self.db)
            if card_id >= 0 and costs[i] != CardCost(card_id, self.db):
                card_id = -1

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

        timestamp = time.perf_counter()
        # my deck
        my_drawn_detected = self._DetectDeck(is_op=False)
        if my_drawn_detected:
            # LogDebug(my_drawn_detected=my_drawn_detected)
            self.my_deck_queue.append(timestamp)
        
        # op deck
        op_drawn_detected = self._DetectDeck(is_op=True)
        if op_drawn_detected:
            # LogDebug(op_drawn_detected=op_drawn_detected)
            self.op_deck_queue.append(timestamp)

        # stream filtering
        num_cards = num_bboxes if invalid_count != num_bboxes else 0
        num_cards = self.filter.Filter(num_cards, dist=0)
        if num_cards > 0:
            self.signaled_num_cards = num_cards
            self.signaled_timestamp = timestamp

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
        
        return True if bboxes else False

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
            recorder = self.card_recorder[num_cards]
            valid = True
            cards = []
            for i, d in enumerate(recorder):
                card = max(d, key=d.get) if d else -1
                if card == -1:
                    valid = False
                    LogError(info=f"{task_type.name}: card[{i}] not detected, {num_cards} in total")
                cards.append(card)

            if valid:
                LogInfo(type=task_type.name, cards=cards,
                        names=[CardName(card, self.db) for card in cards])
        
        # reset
        self.card_recorder[self.signaled_num_cards] = [defaultdict(int) for _ in range(self.signaled_num_cards)]
        self.signaled_num_cards = 0
        self.signaled_timestamp = 0 
