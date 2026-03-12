## MODIFIED Requirements

### Requirement: Automated Feature Benchmark
The system SHALL provide a benchmark pipeline to quantitatively evaluate the image feature extraction algorithm, strictly writing all output and temporary files to `./agent/temp/`.

#### Scenario: Running multiple benchmarks
- **WHEN** the developer runs the benchmark with a unique tag (e.g., `--tag v1`)
- **THEN** the system saves results to `./agent/temp/benchmark_v1.json` without overwriting previous runs.

### Requirement: Benchmark Metrics Evaluation
The benchmark pipeline SHALL calculate statistical metrics to measure algorithm quality. Metrics SHALL be categorized into **General Metrics** (algorithm-independent) and **Implementation Metrics** (details like hash distances).

#### General Metric Definitions:
- **Separation Margin:** The primary indicator of classification safety. A value > 0 means the algorithm can theoretically classify all tested images without any collisions. 
- **Top-1 Accuracy:** The overall success rate of the algorithm's primary prediction.
- **Precision/Recall/F1-Score:** Standard statistical measures of classification accuracy.
- **Edge Case Accuracy:** Success rate on difficult "golden card" images.
- **Edge Case Avg Distance:** The average distance of golden cards to their true identity.
- **Max Extraction Time:** Ensures real-time constraints are met.

#### Implementation Metrics:
- Details specific to the current algorithm (e.g., Min Inter-Class Distance for specific hash components).

#### Scenario: Computing statistical metrics
- **WHEN** the benchmark processes the dataset
- **THEN** it outputs a JSON containing both General and Implementation metrics.

### Requirement: Protected Environment Testing
The benchmark pipeline SHALL support testing against a "pure" database without modifying production source or generated files.

#### Scenario: Rebuilding database for test
- **WHEN** the benchmark starts
- **THEN** it generates a temporary testing database in `./agent/temp/` that omits edge-case solutions, ensuring `_enums_gen.py` and other generated source files are NOT modified.
