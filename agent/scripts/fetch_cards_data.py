import argparse
import csv
import json
import os
import re
import urllib.request
import urllib.error
import time
import concurrent.futures
from tqdm import tqdm
from yatta_mapping import ELEMENT_TAGS, TYPE_TAGS, COST_PROPS, MONSTER_TAGS

# Sentinel so the middle-dot replacement never touches expanded ellipses
ELLIPSIS_PLACEHOLDER = "\u0000"
COLOR_TAG_RE = re.compile(r"</?color(?:=#[0-9A-Fa-f]{8})?>")

PNG_MAGIC = b"\x89PNG\r\n\x1a\n"
WEBP_MAGIC = b"RIFF"

def fetch_json(url, max_retries=3, delay_after=0.1):
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    for attempt in range(max_retries):
        try:
            with urllib.request.urlopen(req, timeout=30) as response:
                data = json.loads(response.read().decode('utf-8'))
                if delay_after > 0:
                    time.sleep(delay_after)
                return data
        except Exception as e:
            print(f"Error fetching {url} (attempt {attempt+1}/{max_retries}): {e}")
            if attempt < max_retries - 1:
                time.sleep(2)
    return None

def normalize_name(name):
    """Normalize names to account for punctuation differences between website and local DB.

    Must be idempotent for both the API ellipsis (U+2026) and the DB-stored
    three-dot form (U+00B7 x3): both canonicalize to U+00B7 x3, while single
    middle dots (U+00B7) become U+30FB.
    """
    if not name:
        return name
    # Strip rich-text color markup (present in single-card API dictionary names)
    name = COLOR_TAG_RE.sub("", name)
    # Hide ellipses first so the middle-dot replacement below cannot touch them
    name = name.replace("…", ELLIPSIS_PLACEHOLDER)   # API form (U+2026)
    name = name.replace("···", ELLIPSIS_PLACEHOLDER)  # DB form (U+00B7 x3)
    # Then normalize single middle dots to the same character
    name = name.replace("·", "・")
    # Finally expand the ellipsis to three middle dots (existing DB convention)
    name = name.replace(ELLIPSIS_PLACEHOLDER, "···")
    return name

def sanitize_image(output_path):
    """Verify the downloaded file is a real PNG; convert WebP content to PNG; reject other formats."""
    with open(output_path, "rb") as f:
        magic = f.read(12)

    if magic.startswith(PNG_MAGIC):
        return True

    if magic[:4] == WEBP_MAGIC and magic[8:12] == b"WEBP":
        try:
            from PIL import Image
            with Image.open(output_path) as img:
                img.save(output_path, "PNG")
            print(f"Converted WebP to PNG: {output_path}")
            return True
        except Exception as e:
            print(f"Error converting WebP to PNG ({output_path}): {e}")
            os.remove(output_path)
            return False

    # Unknown format (e.g. an HTML error page): remove the invalid file so it cannot pollute the database
    print(f"Error: {output_path} is not a valid PNG or WebP (magic={magic[:4]!r}). Removed the file.")
    os.remove(output_path)
    return False

def download_image(url, output_path, max_retries=3, delay_after=0.1):
    if os.path.exists(output_path):
        return True
    
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    for attempt in range(max_retries):
        try:
            with urllib.request.urlopen(req, timeout=30) as response:
                with open(output_path, "wb") as f:
                    f.write(response.read())
            if not sanitize_image(output_path):
                print(f"Warning: invalid image content from {url}, file removed. Check the source manually.")
                return False
            if delay_after > 0:
                time.sleep(delay_after)
            return True
        except Exception as e:
            print(f"Error downloading {url} (attempt {attempt+1}/{max_retries}): {e}")
            if attempt < max_retries - 1:
                time.sleep(2)
    return False

