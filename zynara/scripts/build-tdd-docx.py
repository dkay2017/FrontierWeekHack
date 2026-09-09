#!/usr/bin/env python3
"""Care-Approval-IQ-TDD.docx — TireForge-style: cover page, running header,
readable type, point-form sections, one architecture image, service-by-service."""
import os
from docx import Document
from docx.shared import Pt, RGBColor, Inches
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

TEAL   = RGBColor(0x0C, 0x7F, 0x76)
BLUE   = RGBColor(0x1D, 0x4E, 0xD8)
VIOLET = RGBColor(0x6D, 0x28, 0xD9)
INK    = RGBColor(0x12, 0x2A, 0x32)
GREY   = RGBColor(0x44, 0x55, 0x5C)
HDR    = "EDE6FB"

D = os.path.join(os.path.dirname(__file__), "..", "docs", "design")
DEST = f"{D}/Care-Approval-IQ-TDD-V4.docx"
FIG  = f"{D}/tdd-figures"

doc = Document()

st = doc.styles["Normal"]
st.font.name = "Calibri"; st.font.size = Pt(11); st.font.color.rgb = INK
st.paragraph_format.space_after = Pt(6); st.paragraph_format.line_spacing = 1.16

h1 = doc.styles["Heading 1"]
h1.font.name = "Calibri"; h1.font.size = Pt(16); h1.font.bold = True; h1.font.color.rgb = BLUE
h1.paragraph_format.space_before = Pt(18); h1.paragraph_format.space_after = Pt(6)
h1.paragraph_format.keep_with_next = True

for b in ("List Bullet", "List Bullet 2"):
    s = doc.styles[b]; s.font.name = "Calibri"; s.font.size = Pt(11); s.font.color.rgb = INK
    s.paragraph_format.space_after = Pt(3); s.paragraph_format.line_spacing = 1.14

sec = doc.sections[0]
sec.left_margin = sec.right_margin = Inches(0.85)
sec.top_margin = Inches(0.7); sec.bottom_margin = Inches(0.7)
sec.different_first_page_header_footer = True

# running header (pages 2+) — the brand strip
hp = sec.header.paragraphs[0]; hp.alignment = WD_ALIGN_PARAGRAPH.CENTER
hp.paragraph_format.space_after = Pt(2)
hp.add_run().add_picture(f"{FIG}/runhead.png", width=Inches(6.9))

# footer — small line, every page
fp = sec.footer.paragraphs[0]; fp.alignment = WD_ALIGN_PARAGRAPH.CENTER
fr = fp.add_run("Zynara Health (fictional) · Care Approval IQ · Microsoft Agent Framework · "
                "Architect Track Submission · Deepak Kumar")
fr.font.size = Pt(8); fr.font.color.rgb = GREY

def shade(cell, hx):
    e = OxmlElement("w:shd"); e.set(qn("w:val"), "clear"); e.set(qn("w:fill"), hx)
    cell._tc.get_or_add_tcPr().append(e)

def para(text="", *, italic=False, size=None, color=None, bold=False, after=6, align=None, before=None):
    p = doc.add_paragraph()
    if align: p.alignment = align
    p.paragraph_format.space_after = Pt(after)
    if before is not None: p.paragraph_format.space_before = Pt(before)
    if text:
        r = p.add_run(text); r.italic = italic; r.bold = bold
        if size: r.font.size = Pt(size)
        if color: r.font.color.rgb = color
    return p

def rich(segs, *, style=None, after=6, size=11):
    p = doc.add_paragraph(style=style); p.paragraph_format.space_after = Pt(after)
    for s in segs:
        t, b = s[0], s[1]; it = s[2] if len(s) > 2 else False; col = s[3] if len(s) > 3 else None
        r = p.add_run(t); r.bold = b; r.italic = it; r.font.size = Pt(size)
        if col: r.font.color.rgb = col
    return p

def bullet(segs, *, sub=False):
    return rich(segs, style="List Bullet 2" if sub else "List Bullet", after=3)

def numbered(segs):
    return rich(segs, style="List Number", after=3)

def sub(label):
    """A purple sub-heading, TireForge-style."""
    p = doc.add_paragraph(); p.paragraph_format.space_before = Pt(9); p.paragraph_format.space_after = Pt(3)
    p.paragraph_format.keep_with_next = True
    r = p.add_run(label); r.bold = True; r.font.size = Pt(12); r.font.color.rgb = VIOLET

