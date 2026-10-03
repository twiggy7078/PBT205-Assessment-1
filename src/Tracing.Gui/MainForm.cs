using System.ComponentModel;
using System.Globalization;
using Pbt205.Messaging;

namespace Pbt205.Tracing.Gui;

/// <summary>
/// Task 3 final product. Shows the environment with everyone's current position, and looks up
/// who a person has been in contact with.
///
/// The window only reads the "position" topic and uses the "query" and "query-response" topics.
/// The tracker still does all of the work: this window records nothing itself.
/// </summary>
public sealed class MainForm : Form
{
    private readonly TextBox _endpointBox = new() { Text = "100.74.122.113:5672", Width = 160 };
    private readonly TextBox _userBox = new() { Width = 100 };
    private readonly TextBox _passwordBox = new() { Width = 100, UseSystemPasswordChar = true };
    private readonly NumericUpDown _boardSizeBox = new() { Width = 70, Minimum = 2, Maximum = Board.MaxSize, Value = Board.DefaultSize };
    private readonly Button _connectButton = new() { Text = "Connect", Width = 90 };

    private readonly BoardView _board = new() { Dock = DockStyle.Fill };

    private readonly BindingList<PersonRow> _peopleRows = [];
    private readonly BindingList<ContactRow> _contactRows = [];
    private readonly DataGridView _peopleGrid = NewGrid();
    private readonly DataGridView _contactsGrid = NewGrid();

    private readonly ComboBox _queryBox = new() { Width = 130 };
    private readonly Button _queryButton = new() { Text = "Find contacts", Width = 110, Enabled = false };
    private readonly Label _status = new() { Text = "Not connected.", AutoSize = true, Padding = new Padding(4, 6, 4, 6) };

    private MessageBus? _bus;

    /// <summary>The query this window is waiting for an answer to. Answers to anything else are ignored.</summary>
    private Guid _pendingQuery = Guid.Empty;

