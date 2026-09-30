# Where we are

Last updated: 23 September 2026. Update this file when you finish something.

## Who is doing what

| Person | Working on |
|---|---|
| Andrew | Middleware, shared library, Tasks 1 and 2 code. Integration support for Task 3 |
| Jane | Reports for Tasks 1 and 2 (writing packs are in the Assessment 1 folder) |
| Jacobo | Task 3: the `person` and `tracker` applications |
| Angelo | Task 3: the `query` application, checking the whole thing builds and runs on macOS, and organising the group test session |

## Quick view

| Part | State | Who |
|---|---|---|
| RabbitMQ on the NAS, accounts, Tailscale access | Done | Andrew |
| Shared library (`Pbt205.Messaging`) | Done, reused by every task | Andrew |
| **Task 1** chat command-line app | Done and tested | Andrew |
| **Task 1** chat window (final product) | Built, needs a real run by someone | Andrew |
| **Task 2** order book and matching | Done, 21 checks passing | Andrew |
| **Task 2** `sendOrder` and `exchange` | Done and tested live | Andrew |
| **Task 2** trading window (final product) | Done | Andrew |
| **Task 3** `person` and `tracker` | **Not started.** Skeletons and message contracts only | Jacobo |
| **Task 3** `query` | **Not started.** Skeletons and message contracts only | Angelo |
| **Task 3** tracing window | **Not started.** Needs a Windows machine | |
| Report 1 (chat) | Being written | Jane |
| Report 2 (trading) | Being written | Jane |
| Report 3 (contact tracing) | Waiting on Task 3 code | |
| Group test with everyone connected | Not done | Everyone |
| Three demo videos, one per task | Not started. See `docs/demo-videos.md` | |

## What needs doing, roughly in order

### 1. Task 3: contact tracing (the big one)

Three command-line apps plus a window. The message contracts are already agreed in
`docs/message-contracts.md`, and `Pbt205.Tracing` has the classes stubbed out with notes.

* `person <endpoint> <person-id> [--moves-per-second n] [--board-size n]` starts at a random square
  and publishes a new position every so often. It must not walk off the board.
* `tracker <endpoint>` keeps everyone's current position, records a contact whenever two people are
  on the same square, and answers queries.
* `query <endpoint> <person-id>` asks who someone has been in contact with, prints the answer newest
  first, and exits. It uses a correlation id so it only reads its own answer.
* A window showing the board and letting you look up a person.

Follow `src/Chat.Cli/Program.cs` for the argument handling and `src/Exchange.Cli/Program.cs` for the
pattern of a long-running app that keeps state.

### 2. A group test with all four of us

We have only ever run these on one or two machines. We need one session with everyone connected at
once, for the reports and for the teamwork evidence:

* Everyone in the same chat room, sending messages.
* One person runs `exchange`, the others send orders under their own names.
* Screenshots: the chat window, the trading window with a trade, the exchange console, and the
  RabbitMQ management page (Overview with traffic, and Connections showing all four of us).

### 3. Finish the reports

Reports 1 and 2 are drafted (in the Assessment 1 folder, not in this repo). Each still needs:

* Group member names and who did what, with commits as evidence.
* The screenshots from the group test.
* The repository URL and the submission date.
* A read-through by everyone, since we all put our names to them.

### 4. Before submitting

* Check the whole thing runs against a local RabbitMQ in Docker, since the marker cannot reach the
  NAS. `dotnet run --project src\Chat.Cli -- Marker1 5672` with the guest login.
* `dotnet build` with no warnings.
* Ask the facilitator whether they want a demonstration or a recording as well as the code.

## How to pick something up

1. `git pull`
2. Put your name against it in the table above, commit that change, and push it, so nobody doubles up.
3. Read `docs/message-contracts.md` before writing anything that sends a message.
4. Small, frequent commits. The history is our teamwork evidence.

## Task 3 is split between two people

Jacobo has `person` and `tracker`, which are tested against each other. Angelo has `query` and the
contact tracing window, both of which only read messages, so they can be built from the contracts in
`docs/message-contracts.md` before the other half is finished.

**Agree these together before either of you starts**, because they sit on the boundary between the
two halves. They are set out with suggested answers in section 3 of the Task 3 build pack:

* When exactly a contact is recorded, and whether it is recorded for both people.
* How the query application gets only its own answer (the `correlationId` in the contract).
* What happens when someone tries to move off the board.
* How movement speed is expressed as a start-up argument.

Write the decisions into `docs/message-contracts.md` so report 3 can quote them later.

## Still unowned

* The group test session with all four of us connected, and the screenshots the three reports need.
  Angelo is organising this.
* Submission checks: clean build, the whole thing run against a local RabbitMQ in Docker the way the
  marker will run it, and the three reports agreeing with each other.
* Somebody other than Andrew building and running both windows, since they have only ever run on one
  machine.

## Platforms

The command-line applications target plain .NET and run on Windows, macOS and Linux. Both windows
use Windows Forms, so they build anywhere but only run on Windows. Angelo is on a Mac, so the
contact tracing window needs a Windows owner, and Angelo instead checks that the solution builds and
the console apps run on macOS, which is what the facilitator will see.

Our facilitator uses a Mac, so he will not be able to run the windows himself. The three demo videos
are how the final products get assessed. Say so in one line in each report.
