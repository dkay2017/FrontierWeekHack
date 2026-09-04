# Judging self-assessment — Agent-a-thon 2026

**Purpose:** a running scorecard against the official criteria, so we can target
specific, defensible improvements before submission instead of polishing at
random. Re-run this assessment (or update it) whenever a change plausibly moves
one of the three scores — don't let it go stale.

**Current standing (2026-09-04, session 6):** Innovation **22/30** (re-scored,
up from 16 — see the Re-assessment log), Usability **17/30**, Impact **17/30**
(not yet re-scored since D17 went live). See "Assessment #1" below for the full
original reasoning and "Innovation re-score reasoning" further down for why it
moved.

---

## Official judging criteria

Source: founderz.com/agentathon-terms, Section 8. Judged anonymously by
Pre-Learning Instructors (Microsoft-recognized MVPs). **Max 90 points = 30 × 3.**
Six winners per region (EMEA / Americas / Asia) = top scorers.

| # | Criterion | Question | Low (≤10) | Medium (≤20) | High (≤30) |
|---|---|---|---|---|---|
| 1 | **Innovation** | How original is the AI agent? | Lacks innovation — already exists, or an idea many participants put forward | Some innovation — an addition to an existing agent, or good creativity | Exceptionally innovative — completely new, or an exciting update on an existing agent |
| 2 | **Usability** | How usable is the AI agent? | Difficult to use or performs inconsistently | Limited bugs or issues operating | Performs well consistently and is intuitive to use |
| 3 | **Impact** | Quantitative + qualitative potential? | Potential for good is not high | Has potential to make an impact | Enormous potential to make an impact |

**Tie-break order:** Innovation → Usability → Impact → judges vote directly.

The rubric gives band descriptions only, no numeric sub-thresholds — what
separates e.g. 21 from 29 inside "High" is judge discretion (polish,
completeness, consistency).

---

## Assessment #1 — 2026-09-04 (session 5, self-assessed, strict/unbiased pass)

Scored by Claude acting as an anonymous strict judge, on request, against the
state of the repo/deployment at the end of session 5. **Not a real judge's score
— a calibration exercise so we know where to spend remaining effort.**

### Total: 50 / 90 (≈56%)

| Criterion | Score | Band |
|---|---|---|
| Innovation | **16 / 30** | solid Medium, not High |
| Usability | **17 / 30** | Medium, held down by real fragility |
| Impact | **17 / 30** | Medium, not Enormous |

### Innovation — 16/30

**Against:**
- Agent roles, threshold logic, and much of the prompt language are ported
  **near-verbatim** from the challenge's own reference `agents.py` (see
  DECISIONS / STATUS: "port this logic near-verbatim"). A judge who knows the
  reference will recognise it.
- The base concept (anomaly → fault diagnosis → work order for factory
  predictive maintenance) is **the assigned track scenario itself** — every
  participant got the same `factory/challenge-*` scaffold. Rubric's own Low-band
  wording ("an idea many participants put forward") plausibly applies to the
  cohort.
- C# vs. Python is a tech-stack choice, not an agent-capability innovation.

**For:**
- The **hybrid split** (agent writes prose; deterministic Core owns every number
  driving the gate/write path — D12) is a real reliability pattern beyond the reference.
- **Confidence-gated auto-vs-human-review routing** + sole-writer adapter is a
  genuine design decision, not just more code.
- The 3rd agent (Work Order) + the explicit reasoning for keeping it **out** of
  the visual portal workflow (D14) shows judgment.

**Verdict:** disciplined engineering on a given reference, not a new problem or
an exciting capability. Medium, upper-middle.

### Usability — 17/30

**For:**
- Actually **live end-to-end** right now: dashboard, reviewer workflow, cost
  tab, health tab — verified via real HTTP calls this session, not claimed.
- Anonymous auth = zero friction for a judge to click around.
- Citations (`inc-005` etc.), structured LIKELY CAUSE / ACTIONS / URGENCY text,
  the Pipeline Simulator tab — judge-friendly touches.

**Against (the harsh part):**
- **Cold-start latency is a real live-demo risk.** An emitted reading took
  **~2–3 minutes** to reach the review queue (Y1 Consumption cold start + 3
  sequential Foundry calls) in this session's own test, with **no progress
  indicator**. A judge who clicks "emit" and sees nothing for 3 minutes reads
  that as broken.
- **Build history is evidence of real fragility**, not just backstory: Flex
  Consumption silently stopped serving with zero telemetry; Function Apps got
  soft-deleted; storage auth conflicted; CI hung 25 min on a deadlock;
  orchestrator telemetry silently vanished for a full day (fixed same day, this
  session). This predicts residual risk at demo time.
- **No auth on reviewer write endpoints** (anyone can approve/reject/close) —
  fine for a demo, counts against "trustworthy tool" usability.
- **Operationally high-touch** — a human had to manually disable/re-enable a
  timer to avoid burning quota overnight. Not "intuitive," "needs an operator."

**Verdict:** works well when warm, shaky when cold. Real Medium.

### Impact — 17/30

**For:**
- Predictive maintenance is a legitimately high-value, well-understood problem
  — no defense needed for the domain choice.
- Human-in-the-loop gate + audit trail (cites, trace ids, sole-writer adapter)
  genuinely increases plausibility of real operational adoption over a
  black-box auto-actioner.

**Against:**
- **Zero real-world validation.** 5 synthetic machines, a fixture JSON, a
  weighted-random reading generator. No pilot, no historical failure data, no
  before/after number.
