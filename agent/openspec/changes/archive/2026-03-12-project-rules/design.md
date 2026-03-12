## Context

LumiTracker is an AI-assisted project using Gen-AI agents for spec-driven development. It is structured as a multi-language project (C# WPF frontend, Python CV backend, C++ launcher). To prevent AI agents from creating files in arbitrary paths and polluting the repository, explicit rules are necessary. 

## Goals / Non-Goals

**Goals:**
- Provide clear directory constraints for all Agent spec files (`./agent`).
- Provide an explicit temporary directory for agent workspaces (`./agent/temp`).
- Document the overarching architectural patterns to align AI generation with existing styles.

**Non-Goals:**
- Enforcing human contributor coding styles (this is primarily for AI consistency).
- Modifying any application logic.

## Decisions

- **Agent Constraints location**: Stored as OpenSpec project rules. The agent must acknowledge that it cannot write specs to `.opencode/` or root `/openspec`, but explicitly to `./agent/openspec`.
- **Temp file location**: Designated `./agent/temp/` as the scratchpad to keep root directory clean. 
- **Coding Specifications**: The C# code uses C# 12/.NET 8.0, PascalCase for classes/methods/enums (prefixed with `E`), and `camelCase`/`_camelCase` for fields. Python code uses `snake_case` mostly but classes are `PascalCase`. The design choice here is to encode these directly into a summary prompt or spec that the agent reads.

## Risks / Trade-offs

- [Risk] Agents ignoring the rules -> Mitigation: Reinforce rules by explicitly including them as project context in the OpenSpec repository.
