# PBT205 Assessment 1: Middleware Prototypes

C# (.NET 10) prototypes that talk to each other through RabbitMQ.

| Project | What it is |
|---|---|
| `src/Pbt205.Messaging` | Shared RabbitMQ code used by every task: connecting, publishing and subscribing |
| `src/Pbt205.Chat` | Chat room logic, shared by the console app and the future GUI |
| `src/Chat.Cli` | **Task 1** command-line app (`chat.exe`) |
| `src/Pbt205.Trading` | **Task 2** contracts and order book |
| `src/SendOrder.Cli`, `src/Exchange.Cli` | **Task 2** command-line apps (`sendOrder.exe`, `exchange.exe`) |
| `src/Pbt205.Tracing` | **Task 3** contracts and board (skeleton) |
| `src/Person.Cli`, `src/Tracker.Cli`, `src/Query.Cli` | **Task 3** command-line apps (skeletons) |

Every project reuses `Pbt205.Messaging`. Before writing code for Tasks 2 or 3, read
`docs/message-contracts.md` (what each app sends) and `docs/how-we-work.md` (repo rules and who does what).

## Setup (once per computer)

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download) (`winget install Microsoft.DotNet.SDK.10`). To use an IDE, pick Visual Studio 2026, VS Code with C# Dev Kit, or Rider (Visual Studio 2022 does not support .NET 10).
2. Connect to Tailscale so you can reach the RabbitMQ server at `100.74.122.113:5672`.
3. Save your own RabbitMQ login as environment variables (PowerShell, then open a new terminal):

   ```powershell
   setx PBT205_RABBITMQ_USER "Andrew"
   setx PBT205_RABBITMQ_PASSWORD "your-password"
   ```

   Usernames and passwords are case-sensitive. If these aren't set, the app asks for them when it starts.
   Never put passwords in the code or commit them to Git.

## Build and run

```powershell
dotnet build
dotnet run --project src/Chat.Cli -- Andrew 100.74.122.113:5672
```

Or run the built exe directly:

```powershell
.\src\Chat.Cli\bin\Debug\net10.0\chat.exe Jane 100.74.122.113:5672 --room general
```

### Task 2: trading

Start one exchange, then send orders from as many windows as you like.

```powershell
dotnet run --project src\Exchange.Cli -- 100.74.122.113:5672
dotnet run --project src\SendOrder.Cli -- Andrew 100.74.122.113:5672 SELL 100 10.00
dotnet run --project src\SendOrder.Cli -- Jane 100.74.122.113:5672 BUY 100 10.25
```

The exchange prints each order, any trades, and the state of the book. `sendOrder` sends one order
and exits, as the brief requires. Add `--symbol ACME` to either app to use a different stock.

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
  `position`, `query`, `query-response` (see `Topics.cs`).
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
