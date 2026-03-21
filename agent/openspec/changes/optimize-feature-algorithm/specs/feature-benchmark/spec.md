## ADDED Requirements

### Requirement: Automated Feature Benchmark Pipeline
The system SHALL provide a modular benchmark pipeline package (`watcher/benchmark`) to quantitatively evaluate the image feature extraction algorithm against realistic game engine artifacts.

#### Scenario: Running the benchmark
- **WHEN** the benchmark pipeline is executed via the command line
- **THEN** it generates an evaluation report detailing statistical accuracy, processing time, and the core separation margin between inter-class and intra-class distances.

### Requirement: Realistic Data Augmentations
The benchmark pipeline SHALL apply geometric and lighting augmentations to the dataset to simulate real-world physical and UI variations found in the game environment.

#### Scenario: Simulating runtime UI scale and dynamic card artifacts
- **WHEN** the benchmark processes the dataset for intra-class evaluation
- **THEN** it MUST test images scaled down (e.g., to 0.5x or 0.3x), slightly translated, and layered with local glare or holographic noise textures.

### Requirement: Agent Strategy Sandbox
The benchmark pipeline SHALL support a Strategy pattern sandbox (`sandbox_impl.py`) allowing a separate extraction class to be loaded and tested without modifying the production `feature.py`.

#### Scenario: Testing an experimental algorithm
- **WHEN** the benchmark is configured to use the sandbox implementation
- **THEN** it redirects feature extraction requests through the `ExperimentalActionCardHandler` instead of the default production handler.

### Requirement: Pipeline Logging and Persistence
The benchmark pipeline SHALL save all results to a uniquely identifiable run directory to preserve historical iterations and prevent overwriting data.

#### Scenario: Running multiple benchmarks
- **WHEN** the benchmark pipeline runs
- **THEN** it creates a subdirectory (e.g., `runs/YYYYMMDD_HHMMSS_tag/`) containing the test database, the detailed `benchmark_<tag>.json` report, and a standard `run.log` of console output.

### Requirement: Cross-Run Summarization
The system SHALL provide a summary tool to compare various iterations of the sandbox algorithm against the original baseline.

#### Scenario: Evaluating optimization progress
- **WHEN** the summary tool is executed
- **THEN** it parses all historical run directories, establishes the "baseline" metrics, and outputs a formatted table comparing Separation Margin, Top-1 Accuracy, and Edge Case Accuracy across all runs.

### Requirement: Edge Case Extensibility
The benchmark pipeline SHALL test known hard edge cases, such as "golden card" variants, separately to ensure the algorithm can identify them without explicit database entries.

#### Scenario: Testing golden cards
- **WHEN** the benchmark runs the edge-case module
- **THEN** it specifically attempts to map the golden card images to their base card IDs using only the default feature vectors, reporting success or failure in the JSON output.
