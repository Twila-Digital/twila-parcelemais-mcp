using Parcelemais.Mcp.Tools;
using Xunit;

namespace Parcelemais.Mcp.Tests;

public class ToolTextFormattingTests
{
    [Fact]
    public void Money_FormatsAsBrazilianCurrency()
    {
        var formatted = ToolTextFormatting.Money(1500.5m);
        Assert.Contains("1.500,50", formatted);
    }
}
