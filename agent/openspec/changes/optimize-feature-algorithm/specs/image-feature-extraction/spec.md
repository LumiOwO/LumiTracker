## ADDED Requirements

### Requirement: Robust Card Feature Extraction
The image feature extraction algorithm SHALL produce robust hashes that uniquely identify cards, minimizing collisions between visually similar but distinct cards.

#### Scenario: Distinguishing similar cards
- **WHEN** the algorithm processes two different cards that share visual similarities
- **THEN** the resulting hash distance MUST be greater than the required threshold to avoid false positive matches.

### Requirement: Real-time Feature Extraction Performance
The image feature extraction algorithm SHALL execute quickly enough to support real-time tracking during gameplay.

#### Scenario: Processing a single frame
- **WHEN** the algorithm extracts features for a captured gameplay frame
- **THEN** the extraction process MUST complete within the real-time budget (e.g., < 10ms), ensuring no frame drops or noticeable lag in the backend processing.
