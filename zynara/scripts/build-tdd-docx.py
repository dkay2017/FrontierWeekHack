#!/usr/bin/env python3
"""Generate Care-Approval-IQ-TDD.docx — same shape as the TireForge TDD."""
import os
from docx import Document
from docx.shared import Pt, RGBColor, Inches
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

TEAL = RGBColor(0x0C, 0x7F, 0x76)
BLUE = RGBColor(0x1D, 0x4E, 0xD8)
INK  = RGBColor(0x14, 0x2B, 0x33)
GREY = RGBColor(0x44, 0x55, 0x5C)
HDR_SHADE = "E3EBFC"

DEST = "/workspaces/FrontierWeekHack/zynara/docs/design/Care-Approval-IQ-TDD.docx"
IMG  = "/workspaces/FrontierWeekHack/zynara/docs/design/Care-Approval-IQ-Architecture_DesignV2.png"

doc = Document()

# --- base styles ---
st = doc.styles["Normal"]
st.font.name = "Calibri"
st.font.size = Pt(10.5)
st.font.color.rgb = INK
st.paragraph_format.space_after = Pt(6)
st.paragraph_format.line_spacing = 1.1

for name, sz, col, bold in [("Heading 1", 15, BLUE, True), ("Heading 2", 11.5, INK, True),
                            ("Heading 3", 10.5, GREY, True)]:
    s = doc.styles[name]
    s.font.name = "Calibri"
    s.font.size = Pt(sz)
    s.font.bold = bold
    s.font.color.rgb = col
    s.paragraph_format.space_before = Pt(14 if name == "Heading 1" else 8)
    s.paragraph_format.space_after = Pt(4)
    s.paragraph_format.keep_with_next = True

sec = doc.sections[0]
sec.left_margin = sec.right_margin = Inches(0.85)
sec.top_margin = sec.bottom_margin = Inches(0.8)

def shade(cell, hex_):
    tcPr = cell._tc.get_or_add_tcPr()
    sh = OxmlElement("w:shd"); sh.set(qn("w:val"), "clear"); sh.set(qn("w:fill"), hex_)
    tcPr.append(sh)

def para(text="", *, italic=False, size=None, color=None, bold=False, after=6, align=None):
    p = doc.add_paragraph()
    if align: p.alignment = align
    p.paragraph_format.space_after = Pt(after)
    if text:
        r = p.add_run(text); r.italic = italic; r.bold = bold
        if size: r.font.size = Pt(size)
        if color: r.font.color.rgb = color
    return p

def rich(segments, *, style=None, after=6, size=None):
    """segments: list of (text, bold) or (text, bold, italic)."""
    p = doc.add_paragraph(style=style)
    p.paragraph_format.space_after = Pt(after)
    for seg in segments:
        t, b = seg[0], seg[1]
        it = seg[2] if len(seg) > 2 else False
        r = p.add_run(t); r.bold = b; r.italic = it
        if size: r.font.size = Pt(size)
    return p

def bullet(segments, *, level=0):
    return rich(segments, style="List Bullet" if level == 0 else "List Bullet 2", after=3)

def numbered(segments):
    return rich(segments, style="List Number", after=3)

def table(headers, rows, widths=None):
    t = doc.add_table(rows=1, cols=len(headers))
    t.style = "Table Grid"
    t.alignment = WD_TABLE_ALIGNMENT.CENTER
    t.autofit = True
    for i, h in enumerate(headers):
        c = t.rows[0].cells[i]
        c.text = ""
        run = c.paragraphs[0].add_run(h)
        run.bold = True; run.font.size = Pt(8.5); run.font.color.rgb = INK
        shade(c, HDR_SHADE)
    for row in rows:
        cells = t.add_row().cells
        for i, val in enumerate(row):
            cells[i].text = ""
            p = cells[i].paragraphs[0]
            p.paragraph_format.space_after = Pt(2)
            for seg in (val if isinstance(val, list) else [(val, False)]):
                tx, b = seg[0], seg[1]
                r = p.add_run(tx); r.bold = b; r.font.size = Pt(8.5)
    if widths:
        for row in t.rows:
            for i, w in enumerate(widths):
                row.cells[i].width = Inches(w)
    doc.add_paragraph().paragraph_format.space_after = Pt(2)
    return t

def hrule():
    p = doc.add_paragraph()
    pPr = p._p.get_or_add_pPr()
    bdr = OxmlElement("w:pBdr")
    b = OxmlElement("w:bottom")
    b.set(qn("w:val"), "single"); b.set(qn("w:sz"), "6"); b.set(qn("w:color"), "0FA3A0")
    bdr.append(b); pPr.append(bdr)

