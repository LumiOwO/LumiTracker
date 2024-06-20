
filename = "python/Lib/site-packages/windows_capture/__init__.py"

with open(filename, 'r') as file:
    lines = file.readlines()

with open(filename, 'w') as file:
    for line in lines:
        if "import cv2" in line:
            file.write('#' + line)
        else:
            file.write(line)