## Context

The initial benchmark pipeline (`optimize-feature-algorithm`) correctly verified that a benchmark could run, but the results were output to the root directory, violating project rules, and lacked proper environmental setup (dynamic database generation) to actually prove algorithm improvements. We need to formalize the benchmark methodology to be robust, repeatable, cleanly separated, and fully integrated with our agent-driven workflow.

## Goals / Non-Goals

**Goals:**
- Enforce strict adherence to `AGENTS.md` by directing all generated/intermediate files to `./agent/temp`.
- Document clear definitions of all benchmark metrics.
- Ensure the benchmark dynamically rebuilds the Annoy database without edge case (golden) cards to evaluate the true raw performance of the feature algorithm on difficult images.
- Facilitate an autonomous test script (`agent/scripts/test_auto_loop.py`) that strictly depends on parsed metrics.

**Non-Goals:**
- We are not changing the core feature extraction algorithm logic in this specific change (that is a separate follow-up task based on the results of this optimized benchmark).

## Decisions

**Decision 1: File Output Redirection**
- All intermediate database builds (`.ann` files, JSONs) and the final `benchmark_results.json` will be written to `./agent/temp/`.
- **Rationale:** Ensures compliance with project rules and prevents polluting the main runtime database or project root with test artifacts.

**Decision 2: Dynamic Database Rebuild and Enum Protection**
- The benchmark script will trigger a programmatic build of the `.ann` database files specifically written to `./agent/temp/`. 
- This rebuild MUST use a mocked or locally scoped context that excludes golden cards.
- **CRITICAL:** The rebuild MUST NOT trigger the regeneration of `_enums_gen.py` or any other production source files. Production state must remain untouched.
- **Rationale:** If golden cards are left in the database, they act as explicit bypasses. Protecting enums ensures that the benchmark process does not inadvertently modify the project's source code.

**Decision 3: Result Grouping and Versioning**
- The benchmark will accept a `--tag` (or equivalent) parameter. Results will be saved as `benchmark_{tag}.json` in `./agent/temp/`.
- **Rationale:** Allows agents and humans to run multiple experiments and compare the JSON outputs side-by-side to verify if a change improved or degraded performance.

**Decision 4: Algorithm-Agnostic Metrics**
- The primary metrics in the output (e.g. `separation_margin`, `top1_accuracy`) will be defined based on the *best* performing feature component currently active in the algorithm.
- Implementation-specific details (like `_a` and `_d` for hashing) will be kept in a sub-object or tagged fields for diagnostic purposes but should not be the primary interface for the agent's optimization decisions.
- **Rationale:** Ensures the benchmark remains valid even if we move from Image Hashing to other methods (e.g., lightweight embeddings).

## Risks / Trade-offs

- **Risk:** Rebuilding the database during the benchmark could increase the total time it takes the sub-agent to run the test.
  → **Mitigation:** The database generation is relatively fast, and we can configure the benchmark to optionally load a pre-built temp database if we run multiple rapid tests.
