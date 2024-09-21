from ..config import cfg
cfg.DEBUG = True

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
    task = GTasks.GamePhase

    image_path = 'temp/Snipaste_2024-09-21_21-42-49.png'
    image = cv2.imread(image_path)
    image = cv2.cvtColor(image, cv2.COLOR_BGR2BGRA)
    
    height, width = image.shape[:2]
    ratio_type = GetRatioType(width, height)
    print(width, height, ratio_type)
    task.OnResize(width, height, ratio_type)

    task.frame_buffer = image
    task.fm = frame_manager
    task.Tick()

    plt.imshow(cv2.cvtColor(task.buffer, cv2.COLOR_BGR2RGB))
    plt.show()

if __name__ == "__main__":
    main()