# ============ TITLE BLOCK ============
h = doc.add_paragraph(); h.paragraph_format.space_after = Pt(2)
r = h.add_run("Zynara Health · Care Approval IQ"); r.bold = True; r.font.size = Pt(20); r.font.color.rgb = TEAL
r2 = h.add_run("\nTechnical Design Document"); r2.bold = True; r2.font.size = Pt(13); r2.font.color.rgb = INK
para("Proof beats paperwork.", italic=True, size=10.5, color=TEAL, after=2)
para("Microsoft Agent-a-thon 2026 · Architect Track Submission · Healthcare Prior Authorisation · "
     "Deepak Kumar · Original Work", size=9, color=GREY, after=2)
para("Built on the Microsoft Agent Framework. Companion artefacts: the architecture SVG/PNG "
     "(authoritative diagram), DECISIONS.md (delta log), STATUS.md (build state).",
     italic=True, size=8.5, color=GREY, after=4)
hrule()

# ============ 1 ============
doc.add_heading("1. Problem Statement", level=1)
para("A clinician wants to treat a patient — an MRI, a specialist referral, physiotherapy, surgery. "
     "Before it can go ahead, the patient's insurer must authorise it in advance (“prior authorisation”). "
     "Today that process is manual, slow, and error-prone.")
bullet([("What this costs: ", True),
        ("US hospitals spend $687B/yr on administration vs $346B on direct patient care; prior "
         "authorisation is named the single largest automatable cost centre in health-system admin. "
         "Patients wait weeks; some get worse waiting.", False)])
bullet([("What's missing: ", True),
        ("nobody reads the clinical chart against the specific payer's specific criteria before "
         "submission, and nobody checks whether the fact pattern historically wins on appeal.", False)])
bullet([("The appeals gap (the number this design is built around): ", True),
        ("only 11.5% of denied requests are appealed, yet 80.7% of appeals win (95% for "
         "skilled-nursing). 62% of physicians don't appeal because they believe they'll lose — a "
         "belief provably wrong from the payer's own recorded outcomes.", False)])
bullet([("The ask (per the course brief): ", True),
        ("design a production-ready multi-agent workflow — ≥2 specialised agents with distinct "
         "responsibilities, the tools and data each uses, and why multi-agent beats a single generalist "
         "— that moves a real business process from prototype to production.", False)])

# ============ 2 ============
doc.add_heading("2. Mission & Scope", level=1)
rich([("Request inputs: ", True),
      ("procedure/service requested · patient coverage reference · free-text clinical note · payer + plan · "
       "(post-decision) the denial letter.", False)])
rich([("Mission:", True)], after=3)
for m in ["Assemble a gap-checked prior-authorisation submission",
          "Recommend and draft the appeal when the fact pattern historically wins",
          "Keep a person in front of every outbound action"]:
    bullet([(m, False)])
rich([("In scope for this design:", True)], after=3)
for m in ["End-to-end pipeline: request → needs-auth → evidence gap → claims extraction → "
          "precedent match → critic → deterministic Gate → human review → submit → "
          "(on denial) appeal draft → review",
          "Human-in-the-loop approval for every outbound action (submit and appeal)",
          "Payer-agnostic rules-as-data, with a visible UK ⇄ US region switch that swaps the criteria "
          "set, terminology and appeal-escalation path",
          "Grounding: every recommendation cites the policy clause and the precedent case ids that drove it",
          "The Estimated Recoverable Value — the appeals-gap statistic as the payer's own number "
          "(not-appealed × comparable-win-rate × mean-claim-value)"]:
    bullet([(m, False)])
rich([("Explicitly out of scope (see §10):", True)], after=3)
for m in ["Real payer EHR/FHIR integration — synthetic + public-policy-excerpt data for the demo",
          "Real PHI — request ids and procedure codes only; no patient-identifying data in telemetry or logs",
          "Automated submission without human sign-off — deliberately never built",
          "Clinical decision-making — the system is decision support, never medical or coverage advice"]:
    bullet([(m, False)])

doc.add_heading("2.1 Upstream assumptions", level=2)
para("The pipeline starts after document extraction. Assumed done by processes outside this design: "
     "payer criteria already structured as a versioned Criterion(Id, Text, Mandatory) list in Cosmos; "
     "the clinical note and the denial letter already plain text; precedent metadata and denial-history "
     "cohorts already aggregated. What the pipeline owns: reading that extracted text against the "
     "structured criteria, matching precedents, and grounding the agents in the policy/precedent corpus "
     "via Foundry File Search.")

# ============ 3 ============
doc.add_heading("3. Solution Overview", level=1)
para("Care Approval IQ is a multi-agent system on:")
bullet([("Microsoft Foundry Agent Service ", True), ("— the five reasoning agents", False)])
bullet([("the Microsoft Agent Framework ", True),
        ("— the orchestration layer: a typed workflow graph of executors, hosted on Azure Functions "
         "with Durable Task as the checkpoint / replay backend", False)])
