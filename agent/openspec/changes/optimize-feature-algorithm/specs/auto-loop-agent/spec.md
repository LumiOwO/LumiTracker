## ADDED Requirements

### Requirement: Independent Agent Auto-Loop Execution
The self-loop optimization process SHALL be executable by independent sub-agents, ensuring that context limits are preserved in the main chat and the iteration history is cleanly tracked.

#### Scenario: Launching a sub-agent for optimization
- **WHEN** the main agent delegates the auto-loop task (Task 5.2)
- **THEN** a fresh sub-agent is launched via the `Task` tool with a specific prompt outlining the optimization constraints, current metrics from `SUMMARY.md`, and the requirement to strictly rewrite `sandbox_impl.py`.

### Requirement: Cross-Session State Recovery
The agent auto-loop SHALL be recoverable from a fresh chat session without losing context of the optimization progress.

#### Scenario: Resuming optimization in a new chat
- **WHEN** a user starts a new chat and asks to "resume the auto-loop for optimize-feature-algorithm"
- **THEN** the new agent MUST read the `openspec` context files (proposal, specs, tasks), parse `agent/temp/runs/SUMMARY.md` to understand the historical trend and current best metrics, and read the current `sandbox_impl.py` before continuing the iteration.