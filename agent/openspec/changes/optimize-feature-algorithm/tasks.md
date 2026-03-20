## 1. Benchmark Pipeline Package Structure

- [ ] 1.1 Refactor the existing `benchmark.py` into a package structure `src/LumiTracker.Watcher/watcher/benchmark/`.
- [ ] 1.2 Move the core evaluation logic (Distance matching, JSON output saving) into `watcher/benchmark/pipeline.py`.
- [ ] 1.3 Create `watcher/benchmark/__init__.py` to expose the pipeline entry point.
- [ ] 1.4 Extract the default production feature extraction class wrapper into `watcher/benchmark/default_impl.py`.

## 2. Advanced Augmentations

- [ ] 2.1 Create `watcher/benchmark/augmentor.py` and move existing brightness/blur/noise augmentations.
- [ ] 2.2 Implement `Scale` augmentation to simulate runtime UI sizing (e.g. downscaling to 0.5x).
- [ ] 2.3 Implement `Translation` augmentation to offset the image by a few pixels, simulating dynamic card effects.
- [ ] 2.4 Implement `Local Glare` (Shining) augmentation to overlay angled bright gradients on portions of the card.
- [ ] 2.5 Implement `Holographic Noise` augmentation to overlay high-frequency textures on the image.

## 3. Agent Sandbox Setup

- [ ] 3.1 Create `watcher/benchmark/sandbox_impl.py` containing an empty/pass-through `ExperimentalActionCardHandler` class.
- [ ] 3.2 Modify `pipeline.py` to accept arguments to toggle between testing `default_impl.py` and `sandbox_impl.py`.
- [ ] 3.3 Verify the `sandbox_impl` can be safely overwritten by an agent without breaking the pipeline execution.

## 4. Edge Case Module

- [ ] 4.1 Update `pipeline.py` to load the golden cards specifically as a separate test suite.
- [ ] 4.2 Add logic to score the algorithm's ability to accurately identify golden cards against their base variations.
- [ ] 4.3 Ensure the benchmark outputs a comprehensive JSON including `separation_margin` and `edge_case_accuracy`.

## 5. Agent Auto-Loop Execution

- [ ] 5.1 Document the instructions for the agent (metrics to maximize, file to edit, constraints to obey).
- [ ] 5.2 Execute the agent Auto-Loop: Agent reads baseline JSON -> writes `sandbox_impl.py` -> runs pipeline -> repeats until `separation_margin` > 0 and golden cards pass.
- [ ] 5.3 Review the winning configuration proposed by the agent.

## 6. Finalization

- [ ] 6.1 Copy the winning logic from `sandbox_impl.py` into the production `watcher/feature.py`.
- [ ] 6.2 Ensure `feature.py` uses only integrated runtime modules (`cv2`, `numpy`).
- [ ] 6.3 Run a final baseline test to ensure production performance < 5ms and no regressions.
