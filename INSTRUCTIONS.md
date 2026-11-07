# BgGame_Lib

> Collaboration contract: [`../AGENTS.md`](../AGENTS.md)
> Umbrella status & dependency graph: [`../INSTRUCTIONS.md`](../INSTRUCTIONS.md)
> Mission & principles: [`../VISION.md`](../VISION.md)
> Open work: the [tracker](https://github.com/halheinrich/backgammon/issues), label `BgGame_Lib`.

## What it is for

The game and quiz substrate, in two halves. **Match play** is rules-true
backgammon over BgMoveGen: the state, the cube, the dice and the unattended
loop that BgTournament's server runs and BgInference's agents plug into, with
the transcripts BgMatchFormat_Lib exports. The **quiz substrate** is what
BgQuiz_Blazor builds a run on: composing the problems, scoring the answers and
keeping the lifetime stats. The umbrella's "Direct project references" table
lists every consumer.

C# / .NET 10 class library, no UI, no I/O: a consumer loads and saves its own
bytes. Repo https://github.com/halheinrich/BgGame_Lib, branch `main`. It
depends on BgDataTypes_Lib (boards, plays, decisions, `ProblemKey`, rankings,
cube answers and costs, `EquityDisplay`) and BgMoveGen (`MoveGenerator`). The
XML doc, built on every build, is each member's contract; this file is the map
to the right type, how the parts fit, and the mistakes they invite.

## Build and test

From this directory:

```bash
dotnet build BgGame_Lib.slnx
```

```bash
dotnet test BgGame_Lib.slnx
```

The solution also builds BgDataTypes_Lib, its TestSupport project and BgMoveGen
from their sibling checkouts, so those submodules must be initialized. The
suite is xUnit, runs in seconds and reads nothing from `TestData/`. Warnings
are errors and the library builds its XML doc, so a broken `<see cref>` there
fails the build; the test project builds no XML doc, so nothing checks a `cref`
in a test's comment.

## Layout

- **Match play** — `GameState` / `MatchState` and their snapshots, the
  `Referee` (game end and cube responses; cube legality is
  `GameState.CanDouble`), `MatchRunner` with `MatchParticipant`, `IPlayAgent` /
  `ICubeAgent` and `IMatchObserver`, its results (`MatchResult`, `GameRecord`,
  `GameResult`), dice sources (`SeededDiceSource`, `RecordedDiceSource`,
  `VerifiableDiceSource` with `DiceKey` / `DiceCommitment`), and the
  `Transcript` of a game.
- **Quiz substrate** — scoring (`PlaySubmission`, `SubmittedPlay`,
  `SubmittedCubeAnswer`, `QuizScore`, `ScoreSegment`), lifetime stats
  (`ProblemStats`, `ProblemStatsDocument` and its JSON converter), the mix
  (`QuizMix`, `QuizMixEntry`, `QuizCategory`), the order (`ProblemOrder`),
  the pool functions (`DistinctProblems`, `ProblemSequence` with
  `MixComposition`), `AnswerTypeDistribution`, and `CooperativeYielder` for
  work that must yield to a browser's one thread.
- **Tests** — `BgGame_Lib.Tests`, xUnit; `TestRecords` from
  BgDataTypes_Lib.TestSupport builds the records.
- **`spec/`** — `verifiable-dice-vectors.json`, the cross-language vectors the
  dice tests reproduce.

## How the quiz substrate fits together

A consumer parses its files once, filters the decisions, deduplicates with
`DistinctProblems.OfAsync` (filter first: copies are not interchangeable to
a filter), and composes the run's sequence with
`ProblemSequence.BuildAsync`, handing it the filter's reading of XG's error
as a function for the by-error order: a blank mix presents the whole pool
in the order and reads no stats; an active mix selects by category against the
stats document as of a given instant, apportions by largest remainder,
redistributes shortfall, arranges each row and then the selection in the
order, and reports `Composition`. `BuildAsync` takes the caller's `Random`
(seed it for reproducibility); both functions take the caller's
`CooperativeYielder` and return read-only lists. The run then indexes the
sequence; nothing streams.

Each answer goes through `PlaySubmission.Score` or `SubmittedCubeAnswer.Score`.
A scored submission folds into the session's `QuizScore` and, through
`ProblemStatsDocument.Plus`, into the lifetime document, each fold returning a
new value for the consumer to keep and, for the document, to save.

## Pitfalls

Each entry helps a reader avoid a mistake or make a decision; the contract of
each type named is its XML doc.

### Match play

- **`GameState.ApplyPlay` is the only turn-boundary primitive.** It flips the
  board, the scores and the cube owner together; `state.Board.ApplyPlay` alone
  leaves a half-flipped state. Validation, and the throw before anything
  mutates, are `MoveGenerator.ApplyPlay`'s (BgMoveGen, "Validating turn-boundary
  apply").
- **A 1-point match is cubeless, never Crawford.** External data calling its
  games Crawford is normalised before `MatchState.FromScores`, which refuses the
  combination. Gate a double on `CanDouble`: `DoubleCube` throws rather than
  declines.
- **A resume at one-away says which game it is.** `MatchState.FromScores`
  with the leader one-away and `isCrawford` false is post-Crawford, and no
  later game is Crawford; resuming before the Crawford game is played, pass
  true. The Crawford standing's rule (exactly one side one-away) is
  BgDataTypes_Lib's `MatchStanding`, and its refusal is that library's.
- **Cube responders see their own frame.** `ChooseResponseAsync` gets a detached
  `GameState.OpponentView()`; an `ICubeAgent` written against the offerer's
  frame inverts silently.
- **Read attribution, never derive it.** `TranscriptEntry.OnRollSeat` is the
  frame seat, not the actor: read `CubeTranscriptEntry.ActingSeat` and
  `GameEndedTranscriptEntry.Winner`. `GameResult.OnRollWon` is
  perspective-relative; `GameRecord.Winner` is absolute.
- **Two play entries are equal only in the same encoding.** Ask
  `BoardState.IsSamePlay` whether they hold the same play.
- **The match loop enforces less than it documents**
  (halheinrich/backgammon#286). Enforced: every play is validated before it
  applies, and an out-of-contract agent answer aborts the run with
  `AgentContractViolationException`. Documented only: `IPlayAgent`'s "do not
  mutate". Agents get the live `GameState` (a cube responder alone gets a
  detached view), and the runner takes a play's decision snapshot after the
  agent returns, so a mutation would be recorded as the state it decided in.
  A finished `GameRecord.Transcript` can still be appended to, and
  `PlayTranscriptEntry` does not check its play against its own board.
- **A scripted match still reaches Crawford.** Games ended by cube passes bring
  a match to its Crawford game, where the cube window never opens and the
  script falls out of step; use a money session for exact transcripts.
- **Dice.** A `DiceCommitment` verifies only under the exact commit context: a
  mismatch is a plain `false`. `SeededDiceSource` is reproducible, not
  auditable: use `VerifiableDiceSource` where a party must be able to check the
  dice. Its derivation is a cross-language contract pinned by
  `spec/verifiable-dice-vectors.json`. That file records that an independent
  Python reference, not in this repository, generated it: never regenerate it
  from this implementation, or the tests compare the code with itself.

### Scoring and counting

- **Score through `PlaySubmission.Score` and `SubmittedCubeAnswer.Score`, never
  around them.** Which play is best and each play's error are the ranking's
  (BgDataTypes_Lib, "The ranking"), what an answer costs is
  `CubeDecision.CostOf`'s ("Cube answers and their costs"), and when a loss
  counts as zero is `EquityDisplay.CountsAsZero`'s ("When a loss counts as
  zero"). This library applies that rule in one place, the internal
  `Correctness`. A not-scored or off-list play is a skip of record and folds
  nothing. A cube answer is judged on its whole cost and each part on its own,
  never by comparing with `BestAnswer`, and never by combining its part
  verdicts.
- **Submission equality is identity, not result** (Hal's ruling of 2026-09-27,
  halheinrich/backgammon#273). `SubmittedPlay` compares `ProblemKey` and
  `AfterBoard`; `SubmittedCubeAnswer` compares key and answer; a scored
  `PlaySubmission` compares its `SubmittedPlay`. Read `EquityLoss`, `IsCorrect`,
  `Cost` for results; never dedupe results through a set. The two skips do not
  follow that identity: `NotScored` compares by candidate index and `OffList`
  by case alone (halheinrich/backgammon#287), so application code must not rest
  on `PlaySubmission` equality.
- **A cube answer counts differently in each place, by ruling** (SPEC-scoring
  §3). `QuizScore` adds each answer once to its Total, to the Double row always,
  and to the Take row only when the answer commits to a response. `ProblemStats`
  adds two decisions per answer, each half judged on its own part, with the
  whole cost as the loss. `AnswerTypeDistribution` counts problems present, not
  answers given: a cube position is one, in one bucket, so `Total` is the
  pool's count. They do not reconcile and must not be aligned; do not derive a
  Total by summing rows. That is the code today, the 2026-10-01 rule, which
  SPEC-scoring §3 still states. Hal's ruling of 2026-10-15, not yet implemented,
  replaces it for No double: its implied take is scored, in the Take row and in
  its cost (halheinrich/backgammon#420).
- **A stored tally is never re-scored or migrated.** A cube record can span two
  scoring rules: when costs changed on 2026-10-01
  (halheinrich/backgammon#326), records written before kept their old costs and
  new folds added to them. Whether earlier records keep their old costs under
  halheinrich/backgammon#420's rule is among the questions that issue leaves
  open.

### Documents

- **`ProblemStatsDocument`'s reader is strict and pinned; the types are not.**
  Property names and ordering are fixed by the converter; indentation is the
  writer's. Unknown or missing properties, unknown versions, bad keys or
  impossible tallies throw `JsonException`. The tally rules live only in that
  reader: `ScoreSegment` accepts correct above submitted or a negative count,
  and `ProblemStatsDocument.FromStats` checks only that a cube record holds
  whole answers, so a document built in code can hold a tally its own reader
  refuses (halheinrich/backgammon#328). A retired version throws
  `RetiredStatsSchemaException` and a foldable v4 document
  `FoldableStatsSchemaException`: both derive from `JsonException`, so catch
  them first, name a set-aside file from `SchemaVersion`, and read a foldable
  file with `ReadFoldable` + `Merge` rather than setting it aside. The document
  has no value equality; compare `Problems`.
- **A new wire unit is declared in `BgGameJsonContext`** and its converter is
  public. The completeness test
  (`TheContextDeclares_ExactlyThisLibrarysWireSurface`), not the build, is the
  gate: an undeclared type still works by reflection until trimming removes that
  path. The composition rules are BgDataTypes_Lib's ("Source generation &
  trimming").

### Composing a run

- **`QuizCategory` units differ.** Average loss is per answer, and that threshold
  is in equity (`QuizStatsMeasures.LossPerAnswer`). Wrong rate is per half, and
  that threshold is a fraction. "Seen" means sightings (`Submitted / 2` for a
  cube). `EverythingElse` has no predicate of its own: it means "matched by no
  other entry of this mix", so gate on `IsResidual`.
- **`QuizMix` entry order is meaning and equality**: contested decisions go
  to the earlier entry; the same entries reordered are a different mix.
  Reads are strict; `TryFromJson` yields `Empty`, never a partial mix. A mix
  selects and has no order of its own: the run's `ProblemOrder` presents
  (SPEC-filtering §5, "The mix selects; the order presents").
- **`ProblemSequence`:** a blank mix never dedupes and never reads stats,
  even when a document is given; an active mix dedupes by `DecisionId`
  (first wins), classifies by the derived `ProblemKey`, requires a document
  (an empty one is valid), and leaves decisions matched by no entry
  unreachable unless `EverythingElse` is in the mix. Shuffles and tie orders
  draw from the caller's `Random`: each build draws afresh, and an identically
  seeded `Random` repeats the draw.
- **`DistinctProblems` keys on `ProblemKey`**, so two XGIDs can collapse
  (same-away across lengths, mirror turn); first occurrence survives, full stop.
- **One spelling of each order key, in `ProblemOrdering`.** Without a mix, the
  by-error key reads the host's `recordedError`, never a view of its own: whose
  half of a cube decision it is belongs to the filter layer (SPEC-filtering §8).
  With an active mix, the player's lifetime loss per answer comes first where
  the record holds submissions, and the reading fills in the rest. A NaN or
  infinite reading throws. Keys are read once per build, only for decisions a
  set can reach, paced by the yielder, never inside a comparison.
