# Care Approval IQ — decision log

Deltas from the design of record (`ARCHITECTURE.md`) and the TDD, newest first.
Each entry: what changed, why, and what it touched.

---

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
