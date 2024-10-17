import time
import logging
import cv2
import numpy as np
from datetime import datetime
import os

from .config import cfg, LogDebug, LogInfo, LogWarning, LogError
from .enums import EGameEvent, EClientType
from .database import Database, SaveImage

from .states import *

class FrameManager:
    def __init__(self, client_type, log_dir, test_on_resize):
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
        self.test_on_resize  = test_on_resize
        self.need_capture    = False
        self.log_dir         = log_dir

        # For WeMeet, there are margins in the captured frame
        # This box is used to crop the margins
        self.client_type     = client_type
        self.content_box     = ()
        self.content_not_found_warned = False

    def Resize(self, client_width, client_height):
        if client_width == 0 or client_height == 0:
            return

        self.content_box = ()
        self.content_not_found_warned = False
        GTasks.OnResize(client_width, client_height)

        if self.test_on_resize:
            self.need_capture = True

    def CaptureTest(self, image):
        if self.log_dir == "":
            LogError(info="[CaptureTest] Capture save directory is not given!")
            return

        filename = datetime.now().strftime("%Y-%m-%d_%H-%M-%S") + ".png"
        path = os.path.join(self.log_dir, filename)

        SaveImage(image, path, remove_alpha=True)
        LogInfo(
            type=f"{EGameEvent.CAPTURE_TEST.name}",
            filename=filename,
            width=image.shape[1],
            height=image.shape[0],
            )

    def OnFrameArrived(self, frame_buffer):
        # skip invalid frames
        if frame_buffer.size == 0:
            return

        # Crop margins for WeMeet
        if self.client_type == EClientType.WeMeet.name:
            if len(self.content_box) == 0:
                self.FindContentBox(frame_buffer)
                # In case of content box not found
                if len(self.content_box) == 0:
                    return
                # Remember to resize tasks!
                left, top, right, bottom = self.content_box
                GTasks.OnResize(right - left, bottom - top)

            left, top, right, bottom = self.content_box
            frame_buffer = frame_buffer[top:bottom, left:right]

        # capture test
        if self.need_capture:
            self.CaptureTest(frame_buffer)
            self.need_capture = False

        ####################
        # Main tasks
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
                type=f"{EGameEvent.LOG_FPS.name}",
                fps=f"{fps}"
                )

            self.frame_count   = 0
            self.prev_log_time = cur_time

    def FindContentBox(self, frame_buffer):
        image_hsv = cv2.cvtColor(frame_buffer, cv2.COLOR_BGR2HSV)

        # Define the two colors to remove in HSV (you'll need to convert RGB to HSV)
        color_1_rgb = np.array([255, 255, 255])  # White (255,255,255)
        color_2_rgb = np.array([45, 48, 51])     # Dark gray (45,48,51)

        # Convert these RGB colors to HSV using OpenCV
        color_1_hsv = cv2.cvtColor(np.uint8([[color_1_rgb]]), cv2.COLOR_RGB2HSV)[0][0]
        color_2_hsv = cv2.cvtColor(np.uint8([[color_2_rgb]]), cv2.COLOR_RGB2HSV)[0][0]

        # Define tolerance (adjust as needed)
        tolerance_hue = 10  # For hue tolerance (higher value means more leniency)
        tolerance_sat_val = 40  # For saturation and value tolerance

        # Define lower and upper bounds for color 1 in HSV
        lower_bound_1 = np.array([0, 0, 255 - tolerance_sat_val])
        upper_bound_1 = np.array([180, tolerance_sat_val, 255])

        # Define lower and upper bounds for color 2 in HSV
        lower_bound_2 = np.array([
            color_2_hsv[0] - tolerance_hue, 
            max(0, color_2_hsv[1] - tolerance_sat_val), 
            max(0, color_2_hsv[2] - tolerance_sat_val)])
        upper_bound_2 = np.array([
            color_2_hsv[0] + tolerance_hue, 
            min(255, color_2_hsv[1] + tolerance_sat_val), 
            min(255, color_2_hsv[2] + tolerance_sat_val)])

        # Create masks for both colors
        mask_1 = cv2.inRange(image_hsv, lower_bound_1, upper_bound_1)  # Mask for first color
        mask_2 = cv2.inRange(image_hsv, lower_bound_2, upper_bound_2)  # Mask for second color

        # Combine the two masks
        combined_mask = cv2.bitwise_or(mask_1, mask_2)

        # Invert the mask to keep non-keyed areas
        mask_inv = cv2.bitwise_not(combined_mask)
        mask_inv = cv2.morphologyEx(mask_inv, cv2.MORPH_OPEN, np.ones((5, 5)), iterations=1)
        # SaveImage(mask_inv, "temp/temp1.png", remove_alpha=True)

        height, width = frame_buffer.shape[:2]
        boxes = []
        contours, _ = cv2.findContours(mask_inv, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        for contour in contours:
            x, y, w, h = cv2.boundingRect(contour)  # Get the bounding box coordinates
            if w < width * 0.6 or h < height * 0.6:
                continue
            boxes.append((x, y, w, h))

        if len(boxes) != 1:
            if not self.content_not_found_warned:
                LogWarning(info="[FindContentBox] Failed to find content box.", num_boxes=len(boxes))
                self.content_not_found_warned = True
            return

        left, top, right, bottom = self._RemoveSmallMargins(frame_buffer, boxes[0])
        # if center box too small, regard it as a failure case
        if (right - left <= 200) or (bottom - top <= 200):
            if not self.content_not_found_warned:
                LogWarning(
                    info="[FindContentBox] Found content box, but too small.", 
                    box=(left, top, right, bottom),
                    width=right - left,
                    height=bottom - top,
                    )
                self.content_not_found_warned = True
            return
        self.content_box = (left, top, right, bottom)
        LogInfo(
            info=f"[FindContentBox] Content box found.",
            box=(left, top, right, bottom),
            width=right - left,
            height=bottom - top,
            aspect=(right - left) / (bottom - top),
            )

    def _RemoveSmallMargins(self, frame_buffer, box):
        x, y, w, h = box
        # LogDebug(info=f"{(x, y, w, h)}")
        img = frame_buffer[y:y+h, x:x+w]
        image_width, image_height = w, h
        # SaveImage(img, "temp/temp2.png", remove_alpha=True)

        # Convert the image to grayscale
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        
        # Threshold for dark color (black)
        _, binary = cv2.threshold(gray, 60, 1, cv2.THRESH_BINARY_INV)

        # Get non-zero rows and columns for dark color
        thres = 10
        borders_rows = np.where((binary.sum(axis=1) <= thres) | (binary.sum(axis=1) >= (image_width - thres)))[0]
        borders_cols = np.where((binary.sum(axis=0) <= thres) | (binary.sum(axis=0) >= (image_height - thres)))[0]
        # LogDebug(info=f"{borders_rows=}, {borders_cols=}")

        # Function to find continuous ranges
        def get_continuous_indices(indices):
            if indices.size == 0:
                return []
            return np.split(indices, np.where(np.diff(indices) != 1)[0] + 1)

        # Get continuous rows and columns
        borders_rows = get_continuous_indices(borders_rows)
        borders_cols = get_continuous_indices(borders_cols)
        # LogDebug(info=f"{borders_rows=}, {borders_cols=}")

        top, bottom = 0, image_height
        left, right = 0, image_width
        if len(borders_rows) > 0 and borders_rows[0][0] == 0:
            top = max(int(borders_rows[0][-1]), 0) + 1
        if len(borders_rows) > 0 and borders_rows[-1][-1] == image_height - 1:
            bottom = min(int(borders_rows[-1][0]), image_height) 
        if len(borders_cols) > 0 and borders_cols[0][0] == 0:
            left = max(int(borders_cols[0][-1]) + 1, 0)
        if len(borders_cols) > 0 and borders_cols[-1][-1] == image_width - 1:
            right = min(int(borders_cols[-1][0]), image_width)
        # SaveImage(img[top:bottom, left:right], "temp/temp3.png", remove_alpha=True)

        left   += x
        right  += x
        top    += y
        bottom += y
        return (left, top, right, bottom)
