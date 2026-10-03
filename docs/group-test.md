# Group test session

One sitting with all four of us connected, to prove the prototypes work across four machines and to
collect the screenshots the three reports need. Everything runs against the shared RabbitMQ on the
NAS, so nobody needs Docker for this.

Date: [agree a night]

## Before we start

* `git pull` and `dotnet build`. Everyone on the same code.
* Tailscale connected, and your own RabbitMQ login saved or to hand.
* Close anything private. Screenshots are going into reports with our names on them.
* Angelo is on a Mac, so he runs the command-line apps while the rest of us run the windows. That
  is worth capturing rather than hiding: it shows two different clients talking through the same
  broker.

## Who runs what

| Person |        Task 1        |                Task 2              |              Task 3               |
|--------|----------------------|------------------------------------|-----------------------------------|
| Andrew |      `Chat.Gui`      | `Exchange.Cli`, then `Trading.Gui` | `Tracker.Cli`, then `Tracing.Gui` |
| Jane   |      `Chat.Gui`      |           `SendOrder.Cli`          |           `Person.Cli`            |
| Jacobo |      `Chat.Gui`      |           `SendOrder.Cli`          |           `Person.Cli`            |
| Angelo | `Chat.Cli` (console) |           `SendOrder.Cli`          |     `Person.Cli`, `Query.Cli`     |

Andrew runs the long-lived apps (exchange, tracker) and takes the RabbitMQ management screenshots.

## How to make the screenshots hang together

1. **Everyone types the same agreed line** at some point: `PBT205 group test, [date]`. When that line
   appears in all four screenshots, the figures obviously come from one session.
2. **Screenshot on the call.** Someone says "screenshot now", wait a few seconds so everyone's
   messages have arrived, then capture.
3. **Send your images to Andrew**, not straight to Jane. Andrew names them and passes one tidy set
   across, so nothing is missing the night before submission.
4. **Crop before sending.** A full 4K screen shrinks to unreadable in a Word document. Capture the
   window, not the desktop.

## Task 1: chat

Order matters: a join notice only reaches people already connected, so whoever joins first never
sees their own arrival announced.

1. Andrew joins `general`, then Jane, then Jacobo, then Angelo on the console.
2. A few messages each, including the agreed line.
3. Jane leaves and rejoins in room `study`, types something, and the rest of us confirm we do not
   see it. That covers the multiple-rooms requirement.
4. Angelo types `/quit` so a leave notice appears for everyone.

**Screenshots:** everyone captures their own window or console. Andrew captures the RabbitMQ
Connections tab (four connections, four usernames, `chatGui:` and `chat:` names) and the Overview
page while messages are flowing, showing one queue and one consumer per app.

## Task 2: trading

Andrew starts `Exchange.Cli` first and leaves it open, then opens `Trading.Gui`.

1. Jane sends `SELL 100 10.00`, Jacobo sends `SELL 100 10.50`. Both rest in the book.
2. Angelo sends `BUY 250 10.25`. It trades 100 at **10.00**, not 10.25, and 150 rests as the best
   buy. That is the group's own matching rule, so it is worth saying out loud on camera.
3. Someone sends an order with `--symbol ACME` so a second stock appears.

**Screenshots:** Andrew captures the trading window (last price, waiting orders, recent trades with
other people's names in the buyer and seller columns) and the exchange console mid-match. Everyone
else captures their own `sendOrder` output.

## Task 3: contact tracing

Andrew starts `Tracker.Cli` first, then `Tracing.Gui`.

1. Jane, Jacobo and Angelo each run `Person.Cli` with their own name, `--board-size 4 -m 2`.
   Everyone uses the same board size: it is a convention, nothing enforces it.
2. Let them wander until the tracker logs contacts between more than one pair.
3. Angelo runs `Query.Cli` for someone and reads the answer in his console.
4. Andrew looks the same person up in the window, so the console and the window agree.
5. Short second run at `--board-size 10` to show the size is configurable.

**Screenshots:** Andrew captures the tracing window (several people on the board, contacts listed)
and the tracker console showing contacts between different pairs. Angelo captures his query output.

## At the busiest moment

With chat, trading and tracing apps all connected, Andrew captures the Connections tab one more
time. That single image is the strongest piece of evidence for the middleware section of all three
reports.

## Afterwards

* Andrew names and sends the full set to Jane.
* Everyone writes their own paragraph for the contributions table, in their own words.
* Anything that did not work gets written down the same night, while we remember it. Problems we
  found and fixed are worth marks; problems we forgot are not.
