using Dip.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Dip.Domain.Tests;

public class EnumsTests
{
    [Fact]
    public void TidpFileStatus_CoversTheUploadLifecycle()
    {
        Enum.GetNames<TidpFileStatus>().Should().Contain(new[] { "Importing", "Imported", "Failed" });
    }

    [Fact]
    public void UnifiedStatus_HasFourValues()
    {
        Enum.GetValues<UnifiedStatus>().Length.Should().Be(4);
    }

    // The MIDP workbook is no longer a source: the TIDPs cover it (38 of its
    // documents appear in no TIDP, which the S2 gate reports).
    [Fact]
    public void ImportKind_CoversAllSources()
    {
        Enum.GetNames<ImportKind>().Should().Contain(new[] { "Tidp", "AconexHistory", "Baseline", "Picklists", "Lists" });
    }
}