bullet([("Azure Cosmos DB ", True),
        ("(serverless) for operational state; the unstructured corpus (policy docs, denial letters, "
         "precedent narratives) in Azure Blob Storage indexed by Foundry File Search (one vector index)", False)])
bullet([("a Static Web App ", True), ("dashboard as the reviewer surface", False)])
para("It replaces manual pre-auth assembly and the “don't bother appealing” reflex with a small team "
     "of reasoning agents that build and challenge a case, deterministic code that decides, and a human "
     "who authorises.")
bullet([("Five reasoning agents that collaborate, not a pipeline of prompts. ", True),
        ("needs-auth reads ambiguous plan language · evidence-gap maps the clinical note to the written "
         "criteria · claims-extraction pulls the note's claims and flags a self-contradiction · "
         "precedent-strategist reasons over recorded precedent outcomes and drafts the appeal · critic "
         "tries to disprove the assembled recommendation. Agents reason and challenge one another; the "
         "deterministic Gate decides; the human authorises.", False)])
bullet([("One mandatory outbound path ", True),
        ("(the Submission Adapter) — no agent submits to a payer or files an appeal directly.", False)])
bullet([("A deterministic Gate that weighs a structured model, not one averaged number. ", True),
        ("Mandatory criteria are a hard gate — never averaged away; a contradiction is never silently "
         "resolved; the system can explicitly abstain. Four routes: AutoSubmit / Strengthen / HumanReview "
         "/ Abstain, each shown with its working.", False)])
bullet([("Hybrid principle. ", True),
        ("Deterministic code owns every value that drives a decision or an action; agents produce the "
         "prose and the judgement over unstructured text. Preserved through the framework migration — "
         "the Gate is a plain executor, never an agent.", False)])

doc.add_heading("3.1 Why multi-agent beats one generalist", level=2)
para("One prompt asked to “read the plan, map the note to the criteria, weigh the precedents, draft the "
     "appeal, and check your own work” is worse at every part and impossible to trust. This design is "
     "multi-agent because the agents do genuinely different reasoning and hold each other to account:")
for seg in [
    [("Different reasoning modes. ", True),
     ("Ambiguous-contract reading, free-text comprehension against a rubric, claim extraction, "
      "corpus-level fact-pattern matching, and adversarial self-review are five different skills. Each "
      "agent has one job, one prompt, one evaluation target.", False)],
    [("A dedicated challenger. ", True),
     ("The critic never builds — it only tries to disprove. It catches unsupported claims, "
      "non-comparable precedents, over-strong conclusions and hidden contradictions that a generalist "
      "producing its own answer will not flag on itself. Its verdict can force a human or an abstention.",
      False)],
    [("Independent evaluation. ", True),
     ("evidence-gap is scored in isolation against a labelled dataset (§9) — precision, recall, "
      "mandatory false-negative rate. Only possible because its job is not entangled with four others.",
      False)],
    [("A seam for the human and for the Gate. ", True),
     ("The Gate sits between the agents' reasoning and any action; the RequestPort sits between the Gate "
      "and the reviewer. A monolithic agent that both decides and acts leaves nowhere for either to live.",
      False)],
    [("Auditability. ", True),
     ("Each agent's output is a separate cited record: which criterion, which precedent, which clause, "
      "what the Critic challenged. A regulator or the Financial Ombudsman Service can follow the trail.",
      False)],
]:
    numbered(seg)

# ============ 4 ============
doc.add_heading("4. Technical Architecture", level=1)
if os.path.exists(IMG):
    doc.add_picture(IMG, width=Inches(6.8))
    doc.paragraphs[-1].alignment = WD_ALIGN_PARAGRAPH.CENTER
    para("Care-Approval-IQ-Architecture_DesignV2.svg is the authoritative source; the .png is the same "
         "diagram rasterised for Word / PDF export.", italic=True, size=8, color=GREY, after=8,
         align=WD_ALIGN_PARAGRAPH.CENTER)
