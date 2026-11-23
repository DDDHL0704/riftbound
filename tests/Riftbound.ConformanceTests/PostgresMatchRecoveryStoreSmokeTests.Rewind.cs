using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;
using Npgsql;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class PostgresMatchRecoveryStoreSmokeTests
{
    [Fact]
    public async Task PostgresRewindPreservesOriginalCommandsAndRestoresTheSameStateAndTargets()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Riftbound");
        if (string.IsNullOrWhiteSpace(connection)) return;

        await using var source = NpgsqlDataSource.Create(connection);
        await ApplySchemaAsync(source);
        var roomId = $"rewind-smoke-{Guid.NewGuid():N}";
        try
        {
            var engine = new PlaceholderRuleEngine();
            var journal = new PostgresMatchJournal(source);
            var playerStore = new PostgresMatchPlayerStore(source);
            var recoveryStore = new PostgresMatchRecoveryStore(source);
            var registry = new InMemoryMatchSessionRegistry(engine, journal, recoveryStore, playerStore);
            var live = await registry.GetOrCreatePlayerRoomAsync(roomId, default);
            var alice = await live.EnsurePlayerAsync("alice", default);
            await live.EnsurePlayerAsync("bob", default);
            Assert.True((await live.ReadyAsync("alice", "ready", null, default)).Accepted);
            var opening = await live.ReadyAsync("bob", "ready", null, default);
            Assert.True(opening.Accepted, opening.ErrorMessage);
            var pass = await live.SubmitAsync("alice", "pass", new PassCommand(), null, default);
            Assert.True(pass.Accepted, pass.ErrorMessage);
            var target = live.SnapshotFor("alice").RewindTargets!.Last().Step;
            var started = await live.SubmitAsync("bob", "request", new RequestRewindCommand(target), null, default);
            Assert.True(started.Accepted, started.ErrorMessage);
            var request = started.Snapshots["bob"].RewindRequest!;
            var original = await recoveryStore.LoadAsync(roomId, default);
            Assert.True(original!.IsConsistent, string.Join("; ", original.ValidationErrors));

            var cold = await new InMemoryMatchSessionRegistry(engine, journal, recoveryStore, playerStore)
                .GetOrCreateAsync(roomId, default);
            Assert.Equal(request, cold.SnapshotFor("alice").RewindRequest);
            Assert.Equal("alice", (await cold.ReconnectPlayerAsync("alice", alice.ReconnectToken, default)).PlayerId);
            var rewound = await cold.SubmitAsync("alice", "approve", new AcceptRewindCommand(request.RequestStep), null, default);
            Assert.True(rewound.Accepted, rewound.ErrorMessage);
            Assert.Equal(MatchStateHasher.Hash(opening.State), MatchStateHasher.Hash(rewound.State));

            var recovered = (await recoveryStore.LoadAsync(roomId, default))!;
            Assert.True(recovered.IsConsistent, string.Join("; ", recovered.ValidationErrors));
            Assert.Equal(original.Commands.Select(MatchStateHasher.HashValue),
                recovered.Commands.Take(original.Commands.Count).Select(MatchStateHasher.HashValue));
            Assert.Equal(original.Events.Select(MatchStateHasher.HashValue),
                recovered.Events.Take(original.Events.Count).Select(MatchStateHasher.HashValue));
            Assert.Equal(rewound.ServerTick, recovered.CurrentTick);
            Assert.Equal(MatchStateHasher.Hash(opening.State), MatchStateHasher.Hash(recovered.AuthoritativeState!));
            Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(recovered, engine, default));

            var restored = await new InMemoryMatchSessionRegistry(engine, journal, recoveryStore, playerStore)
                .GetOrCreateAsync(roomId, default);
            Assert.Equal(cold.SnapshotFor("alice").RewindTargets, restored.SnapshotFor("alice").RewindTargets);
            Assert.Equal(rewound.Prompts["alice"].PromptId, restored.PromptFor("alice").PromptId);
        }
        finally
        {
            await DeleteRoomAsync(source, roomId);
        }
    }
}
