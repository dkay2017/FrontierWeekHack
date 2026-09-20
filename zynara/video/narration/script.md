# Care Approval IQ — 3-minute narration script

_"Proof beats paperwork."_ Plain words an 11-year-old could follow. One row per shot, so the words match the picture. About 320 words: roughly 2 minutes 10 seconds spoken, which leaves about 50 seconds for screen moves inside the 3:00. Voice: `en-GB-RyanNeural`, `--rate=-3%`; clip N is `N.mp3`. "GPT-5.4" is read as "G P T five point four".

| # | Slot | On screen | Say |
|---|---|---|---|
| 1 | 0:00–0:20 | **Title card** | "Hospitals lose money every day when insurers refuse to pay for scans. Most of these refusals can be fixed, but fighting each one takes a person about twenty minutes, so most are never challenged. Care Approval IQ uses a team of AI agents to prepare that fight, while a person stays in charge." |
| 2 | 0:20–0:50 | **Architecture diagram** | "Why several agents? Because this is several different jobs. Five agents each do one: check if permission is needed, find missing evidence, pull the facts from the doctor's note, find similar past wins, and a critic that tries to prove the case wrong. A referee, plain code with no AI, decides what happens next. Every send waits for a person, through a locked sender the AI cannot reach." |
| 3 | 0:50–1:05 | **Azure AI Foundry portal** (agents, then critic) | "These are real agents in Microsoft Azure AI Foundry, running GPT-5.4. The critic's only job is to argue against the case. Rules and past cases come from our own document library." |
| 4 | 1:05–1:35 | **Dashboard → Cases** (`demo-appeal`) | "In the dashboard, a person sees a refused scan that is already prepared. Every insurer rule is checked against the note, with the exact sentence quoted and its source shown. Similar past cases show which arguments this insurer has accepted. The person starts from a finished draft." |
| 5 | 1:35–2:00 | **Dashboard → How it thinks** ("Some evidence is missing") | "Now the critic pushes back: the recommendation is stronger than the evidence. The referee, being plain code, holds the case. The system can refuse its own suggestion. This is a replay, so it looks the same every time." |
| 6 | 2:00–2:20 | **Dashboard → Cases** (Approve & send, audit trail) | "Approval is a person's decision, and junior staff cannot approve risky cases. On approve, the sender uses a password from a locked vault, and the whole run is saved as one audit trace." |
| 7 | 2:20–2:45 | **Evidence slide**, green build | "Does a team beat one AI? We tested twenty-four cases where we knew the right answer. The team was right every time, with zero unsafe sends. A single AI got sixty-two percent, with four unsafe sends." |
| 8 | 2:45–3:00 | **Why it matters**, closing card | "About seven thousand pounds could be won back in this sample, with the working shown. Care Approval IQ. Proof beats paperwork." |

**Clip lengths (rendered):** 19.9 / 27.4 / 14.7 / 18.6 / 15.9 / 12.8 / 15.6 / 10.3 seconds; 135 seconds in total. Clips 1 and 3 have under half a second of slack in their slots.