def fig(path, w=6.9):
    if not os.path.exists(path):
        para(f"[missing figure: {path}]", italic=True, color=GREY); return
    doc.add_picture(path, width=Inches(w))
    q = doc.paragraphs[-1]; q.alignment = WD_ALIGN_PARAGRAPH.CENTER
    q.paragraph_format.space_before = Pt(4); q.paragraph_format.space_after = Pt(8)

def h1heading(t):
    doc.add_heading(t, level=1)

def hr(color="0FA3A0"):
    p = doc.add_paragraph(); pPr = p._p.get_or_add_pPr()
    b = OxmlElement("w:pBdr"); bot = OxmlElement("w:bottom")
    bot.set(qn("w:val"), "single"); bot.set(qn("w:sz"), "8"); bot.set(qn("w:color"), color)
    b.append(bot); pPr.append(b)

# =========================================================== COVER PAGE
cov = doc.add_paragraph(); cov.alignment = WD_ALIGN_PARAGRAPH.CENTER
cov.paragraph_format.space_before = Pt(40); cov.paragraph_format.space_after = Pt(0)
cov.add_run().add_picture(f"{FIG}/cover-logo.png", width=Inches(5.3))

para(after=0).paragraph_format.space_after = Pt(70)   # vertical air

t = doc.add_paragraph(); t.alignment = WD_ALIGN_PARAGRAPH.CENTER; t.paragraph_format.space_after = Pt(6)
r = t.add_run("TECHNICAL DESIGN DOCUMENT"); r.bold = True; r.font.size = Pt(26); r.font.color.rgb = BLUE
r.font.name = "Calibri"; r.font.color.rgb = BLUE

para("Version 4", size=13, color=GREY, bold=True, after=18, align=WD_ALIGN_PARAGRAPH.CENTER)
para("Microsoft Agent-a-thon 2026 · Architect Track Submission · Healthcare Prior Authorisation",
     size=12, color=INK, after=3, align=WD_ALIGN_PARAGRAPH.CENTER)
para("Deepak Kumar · Original Work", size=12, color=INK, after=24, align=WD_ALIGN_PARAGRAPH.CENTER)
para("Built on the Microsoft Agent Framework. Companion artefacts: the architecture SVG/PNG "
     "(authoritative diagram) and DECISIONS.md (delta log).",
     italic=True, size=10, color=GREY, after=0, align=WD_ALIGN_PARAGRAPH.CENTER)

doc.add_paragraph().add_run().add_break(WD_BREAK.PAGE)

# =========================================================== 1 · PROBLEM
h1heading("1 · The problem")
para("A doctor wants to treat a patient — an MRI, a referral, physiotherapy, surgery. The insurer "
     "has to authorise it first, and they say no a lot. Two facts sit at the centre of this design:",
     after=6)
bullet([("62% of doctors don't appeal a denial ", True), ("— they believe they'll lose.", False)])
bullet([("80.7% of appeals actually win ", True), ("(95% for skilled-nursing). The belief is wrong, "
        "and the payer's own denial history proves it.", False)])
para("Everything else follows from that gap:", before=4, after=4)
bullet([("Prior authorisation is the single largest automatable cost in US health admin — ", False),
        ("$687B/yr on administration against $346B on direct patient care.", True)])
bullet([("Nobody reads the clinical chart against ", False), ("this", False, True),
        (" payer's, ", False), ("this", False, True),
        (" plan's criteria before the request goes in.", False)])
bullet([("Nobody checks whether the fact pattern has won on appeal before.", False)])
bullet([("So the clinician gives up — and the winnable, un-appealed cases stay as money the insurer "
         "keeps by default.", False)])
para("Care Approval IQ closes that gap: it assembles a gap-checked submission, and when a denial "
     "lands it drafts an appeal from the arguments the payer's own history shows have succeeded in "
     "comparable cases — with a person signing off every outbound action.", before=6)

# =========================================================== 2 · MISSION & SCOPE
h1heading("2 · Mission & scope")
sub("Request inputs")
bullet([("Procedure requested · patient coverage reference · free-text clinical note · payer + plan "
         "· (post-decision) the denial letter.", False)])
