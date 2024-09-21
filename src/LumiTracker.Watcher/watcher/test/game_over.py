from ..enums import ERatioType
from ..frame_manager import FrameManager
from ..states import GTasks
from ..regions import GetRatioType

import cv2

def main():
    frame_manager = FrameManager()
    task = GTasks.GameOver

    image_path = 'temp/Snipaste_2024-06-23_18-33-58.png'
    image = cv2.imread(image_path)
    image = cv2.cvtColor(image, cv2.COLOR_BGR2BGRA)
    
    height, width = image.shape[:2]
    ratio_type = GetRatioType(width, height)
    print(width, height, ratio_type)
    task.OnResize(width, height, ratio_type)

    task.frame_buffer = image
    task.fm = frame_manager
    task.Tick()

if __name__ == "__main__":
    main()