para("Five layers along the request path, plus three cross-cutting concerns.")
table(
    ["#", "Layer", "Components"],
    [
        ["1", [("Intake", True)],
         "The request contract (procedure · plan · region · clinical note · prior denial letter) → "
         "POST /api/cases on the API Proxy, which starts a workflow run (runId = request id)."],
        ["2", [("Compute · Orchestration", True), (" (Microsoft Agent Framework)", False)],
         "CareApprovalWorkflow — a typed graph of executors, hosted on Zynara.WorkflowHost (Azure "
         "Functions + ConfigureDurableWorkflows). Executors run as Durable Task activities — "
         "checkpointed, retried, replay-safe. The orchestrator owns the deterministic Gate; human review "
         "is a MAF RequestPort (pause → /respond/{runId} → resume)."],
        ["3", [("AI Foundry · Agent Service", True)],
         "Five reasoning agents — needs-auth · evidence-gap · claims-extraction · precedent-strategist "
         "· critic — bound as MAF AIAgents to the existing server-side Foundry agents; each behind a "
         "Zynara.Core interface with a deterministic stub twin."],
        ["4", [("Data", True)],
         "Submission Adapter (Azure Function · the only path to payer portal / X12 278 / FHIR / fax) → "
         "Azure Cosmos DB serverless (cases · submissions · auths · denials · agentCalls, plus precedents "
         "/ policies metadata) → Azure Blob Storage (policy docs · denial PDFs · precedent narratives) "
         "→ Foundry File Search (one vector index)."],
        ["5", [("Experience", True)],
         "Reviewer (human) → Dashboard (Zynara.Dashboard, Static Web App + linked backend): Review "
         "queue · Approve/reject (routed to the workflow RequestPort) · Recovery & cost — with a "
         "UK ⇄ US header control → API Proxy (Azure Function; read models + relays /run + "
         "/respond/{runId} to the workflow)."],
    ],
    widths=[0.3, 1.7, 4.7],
)
rich([("Cross-cutting (applies to every layer):", True)], after=3)
bullet([("Security & Identity — ", True),
        ("managed identity first: Cosmos data-plane RBAC, Foundry (Cognitive Services User), Blob (Blob "
         "Data Contributor). The reasoning identity holds no payer secret; the submission identity has no "
         "model access. The payer credential and the two unavoidable strings (App Insights connection, "
         "content-share key) live in Key Vault. No PHI anywhere in scope.", False)])
bullet([("Observability — ", True),
        ("App Insights via the Azure Monitor OpenTelemetry exporter. ZynaraTelemetry emits pipeline.run "
         "→ spoke.<name> → invoke_agent <name> → chat <model> per request (verified live from "
         "zynara-workflowhost). Per-agent cost meter off agentCalls, each row carrying the trace id. "
         "Zynara.Eval CI gate.", False)])
bullet([("Responsible AI — ", True),
        ("the Gate routes every gap or low-confidence case to a human; the Submission Adapter is the sole "
         "actor that touches a payer; deterministic code owns every decision value; every recommendation "
         "retains its grounding. Production hardening is named in §10, not built.", False)])

# ============ 5 ============
doc.add_heading("5. End-to-End Flow", level=1)
para("Submit → NeedsAuth → Gap → Claims → Precedent → Critic → Gate → "
     "Assemble → Route → [AutoSubmit | Review] → Send → (Decision) → Appeal → Review",
     italic=True, size=9.5, color=GREY)
for seg in [
    [("Submit — ", True),
     ("POST /api/cases on the API Proxy; it starts a workflow run "
      "(/api/workflows/CareApprovalPipeline/run?runId={requestId}) and polls the case store for the "
      "assembled result.", False)],
    [("NeedsAuth — ", True),
     ("the needs-auth executor resolves the payer+plan rule set; the agent narrates ambiguous plan "
      "language → {authRequired, code, policyRef}. Not required → stop, nothing submitted.", False)],
    [("Gap — ", True),
     ("evidence-gap reads the clinical note against the payer's written criteria (Foundry File Search) "
      "→ per-criterion status + supporting quote + evidence-quality grade.", False)],
    [("Claims — ", True),
     ("claims-extraction pulls the note's clinical claims and flags a self-contradiction.", False)],
    [("Precedent — ", True),
     ("precedent-match (deterministic) filters the precedents metadata; precedent-strategist reasons over "
      "the File-Search-retrieved narratives, reports the recorded outcomes, and recommends submit / "
      "strengthen / appeal.", False)],
    [("Critic — ", True),
     ("critic runs its seven checks; verdict (Clear / Concerns / Block / Abstain) and any flags attach to "
      "the case.", False)],
    [("Gate — ", True),
     ("deterministic. Any unmet mandatory criterion or a contradiction → HumanReview; a Critic Block "
      "→ HumanReview regardless of the numbers; thin evidence + weak precedent, or a Critic Abstain "
      "→ Abstain; value over the auto-limit → HumanReview; an undocumented supporting criterion, "
      "medium evidence, or Critic Concerns → Strengthen; otherwise → AutoSubmit. Every route "
      "carries its working.", False)],
    [("Assemble & persist — ", True),
     ("one assemble executor runs the whole reasoning pipeline in-process, persists the PipelineResult to "
      "Cosmos, then emits a slim routing message (coarse-graph design — §7).", False)],
    [("Route — ", True),
     ("AutoSubmit → the auto-approve executor records a system decision and hands the case to the "
      "Submission Adapter. Any other route → the workflow raises a ReviewCard on the review "
      "RequestPort and pauses.", False)],
    [("Review — ", True),
     ("the reviewer approves/rejects from the dashboard; the API Proxy delivers the decision to "
      "/respond/{runId}; the workflow resumes, the apply-decision executor enforces ApprovalAuthority, "
      "and on an authorised approve-send hands the case to the Submission Adapter.", False)],
    [("Send — ", True),
     ("the Submission Adapter submits to the payer in their format (portal / X12 278 / FHIR / fax) — "
      "the only outbound path.", False)],
    [("Decision → Appeal — ", True),
     ("on denial, precedent-strategist drafts the appeal citing the misapplied clause and the precedent "
      "ids; critic reviews it; deterministic code fills the dates and the region-specific escalation "
      "route; the reviewer approves; the Adapter files it.", False)],
]:
    numbered(seg)
