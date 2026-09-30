# Demo videos

Three videos to submit, one per task. The facilitator will send instructions on what he wants;
this is the baseline so nothing from the brief gets left off camera. Add his requirements here
when they arrive.

## Rules of thumb

* Show everything the brief lists as a requirement, even the parts that feel obvious. If it is not
  on camera, it did not happen as far as the marker is concerned.
* Record at a readable resolution. Console text at 4K scaled down is unreadable; 1080p with a
  larger console font is better.
* Say whose machine is whose if more than one person appears. The whole point is that the apps are
  talking through middleware rather than to themselves.
* Our facilitator uses a Mac, so he cannot run the two Windows-only windows himself. The videos are
  how the final products get assessed. Worth being thorough with them.
* Keep one take per task rather than stitching clips, if you can. It reads as a working system.

## Task 1: chat

1. Start `chatGui` on two machines, log in with different names, same room.
2. Send messages both ways. Show the sender's name and the time on each message.
3. Show a join notice and a leave notice.
4. One person leaves and joins a different room, showing they no longer see the first room's
   messages. This covers the multiple-rooms extension.
5. The management page: two connections, one private queue per participant.
6. Worth showing the command-line app briefly as well, since that is what the brief asks for first.

## Task 2: trading

1. Start the exchange and leave it visible.
2. Send an order that cannot match, and show it resting in the book.
3. Send an order that crosses it. Show the trade in the exchange console and the price appearing in
   the trading window.
4. Point out that the trade happened at the resting order's price, not the incoming one, since that
   is a decision the group made rather than something the brief specifies.
5. Show a partial fill: a large order taking a smaller one and the remainder resting.
6. Send an order for a second stock, to cover the multiple-stocks extension.

## Task 3: contact tracing

1. Start the tracker, then two or three `person` apps with different identifiers.
2. Show positions arriving and a contact being recorded when two people share a square.
3. Run `query` for one of them, showing contacts newest first, and the app exiting afterwards.
4. Show the window: the board with people on it, and a lookup for one person.
5. Run it again with a board size other than 10, since the brief asks for that to be configurable.

## Before recording

* `git pull` and `dotnet build` so what is on camera matches what is submitted.
* Close anything with private information in it. Passwords, other coursework, personal messages.
* Have RabbitMQ's management page open in a tab, ready to switch to.
* Do a practice run. The first take is always the one where something is not running.
