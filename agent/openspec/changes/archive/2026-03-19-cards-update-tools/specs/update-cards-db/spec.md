## ADDED Requirements

### Requirement: Read and Validate Manually Fixed CSVs
The system SHALL provide a Python script (`update_cards_db.py`) that reads the manually corrected `actions.csv`, `characters.csv`, and `tokens.csv` files, as well as the local `./cards/generated/share_code.csv`. It SHALL perform validation to ensure required fields (like `share_id` or `type`) are present. If invalid data is found, it SHALL print an error, tell the user to fix it, and early return. It SHALL skip processing `tokens.csv` if it is empty (only contains headers).

#### Scenario: Executing the update script
- **WHEN** the update script is executed for a specific version
- **THEN** it reads the manually filled CSVs and aborts if validation fails.

### Requirement: Update the Specific Generated CSVs & Assign IDs
The system SHALL distribute the validated data into the appropriate specific database CSVs located in `./cards/generated`. For each new card, it SHALL read the existing local target CSV to automatically calculate and assign the next sequential internal `id`. It SHALL append the new cards to `share_code.csv`, mapping the provided `share_id` to the newly calculated internal `id`. If any new card is identified as an Artifact (`type == "Artifact"`), the system SHALL print a prominent warning instructing the user to manually update `artifacts.csv` to maintain sequential order.

#### Scenario: Integrating the corrected data
- **WHEN** the script has read and validated the data
- **THEN** it appends the new rows to the corresponding source CSVs in `./cards/generated` with the correct sequentially calculated `id`, updates `share_code.csv`, and triggers warnings for Artifacts if needed.

### Requirement: Move Image Assets to Final Directories
The system SHALL copy the downloaded images from the temporary `images/` directory to their correct final destinations under `./cards/`, renaming them using the newly assigned local sequential `internal_id` according to the `database.py` format conventions.

#### Scenario: Copying images
- **WHEN** processing an Action card
- **THEN** the script moves the temp image to `./cards/actions/action_{internal_id}_{zh-HANS}.png`
- **WHEN** processing a Character card
- **THEN** the script moves the temp image to `./cards/characters/character_{internal_id}_{zh-HANS}.png`. If a manually added avatar image exists at `avatar_{icon_name}.png`, it moves it to `./cards/avatars/avatar_{internal_id}_{zh-HANS}.png`.
- **WHEN** processing a Token card
- **THEN** the script moves the temp image to `./cards/actions/tokens/token_{internal_id}_{zh-HANS}.png`

### Requirement: Preserve Excel Table Source
The system SHALL NOT modify the human-maintained Excel table (`./【七圣】多语言文本&卡牌数据.xlsx`).

#### Scenario: Completing the database update
- **WHEN** the script finishes updating the generated CSVs
- **THEN** the Excel table remains untouched, to be manually updated later
