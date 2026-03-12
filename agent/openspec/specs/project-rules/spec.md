# project-rules Specification

## Purpose
TBD - created by archiving change project-rules. Update Purpose after archive.
## Requirements
### Requirement: Agent File Location Constraints
The AI agent SHALL strictly use designated directories for its operations.

#### Scenario: Creating OpenSpec artifacts
- **WHEN** the agent proposes a change or manages specs
- **THEN** it must operate within the `./agent` path and NOT the repository root.

#### Scenario: Creating temporary files
- **WHEN** the agent requires a temporary workspace or scratchpad
- **THEN** it must save those temporary files to `./agent/temp`.

### Requirement: Code Generation Conventions
The AI agent SHALL adhere to the project's established multi-language coding conventions.

#### Scenario: Modifying C# files
- **WHEN** the agent writes C# code in `LumiTracker` or `LumiTracker.Config`
- **THEN** it must use C# 12/.NET 8.0 conventions, Allman brace style, `PascalCase` for types/methods, `_camelCase` for private fields, and avoid hardcoded strings by using `LumiTracker.Config` localization.

#### Scenario: Modifying Python files
- **WHEN** the agent writes Python code in `LumiTracker.Watcher/watcher`
- **THEN** it must use 4-space indentation, `snake_case` for files/modules/variables, and utilize `config.LogDebug` or `config.LogError` for IPC logging.