sub("In scope — built and demonstrated")
for x in ["End-to-end pipeline: request → needs-auth → evidence gap → claims extraction → precedent "
          "match → critic → deterministic Gate → human review → submit → (on denial) appeal draft → review",
          "A human approves every outbound action — submit and appeal both",
          "Payer-agnostic rules-as-data, with a visible UK ⇄ US switch that swaps criteria, "
          "terminology and the appeal-escalation path",
          "Grounding: every recommendation cites the policy clause and the precedent case ids that drove it",
          "Estimated Recoverable Value — the appeals gap as the payer's own number "
          "(not-appealed × comparable-win-rate × mean-claim-value)"]:
    bullet([(x, False)])
sub("Out of scope — by design")
for x in ["Real payer EHR / FHIR integration — synthetic + public-policy-excerpt data for the demo",
          "Real PHI — request ids and procedure codes only; nothing patient-identifying in logs or traces",
          "Automated submission without human sign-off — deliberately never built",
          "Clinical decision-making — this is decision support, never medical or coverage advice"]:
    bullet([(x, False)])
sub("What the pipeline assumes is already done (upstream)")
bullet([("Payer criteria already structured as a versioned Criterion(Id, Text, Mandatory) list; the "
         "clinical note and denial letter already plain text; precedent metadata already aggregated. "
         "The pipeline reads that extracted text against the structured criteria — it does not do OCR "
         "or policy-PDF ingestion.", False)])

# =========================================================== 3 · SOLUTION OVERVIEW
h1heading("3 · Solution overview")
para("A small team of reasoning agents that build and challenge a case, deterministic code that "
     "decides, and a human who authorises. Built on:", after=4)
bullet([("Microsoft Foundry Agent Service ", True), ("— the five reasoning agents", False)])
bullet([("the Microsoft Agent Framework ", True),
        ("— the orchestration layer: a typed workflow graph of executors, hosted on Azure Functions "
         "with Durable Task as the checkpoint / replay backend", False)])
bullet([("Azure Cosmos DB ", True), ("(serverless) for operational state; the unstructured corpus in "
        "Azure Blob Storage, indexed by one Foundry File Search vector index", False)])
bullet([("a Static Web App ", True), ("dashboard as the reviewer surface", False)])
sub("The idea in four moves")
bullet([("Build — ", True), ("needs-auth reads ambiguous plan language; evidence-gap maps the note "
        "to the written criteria; claims-extraction pulls the note's claims and flags a "
        "self-contradiction.", False)])
bullet([("Find the winning argument — ", True), ("precedent-strategist matches the fact pattern "
        "against recorded outcomes and drafts the appeal citing the exact clause and comparable cases.",
        False)])
bullet([("Challenge — ", True), ("critic never builds; it only tries to disprove the recommendation "
        "— unsupported claims, non-comparable precedents, over-strong conclusions, hidden "
        "contradictions — before a human sees it.", False)])
bullet([("Decide, then authorise — ", True), ("a deterministic Gate (never an agent) sets the "
        "route; every case that needs a submission pauses for a person; the Submission Adapter is "
        "the only path to a payer.", False)])
sub("The hybrid principle")
bullet([("Agents produce prose and judgement over unstructured text. Deterministic code owns every "
         "value that drives a decision or an action. The Gate is a plain executor, never an agent — "
         "preserved through the framework migration.", False)])
sub("Why an agent AND an executor per step")
bullet([("The agent does the unstructured reasoning and returns a typed recommendation; the "
         "deterministic executor then validates it (catching an off-record criterion or precedent), "
         "normalises it, and commits it to the workflow contract the Gate reads. The agent proposes "
         "in prose; code decides what is true.", False)])
sub("Built on the four core skills")
bullet([("Build (Challenge 1) — ", True), ("five Foundry agents via the SDK, each behind a typed "
        "interface with a deterministic stub twin.", False)])
bullet([("Monitor (Challenge 2) — ", True), ("one trace per request, per-agent spans, a per-agent "
        "£/$ meter — verified live from the deployed workflow host.", False)])
bullet([("Evaluate (Challenge 3) — ", True), ("Foundry portal eval (100% Coherence / Fluency) plus a "
        "20-case CI gate on safety metrics.", False)])
