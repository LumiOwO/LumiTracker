## MODIFIED Requirements

### Requirement: Independent Agent Auto-Loop Execution
The self-loop optimization process SHALL be executable by independent sub-agents, ensuring that context limits are preserved in the main chat and the iteration history is cleanly tracked.

#### Scenario: Launching a sub-agent for optimization
- **WHEN** the main agent delegates the auto-loop task (Task 5.2)
- **THEN** a fresh sub-agent is launched via the `Task` tool with a specific prompt outlining the optimization constraints, current metrics from `SUMMARY.md`, and strict instructions for execution. The instructions MUST dictate that the agent:
  1. Creates a unique trial directory using `mkdir -p agent/temp/runs/<trial_name>`.
  2. Writes its experimental feature extraction code into a custom python script within that specific directory (e.g., `agent/temp/runs/<trial_name>/script.py`).
  3. Executes the benchmark by explicitly passing the custom script and run directory: `python.exe -m watcher.benchmark.pipeline --use-sandbox --sandbox-file agent/temp/runs/<trial_name>/script.py --run-dir agent/temp/runs/<trial_name> --tag <trial_name> --hypothesis "<text>"`.
  4. Runs the summary tool `python.exe -m watcher.benchmark.summary` immediately after the benchmark finishes.
  5. Reviews `SUMMARY.md` to determine if the iteration succeeded or failed.

### Requirement: Cross-Session State Recovery
The agent auto-loop SHALL be recoverable from a fresh chat session without losing context of the optimization progress.

#### Scenario: Resuming optimization in a new chat
- **WHEN** a user starts a new chat and asks to "resume the auto-loop for optimize-feature-algorithm"
- **THEN** the new agent MUST read the `openspec` context files (proposal, specs, tasks), parse `agent/temp/runs/SUMMARY.md` to understand the historical trend and current best metrics, and read the best performing trial's python script from its specific historical run directory before launching a new sub-agent for the next iteration.