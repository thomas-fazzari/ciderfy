using Ciderfy.Tui;
using Xunit;

namespace Ciderfy.Tests.Tui;

public sealed class TuiCommandsTests
{
    [Fact]
    public void Parse_KnownCommandsAndAliases_ReturnsKind()
    {
        (string Input, TuiCommandKind ExpectedKind)[] cases =
        [
            ("/quit", TuiCommandKind.Quit),
            ("/exit", TuiCommandKind.Quit),
            ("/q", TuiCommandKind.Quit),
            ("/help", TuiCommandKind.Help),
            ("/h", TuiCommandKind.Help),
            ("/config", TuiCommandKind.Config),
            ("/cfg", TuiCommandKind.Config),
            ("/reset-auth", TuiCommandKind.AuthReset),
        ];

        foreach (var (input, expectedKind) in cases)
        {
            var command = TuiCommands.Parse(input);

            Assert.Equal(expectedKind, command.Kind);
        }
    }

    [Fact]
    public void Parse_AliasWithArgument_ReturnsArgument()
    {
        var command = TuiCommands.Parse("/sf fr");

        Assert.Equal(TuiCommandKind.Storefront, command.Kind);
        Assert.Equal("/sf", command.Name);
        Assert.Equal("fr", command.Argument);
    }

    [Fact]
    public void Parse_ResetAuth_ReturnsAuthReset()
    {
        var command = TuiCommands.Parse("/reset-auth");

        Assert.Equal(TuiCommandKind.AuthReset, command.Kind);
        Assert.Equal("/reset-auth", command.Name);
        Assert.Null(command.Argument);
    }

    [Fact]
    public void Parse_Unknown_ReturnsCommandName()
    {
        var command = TuiCommands.Parse("/wat value");

        Assert.Equal(TuiCommandKind.Unknown, command.Kind);
        Assert.Equal("/wat", command.Name);
        Assert.Null(command.Argument);
    }

    [Fact]
    public void GetSuggestions_FiltersFromCommandTable()
    {
        var suggestion = Assert.Single(
            TuiCommands.GetSuggestions("/reset", awaitingUserToken: false)
        );

        Assert.Equal("/reset-auth", suggestion.Completion);
        Assert.Equal("/reset-auth", suggestion.Usage);
    }

    [Fact]
    public void GetSuggestions_Auth_DoesNotIncludeResetAuth()
    {
        var suggestion = Assert.Single(
            TuiCommands.GetSuggestions("/auth", awaitingUserToken: false)
        );

        Assert.Equal("/auth", suggestion.Completion);
    }

    [Fact]
    public void GetSuggestions_CommandWithArgument_AppendsArgumentSeparator()
    {
        var suggestion = Assert.Single(
            TuiCommands.GetSuggestions("/store", awaitingUserToken: false)
        );

        Assert.Equal("/storefront ", suggestion.Completion);
        Assert.EndsWith(TuiCommands.ArgumentSeparator.ToString(), suggestion.Completion);
    }

    [Fact]
    public void GetSuggestions_WhenAwaitingUserToken_ReturnsEmpty()
    {
        var suggestions = TuiCommands.GetSuggestions("/auth", awaitingUserToken: true);

        Assert.Empty(suggestions);
    }

    [Fact]
    public void HelpEntries_AreBuiltFromCommandTable()
    {
        Assert.Contains(
            TuiCommands.HelpEntries,
            entry =>
                entry
                    is {
                        Usage: "/reset-auth",
                        Description: "Clear cached tokens and re-authenticate"
                    }
        );
        Assert.Contains(TuiCommands.HelpEntries, entry => entry.Usage == "<spotify-url>");
    }
}
