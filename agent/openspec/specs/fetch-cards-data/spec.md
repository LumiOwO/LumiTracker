# fetch-cards-data Specification

## Purpose
TBD - created by syncing change cards-update-tools. Update Purpose after sync.
## Requirements
### Requirement: Diff API Against Local Database
The system SHALL provide a Python script (`fetch_cards_data.py`) that queries the bulk endpoint `https://gi.yatta.moe/api/v2/chs/gcg`. It SHALL read the local `cards/generated/` CSV files to extract existing card names, and identify any cards from the API not present in the local database.

#### Scenario: Script execution
- **WHEN** the script is executed
- **THEN** it finds the diff between the web API and local CSVs to discover newly added cards without needing browser automation

### Requirement: Multi-language Data Retrieval via Bulk API
The system SHALL fetch detailed structured card information in three languages by querying the bulk endpoint `https://gi.yatta.moe/api/v2/{lang}/gcg` for `chs`, `en`, and `jp` and extracting localized name, type, icon, tags, and cost properties.

#### Scenario: API Retrieval
- **WHEN** new cards are identified
- **THEN** the script matches them across the language APIs to aggregate the translations

### Requirement: Temporary CSVs Generation & Output Artifacts
The system SHALL store the fetched data in the `./agent/temp/updates/{version-number}` directory by generating 3 separate files: `actions.csv`, `characters.csv`, and `tokens.csv`. Their headers MUST exactly match the local database. If no tokens exist, `tokens.csv` will contain only the header row. It SHALL also save the raw JSON API response to `raw_api_data.json` and generate a concise `TODO-list.md` advising the user on required manual steps.

#### Scenario: Generating the CSVs
- **WHEN** data is successfully fetched
- **THEN** the script creates the 3 separate CSV files, saves the raw JSON, and provides the TODO-list.

### Requirement: Download Uncompressed Card Images
The system SHALL download the corresponding card images from the website using the `icon` field from the API, formatted as `https://gi.yatta.moe/assets/UI/gcg/{icon}.png`. Since avatar images cannot be reliably fetched programmatically, the system SHALL define a filename pattern (`avatar_{icon_name}.png`) and instruct the user via the `TODO-list.md` to manually download them.

#### Scenario: Image retrieval process
- **WHEN** an `icon` name is discovered from the API response
- **THEN** the script downloads the uncompressed 420x720 PNG image and saves it in `./agent/temp/updates/{version-number}/images/{icon}.png`, and adds instructions to the TODO list for the avatar image.
