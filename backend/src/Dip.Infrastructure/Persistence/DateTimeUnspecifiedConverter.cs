using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dip.Infrastructure.Persistence;

// EF Core value converter that guarantees every DateTime we write to Postgres
// has DateTimeKind.Unspecified. PLAN.md § 1 requires all timestamps be
// 'timestamp without time zone'; Npgsql 8 refuses DateTime.Kind=Utc on that
// column type unless the legacy switch is on.
//
// The AppContext.SetSwitch approach is fragile — the switch has to be set
// before Npgsql's type mapper caches its converters, and once cached it never
// re-reads it. This converter side-steps the whole problem: whatever Kind the
// app hands us, the value that reaches Npgsql has Kind=Unspecified, which
// works whether the switch is on or off.
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