def main():
    parser = argparse.ArgumentParser(description="Fetch new TCG cards data from Yatta API")
    parser.add_argument("version", type=str, help="Target version number (e.g. v6.4)")
    args = parser.parse_args()

    # Use system proxy if set in environment variables
    http_proxy = os.environ.get('http_proxy') or os.environ.get('HTTP_PROXY')
    https_proxy = os.environ.get('https_proxy') or os.environ.get('HTTPS_PROXY')
    
    proxies = {}
    if http_proxy:
        proxies['http'] = http_proxy
    if https_proxy:
        proxies['https'] = https_proxy
        
    if proxies:
        print(f"Using proxy: {proxies}")
        proxy_handler = urllib.request.ProxyHandler(proxies)
        opener = urllib.request.build_opener(proxy_handler)
        urllib.request.install_opener(opener)

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

    # 2. Fetch bulk JSON API to identify new cards and metadata
    print("Fetching bulk API data (CHS, EN, JP)...")
    base_api_url = "https://gi.yatta.moe/api/v2"
    langs = ["chs", "en", "jp"]
    fetched_data = {}
    
    def fetch_lang(lang):
        return lang, fetch_json(f"{base_api_url}/{lang}/gcg")

    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as executor:
        futures = [executor.submit(fetch_lang, lang) for lang in langs]
        for future in tqdm(concurrent.futures.as_completed(futures), total=len(langs), desc="Fetching JSONs", unit="req"):
            lang, data = future.result()
            fetched_data[lang] = data
            
    chs_data = fetched_data.get("chs")
    en_data = fetched_data.get("en")
    jp_data = fetched_data.get("jp")

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
    
    en_items = en_data["data"]["items"] if en_data and "data" in en_data else {}
    jp_items = jp_data["data"]["items"] if jp_data and "data" in jp_data else {}

    # # Save full api data for reference
    # with open(os.path.join(output_dir, "api_chs.json"), "w", encoding="utf-8") as f:
    #     json.dump(chs_data, f, ensure_ascii=False, indent=2)
    # with open(os.path.join(output_dir, "api_en.json"), "w", encoding="utf-8") as f:
    #     json.dump(en_data, f, ensure_ascii=False, indent=2)
    # with open(os.path.join(output_dir, "api_jp.json"), "w", encoding="utf-8") as f:
    #     json.dump(jp_data, f, ensure_ascii=False, indent=2)

    # Save new cards api data
    new_cards_api_data = {
        "chs": new_cards_chs,
        "en": {k: en_items.get(k) for k in new_cards_chs.keys()},
        "jp": {k: jp_items.get(k) for k in new_cards_chs.keys()}
    }
    new_cards_json_path = os.path.join(output_dir, "new_cards_api_data.json")
    with open(new_cards_json_path, "w", encoding="utf-8") as f:
        json.dump(new_cards_api_data, f, ensure_ascii=False, indent=2)

    # 5. Extract relevant fields and download images
    print("Extracting data and gathering image URLs...")
    
    characters = []
    actions = []
    tokens = []
    images_to_download = []
    
    for card_id, chs_info in new_cards_chs.items():
        en_info = en_items.get(card_id, {})
        jp_info = jp_items.get(card_id, {})
        
        icon = chs_info.get("icon", "")
        
        # Download image
        if icon:
            image_url = f"https://gi.yatta.moe/assets/UI/gcg/{icon}.png"
            image_path = os.path.join(images_dir, f"{icon}.png")
            if not os.path.exists(image_path):
                images_to_download.append((image_url, image_path))
                
        # Determine card_category
        raw_type = chs_info.get("type", "")
        if "character" in raw_type.lower():
            card_category = "Character"
        elif "action" in raw_type.lower():
            card_category = "Action" # Or Token, user can manually adjust
        else:
            card_category = raw_type

        # Parse tags and props for auto-fill
        tags = chs_info.get("tags", {}) or {}
        props = chs_info.get("props", {}) or {}
        
        parsed_element = ""
        parsed_is_monster = ""
        parsed_type = ""
        parsed_cost_element = ""
        parsed_cost = ""

        if card_category == "Character":
            for tag in tags.keys():
                if tag in ELEMENT_TAGS:
                    parsed_element = ELEMENT_TAGS[tag]
                    break
            parsed_is_monster = "1" if any(tag in tags for tag in MONSTER_TAGS) else "0"
        else:
            for tag in tags.keys():
                if tag in TYPE_TAGS:
                    parsed_type = TYPE_TAGS[tag]
                    break
            if not parsed_type:
                parsed_type = "Event" if card_category == "Action" else "Token"
            
            for prop, val in props.items():
                if prop in COST_PROPS:
                    parsed_cost_element = COST_PROPS[prop]
                    parsed_cost = str(val)
                    break

        if card_category == "Character":
            characters.append({
                "id": card_id,
                "zh-HANS": normalize_name(chs_info.get("name", "")),
                "zh-HANS_short": "",
                "ja-JP": normalize_name(jp_info.get("name", "")),
                "ja-JP_short": "",
                "en-US": en_info.get("name", ""),
                "en-US_short": "",
                "element": parsed_element,
                "is_monster": parsed_is_monster,
                "talent_id": "",
                "share_id": "",
                "icon_name": icon,
                "avatar_name": f"avatar_{icon}"
            })
        elif card_category == "Action":
            actions.append({
                "id": card_id,
                "zh-HANS": normalize_name(chs_info.get("name", "")),
                "ja-JP": normalize_name(jp_info.get("name", "")),
                "en-US": en_info.get("name", ""),
                "type": parsed_type,
                "element": parsed_cost_element,
                "cost": parsed_cost,
                "snapshot_top": "",
                "share_id": "",
                "icon_name": icon
            })
        else:
            tokens.append({
                "id": card_id,
                "zh-HANS": normalize_name(chs_info.get("name", "")),
                "ja-JP": normalize_name(jp_info.get("name", "")),
                "en-US": en_info.get("name", ""),
                "type": parsed_type,
                "element": parsed_cost_element,
                "cost": parsed_cost,
                "snapshot_top": "",
                "icon_name": icon
            })

    if images_to_download:
        print(f"Downloading {len(images_to_download)} images...")
        def download_worker(item):
            url, path = item
            return download_image(url, path)

        with concurrent.futures.ThreadPoolExecutor(max_workers=16) as executor:
            futures = [executor.submit(download_worker, item) for item in images_to_download]
            for _ in tqdm(concurrent.futures.as_completed(futures), total=len(images_to_download), desc="Images", unit="img"):
                pass

    # 7. Fetch detail APIs to discover sub-card tokens (e.g. Transfiguration sub-cards)
    # that are absent from the bulk API but present in each card's `dictionary` as C-entries
    new_action_ids = [cid for cid, info in new_cards_chs.items()
                      if "action" in str(info.get("type", "")).lower()]
    token_candidates = []
    if new_action_ids:
        print(f"Fetching detail APIs for {len(new_action_ids)} new action cards to find sub-card tokens...")

        def fetch_detail(args):
            card_id, lang = args
            return card_id, lang, fetch_json(f"{base_api_url}/{lang}/gcg/{card_id}")

        detail_tasks = [(cid, lang) for cid in new_action_ids for lang in ["chs", "en", "jp"]]
        detail_data = {}
        with concurrent.futures.ThreadPoolExecutor(max_workers=8) as executor:
            futures = [executor.submit(fetch_detail, t) for t in detail_tasks]
            for _ in tqdm(concurrent.futures.as_completed(futures), total=len(futures), desc="Detail APIs", unit="req"):
                pass
            for fut in futures:
                card_id, lang, data = fut.result()
                if data and "data" in data:
                    detail_data[(card_id, lang)] = data["data"]

        existing_token_names = set()
        token_csv_path = os.path.join(cards_generated_dir, "tokens.csv")
        if os.path.exists(token_csv_path):
            with open(token_csv_path, "r", encoding="utf-8") as f:
                for row in csv.DictReader(f):
                    if "zh-HANS" in row:
                        existing_token_names.add(normalize_name(row["zh-HANS"]))

        for cid in new_action_ids:
            chs_detail = detail_data.get((cid, "chs"))
            if not chs_detail:
                print(f"Warning: detail API failed for card {cid}, sub-card tokens will be missing.")
                continue
            dictionary = chs_detail.get("dictionary") or {}
            token_keys = [k for k in dictionary if k.startswith("C")]
            if not token_keys:
                continue
            parent_is_blessing = "GCG_TAG_CARD_BLESSING" in (chs_detail.get("tags") or {})
            for tk in token_keys:
                entry = dictionary[tk]
                # Only NON_DISCOVERABLE C-entries are sub-card tokens; statuses/buffs
                # (e.g. Shield, RES) use other tags and must be excluded
                if "GCG_TAG_NON_DISCOVERABLE" not in (entry.get("tags") or {}):
                    continue
                element, cost_val = "", ""
                for prop, val in (entry.get("cost") or {}).items():
                    if prop in COST_PROPS:
                        element = COST_PROPS[prop]
                        cost_val = str(val)
                        break
                en_entry = ((detail_data.get((cid, "en")) or {}).get("dictionary") or {}).get(tk) or {}
                jp_entry = ((detail_data.get((cid, "jp")) or {}).get("dictionary") or {}).get(tk) or {}
                candidate = {
                    "id": "",
                    "zh-HANS": normalize_name(entry.get("name", "")),
                    "ja-JP": normalize_name(jp_entry.get("name", "")),
                    "en-US": en_entry.get("name", ""),
                    "type": "Transfiguration" if parent_is_blessing else "Token",
                    "element": element,
                    "cost": cost_val,
                    "snapshot_top": "",
                    "icon_name": ""
                }
                if candidate["zh-HANS"] and candidate["zh-HANS"] not in existing_token_names:
                    token_candidates.append(candidate)

    print(f"Successfully processed {len(characters)} characters, {len(actions)} actions, {len(tokens)} tokens"
          f" (+{len(token_candidates)} auto-extracted sub-card tokens).")

    # 6. Export to separate CSVs
    char_headers = ["id", "zh-HANS", "zh-HANS_short", "ja-JP", "ja-JP_short", "en-US", "en-US_short", "element", "is_monster", "talent_id", "share_id", "icon_name", "avatar_name"]
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
        writer.writerows(token_candidates)

    # Generate TODO list
    todo_path = os.path.join(output_dir, "TODO-list.md")
    todo_items = []
    todo_items.append("1. Fill in missing `share_id` in `characters.csv` and `actions.csv`.")
    todo_items.append("2. Fill in missing `element`, `type`, `cost`, `short_name`, and `snapshot_top` data.")
    todo_items.append("3. Review translations.")
    todo_items.append("4. Move any tokens misclassified as actions from `actions.csv` to `tokens.csv`.")
    if token_candidates:
        todo_items.append(
            "5. Sub-card tokens were auto-extracted into `tokens.csv` (names/element/cost filled). "
            "Fill in `icon_name` and `snapshot_top`, and add token card images to the `images` folder manually.")
    if characters:
        todo_items.append("6. Add avatar images for the following characters manually into the `images` folder:")
        for c in characters:
            todo_items.append(f"    - Name: {c['zh-HANS']}, Expected file: `{c['avatar_name']}.png`")
    with open(todo_path, "w", encoding="utf-8") as f:
        f.write("# TODO List for manual review\n\n")
        for i, item in enumerate(todo_items):
            f.write(f"- [ ] {item}\n")

    print(f"Generated CSVs and TODO list in {output_dir}")

if __name__ == "__main__":
    main()
