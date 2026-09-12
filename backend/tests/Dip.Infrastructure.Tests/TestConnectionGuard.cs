namespace Dip.Infrastructure.Tests;

// Tests create and drop whole databases. The production SQL Server holds the real
// project data, so a connection string pointing at it is refused rather than used
// (decision D10).
public static class TestConnectionGuard
{
    public const string Message = "Tests must not run against the production database";

    private static readonly string[] ProductionHostFragments = ["neon.tech", "databaseasp.net"];

    public static void Assert(string? connectionString)
    {
        if (connectionString is not null
            && ProductionHostFragments.Any(fragment =>
                connectionString.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(Message);
        }
    }
}
