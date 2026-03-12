## Why

The current image feature algorithm for card detection is barely good enough for edge cases and lacks a proper benchmark system to quantify its quality. Because the app must process events in real-time, the feature algorithm must be extremely fast, leading to the use of image-hashing (PHash/DHash/AHash). However, manual tuning of "magic numbers" for the hash algorithms has plateaued. We need a solid automated benchmark pipeline to accurately measure algorithm performance (such as intra-card feature distance and inter-card collision frequency), and we need to use this benchmark to optimize the feature algorithm's accuracy without degrading its speed.

## What Changes

- Introduce a new benchmark pipeline script for the feature extraction algorithm, capable of automated runs.
- The pipeline will establish objective benchmark scores evaluating the hash distances (e.g., maximizing distance between different cards while minimizing distance for augmented versions of the same card).
- Enhance the image feature algorithm (e.g., `feature.py`) to improve the benchmark grades.
- Potential modifications to perceptual hash sizing, region cropping sizes, image pre-processing (like adaptive equalization or filtering) before hashing.

## Capabilities

### New Capabilities
- `feature-benchmark`: A benchmark pipeline and evaluation system to quantitatively grade the image feature algorithm's accuracy, robustness, and performance.

### Modified Capabilities
- `image-feature-extraction`: The underlying algorithm responsible for generating hashes from card images and calculating differences.

## Impact

- **Affected code:** `src/LumiTracker.Watcher/watcher/feature.py`, `src/LumiTracker.Watcher/watcher/database.py`, and potentially the introduction of a new standalone benchmark script in `dev_assets/` or `src/LumiTracker.Watcher/watcher/test/`.
- **Dependencies:** May require standard image augmentation libraries (e.g. within `cv2` or `numpy`) for benchmark generation.
- **Systems:** The card identification accuracy will improve, directly enhancing the reliability of the backend python app during game tracking.
