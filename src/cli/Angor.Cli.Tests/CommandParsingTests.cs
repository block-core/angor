using System.CommandLine;
using System.Text.Json;
using Angor.Cli.Commands.Wallet;
using Angor.Cli.Commands.Projects;
using Angor.Cli.Commands.Founder;
using Angor.Cli.Commands.Investor;
using Angor.Cli.Commands.Lightning;
using Angor.Cli.Commands.Config;
using FluentAssertions;

namespace Angor.Cli.Tests;

[Collection("Composition")]
public class CommandParsingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IServiceProvider _services;

    public CommandParsingTests(CompositionFixture fixture)
    {
        _services = fixture.ServiceProvider;
    }

    [Theory]
    [InlineData("wallet")]
    [InlineData("project")]
    [InlineData("founder")]
    [InlineData("investor")]
    [InlineData("lightning")]
    [InlineData("config")]
    public void AllCommandGroups_AreRegistered(string groupName)
    {
        var root = new RootCommand("test");
        root.AddCommand(WalletCommands.Build(_services, JsonOptions));
        root.AddCommand(ProjectCommands.Build(_services, JsonOptions));
        root.AddCommand(FounderCommands.Build(_services, JsonOptions));
        root.AddCommand(InvestorCommands.Build(_services, JsonOptions));
        root.AddCommand(LightningCommands.Build(_services, JsonOptions));
        root.AddCommand(ConfigCommands.Build(_services, JsonOptions));

        root.Subcommands.Should().Contain(c => c.Name == groupName);
    }

    [Fact]
    public void WalletCommand_HasExpectedSubcommands()
    {
        var cmd = WalletCommands.Build(_services, JsonOptions);
        var names = cmd.Subcommands.Select(c => c.Name).ToList();

        names.Should().Contain("list");
        names.Should().Contain("create");
        names.Should().Contain("balance");
        names.Should().Contain("send");
        names.Should().Contain("receive");
        names.Should().Contain("transactions");
        names.Should().Contain("delete");
        names.Should().Contain("generate-seed");
    }

    [Fact]
    public void FounderCommand_HasExpectedSubcommands()
    {
        var cmd = FounderCommands.Build(_services, JsonOptions);
        var names = cmd.Subcommands.Select(c => c.Name).ToList();

        names.Should().Contain("create-keys");
        names.Should().Contain("my-projects");
        names.Should().Contain("investments");
        names.Should().Contain("approve");
        names.Should().Contain("claimable");
        names.Should().Contain("release");
        names.Should().Contain("spend-stage");
        names.Should().Contain("submit-tx");
    }

    [Fact]
    public void InvestorCommand_HasExpectedSubcommands()
    {
        var cmd = InvestorCommands.Build(_services, JsonOptions);
        var names = cmd.Subcommands.Select(c => c.Name).ToList();

        names.Should().Contain("build-draft");
        names.Should().Contain("my-investments");
        names.Should().Contain("total-invested");
        names.Should().Contain("penalties");
        names.Should().Contain("recovery-status");
        names.Should().Contain("build-recovery");
        names.Should().Contain("check-signatures");
        names.Should().Contain("submit-tx");
        names.Should().Contain("get-nsec");
    }

    [Fact]
    public void LightningCommand_HasExpectedSubcommands()
    {
        var cmd = LightningCommands.Build(_services, JsonOptions);
        var names = cmd.Subcommands.Select(c => c.Name).ToList();

        names.Should().Contain("create-swap");
        names.Should().Contain("monitor-swap");
    }

    [Fact]
    public void ConfigCommand_HasExpectedSubcommands()
    {
        var cmd = ConfigCommands.Build(_services, JsonOptions);
        var names = cmd.Subcommands.Select(c => c.Name).ToList();

        names.Should().Contain("show");
        names.Should().Contain("get-network");
        names.Should().Contain("set-network");
    }
}
