## 1. Environment & Output Redirection

- [ ] 1.1 Modify `src/LumiTracker.Watcher/watcher/benchmark.py` to accept an output directory argument and strictly write results to `./agent/temp/`.
- [ ] 1.2 Implement result grouping using a `--tag` parameter to prevent overwriting outputs, and the result should be written to subdir grouped by tag.
- [ ] 1.3 Ensure metric explanations are removed from the JSON output (maintained in specs instead).

## 2. Dynamic Database Rebuild & Enum Protection

- [ ] 2.1 Refactor `Benchmark` to trigger a programmatic, headless build of database files in `./agent/temp/`.
- [ ] 2.2 Ensure the database build explicitly omits hardcoded golden cards.
- [ ] 2.3 **CRITICAL:** Verify that the test database rebuild does NOT trigger regeneration of `_enums_gen.py`.
- [ ] 2.4 Update the benchmark to load the isolated temp database for testing.

## 3. Algorithm-Agnostic Metrics & Auto-Loop

- [ ] 3.1 Define core general metrics (Separation Margin, Top-1 Accuracy, etc.) in the benchmark output that are independent of implementation details.
- [ ] 3.2 Update `agent/scripts/test_auto_loop.py` to trigger the benchmark and parse these general metrics.
- [ ] 3.3 Ensure the testing logic comprehensively processes ALL available cards to resolve `NaN` and sensitivity issues.
