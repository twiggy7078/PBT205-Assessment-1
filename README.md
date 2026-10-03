# PBT205 Assessment 1: Middleware Prototypes

C# (.NET 10) prototypes that talk to each other through RabbitMQ.

| Project | What it is |
|---|---|
| `src/Pbt205.Messaging` | Shared RabbitMQ code used by every task: connecting, publishing and subscribing |
| `src/Pbt205.Chat` | Chat room logic, shared by the console app and the window |
| `src/Chat.Cli` | **Task 1** command-line app (`chat.exe`) |
| `src/Chat.Gui` | **Task 1** final product: chat window with room selection (Windows only) |
| `src/Pbt205.Trading` | **Task 2** contracts and order book |
| `src/SendOrder.Cli`, `src/Exchange.Cli` | **Task 2** command-line apps (`sendOrder.exe`, `exchange.exe`) |
| `src/Trading.Gui` | **Task 2** final product: live prices window (Windows only) |
| `src/Pbt205.Tracing` | **Task 3** contracts and board (skeleton) |
| `src/Person.Cli`, `src/Tracker.Cli` | **Task 3** command-line apps (`person.exe`, `tracker.exe`) |
| `src/Query.Cli` | **Task 3** contact lookup (skeleton) |
| `src/Tracing.Gui` | **Task 3** final product: environment window (Windows only) |

Every project reuses `Pbt205.Messaging`. Before writing code for Tasks 2 or 3, read
`docs/message-contracts.md` (what each app sends) and `docs/how-we-work.md` (repo rules and who does what).

## Setup (once per computer)

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download) (`winget install Microsoft.DotNet.SDK.10`). To use an IDE, pick Visual Studio 2026, VS Code with C# Dev Kit, or Rider (Visual Studio 2022 does not support .NET 10).
2. Connect to Tailscale so you can reach the RabbitMQ server at `100.74.122.113:5672`.
3. Save your own RabbitMQ login as environment variables (PowerShell, then open a new terminal):

   ```powershell
   setx PBT205_RABBITMQ_USER "<your-rabbitmq-username>"
   setx PBT205_RABBITMQ_PASSWORD "<your-password>"
   ```

   Usernames and passwords are case-sensitive. If these aren't set, the app asks for them when it starts.
   Never put passwords in the code or commit them to Git.

## Build and run

```powershell
dotnet build
dotnet run --project src/Chat.Cli -- <your-name> 100.74.122.113:5672
```

Or run the built exe directly:

```powershell
.\src\Chat.Cli\bin\Debug\net10.0\chat.exe <your-name> 100.74.122.113:5672 --room general
```

The first argument is the name other people see in the room, so each person uses their own:

```powershell
dotnet run --project src/Chat.Cli -- Andrew 100.74.122.113:5672
dotnet run --project src/Chat.Cli -- Jane   100.74.122.113:5672
dotnet run --project src/Chat.Cli -- Jacobo 100.74.122.113:5672 --room general
dotnet run --project src/Chat.Cli -- Angelo 100.74.122.113:5672 --room study
```

#### Chat window (final product)

```powershell
dotnet run --project src\Chat.Gui
```

Fill in the endpoint, your RabbitMQ login, the name others will see and the room, then press Join.
Type a message and press Enter or click Send. Press Leave to go back to the login fields, where you
can pick a different room. This project needs Windows, since it uses WinForms.

### Task 2: trading

Start one exchange, then send orders from as many windows as you like.

```powershell
# One person runs the exchange and leaves it open
dotnet run --project src\Exchange.Cli -- 100.74.122.113:5672
```

```powershell
# Everyone else sends orders from their own machine, under their own name
dotnet run --project src\SendOrder.Cli -- Andrew 100.74.122.113:5672 SELL 100 10.00
dotnet run --project src\SendOrder.Cli -- Jane   100.74.122.113:5672 SELL 100 10.50
dotnet run --project src\SendOrder.Cli -- Jacobo 100.74.122.113:5672 BUY  100 9.75
dotnet run --project src\SendOrder.Cli -- Angelo 100.74.122.113:5672 BUY  250 10.25
```

