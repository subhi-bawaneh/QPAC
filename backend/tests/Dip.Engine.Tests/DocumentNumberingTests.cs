using Dip.Application.Documents;
using FluentAssertions;
using Xunit;

namespace Dip.Engine.Tests;

public class DocumentNumberingTests
{
    // The first row of samples/TIDP-STL.xlsx (docs/excel-analysis.md § 2).
    [Fact]
    public void Compose_ProducesTheSampleWorkbookNumber()
    {
        var number = DocumentNumbering.Compose(
            "QF01012", "NES", "C04518", "SDW", "STL", "00", "Z00000", "0", "ZZ", "0004");

        number.Should().Be("QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004");
    }

    [Theory]
    [InlineData("0", "00")]
    [InlineData("00", "00")]
    [InlineData("4", "04")]
    [InlineData("", "00")]
    [InlineData(null, "00")]
    public void NormalizeZone_PadsToTwo(string? input, string expected)
    {
        DocumentNumbering.NormalizeZone(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("4", "0004")]
    [InlineData("0004", "0004")]
    [InlineData("1400", "1400")]
    [InlineData("", "0000")]
    [InlineData(null, "0000")]
    public void NormalizeSequence_PadsToFour(string? input, string expected)
    {
        DocumentNumbering.NormalizeSequence(input).Should().Be(expected);
    }

    [Fact]
    public void Compose_PadsZoneAndSequence_LeavingOtherFieldsVerbatim()
    {
        var number = DocumentNumbering.Compose(
            "QF01012", "NES", "C04518", "SDW", "STR", "0", "BLAD05", "2", "FL", "101");

        number.Should().Be("QF01012-NES-C04518-SDW-STR-00-BLAD05-2FL0101");
    }

    [Fact]
    public void Compose_TrimsSurroundingWhitespace()
    {
        var number = DocumentNumbering.Compose(
            " QF01012 ", "NES", "C04518", "SDW", "STL", " 00 ", "Z00000", "0", "ZZ", " 0004 ");

        number.Should().Be("QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004");
    }

    // A sequence longer than four characters is a data error we must not silently
    // truncate — the number keeps whatever the source held.
    [Fact]
    public void Compose_DoesNotTruncateOverlongSequence()
    {
        var number = DocumentNumbering.Compose(
            "QF01012", "NES", "C04518", "SDW", "STL", "00", "Z00000", "0", "ZZ", "12345");

        number.Should().EndWith("0ZZ12345");
    }
}
