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

## 5. Generate Single Temporary CSV File

- [x] 5.1 Format the extracted data into a single Pandas DataFrame containing a union of all necessary columns.
- [x] 5.2 Include exact CSV headers for Characters: `zh-HANS_short`, `ja-JP_short`, `en-US_short`, `is_monster`, `talent_id`.
- [x] 5.3 Include exact CSV headers for Actions: `snapshot_top`, `cost`, `type`.
- [x] 5.4 Include common headers: `card_category` (Character/Action/Token), `id` (populated with website ID), `share_id` (leave blank), `zh-HANS`, `ja-JP`, `en-US`, `element`, `icon_name`.
- [x] 5.5 Export the DataFrame to a single file: `./agent/temp/updates/{version}/temp_cards.csv`.

## 6. Setup Database Update Tool

- [x] 6.1 Create the python script file (`./agent/scripts/update_cards_db.py`) for Task 2.
- [x] 6.2 Implement argument parsing to accept the target version number.
- [x] 6.3 Implement logic using Pandas to read the manually corrected `temp_cards.csv` from `./agent/temp/updates/{version}/`.

## 7. Implement Database Updating Logic

- [x] 7.1 Iterate through the corrected rows and split them into Characters, Actions, and Tokens based on `card_category`.
- [x] 7.2 For Characters, extract fields and append to `./cards/generated/characters.csv`.
- [x] 7.3 For Actions, extract fields and append to `./cards/generated/actions.csv`.
- [x] 7.4 For Tokens, extract fields and append to `./cards/generated/tokens.csv`.
- [x] 7.5 Check if `./cards/generated/share_code.csv` and `./cards/generated/language.csv` should be updated automatically or warn the user to handle them if required.

## 8. Migrate Downloaded Images to Target Subdirectories

- [x] 8.1 For each row in the manually corrected CSV, construct the source image path: `./agent/temp/updates/{version}/images/{icon_name}.png`.
- [x] 8.2 Move/Copy the image to `./cards/characters/character_{id}_{zh-HANS}.png` if it is a Character.
- [x] 8.3 Move/Copy the image to `./cards/actions/action_{id}_{zh-HANS}.png` if it is an Action.
- [x] 8.4 Move/Copy the image to `./cards/actions/tokens/token_{id}_{zh-HANS}.png` if it is a Token.

## 9. Validation and Testing

- [ ] 9.1 Run the `fetch_cards_data.py` script with `v6.4` to generate the temp data.
- [ ] 9.2 Manually fill the `share_id` for a few cards as a test to simulate user behavior.
- [ ] 9.3 Run the `update_cards_db.py` script.
- [ ] 9.4 Execute `python src/LumiTracker.Watcher/watcher/database.py` (which builds the db) to verify the pipeline throws no errors and maintains database integrity.
