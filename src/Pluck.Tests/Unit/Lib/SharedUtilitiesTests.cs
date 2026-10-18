using System.Text;
using Pluck.Shared.Lib;

namespace Pluck.Tests.Unit.Lib;

public class SharedUtilitiesTests
{
    [Fact]
    public void GenerateId_ReturnsStringOfRequestedLength()
    {
        var result = SharedUtilities.GenerateId(6);

        result.Length.Should().Be(6);
    }

    [Fact]
    public void GenerateId_ReturnsAlphanumericString()
    {
        var result = SharedUtilities.GenerateId(10);

        result.Should().MatchRegex("^[a-zA-Z2-9]+$");
    }

    [Fact]
    public void GenerateId_MultipleCalls_ReturnUniqueValues()
    {
        var ids = new HashSet<string>();
        for (int i = 0; i < 100; i++)
        {
            ids.Add(SharedUtilities.GenerateId(6));
        }

        ids.Count.Should().Be(100);
    }

    [Fact]
    public void GenerateId_ExcludesConfusingCharacters()
    {
        // The charset excludes: lowercase l, uppercase O, digit 0, digit 1
        var allChars = new StringBuilder();
        for (int i = 0; i < 1000; i++)
        {
            allChars.Append(SharedUtilities.GenerateId(10));
        }
        var result = allChars.ToString();

        result.Should().NotContain("l");
        result.Should().NotContain("O");
        result.Should().NotContain("0");
        result.Should().NotContain("1");
    }

    [Fact]
    public void GenerateId_DifferentLengths_ReturnCorrectLengths()
    {
        SharedUtilities.GenerateId(1).Length.Should().Be(1);
        SharedUtilities.GenerateId(10).Length.Should().Be(10);
        SharedUtilities.GenerateId(50).Length.Should().Be(50);
    }
}
