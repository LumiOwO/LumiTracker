from ..database import SaveImage
from ..enums import ERatioType
from ..frame_manager import FrameManager
from ..regions import GetRatioType
from ..states import GTasks

import cv2
import sys
import logging
logging.getLogger('matplotlib').setLevel(logging.WARNING)
import matplotlib.pyplot as plt

def main(test_round):
    frame_manager = FrameManager()
    task = GTasks.Round

    # image_path = f'temp/control_Round{test_round}.png'
    image_path = f'temp/Snipaste_2024-12-02_22-06-36.png'
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
    # SaveImage(task.buffer, "temp/save/my.png")

if __name__ == "__main__":
    test_round = int(sys.argv[1])
    main(test_round)