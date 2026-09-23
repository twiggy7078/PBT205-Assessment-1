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

Matching rules, agreed as a group:

* A buy matches a sell when the buy price is greater than or equal to the sell price.
* Best price first (highest buy, lowest sell), then earliest order first.
* The trade price is the resting order's price, the one already in the book.
* Quantity is fixed at 100 for this assessment, but it stays a field so it can change later.

## Task 3: contact tracing

```json
{ "person": "andrew", "x": 3, "y": 7, "movedAt": "..." }

{ "correlationId": "7d4f...", "person": "andrew", "askedAt": "..." }

{ "correlationId": "7d4f...", "person": "andrew", "answeredAt": "...",
  "contacts": [ { "person": "jane", "x": 3, "y": 7, "metAt": "..." } ] }
```

* A contact is recorded when two people occupy the same square at the same time.
* Contacts come back newest first.
* `correlationId` lets each query app pick out its own answer from `query-response`.
* Positions are clamped to the board, so nobody walks off the edge.
