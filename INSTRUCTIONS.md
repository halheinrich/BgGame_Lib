# BgGame_Lib

> Collaboration contract: [`../AGENTS.md`](../AGENTS.md)
> Umbrella status & dependency graph: [`../INSTRUCTIONS.md`](../INSTRUCTIONS.md)
> Mission & principles: [`../VISION.md`](../VISION.md)

## What it is

The game and quiz substrate: rules-true match play over BgMoveGen, and the
problem-set, scoring and lifetime-stats types BgQuiz_Blazor builds on. C# /
.NET 10 class library, no UI, no I/O. Solution
`D:\Users\Hal\Documents\Visual Studio 2026\Projects\backgammon\BgGame_Lib\BgGame_Lib.slnx`;
repo https://github.com/halheinrich/BgGame_Lib, branch `main`. Depends on
BgDataTypes_Lib (boards, plays, decisions, `ProblemKey`, rankings, cube
answers and costs, `EquityDisplay`) and BgMoveGen (`MoveGenerator`). The
public surface carries XML docs; this file says only what the docs cannot.

## Layout

- **Match play** — `GameState` / `MatchState` and their snapshots, the
  `Referee` (cube legality), `MatchRunner` with `IPlayAgent` / `ICubeAgent` /
  `IMatchObserver`, dice sources (`SeededDiceSource`, `RecordedDiceSource`,
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

## How the quiz substrate fits together

A consumer parses its files once, filters the decisions, deduplicates with
`DistinctProblems.OfAsync` (filter first: copies are not interchangeable to
a filter), and composes the run's sequence with
`ProblemSequence.BuildAsync`, handing it the filter's reading of XG's error
as a function for the by-error order: a blank mix presents the whole pool
in the order and reads no stats; an active mix selects by category against the
stats document as of a given instant, apportions by largest remainder,
redistributes shortfall, arranges each row and then the selection in the
order, and reports `Composition`. Both functions take the caller's
`Random` (seed it for reproducibility) and `CooperativeYielder`, and return
read-only lists. The run then indexes the sequence; nothing streams.

## Pitfalls

- **`GameState.ApplyPlay` is the only turn-boundary primitive.** It flips
  perspective, scores and cube ownership together; `state.Board.ApplyPlay`
  alone leaves a half-flipped state. Validation lives in
  `MoveGenerator.ApplyPlay` and throws before mutating.
- **`GameSnapshot.Board` is a value; the live `Board` is not.**
- **`DoubleCube` refuses three cases** — no cube (a 1-point match is
  cubeless, never Crawford), the Crawford game, the opponent owns the cube.
  Gate on `CanDouble`. External data calling 1-point games "Crawford" must be
  normalised first.
- **A pass keeps the pre-double cube size** in the `GameResult`; the state
  is not mutated.
- **Score a play through `PlaySubmission.Score`, never around it.** Which
  play is best and each play's error are the ranking's. `IsCorrect` is the
  zero rule on `EquityLoss` (`EquityDisplay.CountsAsZero`); a not-scored or
  off-list play is a skip of record. A cube answer is judged on its whole
  cost, never by comparing with `BestAnswer`, and never by summing its part
  verdicts.
- **Submission equality is identity, not result.** `SubmittedPlay` compares
  `ProblemKey` and `AfterBoard`; `SubmittedCubeAnswer` compares key and
  answer. Read `EquityLoss`, `IsCorrect`, `Cost` for results; never dedupe
  results through a set.
- **`QuizScore` and `ProblemStats` count a cube answer differently, by
  ruling** (SPEC-scoring §3): the session score adds each answer once to its
  Total and only responses to the Take row; the lifetime record adds two
  decisions per answer, each half judged on its own part. They do not
  reconcile and must not be aligned. Do not derive a Total by summing rows.
- **`AnswerTypeDistribution` counts a cube position as one.** `Total` is
  the number of decisions folded in, which lets a consumer use it as the
  pool's count; one bucket per `Add`, always.
- **`ProblemStatsDocument` JSON is strict and pinned.** Property names and
  ordering are fixed by the converter; indentation is the writer's. Unknown
  or missing properties, unknown versions, bad keys or tallies throw
  `JsonException`; a retired version throws `RetiredStatsSchemaException`
  and a foldable v4 document `FoldableStatsSchemaException` — catch both
  before `JsonException`, name a set-aside file from `SchemaVersion`, and
  read a foldable file with `ReadFoldable` + `Merge` rather than setting it
  aside. The document has no value equality; compare `Problems`.
- **A new wire unit is declared in `BgGameJsonContext`** and its converter
  is public; the completeness test, not the build, is the gate (trimming
  breaks reflection fallbacks).
- **A cube record can span two scoring rules** (costs changed 2026-10-01,
  the record kept): never re-score or migrate a tally.
- **`QuizCategory`:** average loss is per answer
  (`QuizStatsMeasures.LossPerAnswer`), wrong rate per half, thresholds are
  fractions, "seen" means sightings (`Submitted / 2` for a cube).
  `EverythingElse` has no predicate; gate on `IsResidual`.
- **`QuizMix` entry order is meaning and equality**: contested decisions go
  to the earlier entry; the same entries reordered are a different mix.
  Reads are strict; `TryFromJson` yields `Empty`, never a partial mix. A mix
  has no order of its own (retired 2026-10-14): the run's `ProblemOrder`
  presents.
- **`ProblemSequence`:** a blank mix never dedupes and never reads stats,
  even when a document is given; an active mix dedupes by `DecisionId`
  (first wins), classifies by the derived `ProblemKey`, requires a document
  (an empty one is valid), and leaves decisions matched by no entry
  unreachable unless `EverythingElse` is in the mix. Under `Shuffled` each
  build is a fresh draw. `DistinctProblems` keys on `ProblemKey`, so two
  XGIDs can collapse (same-away across lengths, mirror turn); first
  occurrence survives, full stop.
- **One spelling of each order key, in `ProblemOrdering`**; the by-error key
  reads the host's `recordedError`, never a view of its own: whose half of a
  cube decision it is belongs to the filter layer (SPEC-filtering §8). A
  NaN or infinite reading throws. Keys are read once per build, only for
  decisions a set can reach, paced by the yielder, never inside a
  comparison.
- **`Match` is aggregation, not ownership**: `GameState.Match` outlives the
  game.
- **Cube responders see their own frame** (`GameState.OpponentView()`, a
  detached snapshot); an `ICubeAgent` written against the offerer's frame
  inverts silently.
- **`TranscriptEntry.OnRollSeat` is the frame seat, not the actor**; read
  `CubeTranscriptEntry.ActingSeat` and `GameEndedTranscriptEntry.Winner`.
  Two play entries are equal only in the same encoding; ask
  `BoardState.IsSamePlay` for the same play.
- **`IMatchObserver` callbacks run inline and fail fast**; there is no abort
  callback.
- **`GameResult.OnRollWon` is perspective-relative**; `GameRecord.Winner`
  is absolute.
- **A scripted match that never plays checkers still hits Crawford**; use a
  money session for exact transcripts.
- **Dice:** a `DiceCommitment` verifies only under the exact commit context
  (a mismatch is a silent false); `SeededDiceSource` is not fair-mode;
  `VerifiableDiceSource` implements a frozen cross-language contract pinned
  by `spec/verifiable-dice-vectors.json` — regenerate vectors from the
  independent reference, never from this implementation.
