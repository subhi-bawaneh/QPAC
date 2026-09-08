using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Dip.Api.IntegrationTests;

// The spreadsheet read model: 34 columns A–AH in PLAN.md § 5.1.1 order, the first
// row's column A holding the composed document number, and paging over the rows.
[Collection(IntegrationTestCollection.Name)]
public class GetFileWorkbookTests
{
    private readonly DipApiFactory _factory;

    public GetFileWorkbookTests(DipApiFactory factory) => _factory = factory;

    [Fact]
    public async Task TidpFile_Exposes34ColumnsInPlanOrder_AndPagesItsRows()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Workbook test");
        var folderId = await TestHelpers.CreateFolderAsync(
            admin, $"Workbook-{Guid.NewGuid():N}", projectId: projectId);
        await TestHelpers.SetTargetAsync(admin, folderId, "Draft");
        var file = await TestHelpers.ImportedAsync(admin, folderId, "TIDP-STL.xlsx");
        var fileId = file.GetProperty("id").GetGuid();

        var book = await TestHelpers.GetJsonAsync(admin, $"/api/folder-files/{fileId}/workbook");

        book.GetProperty("layer").GetString().Should().Be("Draft");
        book.GetProperty("sheets").EnumerateArray().Select(s => s.GetString())
            .Should().Equal("TIDP", "Baseline", "Picklists");

        var sheet = book.GetProperty("sheet");
        sheet.GetProperty("name").GetString().Should().Be("TIDP");

        var columns = sheet.GetProperty("columns").EnumerateArray().ToList();
        columns.Should().HaveCount(34);
        columns[0].GetProperty("letter").GetString().Should().Be("A");
        columns[0].GetProperty("title").GetString().Should().Be("DOCUMENT NUMBER");
        columns[0].GetProperty("editable").GetBoolean().Should().BeFalse(
            "column A is the CONCATENATE formula, recomposed on save");
        columns[21].GetProperty("letter").GetString().Should().Be("V");
        columns[21].GetProperty("title").GetString().Should().Be("CORPORATE DISCIPLINE");
        columns[33].GetProperty("letter").GetString().Should().Be("AH");
        columns[33].GetProperty("title").GetString().Should().Be("02-EXCHANGE DATE");

        var total = sheet.GetProperty("totalRows").GetInt32();
        total.Should().Be(TidpSampleRowCount.Value);

        var firstRow = sheet.GetProperty("rows")[0];
        firstRow.GetProperty("cells").GetArrayLength().Should().Be(34);
        firstRow.GetProperty("rowNumber").GetInt32().Should().Be(1);

        // Column A is the composed number, exactly as the workbook's CONCATENATE is.
        var cells = firstRow.GetProperty("cells").EnumerateArray().Select(c => c.GetString()).ToList();
        var composed = string.Join('-', cells[11], cells[12], cells[13], cells[14], cells[15],
            cells[16], cells[17]) + "-" + cells[18] + cells[19] + cells[20];
        cells[0].Should().Be(composed);

        // Leading zeros are preserved because every cell is a string.
        cells[16].Should().HaveLength(2);
        cells[20].Should().HaveLength(4);

        // The sample's documented first document number is in the sheet.
        var whole = await TestHelpers.GetJsonAsync(
            admin, $"/api/folder-files/{fileId}/workbook?pageSize=2000");
        whole.GetProperty("sheet").GetProperty("rows").EnumerateArray()
            .Select(r => r.GetProperty("cells")[0].GetString())
            .Should().Contain("QF01012-NES-C04518-SDW-STL-00-Z00000-0ZZ0004");

        // The header block carries the nine TIDP labels.
        var header = sheet.GetProperty("headerBlock").EnumerateArray().ToList();
        header.Should().HaveCount(9);
        header.Single(h => h.GetProperty("label").GetString() == "DISCIPLINE")
            .GetProperty("value").GetString().Should().Be("Structural");

        // Paging.
        var page2 = await TestHelpers.GetJsonAsync(
            admin, $"/api/folder-files/{fileId}/workbook?page=2&pageSize=10");
        var rows = page2.GetProperty("sheet").GetProperty("rows");
        rows.GetArrayLength().Should().Be(10);
        rows[0].GetProperty("rowNumber").GetInt32().Should().Be(11);
    }

    [Fact]
    public async Task BaselineAndPicklistTabs_AreReadOnly()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var projectId = await TestHelpers.NewProjectAsync(_factory, "Workbook tabs test");
        var folderId = await TestHelpers.CreateFolderAsync(
            admin, $"WorkbookTabs-{Guid.NewGuid():N}", projectId: projectId);
        var file = await TestHelpers.ImportedAsync(admin, folderId, "Baseline.xlsx");
        var fileId = file.GetProperty("id").GetGuid();

        var baseline = await TestHelpers.GetJsonAsync(
            admin, $"/api/folder-files/{fileId}/workbook?sheet=Baseline");
        var sheet = baseline.GetProperty("sheet");
        sheet.GetProperty("name").GetString().Should().Be("Baseline");
        sheet.GetProperty("totalRows").GetInt32().Should().BeGreaterThan(0);
        sheet.GetProperty("columns").EnumerateArray()
            .Should().OnlyContain(c => !c.GetProperty("editable").GetBoolean());

        var picklists = await TestHelpers.GetJsonAsync(
            admin, $"/api/folder-files/{fileId}/workbook?sheet=Picklists");
        picklists.GetProperty("sheet").GetProperty("name").GetString().Should().Be("Picklists");
    }

    [Fact]
    public async Task UnknownFile_Is404()
    {
        if (!_factory.IsPostgresAvailable) return;

        var admin = await TestHelpers.AuthedAdminAsync(_factory);
        var response = await admin.GetAsync($"/api/folder-files/{Guid.NewGuid()}/workbook");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
