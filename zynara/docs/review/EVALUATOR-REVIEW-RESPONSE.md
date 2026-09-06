# Independent Evaluator Review — response & plan

Source: `../design/Care_Approval_IQ_Agent_a_Thon_Evaluator_Review_COMPLETE.docx`
(independent architect-track review, 2026-09-06 — full text + 4 flow diagrams;
supersedes the earlier `..._Review.docx` which had mangled tables).

---

## Full re-read (2026-09-06) — notes after the COMPLETE version + flowcharts

The scored findings and the plan below are unchanged. The complete text and the
four flow diagrams surfaced these still-open points:

1. ~~The demo has no visible Critic "catch"~~ **DONE (D11)** — `demo-appeal-critic`:
   winning precedents + a drafted appeal, but only 1/3 criteria evidenced → Critic
   `Concerns` → HumanReview, with *"Request more evidence"* as the primary control.
   `docs/runbooks/demo-script.md` builds the 3-minute demo around it.

2. ~~Intra-evidence contradiction is not covered.~~ **DONE (D17)** —
   `claims-extraction` agent + `ContradictionCheck` spoke: assertions → pair
   affirmed vs negated on the same subject → Gate HumanReview + Critic Block +
   Conflicts panel. `demo-contradiction` scenario.

3. **Eval metric gaps** (§10): (a) **policy-citation accuracy** — we score
   *precedent*-citation accuracy but not whether the cited *policy clause* is
   right; (b) **appeal-recommendation agreement** — only covered indirectly via
   route agreement (no `ExpectedAppealVerdict` label). Add both when the dataset
   grows 8 → ~20.

4. **Tiered approval thresholds** (§13). The Gate has one `AutoLimit` (£500); the
   review wants thresholds tiered by financial / operational risk. Fold into
   **P1-3**.

5. **Agent identity + version in the audit record** (§13). `CaseRecord` doesn't
   record which agent (name + version) produced each finding. The Foundry
   provisioner already versions agents — thread it through. Fold into **P1-3**.

6. **Evidence SOURCE granularity** (§8). `CaseView` reports source as "clinical
   note"; production wants document id + location/span. Add to TDD §7.1
   production items (provenance).

7. **Framing** (§4 + recommended-model flowchart). The recommended model names a
   distinct **Precedent Analyst Agent** between Evidence and Appeal Strategist.
   We fold precedent-comparability reasoning into deterministic `AppealMatch`
   ranking + `appeal-builder` + Critic check #3 + the precedent panel. D9 keeps
   our names — fine, but the pitch must be ready to answer "where is the
   precedent analysis?".

Nothing here overturns a decision. Items 1 and 3 are the substantive ones.

---

**Progress (2026-09-06):**
- **P0-3 done** — structured decision model, mandatory criteria never averaged,
  the `Abstain` route (D4). Code + 30 Core tests.
- **P0-2 done** — the Critic agent, seven checks, verdict feeds the Gate (D5).
  Code + 18 Agent tests.
- **P0-1 done** — TDD reframed (§3.1 "why multi-agent", §7.3 eval suite,
  §11.1 "the four questions"), ARCHITECTURE §5/§6/§8/§10 + both mermaids,
  `DECISIONS.md` (D1–D6), architecture SVG + PNG updated to 4 agents + Critic.
- **P0-4 done** — `eval/Zynara.Eval`: the metric suite (evidence precision/recall,
  mandatory false-negative rate, precedent-citation accuracy, hallucination count,
  route agreement, safe-abstention rate, unsafe-automation rate) + a generalist
  baseline for the agents-vs-one-generalist comparison, over 8 labelled cases.
  `dotnet test` hard-gates the safety metrics (unsafe automation = 0, mandatory
  FN = 0). Current run: route agreement 100%, unsafe automation 0 (baseline 2),
  safe abstention 1/1, no hallucinated references.
- **All P0 items complete.**
- **Slice 4 done** — `Zynara.Orchestrator` Durable Functions (hub + per-spoke
  activities + inline Gate); `tests/Zynara.Orchestrator.Tests`. 57 tests green.