para("Throughout: one trace id per request across every hop; every agent call written to agentCalls; no "
    "agent performs an outbound action.", size=9.5, color=GREY)

# ============ 6 ============
doc.add_heading("6. Components, Service by Service", level=1)
rich([("Intake / API Proxy — ", True),
      ("POST /api/cases validates the request DTO and starts a workflow run keyed on request id. The API "
       "Proxy also serves read models to the dashboard and relays reviewer decisions to /respond/{runId}. "
       "It shares the Cosmos case store with the workflow host.", False)])
rich([("Compute · Orchestration (Microsoft Agent Framework) —", True)], after=3)
bullet([("CareApprovalWorkflow — ", True),
        ("the typed executor graph. Two shapes from one definition: a fine-grained per-step graph (Build) "
         "used by the eval harness and the in-process tests, and a coarse durable graph (BuildDurable) "
         "for the hosted app.", False)])
bullet([("Executors — ", True),
        ("needs-auth, evidence-gap, claims-extraction, precedent-match, critic, the Gate, assemble, "
         "auto-approve, review-card, apply-decision. Each is a thin lambda over the existing Zynara.Core "
         "logic; deterministic wrappers, no LLM in the wrapper.", False)])
bullet([("Zynara.WorkflowHost — ", True),
        ("an Azure Functions app; FunctionsApplication.CreateBuilder + ConfigureDurableWorkflows "
         "auto-generate the HTTP surface (/run, /respond/{runId}) and one Durable activity per executor. "
         "Durable Task on AzureWebJobsStorage is the checkpoint / replay backend — no separate "
         "scheduler resource.", False)])
rich([("AI Foundry · Agent Service — ", True),
      ("five agents provisioned via Azure.AI.Projects (AgentAdministrationClient.CreateAgentVersion), "
       "bound at runtime as MAF AIAgents (AIProjectClient.AsAIAgent) — bind-to-existing, never "
       "re-create. Each behind a Zynara.Core interface with a deterministic stub twin.", False)])
table(
    ["Agent", "Reasoning job", "Tools / grounding"],
    [
        ["needs-auth-agent", "Plain-language reading of ambiguous plan text",
         "payer rule-set, procedure-code lookup"],
        ["evidence-gap-agent",
         "Clinical note vs. the numbered criteria — per-criterion status + supporting quote + "
         "evidence-quality grade", "Foundry File Search over policies/"],
        ["claims-extraction-agent", "Pulls the note's clinical claims; flags a self-contradiction",
         "the clinical note"],
        ["precedent-strategist-agent",
         "Corpus-level fact-pattern match over recorded outcomes; drafts the appeal argument",
         "precedents metadata + narratives (File Search)"],
        [[("critic-agent", True)],
         "Tries to disprove the assembled recommendation — the seven checks — and can force a "
         "human or an abstention", "the whole assembled case + File Search"],
    ],
    widths=[1.5, 3.3, 1.9],
)
rich([("Data — ", True),
      ("Submission Adapter (Function; the only path to payer portal / X12 / FHIR / fax); Azure Cosmos DB "
       "serverless (Core/NoSQL) — one container per aggregate, partition key /requestId or "
       "/payerPlan, session consistency; Azure Blob Storage "
       "(policies/<region>/<payer>/<procedure>.md, denials/<requestId>.pdf, precedents/<caseId>.md); "
       "Foundry File Search — the one vector index over the Blob corpus.", False)])
rich([("Experience — ", True),
      ("Reviewer (human); Dashboard (Zynara.Dashboard, Static Web App Standard + linked backend, "
       "same-origin /api); API Proxy.", False)])
