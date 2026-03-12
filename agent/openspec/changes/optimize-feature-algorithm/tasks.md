## 1. Benchmark Pipeline Implementation

- [x] 1.1 Create `src/LumiTracker.Watcher/watcher/benchmark.py` script, ensuring it runs on the system Python executable and relies on libraries like `scikit-learn` for metrics.
- [x] 1.2 Implement image augmentation utilities (e.g., adding blur, changing brightness/contrast) to simulate real-world noise in `benchmark.py`.
- [x] 1.3 Build the evaluation logic to compute Precision, Recall, F1-Score, Top-K Accuracy, and Separation Margin.
- [x] 1.4 Add an extensible Edge Case Test module, specifically integrating the "golden cards" (currently handled as extras in the database) to evaluate edge-case robustness.
- [x] 1.5 Add execution time profiling within the benchmark to track hashing performance.
- [x] 1.6 Ensure the benchmark outputs results in a machine-readable format (e.g., JSON) to facilitate agent parsing.
- [x] 1.7 Test the agent auto-loop pipeline: Create a sub-agent to run the benchmark and parse the results, verifying that the AI agent can autonomously evaluate and iteratively optimize the algorithm.
- [x] 1.8 Create a new batch script `dev_assets/[2.5] run_benchmark.bat` to easily execute the benchmark (ensuring it runs correctly under WSL1 environments by appropriately calling `cmd.exe /c` or the correct Windows python path).

## 2. Feature Algorithm Optimization

- [ ] 2.1 Experiment with tuning the hash sizes and crop regions in `src/LumiTracker.Watcher/watcher/feature.py` against the benchmark. (Remember: `feature.py` MUST NOT import third-party modules outside the integrated runtime's environment like `cv2` or `numpy`).
- [ ] 2.2 Explore adding lightweight preprocessing steps (such as adaptive histogram equalization or sharpening) before hashing.
- [ ] 2.3 Iteratively run the benchmark to find the configuration that maximizes statistical accuracy and Separation Margin, while correctly classifying the golden cards without explicit database inclusion.
- [ ] 2.4 Verify processing times remain within the < 10ms per frame real-time budget.
- [ ] 2.5 Finalize the optimal configuration parameters in `feature.py` and optionally remove the hardcoded golden cards from the database if they are no longer needed.
