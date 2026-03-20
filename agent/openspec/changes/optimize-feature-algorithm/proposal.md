## Why

The current image feature algorithm for card detection uses image hashing (PHash/DHash/AHash) to ensure real-time performance. However, manual tuning of "magic numbers" for the hash algorithms has plateaued, and it still struggles with edge cases (like dynamic golden cards, glares, or scaling artifacts). To solve this, we need an automated benchmark pipeline and a self-optimizing "Auto-Loop" workflow. This allows an AI agent to continuously test new structural changes, crop regions, and preprocessing steps within a sandbox, safely searching for a configuration that guarantees zero collisions without breaking the production pipeline.

## What Changes

- Introduce a comprehensive benchmark pipeline module (`watcher.benchmark`) capable of simulating real-world game engine artifacts (scale, translation, glare, holographic noise).
- Establish an isolated `sandbox_impl.py` where an AI agent can experiment with feature extraction logic without modifying the production code.
- Implement an Auto-Loop Agent Workflow that iteratively modifies the sandbox, runs the benchmark, and optimizes the feature extraction algorithm based on quantitative metrics (e.g., maximizing separation margin).
- Improve the primary feature algorithm (`feature.py`) based on the final winning configuration found by the agent.

## Capabilities

### New Capabilities
- `feature-benchmark`: A robust benchmark pipeline with image augmentation (scale, glare, noise) and Sandbox strategy to quantitatively grade the feature algorithm and facilitate an agent-driven auto-loop.

### Modified Capabilities
- `image-feature-extraction`: The underlying algorithm responsible for generating hashes from card images and calculating distances, which will be upgraded via the self-optimizing loop.

## Impact

- **Affected code:** `src/LumiTracker.Watcher/watcher/feature.py` (eventually), and the introduction of a new `src/LumiTracker.Watcher/watcher/benchmark/` package.
- **Dependencies:** The benchmark will use external libraries like `scikit-learn` in the system Python environment, while the core algorithm remains restricted to the runtime environment (`cv2`, `numpy`).
- **Systems:** Improves card identification accuracy and robustness against game UI variations, lighting, and scaling, while maintaining real-time execution speeds.