- **All §D decisions taken** — see `DECISIONS.md` D9: keep agent names (no
  "Analyst" rename), scope target = **P0 + all P1 + P2**, dataset 8 → ~20,
  P1 next in demo-first order.
- **Re-scored** — `../progress/JUDGING-SELF-ASSESSMENT.md`: the two flagged
  weaknesses 6.5 → ~8.0–8.5; new floor is UX/demo (8.0), addressed by the P1
  dashboard slice.

- **P1-1 + P1-2 done** (D10) — `Zynara.Core.View.CaseView` evidence-first
  projection + precedent panel; `Zynara.ApiProxy` read API; `Zynara.Dashboard`
  static review workspace; `tools/Zynara.DemoDump`.
- **P2-5 done** (D11) — `docs/runbooks/demo-script.md` + the `demo-appeal-critic`
  scenario (visible Critic catch).
- **P2-1 done** (D12) — Estimated Recoverable Value with the formula + confidence
  shown; own dashboard tab.
- **P1-5 done** (D14) — Policy + Regulatory Profile (`RegulatoryProfile` + 7
  dimensions + `/api/profiles` + a low-key header panel).
- **P1-3 done** (D15) — reviewer roles + tiered approval authority + append-only
  case audit trail. Covers review notes #4 + #5.
- **P2-2 done** (D16) — `PipelineMetrics` measured per run; `BenchmarkService`
  before/after table; dashboard **Impact** tab.
