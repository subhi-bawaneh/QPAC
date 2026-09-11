using Dip.Infrastructure.Importers;
using FluentAssertions;
using Xunit;

namespace Dip.Infrastructure.Tests;

// Pure normaliser — no DB, no I/O. Verifies PLAN.md § 5.1.3 edge cases:
//   - Whitespace after hyphens stripped
//   - Trailing -PDF (case-insensitive) removed
//   - Shape check: 8 non-empty segments, else "XXX"
public class NormalizeDocNoTests
{
    [Theory]
    // Standard MIDP-shaped: 8 segments, last=7 chars.
    [InlineData("QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004", "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004")]
    // Spaces after hyphens (real Aconex export shape).
    [InlineData("QF01012- NES- C04518- SDW- STR- 00- BLAD05- 2FL0101",
                "QF01012-NES-C04518-SDW-STR-00-BLAD05-2FL0101")]
    // -PDF suffix on Aconex export.
    [InlineData("QF01012- NES- C04518- SDW- STR- 00- BLAD05- 2FL0101-PDF",
                "QF01012-NES-C04518-SDW-STR-00-BLAD05-2FL0101")]
    // Lowercase -pdf still stripped.
    [InlineData("QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004-pdf",
                "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004")]
    // Multiple internal spaces / tabs mixed with hyphens.
    [InlineData("QF01012-\tNES- \tC04518- SDW-STL-00-Z00000-0ZZ0004",
                "QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004")]
    // A 6-char sequence is a real MIDP number: 240 documents in Tracker.xlsx use one
    // and 174 of them carry Aconex history (docs/excel-analysis.md § 6).
    [InlineData("QF01012-NES-C04518-CAL-CIV-00-Z00000-000004",
                "QF01012-NES-C04518-CAL-CIV-00-Z00000-000004")]
    // 8-char sequences occur too (92 documents).
    [InlineData("QF01012-NES-C04518-SDW-STR-00-CENT01-3L668091",
                "QF01012-NES-C04518-SDW-STR-00-CENT01-3L668091")]
    // Foreign-contract numbers are structurally valid; they never match a MIDP
    // document, which is what InMidp records — normalisation does not filter them.
    [InlineData("QF01012-BSB-C02310-REP-GEN-00-Z00000-000001",
                "QF01012-BSB-C02310-REP-GEN-00-Z00000-000001")]
    public void Normalize_ValidShapes_ReturnStrippedValue(string raw, string expected)
    {
        AconexHistoryImporter.NormalizeDocNo(raw).Should().Be(expected);
    }

    [Theory]
    // 7 segments (missing one) → no number.
    [InlineData("QF01012-NES-C04518-SDW-STL-00-Z00000")]
    // Empty / whitespace.
    [InlineData("")]
    [InlineData("   ")]
    // Random text.
    [InlineData("Some Random File.pdf")]
    // Empty segment in the middle.
    [InlineData("QF01012-NES--SDW-STL-00-Z00000-0ZZ0004")]
    // The seven "SDW-0000xx" values in the sample are two segments and stay unresolved:
    // dropping trailing segments can never invent the six that are missing.
    [InlineData("SDW-000008")]
    [InlineData("SDW-000014")]
    public void Normalize_InvalidShapes_HaveNoNumber(string raw)
    {
        AconexHistoryImporter.NormalizeDocNo(raw).Should().BeNull();
    }

    // Cleaning rules 3 and 4: a run of hyphens collapses, and everything past the
    // eighth segment is dropped. These are the ten raw values in the sample that no
    // longer lose their history (docs/excel-analysis.md).
    [Theory]
    [InlineData("QF01012-NES-C04518-SDW-ARC-00-CENT01-2L11125-CAD",
                "QF01012-NES-C04518-SDW-ARC-00-CENT01-2L11125")]
    [InlineData("QF01012-NES-C04518-SDW-ARC-00-CENT01-2M11426-CAD",
                "QF01012-NES-C04518-SDW-ARC-00-CENT01-2M11426")]
    [InlineData("QF01012-NES-C04518-SDW-CIV-11-CENT01-3FL0511-11",
                "QF01012-NES-C04518-SDW-CIV-11-CENT01-3FL0511")]
    [InlineData("QF01012-NES-C04518-SDW-EBW-01-CENT01-2B30002-CAD",
                "QF01012-NES-C04518-SDW-EBW-01-CENT01-2B30002")]
    [InlineData("QF01012-NES-C04518-SDW-ELL-01-CENT01-2B30002--PDF",
                "QF01012-NES-C04518-SDW-ELL-01-CENT01-2B30002")]
    [InlineData("QF01012-NES-C04518-SDW-ELL-11-CENT01-2B20008-PDF.",
                "QF01012-NES-C04518-SDW-ELL-11-CENT01-2B20008")]
    [InlineData("QF01012-NES-C04518-SDW-FAC-00-Z00000-3000054-CAD",
                "QF01012-NES-C04518-SDW-FAC-00-Z00000-3000054")]
    [InlineData("QF01012-NES-C04518-SDW-STR-00-BLAD01-3FL0414-PDF-CAD",
                "QF01012-NES-C04518-SDW-STR-00-BLAD01-3FL0414")]
    [InlineData("QF01012-NES-C04518-SDW-STR-00-CENT01-2FL1300--PDF",
                "QF01012-NES-C04518-SDW-STR-00-CENT01-2FL1300")]
    public void Normalize_DropsEverythingPastTheEighthSegment(string raw, string expected)
    {
        AconexHistoryImporter.NormalizeDocNo(raw).Should().Be(expected);
    }

    // The tenth: a parenthetical inside the eighth segment is NOT substituted away —
    // no rule rewrites the inside of a segment — so the row gets a well-shaped number
    // that simply matches no document. Recorded so the behaviour is deliberate.
    [Fact]
    public void Normalize_DoesNotEditInsideASegment()
    {
        AconexHistoryImporter.NormalizeDocNo("QF01012-NES-C04518-SDW-FAE-00-CENT01-2L20001(00)-PDF.pdf")
            .Should().Be("QF01012-NES-C04518-SDW-FAE-00-CENT01-2L20001(00)");
    }
}
