# How we work

## The repo

* `main` holds work that builds. Pull before you start: `git pull`.
* Commit little and often, with messages that say what changed
  ("exchange: match orders on price then time", not "update").
* Never commit passwords. RabbitMQ logins live in the `PBT205_RABBITMQ_*` environment variables.
* Build output (`bin`, `obj`, `.vs`) is ignored by `.gitignore`. Don't force it in.

## Who does what

| Area | Projects | Owner |
|---|---|---|
| Shared messaging library | `src/Pbt205.Messaging` | everyone, change by agreement |
| Task 1: chat + GUI | `src/Pbt205.Chat`, `src/Chat.Cli` | |
| Task 2: trading + GUI | `src/Pbt205.Trading`, `src/SendOrder.Cli`, `src/Exchange.Cli` | |
| Task 3: contact tracing + GUI | `src/Pbt205.Tracing`, `src/Person.Cli`, `src/Tracker.Cli`, `src/Query.Cli` | |
| Reports (one per task) | `docs/` | |

Fill in the owners at your first meeting and keep a short record of who did what.
Teamwork is 20% of the marks, and the commit history plus these notes are the evidence.

## Before you say a task is done

1. `dotnet build` with no warnings.
2. Run it against the shared RabbitMQ with someone else running the other side of it.
3. Check the message format still matches `docs/message-contracts.md`.
4. Screenshot the management page (Connections, Exchanges, Queues) for the report.
