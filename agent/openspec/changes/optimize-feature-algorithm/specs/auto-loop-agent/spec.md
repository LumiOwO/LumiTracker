## MODIFIED Requirements

### Requirement: Independent Agent Auto-Loop Execution
The self-loop optimization process SHALL be executable by independent sub-agents, ensuring that context limits are preserved in the main chat and the iteration history is cleanly tracked.

#### Scenario: Launching a sub-agent for optimization
- **WHEN** the main agent delegates the auto-loop task (Task 5.2)
- **THEN** a fresh sub-agent is launched via the `Task` tool with a specific prompt outlining the optimization constraints, current metrics from `SUMMARY.md`, and instructions to create a unique trial folder in `agent/temp/runs/` to store its specific Python script and execute the benchmark passing `--run-dir` and `--sandbox-file`.

### Requirement: Cross-Session State Recovery
The agent auto-loop SHALL be recoverable from a fresh chat session without losing context of the optimization progress.

#### Scenario: Resuming optimization in a new chat
- **WHEN** a user starts a new chat and asks to "resume the auto-loop for optimize-feature-algorithm"
- **THEN** the new agent MUST read the `openspec` context files (proposal, specs, tasks), parse `agent/temp/runs/SUMMARY.md` to understand the historical trend and current best metrics, and read the best performing trial's python script before launching a new sub-agent for the next iteration.