from ..config import cfg
cfg.DEBUG = True

from ..enums import ERatioType
from ..frame_manager import FrameManager

import logging
import os
import sys
import cv2
import numpy as np

logging.getLogger('matplotlib').setLevel(logging.WARNING)
import matplotlib.pyplot as plt


def image():
    frame_manager = FrameManager()
    task = frame_manager.card_flow_task

    image_path = 'temp/Snipaste_2024-07-21_19-05-35.png'
    image = cv2.imread(image_path)
    image = cv2.cvtColor(image, cv2.COLOR_BGR2BGRA)
    
    height, width = image.shape[:2]
    print(width, height)
    task.OnResize(width, height, ERatioType.E16_9)

    task.frame_buffer = image
    task.Tick(frame_manager)

    dst = task.frame_buffer
    # dst = cv2.cvtColor(task.deck_thresh, cv2.COLOR_GRAY2BGRA)
    for box in task.bboxes:
        cv2.rectangle(dst, (box.left, box.top), (box.right, box.bottom), (0, 255, 0), 2)
        # print(box)
    for box in task.my_deck_bboxes:
        box.left   += task.my_deck_crop.left
        box.top    += task.my_deck_crop.top
        box.right  += task.my_deck_crop.left
        box.bottom += task.my_deck_crop.top
        cv2.rectangle(dst, (box.left, box.top), (box.right, box.bottom), (0, 0, 255), 2)
        # print(box)
    for box in task.op_deck_bboxes:
        box.left   += task.op_deck_crop.left
        box.top    += task.op_deck_crop.top
        box.right  += task.op_deck_crop.left
        box.bottom += task.op_deck_crop.top
        cv2.rectangle(dst, (box.left, box.top), (box.right, box.bottom), (0, 0, 255), 2)
        # print(box)
    
    plt.imshow(cv2.cvtColor(dst, cv2.COLOR_BGR2RGB))
    plt.show()


def video():
    input_video_path = 'temp/2024-07-14 18-09-30.mp4'
    output_video_path = 'temp/card_flow_output_video.mp4'
    cap = cv2.VideoCapture(input_video_path)
    width = int(cap.get(cv2.CAP_PROP_FRAME_WIDTH))
    height = int(cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
    fps = cap.get(cv2.CAP_PROP_FPS)
    frame_count = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    print(f"{width=}, {height=}, {fps=}, {frame_count=}")

    fourcc = cv2.VideoWriter_fourcc(*'mp4v')  # 'mp4v' is a popular codec for mp4
    out = cv2.VideoWriter(output_video_path, fourcc, fps, (width, height))

    frame_manager = FrameManager()
    task = frame_manager.card_flow_task
    task.OnResize(width, height, ERatioType.E16_9)

    cnt = 0
    while True:
        print(cnt)
        cnt += 1

        ret, frame = cap.read()
        if not ret:
            break

        task.frame_buffer = cv2.cvtColor(frame, cv2.COLOR_BGR2BGRA)
        task.Tick(frame_manager)

        for box in task.bboxes:
            x = box.left
            y = box.top
            w = box.width
            h = box.height
            cv2.rectangle(frame, (x, y), (x + w, y + h), (0, 255, 0), 2)
        
        for box in task.my_deck_bboxes:
            x = box.left   + task.my_deck_crop.left
            y = box.top    + task.my_deck_crop.top
            w = box.width
            h = box.height
            cv2.rectangle(frame, (x, y), (x + w, y + h), (0, 0, 255), 2)
        
        for box in task.op_deck_bboxes:
            x = box.left   + task.op_deck_crop.left
            y = box.top    + task.op_deck_crop.top
            w = box.width
            h = box.height
            cv2.rectangle(frame, (x, y), (x + w, y + h), (0, 0, 255), 2)
        
        out.write(frame)
    
    cap.release()
    out.release()
    cv2.destroyAllWindows()

if __name__ == "__main__":
    test_func = sys.argv[1]
    if test_func == "image":
        image()
    elif test_func == "video":
        video()