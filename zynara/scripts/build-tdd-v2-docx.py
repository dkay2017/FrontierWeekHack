#!/usr/bin/env python3
"""Care-Approval-IQ-TDD-V2.docx — story-first, infographic-led, tight bullets."""
import os
from docx import Document
from docx.shared import Pt, RGBColor, Inches
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

TEAL = RGBColor(0x0C, 0x7F, 0x76)
BLUE = RGBColor(0x1D, 0x4E, 0xD8)
INK  = RGBColor(0x12, 0x2A, 0x32)
GREY = RGBColor(0x44, 0x55, 0x5C)
HDR  = "E3EBFC"

D = "/workspaces/FrontierWeekHack/zynara/docs/design"
DEST = f"{D}/Care-Approval-IQ-TDD-V2.docx"
FIG  = f"{D}/tdd-v2-figures"
ARCH = f"{D}/Care-Approval-IQ-Architecture_DesignV2.png"

doc = Document()
s = doc.styles["Normal"]
s.font.name = "Calibri"; s.font.size = Pt(10.5); s.font.color.rgb = INK
s.paragraph_format.space_after = Pt(5); s.paragraph_format.line_spacing = 1.12

for name, sz, col in [("Heading 1", 15, BLUE), ("Heading 2", 11, INK)]:
    h = doc.styles[name]
    h.font.name = "Calibri"; h.font.size = Pt(sz); h.font.bold = True; h.font.color.rgb = col
    h.paragraph_format.space_before = Pt(15 if name == "Heading 1" else 6)
    h.paragraph_format.space_after = Pt(5); h.paragraph_format.keep_with_next = True

sec = doc.sections[0]
sec.left_margin = sec.right_margin = Inches(0.8)
sec.top_margin = sec.bottom_margin = Inches(0.75)

def shade(cell, hx):
    tcPr = cell._tc.get_or_add_tcPr()
    e = OxmlElement("w:shd"); e.set(qn("w:val"), "clear"); e.set(qn("w:fill"), hx); tcPr.append(e)

def p(text="", *, italic=False, size=None, color=None, bold=False, after=5, align=None):
    par = doc.add_paragraph()
    if align: par.alignment = align
    par.paragraph_format.space_after = Pt(after)
    if text:
        r = par.add_run(text); r.italic = italic; r.bold = bold
        if size: r.font.size = Pt(size)
        if color: r.font.color.rgb = color
    return par

def rich(segs, *, style=None, after=5, size=None):
    par = doc.add_paragraph(style=style); par.paragraph_format.space_after = Pt(after)
    for seg in segs:
        t, b = seg[0], seg[1]; it = seg[2] if len(seg) > 2 else False
        r = par.add_run(t); r.bold = b; r.italic = it
        if size: r.font.size = Pt(size)
    return par

def bullet(segs):
    return rich(segs, style="List Bullet", after=3)

def fig(path, w=6.7, cap=None):
    if not os.path.exists(path):
        p(f"[missing figure: {path}]", italic=True, color=GREY); return
    doc.add_picture(path, width=Inches(w))
    doc.paragraphs[-1].alignment = WD_ALIGN_PARAGRAPH.CENTER
    doc.paragraphs[-1].paragraph_format.space_before = Pt(4)
    doc.paragraphs[-1].paragraph_format.space_after = Pt(2 if cap else 8)
    if cap:
        p(cap, italic=True, size=8, color=GREY, after=8, align=WD_ALIGN_PARAGRAPH.CENTER)

def table(headers, rows, widths):
    t = doc.add_table(rows=1, cols=len(headers)); t.style = "Table Grid"
    t.alignment = WD_TABLE_ALIGNMENT.CENTER
    for i, h in enumerate(headers):
        c = t.rows[0].cells[i]; c.text = ""
        r = c.paragraphs[0].add_run(h); r.bold = True; r.font.size = Pt(8.5); r.font.color.rgb = INK
        shade(c, HDR)
    for row in rows:
        cs = t.add_row().cells
        for i, val in enumerate(row):
            cs[i].text = ""; par = cs[i].paragraphs[0]; par.paragraph_format.space_after = Pt(2)
            for seg in (val if isinstance(val, list) else [(val, False)]):
                rr = par.add_run(seg[0]); rr.bold = seg[1]; rr.font.size = Pt(8.5)
    for r in t.rows:
        for i, w in enumerate(widths):
            r.cells[i].width = Inches(w)
    doc.add_paragraph().paragraph_format.space_after = Pt(2)

