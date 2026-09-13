using Dip.Application.Documents;
using Dip.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Dip.Engine.Tests;

// Every shape that actually occurs in src/Dip.Api/02.TIDPs, plus the shapes a real
// folder grows over time — a lock file, a nested copy, a renamed root.
public class TidpPathParserTests
{
    private readonly TidpPathParser _parser = new();

    // ----------------------------------------------------------- owner folders

    [Theory]
    [InlineData("01.NAP", 1, "NAP")]
    [InlineData("02.JINGGONG", 2, "JINGGONG")]
    [InlineData("03.ALUTEC", 3, "ALUTEC")]
    [InlineData("04.AFCO", 4, "AFCO")]
    [InlineData("05.DOKA", 5, "DOKA")]
    [InlineData("07. RAWABI", 7, "RAWABI")]
    [InlineData("09. TKE", 9, "TKE")]
    [InlineData("10. FALLPROTEC", 10, "FALLPROTEC")]
    [InlineData("11. NAP PMO", 11, "NAP PMO")]
    [InlineData("12. SANA AL-JAZERAH", 12, "SANA AL-JAZERAH")]
    public void Subcontractor_folders_keep_their_number_and_their_name(
        string folder, int order, string name)
    {
        var parsed = _parser.ParseOwnerFolder(folder);

        parsed.SortOrder.Should().Be(order);
        parsed.OwnerName.Should().Be(name);
        parsed.OwnerType.Should().Be(TidpOwnerType.Subcontractor);
        parsed.Warnings.Should().BeEmpty();
    }

    // The two unowned folders. The point of the null name is that these files have no
    // subcontractor yet — storing "Subcontractor - Unassigned" as one invents a company.
    [Theory]
    [InlineData("06.Subcontractor - Unassigned", 6, TidpOwnerType.Unassigned)]
    [InlineData("08. Provisional Sum", 8, TidpOwnerType.ProvisionalSum)]
    public void Unowned_folders_are_typed_and_carry_no_owner_name(
        string folder, int order, TidpOwnerType expected)
    {
        var parsed = _parser.ParseOwnerFolder(folder);

        parsed.SortOrder.Should().Be(order);
        parsed.OwnerType.Should().Be(expected);
        parsed.OwnerName.Should().BeNull();
    }

    [Fact]
    public void The_keywords_come_from_the_rules_so_a_new_unowned_folder_needs_no_code()
    {
        var parser = new TidpPathParser(new TidpFolderRules
        {
            ProvisionalSumKeywords = ["Provisional Sum", "PS Package"],
        });

        parser.ParseOwnerFolder("13. PS Package B").OwnerType.Should().Be(TidpOwnerType.ProvisionalSum);
        // The sample's own folder still matches, because the list was extended, not replaced.
        parser.ParseOwnerFolder("08. Provisional Sum").OwnerType.Should().Be(TidpOwnerType.ProvisionalSum);
        // A list that is set replaces the default for that keyword only: the other
        // lists keep theirs, which is why `06.Subcontractor - Unassigned` still works.
        parser.ParseOwnerFolder("06.Subcontractor - Unassigned").OwnerType
            .Should().Be(TidpOwnerType.Unassigned);
    }

    [Fact]
    public void An_owner_folder_without_the_number_is_still_an_owner_and_says_so()
    {
        var parsed = _parser.ParseOwnerFolder("NEW TRADE");

        parsed.SortOrder.Should().BeNull();
        parsed.OwnerName.Should().Be("NEW TRADE");
        parsed.OwnerType.Should().Be(TidpOwnerType.Subcontractor);
        parsed.Warnings.Should().ContainSingle().Which.Should().Contain("no 'NN.' prefix");
    }

    // ------------------------------------------------------ discipline folders

    [Theory]
    [InlineData("AR-Architectural", "AR", "Architectural")]
    [InlineData("EL-Electrical", "EL", "Electrical")]
    [InlineData("FC-Facade", "FC", "Facade")]
    [InlineData("ID-Interior Design", "ID", "Interior Design")]
    [InlineData("IN-Infrastructure", "IN", "Infrastructure")]
    [InlineData("LS-Landscape", "LS", "Landscape")]
    [InlineData("ME-Mechanical", "ME", "Mechanical")]
    [InlineData("ST-Structural", "ST", "Structural")]
    public void Discipline_folders_split_into_a_two_letter_code_and_a_name(
        string folder, string code, string name)
    {
        var parsed = _parser.ParseDisciplineFolder(folder);

        parsed.Should().NotBeNull();
        parsed!.DisciplineCode.Should().Be(code);
        parsed.DisciplineName.Should().Be(name);
    }

