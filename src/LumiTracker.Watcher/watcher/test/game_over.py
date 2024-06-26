from ..enums import ERatioType
from ..frame_manager import FrameManager

import cv2

def main():
    frame_manager = FrameManager()
    task = frame_manager.game_over_task

    image_path = 'temp/Snipaste_2024-06-23_18-33-58.png'
    image = cv2.imread(image_path)
    image = cv2.cvtColor(image, cv2.COLOR_BGR2BGRA)
    
    height, width = image.shape[:2]
    print(width, height)
    task.OnResize(width, height, ERatioType.E16_9)

    task.frame_buffer = image
    task.Tick(frame_manager)

if __name__ == "__main__":
    main()