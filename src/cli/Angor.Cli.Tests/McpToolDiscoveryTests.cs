using System.Reflection;
using FluentAssertions;
using ModelContextProtocol.Server;

namespace Angor.Cli.Tests;

public class McpToolDiscoveryTests
{
    [Fact]
    public void AllMcpToolTypes_AreDiscoverable()
    {
        var assembly = typeof(Angor.Cli.McpTools.WalletTools).Assembly;

        var toolTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
            .ToList();

        toolTypes.Should().Contain(t => t.Name == "WalletTools");
        toolTypes.Should().Contain(t => t.Name == "ProjectTools");
        toolTypes.Should().Contain(t => t.Name == "FounderTools");
        toolTypes.Should().Contain(t => t.Name == "InvestorTools");
        toolTypes.Should().Contain(t => t.Name == "LightningTools");
        toolTypes.Should().Contain(t => t.Name == "ConfigTools");
    }

    [Theory]
    [InlineData(typeof(Angor.Cli.McpTools.WalletTools), 12)]
    [InlineData(typeof(Angor.Cli.McpTools.ProjectTools), 7)]
    [InlineData(typeof(Angor.Cli.McpTools.FounderTools), 11)]
    [InlineData(typeof(Angor.Cli.McpTools.InvestorTools), 13)]
    [InlineData(typeof(Angor.Cli.McpTools.LightningTools), 2)]
    [InlineData(typeof(Angor.Cli.McpTools.ConfigTools), 3)]
    public void McpToolType_HasExpectedToolCount(Type toolType, int expectedCount)
    {
        var toolMethods = toolType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null)
            .ToList();

        toolMethods.Should().HaveCount(expectedCount,
            $"{toolType.Name} should have {expectedCount} MCP tools");
    }

    [Fact]
    public void TotalMcpTools_MatchesExpected()
    {
        var assembly = typeof(Angor.Cli.McpTools.WalletTools).Assembly;

        var totalTools = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null))
            .Count();

        // 12 + 7 + 11 + 13 + 2 + 3 = 48
        totalTools.Should().Be(48, "total MCP tools across all tool types");
    }
}