    // Only the first dash splits: the name keeps its own punctuation.
    [Fact]
    public void A_discipline_name_may_contain_anything_including_more_dashes()
    {
        _parser.ParseDisciplineFolder("FY-Fire & Life Safety")!.DisciplineName
            .Should().Be("Fire & Life Safety");
        _parser.ParseDisciplineFolder("XX-Mechanical - Phase 2")!.DisciplineName
            .Should().Be("Mechanical - Phase 2");
    }

    // An owner folder must never be mistaken for a discipline folder: `12. SANA
    // AL-JAZERAH` has a dash in it too.
    [Theory]
    [InlineData("12. SANA AL-JAZERAH")]
    [InlineData("06.Subcontractor - Unassigned")]
    [InlineData("ARC-Architectural")]
    [InlineData("Architectural")]
    public void Anything_that_is_not_XX_dash_name_is_not_a_discipline_folder(string folder) =>
        _parser.ParseDisciplineFolder(folder).Should().BeNull();

    // --------------------------------------------------------------- file names

    [Fact]
    public void A_file_name_splits_into_its_eight_fields()
    {
        var parsed = TidpPathParser.ParseFileName(
            "QF01012-NES-C04518-TDP-ARC-00-000000-000001.xlsx", out var error);

        error.Should().BeNull();
        parsed.Should().NotBeNull();
        parsed!.ProjectCode.Should().Be("QF01012");
        parsed.Originator.Should().Be("NES");
        parsed.Contract.Should().Be("C04518");
        parsed.DocType.Should().Be("TDP");
        parsed.DisciplineTag.Should().Be("ARC");
        parsed.Zone.Should().Be("00");
        parsed.Level.Should().Be("000000");
        parsed.Sequence.Should().Be("000001");
    }

    // RAWABI's file is the reason the sequence is a string of no assumed length, and
    // Zone "00" / Level "000000" are the reason none of the three is ever a number.
    [Theory]
    [InlineData("QF01012-NES-C04518-TDP-ARC-00-000000-19000.xlsx", "19000")]
    [InlineData("QF01012-NES-C04518-TDP-STL-00-000000-900001.xlsx", "900001")]
    [InlineData("QF01012-NES-C04518-TDP-FAC-00-000000-460001.xlsx", "460001")]
    [InlineData("QF01012-NES-C04518-TDP-ELE-00-000000-000004.xlsx", "000004")]
    public void The_sequence_keeps_its_own_length_and_its_leading_zeros(
        string fileName, string sequence)
    {
        var parsed = TidpPathParser.ParseFileName(fileName, out _);

        parsed!.Sequence.Should().Be(sequence);
        parsed.Zone.Should().Be("00");
        parsed.Level.Should().Be("000000");
    }

    [Theory]
    [InlineData("QF01012-NES-C04518-TDP-ARC-00-000000.xlsx", "7 dash-separated part(s)")]
    [InlineData("QF01012-NES-C04518-TDP-ARC-00-000000-000001-A.xlsx", "9 dash-separated part(s)")]
    [InlineData("QF01012-NES-C04518-TDP-ARC-00-000000-00A001.xlsx", "not a sequence number")]
    [InlineData("Copy of TIDP.xlsx", "1 dash-separated part(s)")]
    public void A_name_that_does_not_parse_says_why(string fileName, string expected)
    {
        TidpPathParser.ParseFileName(fileName, out var error).Should().BeNull();
        error.Should().Contain(expected);
    }

    [Theory]
    [InlineData("~$QF01012-NES-C04518-TDP-ARC-00-000000-000001.xlsx", "lock file")]
    [InlineData(".DS_Store", "hidden file")]
    [InlineData("notes.txt", "not a TIDP workbook extension")]
    [InlineData("TIDP register", "no file extension")]
    public void Files_that_are_not_workbooks_are_skipped_with_a_reason(
        string fileName, string expected) =>
        _parser.SkipReason(fileName).Should().Contain(expected);

    [Theory]
    [InlineData("QF01012-NES-C04518-TDP-ARC-00-000000-000001.xlsx")]
    [InlineData("QF01012-NES-C04518-TDP-ARC-00-000000-000001.XLSX")]
    [InlineData("QF01012-NES-C04518-TDP-ARC-00-000000-000001.xlsm")]
    public void Workbooks_are_not_skipped(string fileName) =>
        _parser.SkipReason(fileName).Should().BeNull();

