import argparse
import csv
import json
import os
import urllib.request
import urllib.error
import time

def fetch_json(url):
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    try:
        with urllib.request.urlopen(req) as response:
            return json.loads(response.read().decode('utf-8'))
    except Exception as e:
        print(f"Error fetching {url}: {e}")
        return None

def normalize_name(name):
    """Normalize names to account for punctuation differences between website and local DB."""
    if not name:
        return name
    # Normalize ellipsis to three middle dots first
    name = name.replace("…", "···")
    # Then normalize all middle dots to the same character
    name = name.replace("·", "・")
    return name

def download_image(url, output_path):
    if os.path.exists(output_path):
        return True
    
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    try:
        with urllib.request.urlopen(req) as response:
            with open(output_path, "wb") as f:
                f.write(response.read())
        return True
    except Exception as e:
        print(f"Error downloading {url}: {e}")
        return False

def main():
    parser = argparse.ArgumentParser(description="Fetch new TCG cards data from Yatta API")
    parser.add_argument("version", type=str, help="Target version number (e.g. v6.4)")
    args = parser.parse_args()

    # Determine paths
    repo_root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    cards_generated_dir = os.path.join(repo_root, "cards", "generated")
    output_dir = os.path.join(repo_root, "agent", "temp", "updates", args.version)
    images_dir = os.path.join(output_dir, "images")
    os.makedirs(images_dir, exist_ok=True)

    print(f"Starting fetch for version {args.version}")
    
    # 1. Read existing local CSV files to build a set of existing card names
    existing_names = set()
    for csv_name in ["actions.csv", "characters.csv", "tokens.csv"]:
        csv_path = os.path.join(cards_generated_dir, csv_name)
        if not os.path.exists(csv_path):
            print(f"Warning: {csv_path} not found.")
            continue
        with open(csv_path, "r", encoding="utf-8") as f:
            reader = csv.DictReader(f)
            for row in reader:
                if "zh-HANS" in row:
                    existing_names.add(normalize_name(row["zh-HANS"]))
                    
    print(f"Loaded {len(existing_names)} existing card names from local database.")

    # 2. Fetch the Chinese bulk JSON API to identify new cards
    api_url_chs = "https://gi.yatta.moe/api/v2/chs/gcg"
    print(f"Fetching {api_url_chs}...")
    chs_data = fetch_json(api_url_chs)
    if not chs_data or "data" not in chs_data or "items" not in chs_data["data"]:
        print("Failed to fetch or parse API data.")
        return

    # 3. Diff to find new cards
    new_cards_chs = {}
    for card_id, card_info in chs_data["data"]["items"].items():
        if normalize_name(card_info.get("name", "")) not in existing_names:
            new_cards_chs[card_id] = card_info

    if not new_cards_chs:
        print("No new cards found. Exiting.")
        return

    print(f"Identified {len(new_cards_chs)} new cards.")
    
    # 4. Fetch English and Japanese data
    print("Fetching English and Japanese data...")
    en_data = fetch_json("https://gi.yatta.moe/api/v2/en/gcg")
    jp_data = fetch_json("https://gi.yatta.moe/api/v2/jp/gcg")
    
    en_items = en_data["data"]["items"] if en_data and "data" in en_data else {}
    jp_items = jp_data["data"]["items"] if jp_data and "data" in jp_data else {}

    # Save raw api data for new cards
    raw_api_data = {
        "chs": new_cards_chs,
        "en": {k: en_items.get(k) for k in new_cards_chs.keys()},
        "jp": {k: jp_items.get(k) for k in new_cards_chs.keys()}
    }
    raw_json_path = os.path.join(output_dir, "raw_api_data.json")
    with open(raw_json_path, "w", encoding="utf-8") as f:
        json.dump(raw_api_data, f, ensure_ascii=False, indent=2)

    # 5. Extract relevant fields and download images
    print("Downloading images and processing data...")
    
    characters = []
    actions = []
    tokens = []
    
    for card_id, chs_info in new_cards_chs.items():
        en_info = en_items.get(card_id, {})
        jp_info = jp_items.get(card_id, {})
        
        icon = chs_info.get("icon", "")
        
        # Download image
        if icon:
            image_url = f"https://gi.yatta.moe/assets/UI/gcg/{icon}.png"
            image_path = os.path.join(images_dir, f"{icon}.png")
            if not os.path.exists(image_path):
                print(f"  Downloading image for {chs_info.get('name')}...")
                download_image(image_url, image_path)
                time.sleep(0.1) # Be polite to the server
                
        # Determine card_category
        raw_type = chs_info.get("type", "")
        if "character" in raw_type.lower():
            card_category = "Character"
        elif "action" in raw_type.lower():
            card_category = "Action" # Or Token, user can manually adjust
        else:
            card_category = raw_type

        if card_category == "Character":
            characters.append({
                "id": card_id,
                "zh-HANS": chs_info.get("name", ""),
                "zh-HANS_short": "",
                "ja-JP": jp_info.get("name", ""),
                "ja-JP_short": "",
                "en-US": en_info.get("name", ""),
                "en-US_short": "",
                "element": "",
                "is_monster": "",
                "talent_id": "",
                "share_id": "",
                "icon_name": icon
            })
        elif card_category == "Action":
            actions.append({
                "id": card_id,
                "zh-HANS": chs_info.get("name", ""),
                "ja-JP": jp_info.get("name", ""),
                "en-US": en_info.get("name", ""),
                "type": "",
                "element": "",
                "cost": "",
                "snapshot_top": "",
                "share_id": "",
                "icon_name": icon
            })
        else:
            tokens.append({
                "id": card_id,
                "zh-HANS": chs_info.get("name", ""),
                "ja-JP": jp_info.get("name", ""),
                "en-US": en_info.get("name", ""),
                "type": "",
                "element": "",
                "cost": "",
                "snapshot_top": "",
                "icon_name": icon
            })

    print(f"Successfully processed {len(characters)} characters, {len(actions)} actions, {len(tokens)} tokens.")

    # 6. Export to separate CSVs
    char_headers = ["id", "zh-HANS", "zh-HANS_short", "ja-JP", "ja-JP_short", "en-US", "en-US_short", "element", "is_monster", "talent_id", "share_id", "icon_name"]
    action_headers = ["id", "zh-HANS", "ja-JP", "en-US", "type", "element", "cost", "snapshot_top", "share_id", "icon_name"]
    token_headers = ["id", "zh-HANS", "ja-JP", "en-US", "type", "element", "cost", "snapshot_top", "icon_name"]

    with open(os.path.join(output_dir, "characters.csv"), "w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=char_headers)
        writer.writeheader()
        writer.writerows(characters)

    with open(os.path.join(output_dir, "actions.csv"), "w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=action_headers)
        writer.writeheader()
        writer.writerows(actions)

    with open(os.path.join(output_dir, "tokens.csv"), "w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=token_headers)
        writer.writeheader()
        writer.writerows(tokens)

    # Generate TODO list
    todo_path = os.path.join(output_dir, "TODO-list.md")
    with open(todo_path, "w", encoding="utf-8") as f:
        f.write("# TODO List for manual review\n\n")
        f.write("- [ ] 1. Fill in missing `share_id` in `characters.csv` and `actions.csv`.\n")
        f.write("- [ ] 2. Fill in missing `element`, `type`, `cost`, `short_name`, and `snapshot_top` data.\n")
        f.write("- [ ] 3. Review translations.\n")
        f.write("- [ ] 4. Move any tokens misclassified as actions from `actions.csv` to `tokens.csv`.\n")
        if characters:
            f.write("- [ ] 5. Add avatar images for the following characters manually into the `images` folder:\n")
            for c in characters:
                f.write(f"    - Name: {c['zh-HANS']}, Expected file: `avatar_{c['icon_name']}.png`\n")

    print(f"Generated CSVs and TODO list in {output_dir}")

if __name__ == "__main__":
    main()
