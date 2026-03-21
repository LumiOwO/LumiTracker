## MODIFIED Requirements

### Requirement: Robust Card Feature Extraction
The image feature extraction algorithm SHALL produce robust hashes that uniquely identify cards, maximizing the separation margin between inter-class collisions and intra-class noise variations (such as game-engine scaling, local glare, or holographic effects).

The optimization goals MUST adhere to the following strict priority hierarchy during the automated tuning loop:

1. **Priority 1 (Absolute Constraint): Edge Case Accuracy**
   - The algorithm MUST achieve 100% accuracy on the Golden Cards edge cases. The auto-loop cannot stop until this is met.
2. **Priority 2 (Absolute Constraint): Real-time Performance**
   - The algorithm MUST execute in < 5.0 ms per frame on average.
3. **Priority 3 (Primary Optimization): Separation Margin**
   - The algorithm SHALL strive to achieve a Separation Margin > 0 to mathematically guarantee zero collisions.
4. **Priority 4 (Regression Prevention): Baseline Accuracy**
   - The algorithm MUST maintain a Top-1 Accuracy > 99.5% and F1 Score > 99.5% on the base dataset.

#### Scenario: Distinguishing similar cards under noise
- **WHEN** the algorithm processes a frame containing a card with slight scaling or lighting artifacts
- **THEN** it MUST correctly match the base card ID and maintain a feature distance lower than the closest mismatching card class, resulting in a strictly positive separation margin.

### Requirement: Real-time Feature Extraction Performance
The image feature extraction algorithm SHALL execute quickly enough to support real-time tracking during gameplay, even with advanced preprocessing or hashing methods.

#### Scenario: Processing a single frame
- **WHEN** the algorithm extracts features for a captured gameplay frame
- **THEN** the extraction process MUST complete within the real-time budget (e.g., < 10ms per frame, aiming for < 5ms), ensuring no frame drops or noticeable lag in the backend processing.
