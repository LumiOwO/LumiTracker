from ..config import cfg
cfg.DEBUG = True

from ..regions import GetRatioType
from ..frame_manager import FrameManager
from ..feature import CardName
from ..states import GTasks
from ..enums import EClientType

import logging
import os
import sys
import cv2
import numpy as np

logging.getLogger('matplotlib').setLevel(logging.WARNING)
import matplotlib.pyplot as plt


def image(image_path='temp/Snipaste_2024-08-03_16-02-12.png'):
    frame_manager = FrameManager(EClientType.YuanShen)
    task = GTasks.CardFlow
    frame_manager.round = 1

    image = cv2.imread(image_path)
    image = cv2.cvtColor(image, cv2.COLOR_BGR2BGRA)
    
    height, width = image.shape[:2]
    ratio_type = GetRatioType(width, height)
    print(width, height, ratio_type)
    task.OnResize(width, height, ratio_type)

    task.frame_buffer = image
    task.fm = frame_manager
    task.Tick()
    print("Detected:")
    for num, recorder in task.card_recorder.items():
        res = ""
        for i in range(num):
            if i > 0:
                res += ", "
            if not recorder[i]:
                res += "____"
            else:
                card_id = next(iter(recorder[i].keys()))
                res += CardName(card_id, frame_manager.db)
        print(f"{num}: {res}")

    # dst = task.thresh
    # dst = task.center_buffer
    dst = task.frame_buffer
    # dst = cv2.cvtColor(task.my_deck_edges, cv2.COLOR_GRAY2BGRA)
    for box in task.bboxes:
        # box.left   += task.center_crop.left
        # box.top    += task.center_crop.top
        # box.right  += task.center_crop.left
        # box.bottom += task.center_crop.top
        cv2.rectangle(dst, (box.left, box.top), (box.right, box.bottom), (0, 255, 0), 2)
        # print(box)
    scale = task.my_deck_crop.height / task.DECK_DST_HEIGHT
    print(f"{scale=}, {task.my_deck_crop=}")
    # for box in task.my_deck_bboxes:
    #     box.left   = int(box.left   * scale) + task.my_deck_crop.left
    #     box.top    = int(box.top    * scale) + task.my_deck_crop.top
    #     box.right  = int(box.right  * scale) + task.my_deck_crop.left
    #     box.bottom = int(box.bottom * scale) + task.my_deck_crop.top
    #     cv2.rectangle(dst, (box.left, box.top), (box.right, box.bottom), (0, 0, 255), 2)
        # print(box)
    # for box in task.op_deck_bboxes:
        # box.left   = int(box.left   * scale) + task.op_deck_crop.left
        # box.top    = int(box.top    * scale) + task.op_deck_crop.top
        # box.right  = int(box.right  * scale) + task.op_deck_crop.left
        # box.bottom = int(box.bottom * scale) + task.op_deck_crop.top
    #     cv2.rectangle(dst, (box.left, box.top), (box.right, box.bottom), (0, 0, 255), 2)
        # print(box)
    
    my_deck_buffer = task.frame_buffer[
        task.my_deck_crop.top  : task.my_deck_crop.bottom, 
        task.my_deck_crop.left : task.my_deck_crop.right
    ]
    plt.imshow(cv2.cvtColor(my_deck_buffer, cv2.COLOR_BGR2RGB))
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
    task = GTasks.CardFlow
    ratio_type = GetRatioType(width, height)
    print(width, height, ratio_type)
    task.OnResize(width, height, ratio_type)
    task.fm = frame_manager

    cnt = 0
    while True:
        print(cnt)
        cnt += 1

        ret, frame = cap.read()
        if not ret:
            break

        task.frame_buffer = cv2.cvtColor(frame, cv2.COLOR_BGR2BGRA)
        task.Tick()

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
        # for i in range(0, 20):
        #     image(f"temp/center{i + 1}.png")
    elif test_func == "video":
        video()