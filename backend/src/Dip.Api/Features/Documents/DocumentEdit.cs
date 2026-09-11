namespace Dip.Api.Features.Documents;

// One data exchange as the editor sends it. Exchanges are replaced wholesale
// rather than patched: the grid always posts both, and a missing one means the
// exchange was cleared.
public sealed record ExchangeInput(
    int Number,
    string? Stage,
    string? ProgrammeRef,
    string? Author,
    string? Geometrical,
    string? NonGeometrical,
    int? DurationDays,
    string? Predecessor,
    DateTime? ExchangeDate);
