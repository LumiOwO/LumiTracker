## Why

Currently, updating the database with new cards is a manual and error-prone process. These new Python tools will automate the retrieval of card information and high-quality images from the web, and streamline the integration of this data into the main `./cards` database after a manual review step.

## What Changes

- Introduce a script to fetch new card data from `https://gi.yatta.moe/chs/changelog?v={version}`.
- Automatically download uncompressed PNG card images (420x720 resolution).
- Fetch localized data for Chinese (chs), English (en), and Japanese (jp).
- Store fetched data into temporary CSV files in `./agent/temp/updates/{version}` with temporary IDs.
- Introduce a second script to take the manually corrected temporary CSV data (which maps temporary IDs to real share IDs) and update the main `./cards` database.

## Capabilities

### New Capabilities
- `fetch-cards-data`: Handles fetching new card information and images from the web, storing them in a temporary structure.
- `update-cards-db`: Handles reading the manually corrected temporary data and applying the updates to the main `./cards` database.

### Modified Capabilities

## Impact

- Adds new utility scripts to the project to automate card updates.
- Temporary files will be generated in `./agent/temp/updates/`.
- Modifies the contents of the `./cards` directory (specifically the CSV files and potentially image folders) after the update step. No changes to the `generate.py` or the human-maintained Excel file.
