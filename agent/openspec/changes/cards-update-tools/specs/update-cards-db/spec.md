## ADDED Requirements

### Requirement: Read Single Manually Fixed CSV
The system SHALL provide a Python script (`update_cards_db.py`) that reads the single manually corrected `temp_cards.csv` file from `./agent/temp/updates/{version-number}`.

#### Scenario: Executing the update script
- **WHEN** the update script is executed for a specific version
- **THEN** it reads the manually filled information, validating that required fields (`share_id`, `id`, exact types) are provided

### Requirement: Update the Specific Generated CSVs
The system SHALL distribute the data from the single `temp_cards.csv` into the appropriate specific database CSVs located in `./cards/generated`. Character data SHALL go to `characters.csv` (headers: `id`, `zh-HANS`, `zh-HANS_short`, `ja-JP`, `ja-JP_short`, `en-US`, `en-US_short`, `element`, `is_monster`, `talent_id`, `share_id`), and Action/Token data SHALL go to `actions.csv`/`tokens.csv` (headers: `id`, `zh-HANS`, `ja-JP`, `en-US`, `type`, `element`, `cost`, `snapshot_top`, `share_id`).

#### Scenario: Integrating the corrected data
- **WHEN** the script has read the corrected temporary data
- **THEN** it appends the new rows to the corresponding source CSVs in `./cards/generated` without modifying the original sorting or header structure

### Requirement: Move Image Assets to Final Directories
The system SHALL copy the downloaded images from the temporary `images/` directory to their correct final destinations under `./cards/`, renaming them according to the `database.py` format conventions.

#### Scenario: Copying images
- **WHEN** processing an Action card
- **THEN** the script moves the temp image to `./cards/actions/action_{id}_{zh-HANS}.png`
- **WHEN** processing a Character card
- **THEN** the script moves the temp image to `./cards/characters/character_{id}_{zh-HANS}.png`
- **WHEN** processing a Token card
- **THEN** the script moves the temp image to `./cards/actions/tokens/token_{id}_{zh-HANS}.png`

### Requirement: Preserve Excel Table Source
The system SHALL NOT modify the human-maintained Excel table (`./【七圣】多语言文本&卡牌数据.xlsx`).

#### Scenario: Completing the database update
- **WHEN** the script finishes updating the generated CSVs
- **THEN** the Excel table remains untouched, to be manually updated later
