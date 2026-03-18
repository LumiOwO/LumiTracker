## 1. Setup Data Fetching Tool

- [x] 1.1 Create the python script file (`./agent/scripts/fetch_cards_data.py`) for Task 1.
- [x] 1.2 Implement argument parsing using `argparse` to accept the target version number (e.g., `v6.4`) to determine the output folder.

## 2. Identify New Cards via Diffing

- [x] 2.1 Read the existing local CSV files (`./cards/generated/actions.csv`, `characters.csv`, `tokens.csv`).
- [x] 2.2 Create a set of existing card names using the `zh-HANS` column from the local databases.
- [x] 2.3 Make an HTTP request to the bulk JSON API endpoint: `https://gi.yatta.moe/api/v2/chs/gcg`.
- [x] 2.4 Iterate through the API items. If a card's name is NOT in the local existing names set, mark it as a new card.

## 3. Fetch Detailed Card Data

- [x] 3.1 Fetch the bulk JSON API endpoints for English and Japanese: `.../en/gcg`, `.../jp/gcg`.
- [x] 3.2 For each identified new card, extract relevant fields across languages: `name`, `type`, `tags` (element, weapon, nation), `props` (HP, Energy, Cost), and `icon`.
- [x] 3.3 Ensure the script extracts the website's ID from the API and maps it directly to the CSV `id` field.

## 4. Download Uncompressed Card Images

- [x] 4.1 Construct the image URL using the `icon` field from the API: `https://gi.yatta.moe/assets/UI/gcg/{icon}.png`.
- [x] 4.2 Download the uncompressed PNG image.
- [x] 4.3 Validate the image is downloaded successfully and save it to `./agent/temp/updates/{version}/images/{icon}.png`.

## 5. Generate Temporary Output Files

- [x] 5.1 Format the extracted data into separate Pandas DataFrames for Characters, Actions, and Tokens.
- [x] 5.2 Ensure headers match the local `./cards/generated` database exactly.
- [x] 5.3 Export the DataFrames to `actions.csv`, `characters.csv`, and `tokens.csv` in `./agent/temp/updates/{version}/`. If no tokens exist, create `tokens.csv` with only the header row.
- [x] 5.4 Save the raw JSON api response to `raw_api_data.json` in the same temp directory.
- [x] 5.5 Generate a concise `TODO-list.md` instructing the user to manually fill blank cells (like `share_id`), download avatar images to the temp folder naming them `avatar_{icon_name}.png`, and update translations.

## 6. Setup Database Update Tool

- [x] 6.1 Create the python script file (`./agent/scripts/update_cards_db.py`) for Task 2.
- [x] 6.2 Implement argument parsing to accept the target version number.
- [x] 6.3 Implement logic using Pandas to read the manually corrected `actions.csv`, `characters.csv`, `tokens.csv`, and the local `share_code.csv`.

## 7. Implement Database Updating Logic

- [x] 7.1 Add validation to check for missing required fields (like `share_id` or `type`). Print an error and early return if invalid data is found. Skip `tokens.csv` if it's empty (only has headers).
- [x] 7.2 Read the local target CSVs to automatically calculate the next sequential internal `id` for each new card.
- [x] 7.3 Append the validated data to `./cards/generated/characters.csv`, `actions.csv`, and `tokens.csv`.
- [x] 7.4 Automatically append new cards to `./cards/generated/share_code.csv` mapping `share_id` to the newly calculated internal `id` and `is_character` flag.
- [x] 7.5 Check if any new card has `type == "Artifact"` and print a prominent warning instructing the user to manually update `artifacts.csv` due to its strict manual ordering.

## 8. Migrate Downloaded Images to Target Subdirectories

- [x] 8.1 For each row, construct the source image path: `./agent/temp/updates/{version}/images/{icon_name}.png`.
- [x] 8.2 Move/Copy the image to `./cards/characters/character_{internal_id}_{zh-HANS}.png` if it is a Character.
- [x] 8.3 Move/Copy the image to `./cards/actions/action_{internal_id}_{zh-HANS}.png` if it is an Action.
- [x] 8.4 Move/Copy the image to `./cards/actions/tokens/token_{internal_id}_{zh-HANS}.png` if it is a Token.
- [x] 8.5 Check if an avatar image exists at `./agent/temp/updates/{version}/images/avatar_{icon_name}.png` and migrate it to `./cards/avatars/avatar_{internal_id}_{zh-HANS}.png`.

## 9. Validation and Testing

- [x] 9.1 Test validation failures: Intentionally omit `share_id` for a fetched card and ensure `update_cards_db.py` gracefully aborts with an error without corrupting data.
- [x] 9.2 Run `fetch_cards_data.py v6.4` to generate the 3 temp CSVs and `TODO-list.md`.
- [x] 9.3 Prepare manual data: Copy missing field data from the `.bak` files into the temp CSVs to simulate a user filling them out.
- [x] 9.4 Prepare mock avatars: Find the existing correct avatar images from `./cards/avatars/` and copy them to the temp images folder, renaming them to the `avatar_{icon_name}.png` pattern.
- [x] 9.5 Run `update_cards_db.py v6.4` to execute the pipeline.
- [x] 9.6 Execute `python src/LumiTracker.Watcher/watcher/database.py` to verify database integrity.
- [x] 9.7 Diff the final generated databases against the `.bak` files to ensure a 100% exact match in rows and values.
