## Why

The initial implementation of the feature benchmark pipeline successfully executes, but has several architectural and methodological flaws that hinder autonomous optimization. First, the benchmark metrics are not clearly defined in the specs and are tightly coupled to the current implementation (AHash/DHash), making it hard to evaluate future algorithms. Second, the pipeline generates intermediate and result files in the project root instead of strictly utilizing the `./agent/temp` directory, violating `AGENTS.md` rules. Third, result files are overwritten on each run, preventing comparison between different algorithm versions. Fourth, the benchmark tests against a polluted database where edge cases are hardcoded. Finally, we must ensure that the benchmark's dynamic database rebuilds do not modify protected generated files like `_enums_gen.py`.

## What Changes

- Redefine benchmark metrics to be algorithm-agnostic (General Metrics) while maintaining implementation-specific details for reference.
- Enforce strict file location rules (`./agent/temp`) and implement result grouping/tagging for multi-version comparisons.
- Update the benchmark pipeline to dynamically generate a clean temporary `.ann` database in `./agent/temp` without modifying any production source or generated enum files.
- Remove redundant metric documentation from the code/JSON, maintaining it strictly in the OpenSpec documentation.
- Solidify the auto-loop test script for fully autonomous sub-agent delegation.

## Capabilities

### Modified Capabilities
- `feature-benchmark`: Add explicit metric definitions, enforce strict file location rules (`./agent/temp`), implement result grouping, and mandate an isolated database rebuild that protects production generated files.

## Impact

- **Affected code:** `src/LumiTracker.Watcher/watcher/benchmark.py`, `agent/scripts/test_auto_loop.py`, `dev_assets/[2.5] run_benchmark.bat`.
- **Systems:** The benchmark pipeline will become a highly reliable, fully autonomous, and version-comparable tool that allows for legitimate testing of new extraction methods without impacting production state.
