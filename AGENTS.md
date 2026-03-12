# LumiTracker Project Rules

**CRITICAL: You must adhere to the following rules for all tasks.**

## 1. Agent File Location Constraints

All agent-generated files MUST be placed in their designated subdirectories under `./agent/`. **NEVER** create these files in the repository root or anywhere else.

- **`./agent/openspec/`** : OpenSpec artifacts, changes, and specs. (Always run `openspec` commands with `workdir: ./agent` or from within `./agent/`).
- **`./agent/temp/`** : Temporary workspaces, scratchpads, intermediate test databases, and benchmark result outputs.
- **`./agent/scripts/`** : Reusable utility or test scripts written for the agent's autonomous workflows.

## 2. Project Structure

All source code is located under `./src`:
- **`LumiTracker`**: Main frontend app, C# .NET 8.0 WPF project.
- **`LumiTracker.Config`**: C# frontend common library (configurations, enums, etc).
- **`LumiTracker.Launcher`**: C++ launcher, performs environment checks.
- **`LumiTracker.OB`**: Observer sub-application, C# WPF (lower priority for feature development).
- **`LumiTracker.Watcher`**: Backend module. Contains C# API definitions and the Python computer vision backend logic (`src/LumiTracker.Watcher/watcher`).

## 3. Code Generation Conventions

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

## 4. Python Executable Environment Rules
- **System Python (Development):** Use the system Python executable (e.g. `python.exe` in PATH) for running development scripts, benchmarks, tests, and database generations. Third-party metric modules (like `scikit-learn`, `matplotlib`, etc.) can be installed and used freely in the system Python environment for benchmarking purposes.
- **Integrated Python (Runtime):** The final application uses a bundled standalone Python environment (`./python`). The core feature extraction algorithm (`src/LumiTracker.Watcher/watcher/feature.py`) **MUST ONLY** use modules that already exist in this integrated runtime (e.g., `cv2`, `numpy`). Do NOT add new third-party module dependencies to the runtime feature extraction algorithm without explicit approval.
