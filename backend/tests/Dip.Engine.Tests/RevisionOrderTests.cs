using Dip.Application.Engine;
using FluentAssertions;
using Xunit;

namespace Dip.Engine.Tests;

// Revision is a free-text string in the export. Every value in the sample is numeric
// ("00".."07"), but Aconex issues "P00" and "A" too, so "which is later" is one rule
// in one place rather than whatever string comparison happens to do.
public class RevisionOrderTests
{
    [Theory]
    [InlineData("01", "00")]
    [InlineData("10", "09")]      // the case an ordinal comparison gets wrong
    [InlineData("7", "07")]       // equal by value, whatever the padding
    [InlineData("00", "P00")]     // numeric outranks non-numeric
    [InlineData("00", "A")]
    public void NumericRevisionsCompareByValue(string left, string right)
    {
        var sign = Math.Sign(RevisionOrder.Compare(left, right));
        var expected = left.TrimStart('0') == right.TrimStart('0') ? 0 : 1;
        sign.Should().Be(expected);
    }

    [Fact]
    public void EqualValuesCompareEqual()
    {
        RevisionOrder.Compare("03", "03").Should().Be(0);
        RevisionOrder.Compare("3", "03").Should().Be(0);
        RevisionOrder.Compare("P01", "p01").Should().Be(0);
    }

    [Fact]
    public void NonNumericRevisionsCompareOrdinally()
    {
        RevisionOrder.Compare("P01", "P00").Should().BePositive();
        RevisionOrder.Compare("B", "A").Should().BePositive();
        RevisionOrder.Compare("A", "B").Should().BeNegative();
    }

    [Fact]
    public void ComparisonIsAntisymmetric()
    {
        foreach (var (left, right) in new[] { ("01", "00"), ("00", "A"), ("P01", "P00") })
        {
            Math.Sign(RevisionOrder.Compare(left, right))
                .Should().Be(-Math.Sign(RevisionOrder.Compare(right, left)));
        }
    }

    [Theory]
    [InlineData(null, null, 0)]
    [InlineData("00", null, 1)]
    [InlineData(null, "00", -1)]
    public void MissingRevisionsRankBelowNumericOnes(string? left, string? right, int expected)
    {
        Math.Sign(RevisionOrder.Compare(left, right)).Should().Be(expected);
    }
}
