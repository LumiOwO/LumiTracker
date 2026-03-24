## MODIFIED Requirements

### Requirement: Independent Agent Auto-Loop Execution
The self-loop optimization process SHALL be executable exclusively by independent sub-agents. The MAIN agent MUST NOT perform the trial iterations itself, to ensure context limits are preserved in the main chat.

#### Scenario: Launching a sub-agent for optimization
- **WHEN** the main agent executes the auto-loop task (Task 5.2)
- **THEN** the main agent MUST use the `Task` tool to launch a fresh sub-agent, instructing it to run a batch of iterations (e.g., 3-5 trials). The prompt MUST explicitly state the prioritized optimization goals (Golden Cards = 100%, Sep Margin > 0, Time < 5ms, Accuracy > 99.5%). The prompt MUST dictate that for EACH trial in the batch, the sub-agent must:
  1. Create a unique trial directory using `mkdir -p agent/temp/runs/<trial_name>`.
  2. Write its experimental feature extraction code into a custom python script within that specific directory (e.g., `agent/temp/runs/<trial_name>/script.py`).
  3. Execute the benchmark from the repository root directory by explicitly setting PYTHONPATH, and passing the custom script and run directory: `PYTHONPATH=src/LumiTracker.Watcher python.exe -m watcher.benchmark.pipeline --use-sandbox --sandbox-file agent/temp/runs/<trial_name>/script.py --run-dir agent/temp/runs/<trial_name> --tag <trial_name> --hypothesis "<text>"`.
- **AND THEN** only AFTER the entire batch of target iterations is complete (or if the loop is aborted), the agent MUST run the summary tool `PYTHONPATH=src/LumiTracker.Watcher python.exe -m watcher.benchmark.summary` ONCE to evaluate all metrics, and then review `SUMMARY.md` to report the findings.

#### Scenario: Execution Constraints
- **WHEN** the sub-agent performs its task
- **THEN** it MUST strictly obey these execution constraints:
  1. It MUST NOT run exploratory commands (like `pipeline --help`) or single pre-tests before starting the requested batch of iterations. It must begin the iterations immediately.
  2. It MUST NOT modify `assets/config.json` or any production configuration files. All parameter tuning (like thresholds, hash sizes) MUST be hardcoded directly into the custom trial Python script.

### Requirement: Cross-Session State Recovery
The agent auto-loop SHALL be recoverable from a fresh chat session without losing context of the optimization progress.

#### Scenario: Resuming optimization in a new chat
- **WHEN** a user starts a new chat and asks to "resume the auto-loop for optimize-feature-algorithm"
- **THEN** the new agent MUST read the `openspec` context files (proposal, specs, tasks), parse `agent/temp/runs/SUMMARY.md` to understand the historical trend and current best metrics, and read the best performing trial's python script from its specific historical run directory before launching a new sub-agent for the next iteration.