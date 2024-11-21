from ..enums import ERatioType
from ..frame_manager import FrameManager
from ..states import GTasks
from ..regions import GetRatioType

import cv2
import logging
logging.getLogger('matplotlib').setLevel(logging.WARNING)
import matplotlib.pyplot as plt

def main():
    frame_manager = FrameManager()
    task = GTasks.GameStart

    image_path = 'temp/Snipaste_2024-11-20_23-20-19.png'
    image = cv2.imread(image_path)
    image = cv2.cvtColor(image, cv2.COLOR_BGR2BGRA)
    
    height, width = image.shape[:2]
    ratio_type = GetRatioType(width, height)
    print(width, height, ratio_type)
    task.OnResize(width, height, ratio_type)

    task.frame_buffer = image
    task.fm = frame_manager

    for i in range(6):
        print(i)
        offset = task.vs_left_offsets[i]
        buffer = task.frame_buffer[
            task.vs_anchor_box.top  : task.vs_anchor_box.bottom, 
            task.vs_anchor_box.left + offset : task.vs_anchor_box.right + offset
        ]
        plt.imshow(cv2.cvtColor(buffer, cv2.COLOR_BGR2RGB))
        plt.show()

    task.Tick()
    task.DetectCharacters()

if __name__ == "__main__":
    main()