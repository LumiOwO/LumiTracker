## 1. Benchmark Pipeline Package Structure

- [x] 1.1 Refactor the existing `benchmark.py` into a package structure `src/LumiTracker.Watcher/watcher/benchmark/`.
- [x] 1.2 Move the core evaluation logic (Distance matching, JSON output saving) into `watcher/benchmark/pipeline.py`.
- [x] 1.3 Create `watcher/benchmark/__init__.py` to expose the pipeline entry point.
- [x] 1.4 Extract the default production feature extraction class wrapper into `watcher/benchmark/default_impl.py`.

## 2. Advanced Augmentations

- [x] 2.1 Create `watcher/benchmark/augmentor.py` and move existing brightness/blur/noise augmentations.
- [x] 2.2 Implement `Scale` augmentation to simulate runtime UI sizing (e.g. downscaling to 0.5x).
- [x] 2.3 Implement `Translation` augmentation to offset the image by a few pixels, simulating dynamic card effects.
- [x] 2.4 Implement `Local Glare` (Shining) augmentation to overlay angled bright gradients on portions of the card.
- [x] 2.5 Implement `Holographic Noise` augmentation to overlay high-frequency textures on the image.

## 3. Agent Sandbox Setup

- [x] 3.1 Create `watcher/benchmark/pipeline.py` sandbox execution logic that accepts a custom standalone python script via `--sandbox-file`.
- [x] 3.2 Modify `pipeline.py` to accept arguments to toggle between testing `default_impl.py` and the custom sandbox file.
- [x] 3.3 Verify the custom sandbox script can be dynamically loaded and tested without breaking the pipeline execution or overwriting previous trials.

## 4. Edge Case Module

- [x] 4.1 Update `pipeline.py` to load the golden cards specifically as a separate test suite.
- [x] 4.2 Add logic to score the algorithm's ability to accurately identify golden cards against their base variations.
- [x] 4.3 Ensure the benchmark outputs a comprehensive JSON including `separation_margin` and `edge_case_accuracy`.
- [x] 4.4 Update the pipeline to save all DB, JSON, and `run.log` outputs to unique `runs/<timestamp>_<tag>` directories.
- [x] 4.5 Create a `watcher/benchmark/summary.py` script to generate a markdown comparison table of all historical runs vs the baseline.

## 5. Agent Auto-Loop Execution

- [x] 5.1 Document the instructions for the agent (metrics to maximize, file to edit, constraints to obey).
- [ ] 5.2 Execute the agent Auto-Loop via Sub-Agents: The MAIN agent MUST NOT perform trials itself. Instead, use the `Task` tool to launch a sub-agent and instruct it to perform a batch of iterations (e.g., 3-5 trials). For each trial, the sub-agent MUST create a unique directory `agent/temp/runs/<trial_name>`, write its custom script there, and run the pipeline from the project root using `PYTHONPATH=src/LumiTracker.Watcher python.exe -m watcher.benchmark.pipeline --use-sandbox --sandbox-file <path> --run-dir <path> --hypothesis "<text>"`. The sub-agent MUST NOT perform exploratory pre-tests or modify `assets/config.json`. The `watcher.benchmark.summary` tool MUST ONLY be run ONCE after the entire batch of iterations is complete. Repeat this sub-agent delegation process until Golden Cards = 100% and Separation Margin > 0.
- [ ] 5.3 Review the winning configuration proposed by the agent.

## 6. Finalization

- [ ] 6.1 Copy the winning logic from the best trial's custom script into the production `watcher/feature.py`.
- [ ] 6.2 Ensure `feature.py` uses only integrated runtime modules (`cv2`, `numpy`).
- [ ] 6.3 Run a final baseline test to ensure production performance < 5ms and no regressions.
