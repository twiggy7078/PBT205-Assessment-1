# Message contracts

Every app agrees on these before anyone writes code, because mismatched messages are where
group projects lose time during integration. Change them only by agreement, and tell the team.

All messages are UTF-8 JSON with camelCase names and enums written as text.
Topic names live in `src/Pbt205.Messaging/Topics.cs`.

| Topic (exchange) | Routing key | Published by | Read by | Message |
|---|---|---|---|---|
| `room` | room name (`general`) | chat | chat | `ChatMessage` |
| `orders` | stock symbol (`XYZ`) | sendOrder | exchange | `OrderMessage` |
| `trades` | stock symbol | exchange | GUI, anyone watching | `TradeMessage` |
| `book` * | stock symbol | exchange | trading GUI | `BookSnapshot` |
| `position` | person id | person | tracker, GUI | `PositionMessage` |
| `query` | person id | query | tracker | `ContactQuery` |
| `query-response` | person id | tracker | query, GUI | `ContactQueryResponse` |

## Task 1: chat

```json
{ "kind": "Message", "senderId": "0f8c...", "username": "Jane",
  "room": "general", "text": "hello", "sentAt": "2026-09-23T04:10:00+00:00" }
```
`kind` is `Message`, `Joined` or `Left`. `senderId` is unique per running app, so an app skips its own messages.

## Task 2: trading

```json
{ "orderId": "3b1e...", "username": "Andrew", "symbol": "XYZ",
  "side": "Buy", "quantity": 100, "price": 10.50, "placedAt": "..." }

{ "tradeId": "9ac2...", "symbol": "XYZ", "buyer": "Andrew", "seller": "Jane",
  "quantity": 100, "price": 10.50, "tradedAt": "..." }
```

\* `book` is ours, not the brief's. After each order the exchange publishes the state of that
stock's book, so the trading window can show orders that are still waiting:

```json
{ "symbol": "XYZ", "lastTradedPrice": 10.00, "asOf": "...",
  "bids": [ { "username": "Jane", "quantity": 150, "price": 10.25 } ],
  "asks": [ { "username": "Andrew", "quantity": 100, "price": 10.50 } ] }
```

Both sides are best price first, at most ten deep.

Matching rules, agreed as a group:

* A buy matches a sell when the buy price is greater than or equal to the sell price.
* Best price first (highest buy, lowest sell), then earliest order first.
* The trade price is the resting order's price, the one already in the book.
* Quantity is fixed at 100 for this assessment, but it stays a field so it can change later.

## Task 3: contact tracing

```json
{ 
  "clientId": "3b1e...", 
  "person": "andrew", 
  "x": 3, 
  "y": 7, 
  "movedAt": "2026-09-23T04:00:00+00:00"
}

{ 
  "correlationId": "7d4f...", 
  "person": "andrew", 
  "askedAt": "2026-09-23T04:00:00+00:00"
}

{
  "correlationId": "7d4f...",
  "person": "andrew",
  "answeredAt": "2026-09-23T04:00:00+00:00",
  "contacts": [
    { "person": "jane", "x": 3, "y": 7, "metAt": "2026-09-23T04:00:00+00:00" }
  ]
}
```

* A contact is recorded when two people occupy the same square at the same time. It is recorded
  once, when someone arrives on an occupied square, and for both people.
* Contacts come back newest first.
* `correlationId` lets each query app pick out its own answer from `query-response`.
* `person` keeps itself inside its own board, so nobody walks off the edge. The tracker does not
  clamp: it trusts the positions it receives.
* **Board size is a convention, not a rule.** Each `person` is given its board size at start-up and
  nothing carries it in the messages, so the tracker and the window cannot tell what size anyone is
  using. The group agreed (option A, 3 October) that everyone passes the same `--board-size` for a
  run, and the window is set to match. Putting the size in `PositionMessage` was considered and
  rejected as more than the brief asks for. Write this up in report 3 as a limitation.
* **Duplicate names are renamed.** If two people start with the same identifier, the tracker gives
  the second one `Name-2`. The person's own console still shows the name it was started with, so a
  lookup has to use the resolved name shown by the tracker.
