# Agent Scripts

Reusable utility scripts for autonomous workflows. Run all scripts from the **project root**.

## TCG Cards Update Pipeline

### 1. Fetch Data
Diffs API vs local DB and downloads card art.
```bash
python agent/scripts/fetch_cards_data.py <version>
```
- **Output**: `./agent/temp/updates/<version>/`
- **Action**: Check `TODO-list.md`. Fill `share_id` and missing cells in temp CSVs.
- **Avatars**: Place manual avatars in `images/` as `avatar_{icon_name}.png`.

### 2. Update Database
Validates temp CSVs and migrates data/images to `./cards/`.
```bash
# Set encoding if Unicode errors occur in terminal
PYTHONIOENCODING=utf-8 python agent/scripts/update_cards_db.py <version>
```

---
*Other scripts in this directory are specialized utilities for agent-specific tasks.*
