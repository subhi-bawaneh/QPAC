using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dip.Infrastructure.Persistence;

// EF Core value converter that guarantees every DateTime we write to the database has
// DateTimeKind.Unspecified. PLAN.md § 1 requires naive timestamps that match Excel,
// regardless of what Kind the in-memory value happens to carry.
public sealed class DateTimeUnspecifiedConverter : ValueConverter<DateTime, DateTime>
{
    public DateTimeUnspecifiedConverter()
        : base(
            toProvider => DateTime.SpecifyKind(toProvider, DateTimeKind.Unspecified),
            fromProvider => DateTime.SpecifyKind(fromProvider, DateTimeKind.Unspecified))
    {
    }
}

public sealed class NullableDateTimeUnspecifiedConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableDateTimeUnspecifiedConverter()
        : base(
            toProvider => toProvider == null ? null : DateTime.SpecifyKind(toProvider.Value, DateTimeKind.Unspecified),
            fromProvider => fromProvider == null ? null : DateTime.SpecifyKind(fromProvider.Value, DateTimeKind.Unspecified))
    {
    }
}
