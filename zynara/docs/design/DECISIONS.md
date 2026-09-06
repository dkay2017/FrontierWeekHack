# Care Approval IQ — decision log

Deltas from the design of record (`ARCHITECTURE.md`) and the TDD, newest first.
Each entry: what changed, why, and what it touched.

---

## D9 · Evaluator-plan decisions locked

**2026-09-06.** Answers to the open questions in
`../review/EVALUATOR-REVIEW-RESPONSE.md` §D:
- **Framing (§D-1):** keep the existing agent names (`needs-auth`, `evidence-gap`,
  `appeal-builder`, `critic`) — no rename to "Analyst" roles. The multi-agent
  story is carried by the Critic + the "why multi-agent" section + the measured
  agents-vs-generalist comparison, not by role titles.
- **Sequencing (§D-3):** all P0 first, then the orchestrator — done.
- **Dataset (§D-4):** 8 labelled eval cases now; grow toward ~20 as the corpus
  and payer profiles land. Ground truth authored alongside each case.
- **Scope (§D-5):** target **P0 + all P1 + P2**. P1 next, demo-first order
  (P1-2 precedent panel + P1-1 evidence-first card → `Zynara.ApiProxy` read model
  + `Zynara.Dashboard`), then P1-5 / P1-3 / P1-4, then P2.

## D8 · Slice 4 — Durable orchestrator built; interim in-memory store

**2026-09-06.** `Zynara.Orchestrator` (.NET 8 isolated Functions) implements
TD-1 / TD-2 / TD-3: `POST /api/requests` Durable HTTP starter (keyed on request
id, idempotent), `AuthOrchestrator` hub sequencing five spoke Activity Functions
with a 3-attempt retry and owning the Gate inline, early-stop when auth is not
required. It is a step-for-step mirror of `Zynara.Core.Pipeline.AuthPipeline`,
which stays the tested single-process reference. Until `Zynara.Data` lands,
`DemoWorld` seeds an in-memory `IZynaraStore`. New Gate rule: an appeal draft
(`request.DenialLetter` present) never auto-submits — always HumanReview.
Touched: `src/Zynara.Orchestrator/*`, `tests/Zynara.Orchestrator.Tests/*`,
`Gate.cs`, `AuthPipeline.cs` (`DraftBuilder` → public), STATUS.

## D7 · Evaluation is a CI-gated metric suite, not a pass/fail script

**2026-09-06.** Evaluator P0-4. `eval/Zynara.Eval` runs the labelled case set
through the pipeline (deterministic stubs → repeatable) and scores: evidence
precision/recall, mandatory false-negative rate, precedent-citation accuracy,
hallucinated-reference count, Gate route agreement, safe-abstention rate,
unsafe-automation rate — plus a `GeneralistBaseline` straw man for the
agents-vs-one-generalist comparison. `dotnet test` **hard-gates** unsafe
automation = 0 and mandatory FN = 0.
Touched: `eval/Zynara.Eval/*`, `Zynara.sln`, TDD §7.3, STATUS.

## D6 · expiry-watch and policy-drift are deterministic services, not agents

**2026-09-06.** The independent review flagged that date arithmetic (expiry) and a
text diff (drift) do not need an LLM. `ExpiryMath` and `PolicyDiff` stay as
deterministic spokes with a **thin optional AI narrator** for the alert wording;
they are no longer counted among the reasoning agents. The reasoning agents are
now: needs-auth, evidence-gap, appeal-builder, **critic**.
Touched: TDD §3/§6, ARCHITECTURE §5, the architecture SVG, STATUS.

## D5 · Add the Critic agent

**2026-09-06.** Evaluator P0-2. A second reasoning agent that tries to *disprove*
the assembled recommendation before the Gate — the thing that makes this
multi-agent rather than five specialised prompts. Seven checks (claim support,
clause support, precedent comparability, contradiction, mandatory criteria,
recommendation strength, abstention). Its verdict feeds the Gate: `Abstain` →
abstain, `Block` → human review even with perfect numbers, `Concerns` → cannot
auto-submit.
Touched: `Zynara.Core/Agents/Contracts.cs` (`ICriticAgent`, `CriticReview`),
`Zynara.Core/Pipeline/CriticCheck.cs`, `Gate.cs`, `Zynara.Agents/{Stubs,Foundry}`,
DI, provisioner, TDD §3/§5/§6, SVG.

## D4 · Structured decision model replaces the averaged readiness score

**2026-09-06.** Evaluator P0-3. A single `readiness = 6/7` number can average a
missing **mandatory** criterion away. Replaced with:
- `Criterion.Mandatory` — a hard gate, never averaged.
- `IEvidenceGapAgent` returns per-criterion findings
  (Documented / Partial / Missing / Contradicted) + the supporting clinical
  statement + an overall `EvidenceQuality`.
- The Gate weighs a `DecisionModel` and picks one of four routes:
  **AutoSubmit / Strengthen / HumanReview / Abstain**. Any unmet mandatory
  criterion or a contradiction → HumanReview (never resolved silently);
  Low evidence + Weak/None precedent support → **Abstain**.
Touched: `Zynara.Core/Model/{Domain,Results}.cs`, `Agents/Contracts.cs`,
`Pipeline/{EvidenceGapMatch,AppealMatch,AuthPipeline}.cs`, `Gating/Gate.cs`,
`Zynara.Agents/*`, TDD §3, ARCHITECTURE §8.

## D3 · Precedent matches carry their score and matched facts

**2026-09-06.** Groundwork for the evidence-first reviewer UI (evaluator P1-2).
`AppealMatch` now returns `PrecedentMatch(Precedent, Similarity, MatchedFacts)`
and derives a `PrecedentSupport` level (Strong / Moderate / Weak / None) the Gate
consumes.
Touched: `Agents/Contracts.cs`, `Pipeline/AppealMatch.cs`, `Model/Results.cs`.

## D2 · Agent layer built stub-first, Foundry behind the same interface

**2026-09-06.** Each agent is a `Zynara.Core` interface with a deterministic stub
twin (the default, used in CI) and a hosted Foundry implementation swapped in by
`ZYNARA_AGENTS=foundry`. Pattern carried from the prior project. Lets the whole
pipeline run in CI with zero live inference.
Touched: `Zynara.Core/Agents/*`, `Zynara.Agents/*`.

## D1 · No queue, Cosmos not SQL, no separate Intake

**2026-09-06.** See TDD §12 · TD-3 / TD-4 / TD-5. The Durable HTTP starter is the
async boundary; Cosmos DB (serverless) for operational state; Blob + Foundry
File Search for the unstructured corpus; `Zynara.Intake` project dropped (folded
into the orchestrator app).
Touched: architecture SVG/PNG, TDD §3–§12, ARCHITECTURE §8–§17, the repo tree.
