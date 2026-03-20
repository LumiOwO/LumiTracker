## Context

Currently, LumiTracker uses image hashing (AHash, DHash, PHash) via a Python backend (`LumiTracker.Watcher/watcher/feature.py`) to quickly identify game cards and controls in real-time. The algorithm struggles with edge cases such as dynamic golden cards (local shining/glare, texture noise, slight offsets) and scale differences in various UI states, leading to overlapping distance scores and inevitable collisions. We need an automated benchmark pipeline and an agent-driven auto-loop to quantitatively score the algorithm's robustness against these exact artifacts, and allow an AI agent to freely experiment with new feature extraction logic in an isolated Sandbox.

## Goals / Non-Goals

**Goals:**
- Create a `watcher/benchmark` package containing the benchmark pipeline, data augmentations, and a Strategy pattern Sandbox (`sandbox_impl.py`).
- Implement rigorous augmentations in the benchmark: Scale, Translation, Local Glare (Shining), and Local Holographic Noise (Texture), to simulate game engine realities.
- Establish an Agent Auto-Loop workflow where the AI reads benchmark metrics, modifies `sandbox_impl.py`, and iterates until positive separation margins are achieved and golden cards are correctly matched.
- Maintain real-time performance of the finalized feature algorithm.

**Non-Goals:**
- Replacing the image hashing approach entirely with a slow, complex Neural Network.
- Changing the C# frontend's interaction with the watcher.

## Decisions

**Decision 1: Benchmark Pipeline as a Package (`watcher/benchmark`)**
- We will refactor the benchmark into a module package with `pipeline.py`, `augmentor.py`, `default_impl.py`, and `sandbox_impl.py`.
- **Rationale:** Separating the augmentations and the sandbox implementation from the core pipeline makes the architecture clean and allows the agent to safely overwrite `sandbox_impl.py` without risk of breaking the pipeline.

**Decision 2: Extensive Augmentations for Realistic Simulation**
- The `ImageAugmentor` will simulate physical edge cases found in the game: Scale Down (for UI variations), Translation/Offset, Local Glare, and Holographic Texture Noise.
- **Rationale:** The golden cards fail primarily due to these local, non-global artifacts. Simulating them explicitly forces the agent's algorithm to become invariant to them.

**Decision 3: Agent Sandbox (Strategy Pattern)**
- The benchmark will execute feature extraction through an interface. The default uses `feature.py`. The agent will write an `ExperimentalActionCardHandler` class in `sandbox_impl.py`.
- **Rationale:** This creates a strict boundary. The agent is free to completely rewrite how crops are merged, what preprocessing is done (e.g. CLAHE, edge hashing), and what hash combinations are used, without touching production code until the solution is proven.

**Decision 4: Feature Optimization Constraints**
- The sandbox must only use modules available in the bundled Python runtime (e.g., `cv2`, `numpy`). New third-party module dependencies (like `scipy` or `scikit-image`) are strictly forbidden.
- **Rationale:** The runtime environment is constrained to keep the distributed application size small and execution fast.

## Risks / Trade-offs

- **Risk:** The agent might find an algorithm that perfectly overfits the augmentations but slows down extraction time significantly.
  → **Mitigation:** The benchmark pipeline tracks `avg_extraction_time_ms`. The agent is instructed to keep this under 5ms (or strictly < 10ms) and will penalize/reject solutions that take too long.
- **Risk:** Agent looping can be slow and costly if the search space is undirected.
  → **Mitigation:** Provide the agent with clear metrics (Separation Margin) and explicit tuning directions (e.g., ignoring DC component, CLAHE, edge detection) to accelerate the search.
