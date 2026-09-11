using Dip.Application.Documents;
using FluentAssertions;
using Xunit;

namespace Dip.Engine.Tests;

public class DocumentNumberingTests
{
    // SDW carries a four-digit serial in the sample (DocumentTypeSerials).
    private const int SdwWidth = 4;

    // The first row of samples/TIDP-STL.xlsx (docs/excel-analysis.md § 2).
    [Fact]
    public void Compose_ProducesTheSampleWorkbookNumber()
    {
        var number = DocumentNumbering.Compose(
            "QF01012", "NES", "C04518", "SDW", "STL", "00", "Z00000", "0", "ZZ", "0004", SdwWidth);

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
    [InlineData("4", 4, "0004")]
    [InlineData("0004", 4, "0004")]
    [InlineData("1400", 4, "1400")]
    [InlineData("", 4, "0000")]
    [InlineData(null, 4, "0000")]
    // A three-digit type (CAL, ANL, REP) pads to three, not four.
    [InlineData("4", 3, "004")]
    [InlineData("004", 3, "004")]
    public void NormalizeSequence_PadsToTheTypesWidth(string? input, int width, string expected)
    {
        DocumentNumbering.NormalizeSequence(input, width).Should().Be(expected);
    }

    // The defect this fixes: a three-digit serial padded to four is a different
    // document. 12 SDW rows in the sample MIDP carry a three-digit serial and Aconex
    // holds the three-digit form.
    [Theory]
    [InlineData("300182", 4, "300182")]
    [InlineData("12345", 4, "12345")]
    [InlineData("0004", 3, "0004")]
    public void NormalizeSequence_NeverTruncates(string input, int width, string expected)
    {
        DocumentNumbering.NormalizeSequence(input, width).Should().Be(expected);
    }

    [Fact]
    public void Compose_PadsZoneAndSequence_LeavingOtherFieldsVerbatim()
    {
        var number = DocumentNumbering.Compose(
            "QF01012", "NES", "C04518", "SDW", "STR", "0", "BLAD05", "2", "FL", "101", SdwWidth);

        number.Should().Be("QF01012-NES-C04518-SDW-STR-00-BLAD05-2FL0101");
    }

    [Fact]
    public void Compose_TrimsSurroundingWhitespace()
    {
        var number = DocumentNumbering.Compose(
            " QF01012 ", "NES", "C04518", "SDW", "STL", " 00 ", "Z00000", "0", "ZZ", " 0004 ", SdwWidth);

        number.Should().Be("QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004");
    }

    // A sequence longer than the type's width is a data point, not an error to cut.
    [Fact]
    public void Compose_DoesNotTruncateOverlongSequence()
    {
        var number = DocumentNumbering.Compose(
            "QF01012", "NES", "C04518", "SDW", "STL", "00", "Z00000", "0", "ZZ", "12345", SdwWidth);

        number.Should().EndWith("0ZZ12345");
    }

    // ------------------------------------------------------- NormalizeNumber

    [Theory]
    // The one difference between the sheet's form and the form Aconex holds:
    // 19 rows of the sample MIDP carry a one-digit zone in column A.
    [InlineData("QF01012-NES-C04518-SDW-FAC-0-BLAD02-3FL0007",
                "QF01012-NES-C04518-SDW-FAC-00-BLAD02-3FL0007")]
    [InlineData("QF01012-NES-C04518-SDW-FAC-00-BLAD02-3FL0007",
                "QF01012-NES-C04518-SDW-FAC-00-BLAD02-3FL0007")]
    // Whitespace out, upper case in.
    [InlineData(" qf01012- nes-C04518-sdw-stl-00-z00000-0zz0004 ",
                "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004")]
    public void NormalizeNumber_PadsTheZoneAndCleans(string raw, string expected)
    {
        DocumentNumbering.NormalizeNumber(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void NormalizeNumber_IsEmptyForABlankCell(string? raw, string expected)
    {
        DocumentNumbering.NormalizeNumber(raw).Should().Be(expected);
    }

    // A number that is not eight segments is cleaned but never reshaped: whatever the
    // sheet holds is what the register holds.
    [Fact]
    public void NormalizeNumber_LeavesAnUnexpectedShapeAlone()
    {
        DocumentNumbering.NormalizeNumber("SDW-000008").Should().Be("SDW-000008");
    }

    // ------------------------------------------------------- FirstDifference

    [Fact]
    public void FirstDifference_IsNullWhenTheyAgree()
    {
        DocumentNumbering.FirstDifference(
            "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004",
            "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004").Should().BeNull();
    }

    // The 4 rows whose ZONE cell says 05 under a number that says 04.
    [Fact]
    public void FirstDifference_NamesTheZone()
    {
        var difference = DocumentNumbering.FirstDifference(
            "QF01012-NES-C04518-SDW-ARC-04-CENT01-2L11128",
            "QF01012-NES-C04518-SDW-ARC-05-CENT01-2L11128");

        difference.Should().Be(("ZONE", "04", "05"));
    }

    // The 12 SDW rows the sheet kept at three digits.
    [Fact]
    public void FirstDifference_NamesTheSequence()
    {
        var difference = DocumentNumbering.FirstDifference(
            "QF01012-NES-C04518-SDW-FAC-00-CENT01-300182",
            "QF01012-NES-C04518-SDW-FAC-00-CENT01-3000182");

        difference.Should().Be(("SEQUENCE", "300182", "3000182"));
    }

    [Fact]
    public void FirstDifference_ReportsAShapeChangeAsTheWholeNumber()
    {
        var difference = DocumentNumbering.FirstDifference(
            "SDW-000008",
            "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004");

        difference!.Value.Field.Should().Be("NUMBER");
    }
}
