# Care Approval IQ — self-assessment vs. the evaluator scorecard

**Purpose:** re-score against the independent architect-track review
(`../review/EVALUATOR-REVIEW-RESPONSE.md`) after each batch of work, so effort
targets the lowest defensible score rather than random polish. Not a real judge's
score — a calibration exercise.

The official Agent-a-thon rubric is only Innovation / Usability / Impact (30 each,
see `../../../tireforge/docs/progress/JUDGING-SELF-ASSESSMENT.md` for the rubric
text). The evaluator's 10-dimension card below is the finer-grained instrument we
use to steer.

---

## Re-score #1 — 2026-09-06 (after all P0 + slice 4 orchestrator)

| Dimension | Review (start) | Now | Why it moved / what caps it |
|---|---:|---:|---|
| Problem / Impact | 9.0 | 9.0 | Unchanged — still strong. Cap: business-value figure not yet measured (P2-1 / P2-2). |
| Architecture | 8.5 | 8.7 | Orchestrator now built exactly to TD-1/TD-2 (hub + per-spoke activities + inline Gate), tested via `FakeOrchestrationContext`. Cap: no infra, no `Zynara.Data`. |
| Microsoft Foundry usage | 8.0 | 8.0 | Unchanged — Critic added as a 4th hosted agent + provisioner, but not demonstrated live end-to-end yet. |
| **Multi-agent justification** | **6.5** | **8.0** | Critic agent adds genuine agent-to-agent challenge (D5); TDD §3.1 "why multi-agent beats one generalist"; **measured** agents-vs-generalist comparison in CI (route agreement 100% vs 50%, unsafe automation 0 vs 2). Cap: comparison uses deterministic stubs, not live models. |
| Safety / Responsible AI | 8.5 | 9.0 | Structured decision model — mandatory criteria are a hard gate, `Abstain` route added (D4); Critic can force review/abstain over perfect numbers; appeals never auto-submit; CI hard-gates unsafe automation = 0 and mandatory FN = 0. |
| Technical execution | 8.5 | 8.7 | 57 tests green; eval suite; orchestrator. Cap: two `StubEvidenceGapAgent` nullable warnings; no CI pipeline yet. |
| **Evaluation / evidence** | **6.5** | **8.5** | `eval/Zynara.Eval` — the full metric suite (evidence P/R, mandatory FN, citation accuracy, hallucination count, route agreement, safe-abstention, unsafe-automation) + generalist baseline, 8 labelled cases, CI-gated. Cap: 8 cases is thin; no before/after manual baseline (P2-2). |
| UX / demo potential | 8.0 | 8.0 | Unchanged — `Zynara.Dashboard` / `Zynara.ApiProxy` still empty. This is now the lowest-hanging fruit (P1-1 / P1-2). |
| Innovation | 8.0 | 8.2 | Precedent-driven appeal + the Critic-as-challenger pattern. Cap: precedent panel not yet visible. |
| Production credibility | 8.0 | 8.2 | Production gaps enumerated in TDD §7.1; interim in-memory store is explicitly labelled. Cap: RBAC / audit / agent-identity isolation not built (P1-3 / P1-4). |

**Weighted read:** the two flagged weaknesses (multi-agent justification, evaluation)
have moved from 6.5 → ~8.0–8.5. The new floor is **UX / demo (8.0)** — nothing
visible yet — followed by Foundry usage (8.0), both addressed by the P1 demo
slice (dashboard + precedent panel + a live Critic moment).

**Next lever, in order:** P1-2 + P1-1 (dashboard) → P2-5 (demo script) →
P2-1 / P2-2 (measured business value) → P1-5 → P1-3 / P1-4.
