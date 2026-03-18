## ADDED Requirements

### Requirement: Test Edge Cases and Validation

The system SHALL provide detailed manual testing instructions or automated tests to ensure the edge cases of the database update pipeline are handled gracefully. The testing scenarios MUST cover the following cases:

1. **Validation Abort on Incomplete Data**:
   - Manually clear a required cell (e.g., `share_id` or `type`) in the temporary CSV.
   - Run `update_cards_db.py`.
   - Ensure the script prints a clear error message indicating which row/field is invalid and exits without making any changes to the database.

2. **Empty Tokens File Handling**:
   - Ensure `tokens.csv` in the temp folder only contains headers with no data rows.
   - Run `update_cards_db.py`.
   - Ensure the script processes Characters and Actions correctly and safely skips `tokens.csv` without throwing parsing errors.

3. **Artifact Cards Warning**:
   - Add a dummy card to the temporary `actions.csv` with `type = "Artifact"`.
   - Run `update_cards_db.py`.
   - Ensure the script prints a prominent warning in the console at the end of its run, instructing the user to manually update `artifacts.csv` to maintain sequential order.

4. **Sequential ID and Share Code Mapping Calculation**:
   - Verify that the updated rows in `characters.csv` and `actions.csv` are assigned the correct sequential `internal_id` based on the previous maximum ID in the file.
   - Verify that `share_code.csv` is correctly updated with the new `share_id` mapping to the correct `internal_id` and the correct `is_character` flag.

5. **Image Migration and Avatar Fallback**:
   - Place a dummy avatar image matching the `avatar_{icon_name}.png` pattern in the temp `images` folder.
   - Run `update_cards_db.py`.
   - Verify that standard card images are migrated to their respective folders with the new sequential `internal_id`.
   - Verify that the dummy avatar image is migrated successfully to `./cards/avatars/avatar_{internal_id}_{zh-HANS}.png`.

6. **Version 6.4 End-to-End Integration Test**:
   - Start with a "clean" local database by ensuring all v6.4 cards (e.g., Ineffa, Goldflame Qucusaur Tyrant, Kuuvahki Experimental Design Bureau, etc.) are removed from `characters.csv`, `actions.csv`, and `share_code.csv`.
   - Run `fetch_cards_data.py v6.4`.
   - Check the `TODO-list.md` and populate the temp CSV files by copying the accurate properties from the `.bak` files (which serve as the "ground truth" answer key).
   - Simulate manual avatar downloading by copying existing correct avatars from `./cards/avatars/` to the temp images folder, named as `avatar_{icon_name}.png`.
   - Run `update_cards_db.py v6.4`.
   - Run `database.py` and verify it succeeds with no fatal index mapping errors.
   - Verify that the resulting `./cards/generated/*.csv` files perfectly match the `.bak` files.
