using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Riftbound.Contracts;

namespace Riftbound.GodotClient;

public partial class Main : Control
{
    [Export] public string ServerUrl { get; set; } = "http://127.0.0.1:5088";
    [Export] public string RoomId { get; set; } = "godot-local";
    [Export] public string Handle { get; set; } = "godot";
    [Export] public string PlayerKey { get; set; } = "godot-local-development-key";
    [Export] public string OfficialCatalogSnapshotPath { get; set; } = "res://../../data/official/card-catalog.zh-CN.json";
    [Export] public string PreviewCardNo { get; set; } = "UNL-181/219";

    private Label? _status;
    private RichTextLabel? _log;
    private TextureRect? _officialCardPreview;
    private HubConnection? _connection;
    private readonly CancellationTokenSource _shutdown = new();

    public override async void _Ready()
    {
        _status = GetNode<Label>("Status");
        _log = GetNode<RichTextLabel>("Log");
        _officialCardPreview = GetNode<TextureRect>("OfficialCardPreviewFrame/OfficialCardPreview");
        AppendLog("Client booted. Waiting for server authority.");
        _ = LoadOfficialCardPreviewAsync();
        await ConnectAndRequestSnapshotAsync();
    }

    public override void _ExitTree()
    {
        _shutdown.Cancel();
        _ = DisconnectAsync();
        _shutdown.Dispose();
    }

    private async Task LoadOfficialCardPreviewAsync()
    {
        try
        {
            var catalog = await new OfficialCardCatalogService()
                .LoadSnapshotAsync(OfficialCatalogSnapshotPath, _shutdown.Token);
            AppendLog($"Official catalog loaded: {catalog.Count} cards.");

            if (!catalog.TryGetValue(PreviewCardNo, out var card))
            {
                AppendLog($"[color=yellow]Official preview card not found: {Escape(PreviewCardNo)}[/color]");
                return;
            }

            var image = await new OfficialCardImageLoader().LoadOfficialFrontImageAsync(card, _shutdown.Token);
            if (image is null)
            {
                AppendLog($"[color=yellow]No official front image for {Escape(card.CardNo)} {Escape(card.CardName)}[/color]");
                return;
            }

            QueueMainThread(nameof(ApplyOfficialCardPreview), image);
            AppendLog($"Official card image loaded: {Escape(card.CardNo)} {Escape(card.CardName)}.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppendLog($"[color=yellow]Official image preview unavailable: {Escape(ex.Message)}[/color]");
        }
    }

    private async Task ConnectAndRequestSnapshotAsync()
    {
        try
        {
            SetStatus("Connecting");
            _connection = BuildConnection();
            RegisterServerHandlers(_connection);

            await _connection.StartAsync();
            SetStatus("Connected");
            AppendLog($"Connected to {ServerUrl}/hubs/game.");

            var auth = await _connection.InvokeAsync<AuthResultDto>("Authenticate", Handle, PlayerKey);
            AppendLog($"Authenticate: {auth.Status} ({auth.Handle}).");
            if (!auth.Authenticated)
            {
                SetStatus($"Authentication rejected: {auth.Status}");
                return;
            }

            await _connection.InvokeAsync("JoinRoom", RoomId, auth.Handle, null);
            AppendLog($"JoinRoom requested: room={RoomId}, player={auth.Handle}.");

            await _connection.InvokeAsync("RequestSnapshot", RoomId, auth.Handle);
            AppendLog("RequestSnapshot submitted.");
        }
        catch (Exception ex)
        {
            SetStatus("Connection error");
            AppendLog($"[color=red]{Escape(ex.GetType().Name)}: {Escape(ex.Message)}[/color]");
            GD.PushError(ex.ToString());
        }
    }

    private HubConnection BuildConnection()
    {
        return new HubConnectionBuilder()
            .WithUrl($"{ServerUrl.TrimEnd('/')}/hubs/game")
            .WithAutomaticReconnect()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .Build();
    }

    private void RegisterServerHandlers(HubConnection connection)
    {
        connection.Reconnecting += error =>
        {
            SetStatus("Reconnecting");
            AppendLog($"Reconnecting: {error?.Message ?? "unknown reason"}.");
            return Task.CompletedTask;
        };
        connection.Reconnected += connectionId =>
        {
            SetStatus("Connected");
            AppendLog($"Reconnected: {connectionId ?? "no connection id"}.");
            return Task.CompletedTask;
        };
        connection.Closed += error =>
        {
            SetStatus("Disconnected");
            AppendLog($"Closed: {error?.Message ?? "normal close"}.");
            return Task.CompletedTask;
        };

        connection.On<WsServerMessage>("Joined", message => LogMessage("Joined", message));
        connection.On<WsServerMessage>("Snapshot", message => LogMessage("Snapshot", message));
        connection.On<WsServerMessage>("Prompt", message => LogMessage("Prompt", message));
        connection.On<WsServerMessage>("Events", message => LogMessage("Events", message));
        connection.On<WsServerMessage>("Error", message => LogMessage("Error", message));
        connection.On<WsServerMessage>("Matchmaking", message => LogMessage("Matchmaking", message));
    }

    private void LogMessage(string channel, WsServerMessage message)
    {
        AppendLog(
            $"[b]{Escape(channel)}[/b] type={message.Type} room={Escape(message.RoomId)} player={Escape(message.PlayerId)} tick={message.ServerTick} payload={PayloadSummary(message.Payload)}");
    }

    private static string PayloadSummary(object? payload)
    {
        return payload switch
        {
            null => "null",
            JsonElement element => element.ValueKind.ToString(),
            _ => payload.GetType().Name
        };
    }

    private async Task DisconnectAsync()
    {
        if (_connection is null)
        {
            return;
        }

        await _connection.DisposeAsync();
        _connection = null;
    }

    private void SetStatus(string text)
    {
        GD.Print($"[Riftbound] {text}");
        QueueMainThread(nameof(ApplyStatus), text);
    }

    private void AppendLog(string text)
    {
        GD.Print($"[Riftbound] {text}");
        QueueMainThread(nameof(ApplyLog), text);
    }

    public void ApplyStatus(string text)
    {
        if (_status is not null)
        {
            _status.Text = text;
        }
    }

    public void ApplyLog(string text)
    {
        if (_log is null)
        {
            return;
        }

        _log.AppendText($"{text}\n");
    }

    public void ApplyOfficialCardPreview(Image image)
    {
        if (_officialCardPreview is not null)
        {
            _officialCardPreview.Texture = ImageTexture.CreateFromImage(image);
        }
    }

    private void QueueMainThread(StringName method, Variant value)
    {
        if (!IsInsideTree())
        {
            return;
        }

        CallDeferred(method, value);
    }

    private static string Escape(string value)
    {
        return value
            .Replace("[", "[lb]", StringComparison.Ordinal)
            .Replace("]", "[rb]", StringComparison.Ordinal);
    }
}