def hr():
    par = doc.add_paragraph(); pPr = par._p.get_or_add_pPr()
    b = OxmlElement("w:pBdr"); bot = OxmlElement("w:bottom")
    bot.set(qn("w:val"), "single"); bot.set(qn("w:sz"), "6"); bot.set(qn("w:color"), "0FA3A0")
    b.append(bot); pPr.append(b)

def callout(title, lines):
    t = doc.add_table(rows=1, cols=1); t.style = "Table Grid"
    c = t.rows[0].cells[0]; shade(c, "F1F7F6"); c.text = ""
    par = c.paragraphs[0]; r = par.add_run(title); r.bold = True; r.font.size = Pt(10); r.font.color.rgb = TEAL
    for ln in lines:
        q = c.add_paragraph(); q.paragraph_format.space_after = Pt(2)
        for seg in ln:
            rr = q.add_run(seg[0]); rr.bold = seg[1]; rr.font.size = Pt(9.5)
    c.width = Inches(6.9)
    doc.add_paragraph().paragraph_format.space_after = Pt(2)

# ================= TITLE =================
h = doc.add_paragraph(); h.paragraph_format.space_after = Pt(2)
r = h.add_run("Care Approval IQ"); r.bold = True; r.font.size = Pt(22); r.font.color.rgb = TEAL
r2 = h.add_run("   Technical Design Document · v2"); r2.bold = True; r2.font.size = Pt(12); r2.font.color.rgb = INK
p("Proof beats paperwork.", italic=True, size=11, color=TEAL, after=2)
p("Zynara Health (fictional) · Deepak Kumar · Architect Track · Microsoft Agent-a-thon 2026 · "
  "built on the Microsoft Agent Framework", size=9, color=GREY, after=4)
hr()
p("The whole system in one line: five reasoning agents build and challenge a prior-authorisation "
  "case, deterministic code decides, a human authorises, and one adapter talks to the payer — "
  "with the payer's own denial history telling us which appeals win.", size=10.5, bold=False, after=8)

# ================= 1 · THE PROBLEM =================
doc.add_heading("1 · The problem, in one number", level=1)
p("62% of physicians don't appeal a denied prior-authorisation. They think they'll lose. "
  "They're wrong — 80.7% of appeals win.", bold=True, size=11, after=6)
fig(f"{FIG}/fig-appeals-gap.png")
bullet([("Prior authorisation is the single largest automatable cost in US health admin — "
         "$687B/yr on administration against $346B on direct patient care.", False)])
bullet([("Nobody reads the clinical chart against ", False),
        ("this specific payer's, this specific plan's", False, True),
        (" criteria before the request goes in.", False)])
bullet([("Nobody checks the payer's own recorded outcomes to see whether the case wins on appeal.", False)])
bullet([("So the clinician gives up, and the winnable-but-un-appealed cases stay as money the "
         "insurer keeps by default.", False)])

# ================= 2 · THE SOLUTION =================
doc.add_heading("2 · The solution", level=1)
fig(f"{FIG}/fig-pipeline-flow.png")
bullet([("Build the submission — ", True),
        ("needs-auth, evidence-gap and claims-extraction map the free-text note onto the numbered "
         "criteria, per criterion, with the supporting quote.", False)])
bullet([("Find the winning argument — ", True),
        ("precedent-strategist matches the fact pattern against recorded outcomes and drafts the "
         "appeal citing the exact clause and the comparable cases.", False)])
bullet([("Challenge it — ", True),
        ("critic never builds; it only tries to disprove the recommendation — unsupported claims, "
         "non-comparable precedents, over-strong conclusions, hidden contradictions — before a "
         "human ever sees it.", False)])
bullet([("Decide, then authorise — ", True),
        ("a deterministic Gate (never an agent) routes to AutoSubmit / Strengthen / HumanReview / "
         "Abstain, each with its working. The route sets the reviewer's headline and default action; "
         "it never decides whether a person is asked. No payer submission happens without an explicit "
         "human /respond, and the Submission Adapter is the only path to a payer.", False)])
rich([("Why an agent AND an executor per step. ", True),
      ("The agent does the unstructured reasoning and returns a typed recommendation; the deterministic "
       "executor then validates it (catching a criterion id or precedent that isn't on record), "
       "normalises it, and commits it to the workflow contract the Gate consumes. The agent proposes "
       "in prose; code decides what is true.", False)])

# ================= 3 · THE FOUR CORE SKILLS =================
doc.add_heading("3 · Built on the four core skills", level=1)
p("The four skills the course is built around — Build, Monitor, Evaluate, Orchestrate — each "
  "maps to one challenge, and each is carried end to end into a system that runs live.", after=6)
fig(f"{FIG}/fig-four-skills.png")

