## Context

Currently, LumiTracker uses image hashing (AHash, DHash, PHash) via a Python backend (`LumiTracker.Watcher/watcher/feature.py`) to quickly identify game cards and controls in real-time. The algorithm's quality is evaluated manually by reading logs (`a.txt`) generated during database updates, which lists image hash distances and identifies collisions where hash distances fall below an acceptable threshold. The current performance is acceptable but suffers from edge case collisions and lacks a structured benchmark. We need an automated benchmark pipeline to quantitatively score the algorithm's robustness, and we need to use this benchmark to tweak the algorithm (e.g., hash sizing, crop areas, or preprocessing) to achieve better accuracy without losing the real-time speed advantage.

## Goals / Non-Goals

**Goals:**
- Create a standalone benchmark pipeline that evaluates the accuracy and robustness of the image feature algorithm.
- The benchmark should simulate real-world noise (e.g., blur, brightness changes, contrast shifts) to evaluate robustness.
- Improve the hash distance margins between different cards (reduce collisions).
- Maintain real-time performance of the feature algorithm.

**Non-Goals:**
- Replacing the image hashing approach entirely with a slow, complex Neural Network.
- Changing the C# frontend's interaction with the watcher.

## Decisions

**Decision 1: Benchmark Pipeline as a Python Module**
- We will add a new benchmark script, e.g., `watcher.benchmark`, runnable via the system Python executable (similar to the database update process).
- **Rationale:** Keeping the benchmark within the `watcher` package allows it to easily import the feature extraction logic, `config`, and mock the `Database` class. It simplifies execution and integrates seamlessly with existing `dev_assets` workflows.
- **Alternatives Considered:** A standalone batch script or external testing framework. Rejected because keeping it native to the Python package allows direct API testing of the `feature.py` functions without IPC overhead.

**Decision 2: Benchmark Metrics**
- The benchmark will compute a robust suite of image retrieval metrics:
  1. **Separation Margin:** Minimum Inter-Class Distance - Maximum Intra-Class Distance. A positive margin guarantees no collisions even under noisy conditions.
  2. **Top-1 and Top-K Accuracy:** Evaluates if the true card is ranked at the very top or within the closest `K` neighbors.
  3. **Precision, Recall, and F1-Score:** Measures the trade-off between false positives (collisions with wrong cards) and false negatives (failing to recognize a valid card variation).
  4. **ROC-AUC / PR-AUC:** Provides an aggregate score across all possible distance threshold values.
- **Rationale:** While separation margin is a strict heuristic, statistical metrics (like Top-K accuracy and F1) provide a better overall picture of the algorithm's capability. We can use third-party libraries like `scikit-learn` in our system Python environment to calculate these efficiently.

**Decision 3: Feature Optimization and Environment Rules**
- We will experiment with adjusting the `MultiPHash` parameters, adding or modifying image preprocessing steps (like adaptive histogram equalization), and optimizing the cropped regions.
- **Critical Constraint:** `feature.py` MUST only use modules available in the bundled Python runtime (e.g., `cv2`, `numpy`). New third-party module dependencies (like `scipy` or `scikit-image`) are strictly forbidden in `feature.py`.
- **Rationale:** The runtime environment is constrained to keep the distributed application size small and execution fast.

**Decision 4: Extensible Edge Case Testing (Golden Cards)**
- The benchmark pipeline will include an extensible module for specifically testing known edge cases.
- We will use the existing "golden cards" (which currently have to be manually added to the database as extras) as our primary edge case test suite.
- **Rationale:** The ultimate goal of optimizing the feature algorithm is to eliminate the need for manually inserting edge-case images into the main database. By making the golden cards formal benchmark test cases, we can prove the algorithm handles them naturally. The system will be designed so more edge cases can be easily added over time.

## Risks / Trade-offs

- **Risk:** Increasing hash size for better accuracy might slow down the database lookup (AnnoyIndex) and the hash extraction time.
  → **Mitigation:** Benchmark pipeline will also track and report the average processing time per frame to ensure it stays within real-time budgets (e.g., < 10ms per frame).
- **Risk:** Preprocessing (like blur or equalization) might wash out subtle card details needed for distinction.
  → **Mitigation:** Rely strictly on the objective benchmark scores to decide if a preprocessing step is beneficial.
