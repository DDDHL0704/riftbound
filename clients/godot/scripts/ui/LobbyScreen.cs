using System;
using Godot;

namespace Riftbound.GodotClient.Ui;

public partial class LobbyScreen : AppScreen
{
    public event Action? ConnectRequested;
    public event Action? ReconnectRequested;
    public event Action? CreatePublicMatchRequested;
    public event Action? QueueRequested;
    public event Action? CancelQueueRequested;
    public event Action? JoinPublicMatchRequested;
    public event Action? SubmitDeckRequested;
    public event Action? ReadyRequested;

    private Label _connectionStatus = null!;
    private Label _matchmakingStatus = null!;
    private Label _setupGuidance = null!;
    private LineEdit _handleInput = null!;
    private LineEdit _roomInput = null!;
    private OptionButton _publicMatchSelect = null!;
    private OptionButton _deckSelect = null!;
    private Button _connectButton = null!;
    private Button _reconnectButton = null!;
    private Button _queueButton = null!;
    private Button _cancelQueueButton = null!;
    private Button _joinPublicMatchButton = null!;
    private Button _submitDeckButton = null!;
    private Button _readyButton = null!;

    public string HandleText
    {
        get => _handleInput.Text;
        set => _handleInput.Text = value;
    }

    public string RoomText
    {
        get => _roomInput.Text;
        set => _roomInput.Text = value;
    }

    public int SelectedDeckIndex => Math.Max(0, _deckSelect.Selected);
    public int SelectedPublicMatchIndex => Math.Max(0, _publicMatchSelect.Selected);

    public override void _Ready()
    {
        _connectionStatus = GetNode<Label>("%ConnectionStatus");
        _matchmakingStatus = GetNode<Label>("%MatchmakingStatus");
        _setupGuidance = GetNode<Label>("%SetupGuidance");
        _handleInput = GetNode<LineEdit>("%HandleInput");
        _roomInput = GetNode<LineEdit>("%RoomInput");
        _publicMatchSelect = GetNode<OptionButton>("%PublicMatchSelect");
        _deckSelect = GetNode<OptionButton>("%DeckSelect");
        _connectButton = GetNode<Button>("%ConnectButton");
        _reconnectButton = GetNode<Button>("%ReconnectButton");
        _queueButton = GetNode<Button>("%QueueButton");
        _cancelQueueButton = GetNode<Button>("%CancelQueueButton");
        _joinPublicMatchButton = GetNode<Button>("%JoinPublicMatchButton");
        _submitDeckButton = GetNode<Button>("%SubmitDeckButton");
        _readyButton = GetNode<Button>("%ReadyButton");

        _connectButton.Pressed += () => ConnectRequested?.Invoke();
        _reconnectButton.Pressed += () => ReconnectRequested?.Invoke();
        GetNode<Button>("%CreatePublicMatchButton").Pressed += () => CreatePublicMatchRequested?.Invoke();
        _queueButton.Pressed += () => QueueRequested?.Invoke();
        _cancelQueueButton.Pressed += () => CancelQueueRequested?.Invoke();
        _joinPublicMatchButton.Pressed += () => JoinPublicMatchRequested?.Invoke();
        _submitDeckButton.Pressed += () => SubmitDeckRequested?.Invoke();
        _readyButton.Pressed += () => ReadyRequested?.Invoke();

        ApplyTheme();
        SetStatus("Not connected", connected: false, waiting: false);
        SetSetupState(canSubmitDeck: false, canReady: false, "Connect to choose a deck and prepare.");
    }

    public void ApplyTheme()
    {
        MinimalTheme.Apply(this);
        _readyButton.AddThemeStyleboxOverride("normal", MinimalTheme.Panel(MinimalTheme.Selectable));
        _readyButton.AddThemeStyleboxOverride("hover", MinimalTheme.Panel(new Color(MinimalTheme.Selectable, 0.84f)));
        _readyButton.AddThemeStyleboxOverride("focus", MinimalTheme.Outline(OfficialCardVisualState.Selected));
    }

    public void SetStatus(string text, bool connected, bool waiting)
    {
        _connectionStatus.Text = text;
        _connectButton.Disabled = connected;
        _reconnectButton.Disabled = connected;
        _queueButton.Disabled = waiting;
        _cancelQueueButton.Disabled = !waiting;
    }

    public void SetMatchmakingStatus(string text, bool waiting)
    {
        _matchmakingStatus.Text = text;
        _queueButton.Disabled = waiting;
        _cancelQueueButton.Disabled = !waiting;
    }

    public void SetDeckOptions(Godot.Collections.Array<Godot.Collections.Dictionary> decks, int selected)
    {
        _deckSelect.Clear();
        if (decks.Count == 0)
        {
            _deckSelect.AddItem("No preconstructed decks available");
            _deckSelect.SetItemDisabled(0, true);
            return;
        }

        for (var index = 0; index < decks.Count; index++)
        {
            var deck = decks[index];
            var name = ReadText(deck, "name", "Deck");
            var description = ReadText(deck, "description");
            _deckSelect.AddItem(string.IsNullOrWhiteSpace(description) ? name : $"{name} - {description}");
        }

        _deckSelect.Select(Math.Clamp(selected, 0, decks.Count - 1));
    }

    public void SetPublicMatches(Godot.Collections.Array<Godot.Collections.Dictionary> matches)
    {
        _publicMatchSelect.Clear();
        if (matches.Count == 0)
        {
            _publicMatchSelect.AddItem("No public rooms available");
            _publicMatchSelect.SetItemDisabled(0, true);
            _joinPublicMatchButton.Disabled = true;
            return;
        }

        for (var index = 0; index < matches.Count; index++)
        {
            var match = matches[index];
            var room = ReadText(match, "roomId", "Room");
            var host = ReadText(match, "hostPlayerId", "Host");
            var seats = ReadText(match, "seats");
            var status = ReadText(match, "status");
            _publicMatchSelect.AddItem($"{room} - {host} - {seats} {status}".Trim());
        }

        _publicMatchSelect.Select(0);
        _joinPublicMatchButton.Disabled = false;
    }

    public void SetSetupState(bool canSubmitDeck, bool canReady, string guidance)
    {
        _submitDeckButton.Disabled = !canSubmitDeck;
        _readyButton.Disabled = !canReady;
        _setupGuidance.Text = guidance;
    }

    private static string ReadText(Godot.Collections.Dictionary source, string key, string fallback = "")
    {
        return source.TryGetValue(key, out var value) ? value.AsString() : fallback;
    }
}
