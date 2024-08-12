import json
import sys

def main(publish_dir):
    # File path to the JSON file
    file_path = f"{publish_dir}assets/config.json"

    # Step 1: Read the JSON file
    with open(file_path, "r") as file:
        cfg = json.load(file)

    # Step 2: Modify the dictionary
    reset_configs = {
        "DEBUG" : False,
        "DEBUG_SAVE" : False,
        "frame_limit" : 50,
        "lang" : "zh-HANS",
        "closing_behavior" : "Minimize",
        "theme" : "Dark",
        "client_type" : "YuanShen",
        "capture_type": "BitBlt",
        "show_ui_outside" : False,
        "show_closing_dialog" : True,
        "check_updates_on_startup" : True,
    }
    cfg.update(reset_configs)


    # Step 3: Write the modified dictionary back to the JSON file
    with open(file_path, "w", encoding="utf-8") as file:
        json.dump(cfg, file, indent=2, ensure_ascii=False)

if __name__ == "__main__":
    publish_dir = sys.argv[1]
    main(publish_dir)