rich([("The evaluation, in numbers. ", True),
      ("Zynara.Eval replays 20 labelled synthetic cases (ground truth per criterion, expected route, "
       "expected citations) through the pipeline with deterministic reasoning stubs, so CI is "
       "repeatable. It reports route agreement and safety-shaped metrics — not a headline "
       "accuracy figure, which 20 cases could not support. The gate tests fail the build on any "
       "regression. Current run:", False)], after=4)
table(
    ["Metric", "Result", "CI gate"],
    [
        ["Evidence extraction — precision / recall", "100% / 100%", "—"],
        ["Mandatory-criterion false-negatives", "0 / 3", "must be 0"],
        ["Policy-citation accuracy", "100%", "≥ 95%"],
        ["Precedent-citation accuracy", "100%", "≥ 80%"],
        ["Hallucinated references", "0", "must be 0"],
        ["Appeal-recommendation agreement (19 cases)", "100%", "≥ 80%"],
        ["Safe-abstention rate", "2 / 2 (100%)", "≥ 80%"],
        ["Unsafe automations", "0", "must be 0"],
    ],
    widths=[3.4, 1.6, 1.7],
)
rich([("Multi-agent vs. one generalist (same 20 cases). ", True),
      ("A deterministic single-prompt baseline scores its cases the naive way and auto-submits when "
       "every keyword hits — no notion of a mandatory criterion, no contradiction check, never "
       "abstains. Result: route agreement — the pipeline 100%, the baseline 55%. Unsafe automations — "
       "the pipeline 0, the baseline 5 (it would have auto-submitted five cases with a contradiction "
       "in the note, an over-limit value, or only partial evidence). The gate test fails if the "
       "pipeline is not measurably safer than the baseline.", False)])

# ================= 4 · BUSINESS VALUE =================
doc.add_heading("4 · The business value", level=1)
p("Impact as a number the payer can check — not a marketing figure.", after=6)
fig(f"{FIG}/fig-recoverable-value.png")
rich([("Estimated Recoverable Value ", True),
      ("is built (RecoveryEstimator + GET /api/recovery + its own dashboard tab): the headline "
       "number, the four inputs, the arithmetic spelled out, and a confidence level set by the sample "
       "size. No invented improvement percentage.", False)])

# ================= 5 · ARCHITECTURE =================
doc.add_heading("5 · Architecture", level=1)
fig(ARCH, w=6.9,
    cap="Care-Approval-IQ-Architecture_DesignV2.svg is the authoritative source.")
table(
    ["#", "Layer", "Components"],
    [
        ["1", [("Intake", True)],
         "POST /api/cases on the API Proxy → starts a workflow run (runId = request id)."],
        ["2", [("Compute · Orchestration", True), (" — Microsoft Agent Framework", False)],
         "CareApprovalWorkflow, a typed executor graph hosted on Zynara.WorkflowHost (Azure Functions + "
         "ConfigureDurableWorkflows). Executors run as Durable Task activities — checkpointed, retried, "
         "replay-safe. The orchestrator owns the Gate; every send pauses at a MAF RequestPort until a "
         "human answers /respond."],
        ["3", [("AI Foundry · Agent Service", True)],
         "needs-auth · evidence-gap · claims-extraction · precedent-strategist · critic — bound as MAF "
         "AIAgents to the existing server-side Foundry agents (bind-to-existing, never re-create)."],
        ["4", [("Data", True)],
         "Submission Adapter (Azure Function · the only outbound path) → Azure Cosmos DB serverless "
         "(cases · submissions · auths · denials · agentCalls, + precedents / policies metadata) → "
         "Azure Blob Storage (policy docs · denial PDFs · precedent narratives) → Foundry File Search "
         "(one vector index)."],
        ["5", [("Experience", True)],
         "Reviewer (human) → Dashboard (Static Web App + linked backend): review queue · approve/reject "
         "(routed to the workflow RequestPort) · recovery & cost, with a UK ⇄ US header control → "
         "API Proxy (read models + relays /run + /respond/{runId})."],
    ],
    widths=[0.3, 1.9, 4.5],
)
rich([("Cross-cutting: ", True),
      ("managed identity everywhere (the reasoning identity holds no payer secret; the submission "
       "identity has no model access); Key Vault for the payer credential and the two unavoidable "
       "strings; App Insights via OpenTelemetry; the Zynara.Eval CI gate. Production hardening — "
       "private networking, an AI-governance enforcement layer, a CI/CD deploy pipeline, HA/DR, PHI "
       "handling — is named in §7, not built.", False)])

