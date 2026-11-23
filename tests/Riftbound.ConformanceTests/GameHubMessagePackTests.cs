using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using Riftbound.Api.Hubs;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class GameHubMessagePackTests
{
    [Fact]
    public void MessagePackPromptMatchesTheJsonPayload()
    {
        var (jsonProtocol, messagePack) = Protocols();
        var state = PlayCostPreviewTests.Position(new(8, 4));
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var message = new WsServerMessage(MessageType.PROMPT, state.RoomId, "P1", state.Tick, prompt);
        var invocation = new InvocationMessage("Prompt", [message]);
        var json = ReadInvocation(jsonProtocol, invocation, typeof(JsonElement));
        var binary = ReadInvocation(messagePack, invocation, typeof(JsonElement));
        var expected = Assert.IsType<JsonElement>(Assert.Single(json.Arguments));
        var actual = Assert.IsType<JsonElement>(Assert.Single(binary.Arguments));
        Assert.Equal("PROMPT", actual.GetProperty("type").GetString());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected.GetRawText()), JsonNode.Parse(actual.GetRawText())));
        Assert.True(messagePack.GetMessageBytes(invocation).Length < jsonProtocol.GetMessageBytes(invocation).Length);
    }

    [Fact]
    public void MessagePackBindsRawCommandsAndCamelCasePreviewRequests()
    {
        var (_, protocol) = Protocols();
        using var command = JsonDocument.Parse("""
            {"cmdType":"PLAY_CARD","sourceObjectId":"CARD","cardNo":"SFD·143/221","targetObjectIds":[],
             "optionalCosts":["TAP_RUNE:R1","RECYCLE_RUNE:R1"],"promptId":"prompt","snapshotTick":4294967299}
            """);
        var submit = ReadInvocation(protocol,
            new InvocationMessage("SubmitIntent", ["room", "P1", "intent", command.RootElement]),
            typeof(string), typeof(string), typeof(string), typeof(JsonElement));
        var raw = Assert.IsType<JsonElement>(submit.Arguments[3]);
        Assert.Equal(4_294_967_299L, raw.GetProperty("snapshotTick").GetInt64());
        var mapped = Assert.IsType<PlayCardCommand>(GameCommandJsonMapper.Map(raw));
        Assert.Equal(["TAP_RUNE:R1", "RECYCLE_RUNE:R1"], mapped.OptionalCosts);

        var preview = JsonSerializer.SerializeToElement(new
        {
            requestId = "quote",
            promptId = "prompt",
            snapshotTick = 4_294_967_299L,
            command = command.RootElement
        });
        var bound = ReadInvocation(protocol, new InvocationMessage("PreviewPlayCard", ["room", preview]),
            typeof(string), typeof(PlayCostPreviewRequestDto));
        var request = Assert.IsType<PlayCostPreviewRequestDto>(bound.Arguments[1]);
        Assert.Equal("quote", request.RequestId);
        Assert.Equal(4_294_967_299L, request.SnapshotTick);
        Assert.Equal(mapped.SourceObjectId, request.Command.SourceObjectId);
        Assert.Equal(mapped.OptionalCosts, request.Command.OptionalCosts);
    }

    private static (IHubProtocol Json, IHubProtocol MessagePack) Protocols()
    {
        var json = new JsonHubProtocolOptions();
        json.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        json.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        var messagePack = new MessagePackHubProtocolOptions();
        messagePack.SerializerOptions = GameMessagePackResolver.Configure(messagePack.SerializerOptions, json.PayloadSerializerOptions);
        return (new JsonHubProtocol(Options.Create(json)), new MessagePackHubProtocol(Options.Create(messagePack)));
    }

    private static InvocationMessage ReadInvocation(IHubProtocol protocol, InvocationMessage invocation, params Type[] parameterTypes)
    {
        var bytes = new ReadOnlySequence<byte>(protocol.GetMessageBytes(invocation));
        Assert.True(protocol.TryParseMessage(ref bytes, new Binder(parameterTypes), out var decoded));
        Assert.True(bytes.IsEmpty);
        return Assert.IsType<InvocationMessage>(decoded);
    }

    private sealed class Binder(Type[] parameterTypes) : IInvocationBinder
    {
        public IReadOnlyList<Type> GetParameterTypes(string methodName) => parameterTypes;
        public Type GetReturnType(string invocationId) => throw new NotSupportedException();
        public Type GetStreamItemType(string streamId) => throw new NotSupportedException();
    }
}