bullet([("Orchestrate (Challenge 4) — ", True), ("a Microsoft Agent Framework workflow graph with a "
        "first-class human-in-the-loop RequestPort.", False)])

# =========================================================== 4 · ARCHITECTURE
h1heading("4 · Technical architecture")
# python-docx needs a raster image. Render Care-Approval-IQ-Architecture_Design-V4.svg
# (the authoritative diagram) to this PNG at ~2x before running the build.
fig(f"{D}/Care-Approval-IQ-Architecture_Design-V4.png", w=6.95)
para("Five layers along the request path, plus three cross-cutting concerns.", after=4)

sub("1 · Intake")
bullet([("The request — procedure · plan · region · clinical note · prior denial letter.", False)])
bullet([("POST /api/cases on the API Proxy starts a workflow run, keyed on the request id.", False)])

sub("2 · Compute · Orchestration — Microsoft Agent Framework")
bullet([("CareApprovalWorkflow — a typed graph of executors, hosted on Zynara.WorkflowHost "
         "(Azure Functions + ConfigureDurableWorkflows).", False)])
bullet([("Each executor runs as a Durable Task activity — checkpointed, retried, replay-safe.", False)])
bullet([("The orchestrator owns the deterministic Gate; every send pauses at a RequestPort until a "
         "human answers /respond.", False)])

sub("3 · AI Foundry · Agent Service")
bullet([("Five reasoning agents — needs-auth · evidence-gap · claims-extraction · "
         "precedent-strategist · critic.", False)])
bullet([("Bound as MAF AIAgents to the existing server-side Foundry agents (bind-to-existing, never "
         "re-create); each also has a deterministic stub twin for CI.", False)])

sub("4 · Data")
bullet([("Submission Adapter (Azure Function) — the only path to a payer (portal / X12 278 / FHIR / fax).",
         False)])
bullet([("Azure Cosmos DB serverless — cases · submissions · auths · denials · agentCalls, plus "
         "precedent / policy metadata.", False)])
bullet([("Azure Blob Storage — policy docs · denial PDFs · precedent narratives.", False)])
bullet([("Foundry File Search — one vector index over the Blob corpus that the agents query.", False)])

sub("5 · Experience")
bullet([("Reviewer (human).", False)])
bullet([("Dashboard (Static Web App + linked backend) — review queue · approve / reject · "
         "recovery & cost, with a UK ⇄ US header control.", False)])
bullet([("API Proxy (Azure Function) — serves read models and relays /run + /respond/{runId} to the "
         "workflow.", False)])

sub("Cross-cutting (every layer)")
bullet([("Security & Identity — ", True), ("managed identity everywhere. The reasoning identity holds "
        "no payer secret; the submission identity has no model access. Key Vault holds the payer "
        "credential and the two unavoidable strings. No PHI anywhere.", False)])
bullet([("Observability — ", True), ("one trace per request (pipeline.run → spoke.* → invoke_agent * "
        "→ chat gpt-5.4 → submission.send), a per-agent £/$ cost meter, and the Zynara.Eval CI gate.",
        False)])
bullet([("Responsible AI — ", True), ("every send is human-approved; deterministic code owns every "
        "decision value; every recommendation keeps its citations; an in-product disclaimer states "
        "decision support, not coverage or medical advice.", False)])

# =========================================================== 5 · END-TO-END FLOW
h1heading("5 · End-to-end flow")
para("Submit → NeedsAuth → Gap → Claims → Precedent → Critic → Gate → Assemble → Pause for a "
     "reviewer → Send → (on denial) Appeal → Review", italic=True, size=10.5, color=GREY, after=6)
