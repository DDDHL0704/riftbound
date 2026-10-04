using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Riftbound.Contracts;

namespace Riftbound.GodotClient;

public sealed class RiftboundGameHubClient : IAsyncDisposable
{
    private readonly HubConnection connection;
    private volatile bool restoringSession;
    private TaskCompletionSource<bool>? restorationSnapshot;
    private volatile bool restorationJoined;

    public RiftboundGameHubClient(string serverUrl)
    {
        ServerUrl = serverUrl.TrimEnd('/');
        connection = BuildConnection(ServerUrl);
        RegisterServerHandlers(connection);
    }

    public string ServerUrl { get; }

    public bool IsConnected => connection.State == HubConnectionState.Connected && !restoringSession;

    public event Func<Task>? RestoreSession;

    public event Action<string>? StatusChanged;

    public event Action<string>? LogReceived;

    public event Action<string, WsServerMessage>? ServerMessageReceived;

    public async Task<bool> StartAsync(CancellationToken cancellationToken)
    {
        if (connection.State != HubConnectionState.Disconnected)
        {
            return false;
        }

        await connection.StartAsync(cancellationToken);
        return true;
    }

    public Task<AuthResultDto> AuthenticateAsync(
        string handle,
        string playerKey,
        CancellationToken cancellationToken)
    {
        return connection.InvokeAsync<AuthResultDto>(
            "Authenticate",
            handle,
            playerKey,
            cancellationToken);
    }

    public Task JoinRoomAsync(
        string roomId,
        string playerId,
        string? reconnectToken,
        CancellationToken cancellationToken)
    {
        return connection.InvokeAsync(
            "JoinRoom",
            roomId,
            playerId,
            reconnectToken,
            cancellationToken);
    }

    public Task ReconnectAsync(
        string roomId,
        string playerId,
        string reconnectToken,
        CancellationToken cancellationToken)
    {
        return connection.InvokeAsync(
            "Reconnect",
            roomId,
            playerId,
            reconnectToken,
            cancellationToken);
    }

    public Task RequestSnapshotAsync(
        string roomId,
        string playerId,
        CancellationToken cancellationToken)
    {
        return connection.InvokeAsync(
            "RequestSnapshot",
            roomId,
            playerId,
            cancellationToken);
    }

    public Task<CreatePublicMatchResultDto?> CreatePublicMatchAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        return connection.InvokeAsync<CreatePublicMatchResultDto?>(
            "CreatePublicMatch",
            playerId,
            cancellationToken);
    }

    public Task<MatchmakingStatusDto> EnqueueMatchmakingAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        return connection.InvokeAsync<MatchmakingStatusDto>(
            "EnqueueMatchmaking",
            playerId,
            cancellationToken);
    }

    public Task<MatchmakingStatusDto> CancelMatchmakingAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        return connection.InvokeAsync<MatchmakingStatusDto>(
            "CancelMatchmaking",
            playerId,
            cancellationToken);
    }

    public Task<CommandReceiptDto> ReadyAsync(
        string roomId,
        string playerId,
        string clientIntentId,
        CancellationToken cancellationToken)
    {
        return connection.InvokeAsync<CommandReceiptDto>(
            "Ready",
            roomId,
            playerId,
            clientIntentId,
            cancellationToken);
    }

    public Task<CommandReceiptDto> SubmitIntentAsync(
        string roomId,
        string playerId,
        string clientIntentId,
        object command,
        CancellationToken cancellationToken)
    {
        return connection.InvokeAsync<CommandReceiptDto>(
            "SubmitIntent",
            roomId,
            playerId,
            clientIntentId,
            command,
            cancellationToken);
    }

    public Task<PlayCostQuoteDto> PreviewPlayCardAsync(string roomId, PlayCostPreviewRequestDto request, CancellationToken cancellationToken)
        => connection.InvokeAsync<PlayCostQuoteDto>("PreviewPlayCard", roomId, request, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        restorationSnapshot?.TrySetCanceled();
        await connection.DisposeAsync();
    }

    private static HubConnection BuildConnection(string serverUrl)
    {
        return new HubConnectionBuilder()
            .WithUrl($"{serverUrl}/hubs/game")
            .WithAutomaticReconnect()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .Build();
    }

    private void RegisterServerHandlers(HubConnection hubConnection)
    {
        hubConnection.Reconnecting += error =>
        {
            restoringSession = true;
            StatusChanged?.Invoke("Reconnecting");
            LogReceived?.Invoke($"Reconnecting: {error?.Message ?? "unknown reason"}.");
            return Task.CompletedTask;
        };
        hubConnection.Reconnected += async connectionId =>
        {
            restorationJoined = false;
            restorationSnapshot = new(TaskCreationOptions.RunContinuationsAsynchronously);
            StatusChanged?.Invoke("Restoring");
            try
            {
                if (RestoreSession is not null) await RestoreSession();
                await restorationSnapshot.Task.WaitAsync(TimeSpan.FromSeconds(15));
                restoringSession = false;
                StatusChanged?.Invoke("Connected");
                LogReceived?.Invoke("Connection and session restored.");
            }
            catch (Exception error)
            {
                StatusChanged?.Invoke("Recovery failed");
                LogReceived?.Invoke($"Session recovery failed: {error.Message}");
            }
            finally { restorationSnapshot = null; }
        };
        hubConnection.Closed += error =>
        {
            restoringSession = false;
            StatusChanged?.Invoke("Disconnected");
            LogReceived?.Invoke($"Closed: {error?.Message ?? "normal close"}.");
            return Task.CompletedTask;
        };

        hubConnection.On<WsServerMessage>("Joined", message =>
        {
            restorationJoined = true;
            ServerMessageReceived?.Invoke("Joined", message);
        });
        hubConnection.On<WsServerMessage>("Snapshot", message =>
        {
            ServerMessageReceived?.Invoke("Snapshot", message);
            if (restorationJoined) restorationSnapshot?.TrySetResult(true);
        });
        hubConnection.On<WsServerMessage>("Prompt", message => ServerMessageReceived?.Invoke("Prompt", message));
        hubConnection.On<WsServerMessage>("Events", message => ServerMessageReceived?.Invoke("Events", message));
        hubConnection.On<WsServerMessage>("Error", message =>
        {
            restorationSnapshot?.TrySetException(new InvalidOperationException("服务器未能恢复对局，请重新连接或返回大厅。"));
            ServerMessageReceived?.Invoke("Error", message);
        });
        hubConnection.On<WsServerMessage>("Matchmaking", message => ServerMessageReceived?.Invoke("Matchmaking", message));
    }
}
