using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Orchestrator.Orchestration;

// Durable activities take a single input object. These carry the earlier spoke
// outputs a later spoke needs, so every activity stays a pure (input → output)
// step the orchestrator can retry and replay in isolation.

public sealed record AppealMatchInput(Request Request, EvidenceGapAssessment Assessment);

public sealed record CriticInput(
    Request Request, NeedsAuthResult NeedsAuth, EvidenceGapResult Gap, AppealMatchResult Appeal,
    ContradictionResult Contradiction);

public sealed record DraftInput(
    Request Request, NeedsAuthResult NeedsAuth, EvidenceGapResult Gap, AppealMatchResult Appeal);

public sealed record PersistInput(Request Request, PipelineResult Result);