for x in [
    [("Submit — ", True), ("POST /api/cases starts a workflow run; the API Proxy polls the case "
     "store for the assembled result.", False)],
    [("NeedsAuth — ", True), ("resolve the payer + plan rule set; the agent narrates any ambiguous "
     "plan language. Not required → stop, nothing submitted.", False)],
    [("Gap — ", True), ("evidence-gap reads the note against the written criteria (File Search) → "
     "per-criterion status + supporting quote + evidence-quality grade.", False)],
    [("Claims — ", True), ("claims-extraction pulls the note's clinical claims and flags a "
     "self-contradiction.", False)],
    [("Precedent — ", True), ("deterministic code filters the precedent metadata; "
     "precedent-strategist reasons over the retrieved narratives and recommends submit / strengthen "
     "/ appeal.", False)],
    [("Critic — ", True), ("runs its seven checks; verdict (Clear / Concerns / Block / Abstain) and "
     "any flags attach to the case.", False)],
    [("Gate — ", True), ("deterministic. Any unmet mandatory criterion or a contradiction → "
     "HumanReview; a Critic block → HumanReview; thin evidence + weak precedent → Abstain; over the "
     "value limit → HumanReview; an undocumented supporting criterion → Strengthen; otherwise → "
     "ReadyToSubmit. Every route carries its working.", False)],
    [("Assemble & persist — ", True), ("one executor runs the whole pipeline, writes the result to "
     "Cosmos, then emits a slim routing message.", False)],
    [("Pause for a reviewer — ", True), ("every route raises a review card on the RequestPort and "
     "suspends. The route only changes the headline — “ready — one click to send” for ReadyToSubmit, "
     "“needs your judgement” for HumanReview.", False)],
    [("Send — ", True), ("the reviewer's /respond resumes the workflow; the apply-decision executor "
     "enforces ApprovalAuthority; on an authorised approve-send the Submission Adapter submits.", False)],
    [("Appeal — ", True), ("on denial, precedent-strategist drafts the appeal citing the misapplied "
     "clause and the precedent ids; critic reviews it; deterministic code fills the dates and the "
     "region-specific escalation route; the reviewer approves; the Adapter files it.", False)],
]:
    numbered(x)
para("Throughout: one trace id per request across every hop; every agent call written to "
     "agentCalls; no agent performs an outbound action.", size=10.5, color=GREY, before=4)

# =========================================================== 6 · COMPONENTS
h1heading("6 · Components, service by service")

sub("Intake / API Proxy")
bullet([("API Proxy ", True), ("(Azure Function) — validates the request DTO, starts a workflow run "
        "keyed on the request id, serves read models to the dashboard, and relays reviewer decisions "
        "to /respond/{runId}. Shares the Cosmos case store with the workflow host.", False)])

sub("Compute · Orchestration (Microsoft Agent Framework)")
bullet([("CareApprovalWorkflow ", True), ("— the typed executor graph. Two shapes from one "
        "definition: a fine-grained per-step graph for the eval harness and the in-process tests, and "
        "a coarse durable graph for the hosted app.", False)])
bullet([("Executors ", True), ("— needs-auth, evidence-gap, claims-extraction, precedent-match, "
        "critic, the Gate, assemble, review-card, apply-decision. Each is a thin wrapper over the "
        "existing Zynara.Core logic; no LLM in the wrapper.", False)])
bullet([("Zynara.WorkflowHost ", True), ("(Azure Functions) — ConfigureDurableWorkflows generates "
        "the HTTP surface (/run, /respond/{runId}) and one Durable activity per executor at runtime. "
        "AzureWebJobsStorage is the Durable Task backend — no separate scheduler resource.", False)])

sub("AI Foundry · Agent Service")
bullet([("needs-auth-agent ", True), ("— reads ambiguous plan text and returns "
        "{authRequired, code, policyRef}. ", False), ("Grounding: ", True), ("payer rule-set.", False)])
bullet([("evidence-gap-agent ", True), ("— reads the clinical note against the numbered criteria, "
        "per criterion, with the supporting quote and an evidence-quality grade. ", False),
        ("Grounding: ", True), ("File Search over the policy corpus.", False)])
bullet([("claims-extraction-agent ", True), ("— pulls the note's clinical claims and flags a "
        "self-contradiction. ", False), ("Grounding: ", True), ("the clinical note.", False)])
bullet([("precedent-strategist-agent ", True), ("— corpus-level fact-pattern match over recorded "
        "outcomes; drafts the appeal argument. ", False), ("Grounding: ", True),
        ("precedent metadata (Cosmos) + narratives (File Search).", False)])
bullet([("critic-agent ", True), ("— tries to disprove the assembled recommendation (the seven "
        "checks) and can force a human or an abstention. ", False), ("Grounding: ", True),
        ("the whole assembled case + File Search to verify citations.", False)])

sub("Data")
bullet([("Submission Adapter ", True), ("(Azure Function) — the sole outbound path: payer portal / "
        "X12 278 / FHIR / fax. Guards: only approve-send cases, idempotent.", False)])
