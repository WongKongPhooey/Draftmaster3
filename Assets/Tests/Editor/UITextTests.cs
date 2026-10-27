using Draftmaster.Controls;
using NUnit.Framework;

// The letter-case rule (Docs/Type-Scale.md): keys and enum names arriving in capitals come out in Title Case,
// acronyms stay capitals, and text already written in mixed case is left as it was.
public class UITextTests
{
    [TestCase("SHORT TRACKS", "Short Tracks")]
    [TestCase("TOP 10S", "Top 10s")]
    [TestCase("TYRE MGMT", "Tyre Mgmt")]
    [TestCase("MINI-MAP", "Mini-Map")]
    [TestCase("OUT OF THE RACE", "Out of the Race")]
    [TestCase("THE ROAD TO DAYTONA", "The Road to Daytona")]
    [TestCase("DNF", "DNF")]
    [TestCase("XP BAR", "XP Bar")]
    [TestCase("N/A", "N/A")]
    public void AllCapsBecomesTitleCase(string input, string expected)
    {
        Assert.AreEqual(expected, UIText.TitleCase(input));
    }

    [TestCase("Racing line")]
    [TestCase("McDowell")]
    [TestCase("#8 · 0/100")]
    [TestCase("")]
    public void MixedCaseAndNoLettersLeftAlone(string input)
    {
        Assert.AreEqual(input, UIText.TitleCase(input));
    }
}
