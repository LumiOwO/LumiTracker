## MODIFIED Requirements

### Requirement: Robust Card Feature Extraction
The image feature extraction algorithm SHALL produce robust hashes that uniquely identify cards, maximizing the separation margin between inter-class collisions and intra-class noise variations (such as game-engine scaling, local glare, or holographic effects).

#### Scenario: Distinguishing similar cards under noise
- **WHEN** the algorithm processes a frame containing a card with slight scaling or lighting artifacts
- **THEN** it MUST correctly match the base card ID and maintain a feature distance lower than the closest mismatching card class, resulting in a strictly positive separation margin.

### Requirement: Real-time Feature Extraction Performance
The image feature extraction algorithm SHALL execute quickly enough to support real-time tracking during gameplay, even with advanced preprocessing or hashing methods.

#### Scenario: Processing a single frame
- **WHEN** the algorithm extracts features for a captured gameplay frame
- **THEN** the extraction process MUST complete within the real-time budget (e.g., < 10ms per frame, aiming for < 5ms), ensuring no frame drops or noticeable lag in the backend processing.
