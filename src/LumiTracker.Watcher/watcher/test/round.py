from ..enums import ERatioType
from ..frame_manager import FrameManager
from ..regions import GetRatioType
from ..states import GTasks

import cv2
import sys

def main(test_round):
    frame_manager = FrameManager()
    task = GTasks.Round

    image_path = f'temp/control_Round{test_round}.png'
    # image_path = f'temp/Snipaste_2024-06-25_00-21-23.png'
    image = cv2.imread(image_path)
    image = cv2.cvtColor(image, cv2.COLOR_BGR2BGRA)
    
    height, width = image.shape[:2]
    print(width, height)
    ratio_type = GetRatioType(width, height)
    task.OnResize(width, height, ratio_type)

    task.frame_buffer = image
    task.fm = frame_manager
    task.Tick()

    # cv2.imshow('Image', task.buffer)
    # cv2.waitKey(0)

if __name__ == "__main__":
    test_round = int(sys.argv[1])
    main(test_round)