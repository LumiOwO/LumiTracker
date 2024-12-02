from ..database import SaveImage
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
    task = GTasks.CardBack

    image_path = 'temp/Snipaste_2024-12-02_22-06-36.png'
    image = cv2.imread(image_path)
    image = cv2.cvtColor(image, cv2.COLOR_BGR2BGRA)
    
    height, width = image.shape[:2]
    ratio_type = GetRatioType(width, height)
    print(width, height, ratio_type)
    task.OnResize(width, height, ratio_type)

    task.frame_buffer = image
    task.fm = frame_manager
    task.Tick()

    boxes = [task.history_box, task.my_card_back_box, task.op_card_back_box]
    for box in boxes:
        buffer = task.frame_buffer[
            box.top  : box.bottom, 
            box.left : box.right
        ]
        plt.imshow(cv2.cvtColor(buffer, cv2.COLOR_BGR2RGB))
        plt.show()
        # SaveImage(buffer, "temp/save/history.png")

if __name__ == "__main__":
    main()