# ================= 6 · VERIFIED LIVE =================
doc.add_heading("6 · Verified live", level=1)
callout("Deployed to zynara-spike-rg; verified end to end against real GPT-5.4:", [
    [("• ", False), ("Every route pauses. ", True),
     ("The workflow assembles, persists to Cosmos, and suspends on the review RequestPort. The Gate "
      "route only changes the card — “ready — one click to send” (AutoSubmit) or “needs your "
      "judgement” (HumanReview). The reviewer's /respond resumes it; the authority check runs in the "
      "apply-decision executor; on an authorised approve-send the case is submitted.", False)],
    [("• ", False), ("No CustomStatus error ", True),
     ("with real agents (the coarse-graph design keeps the workflow snapshot under Durable Task's "
      "16 KB cap).", False)],
    [("• ", False), ("App Insights ", True),
     ("shows the full pipeline.run → spoke.* → invoke_agent * → chat gpt-5.4 → submission.send "
      "trace tree from the workflow host.", False)],
    [("• ", False), ("122 tests green ", True),
     ("(unit + workflow + the Zynara.Eval gate) on every commit via zynara-ci.yml.", False)],
])

# ================= 7 · TECHNOLOGY DECISIONS =================
doc.add_heading("7 · Technology decisions", level=1)
table(
    ["#", "Decision", "Why (one line)"],
    [
        [[("TD-1", True)], "Orchestration is a Microsoft Agent Framework workflow graph, not agent-to-agent chaining",
         "The Gate, the pipeline order and the audit trail must be deterministic and inspectable; the "
         "framework gives the typed graph, the HITL port and durable replay as primitives — less "
         "bespoke code than a hand-rolled Durable Functions hub for the same properties."],
        [[("TD-2", True)], "Pipeline steps are MAF executors with a deterministic wrapper, each with a stub twin",
         "Per-step retry isolation; the prose→typed-value boundary is a named unit; zero-inference CI "
         "runs; replay-safe resume."],
        [[("TD-3", True)], "No request queue — the workflow's HTTP /run is the async boundary",
         "Low volume, no burst; /run returns 202 and the workflow runs on Durable Task's own control "
         "queues."],
        [[("TD-4", True)], "Operational store is Azure Cosmos DB serverless, not Azure SQL",
         "A case is a nested aggregate that maps to one JSON document; partition by /requestId; "
         "serverless billing fits spiky demo volume; no migrations."],
        [[("TD-5", True)], "Corpus is Blob Storage + Foundry File Search (one vector index), separate from Cosmos",
         "Retrieval over policy / precedent text is semantic, not a query; File Search owns the index "
         "natively; Cosmos keeps only the structured metadata."],
        [[("TD-6", True)], "Foundry Agent Service (persistent hosted agents) bound as MAF AIAgents",
         "Versioned, portal-visible assets with File Search + tracing wired; AsAIAgent binds to the "
         "existing agent; each stays behind a Zynara.Core interface."],
        [[("TD-7", True)], "C# / .NET 8 · azd + Bicep · one resource group · managed-identity-first · W3C trace context",
         "First-class Agent Framework SDK; static types make the prose→contract boundary a compile-time "
         "guarantee; one azd up provisions and deploys; one trace per request feeds the audit trail and "
         "the cost meter."],
    ],
    widths=[0.4, 2.3, 4.0],
)

# ================= 8 · SCOPE & ROADMAP =================
doc.add_heading("8 · Scope & roadmap", level=1)
rich([("Deliberately not built (production hardening, named so the gap is understood): ", True),
      ("network isolation with a subnet boundary between the reasoning plane and the payer plane; an "
       "AI-governance enforcement layer (per-agent quota, rate limits, spend caps); a CI/CD deploy "
       "pipeline with a staging slot; model/prompt versioning linked to outcomes; live quality "
       "monitoring on de-identified traffic; HA/DR; secret rotation; a full PHI-handling design "
       "(the demo carries none by design).", False)])
rich([("Roadmap: ", True),
      ("real payer connectivity (FHIR Prior Authorization / Provider Access APIs, X12 278 "
       "clearinghouse, portal RPA — CMS-0057-F, Jan 2027); FHIR bundle intake; an "
       "eligibility / active-coverage check; the Early Warnings monitors (expiry-date math, "
       "policy-version diff — interfaces and stubs exist, feature unwired); peer-to-peer prep; "
       "learning from reviewer edits.", False)])

hr()
p("Zynara Health (fictional) · Care Approval IQ · Microsoft Agent Framework · Architect Track "
  "Submission · Deepak Kumar", size=8, color=GREY, align=WD_ALIGN_PARAGRAPH.CENTER)

doc.save(DEST)
print("wrote", DEST, os.path.getsize(DEST))
