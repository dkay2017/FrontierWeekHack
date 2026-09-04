# Course "Final Activity" brief — design + deliver a multi-agent solution

**Source:** the Microsoft Foundry course's own closing assignment (screenshot
confirmed verbatim by the user, 2026-09-04). **This is a separate artifact from
the Agent-a-thon's 90-point judging rubric** — see
`JUDGING-SELF-ASSESSMENT.md` for that one. Both are satisfied by describing the
same system; keep them cross-referenced, don't conflate them.

| | This brief | Agent-a-thon rubric |
|---|---|---|
| Format | A design write-up answering 5 specific questions | Scored 0–30 × 3 (Innovation / Usability / Impact) |
| Judged on | Whether you *define* the 5 things clearly | How the *built system* performs |

## The brief, verbatim

> Throughout this program, you have explored the complete lifecycle of building
> enterprise AI solutions with Microsoft Foundry. You learned how to create
> specialised agents, connect them to tools and knowledge sources, monitor and
> trace their behaviour, evaluate their quality using datasets and metrics, and
> orchestrate them into scalable workflows.
>
> Now it is your turn to apply these concepts by designing a production-ready
> multi-agent solution that addresses a real business challenge. The goal is not
> simply to build a working agent, but to demonstrate how you would move from
> prototype to production using the architectural patterns covered in the course.
>
> **Step 1: Design your AI solution.** Choose a realistic business scenario that
> could benefit from an AI-powered workflow. Define:
> 1. The business problem you want to solve.
> 2. The intended users of the solution.
> 3. At least two specialised agents with distinct responsibilities.
> 4. The tools, data sources, or knowledge bases that each agent will use.
> 5. Why a multi-agent approach is more effective than a single-agent solution.
>
> Your design should clearly show how information will flow between agents and
> how they contribute to solving the overall problem.

## Mapping: Meridian Anomaly & Predictive IQ → the 5 points

1. **Business problem** — unplanned downtime and catastrophic failure on a tire
   manufacturing line, caught too late (only after a threshold is already
   breached) and diagnosed too slowly/inconsistently by manual inspection.

2. **Intended users** — maintenance reviewers/technicians (the human-in-the-loop
   approve/reject/close workflow) and plant operations management (Health Report
   + Cost & Governance views).

3. **≥2 specialised agents, distinct responsibilities** — **3 live today, a 4th
   designed** (DECISIONS D17):
   - **Anomaly Detection Agent** — confirms/narrates a threshold breach.
   - **Fault Diagnosis Agent** — root-causes it against maintenance history, no
     tools, pure reasoning over cited evidence.
   - **Work Order Agent** — drafts the actionable maintenance instruction,
     invoked only when the deterministic Gate permits it (D14).
   - **Predictive Maintenance Agent** *(designed, not yet deployed — D17)* —
     narrates a trend before it becomes a fault at all.

4. **Tools / data sources per agent**:
   - Anomaly Detection → `check_thresholds` tool over live sensor readings.
   - Fault Diagnosis → grounded in `HistoryMatch` results (cited prior incident
     records: `inc-…` ids), no tools.
   - Work Order → the confirmed diagnosis + the triggering reading id, no
     external tools.
   - Predictive Maintenance → the deterministic `TrendCheck` (T0) output
     (rate / ETA / confidence), no tools of its own.

5. **Why multi-agent, not single-agent** — each agent has a narrow,
   independently-evaluable, auditable responsibility (the portal evaluates the
   Anomaly agent in isolation — Challenge 3); a single agent doing
   detect+diagnose+act would be an opaque black box with no seam for the human
   review gate to sit in, and no way to invoke the costly Work Order step
   *conditionally* (only when the deterministic Gate says a work order is
   warranted, not on every reading).

**Information flow:** Reading → threshold check (T1) → Anomaly Detection agent →
(if anomalous) history match (T2) → Fault Diagnosis agent → deterministic
confidence/severity Gate → Work Order agent (conditional) → human reviewer →
issued. A parallel trend check (T0) feeds the Predictive Maintenance agent
regardless of whether the current reading is anomalous — reactive and predictive
paths run side by side, not one gating the other.

## Where this leaves the "agents = just the reference, renamed" question

Points 3–5 of this brief are exactly where the Agent-a-thon's Innovation
criterion also gets scored (see `JUDGING-SELF-ASSESSMENT.md` — Innovation was
16/30, capped by "ported near-verbatim from the reference"). The T0 predictive
agent (point 3's 4th agent) is the answer to both asks at once: it's a
responsibility the reference doesn't have at all, not a re-skin of one it does.
That's why D17 was prioritized over polish items — it's the one change that
moves *both* documents, not just one.