- **P2-3 done** (D17) — `claims-extraction` agent + `ContradictionCheck` spoke;
  the note-against-itself contradiction flow (review note #2). **92 tests green.**

**Remaining:** P1-4 (infra identity isolation — needs `infra/` Bicep), review
note #3 (policy-citation + appeal-verdict eval metrics). Cosmos `caseAudit` +
real Entra roles land with `Zynara.Data` / `infra/`. **All P1 + all P2 code
complete bar P1-4 (infra).**

---

## 1. What the review says (summary)

**Verdict:** "shortlist-worthy today; potentially exceptional with targeted
changes." Overall **~8.0 / 10**, clear route to 9+.

| Area | Score | Note |
|---|---|---|
| Problem / Impact | 9 | Excellent |
| Architecture | 8.5 | Strong production thinking |
| Microsoft Foundry usage | 8 | Good, can be stronger |
| **Multi-agent justification** | **6.5** | **Biggest weakness** |
| Safety / Responsible AI | 8.5 | Strong foundations |
| Technical execution | 8.5 | Serious pro-code architecture |
| **Evaluation / evidence** | **6.5** | Needs quantitative proof |
| UX / demo potential | 8 | Very promising |
| Innovation | 8 | Precedent-driven appeal is interesting |
| Production credibility | 8 | Several hardening gaps |

**The four questions we must answer to win (review §20):**

1. Prove the agents make **better decisions together** than one generalist.
2. Prove the system **knows when it is uncertain** (safe abstention).
3. Prove **why a human should trust** the recommendation (evidence provenance).
4. Prove the **business value with measurable evidence** (not a marketing figure).

**What the review says we already get right:** the deterministic Durable
orchestration + Gate, the sole outbound path, human approval for every outbound
action, retained grounding, Cosmos + Blob/File-Search split, no PHI in the demo,
the observability/eval/cost story, and documenting the production gaps openly.
**None of these should change** (review §18).

---

## 2. Plan items

Grouped by the review's own priority. "Where" = the artifacts each item touches.
"Now?" flags whether it is best done before resuming the orchestrator build
(slice 4) — several P0s rework the Gate / evidence model we just built, so they
are cheaper to do now than later.

### P0 — must fix (review: "very high value")

| # | Item | What changes | Where | Now? |
|---|---|---|---|---|
| **P0-1** | **Reframe + prove the multi-agent story** | Present the agents as collaborating analysts under a Case Supervisor, not a linear row of LLM workers. New TDD section: *why multi-agent beats one generalist*, backed by P0-4 evidence. Keep deterministic sequencing + Gate (that is a strength — do **not** touch it). | diagram, TDD §3/§6, ARCHITECTURE, STATUS | doc-only, do first |
| **P0-2** | **Add the Critic agent** | New `ICriticAgent` + `CriticReview`: is every claim supported? does the cited clause support it? are the precedents comparable? contradictory evidence? mandatory criterion missing? recommendation stronger than the evidence? should it abstain? Runs after the Appeal Strategist, before the Gate; its output feeds the Gate (a failed check → review / abstain) and the reviewer UI. Stub + Foundry + prompt + provisioner + tests. | `Zynara.Core/Agents`, `Zynara.Agents/{Stubs,Foundry}`, orchestrator, diagram, TDD | **yes** — biggest single lift for the 6.5 → 8+ |
| **P0-3** | **Replace the generic readiness score with a structured decision model** | `Criterion` gets a `Mandatory` flag. `EvidenceGapResult` becomes: mandatory PASS/FAIL, supporting PASS/PARTIAL/MISSING, evidence quality HIGH/MED/LOW, contradictions NONE/DETECTED, precedent support STRONG/MODERATE/WEAK/NONE. Gate route becomes `AutoSubmit \| Strengthen \| HumanReview \| Abstain`; any mandatory FAIL or a contradiction is never averaged away. Rework `Gate.cs`, `EvidenceGapMatch.cs`, `Results.cs`, the evidence-gap agent output shape, and the Gate/pipeline tests. Drop the `0.82 = 6/7` example from the TDD. | `Zynara.Core`, `Zynara.Agents`, tests, TDD §3, ARCHITECTURE §8, diagram | **yes** — slice 2 just landed; rework before it calcifies |
| **P0-4** | **Expand the evaluation suite** | `Zynara.Eval` grows: evidence-extraction precision/recall, mandatory-criterion FP/FN rate, policy-citation accuracy, precedent-citation accuracy, hallucination / unsupported-claim rate, appeal-recommendation agreement vs. labels, **safe-abstention rate**, unsafe-automation rate. Needs a richer labelled case set (ground truth per criterion + expected route + expected citations). CI hard-gates the safety metrics (unsafe automation = 0; mandatory false-negative below a set bar). | `eval/Zynara.Eval`, `data/cases`, CI, TDD §7/§10, self-assessment | partly — the harness shape now, the dataset alongside the build |

### P1 — strongly recommended

| # | Item | What changes | Where |
|---|---|---|---|
| **P1-1** | **Evidence-first HITL workspace** | The review card leads with WHY / EVIDENCE (which clinical statement satisfies each criterion) / SOURCE / POLICY clause / VERSION / PRECEDENT / CONFIDENCE / CONFLICTS / AI ACTION, then HUMAN CONTROL (approve · edit · request evidence · reject). Not an approve-button-first screen. | `Zynara.Dashboard`, `Zynara.ApiProxy` read model, TDD §6 |
| **P1-2** | **Precedent panel = the killer feature** | Top-3 comparable cases; a fact-pattern similarity score each; denied-then-won-on-appeal badges; the matched-facts list; the involved policy clause; "these precedents drove the recommendation"; drill-in to the narrative. Expose the ranking scores + matched facts from `AppealMatch`. | Dashboard, ApiProxy, `AppealMatch` result shape, TDD §5 (Innovation), demo |
| **P1-3** | **RBAC & approval authority** | Reviewer roles + approval authority; approval thresholds tiered by financial / operational risk (feeds the Gate's auto-limit); audit every reviewer decision (identity, timestamp, edits) and every agent call (agent name + version) into the case record. | `Zynara.Core` (audit model), `Zynara.ApiProxy` (Entra app roles), a Cosmos `caseAudit` container, TDD §7 / ARCH §8, infra |
| **P1-4** | **Technically enforce agent isolation** | The reasoning agents run under an identity with no path to payer connectivity; only the Submission Adapter's identity holds outbound-integration permission. Document the identity + network boundary. | infra Bicep, TDD §7.1 / ARCH §8, diagram note |
| **P1-5** | **UK ⇄ US → "Policy + Regulatory Profile"** | The profile carries policy pack + terminology + workflow + regulatory profile + appeal/escalation path + coding conventions + integration profile — not a document swap. Rename the control and `config/regions.json` → `config/profiles/`. **Guardrail (§18): must not become the centrepiece.** | `Zynara.Core` (Profile model), diagram, TDD §2/§7.2, dashboard label |

### P2 — recommended / consider

| # | Item | What changes | Where |
|---|---|---|---|
| **P2-1** | **"Estimated Recoverable Value" with assumptions + confidence** | Rename Recovery £. Show denied count · not-appealed count · comparable win rate · average recoverable value · the estimate · a confidence level · the transparent formula. Keep the concept, expose the working. | Dashboard, TDD §5/§11 |
| **P2-2** | **Benchmark real performance (before / after)** | Instrument the demo pipeline for: case-prep time, criteria-check time, evidence gaps found, precedent-search time, reviewer effort, cost per case, unsupported-recommendation rate. Measure; label any estimated manual baseline; invent nothing. | eval / instrumentation, TDD §11, demo, self-assessment |
| **P2-3** | **Explicit contradiction-detection flow** | Claims extraction → intra-evidence contradiction check → a DETECTED contradiction is never silently resolved → route to HumanReview / Abstain and surface the conflict to the reviewer. (The contradiction *dimension* is in P0-3; this is the dedicated flow + UI.) | `Zynara.Core`, dashboard, TDD §3 |
| **P2-4** | **Demote expiry-watch / policy-drift from "agents" to deterministic services + optional narration** | Keep `ExpiryMath` / `PolicyDiff` as deterministic spokes with a thin optional AI narrator; stop counting them as reasoning "agents". Trims the story to 4 reasoning agents + Critic. Narrative + diagram change, minimal code. **Decision needed (§D-2).** | diagram, TDD §3/§6, `Zynara.Agents` (label only), STATUS |
| **P2-5** | **Demo golden path + one WOW moment** | Script the 3-min demo around a denial → the full evidence-based case → the Critic challenging an unsupported claim → correction → Gate → human approve → send → the auditable trail. **Guardrail: no infra walkthrough.** | `docs/runbooks/demo-script.md`, dashboard demo mode, video plan |

### Cross-cutting (falls out of the above)

- **`DECISIONS.md`** — start the delta log; record every change here as D1…Dn with rationale.
- **TDD "The four questions"** — a short section answering review §20 head-on.
- **Self-assessment / JUDGING** — re-score against the evaluator scorecard after each P0 lands.
- **Diagram** — Critic node, analyst framing, isolation note, Gate route enum wording, a precedent-panel hint in Experience.

---

## 3. Guardrails — do NOT (review §18)

- No agents added for count's sake.
- No Azure services added for an enterprise look.
- No autonomous payer submission.
- **Never replace the deterministic Gate with an LLM decision.**
- The region switch is not the centrepiece.
- No infrastructure walkthrough in the demo.
- No business-impact claim without the calculation or the measurement.

---

## D. Decisions needed before starting

1. **Role naming / framing.** Adopt "Case Supervisor + Policy Analyst + Evidence
   Analyst + Precedent Analyst + Appeal Strategist + Critic"? Or keep the current
   names (`needs-auth` etc.) and just add the Critic + reframe the narrative?
2. **Expiry-watch / policy-drift (P2-4).** Demote to deterministic services, or
   keep them as agents? (The review only *suggests considering* this.)
3. **Sequencing.** Do all four P0s before resuming the orchestrator (slice 4), or
   interleave P0-2 / P0-3 with the orchestrator build?
4. **Labelled dataset for P0-4.** Target size for the deadline, and who labels
   the ground truth (criteria met/missing, expected route, expected citations).
5. **Scope for the deadline.** All P0 + P1, or P0 + a subset of P1? P2 is
   "if time".
