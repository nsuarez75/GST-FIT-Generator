using GstFitGenerator.Core;

namespace GstFitGenerator.Tests;

public class ScreenTests
{
    [Fact]
    public void Name_NumberedPrefix_IncreasesPerScreen()
    {
        Assert.Equal("CO_0000_100_Traceability_01", Screen.Name("CO_0000_100", 1));
        Assert.Equal("CO_0000_106_Traceability_07", Screen.Name("CO_0000_100", 7));
        Assert.Equal("CO_0000_123_Traceability_24", Screen.Name("CO_0000_100", 24));
        Assert.Equal(106, Screen.HmiNumber("CO_0000_100", 7));
    }

    [Fact]
    public void Name_LeadingZeros_KeepsWidth()
    {
        Assert.Equal("CO_0000_010_Traceability_02", Screen.Name("CO_0000_009", 2));
    }

    [Fact]
    public void Name_PrefixWithoutNumber_RepeatsPrefix()
    {
        Assert.Equal("PANTALLA_Traceability_03", Screen.Name("PANTALLA", 3));
        Assert.Null(Screen.HmiNumber("PANTALLA", 3));
    }
}
