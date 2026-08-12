import argparse
import csv
import os
import shutil
import sys

def get_next_id(csv_path):
    if not os.path.exists(csv_path):
        return 0
    with open(csv_path, "r", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        rows = list(reader)
        if rows:
            return int(rows[-1]["id"]) + 1
    return 0

def validate_data(file_path, required_fields, is_token=False):
    if not os.path.exists(file_path):
        if is_token:
            return []
        print(f"Error: Required file not found: {file_path}")
        sys.exit(1)
        
    with open(file_path, "r", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        rows = list(reader)
        
    if is_token and not rows:
        return []
        
    for i, row in enumerate(rows):
        for field in required_fields:
            if not row.get(field) or not str(row.get(field)).strip():
                print(f"Validation Error in {file_path} at row {i+2}: '{field}' is missing or blank.")
                print("Please fix the errors and try again.")
                sys.exit(1)
    return rows

def append_to_csv(csv_path, rows, headers):
    file_exists = os.path.exists(csv_path)
    with open(csv_path, "a", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=headers, extrasaction='ignore')
        if not file_exists:
            writer.writeheader()
        for row in rows:
            writer.writerow(row)

def append_share_code(csv_path, share_code_rows):
    file_exists = os.path.exists(csv_path)
    headers = ["share_id", "name", "is_character", "internal_id"]
    with open(csv_path, "a", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=headers, extrasaction='ignore')
        if not file_exists:
            writer.writeheader()
        for row in share_code_rows:
            writer.writerow(row)

def main():
    parser = argparse.ArgumentParser(description="Update cards database with manually corrected data")
    parser.add_argument("version", type=str, help="Target version number (e.g. v6.4)")
    args = parser.parse_args()

    repo_root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    update_dir = os.path.join(repo_root, "agent", "temp", "updates", args.version)
    images_dir = os.path.join(update_dir, "images")
    generated_dir = os.path.join(repo_root, "cards", "generated")
    
    char_temp = os.path.join(update_dir, "characters.csv")
    action_temp = os.path.join(update_dir, "actions.csv")
    token_temp = os.path.join(update_dir, "tokens.csv")

    print(f"Validating CSVs in {update_dir}...")
    
    characters = validate_data(char_temp, ["share_id", "zh-HANS", "icon_name", "avatar_name"])
    actions = validate_data(action_temp, ["share_id", "zh-HANS", "type", "icon_name"])
    tokens = validate_data(token_temp, ["zh-HANS", "type", "icon_name"], is_token=True)

    print(f"Validation passed. Found {len(characters)} characters, {len(actions)} actions, {len(tokens)} tokens.")

    # Validation: Check share_id sequence (added share_ids must be consecutive)
    all_added_sids = []
    for card_list, list_name in [(characters, "Characters"), (actions, "Actions")]:
        for i, row in enumerate(card_list):
            sid_str = str(row.get("share_id", "")).strip()
            if not sid_str:
                print(f"Validation Error in {list_name} at row {i+2}: 'share_id' is missing or blank.")
                sys.exit(1)
            try:
                sid = int(sid_str)
            except ValueError:
                print(f"Validation Error in {list_name} at row {i+2}: 'share_id' ({sid_str}) is not a valid integer.")
                sys.exit(1)
            all_added_sids.append(sid)

    sorted_sids = sorted(all_added_sids)
    if sorted_sids and sorted_sids != list(range(sorted_sids[0], sorted_sids[0] + len(sorted_sids))):
        print(f"Validation Error: added share_ids must be consecutive ({sorted_sids[0]}..{sorted_sids[-1]}), "
              f"found gaps or duplicates: {all_added_sids}.")
        sys.exit(1)

    share_code_csv = os.path.join(generated_dir, "share_code.csv")
    existing_share_ids = set()
    if os.path.exists(share_code_csv):
        with open(share_code_csv, "r", encoding="utf-8") as f:
            reader = csv.DictReader(f)
            for row in reader:
                if row.get("share_id"):
                    existing_share_ids.add(row["share_id"].strip())

    share_code_updates = []
    has_artifact = False

    # Validation: Check for duplicate share_ids
    for card_list in [characters, actions]:
        for row in card_list:
            sid = str(row.get("share_id", "")).strip()
            if sid and sid in existing_share_ids:
                print(f"Fatal Error: Duplicate share_id '{sid}' found for card '{row.get('zh-HANS')}'.")
                print("This share_id already exists in share_code.csv. Please check the database manually.")
                sys.exit(1)
            if sid:
                existing_share_ids.add(sid) # Prevent duplicates within the same batch

    # Update Characters
    char_csv = os.path.join(generated_dir, "characters.csv")
    action_csv = os.path.join(generated_dir, "actions.csv")

    # Read existing characters so new talents can be appended to their talent_id
    char_headers = ["id", "zh-HANS", "zh-HANS_short", "ja-JP", "ja-JP_short", "en-US", "en-US_short", "element", "is_monster", "talent_id", "share_id"]
    existing_char_rows = []
    if os.path.exists(char_csv):
        with open(char_csv, "r", encoding="utf-8") as f:
            reader = csv.DictReader(f)
            existing_char_rows = list(reader)

    known_character_share_ids = set()
    for row in characters:
        known_character_share_ids.add(str(row.get("share_id", "")).strip())
    for row in existing_char_rows:
        if row.get("share_id"):
            known_character_share_ids.add(row["share_id"].strip())

    # Pre-compute new action internal ids so character talent_id can reference them
    next_action_id = get_next_id(action_csv)
    talent_ids_by_character = {}
    for idx, row in enumerate(actions):
        if row.get("type") == "Talent":
            csid = str(row.get("character_share_id", "")).strip()
            if not csid:
                print(f"Fatal Error: Talent card '{row.get('zh-HANS')}' is missing 'character_share_id'.")
                print("Please fill in the character_share_id for every talent card and try again.")
                sys.exit(1)
            if csid not in known_character_share_ids:
                print(f"Fatal Error: Talent card '{row.get('zh-HANS')}' has character_share_id '{csid}' "
                      "which does not match any existing or newly added character.")
                sys.exit(1)
            talent_ids_by_character.setdefault(csid, []).append(next_action_id + idx)

    next_char_id = get_next_id(char_csv)

    # Append new talent ids to existing characters' talent_id
    for row in existing_char_rows:
        csid = str(row.get("share_id", "")).strip()
        new_tids = talent_ids_by_character.get(csid)
        if not new_tids:
            continue
        existing_tids = [t.strip() for t in str(row.get("talent_id", "")).split(",") if t.strip()]
        for t in new_tids:
            if str(t) not in existing_tids:
                existing_tids.append(str(t))
        row["talent_id"] = ",".join(existing_tids)

    # Build new character rows
    if characters:
        for row in characters:
            icon = row.get("icon_name")
            zh_name = row.get("zh-HANS")
            share_id = row.get("share_id")
            
            row["id"] = next_char_id
            row["talent_id"] = ",".join(str(t) for t in talent_ids_by_character.get(str(share_id), []))
            
            # Migrate Image
            if icon and zh_name:
                src_img = os.path.join(images_dir, f"{icon}.png")
                if os.path.exists(src_img):
                    dest_img = os.path.join(repo_root, "cards", "characters", f"character_{next_char_id}_{zh_name}.png")
                    shutil.copy2(src_img, dest_img)
                    print(f"  Migrated image to {dest_img}".encode('gbk', 'replace').decode('gbk'))
                
                src_avatar = os.path.join(images_dir, f"{row.get('avatar_name')}.png")
                if os.path.exists(src_avatar):
                    dest_avatar = os.path.join(repo_root, "cards", "avatars", f"avatar_{next_char_id}_{zh_name}.png")
                    shutil.copy2(src_avatar, dest_avatar)
                    print(f"  Migrated avatar image to {dest_avatar}".encode('gbk', 'replace').decode('gbk'))

            share_code_updates.append({
                "share_id": share_id,
                "name": zh_name,
                "is_character": 1,
                "internal_id": next_char_id
            })
            next_char_id += 1

    # Write back all character rows (existing rows may have updated talent_id)
    all_char_rows = existing_char_rows + characters
    if all_char_rows:
        with open(char_csv, "w", encoding="utf-8", newline="") as f:
            writer = csv.DictWriter(f, fieldnames=char_headers, extrasaction='ignore')
            writer.writeheader()
            writer.writerows(all_char_rows)

    # Update Actions
    if actions:
        action_csv = os.path.join(generated_dir, "actions.csv")
        next_id = get_next_id(action_csv)

        for row in actions:
            icon = row.get("icon_name")
            zh_name = row.get("zh-HANS")
            share_id = row.get("share_id")
            type_val = row.get("type", "")
            
            if type_val == "Artifact":
                has_artifact = True
                
            row["id"] = next_id
            
            # Migrate Image
            if icon and zh_name:
                src_img = os.path.join(images_dir, f"{icon}.png")
                if os.path.exists(src_img):
                    dest_img = os.path.join(repo_root, "cards", "actions", f"action_{next_id}_{zh_name}.png")
                    shutil.copy2(src_img, dest_img)
                    print(f"  Migrated image to {dest_img}".encode('gbk', 'replace').decode('gbk'))

            share_code_updates.append({
                "share_id": share_id,
                "name": zh_name,
                "is_character": 0,
                "internal_id": next_id
            })
            next_id += 1

        action_headers = ["id", "zh-HANS", "ja-JP", "en-US", "type", "element", "cost", "snapshot_top", "share_id"]
        append_to_csv(action_csv, actions, action_headers)

    # Update Tokens
    if tokens:
        token_csv = os.path.join(generated_dir, "tokens.csv")
        next_id = get_next_id(token_csv)

        for row in tokens:
            icon = row.get("icon_name")
            zh_name = row.get("zh-HANS")
            
            row["id"] = next_id
            
            # Migrate Image
            if icon and zh_name:
                src_img = os.path.join(images_dir, f"{icon}.png")
                if os.path.exists(src_img):
                    dest_img = os.path.join(repo_root, "cards", "actions", "tokens", f"token_{next_id}_{zh_name}.png")
                    os.makedirs(os.path.dirname(dest_img), exist_ok=True)
                    shutil.copy2(src_img, dest_img)
                    print(f"  Migrated image to {dest_img}".encode('gbk', 'replace').decode('gbk'))
            next_id += 1

        token_headers = ["id", "zh-HANS", "ja-JP", "en-US", "type", "element", "cost", "snapshot_top"]
        append_to_csv(token_csv, tokens, token_headers)

    if share_code_updates:
        share_code_updates.sort(key=lambda r: int(r["share_id"]))
        append_share_code(share_code_csv, share_code_updates)
        print(f"Appended {len(share_code_updates)} entries to share_code.csv")

    print("Database update complete.")
    
    if has_artifact:
        print("\n" + "="*60)
        print("WARNING: Artifact card(s) detected in the update!")
        print("Please manually update './cards/generated/artifacts.csv' to maintain the correct sequential ordering of artifacts.")
        print("="*60 + "\n")

if __name__ == "__main__":
    main()
