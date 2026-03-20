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

### Requirement: Edge Case Extensibility
The benchmark pipeline SHALL test known hard edge cases, such as "golden card" variants, separately to ensure the algorithm can identify them without explicit database entries.

#### Scenario: Testing golden cards
- **WHEN** the benchmark runs the edge-case module
- **THEN** it specifically attempts to map the golden card images to their base card IDs using only the default feature vectors, reporting success or failure in the JSON output.
