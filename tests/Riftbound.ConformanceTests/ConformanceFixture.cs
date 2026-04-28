using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;

namespace Riftbound.ConformanceTests;

public sealed record ConformanceFixture(
    int SchemaVersion,
    string FixtureId,
    string Description,
    string Source,
    string RoomId,
    IReadOnlyList<string> Players,
    IReadOnlyList<ConformanceCommand> Commands,
    ConformanceExpected Expected)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<ConformanceFixture> LoadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var fixture = await JsonSerializer.DeserializeAsync<ConformanceFixture>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return fixture ?? throw new InvalidOperationException($"Fixture {path} could not be deserialized.");
    }
}

public sealed record ConformanceCommand(
    string PlayerId,
    string ClientIntentId,
    JsonElement Cmd);

public sealed record ConformanceExpected(
    long FinalTick,
    IReadOnlyList<string> EventKinds,
    IReadOnlyDictionary<string, IReadOnlyList<string>> PromptActions);

public sealed record ConformanceRunResult(
    long FinalTick,
    IReadOnlyList<string> EventKinds,
    IReadOnlyDictionary<string, ActionPromptDto> Prompts);

public static class ConformanceFixtureRunner
{
    public static async Task<ConformanceRunResult> RunAsync(
        ConformanceFixture fixture,
        IRuleEngine ruleEngine,
        CancellationToken cancellationToken)
    {
        var session = new MatchSession(fixture.RoomId, ruleEngine);
        foreach (var playerId in fixture.Players)
        {
            session.EnsurePlayer(playerId);
        }

        ResolutionResult? last = null;
        var eventKinds = new List<string>();
        foreach (var command in fixture.Commands)
        {
            var mapped = GameCommandJsonMapper.Map(command.Cmd);
            last = await session.SubmitAsync(
                    command.PlayerId,
                    command.ClientIntentId,
                    mapped,
                    cancellationToken)
                .ConfigureAwait(false);

            eventKinds.AddRange(last.Events.Select(gameEvent => gameEvent.Kind));
        }

        if (last is null)
        {
            throw new InvalidOperationException($"Fixture {fixture.FixtureId} does not contain commands.");
        }

        return new ConformanceRunResult(last.State.Tick, eventKinds, last.Prompts);
    }
}
