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

    # 5. Extract relevant fields and download images
    print("Downloading images and processing data...")
    merged_new_cards = []
    
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

        merged_card = {
            "card_category": card_category,
            "id": card_id,
            "share_id": "",
            "zh-HANS": chs_info.get("name", ""),
            "zh-HANS_short": "",
            "ja-JP": jp_info.get("name", ""),
            "ja-JP_short": "",
            "en-US": en_info.get("name", ""),
            "en-US_short": "",
            "type": "", # To be filled manually or extracted from tags
            "element": "", # To be filled manually or extracted from tags
            "cost": "",
            "snapshot_top": "",
            "is_monster": "",
            "talent_id": "",
            "icon_name": icon
        }
        merged_new_cards.append(merged_card)

    print(f"Successfully processed {len(merged_new_cards)} new cards.")

    # 6. Export to CSV
    csv_path = os.path.join(output_dir, "temp_cards.csv")
    headers = [
        "card_category", "id", "share_id", 
        "zh-HANS", "zh-HANS_short", 
        "ja-JP", "ja-JP_short", 
        "en-US", "en-US_short", 
        "type", "element", "cost", "snapshot_top", 
        "is_monster", "talent_id", "icon_name"
    ]
    with open(csv_path, "w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=headers)
        writer.writeheader()
        writer.writerows(merged_new_cards)
        
    print(f"Generated {csv_path}")

if __name__ == "__main__":
    main()
