# Care Approval IQ — 3-minute demo script

**Goal:** show the business problem being solved, not the infrastructure.
**Format:** screen recording of the reviewer dashboard (`Zynara.Dashboard`).
**Length:** 3:00 hard cap. **Never** open the Azure portal or walk the architecture.

The memorable line (say it verbatim near the end):

> "The AI did not just write an appeal. It found historical evidence, **challenged
> its own recommendation**, and handed the reviewer an auditable case for action."

## The four things this demo proves (evaluator §20)

1. The agents make a **better decision together** than one generalist — the Critic
   overturns a recommendation the other agents produced.
2. The system **knows when it is uncertain** — the abstain case.
3. **Why a human should trust it** — every criterion shows its evidence, source,
   policy clause and version; every precedent shows why it is comparable.
4. **Measurable value** — the queue clears denials into evidence-backed appeals;
   the Estimated Recoverable Value tile (P2-1) shows the formula, not a slogan.

---

## Pre-flight

- [ ] `demo-cases.json` regenerated: `dotnet run --project tools/Zynara.DemoDump -- src/Zynara.Dashboard/demo-cases.json`
- [ ] Dashboard served (offline is fine): open `src/Zynara.Dashboard/index.html`.
      For a live run: `func start` in `src/Zynara.ApiProxy`, then `index.html?api=http://localhost:7071`.
- [ ] If running live: **clear the Cosmos `cases` container** and do **one** scenario pass, so the
      queue is a handful of distinct rows, not a pile of identical re-runs.
- [ ] Window sized so the review card fits without scrolling to the precedent panel.
- [ ] Start on the **Review Queue** tab with **`demo-appeal-critic`** selected.
- [ ] Recording at 1080p, cursor visible, no notifications.

## Optional 15-second opener — the Pipeline Simulator tab

The **Pipeline simulator** tab (illustrative, canned) steps the eight stages —
Intake → Needs-auth → Evidence → Contradiction → Precedent → Critic → Gate →
Decision — with a trace log. Run the **"only 1 of 3 criteria evidenced"** case
once to plant the shape of the pipeline, then switch to Review queue for the real
thing. Skip it if you are tight on time — the real card is the story.

---

## Beat sheet (3:00)

| Time | On screen | Say |
|---|---|---|
| **0:00–0:20** | Review Queue. Point at the queue: `demo-appeal` (ready), `demo-appeal-critic` (selected), `demo-abstain`. | "Prior authorisation: a clinician requests a scan, the payer wants proof the criteria are met, and denials pile up unappealed. This is a reviewer's queue of assembled cases — each one already worked by the agents." |
| **0:20–0:40** | `demo-appeal-critic` card — the **WHY** header + the **denial**: "denied under code MN-01: conservative treatment not evidenced". | "This one was denied. The needs-auth agent resolved the plan rule and code; the denial reason and the policy clause are right here." |
| **0:40–1:05** | Scroll to the **Evidence** table. c1 *physiotherapy* = Documented with the quoted note sentence; c2, c3 = **Missing**. | "The evidence agent read the clinical note against the three written criteria. Physiotherapy is documented — here's the exact sentence and its source. Radiculopathy and the imaging-changes-management criterion are **not in the note**." |
| **1:05–1:30** | The **Precedent panel** — P-1 and P-2: match %, **won on appeal** badge, **initially denied** badge, matched-fact chips, clause 2.1, "▲ drove the recommendation". | "The precedent-strategist found two comparable cases on the payer's own record. Both were denied under the same code, both **won on appeal** under clause 2.1. It drafted an appeal and recommended we file it." |
| **1:30–2:05** | The **Critic** card — verdict **Concerns**, flag: *recommendation-strength — the recommendation is firmer than Low-quality evidence supports*. Then the header line: "The Critic challenged the recommendation…". | "Then the **Critic** — a separate agent whose only job is to disprove the case — pushed back. Two precedents won, yes, but this record only evidences **one of three criteria**. Filing now would be over-reaching. **This is the multi-agent part**: one agent built the case, another tore into it." |
| **2:05–2:25** | The **Gate** card — route **HumanReview**, the decision model row (mandatory all met · supporting 0/0/2 missing · evidence Low · precedent support Moderate). | "The deterministic Gate — not an LLM — takes the Critic's concern and the numbers and routes it to a human. An appeal never files itself." |
| **2:25–2:45** | The **Human control** bar — **Request more evidence** is the primary action, not Approve. Click it; the "recorded" confirmation appears. | "And the reviewer isn't shown an approve button first. The suggested next step is **request the missing evidence** — because that's what the case needs. One click, logged against the case." |
| **2:45–3:00** | Click `demo-abstain` in the queue — status **System abstained**, confidence **Low**, empty precedent panel. | "And when there's no comparable history and the record is thin, the system **doesn't guess — it abstains**. That's the trust boundary." *(Then the memorable line.)* |

---

## Coda — carry one case through to the send (re-eval §18, 2:40–3:00)

The re-eval asks the demo to end on the *whole* path, not just the safety catch.
If the abstain beat runs short, or as the deliberate close:

Click `demo-appeal` — the clean version: all three criteria documented, same two
winning precedents, Critic **Clear**, Gate route **Ready to submit**, primary
action **Approve & send**. Say: *"Same pipeline. When the evidence is actually
there, the reviewer approves and the Submission Adapter sends — one outbound path,
one trace from the request through every agent to the payer acknowledgement, one
append-only audit record."* Click **Approve & send**; the confirmation and the
audit-trail entry appear in the drawer. (The App Insights trace tree is a
pre-captured screenshot if you want to hold it up for 3 seconds — do **not**
navigate Azure live.)

## Backup if the live API is down

The dashboard runs fully offline from `demo-cases.json`. Nothing in the script
needs the API — the "Request more evidence" click is recorded in the page.

---

## Maps to the review's golden path (§15 / flowchart 4)

| Review step | Demo beat |
|---|---|
| Denied case → policy + denial reason | 0:20–0:40 |
| Evidence gaps / contradictions | 0:40–1:05 |
| Comparable precedents | 1:05–1:30 |
| Appeal strategy | 1:05–1:30 (drafted) |
| **Critic challenges an unsupported claim** | **1:30–2:05** |
| Correct / request evidence / abstain | 2:25–2:45 |
| Deterministic Gate | 2:05–2:25 |
| Human approval | 2:25–2:45 · and the coda (Approve & send) |
| Submission Adapter sends | the coda — Approve & send on `demo-appeal`, drawer confirmation + audit entry |
| Evidence trail + trace | visible throughout the card; trace tree as a held-up screenshot in the coda |
