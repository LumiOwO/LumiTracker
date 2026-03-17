## ADDED Requirements

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

### Requirement: Single Temporary CSV Generation
The system SHALL store the fetched data in the `./agent/temp/updates/{version-number}` directory in a single file named `temp_cards.csv`. The CSV MUST contain columns encompassing both Character and Action/Token formats. It SHALL use the website's ID as the `id` column but leave `share_id` blank.

#### Scenario: Generating the CSV
- **WHEN** data is successfully fetched
- **THEN** the script creates one `temp_cards.csv` file that maps the website ID and leaves `share_id` blank for manual filling

### Requirement: Download Uncompressed Card Images
The system SHALL download the corresponding card images from the website using the `icon` field from the API, formatted as `https://gi.yatta.moe/assets/UI/gcg/{icon}.png`.

#### Scenario: Image retrieval process
- **WHEN** an `icon` name is discovered from the API response
- **THEN** the script downloads the uncompressed 420x720 PNG image and saves it in `./agent/temp/updates/{version-number}/images/{icon}.png`
