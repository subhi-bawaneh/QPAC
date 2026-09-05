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
    // 7 segments (missing one) → XXX.
    [InlineData("QF01012-NES-C04518-SDW-STL-00-Z00000")]
    // Empty / whitespace.
    [InlineData("")]
    [InlineData("   ")]
    // Random text.
    [InlineData("Some Random File.pdf")]
    // Empty segment in the middle.
    [InlineData("QF01012-NES--SDW-STL-00-Z00000-0ZZ0004")]
    public void Normalize_InvalidShapes_ReturnXXX(string raw)
    {
        AconexHistoryImporter.NormalizeDocNo(raw).Should().Be(AconexHistoryImporter.InvalidDocNoSentinel);
    }
}