rich([("Cross-cutting — ", True),
      ("managed identity; Key Vault (payer credential + App Insights + content-share strings); App "
       "Insights (one trace id per request); Zynara.Eval (labelled clinical-case set, CI-gated); "
       "Zynara.DbDeploy (azd post-provision — create containers, upload the corpus, seed).", False)])

# ============ 7 ============
doc.add_heading("7. The Microsoft Agent Framework Workflow — in Detail", level=1)
para("The orchestration layer is the design's centrepiece and the reference pattern for the team's "
     "forthcoming agent projects.")
rich([("Why the framework, not a hand-rolled orchestrator. ", True),
      ("The Microsoft Agent Framework — the unification of Semantic Kernel and AutoGen, Microsoft's "
       "recommended framework for production agent systems on .NET — provides, as framework "
       "primitives, exactly what a prior-auth pipeline needs and would otherwise hand-build: a typed "
       "graph of executors with conditional edges, a first-class human-in-the-loop pause/resume "
       "(RequestPort), durable checkpoint + replay via Durable Task, and built-in OpenTelemetry. "
       "Building this reference pattern once, on the framework Microsoft recommends, is worth more than "
       "the equivalent bespoke Durable Functions code — for this project and the next.", False)])
rich([("The graph.", True)], after=2)
mono = doc.add_paragraph()
mr = mono.add_run(
    "Request → assemble ─┬(AutoSubmit)→ auto-approve → submit\n"
    "                    └(else)→ review-card → [review RequestPort] → apply-decision\n"
    "                                                          └(approve-send, authorised)→ submit")
mr.font.name = "Consolas"; mr.font.size = Pt(8.5); mr.font.color.rgb = GREY
mono.paragraph_format.space_after = Pt(6)
bullet([("The Gate is a plain executor, ", True), ("never an agent — the hybrid principle, preserved.", False)])
bullet([("Human review is a RequestPort ", True),
        ("— the workflow pauses with a ReviewCard, the host answers /respond/{runId} with a "
         "ReviewDecision, the workflow resumes at apply-decision.", False)])
bullet([("assemble is coarse by design. ", True),
        ("The framework serialises the workflow's runtime snapshot into Durable Task's CustomStatus, "
         "which is hard-capped at 16 KB. A fine-grained graph that threaded every agent result down the "
         "edges exceeded that. So the hosted graph runs the whole reasoning pipeline inside one assemble "
         "executor, persists the bulky PipelineResult to Cosmos, and threads only a slim routing message "
         "from there. The fine-grained per-step graph is retained for InProcessExecution — that is "
         "where the per-step route-agreement tests and the eval harness run.", False)])
rich([("Hosting. ", True),
      ("ConfigureDurableWorkflows on an Azure Functions app generates the HTTP surface and the "
       "activity-per-executor set at runtime; AzureWebJobsStorage is the Durable Task backend, so there "
       "is no Durable Task Scheduler resource to provision. The workflow host runs alongside the v1 "
       "Durable Functions orchestrator during the transition and is deployed live to zynara-spike-rg.",
       False)])
rich([("Verified live ", True),
      ("(real GPT-5.4): both the auto-submit path and the human-review → respond → submit path "
       "complete end-to-end with no CustomStatus error; App Insights shows the full pipeline.run → "
       "spoke.* → invoke_agent * → chat gpt-5.4 → submission.send trace tree from "
       "zynara-workflowhost.", False)])

# ============ 8 ============
doc.add_heading("8. The Deterministic Gate & Responsible AI", level=1)
rich([("The Gate weighs a structured model, not one number. ", True),
      ("Inputs: mandatory-criterion status, supporting-criterion counts, evidence quality, contradiction "
       "flag, precedent-support level, the Critic verdict, the estimated value, and whether this is an "
       "appeal. Rules: any unmet mandatory criterion or a contradiction → HumanReview; a Critic Block "
       "→ HumanReview regardless of the numbers; a Critic Abstain, or low-quality evidence with "
       "weak/none precedent support → Abstain; value over the auto-limit → HumanReview; an "
       "undocumented supporting criterion, medium evidence, or Critic Concerns → Strengthen; "
       "otherwise → AutoSubmit. Every route is shown with its working.", False)])
