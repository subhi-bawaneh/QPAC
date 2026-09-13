using Dip.Infrastructure.Excel;
using Dip.Infrastructure.Importers;
using FluentAssertions;
using Xunit;

namespace Dip.Infrastructure.Tests;

// The column map against the real workbook, with no database in the way.
//
// Every one of the 34 columns must be found. Two of them were not, for the life of
// the code: the template writes the duration headers as "01-DURATION \n(DAYS)", with
// a line break inside the cell so the label wraps in two lines, and the old exact
// match left the newline in the middle of the string. Both mapped to -1, and
// ReadExchange's `duration > 0` guard turned that into a null duration on every row
// of every file rather than into an error anyone would see.
public class DocumentColumnMapTests
{
    [Fact]
    public void Every_column_of_the_sample_TIDP_is_found()
    {
        var map = MapOf("TIDP-STL.xlsx");

        // Spelled out rather than looped: a column silently mapping to -1 is exactly
        // the failure this test exists to catch, so each one is named.
        using var _ = new FluentAssertions.Execution.AssertionScope();
        map.DocumentNumber.Should().BePositive(nameof(map.DocumentNumber));
        map.Title.Should().BePositive(nameof(map.Title));
        map.ExtractedFromModel.Should().BePositive(nameof(map.ExtractedFromModel));
        map.ScopeArea.Should().BePositive(nameof(map.ScopeArea));
        map.AuthoringSoftware.Should().BePositive(nameof(map.AuthoringSoftware));
        map.ExchangeFormat.Should().BePositive(nameof(map.ExchangeFormat));
        map.Scale.Should().BePositive(nameof(map.Scale));
        map.DeliveryMilestone.Should().BePositive(nameof(map.DeliveryMilestone));
        map.PackageName.Should().BePositive(nameof(map.PackageName));
        map.ActivityId.Should().BePositive(nameof(map.ActivityId));
        map.ClassificationCode.Should().BePositive(nameof(map.ClassificationCode));
        map.F01.Should().BePositive(nameof(map.F01));
        map.F02.Should().BePositive(nameof(map.F02));
        map.F03.Should().BePositive(nameof(map.F03));
        map.F04.Should().BePositive(nameof(map.F04));
        map.F05.Should().BePositive(nameof(map.F05));
        map.F06.Should().BePositive(nameof(map.F06));
        map.F07.Should().BePositive(nameof(map.F07));
        map.F08A.Should().BePositive(nameof(map.F08A));
        map.F08B.Should().BePositive(nameof(map.F08B));
        map.F08C.Should().BePositive(nameof(map.F08C));
        map.CorporateDiscipline.Should().BePositive(nameof(map.CorporateDiscipline));
        map.Ex1Author.Should().BePositive(nameof(map.Ex1Author));
        map.Ex1Geometrical.Should().BePositive(nameof(map.Ex1Geometrical));
        map.Ex1NonGeometrical.Should().BePositive(nameof(map.Ex1NonGeometrical));
        map.Ex1Duration.Should().BePositive(nameof(map.Ex1Duration));
        map.Ex1Predecessor.Should().BePositive(nameof(map.Ex1Predecessor));
        map.Ex1ExchangeDate.Should().BePositive(nameof(map.Ex1ExchangeDate));
        map.Ex2Author.Should().BePositive(nameof(map.Ex2Author));
        map.Ex2Geometrical.Should().BePositive(nameof(map.Ex2Geometrical));
        map.Ex2NonGeometrical.Should().BePositive(nameof(map.Ex2NonGeometrical));
        map.Ex2Duration.Should().BePositive(nameof(map.Ex2Duration));
        map.Ex2Predecessor.Should().BePositive(nameof(map.Ex2Predecessor));
        map.Ex2ExchangeDate.Should().BePositive(nameof(map.Ex2ExchangeDate));
    }

    // The two that the wrapped header used to hide, at the columns the template puts
    // them in (Z and AF). Pinned by position so a future template that moves them is
    // a deliberate change rather than a silent one.
    [Fact]
    public void The_wrapped_duration_headers_map_to_their_columns()
    {
        var map = MapOf("TIDP-STL.xlsx");

        map.Ex1Duration.Should().Be(26, "'01-DURATION \\n(DAYS)' is column Z");
        map.Ex2Duration.Should().Be(32, "'02-DURATION \\n(DAYS)' is column AF");
    }

    private static DocumentRowParser.DocumentColumnMap MapOf(string sample)
    {
        var reader = new ClosedXmlReader();
        using var workbook = reader.Open(SampleFiles.Open(sample));
        workbook.TryGetSheet("TIDP_Sheet", out var sheet).Should().BeTrue();

        var headerRow = DocumentRowParser.FindDocumentTableHeader(sheet!);
        return DocumentRowParser.MapDocumentColumns(sheet!, headerRow);
    }
}
