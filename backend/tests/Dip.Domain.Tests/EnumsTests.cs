using Dip.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Dip.Domain.Tests;

public class EnumsTests
{
    [Fact]
    public void DataTarget_HasLiveAndDraft()
    {
        Enum.GetNames<DataTarget>().Should().Contain(new[] { "Live", "Draft" });
    }

    [Fact]
    public void UnifiedStatus_HasFourValues()
    {
        Enum.GetValues<UnifiedStatus>().Length.Should().Be(4);
    }

    [Fact]
    public void ImportKind_CoversAllSources()
    {
        Enum.GetNames<ImportKind>().Should().Contain(new[] { "Tidp", "Midp", "AconexHistory", "Baseline", "Picklists", "Lists" });
    }
}
