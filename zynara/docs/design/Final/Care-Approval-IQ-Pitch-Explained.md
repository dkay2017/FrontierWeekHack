# Care Approval IQ — the pitch, explained simply

_"Proof beats paperwork."_ Written so an 11-year-old could follow it.

---

## Slide 1 — The problem: the permission-slip mess

Before a hospital does an expensive scan (like an MRI), the **insurance company** must say "yes, we'll pay". That's *prior authorisation*.

Insurers often say **no**, and many of those no's are fixable: the doctor's note missed one detail, or the paperwork didn't match the insurer's rules. Fixing it means a person reading long notes and rulebooks, then writing an appeal letter. It is slow, so patients wait, and most denials are never appealed at all.

## Slide 2 — The idea: a team of specialists with a strict referee

Think of a school group project. Each kid has one job, and the teacher makes the final call.

1. **Intake** — the clinician sends the medical note and the insurer's "no" letter.
2. **Five AI agents** each do one thinking job.
3. **A plain, rule-based program** (no AI) runs the steps and makes the decision.
4. **A person always presses the final "approve" button.**

## Slide 3 — The five agents

| Agent | Kid-level job |
|---|---|
| **needs-auth** | "Does the insurer even require permission for this?" |
| **evidence-gap** | "The rulebook is a checklist. Does the doctor's note tick each box?" |
| **claims-extraction** | "Pull out the facts from the note and spot anything that contradicts itself." |
| **precedent-strategist** | "Has the insurer said yes to a case like this before? What argument worked? Draft the appeal." |
| **critic** | "Try to prove the case is wrong." The friend who checks your homework. |

Why five? They are five genuinely different kinds of thinking. We deliberately did **not** make agents for boring, exact work (date maths, comparing rule versions, routing, thresholds). Plain code does that better.

## Slide 4 — The key trick: the AI never decides

The agents only **think and write**. The **Gate** (ordinary code, built on Microsoft Agent Framework) picks one of four outcomes:

- **Ready to submit**
- **Strengthen** the case first
- **Human review**
- **Abstain** ("we're not sure, so we won't touch it")

Every send **pauses until a human answers**. AI can be confidently wrong, so the decision stays in code that can be checked.

## Slide 5 — Proof, not promises

We tested on 24 labelled cases against **one all-in-one AI** given the same information.

| | Our 5-agent pipeline | Single generalist AI |
|---|---|---|
| Route agreement | **100%** | 62.5% |
| Unsafe automatic sends | **0** | 4 |
| Correctly held back when unsure | **4 / 4** | 1 / 4 |

## Slide 6 — One sentence

> It helps hospitals fight insurance denials faster: five AI specialists prepare the case, a strict rule-based referee decides what happens next, and a human always presses the final button.

---

# The dashboard, tab by tab

The dashboard is the "control room" where a human reviewer sees everything. It has four tabs. The "Reviewing as" box (top right) picks your role, and the "Profile" box picks the country and insurer's rulebook (for example UK · Bupa).

## Tab 1 — Review queue: "the to-do pile"
A list of denied cases waiting for a person. Click one and a panel slides open showing the whole case:
- which rulebook items the doctor's note **does** and **doesn't** cover, with the exact quote as proof;
- similar past cases the insurer approved;
- what the Critic objected to;
- the Gate's suggested next step.

The reviewer then presses **Approve & send** or **Reject**. If they approve, the system sends it and records who did what.
*Think: a teacher's marking pile, where each essay already has notes in the margin.*

## Tab 2 — Early warnings: "the smoke alarms"
Two simple, non-AI watchers:
- **expiry-watch:** "This permission slip runs out soon, so book it before it expires."
- **policy-drift:** "The insurer changed its rulebook, so old cases may need rechecking."

They only **warn**. They never decide or send anything. (The demo data has no warnings right now.)
*Think: a phone reminder that the milk is about to go off.*

## Tab 3 — Impact: "the scoreboard"
Shows why this is worth doing.
- **Estimated recoverable value (£7,164):** 21 denials nobody appealed × the 66% chance they'd win × the average claim of £560. The formula is shown so anyone can check it.
- **By payer & procedure:** which insurer and scan lose the most money. MRI lumbar spine is the big one, with a 70% win rate. Shoulder scans are a "low confidence" guess, because there are only 5 past cases.
- **Before / after:** doing it by hand (about 20 minutes a case, gaps found only after a denial) versus Care Approval IQ (assembled automatically, every criterion checked, gaps flagged up front). Manual numbers are labelled **EST.** because they're estimates, and we say so honestly.
*Think: a report card that shows its working.*

## Tab 4 — Pipeline simulator: "the slow-motion replay"
Pick a sample case and press **Run pipeline**. Eight boxes light up one by one, and a log explains each step:

Intake → Needs-auth → Evidence → Contradiction → Precedent → Critic → Gate → Decision

Example: a denied MRI has only 1 of 3 checklist items evidenced. The Critic says "your recommendation is firmer than the evidence allows". The Gate routes it to **Strengthen**, so it is **not sent** and goes back to the clinician for the missing evidence.
It is a **canned, illustrative** replay so it runs the same every time. The real pipeline runs on the server, and the Review queue shows its real output.
*Think: watching a football goal from every camera angle.*

---

## Quick recap for the demo

| Tab | One-liner | Say this in the video |
|---|---|---|
| Review queue | The to-do pile | "Here's a denial, fully prepared and cited. A human decides." |
| Early warnings | Smoke alarms | "Simple watchers that warn, never decide." |
| Impact | Scoreboard | "£7,164 recoverable here, and you can check the maths." |
| Pipeline simulator | Slow-motion replay | "Watch the referee stop an unsafe send." |
