namespace Dip.Infrastructure.Tests;

// Tests create and drop schemas. Neon holds the real project data, so a
// connection string pointing at it is refused rather than used (decision D10).
public static class TestConnectionGuard
{
    public const string Message = "Tests must not run against Neon";

    public static void Assert(string? connectionString)
    {
        if (connectionString is not null
            && connectionString.Contains("neon.tech", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(Message);
        }
    }
}