bullet([("Azure Cosmos DB ", True), ("serverless (Core / NoSQL) — one container per aggregate, "
        "partition key /requestId or /payerPlan, session consistency.", False)])
bullet([("Azure Blob Storage ", True), ("— policies/<region>/<payer>/<procedure>.md, "
        "denials/<requestId>.pdf, precedents/<caseId>.md.", False)])
bullet([("Foundry File Search ", True), ("— the one vector index over the Blob corpus.", False)])

sub("Experience")
bullet([("Reviewer ", True), ("(human) — approves or rejects the cases the workflow suspends on.", False)])
bullet([("Dashboard ", True), ("(Static Web App Standard + linked backend) — review queue, per-case "
        "evidence / precedents / Critic drawer, the Estimated Recoverable Value tab, the cost tab, "
        "and the UK ⇄ US switch.", False)])
bullet([("API Proxy ", True), ("(Azure Function) — read models + reviewer actions.", False)])

sub("Cross-cutting")
bullet([("Managed identity ", True), ("— Cosmos data-plane RBAC, Foundry (Cognitive Services User), "
        "Blob (Blob Data Contributor). Two identities, split by plane.", False)])
bullet([("Key Vault ", True), ("— the payer credential, the App Insights connection string, the "
        "content-share key.", False)])
bullet([("App Insights ", True), ("— one W3C trace per request; the trace id is written onto each "
        "agentCalls cost row.", False)])
bullet([("Zynara.Eval ", True), ("— the labelled clinical-case set, replayed in CI; hard-gates the "
        "safety metrics.", False)])
bullet([("Zynara.DbDeploy ", True), ("— azd post-provision: create containers, upload the corpus, "
        "seed the demo data.", False)])

# =========================================================== 7 · THE MAF WORKFLOW
h1heading("7 · The Microsoft Agent Framework workflow, in detail")
para("The orchestration layer is the design's centrepiece and the reference pattern for the team's "
     "next agent projects.", after=4)
sub("Why the framework, not a hand-rolled orchestrator")
bullet([("The Microsoft Agent Framework (the unification of Semantic Kernel and AutoGen) gives, as "
         "primitives, exactly what a prior-auth pipeline needs and would otherwise hand-build: a "
         "typed graph of executors with conditional edges, a first-class human-in-the-loop "
         "pause / resume (RequestPort), durable checkpoint + replay via Durable Task, and built-in "
         "OpenTelemetry.", False)])
bullet([("Building this reference pattern once, on the framework Microsoft recommends, is worth more "
         "than the equivalent bespoke Durable Functions code — for this project and the next.", False)])
sub("The graph")
bullet([("Request → assemble → (auth not required) finalize, or → review-card → [review "
         "RequestPort] → apply-decision → (approve-send, authorised) → submit.", False)])
bullet([("The Gate is a plain executor, never an agent.", False)])
bullet([("Every route pauses at the RequestPort. The Gate route only sets the reviewer's headline "
         "— it never decides whether a person is asked. No payer submission without an explicit "
         "human /respond.", False)])
sub("The coarse-graph constraint")
bullet([("The framework serialises the workflow's runtime snapshot into Durable Task's CustomStatus, "
         "hard-capped at 16 KB. A fine-grained graph that threaded every agent result down the edges "
         "exceeded that. So the hosted graph runs the whole reasoning pipeline inside one assemble "
         "executor, persists the bulky result to Cosmos, and threads only a slim routing message. "
         "The fine-grained per-step graph is retained for in-process runs and the tests.", False)])
sub("Verified live")
bullet([("Deployed to zynara-spike-rg; run end to end against real GPT-5.4. Every route suspends on "
         "the RequestPort; the reviewer's /respond resumes it; the authority check runs; on an "
         "authorised approve-send the case is submitted. No “system” decision anywhere. No "
         "CustomStatus error. App Insights shows the full trace tree from the workflow host. "
         "122 tests green on every commit (zynara-ci.yml).", False)])

# =========================================================== 8 · GATE & RESPONSIBLE AI
h1heading("8 · The deterministic Gate & Responsible AI")
sub("The Gate weighs a structured model, not one number")
bullet([("Inputs: mandatory-criterion status, supporting-criterion counts, evidence quality, "
         "contradiction flag, precedent-support level, the Critic verdict, the estimated value, "
         "whether this is an appeal.", False)])
