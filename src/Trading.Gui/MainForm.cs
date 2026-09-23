using System.ComponentModel;
using System.Globalization;
using Pbt205.Messaging;

namespace Pbt205.Trading.Gui;

/// <summary>
/// Task 2 final product. Shows the latest price each stock traded at, a list of recent trades,
/// and lets the user send an order without leaving the window.
///
/// The window only listens to the "trades" topic and publishes to "orders". All matching still
/// happens in the exchange app, so the exchange must be running for anything to trade.
/// </summary>
public sealed class MainForm : Form
{
    private readonly TextBox _endpointBox = new() { Text = "100.74.122.113:5672", Width = 170 };
    private readonly TextBox _userBox = new() { Width = 110 };
    private readonly TextBox _passwordBox = new() { Width = 110, UseSystemPasswordChar = true };
    private readonly Button _connectButton = new() { Text = "Connect", Width = 90 };

    private readonly BindingList<StockRow> _stocks = [];
    private readonly BindingList<TradeRow> _trades = [];
    private readonly BindingList<BookRow> _book = [];
    private readonly DataGridView _stocksGrid = NewGrid();
    private readonly DataGridView _tradesGrid = NewGrid();
    private readonly DataGridView _bookGrid = NewGrid();

    /// <summary>The newest book snapshot for each stock, keyed by symbol.</summary>
    private readonly Dictionary<string, BookSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    private readonly ComboBox _symbolBox = new() { Width = 90, Text = TradingNames.DefaultSymbol };
    private readonly ComboBox _sideBox = new() { Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _quantityBox = new() { Width = 80, Minimum = 1, Maximum = 1_000_000, Value = TradingNames.FixedQuantity };
    private readonly NumericUpDown _priceBox = new() { Width = 90, Minimum = 0.01m, Maximum = 1_000_000m, DecimalPlaces = 2, Increment = 0.25m, Value = 10.00m };
    private readonly Button _sendButton = new() { Text = "Send order", Width = 100, Enabled = false };

    private readonly Label _status = new() { Text = "Not connected.", AutoSize = true, Padding = new Padding(4, 6, 4, 6) };

    private MessageBus? _bus;

    public MainForm()
    {
        Text = "PBT205 Trading (Task 2)";
        MinimumSize = new Size(900, 520);
        Size = new Size(1000, 600);

        _sideBox.Items.AddRange([nameof(OrderSide.Buy), nameof(OrderSide.Sell)]);
        _sideBox.SelectedIndex = 0;
        _symbolBox.Items.Add(TradingNames.DefaultSymbol);

        // Prefill the login from the environment variables, the same ones the console apps use.
        BrokerCredentials? saved = BrokerCredentials.FromEnvironment();
        if (saved is not null)
        {
            _userBox.Text = saved.UserName;
            _passwordBox.Text = saved.Password;
        }

        _stocksGrid.DataSource = _stocks;
        _tradesGrid.DataSource = _trades;
        _bookGrid.DataSource = _book;

        _connectButton.Click += async (_, _) => await ToggleConnectionAsync();
        _sendButton.Click += async (_, _) => await SendOrderAsync();

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

        var connectionBar = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        connectionBar.Controls.AddRange([
            NewLabel("Endpoint:"), _endpointBox,
            NewLabel("User:"), _userBox,
            NewLabel("Password:"), _passwordBox,
            _connectButton,
        ]);

        // Three side-by-side panels: prices, orders still waiting, and completed trades.
        var grids = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        grids.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        grids.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        grids.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37));
        grids.Controls.Add(NewPanel(_stocksGrid, "Latest price by stock"), 0, 0);
        grids.Controls.Add(NewPanel(_bookGrid, "Waiting orders (order book)"), 1, 0);
        grids.Controls.Add(NewPanel(_tradesGrid, "Recent trades"), 2, 0);

        var ticket = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        ticket.Controls.AddRange([
            NewLabel("Stock:"), _symbolBox,
            NewLabel("Side:"), _sideBox,
            NewLabel("Quantity:"), _quantityBox,
            NewLabel("Price:"), _priceBox,
            _sendButton,
        ]);

