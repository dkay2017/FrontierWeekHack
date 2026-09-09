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

---

## Note — 2026-09-06 (after P1-1 + P1-2)

`Zynara.ApiProxy` + `Zynara.Dashboard` landed (D10): the evidence-first review
card, the precedent panel, the Critic surface, the Gate decision model, the
human-control bar, and an offline demo dataset. Provisional deltas:

- **UX / demo potential 8.0 → 8.5** — the reviewer workspace exists and is the
  shape the review asked for (evidence and precedent lead, not the approve
  button). Cap: not yet run through a Functions host / deployed; no scripted
  demo (P2-5).
- **Multi-agent justification 8.0 → 8.3** — the Critic is now *visible* to the
  reviewer as a distinct challenger with its own flags, not just an internal
  signal.
- **Innovation 8.2 → 8.4** — the precedent panel (match %, won-on-appeal badges,
  matched facts, "drove the recommendation") is now demonstrable.

New floor: **Microsoft Foundry usage (8.0)** — still no hosted-agent run shown
end-to-end. Then Production credibility (8.2, RBAC/audit/isolation).

---

## Judging Q&A — anticipated questions (V3.1 re-eval §12, §13, §20)

Prep for the demo/judging conversation. Keep the answers this tight.

**Q · Why five agents? Why not one?**
Five *materially different reasoning responsibilities* over unstructured text:
resolve an ambiguous plan rule; map a free-text note to written criteria; extract
and reconcile clinical claims; find and reason about comparable precedent;
adversarially challenge the assembled case. One prompt doing all five is worse at
each, and gives the Critic nothing independent to push against. **Measured** (24
labelled cases, `eval/Zynara.Eval/BASELINE.md`): the pipeline agrees with the
expert labels 100% vs **62.5%** for a credible one-pass GPT-5.4 generalist given
the same inputs and a safety-focused prompt; **0 unsafe automations vs 4**; safe
abstention **4/4 vs 1/4**. Not a straw man — the generalist was told to never
auto-submit and to abstain on thin evidence, and still did neither reliably.

**Q · Why not a sixth agent?**
The remaining work is deterministic — expiry-date math, policy diffing, precedent
similarity ranking, threshold/value checks, and the Gate routing. That is plain
code (D6). An agent there would swap an auditable, testable calculation for a
non-deterministic one. A sixth agent would weaken the architecture, not strengthen it.

**Q · Does the AI submit claims automatically?**
No. The Gate determines *readiness*; a human authorises every outbound action; only
then does the Submission Adapter execute. Every send pauses at the review port
(D34/D35). `READY_TO_SUBMIT` is the Gate's assessment, not an action.

**Q · Will the appeal win?**
The system does not predict outcomes. It surfaces the arguments the payer's *own
history* shows have succeeded in comparable denied cases, with the precedents and
clauses cited, for a human to decide.

**Q · The four review questions (§20):**
1. *Do the agents decide better together than one generalist?* — §3.1 argument +
   the eval suite's measured comparison (route agreement, mandatory FN rate, safe
   abstention vs. a single-prompt baseline).
2. *Does the system know when it is uncertain?* — the explicit `Abstain` route (D4);
   safe-abstention rate is a hard CI gate.
3. *Why should a human trust it?* — every criterion shows evidence, source, clause
   and version; every precedent shows why it is comparable; the Critic's challenge
   is visible; the Gate decision model is shown, not a black-box score.
4. *Is the value real?* — Estimated Recoverable Value: `not-appealed ×
   comparable-win-rate × mean-claim-value`, every input on the surface, confidence
   set by sample size. A number the payer can check, not a marketing figure.
