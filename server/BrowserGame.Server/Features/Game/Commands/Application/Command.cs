namespace BrowserGame.Server.Features.Game;

/// <summary>Ein Eintrag der <see cref="CommandRegistry"/> (015): erwartete Zahl Parameter und was er tut.</summary>
internal sealed record Command(int ParameterCount, Action<CommandContext, IReadOnlyList<string>> Run);
