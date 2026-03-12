## Why

The project currently lacks explicit documented rules and coding specifications for AI agents or contributors. Establishing standardized project rules and AI agent conventions (like specifying directory locations) ensures that development and maintenance operations are consistent and predictable, minimizing the chance of agent-generated errors.

## What Changes

- Add a centralized project rules and coding specification document.
- Explicitly define that all Agent spec files belong in `./agent`.
- Explicitly define that any agent-generated temporary files must be saved to `./agent/temp`.
- Document project structure, core frameworks (C# .NET 8.0, WPF UI, Python computer vision), and styling conventions as established in the current codebase.

## Capabilities

### New Capabilities
- `project-rules`: Establishes the foundational project rules, AI agent guidelines, and coding specifications for the LumiTracker project.

### Modified Capabilities
None

## Impact

- **Documentation**: New project rules files will be added.
- **Agent Workflow**: All agents operating in this repository must abide by these rules, specifically path constraints (`./agent`, `./agent/temp`).
- **Codebase**: No direct logic changes, but shapes future implementation and structural conventions.