With those four orders, Angelo's buy at 10.25 takes Andrew's cheaper sell at 10.00 and 150 of his
shares rest in the book as the best buy. Jane's sell at 10.50 and Jacobo's buy at 9.75 stay in the
book, because nobody will meet those prices yet.

The exchange prints each order, any trades, and the state of the book. `sendOrder` sends one order
and exits, as the brief requires. Add `--symbol ACME` to either app to use a different stock.

#### Trading window (final product)

```powershell
dotnet run --project src\Trading.Gui
```

Fill in the endpoint and your RabbitMQ login, then press Connect. The window shows the latest price
each stock traded at, the orders still waiting in the book, the most recent trades, and can send
orders itself. It only listens and
publishes: the exchange app still does the matching, so it must be running for anything to trade.
This project needs Windows, since it uses WinForms.

Only one exchange should run at a time against the same RabbitMQ. Two exchanges would each keep
half the orders, and matches would be missed.

### `chat` arguments

```
chat <username> <endpoint> [--room <name>]
```

| Argument | Meaning |
|---|---|
| `username` | Name shown to other people in the room |
| `endpoint` | RabbitMQ endpoint: `5672` (this computer), `host:5672`, or `amqp://host:5672` |
| `--room`, `-r` | Room to join. Default `general`. Letters, digits, `-` and `_` only; not case-sensitive |

Type a message and press Enter to send it. Type `/quit` or press Ctrl+C to leave.

Exit codes: `0` normal, `1` bad arguments or no login, `2` could not connect.

## How the messaging works

* Each topic in the brief is a **durable topic exchange** in RabbitMQ: `room`, `orders`, `trades`,
  `position`, `query`, `query-response` (see `Topics.cs`). We added one of our own, `book`, so the
  trading window can show orders that have not traded yet.
* For chat, the **routing key is the room name**, so one `room` exchange handles any number of rooms.
* Each running app creates its **own private queue** (server-named, exclusive, auto-delete) bound to the
  exchange. Every app gets its own copy of each message. A single shared queue would instead hand each
  message to only one app.
* Messages are **JSON** (camelCase, enums as text). The chat message contract:

  ```json
  {
    "kind": "Message",
    "senderId": "0f8c...",
    "username": "Jane",
    "room": "general",
    "text": "hello",
    "sentAt": "2026-09-16T04:10:00+00:00"
  }
  ```

  `kind` is `Message`, `Joined` or `Left`. `senderId` is unique per running app, so an app can skip
  its own messages even if two people use the same name.
* Each connection is named (for example `chat: Andrew`) so it's easy to spot on the management page's
  Connections tab.

## Known limitations (useful for the report)

* **Messages sent while an app is disconnected are lost for that app.** Its private queue is deleted
  when it disconnects. The client reconnects automatically (every 5 seconds) and carries on, but there
  is no chat history.
* **No delivery guarantee.** Messages are auto-acknowledged and publisher confirms are off, which is
  fine for chat but not for trading.
* **Console input and incoming messages share one window**, so a message can arrive in the middle of
  what you're typing. The GUI version fixes this.
* **Usernames aren't checked by the server.** Anyone with a RabbitMQ login can use any display name.

### Task 3: contact tracing

Start one tracker, then as many people as you like. A small board makes contacts happen sooner.

```powershell
# One person runs the tracker and leaves it open
dotnet run --project src\Tracker.Cli -- 100.74.122.113:5672
```

```powershell
# Everyone else walks around under their own name
dotnet run --project src\Person.Cli -- 100.74.122.113:5672 Andrew --board-size 4 -m 2
dotnet run --project src\Person.Cli -- 100.74.122.113:5672 Jacobo --board-size 4 -m 2
```

The tracker prints each move, and a CONTACT line whenever two people land on the same square.
If two people start with the same name, the tracker renames the second to `Name-2`, and that is
the name to use when looking them up.

#### Tracing window (final product)

```powershell
dotnet run --project src\Tracing.Gui
```

Fill in the endpoint, your RabbitMQ login and the board size, then press Connect. The window draws
everyone on the board as they move, lists who it has seen, and looks up a person's contacts. Type a
name and press Find contacts, or double-click someone in the list. The tracker must be running for
lookups to be answered. This project needs Windows, since it uses WinForms.
