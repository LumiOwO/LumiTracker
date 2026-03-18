## Why

Currently, updating the database with new cards is a manual and error-prone process. These new Python tools will automate the retrieval of card information and high-quality images from the web, and streamline the integration of this data into the main `./cards` database after a manual review step.

## What Changes

- Introduce a script to fetch new card data by diffing the bulk API against the local database.
- Store fetched data into 3 separate temporary CSV files (`actions.csv`, `characters.csv`, `tokens.csv`) matching the local database format, and save the raw API data.
- Download uncompressed PNG card images (420x720). Since avatar images cannot be reliably fetched programmatically, prompt the user via a generated `TODO-list.txt` to add them manually to the temporary folder.
- Introduce a second script to validate the manually corrected CSVs, update the main `./cards` database (including `share_code.csv`), and migrate the images using the local sequential `internal_id`.
- Notify the user if `artifacts.csv` requires manual updates for new artifact cards.

## Capabilities

### New Capabilities
- `fetch-cards-data`: Handles fetching new card information and images from the web, storing them in a temporary structure.
- `update-cards-db`: Handles reading the manually corrected temporary data and applying the updates to the main `./cards` database.

### Modified Capabilities

## Impact

- Adds new utility scripts to the project to automate card updates.
- Temporary files will be generated in `./agent/temp/updates/`.
- Modifies the contents of the `./cards` directory (specifically the CSV files and potentially image folders) after the update step. No changes to the `generate.py` or the human-maintained Excel file.
