using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Tracker.GetTrackerDocument;

// One document with its full Aconex revision history — the detail drawer, and the
// screen behind PLAN.md § 5.4.1's "Selected Revision" columns.
[Permission(Permissions.ReportsView)]
public sealed record GetTrackerDocumentQuery(Guid DocumentId) : IQuery<TrackerDocumentDto>;