        layout.Controls.Add(connectionBar, 0, 0);
        layout.Controls.Add(grids, 0, 1);
        layout.Controls.Add(ticket, 0, 2);
        layout.Controls.Add(_status, 0, 3);
        return layout;
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
                endpoint, new BrokerCredentials(_userBox.Text.Trim(), _passwordBox.Text), "tradingGui");
            bus.StatusChanged += message => RunOnUiThread(() => SetStatus(message));

            // "#" means every stock. Each window gets its own copy of every trade.
            await bus.SubscribeAsync<TradeMessage>(
                Topics.Trades,
                bindingKey: "#",
                (trade, _) =>
                {
                    RunOnUiThread(() => ApplyTrade(trade));
                    return Task.CompletedTask;
                },
                ex => RunOnUiThread(() => SetStatus(ex.Message)));

            // Beyond the brief: the exchange also publishes the state of each book, so this window
            // can show orders that are still waiting rather than only completed trades.
            await bus.SubscribeAsync<BookSnapshot>(
                Topics.Book,
                bindingKey: "#",
                (snapshot, _) =>
                {
                    RunOnUiThread(() => ApplySnapshot(snapshot));
                    return Task.CompletedTask;
                },
                ex => RunOnUiThread(() => SetStatus(ex.Message)));

            _bus = bus;
            _connectButton.Text = "Disconnect";
            _sendButton.Enabled = true;
            SetStatus($"Connected to {endpoint} as {_userBox.Text.Trim()}. Waiting for trades.");
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
        _sendButton.Enabled = false;
        _connectButton.Text = "Connect";
        if (bus is not null)
        {
            await bus.DisposeAsync();
        }
    }

    private async Task SendOrderAsync()
    {
        MessageBus? bus = _bus;
        if (bus is null)
        {
            return;
        }

        string symbol = _symbolBox.Text.Trim();
        string? symbolError = TradingNames.ValidateSymbol(symbol);
        if (symbolError is not null)
        {
            ShowProblem(symbolError);
            return;
        }
        symbol = TradingNames.NormaliseSymbol(symbol);

        var order = new OrderMessage(
            OrderId: Guid.NewGuid(),
            Username: _userBox.Text.Trim(),
            Symbol: symbol,
            Side: _sideBox.SelectedIndex == 1 ? OrderSide.Sell : OrderSide.Buy,
            Quantity: (int)_quantityBox.Value,
            Price: _priceBox.Value,
            PlacedAt: DateTimeOffset.UtcNow);

        _sendButton.Enabled = false;
        try
        {
            await bus.PublishAsync(Topics.Orders, symbol, order);
            SetStatus($"Sent {order.Side.ToString().ToUpperInvariant()} {order.Quantity} {symbol} " +
                      $"@ {order.Price.ToString("0.00", CultureInfo.InvariantCulture)}. " +
                      "It only trades while the exchange app is running.");
            if (!_symbolBox.Items.Contains(symbol))
            {
                _symbolBox.Items.Add(symbol);
            }
        }
        catch (MessageBusException ex)
        {
            ShowProblem(ex.Message);
        }
        finally
        {
            _sendButton.Enabled = _bus is not null;
        }
    }

    /// <summary>Updates the grids. Always called on the UI thread.</summary>
    private void ApplyTrade(TradeMessage trade)
    {
        if (string.IsNullOrWhiteSpace(trade.Symbol) || trade.Price <= 0m || trade.Quantity <= 0)
        {
            SetStatus("Ignored an incomplete trade message.");
            return;
        }

        int index = -1;
        for (int i = 0; i < _stocks.Count; i++)
        {
            if (string.Equals(_stocks[i].Symbol, trade.Symbol, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            var row = new StockRow(trade.Symbol);
            row.Apply(trade);
            _stocks.Add(row);
        }
        else
        {
            _stocks[index].Apply(trade);
            _stocks.ResetItem(index);
        }

        _trades.Insert(0, new TradeRow(trade));
        while (_trades.Count > 200)
        {
            _trades.RemoveAt(_trades.Count - 1);
        }

        SetStatus($"Last trade: {trade.Quantity} {trade.Symbol} @ " +
                  $"{trade.Price.ToString("0.00", CultureInfo.InvariantCulture)} " +
                  $"({trade.Buyer} bought from {trade.Seller}).");
    }

    /// <summary>Replaces the waiting orders for one stock. Always called on the UI thread.</summary>
    private void ApplySnapshot(BookSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Symbol) || snapshot.Bids is null || snapshot.Asks is null)
        {
            SetStatus("Ignored an incomplete order book message.");
            return;
        }

        _snapshots[snapshot.Symbol] = snapshot;

        // Small lists, so rebuilding the whole grid is simpler than working out what changed.
        _book.Clear();
        foreach (BookSnapshot book in _snapshots.Values.OrderBy(b => b.Symbol, StringComparer.OrdinalIgnoreCase))
        {
            foreach (BookEntry bid in book.Bids)
            {
                _book.Add(new BookRow(book.Symbol, "Buy", bid));
            }
            foreach (BookEntry ask in book.Asks)
            {
                _book.Add(new BookRow(book.Symbol, "Sell", ask));
            }
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
        MessageBox.Show(this, message, "Trading", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    /// <summary>A titled panel holding one grid. The grid is added first so the title sits above it.</summary>
    private static Panel NewPanel(DataGridView grid, string title)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(2) };
        panel.Controls.Add(grid);
        panel.Controls.Add(NewHeader(title));
        return panel;
    }

    private static Label NewLabel(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(8, 6, 2, 0) };

    private static Label NewHeader(string text) =>
        new() { Text = text, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(2, 4, 2, 6), Font = new Font(DefaultFont, FontStyle.Bold) };

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