bullet([("Rules: any unmet mandatory criterion or a contradiction → HumanReview; a Critic block "
         "→ HumanReview regardless of the numbers; a Critic abstain, or low-quality evidence with "
         "weak / none precedent support → Abstain; value over the auto-limit → HumanReview; an "
         "undocumented supporting criterion or Critic concerns → Strengthen; otherwise → ReadyToSubmit.",
         False)])
bullet([("A missing mandatory criterion is never averaged away. Every route is shown with its working.",
         False)])
sub("Responsible AI — built and demonstrated")
bullet([("Every outbound action is human-approved ", True), ("— submit and appeal both.", False)])
bullet([("Bounded agents ", True), ("— deterministic code owns every value that drives a decision or "
        "an action.", False)])
bullet([("Grounding & citation ", True), ("— every recommendation records the policy clause ids, "
        "criteria version and precedent case ids on the case document. Any decision replays: which "
        "criterion → which precedent → which clause.", False)])
bullet([("Reviewer roles + approval authority ", True), ("(ApprovalAuthority) — four roles "
        "(Coordinator → Reviewer → Senior Reviewer → Medical Director). The authority to "
        "approve-and-send is the higher of the route tier and the financial-risk tier; an appeal is "
        "always at least a Senior sign-off. A refused decision is written to the audit trail.", False)])
bullet([("Append-only case audit trail ", True), ("— every run records the agent implementation and "
        "version behind each step; every reviewer action records identity, role, timestamp, note; "
        "every refusal records the reason.", False)])
bullet([("No PHI in scope ", True), ("— request ids and procedure codes only in logs and traces.", False)])

# =========================================================== 9 · EVALUATION
h1heading("9 · Evaluation — proving it works")
sub("Quality — the Foundry portal evaluation")
bullet([("evidence-gap-agent scored in the Foundry portal: 100% Coherence / Fluency over a 15-turn "
         "dataset (runbook: docs/runbooks/challenge-3-portal-evaluation.md).", False)])
sub("Correctness — the Zynara.Eval CI gate")
para("24 labelled synthetic cases (ground truth per criterion, expected route, expected citations), "
     "replayed through the pipeline with deterministic reasoning stubs so CI is repeatable. Reports "
     "route agreement and safety-shaped metrics — not a headline accuracy figure. Current run:", after=4)
t = doc.add_table(rows=1, cols=3); t.style = "Table Grid"; t.alignment = WD_TABLE_ALIGNMENT.CENTER
for i, hh in enumerate(["Metric", "Result", "CI gate"]):
    c = t.rows[0].cells[i]; c.text = ""
    r = c.paragraphs[0].add_run(hh); r.bold = True; r.font.size = Pt(10); shade(c, HDR)
for row in [
    ("Evidence extraction — precision / recall", "100% / 100%", "—"),
    ("Mandatory-criterion false-negatives", "0 / 7", "must be 0"),
    ("Policy-citation accuracy", "100%", "≥ 95%"),
    ("Precedent-citation accuracy", "100%", "≥ 80%"),
    ("Hallucinated references", "0", "must be 0"),
    ("Appeal-recommendation agreement (23 cases)", "100%", "≥ 80%"),
    ("Safe-abstention rate", "4 / 4 (100%)", "≥ 80%"),
    ("Unsafe automations", "0", "must be 0"),
]:
    cs = t.add_row().cells
    for i, v in enumerate(row):
        cs[i].text = ""; rr = cs[i].paragraphs[0].add_run(v); rr.font.size = Pt(10)
        if i == 0: rr.bold = True
for r in t.rows:
    r.cells[0].width = Inches(3.5); r.cells[1].width = Inches(1.6); r.cells[2].width = Inches(1.6)
doc.add_paragraph().paragraph_format.space_after = Pt(4)
sub("Multi-agent vs. one credible generalist — same 24 cases")
bullet([("The baseline is one GPT-5.4 generalist given the same inputs and a prompt that explicitly "
         "tells it to never auto-submit, never call an unmet mandatory criterion met, and abstain on "
         "thin evidence. Its verbatim answers are committed as replay fixtures so CI stays offline.", False)])
