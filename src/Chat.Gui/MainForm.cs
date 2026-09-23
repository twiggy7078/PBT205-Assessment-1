using Pbt205.Messaging;

namespace Pbt205.Chat.Gui;

/// <summary>
/// Task 1 final product: a chat window in which a user logs in, chooses a room and chats.
///
/// All of the chat behaviour comes from <see cref="ChatRoomClient"/>, the same class the
/// command-line application uses, so the two cannot drift apart. This window only handles
/// the interface: collecting a login, showing messages, and sending what the user types.
/// </summary>
public sealed class MainForm : Form
{
    private const string TranscriptSeparator = "------------------------------";

    private readonly TextBox _endpointBox = new() { Text = "100.74.122.113:5672", Width = 170 };
    private readonly TextBox _userBox = new() { Width = 100 };
    private readonly TextBox _passwordBox = new() { Width = 100, UseSystemPasswordChar = true };
    private readonly TextBox _displayNameBox = new() { Width = 110 };
    private readonly ComboBox _roomBox = new() { Width = 120, Text = ChatNames.DefaultRoom };
    private readonly Button _connectButton = new() { Text = "Join", Width = 80 };

    private readonly RichTextBox _transcript = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        BackColor = SystemColors.Window,
        BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Consolas", 10f),
        WordWrap = true,
        DetectUrls = false,
    };

    private readonly TextBox _messageBox = new() { Dock = DockStyle.Fill, Enabled = false };
    private readonly Button _sendButton = new() { Text = "Send", Width = 80, Enabled = false };
    private readonly Label _status = new() { Text = "Not connected.", AutoSize = true, Padding = new Padding(4, 6, 4, 6) };

    private MessageBus? _bus;
    private ChatRoomClient? _chat;

    public MainForm()
    {
        Text = "PBT205 Chat (Task 1)";
        MinimumSize = new Size(760, 480);
        Size = new Size(880, 600);

        _roomBox.Items.AddRange([ChatNames.DefaultRoom, "study", "trading"]);

        // The RabbitMQ login is pre-filled from the environment variables, the same ones the
        // console application uses. The display name defaults to the RabbitMQ username.
        BrokerCredentials? saved = BrokerCredentials.FromEnvironment();
        if (saved is not null)
        {
            _userBox.Text = saved.UserName;
            _passwordBox.Text = saved.Password;
            _displayNameBox.Text = saved.UserName;
        }

        _connectButton.Click += async (_, _) => await ToggleConnectionAsync();
        _sendButton.Click += async (_, _) => await SendAsync();
        _messageBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                // Stop the beep Windows makes when Enter reaches a single-line text box.
                e.SuppressKeyPress = true;
                await SendAsync();
            }
        };

        Controls.Add(BuildLayout());
        FormClosing += async (_, _) => await DisconnectAsync();
    }

    private Control BuildLayout()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var login = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        login.Controls.AddRange([
            NewLabel("Endpoint:"), _endpointBox,
            NewLabel("RabbitMQ user:"), _userBox,
            NewLabel("Password:"), _passwordBox,
            NewLabel("Display name:"), _displayNameBox,
            NewLabel("Room:"), _roomBox,
            _connectButton,
        ]);

        // The message box fills the width; the send button keeps its own width.
        var sendRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        sendRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sendRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sendRow.Controls.Add(_messageBox, 0, 0);
        sendRow.Controls.Add(_sendButton, 1, 0);

        layout.Controls.Add(login, 0, 0);
        layout.Controls.Add(_transcript, 0, 1);
        layout.Controls.Add(sendRow, 0, 2);
        layout.Controls.Add(_status, 0, 3);
        return layout;
    }

    private async Task ToggleConnectionAsync()
    {
        if (_chat is not null)
        {
            await DisconnectAsync();
            AppendSystem("Left the room.");
            SetStatus("Not connected.");
            return;
        }

        string displayName = _displayNameBox.Text.Trim();
        string room = _roomBox.Text.Trim();

        string? nameError = ChatNames.ValidateUsername(displayName) ?? ChatNames.ValidateRoom(room);
        if (nameError is not null)
        {
            ShowProblem(nameError);
            return;
        }
        if (!BrokerEndpoint.TryParse(_endpointBox.Text, out BrokerEndpoint? endpoint, out string endpointError))
        {
            ShowProblem(endpointError);
            return;
        }
        if (_userBox.Text.Trim().Length == 0)
        {
            ShowProblem("Enter your RabbitMQ username.");
            return;
        }

        _connectButton.Enabled = false;
        SetStatus($"Connecting to {endpoint}...");
        try
        {
            // Joining opens the connection; leaving closes it again, so a user can join a
            // different room, or connect as someone else, without restarting the application.
            MessageBus bus = await MessageBus.ConnectAsync(
                endpoint, new BrokerCredentials(_userBox.Text.Trim(), _passwordBox.Text), $"chatGui: {displayName}");
            bus.StatusChanged += message => RunOnUiThread(() => AppendSystem(message));
            _bus = bus;

            _chat = await ChatRoomClient.JoinAsync(bus, displayName, room);
            _chat.MessageReceived += message => RunOnUiThread(() => AppendChat(message));
            _chat.ErrorOccurred += ex => RunOnUiThread(() => AppendSystem(ex.Message));

            AppendSystem($"Joined #{_chat.Room} as {_chat.Username}.");
            SetStatus($"Connected to {endpoint} as {_chat.Username} in #{_chat.Room}.");
            SetConnectedState(true);
            _messageBox.Focus();
        }
        catch (Exception ex) when (ex is MessageBusException or ArgumentException)
        {
            ShowProblem(ex.Message);
            SetStatus("Not connected.");
            await DisconnectAsync();
        }
        finally
        {
            _connectButton.Enabled = true;
        }
    }

    private async Task DisconnectAsync()
    {
        ChatRoomClient? chat = _chat;
        MessageBus? bus = _bus;
        _chat = null;
        _bus = null;
        SetConnectedState(false);

        if (chat is not null)
        {
            await chat.DisposeAsync();
        }
        if (bus is not null)
        {
            await bus.DisposeAsync();
        }
    }

    private async Task SendAsync()
    {
        ChatRoomClient? chat = _chat;
        string text = _messageBox.Text.Trim();
        if (chat is null || text.Length == 0)
        {
            return;
        }

        _sendButton.Enabled = false;
        try
        {
            await chat.SendAsync(text);
            _messageBox.Clear();
            // The room never echoes a message back to its sender, so show it here.
            AppendLine($"[{DateTime.Now:HH:mm:ss}] {chat.Username}: {text}", Color.FromArgb(0, 102, 51));
        }
        catch (Exception ex) when (ex is ArgumentException or MessageBusException)
        {
            ShowProblem(ex.Message);
        }
        finally
        {
            _sendButton.Enabled = _chat is not null;
            _messageBox.Focus();
        }
    }

    private void AppendChat(ChatMessage message)
    {
        string time = message.SentAt.ToLocalTime().ToString("HH:mm:ss");
        switch (message.Kind)
        {
            case ChatMessageKind.Message:
                AppendLine($"[{time}] {message.Username}: {message.Text}", Color.FromArgb(0, 51, 153));
                break;
            case ChatMessageKind.Joined:
                AppendLine($"[{time}] * {message.Username} joined #{message.Room}", Color.Gray);
                break;
            case ChatMessageKind.Left:
                AppendLine($"[{time}] * {message.Username} left #{message.Room}", Color.Gray);
                break;
        }
    }

    private void AppendSystem(string text) => AppendLine($"* {text}", Color.Gray);

    /// <summary>Adds a line and scrolls to it. Always called on the UI thread.</summary>
    private void AppendLine(string text, Color colour)
    {
        _transcript.SelectionStart = _transcript.TextLength;
        _transcript.SelectionLength = 0;
        _transcript.SelectionColor = colour;
        _transcript.AppendText(text + Environment.NewLine);
        _transcript.SelectionColor = _transcript.ForeColor;
        _transcript.ScrollToCaret();
    }

    private void SetConnectedState(bool connected)
    {
        _connectButton.Text = connected ? "Leave" : "Join";
        _messageBox.Enabled = connected;
        _sendButton.Enabled = connected;
        _endpointBox.Enabled = !connected;
        _userBox.Enabled = !connected;
        _passwordBox.Enabled = !connected;
        _displayNameBox.Enabled = !connected;
        _roomBox.Enabled = !connected;

        if (connected && !_roomBox.Items.Contains(_roomBox.Text))
        {
            _roomBox.Items.Add(_roomBox.Text);
        }
        if (!connected)
        {
            AppendLine(TranscriptSeparator, Color.LightGray);
        }
    }

    private void RunOnUiThread(Action action)
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }
        if (InvokeRequired)
        {
            BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    private void SetStatus(string message) => _status.Text = message;

    private void ShowProblem(string message) =>
        MessageBox.Show(this, message, "Chat", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private static Label NewLabel(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(8, 6, 2, 0) };
}