    public MainForm()
    {
        Text = "PBT205 Contact Tracing (Task 3)";
        MinimumSize = new Size(1000, 620);
        Size = new Size(1120, 700);

        BrokerCredentials? saved = BrokerCredentials.FromEnvironment();
        if (saved is not null)
        {
            _userBox.Text = saved.UserName;
            _passwordBox.Text = saved.Password;
        }

        _peopleGrid.DataSource = _peopleRows;
        _contactsGrid.DataSource = _contactRows;

        _boardSizeBox.ValueChanged += (_, _) => _board.BoardSize = (int)_boardSizeBox.Value;
        _connectButton.Click += async (_, _) => await ToggleConnectionAsync();
        _queryButton.Click += async (_, _) => await SendQueryAsync();

        // Double-clicking someone in the list looks them up, which is quicker than typing.
        _peopleGrid.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex < _peopleRows.Count)
            {
                _queryBox.Text = _peopleRows[e.RowIndex].Person;
                await SendQueryAsync();
            }
        };

        Controls.Add(BuildLayout());
        FormClosing += async (_, _) => await DisconnectAsync();
    }

    private Control BuildLayout()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var connectionBar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        connectionBar.Controls.AddRange([
            NewLabel("Endpoint:"), _endpointBox,
            NewLabel("User:"), _userBox,
            NewLabel("Password:"), _passwordBox,
            NewLabel("Board size:"), _boardSizeBox,
            _connectButton,
        ]);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        body.Controls.Add(NewPanel(_board, "The environment"), 0, 0);
        body.Controls.Add(BuildSidePanel(), 1, 0);

        layout.Controls.Add(connectionBar, 0, 0);
        layout.Controls.Add(body, 0, 1);
        layout.Controls.Add(_status, 0, 2);
        return layout;
    }

    private Control BuildSidePanel()
    {
        var side = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 58));

        var queryRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(2, 6, 2, 6) };
        queryRow.Controls.AddRange([NewLabel("Person:"), _queryBox, _queryButton]);

        side.Controls.Add(NewPanel(_peopleGrid, "People seen (double-click to look up)"), 0, 0);
        side.Controls.Add(queryRow, 0, 1);
        side.Controls.Add(NewPanel(_contactsGrid, "Contacts, newest first"), 0, 2);
        return side;
    }

    private async Task ToggleConnectionAsync()
    {
        if (_bus is not null)
        {
            await DisconnectAsync();
            SetStatus("Disconnected.");
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
            MessageBus bus = await MessageBus.ConnectAsync(
                endpoint, new BrokerCredentials(_userBox.Text.Trim(), _passwordBox.Text), "tracingGui");
            bus.StatusChanged += message => RunOnUiThread(() => SetStatus(message));

            // Positions from every person, and answers to our own queries.
            await bus.SubscribeAsync<PositionMessage>(
                Topics.Position, "#",
                (position, _) =>
                {
                    RunOnUiThread(() => ApplyPosition(position));
                    return Task.CompletedTask;
                },
                ex => RunOnUiThread(() => SetStatus(ex.Message)));

            await bus.SubscribeAsync<ContactQueryResponse>(
                Topics.QueryResponse, "#",
                (response, _) =>
                {
                    RunOnUiThread(() => ApplyQueryResponse(response));
                    return Task.CompletedTask;
                },
                ex => RunOnUiThread(() => SetStatus(ex.Message)));

            _bus = bus;
            _board.BoardSize = (int)_boardSizeBox.Value;
            _connectButton.Text = "Disconnect";
            _queryButton.Enabled = true;
            SetStatus($"Connected to {endpoint}. Waiting for positions. The tracker must be running for lookups to work.");
        }
        catch (Exception ex) when (ex is MessageBusException or OperationCanceledException)
        {
            ShowProblem(ex.Message);
            SetStatus("Not connected.");
        }
        finally
        {
            _connectButton.Enabled = true;
        }
    }

    private async Task DisconnectAsync()
    {
        MessageBus? bus = _bus;
        _bus = null;
        _queryButton.Enabled = false;
        _connectButton.Text = "Connect";
        if (bus is not null)
        {
            await bus.DisposeAsync();
        }
    }

    private async Task SendQueryAsync()
    {
        MessageBus? bus = _bus;
        string person = _queryBox.Text.Trim();
        if (bus is null)
        {
            return;
        }
        if (person.Length == 0)
        {
            ShowProblem("Enter the name of the person to look up.");
            return;
        }

        var query = new ContactQuery(Guid.NewGuid(), person, DateTimeOffset.UtcNow);
        _pendingQuery = query.CorrelationId;
        _contactRows.Clear();

        _queryButton.Enabled = false;
        try
        {
            await bus.PublishAsync(Topics.Query, person, query);
            SetStatus($"Asked the tracker about '{person}'. Waiting for an answer.");
        }
        catch (MessageBusException ex)
        {
            ShowProblem(ex.Message);
        }
        finally
        {
            _queryButton.Enabled = _bus is not null;
        }
    }

    /// <summary>Moves someone on the board. Always called on the UI thread.</summary>
    private void ApplyPosition(PositionMessage position)
    {
        if (string.IsNullOrWhiteSpace(position.Person))
        {
            SetStatus("Ignored a position message with no person on it.");
            return;
        }

        _board.Update(new PersonMarker(position.ClientId, position.Person, position.X, position.Y, position.MovedAt));

        int index = -1;
        for (int i = 0; i < _peopleRows.Count; i++)
        {
            if (_peopleRows[i].ClientId == position.ClientId)
            {
                index = i;
                break;
            }
        }

        var row = new PersonRow(position);
        if (index < 0)
        {
            _peopleRows.Add(row);
            if (!_queryBox.Items.Contains(position.Person))
            {
                _queryBox.Items.Add(position.Person);
            }
        }
        else
        {
            _peopleRows[index] = row;
        }
    }

    /// <summary>Shows the tracker's answer, if it is the one we asked for.</summary>
    private void ApplyQueryResponse(ContactQueryResponse response)
    {
        if (response.CorrelationId != _pendingQuery)
        {
            return; // Somebody else's question.
        }

        _contactRows.Clear();
        foreach (Contact contact in response.Contacts ?? [])
        {
            _contactRows.Add(new ContactRow(contact));
        }

        SetStatus(_contactRows.Count == 0
            ? $"'{response.Person}' has not been in contact with anyone yet."
            : $"'{response.Person}' has been in contact {_contactRows.Count} time(s), newest first.");
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
        MessageBox.Show(this, message, "Contact tracing", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private static Label NewLabel(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(8, 6, 2, 0) };

    /// <summary>A titled panel. The content is added first so the title sits above it.</summary>
    private static Panel NewPanel(Control content, string title)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2) };
        panel.Controls.Add(content);
        panel.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(2, 4, 2, 6),
            Font = new Font(DefaultFont, FontStyle.Bold),
        });
        return panel;
    }

    private static DataGridView NewGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = SystemColors.Window,
    };
}

/// <summary>One row in the people grid.</summary>
public sealed class PersonRow(PositionMessage position)
{
    [Browsable(false)]
    public Guid ClientId { get; } = position.ClientId;

    public string Person { get; } = position.Person;

    public string Position { get; } = string.Create(CultureInfo.InvariantCulture, $"({position.X}, {position.Y})");

    public string LastMoved { get; } = position.MovedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}

/// <summary>One row in the contacts grid.</summary>
public sealed class ContactRow(Contact contact)
{
    public string Person { get; } = contact.Person;

    public string Where { get; } = string.Create(CultureInfo.InvariantCulture, $"({contact.X}, {contact.Y})");

    public string When { get; } = contact.MetAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}
