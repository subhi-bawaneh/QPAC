namespace Dip.Api.Features.Folders.GetFileWorkbook;

// Columns A–AH of the TIDP/MIDP sheet, in the exact order of PLAN.md § 5.1.1.
// Column A is the composed document number and is therefore never editable — it is
// recomputed from L..U on save, like the CONCATENATE formula in the workbook.
internal static class WorkbookColumns
{
    public const string Text = "text";
    public const string Date = "date";
    public const string Int = "int";

    public static readonly IReadOnlyList<WorkbookColumn> Document =
    [
        new("A", "DOCUMENT NUMBER", "documentNumber", 320, false, Text),
        new("B", "DOCUMENT TITLE", "title", 320, true, Text),
        new("C", "EXTRACTED FROM MODEL", "extractedFromModel", 160, true, Text),
        new("D", "SCOPE AREA", "scopeArea", 160, true, Text),
        new("E", "AUTHORING SOFTWARE", "authoringSoftware", 160, true, Text),
        new("F", "EXCHANGE FORMAT", "exchangeFormat", 140, true, Text),
        new("G", "SCALE", "scale", 100, true, Text),
        new("H", "DELIVERY MILESTONE", "deliveryMilestone", 140, true, Date),
        new("I", "PACKAGE NAME", "packageName", 160, true, Text),
        new("J", "ACTIVITY ID", "activityId", 180, true, Text),
        new("K", "CLASSIFICATION CODE", "classificationCode", 140, true, Text),
        new("L", "PROJECT", "f01Project", 100, true, Text),
        new("M", "ORIGINATOR", "f02Originator", 100, true, Text),
        new("N", "CONTRACT", "f03Contract", 100, true, Text),
        new("O", "DOCUMENT TYPE", "f04DocType", 110, true, Text),
        new("P", "DISCIPLINE", "f05Discipline", 100, true, Text),
        new("Q", "AREA/ZONE", "f06Zone", 100, true, Text),
        new("R", "VENUE/BUILDING", "f07Building", 120, true, Text),
        new("S", "DRAWING TYPE", "f08ADrawingType", 110, true, Text),
        new("T", "LEVEL", "f08BLevel", 90, true, Text),
        new("U", "SEQUENCE NUMBER", "f08CSequence", 130, true, Text),
        new("V", "CORPORATE DISCIPLINE", "corporateDiscipline", 170, true, Text),
        new("W", "01-AUTHOR", "ex1Author", 160, true, Text),
        new("X", "01-GEOMETRICAL", "ex1Geometrical", 140, true, Text),
        new("Y", "01-NON GEOMETRICAL", "ex1NonGeometrical", 160, true, Text),
        new("Z", "01-DURATION (DAYS)", "ex1DurationDays", 140, true, Int),
        new("AA", "01-PREDECESSOR", "ex1Predecessor", 180, true, Text),
        new("AB", "01-EXCHANGE DATE", "ex1ExchangeDate", 140, true, Date),
        new("AC", "02-AUTHOR", "ex2Author", 160, true, Text),
        new("AD", "02-GEOMETRICAL", "ex2Geometrical", 140, true, Text),
        new("AE", "02-NON GEOMETRICAL", "ex2NonGeometrical", 160, true, Text),
        new("AF", "02-DURATION (DAYS)", "ex2DurationDays", 140, true, Int),
        new("AG", "02-PREDECESSOR", "ex2Predecessor", 180, true, Text),
        new("AH", "02-EXCHANGE DATE", "ex2ExchangeDate", 140, true, Date),
    ];

    public static readonly IReadOnlyList<WorkbookColumn> Baseline =
    [
        new("A", "Package", "package", 320, false, Text),
        new("B", "Activity Code", "activityCode", 200, false, Text),
        new("C", "Activity", "activity", 120, false, Text),
        new("D", "Original Duration", "originalDuration", 140, false, Int),
        new("E", "Start", "start", 120, false, Date),
        new("F", "Finish", "finish", 120, false, Date),
    ];

    public static readonly IReadOnlyList<WorkbookColumn> Picklists =
    [
        new("A", "List", "field", 200, false, Text),
        new("B", "Code", "code", 180, false, Text),
        new("C", "Description", "description", 320, false, Text),
        new("D", "Order", "sortOrder", 90, false, Int),
    ];
}
