using Ncx.Core.Model;

namespace Ncx.Core.Tests.Parsing;

/// <summary>
/// The block rules of the STOCK verb: the eleventh verb of language 5 rule 1, which carries the bare absolute X, Y and
/// Z of one corner of its bounding box and MAX:X, MAX:Y and MAX:Z of the opposite one, and no other axis word (language
/// 4.2, 5 rules 1 and 2; D117, D351).
/// </summary>
public sealed class StockBlockRulesTests
{
    // STOCK=BOX and STOCK=CYLINDER take the six coordinate words of the bounding box (language 4.2, D351).
    [Theory]
    [InlineData("STOCK=BOX X=0 Y=0 Z=-20 MAX:X=100 MAX:Y=100 MAX:Z=0")]
    [InlineData("STOCK=CYLINDER X=-40 Y=-40 Z=-50 MAX:X=40 MAX:Y=40 MAX:Z=0")]
    [InlineData("STOCK=BOX X={$V1} Y=0 Z=0 MAX:X=10 MAX:Y=10 MAX:Z=10")]
    public void Stock_TheSixCoordinateWords_AreAccepted(string text)
    {
        Block block = ParseText.CleanBlock(text);

        Assert.Equal("STOCK", block.Verb?.Key);
    }

    // STOCK is a verb: a second verb beside it is PAR011 (language 5 rule 1, D351).
    [Fact]
    public void Stock_BesideAnotherVerb_IsTwoVerbs()
    {
        var diagnostics = new Diagnostics("test.ncx");
        ParseText.Block("STOCK=BOX RAPID X=0", diagnostics);

        Assert.Equal(Severity.Error, ParseText.Single(diagnostics, DiagnosticCodes.TwoVerbs).Severity);
    }

    // An incremental word, another axis word, a machine axis word or an arc word under STOCK is PAR012 (language 4.2,
    // the D117 paragraph; D351).
    [Theory]
    [InlineData("STOCK=BOX IX=0 Y=0 Z=0 MAX:X=1 MAX:Y=1 MAX:Z=1")]
    [InlineData("STOCK=BOX X=0 Y=0 Z=0 A=0 MAX:X=1 MAX:Y=1 MAX:Z=1")]
    [InlineData("STOCK=BOX X=0 Y=0 Z2=0 MAX:X=1 MAX:Y=1 MAX:Z=1")]
    [InlineData("STOCK=BOX X=0 Y=0 Z=0 CENTER:X=1 MAX:X=1 MAX:Y=1 MAX:Z=1")]
    public void Stock_AnyOtherAxisWord_IsAxisWordWithoutVerb(string text)
    {
        var diagnostics = new Diagnostics("test.ncx");
        ParseText.Block(text, diagnostics);

        Diagnostic diagnostic = ParseText.Single(diagnostics, DiagnosticCodes.AxisWordWithoutVerb);
        Assert.Equal(Severity.Error, diagnostic.Severity);
        Assert.Contains("STOCK carries X, Y, Z and MAX:X, MAX:Y, MAX:Z", diagnostic.Message);
    }

    // MAX:X, MAX:Y and MAX:Z are the opposite corner of STOCK and stand under no other verb (language 4.2, D351).
    [Theory]
    [InlineData("LINE X=1 MAX:X=5")]
    [InlineData("SHIFT X=1 MAX:X=5")]
    [InlineData("MAX:X=5")]
    public void Max_UnderAnotherVerbOrNone_IsAxisWordWithoutVerb(string text)
    {
        var diagnostics = new Diagnostics("test.ncx");
        ParseText.Block(text, diagnostics);

        Diagnostic diagnostic = ParseText.Single(diagnostics, DiagnosticCodes.AxisWordWithoutVerb);
        Assert.Contains("STOCK", diagnostic.Message);
    }

    // MAX takes the address X, Y or Z only (language 4.2, D351).
    [Fact]
    public void Max_WithAnotherAddress_IsRefusedByTheCatalog()
    {
        var diagnostics = new Diagnostics("test.ncx");
        ParseText.Block("STOCK=BOX X=0 Y=0 Z=0 MAX:X=1 MAX:Y=1 MAX:C=1", diagnostics);

        Assert.Equal(Severity.Error,
            ParseText.Single(diagnostics, DiagnosticCodes.WordAddressNotAccepted).Severity);
    }
}