bullet([("Route agreement with the expert labels: the pipeline 100%, the generalist 62.5%.", True)])
bullet([("Unsafe automations: the pipeline 0, the generalist 4 ", True),
        ("— it wanted to submit four cases an expert would send to a human, one with a "
         "self-contradicting note.", False)])
bullet([("Safe abstention: the pipeline 4 / 4, the generalist 1 / 4.", True)])
bullet([("The CI gate fails the build if the pipeline is not measurably safer than the generalist.", False)])

# =========================================================== 10 · DEPLOYMENT, SCOPE & ROADMAP
h1heading("10 · Deployment, scope & roadmap")
sub("Deliberate trade-offs for a solo build in the competition window")
for x in ["Microsoft Agent Framework for orchestration — a deliberate investment, not a deadline "
          "compromise; the reference pattern for the team's next agent projects. Rollback is the "
          "tagged v1.0-durable Durable Functions version.",
          "Synthetic + public-excerpt data over real payer integration — CMS-0057-F forces the real "
          "FHIR APIs into existence on a 2027 timeline; ~20 LLM-generated labelled cases, "
          "transcribed public criteria (Bupa CCSD + US Medicare LCDs), synthetic past outcomes.",
          "One resource group, references the existing Foundry account — everything deploys into "
          "zynara-spike-rg; Consumption (Y1) Linux Function apps; Cosmos serverless; Static Web App.",
          "Cosmos DB serverless for operational state, Blob + File Search for the corpus — document "
          "shape, single-partition reads, semantic retrieval owned by File Search.",
          "Pre-computed demo playback for the scripted 3-minute demo; the live pipeline shown "
          "separately on one fresh case."]:
    bullet([(x, False)])
sub("Deliberately not built — production hardening, named so the gap is understood")
for x in [
    ("Network isolation", " — Cosmos, Blob and Foundry behind private endpoints; the Function Apps "
     "VNet-integrated; a subnet boundary so the reasoning plane cannot reach the payer plane. The "
     "identity boundary is built; the network boundary is the layer on top."),
    ("AI governance (enforcement)", " — per-agent Foundry quota and rate limits, a model allow-list, "
     "per-tenant spend caps, prompt-shield / jailbreak filtering on agent inputs — enforced at the "
     "platform. Cost metering is built (the dashboard cost tab); enforcement is the production layer."),
    ("CI/CD deployment pipeline", " — build + test + eval-gate (already in CI) → azd deploy to a "
     "staging slot → smoke test → manual approval → production, with infra drift detection."),
    ("Model / prompt versioning", " — every agent version and its system prompt pinned against the "
     "outcomes it produced; prompt changes through the same PR + eval gate as code."),
    ("Live quality monitoring", " — the eval harness run against de-identified live traffic on a "
     "schedule, alerting on accuracy regression and drift."),
    ("HA / DR", " — Cosmos multi-region or zone-redundant writes; Blob GRS; a documented RTO / RPO "
     "and a tested restore runbook."),
    ("Secrets & compliance", " — Key Vault secret rotation, a data-residency guarantee per region, a "
     "retention / deletion policy, and a full PHI-handling design (the demo carries none by design)."),
    ("Real payer connectivity", " — the Submission Adapter's portal-RPA / X12 278 clearinghouse / "
     "FHIR Prior-Authorization implementations, plus an eligibility check."),
]:
    bullet([(x[0], True), (x[1], False)])
sub("Roadmap")
for x in ["FHIR R4 bundle intake — v1 takes a simplified request DTO",
          "Eligibility / active-coverage check — a separate payer system call",
          "Early Warnings — ExpiryMath (expiry date arithmetic) + PolicyDiff (policy version diff) as "
          "advisory monitors; interfaces and stubs exist, the feature is unwired",
          "Peer-to-peer prep — the clinician's talking points for the call with the insurer's medical director",
          "Learning from reviewer edits — the Gate threshold and the draft templates improve from what "
          "reviewers actually change"]:
    bullet([(x, False)])

hr()
para("Zynara Health (fictional) · Care Approval IQ · Microsoft Agent Framework · Architect Track "
     "Submission · Deepak Kumar", size=8.5, color=GREY, align=WD_ALIGN_PARAGRAPH.CENTER, before=4)

doc.save(DEST)
print("wrote", DEST, os.path.getsize(DEST))
