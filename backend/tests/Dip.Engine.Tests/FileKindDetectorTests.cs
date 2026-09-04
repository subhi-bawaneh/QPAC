using Dip.Application.Files;
using Dip.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Dip.Engine.Tests;

public class FileKindDetectorTests
{
    [Theory]
    [InlineData("QF01012-NES-C04518-TDP-STL-00-000000-000001.xlsx", FileKind.Tidp)]
    [InlineData("QF01012-NES-C04518-MDP-GEN-00-000000-000001.xlsx", FileKind.Midp)]
    [InlineData("QPAC - Engineering Tracker.xlsx", FileKind.AconexHistory)]
    [InlineData("QPAC - Engineering Baseline.xlsx", FileKind.Baseline)]
    [InlineData("QPAC-PickLists.xlsx", FileKind.Picklists)]
    [InlineData("Some Aconex Export.xlsx", FileKind.AconexHistory)]
    [InlineData("random-notes.docx", FileKind.Unknown)]
    [InlineData("", FileKind.Unknown)]
    public void Detect_MatchesExpectedKind(string fileName, FileKind expected)
    {
        FileKindDetector.Detect(fileName).Should().Be(expected);
    }

    [Fact]
    public void Detect_IsCaseInsensitive()
    {
        FileKindDetector.Detect("qf01012-nes-c04518-tdp-str-00-000000-000001.XLSX")
            .Should().Be(FileKind.Tidp);
    }
}