- The one quantitative artifact in the product (Cost & Governance tab) measures
  **the cost of running the agent** (token spend), not **the value it
  delivers** (avoided downtime, saved maintenance $) — points the wrong way for
  this criterion.
- Predictive maintenance is one of the **most crowded categories** in
  enterprise AI (Azure's own reference architectures, GE, IBM Maximo, Uptake,
  …). "Enormous potential" needs a differentiator that raises the ceiling above
  the category baseline; this is a faithful, well-engineered instance of the
  category, not a new angle on it.

**Verdict:** real potential, not evidenced, not differentiated. Medium.

---

## Progress since the 2026-09-04 assessment (not yet re-scored)

- **Item 6 built AND deployed** — T0 TrendCheck (predictive early warning), see
  DECISIONS D17. Core/Data/API/Dashboard shipped, 136/136 tests green,
  **live on Azure as of 2026-09-04** — provisioned, redeployed, migration
  applied, and verified: the orchestrator organically raised 2 real early
  warnings from live sensor traffic with no test scenario prompting it. This is
  the strongest evidence available that Innovation should move off 16/30 at the
  next formal re-score — not just built, but caught something true, unprompted,
  in production.
- **The "TireForge Industries" tell is fixed** — that name turned out to be the
  challenge scenario's own fictional company, verbatim, in our agent prompts
  and seed data. Renamed to "Meridian Tire Manufacturing" (display layer only),
  and the renamed prompts are now live (agents re-provisioned to `:3`/`:2`/`:2`).
  This wasn't in the original scoring reasoning (found afterward in
  conversation) but would have counted against Innovation/Usability polish if
  left as-is.
- **Formal re-score still owed** — this file's headline numbers (50/90,
  16/17/17) are from before D17 shipped. Worth a fresh strict pass once the
  Challenge 3 eval is re-run and there's been a live demo click-through.

## Improvement backlog (priority order, cheapest/highest-leverage first)

Track status here as items land; re-score when a batch is done.

| # | Target | Action | Status |
|---|---|---|---|
| 1 | Usability | Add a visible "processing…" / staged progress state in the Pipeline Simulator (and/or the dashboard) for the 2–3 min cold-start window — turns the biggest live-demo risk into a designed experience instead of a silent gap | ⬜ not started |
| 2 | Innovation | Foreground the hybrid agent/deterministic-gate pattern and the "2-in-portal, 3rd-called-conditionally" architecture decision explicitly in the submission write-up — this reasoning currently lives only in `DECISIONS.md`, not in front of a judge | ⬜ not started |
| 3 | Impact | Attach even an illustrative ROI figure to the Health Report tab (e.g. "$X/hour downtime × early-catch window ≈ $Y saved") — converts a narrative claim into a quantified one | ⬜ not started |
| 4 | Usability | Consider light auth (or at least a visible "demo mode, unauthenticated" banner) on reviewer write endpoints so it doesn't read as an unguarded prod tool | ⬜ not started |
| 5 | Usability | Warm-keep or pre-warm the orchestrator before a scheduled judge session (Y1 cold start is the root cause of #1) | ⬜ not started |
| 6 | Innovation | Consider one genuinely new capability beyond the reference (not just re-engineering it) if time allows — e.g. proactive maintenance scheduling suggestion, cross-machine pattern detection, or a capability the Python reference doesn't have at all | ✅ **live** (D17 — T0 TrendCheck, predictive early warning). 136/136 tests green, deployed + verified 2026-09-04 — the orchestrator organically raised 2 real early warnings from live sensor traffic with no test scenario prompting it. Fast-follow, not required: a 4th Foundry agent (`predictive-maintenance-agent`) to narrate it instead of the deterministic sentence. |

---

## Re-assessment log

| Date | Trigger | Total | Innovation | Usability | Impact |
|---|---|---|---|---|---|
| 2026-09-04 | Initial strict self-assessment (session 5) | 50/90 | 16 | 17 | 17 |
| 2026-09-04 | Innovation re-scored only (session 6) — D17 live, not just built; the orchestrator organically raised 2 real early warnings from live sensor traffic, unprompted | n/a — see below | **22** (+6) | 17 (not re-scored) | 17 (not re-scored) |

### Innovation re-score reasoning (2026-09-04, session 6) — 16 → 22

**Moved by:** the single gap that capped it last time — "reacts after a breach
despite being named *predictive* maintenance" — is now closed **and proven
live**, not just built: the orchestrator raised 2 real early warnings on its
own from live sensor data with no demo scenario prompting it. This changes the
system's *operating mode* (reactive-only → reactive **and** predictive running
in parallel), not just its feature count — structurally different from "an
addition to an existing agent" (Medium-band language).

**Not scored higher (28–30, "completely new") because:** linear-trend early
warning is not a novel algorithm — it's textbook statistical process control,
decades old in industrial monitoring. New to this solution and this
competition's scaffold, not new to the world. It's also still one deterministic
module + a UI tab on the same 3-agent architecture, not a rethink of the agent
paradigm itself (no agent debate, no closed-loop learning, no cross-machine
reasoning). **22 = solidly High band, its lower half** — "an exciting update on
an existing agent" (the rubric's own High-band language, and accurate: it gives
the Anomaly Detection concept's previously-unused recent-reading window a
genuinely new forward-looking capability), not "completely new."

**Usability/Impact intentionally not re-scored here** — the user asked
specifically for Innovation. A full re-score is still owed once the remaining
backlog items land (see the improvement backlog above).