rich([("In scope, built and demonstrated:", True)], after=3)
for seg in [
    [("Every outbound action is human-approved ", True),
     ("— submit and appeal both. The Submission Adapter is the sole actor with payer reach.", False)],
    [("Bounded agents ", True),
     ("— deterministic code owns every value that drives a decision or an action.", False)],
    [("Grounding & citation, retained ", True),
     ("— every recommendation records the policy clause ids, criteria version, and precedent case "
      "ids on the case document alongside the Gate's working. Any decision replays: which criterion "
      "→ which precedent → which clause.", False)],
    [("Reviewer roles + approval authority ", True),
     ("(ApprovalAuthority) — four roles (Coordinator → Reviewer → Senior Reviewer → "
      "Medical Director). The authority to approve-and-send is the higher of the route tier and the "
      "financial-risk tier; an appeal is always at least a Senior sign-off. A refused decision is "
      "written to the audit trail. Enforced in the apply-decision executor.", False)],
    [("Append-only case audit trail ", True),
     ("(CaseRecord.Audit) — every run records the agent implementation and version behind each step; "
      "every reviewer action records identity, role, timestamp, note; every refusal records the reason.",
      False)],
    [("No PHI in scope ", True),
     ("— request ids and procedure codes only in telemetry, logs, and the trace tree.", False)],
    [("Disclaimer, shown in-product: ", True),
     ("Care Approval IQ is decision support, not coverage or medical advice. A person makes every "
      "decision.", False, True)],
]:
    bullet(seg)

# ============ 9 ============
doc.add_heading("9. Evaluation — beyond classification accuracy", level=1)
para("Two halves.")
rich([("Quality — the Foundry portal evaluation ", True),
      ("of evidence-gap-agent: Coherence / Fluency over a 15-turn dataset; runbook "
       "docs/runbooks/challenge-3-portal-evaluation.md. Result: 100% Coherence / Fluency.", False)])
rich([("Correctness — the Zynara.Eval CI gate ", True),
      ("replays a 20-case labelled clinical set (ground truth per criterion + expected route + expected "
       "citations) and measures:", False)])
table(
    ["Metric", "Why it matters"],
    [
        ["Evidence-extraction precision / recall", "finds what is there without inventing what is not"],
        [[("Mandatory-criterion false-negative rate", True)],
         "the unsafe direction — calling a missing mandatory criterion “met” — CI hard-gate: 0"],
        ["Policy-citation accuracy", "the draft names the governing policy ref — CI floor 95%"],
        ["Precedent-citation accuracy", "the cited precedent ids match the labelled set — CI floor 80%"],
        [[("Hallucination rate", True)],
         "criterion / precedent / clause ids not on record — CI hard-gate: 0"],
        ["Appeal-recommendation agreement vs. labels",
         "the precedent-strategist verdict matches the expert label — CI floor 80%"],
        [[("Safe-abstention rate", True)],
         "of cases an expert marks “not enough to advise”, how many routed to Abstain — CI floor 80%"],
        [[("Unsafe-automation rate", True)],
         "cases auto-submitted that an expert would not have — CI hard-gate: 0"],
    ],
    widths=[2.6, 4.1],
)
para("The agents-vs-generalist comparison runs the same set through a single-prompt baseline: route "
    "agreement 100% vs the baseline's 55%; unsafe automation 0 vs the baseline's 5.")

# ============ 10 ============
doc.add_heading("10. Deployment & Scope Decisions", level=1)
para("Deliberate trade-offs for a solo build inside the competition window (deadline 2026-09-23 "
     "midnight US).")
for seg in [
    [("Microsoft Agent Framework for orchestration ", True),
     ("— a deliberate investment, not a deadline compromise: the reference pattern for the team's "
      "forthcoming agent projects and Microsoft's recommended approach. Rollback is the tagged "
      "v1.0-durable Durable Functions version.", False)],
    [("Synthetic + public-excerpt data over real payer integration ", True),
     ("— CMS-0057-F forces the real FHIR APIs into existence on a 2027 timeline; for the demo, "
      "~20 LLM-generated labelled clinical cases, transcribed public criteria (Bupa CCSD + a small set "
      "of US Medicare LCDs), synthetic past outcomes, and policy-version pairs for the drift narrative.",
      False)],
    [("One resource group, references the existing Foundry account ", True),
     ("— everything deploys into zynara-spike-rg alongside the pre-existing Foundry care-approval "
      "project; Consumption (Y1) Linux Function apps; identity-based storage; Cosmos serverless; Static "
      "Web App.", False)],
    [("Cosmos DB serverless for operational state, Blob + File Search for the corpus ", True),
     ("— see §12 (TD-4, TD-5).", False)],
    [("Pre-computed demo playback over live inference ", True),
     ("for the scripted 3-minute demo; the live pipeline is shown separately on one fresh case.", False)],
    [("Production hardening named, not built ", True),
     ("— network isolation (private endpoints, a subnet boundary between the reasoning plane and the "
      "payer plane), an AI-governance layer (per-agent quota / rate limits / spend caps), a CI/CD deploy "
      "pipeline, model / prompt versioning linked to outcomes, live quality monitoring on de-identified "
      "traffic, HA/DR, secret rotation, and a full PHI-handling design. The identity boundary is built; "
      "the network boundary is the production layer on top.", False)],
]:
    bullet(seg)

