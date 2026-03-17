import argparse
import csv
import os
import shutil

def main():
    parser = argparse.ArgumentParser(description="Update cards database with manually corrected data")
    parser.add_argument("version", type=str, help="Target version number (e.g. v6.4)")
    args = parser.parse_args()

    repo_root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    update_dir = os.path.join(repo_root, "agent", "temp", "updates", args.version)
    temp_csv_path = os.path.join(update_dir, "temp_cards.csv")
    images_dir = os.path.join(update_dir, "images")
    
    if not os.path.exists(temp_csv_path):
        print(f"Error: Temporary CSV file not found at {temp_csv_path}")
        return

    print(f"Reading {temp_csv_path}...")
    
    characters = []
    actions = []
    tokens = []
    
    with open(temp_csv_path, "r", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            cat = row.get("card_category", "").strip().lower()
            if cat == "character":
                characters.append(row)
            elif cat == "action":
                actions.append(row)
            elif cat == "token":
                tokens.append(row)
            else:
                print(f"Warning: Unknown category '{cat}' for id {row.get('id')}")

    print(f"Found {len(characters)} characters, {len(actions)} actions, {len(tokens)} tokens.")

    generated_dir = os.path.join(repo_root, "cards", "generated")

    # Update Characters
    if characters:
        char_csv = os.path.join(generated_dir, "characters.csv")
        char_headers = ["id", "zh-HANS", "zh-HANS_short", "ja-JP", "ja-JP_short", "en-US", "en-US_short", "element", "is_monster", "talent_id", "share_id"]
        with open(char_csv, "a", encoding="utf-8", newline="") as f:
            writer = csv.DictWriter(f, fieldnames=char_headers, extrasaction='ignore')
            for row in characters:
                writer.writerow(row)
                
                # Migrate Image
                icon = row.get("icon_name")
                id_val = row.get("id")
                zh_name = row.get("zh-HANS")
                if icon and id_val and zh_name:
                    src_img = os.path.join(images_dir, f"{icon}.png")
                    if os.path.exists(src_img):
                        dest_img = os.path.join(repo_root, "cards", "characters", f"character_{id_val}_{zh_name}.png")
                        shutil.copy2(src_img, dest_img)
                        print(f"  Migrated image to {dest_img}")

    # Update Actions
    if actions:
        action_csv = os.path.join(generated_dir, "actions.csv")
        action_headers = ["id", "zh-HANS", "ja-JP", "en-US", "type", "element", "cost", "snapshot_top", "share_id"]
        with open(action_csv, "a", encoding="utf-8", newline="") as f:
            writer = csv.DictWriter(f, fieldnames=action_headers, extrasaction='ignore')
            for row in actions:
                writer.writerow(row)
                
                # Migrate Image
                icon = row.get("icon_name")
                id_val = row.get("id")
                zh_name = row.get("zh-HANS")
                if icon and id_val and zh_name:
                    src_img = os.path.join(images_dir, f"{icon}.png")
                    if os.path.exists(src_img):
                        dest_img = os.path.join(repo_root, "cards", "actions", f"action_{id_val}_{zh_name}.png")
                        shutil.copy2(src_img, dest_img)
                        print(f"  Migrated image to {dest_img}")

    # Update Tokens
    if tokens:
        token_csv = os.path.join(generated_dir, "tokens.csv")
        token_headers = ["id", "zh-HANS", "ja-JP", "en-US", "type", "element", "cost", "snapshot_top"]
        with open(token_csv, "a", encoding="utf-8", newline="") as f:
            writer = csv.DictWriter(f, fieldnames=token_headers, extrasaction='ignore')
            for row in tokens:
                writer.writerow(row)
                
                # Migrate Image
                icon = row.get("icon_name")
                id_val = row.get("id")
                zh_name = row.get("zh-HANS")
                if icon and id_val and zh_name:
                    src_img = os.path.join(images_dir, f"{icon}.png")
                    if os.path.exists(src_img):
                        # Assuming token dir is cards/actions/tokens based on watcher/database.py mapping
                        dest_img = os.path.join(repo_root, "cards", "actions", "tokens", f"token_{id_val}_{zh_name}.png")
                        shutil.copy2(src_img, dest_img)
                        print(f"  Migrated image to {dest_img}")

    print("Please manually update share_code.csv and language.csv if required by your pipeline.")
    print("Database update complete.")

if __name__ == "__main__":
    main()