    // --------------------------------------------------------------- full paths

    [Fact]
    public void A_file_under_an_owner_and_a_discipline_resolves_to_both()
    {
        var parsed = _parser.ParsePath(
            "01.NAP/FY-Fire & Life Safety/QF01012-NES-C04518-TDP-FLS-00-000000-000001.xlsx");

        parsed.IsImportable.Should().BeTrue();
        parsed.Owner!.OwnerName.Should().Be("NAP");
        parsed.Owner.SortOrder.Should().Be(1);
        parsed.Discipline!.DisciplineCode.Should().Be("FY");
        parsed.Discipline.DisciplineName.Should().Be("Fire & Life Safety");
        parsed.File!.DisciplineTag.Should().Be("FLS");
        parsed.Warnings.Should().BeEmpty();
    }

    // `02.JINGGONG` and the other single-trade folders hold their files directly. The
    // discipline then comes from the file name alone, and no discipline folder exists.
    [Fact]
    public void A_file_directly_under_its_owner_has_no_discipline_folder()
    {
        var parsed = _parser.ParsePath(
            "02.JINGGONG/QF01012-NES-C04518-TDP-STL-00-000000-000001.xlsx");

        parsed.IsImportable.Should().BeTrue();
        parsed.Owner!.OwnerName.Should().Be("JINGGONG");
        parsed.Discipline.Should().BeNull();
        parsed.File!.DisciplineTag.Should().Be("STL");
        parsed.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void An_unassigned_folders_files_carry_no_owner_name()
    {
        var parsed = _parser.ParsePath(
            "06.Subcontractor - Unassigned/QF01012-NES-C04518-TDP-KNL-00-000000-800001.xlsx");

        parsed.Owner!.OwnerType.Should().Be(TidpOwnerType.Unassigned);
        parsed.Owner.OwnerName.Should().BeNull();
        parsed.File!.Sequence.Should().Be("800001");
    }

    [Fact]
    public void A_provisional_sum_folders_files_carry_no_owner_name()
    {
        var parsed = _parser.ParsePath(
            "08. Provisional Sum/QF01012-NES-C04518-TDP-INT-00-000000-000002.xlsx");

        parsed.Owner!.OwnerType.Should().Be(TidpOwnerType.ProvisionalSum);
        parsed.Owner.OwnerName.Should().BeNull();
        parsed.Owner.SortOrder.Should().Be(8);
        parsed.Discipline.Should().BeNull();
    }

    // The reason the key is the path: this one name is in the sample three times.
    [Fact]
    public void The_same_file_name_under_three_owners_is_three_different_paths()
    {
        const string name = "QF01012-NES-C04518-TDP-ARC-00-000000-000001.xlsx";
        var paths = new[]
        {
            $"01.NAP/AR-Architectural/{name}",
            $"08. Provisional Sum/{name}",
            $"11. NAP PMO/AR-Architectural/{name}",
        };

        var parsed = paths.Select(_parser.ParsePath).ToList();

        parsed.Should().OnlyContain(p => p.IsImportable);
        parsed.Select(p => p.RelativePath).Should().OnlyHaveUniqueItems();
        parsed.Select(p => p.FileName).Should().AllBe(name);
        parsed.Select(p => p.Owner!.OwnerName).Should().Equal("NAP", null, "NAP PMO");
    }

    // Deeper than owner/discipline is not the shape, and not a reason to lose a file.
    [Fact]
    public void A_file_nested_deeper_is_kept_under_its_full_path_with_a_warning()
    {
        var parsed = _parser.ParsePath(
            "01.NAP/AR-Architectural/Superseded/QF01012-NES-C04518-TDP-ARC-00-000000-000001.xlsx");

        parsed.IsImportable.Should().BeTrue();
        parsed.RelativePath.Should()
            .Be("01.NAP/AR-Architectural/Superseded/QF01012-NES-C04518-TDP-ARC-00-000000-000001.xlsx");
        parsed.Owner!.OwnerName.Should().Be("NAP");
        parsed.Discipline!.DisciplineCode.Should().Be("AR");
        parsed.Warnings.Should().ContainSingle().Which.Should().Contain("deeper than owner/discipline");
    }

    [Fact]
    public void A_level_two_folder_that_is_not_a_discipline_folder_warns_and_keeps_the_file()
    {
        var parsed = _parser.ParsePath(
            "01.NAP/Archive/QF01012-NES-C04518-TDP-ARC-00-000000-000001.xlsx");

        parsed.IsImportable.Should().BeTrue();
        parsed.Discipline.Should().BeNull();
        parsed.Warnings.Should().ContainSingle().Which.Should().Contain("not a 'XX-Name' discipline folder");
    }

    [Fact]
    public void A_file_at_the_root_has_no_owner_to_attribute_it_to()
    {
        var parsed = _parser.ParsePath("QF01012-NES-C04518-TDP-ARC-00-000000-000001.xlsx");

        parsed.IsImportable.Should().BeFalse();
        parsed.IsSkipped.Should().BeFalse();
        parsed.Error.Should().Contain("outside any owner folder");
    }

    [Fact]
    public void A_lock_file_inside_a_real_folder_is_skipped_not_failed()
    {
        var parsed = _parser.ParsePath(
            "01.NAP/AR-Architectural/~$QF01012-NES-C04518-TDP-ARC-00-000000-000001.xlsx");

        parsed.IsSkipped.Should().BeTrue();
        parsed.IsImportable.Should().BeFalse();
    }

    [Fact]
    public void A_workbook_whose_name_does_not_parse_fails_and_keeps_its_owner()
    {
        var parsed = _parser.ParsePath("01.NAP/AR-Architectural/Copy of TIDP.xlsx");

        parsed.IsSkipped.Should().BeFalse();
        parsed.IsImportable.Should().BeFalse();
        parsed.Owner!.OwnerName.Should().Be("NAP");
        parsed.Error.Should().Contain("expected 8");
    }

    // ------------------------------------------------------- paths and the root

    [Theory]
    [InlineData(@"01.NAP\AR-Architectural\a.xlsx", "01.NAP/AR-Architectural/a.xlsx")]
    [InlineData("/01.NAP//AR-Architectural/a.xlsx", "01.NAP/AR-Architectural/a.xlsx")]
    [InlineData("./01.NAP/AR-Architectural/a.xlsx", "01.NAP/AR-Architectural/a.xlsx")]
    [InlineData("  01.NAP / AR-Architectural /a.xlsx ", "01.NAP/AR-Architectural/a.xlsx")]
    public void Normalize_makes_one_key_out_of_every_way_of_writing_a_path(
        string input, string expected) =>
        TidpPathParser.Normalize(input).Should().Be(expected);

    // The root the operator picked is not part of the key: the same folder uploaded as
    // `02.TIDPs` and as a renamed copy must match the rows it made last time.
    [Fact]
    public void The_root_is_stripped_and_only_when_it_is_actually_the_prefix()
    {
        TidpPathParser.StripRoot("02.TIDPs/01.NAP/a.xlsx", "02.TIDPs").Should().Be("01.NAP/a.xlsx");
        TidpPathParser.StripRoot("02.tidps/01.NAP/a.xlsx", "02.TIDPs").Should().Be("01.NAP/a.xlsx");
        TidpPathParser.StripRoot("01.NAP/a.xlsx", "02.TIDPs").Should().Be("01.NAP/a.xlsx");
        TidpPathParser.StripRoot("02.TIDPsOther/01.NAP/a.xlsx", "02.TIDPs")
            .Should().Be("02.TIDPsOther/01.NAP/a.xlsx");
    }

    [Fact]
    public void The_root_is_detected_only_when_every_path_shares_one_folder()
    {
        TidpPathParser.DetectRoot(["02.TIDPs/01.NAP/a.xlsx", "02.TIDPs/02.JINGGONG/b.xlsx"])
            .Should().Be("02.TIDPs");
        TidpPathParser.DetectRoot(["01.NAP/a.xlsx", "02.JINGGONG/b.xlsx"]).Should().BeNull();
        TidpPathParser.DetectRoot(["a.xlsx"]).Should().BeNull();
        TidpPathParser.DetectRoot([]).Should().BeNull();
    }

    [Theory]
    [InlineData("01.NAP/AR-Architectural/a.xlsx", "01.NAP", "01.NAP/AR-Architectural")]
    [InlineData("02.JINGGONG/a.xlsx", "02.JINGGONG", null)]
    public void The_owner_and_discipline_folder_keys_come_off_the_file_path(
        string path, string owner, string? discipline)
    {
        TidpPathParser.OwnerPath(path).Should().Be(owner);
        TidpPathParser.DisciplinePath(path).Should().Be(discipline);
    }
}