# ============ 11 ============
doc.add_heading("11. Out of Scope · Future Roadmap", level=1)
for seg in [
    [("Real payer connectivity ", True),
     ("— FHIR Prior Authorization / Provider Access APIs, X12 278 clearinghouse, portal RPA "
      "(CMS-0057-F, Jan 2027).", False)],
    [("FHIR bundle intake ", True), ("— v1 takes a simplified request DTO.", False)],
    [("Eligibility / active-coverage check ", True),
     ("— a separate payer system call; assumed valid for the demo.", False)],
    [("Early Warnings ", True),
     ("— ExpiryMath (expiry date arithmetic) and PolicyDiff (policy version diff) as advisory "
      "deterministic monitors; the interfaces and stubs exist, the feature is unwired.", False)],
    [("Peer-to-peer prep ", True),
     ("— the clinician's talking points for the call with the insurer's medical director.", False)],
    [("Learning from reviewer edits ", True),
     ("— closing the loop so the Gate threshold and the draft templates improve from what reviewers "
      "change.", False)],
]:
    bullet(seg)

# ============ 12 ============
doc.add_heading("12. Technology Decisions (ADR summary)", level=1)
table(
    ["#", "Decision", "Why", "Rejected alternative"],
    [
        [[("TD-1", True)],
         "Orchestration is a Microsoft Agent Framework workflow — a typed executor graph — not "
         "agent-to-agent chaining",
         "The auto-submit-vs-review decision, the readiness threshold, the pipeline order and the audit "
         "trail must be deterministic and inspectable; agents must never own the Gate. The framework "
         "gives the typed graph, the HITL port, and durable replay as primitives.",
         "A generalist agent orchestrating via connected agents — non-deterministic order, no "
         "auditable Gate. A hand-rolled Durable Functions hub — more bespoke code for the same "
         "properties the framework owns."],
        [[("TD-2", True)],
         "The pipeline steps are MAF executors with a deterministic wrapper, each with a stub twin",
         "Per-step retry isolation; the prose→typed-value boundary is a named unit; a stub twin per "
         "step runs the whole pipeline in CI with zero live inference; replay-safe resume via Durable Task.",
         "Five helper methods inside one function — loses all four."],
        [[("TD-3", True)],
         "No explicit request queue — the workflow's HTTP /run is the async boundary",
         "Request/response at low volume, no burst to absorb; /run returns 202 immediately and the "
         "workflow runs on Durable Task's own control queues.",
         "A dedicated Storage Queue — an extra resource and failure mode for load that does not exist."],
        [[("TD-4", True)],
         "Operational store is Azure Cosmos DB serverless, not Azure SQL",
         "A case is a nested aggregate that maps to one JSON document; partition by /requestId makes "
         "“everything for this case” a single-partition read; serverless billing fits spiky demo "
         "volume; no migrations.",
         "Azure SQL serverless — relational schema, EF migrations, join modelling for "
         "document-shaped data."],
        [[("TD-5", True)],
         "Unstructured corpus is Blob Storage + Foundry File Search, one vector index, separate from Cosmos",
         "Retrieval over the corpus is semantic, not a query; File Search builds and owns the index "
         "natively. Cosmos keeps only the structured metadata.",
         "Cosmos-native vector search — couples the corpus to the operational DB and still needs a "
         "home-grown embedding pipeline. Azure AI Search — another service to secure for no demo gain."],
        [[("TD-6", True)],
         "Foundry Agent Service (persistent hosted agents) bound as MAF AIAgents",
         "Agents are versioned, portal-visible assets with File Search + tool-calling + tracing wired; "
         "AsAIAgent binds to the existing server-side agent (never re-creates); each agent stays behind a "
         "Zynara.Core interface.",
         "Hand-rolled chat-completions calls — rebuild retrieval, tool loop, versioning and tracing; "
         "lose the portal asset view."],
        [[("TD-7", True)],
         "C# / .NET 8, azd + Bicep, one resource group, managed-identity-first from commit 1, App "
         "Insights + W3C trace context",
         "The Agent Framework's .NET SDK is first-class; static types make the prose→contract "
         "boundary a compile-time guarantee; one azd up provisions and deploys; one correlated trace per "
         "request feeds both the audit trail and the cost meter.",
         "Portal click-ops or retrofitted identity — not reproducible, a security gap."],
    ],
    widths=[0.4, 1.6, 2.5, 2.2],
)

hrule()
para("Zynara Health (fictional) · Care Approval IQ · Microsoft Agent Framework · Deepak Kumar · "
     "Architect Track Submission", size=8, color=GREY, align=WD_ALIGN_PARAGRAPH.CENTER)

doc.save(DEST)
print("wrote", DEST, os.path.getsize(DEST), "bytes")
