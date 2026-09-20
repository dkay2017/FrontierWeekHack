# Care Approval IQ — the 3-minute pitch, explained simply

_"Proof beats paperwork."_ Written so an 11-year-old could follow it. Each row matches one shot in the video, so the words and the picture always agree. Read at a calm pace (about 150 words a minute), the narration takes about 2 minutes 10 seconds.

## The narration, shot by shot

| Time | On screen | Say (plain words) |
|---|---|---|
| 0:00–0:20 | **Title card** | "Hospitals lose money every day when insurers refuse to pay for scans. Most of these refusals can be fixed, but fighting each one takes a person about twenty minutes, so most are never challenged. Care Approval IQ uses a team of AI agents to prepare that fight, while a person stays in charge." |
| 0:20–0:50 | **Architecture diagram** | "Why several agents? Because this is several different jobs. Five agents each do one: check if permission is needed, find missing evidence, pull the facts from the doctor's note, find similar past wins, and a critic that tries to prove the case wrong. A referee, plain code with no AI, decides what happens next. Every send waits for a person, through a locked sender the AI cannot reach." |
| 0:50–1:05 | **Azure AI Foundry portal** (agents, then the critic) | "These are real agents in Microsoft Azure AI Foundry, running GPT-5.4. The critic's only job is to argue against the case. Rules and past cases come from our own document library." |
| 1:05–1:35 | **Dashboard → Cases** (open `demo-appeal`) | "In the dashboard, a person sees a refused scan that is already prepared. Every insurer rule is checked against the note, with the exact sentence quoted and its source shown. Similar past cases show which arguments this insurer has accepted. The person starts from a finished draft." |
| 1:35–2:00 | **Dashboard → How it thinks** ("Some evidence is missing") | "Now the critic pushes back: the recommendation is stronger than the evidence. The referee, being plain code, holds the case. The system can refuse its own suggestion. This is a replay, so it looks the same every time." |
| 2:00–2:20 | **Dashboard → Cases** (Approve & send, then the audit trail) | "Approval is a person's decision, and junior staff cannot approve risky cases. On approve, the sender uses a password from a locked vault, and the whole run is saved as one audit trace." |
| 2:20–2:45 | **Evidence slide**, then the green build | "Does a team beat one AI? We tested twenty-four cases where we knew the right answer. The team was right every time, with zero unsafe sends. A single AI got sixty-two percent, with four unsafe sends." |
| 2:45–3:00 | **Why it matters**, then the closing card | "About seven thousand pounds could be won back in this sample, with the working shown. Care Approval IQ. Proof beats paperwork." |

## Words to know

- **Prior authorisation:** the hospital asking the insurer "will you pay?" before the scan.
- **Denial / appeal:** the insurer says no; the appeal is a letter arguing back.
- **AI helper (agent):** an AI that does one job only.
- **Referee (Gate):** ordinary code, not AI, that picks one of four results: *Ready to send*, *Needs more evidence*, *Needs a person*, or *Not sure — held back*.

## The dashboard in three lines

- **Cases:** the to-do pile. Each refused scan comes with its quotes, similar past cases, the critic's objections and a draft letter.
- **Why it matters:** the scoreboard. About £7,164 could be won back, worked out scan by scan with "See the working". The by-hand numbers say "est." because they are estimates.
- **How it thinks:** a pre-recorded replay of the eight steps, not the live system.
