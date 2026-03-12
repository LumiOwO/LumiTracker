# LumiTracker Project Rules

**CRITICAL: You must adhere to the following rules for all tasks.**

## 1. Agent File Location Constraints

- **OpenSpec Artifacts:** When managing specs or proposing changes, you MUST operate within the `./agent` directory and NOT the repository root. All OpenSpec files and changes are located in `./agent/openspec`.
- **Temporary Files:** If you require a temporary workspace or scratchpad file, you MUST save those temporary files to `./agent/temp/`. Do NOT create temporary files in the repository root or anywhere else.

## 2. Code Generation Conventions

You MUST adhere to the project's established multi-language coding conventions.

### Commenting Conventions
- **Language & Character Set:** You MUST use only English and ASCII characters in comments.
- **Style:** Do not write novels in comments. Keep comments concise and professional.
- **Placement:** Only give comments on keypoint lines or complex logic. Let the code explain itself; do not rely on comments for straightforward logic.

### C# Conventions (`LumiTracker`, `LumiTracker.Config`, `LumiTracker.Watcher`)
- **Language/Framework:** C# 12 / .NET 8.0, WPF UI.
- **Brace Style:** Use **Allman style** (braces on a new line).
- **Indentation:** Use **4 spaces**. Do not use tabs.
- **Naming:** 
  - `PascalCase` for Classes, Structs, Interfaces (interfaces prefixed with `I`), Methods, and Properties.
  - `PascalCase` for Enums (prefixed with `E`, e.g., `EPackageType`).
  - `camelCase` or `_camelCase` for private fields and local variables.
- **Localization:** User-facing strings must use the localization engine (e.g., `Lang.UpdatePrompt_Fetching` or `LocalizationExtension.Create("Key")` in WPF XAML). Avoid hardcoded UI strings.
- **Logging:** Use `Configuration.Logger` for outputting state. Do not use `Console.WriteLine` directly.

### Python Conventions (`LumiTracker.Watcher/watcher`)
- **Indentation:** Use **4 spaces**. Do not use tabs.
- **Naming:** 
  - `snake_case` for modules, files, standalone functions, variables, and parameters.
  - `PascalCase` for Classes and methods inside classes (to mirror the C# API design, e.g., `def Start(...)`).
- **Logging/IPC:** Use `LogDebug`, `LogError`, or specific message wrappers provided in `config.py` rather than standard `print()` statements for IPC communication with C#.
