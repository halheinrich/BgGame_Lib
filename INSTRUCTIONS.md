# BgGame_Lib

> Collaboration contract: [`../AGENTS.md`](../AGENTS.md)
> Umbrella status & dependency graph: [`../INSTRUCTIONS.md`](../INSTRUCTIONS.md)
> Mission & principles: [`../VISION.md`](../VISION.md)

## Stack

C# / .NET 10 / Class Library / xUnit. Pure substrate — no UI dependency, no
I/O. Razor-free by design (the human-via-clicks `IPlayAgent` lives in
BgQuiz_Blazor; everything in this library is reusable from non-Blazor consumers
including future bot-vs-bot drivers and replay analytics).

## Solution

`D:\Users\Hal\Documents\Visual Studio 2026\Projects\backgammon\BgGame_Lib\BgGame_Lib.slnx`

## Repo

https://github.com/halheinrich/BgGame_Lib — branch `main`.

## Depends on

- **BgDataTypes_Lib** — `Move`, `Play`, `BoardState` (with public
  `ApplyPlay` turn-boundary primitive), `BgDecisionData`, `CubeOwner`,
  `CubeAction`, plus the enums' string serialization contract.
- **BgMoveGen** — `MoveGenerator` (legal-play enumeration and the
  validating turn-boundary `MoveGenerator.ApplyPlay`).

## Directory tree

```
BgGame_Lib.slnx
BgGame_Lib/
  BgGame_Lib.csproj
  GameResult.cs           — record + GameResultKind enum (single / gammon / backgammon)
  GameSnapshot.cs         — immutable record (transcript-friendly)
  GameState.cs            — mutable: Board + cube state; aggregates a MatchState
  ICubeAgent.cs           — two-method interface (offer / response)
  IPlayAgent.cs           — async Play decision interface
  IProblemSetSource.cs    — re-iterable IAsyncEnumerable<BgDecisionData>
  MatchSnapshot.cs        — immutable record
  MatchState.cs           — mutable: match length, scores, Crawford
  QuizScore.cs            — immutable cumulative score: play / double / take segments + derived total
  Referee.cs              — skeletal: end-of-game, ApplyCubeResponse
  ScoreSegment.cs         — immutable per-category running tally (submitted / correct / equity loss)
  SubmittedCubeAction.cs  — record: user cube decision + per-half (doubler/taker) results
  SubmittedPlay.cs        — record: user play + matched candidate + equity loss
  Transcript.cs           — append-only ordered list of TranscriptEntry
  TranscriptEntry.cs      — abstract record + Play / Cube / GameEnded subtypes
BgGame_Lib.Tests/
  BgGame_Lib.Tests.csproj
  CubeAgentContractTests.cs
  GameStateTests.cs
  MatchStateTests.cs
  PlayAgentContractTests.cs
  ProblemSetSourceContractTests.cs
  QuizScoreTests.cs
  ScoreSegmentTests.cs
  RefereeTests.cs
  TranscriptTests.cs
  TrivialTypeTests.cs
```

## Architecture

### Substrate role across modes

This library is the multi-mode scaffolding shared by the four eventual play
modes (scored quiz, user-vs-user, user-vs-bot, bot-vs-bot tournament). It
ships the abstractions; concrete implementations of `IPlayAgent`,
`ICubeAgent`, and `IProblemSetSource` live in the consumers (today:
BgQuiz_Blazor; future: BgRLEngine inference wrapper, online evaluator
harnesses, problem-set hosting backends). Phase 1 quiz mode exercises only a
slice (the play-grader as a passive `IPlayAgent`, a server-disk
`IProblemSetSource`); the rest is built ahead per ../AGENTS.md "Best-practice
bias" so Phase 2+ modes plug in without rewrite.

### State composition

Match-level vs. game-level state are separate types with distinct lifetimes:

- `MatchState` lives for the duration of a match. Mutable. Owns the match
  length, the on-roll-relative scores, and the Crawford flag.
- `GameState` lives for one game within the match. Mutable. Owns the board
  and the cube state. Aggregates a `MatchState` reference (does not own it —
  the same `MatchState` persists across multiple `GameState`s as games come
  and go).

Why split rather than composite: lifetimes are encoded in the type system
(a new `GameState` is constructed at game start; the surrounding `MatchState`
continues), match-level invariants (Crawford transitions, match-end
detection) have a single home, and Phase 2+ tournament drivers work against
a clean separation. Composite would have mixed scopes and force callers to
remember which fields reset between games.

### On-roll-relative perspective

All state is stored from the on-roll player's perspective, matching
BgDataTypes_Lib's `Mop` convention:

- `GameState.Board`: 26-element point array; positive = on-roll's checkers,
  negative = opponent's; `[0]` = opponent bar, `[25]` = on-roll bar.
- `MatchState.OnRollScore` / `OpponentScore`: scores labeled relative to
  the active perspective.
- `GameState.CubeOwner`: `OnRoll` / `Opponent` / `Centered` likewise mean
  the current perspective's labels.

All three flip together when control passes between players. The single
public turn-transition primitive is `GameState.ApplyPlay(play, die1, die2)`,
which:

1. Delegates to `MoveGenerator.ApplyPlay(Board, play, die1, die2)` —
   validates the play is legal, applies all moves, and flips the board
   atomically (`MoveGenerator.ApplyPlay` is the validating wrapper around
   `BoardState.ApplyPlay` introduced in BgDataTypes_Lib / BgMoveGen).
   Throw-before-mutate: an illegal play raises `ArgumentException` and
   leaves the substrate untouched.
2. Calls the internal `MatchState.SwapPerspective()` to swap the score
   labels.
3. Inlines the cube-owner switch (`OnRoll ↔ Opponent`, `Centered`
   stays) on `GameState`.

There is no public `SwapPerspective` / `Flip` surface on `GameState`,
`MatchState`, or `BoardState`. The board-side `Flip()` is private inside
`BoardState` (BgDataTypes_Lib); `MatchState.SwapPerspective` is internal
and reached from tests via `InternalsVisibleTo("BgGame_Lib.Tests")`.
External consumers reasoning in the on-roll perspective never need to flip directly —
`GameState.ApplyPlay` is the only path that crosses a turn boundary, and
half-flipped intermediate states are unreachable.

### Mutability decisions

| Type | Mutability | Rationale |
|---|---|---|
| `MatchState`, `GameState`, `Transcript` | Mutable, append-only or method-driven transitions | Game and match progression naturally mutate; matches BgMoveGen's apply/undo idiom. |
| `MatchSnapshot`, `GameSnapshot`, `TranscriptEntry` and subtypes, `GameResult`, `SubmittedPlay`, `SubmittedCubeAction`, `ScoreSegment`, `QuizScore` | Immutable records, init-only / by-value | Transcript-friendly; safe to share across threads and across history. |

`Snapshot()` methods on `MatchState` and `GameState` produce immutable
records. `GameSnapshot.Board` is a defensive copy of `BoardState.Points`,
so subsequent mutations of the live board do not retroactively change a
snapshot already captured.

### Agent abstractions

`IPlayAgent.ChoosePlayAsync` and `ICubeAgent.ChooseOfferAsync` /
`ChooseResponseAsync` all return `ValueTask<T>` and accept a
`CancellationToken`.

- **Async surface** keeps the door open for online evaluators (network bots,
  BgRLEngine inference wrappers) without forcing in-memory agents (quiz
  graders, replay readers) to pay a Task allocation per call —
  `ValueTask<T>.FromResult(...)` completes synchronously.
- **Two cube methods** rather than one with a context discriminator narrow
  the legal return-set per call: the offer side returns `NoDouble | Double`,
  the response side returns `Take | Pass`. The driver (Referee or its
  consumer) calls the right method at the right time.

The contract requires agents to produce a valid result; "no opinion" is not
encoded as a sentinel return value. Resign / timeout exits are deferred to
Phase 2+ via cancellation-token cooperation.

### Referee scope

The skeletal Referee covers:

1. **End-of-game detection** — `IsGameOver(GameState) → GameResult?`. Counts
   on-roll vs. opponent checkers; classifies single / gammon / backgammon
   based on (a) whether the loser has borne off any checkers and (b) whether
   the loser has a checker on the bar or in the winner's home board. Cube size
   is folded into the returned result via `GameResult.Points`.
2. **Cube response application** —
   `ApplyCubeResponse(GameState, CubeAction) → GameResult?`. `Take` doubles
   the cube and transfers ownership to the responder; returns null. `Pass`
   returns a single-win `GameResult` at the pre-double cube value (state
   unchanged so the caller decides how to dispose of the game). Throws on
   `NoDouble` / `Double` — those are offer-side values.

Play application is **not** on the Referee — it lives on
`GameState.ApplyPlay(play, die1, die2)` (see "On-roll-relative
perspective"). Turn-boundary mutation is a property of the game state
itself; the validated apply primitive lives in BgMoveGen
(`MoveGenerator.ApplyPlay`). Routing it through Referee would have
re-implemented legality checking outside the move generator — an
encapsulation leak — so the Referee owns only what is genuinely
arbitration logic (game-end classification and cube-response handling).

Out of skeletal scope (Phase 2+): driver loop / coroutine, time controls,
post-Crawford automatic-doubling rules, match-end detection (which lives on
`MatchState.IsMatchOver`), and full legal-to-offer validation against match
context. The umbrella INSTRUCTIONS.md tracks which features arrive with
which mode.

### Transcript model

`Transcript` is an append-only mutable container of `TranscriptEntry`
records. Each entry carries an immutable `GameSnapshot` (which itself
contains a `MatchSnapshot`), so an entry retains its captured state
regardless of subsequent live mutations.

Three concrete entry subtypes — `PlayTranscriptEntry`,
`CubeTranscriptEntry`, `GameEndedTranscriptEntry` — form a closed
discriminated union via abstract base record + pattern matching. A single
record with nullable fields would be worse; legal-payload-by-discriminator
would have to be enforced at runtime rather than by the type system.

### Problem-set source

`IProblemSetSource` is the interface every quiz / replay loop reads from.
Surface: `Name` (display label), `Count` (`int?`, null when streaming), and
`EnumerateAsync(CancellationToken) → IAsyncEnumerable<BgDecisionData>`.

Re-iterable contract: each `EnumerateAsync` call yields a fresh enumeration
from the start. Implementations that genuinely cannot replay (e.g., a
one-shot in-memory upload that has been consumed) document the limitation
and throw on second call.

Phase 1 ships against a server-disk implementation in BgQuiz_Blazor.
Future implementations (uploaded files, deployed problem-set bundles,
curated libraries) plug in via the same interface without changes to the
consuming quiz controller.

### Quiz-result records

**`SubmittedPlay`** captures one user play scored against a position's
candidates: the chosen `Play`, the matched-candidate index (nullable for
analysis omissions), the equity loss vs. best, and an `IsCorrect` flag.

**`SubmittedCubeAction`** is the cube analog — the scored carrier for one
cube position. A cube position is two independent atomic decisions (the
doubler's offer choice and the taker's response choice), so the record holds
the user's `CubeDecisionPair` plus a per-half result for each side:
`DoublerEquityLoss` / `TakerEquityLoss` and `DoublerCorrect` / `TakerCorrect`.
The four result fields are pre-computed consumer-side from the position's
analysis (`DecisionData.DoublerActionError` / `TakerActionError` against
`BestDoublerAction` / `BestTakerAction`); this library only carries and
accumulates them.

**`ScoreSegment`** is the single accumulation primitive: an immutable
`(Submitted, Correct, TotalEquityLoss)` tally with derived `AverageEquityLoss`
and `Accuracy` (the correct/submitted fraction in [0, 1] — percentage
rendering is a display concern left to the consumer). `Add(submitted, correct,
loss)` and `operator +` both return a fresh segment; `Add` is defined in terms
of `operator +`, so component-wise combination has one definition.
`operator +` null-guards both operands (records are reference types).

**`QuizScore`** is an immutable cumulative record holding three independent
`ScoreSegment`s — `PlayDecisions`, `DoubleDecisions`, `TakeDecisions` — plus a
derived `Total` (their sum via `operator +`, never stored, so it cannot drift).
Two overloads fold submissions in and return a fresh score (no aliasing, fitting
Razor's render-state model):

- `Plus(SubmittedPlay)` adds one submission to `PlayDecisions`.
- `Plus(SubmittedCubeAction)` adds **one submission to each** of
  `DoubleDecisions` and `TakeDecisions` — a cube position scores its doubler
  and taker halves independently; there is no combine rule across the two.

The per-problem history is intentionally not in the score — consumers that
need it keep an `IReadOnlyList<SubmittedPlay>` / `SubmittedCubeAction`
alongside.

### Why these types live here, not in BgDataTypes_Lib

BgDataTypes_Lib's charter is the shared data layer: types and pure
data-shape translations between data types or to primitives. Game-mode
logic — agents, referees, transcripts, problem-set iteration, quiz scoring
— is not data-shape work; it's domain logic that consumers other than the
shared-data layer need. The BgDataTypes_Lib charter explicitly cites
`SubmittedPlay` and `QuizScore` as belonging to BgGame_Lib for this reason.

## Public API

```csharp
// State
public sealed class MatchState
{
    public int MatchLength { get; init; }
    public int OnRollScore { get; }
    public int OpponentScore { get; }
    public bool IsCrawford { get; }
    public bool IsMatchOver { get; }
    public static MatchState NewMatch(int matchLength);
    public static MatchState FromScores(int matchLength, int onRollScore, int opponentScore, bool isCrawford);
    public void AwardGame(GameResult result);
    public MatchSnapshot Snapshot();
}

public sealed class GameState
{
    public BoardState Board { get; }
    public MatchState Match { get; }
    public int CubeSize { get; }
    public CubeOwner CubeOwner { get; }
    public static GameState NewGame(MatchState match);
    public static GameState FromPosition(MatchState match, BoardState board, int cubeSize, CubeOwner cubeOwner);
    public void ApplyPlay(Play play, int die1, int die2);   // unified turn-transition primitive
    public void DoubleCube();
    public GameSnapshot Snapshot();
}

public sealed record MatchSnapshot(int MatchLength, int OnRollScore, int OpponentScore, bool IsCrawford);
public sealed record GameSnapshot(IReadOnlyList<int> Board, int CubeSize, CubeOwner CubeOwner, MatchSnapshot Match);

// Result types
public enum GameResultKind { WinSingle = 1, WinGammon = 2, WinBackgammon = 3 }
public sealed record GameResult(GameResultKind Kind, bool OnRollWon, int CubeSize) { public int Points { get; } }
// CubeAction (NoDouble | Double | Take | Pass) is defined in BgDataTypes_Lib;
// the signatures below consume it.

// Agents
public interface IPlayAgent
{
    ValueTask<Play> ChoosePlayAsync(GameState state, int die1, int die2, CancellationToken cancellationToken = default);
}

public interface ICubeAgent
{
    ValueTask<CubeAction> ChooseOfferAsync(GameState state, CancellationToken cancellationToken = default);
    ValueTask<CubeAction> ChooseResponseAsync(GameState state, CancellationToken cancellationToken = default);
}

// Referee
public sealed class Referee
{
    public GameResult? IsGameOver(GameState state);
    public GameResult? ApplyCubeResponse(GameState state, CubeAction response);
}

// Transcript
public abstract record TranscriptEntry(GameSnapshot State);
public sealed record PlayTranscriptEntry(GameSnapshot State, int Die1, int Die2, Play ChosenPlay) : TranscriptEntry(State);
public sealed record CubeTranscriptEntry(GameSnapshot State, CubeAction Action) : TranscriptEntry(State);
public sealed record GameEndedTranscriptEntry(GameSnapshot State, GameResult Result) : TranscriptEntry(State);

public sealed class Transcript
{
    public IReadOnlyList<TranscriptEntry> Entries { get; }
    public void Append(TranscriptEntry entry);
}

// Problem set
public interface IProblemSetSource
{
    string Name { get; }
    int? Count { get; }
    IAsyncEnumerable<BgDecisionData> EnumerateAsync(CancellationToken cancellationToken = default);
}

// Quiz scoring
public sealed record SubmittedPlay(Play UserPlay, int? MatchedCandidateIndex, double EquityLoss, bool IsCorrect);
public sealed record SubmittedCubeAction(
    CubeDecisionPair UserDecision,
    double DoublerEquityLoss, double TakerEquityLoss,
    bool DoublerCorrect, bool TakerCorrect);

public sealed record ScoreSegment(int Submitted, int Correct, double TotalEquityLoss)
{
    public static ScoreSegment Empty { get; }
    public double AverageEquityLoss { get; }   // 0 when none submitted
    public double Accuracy { get; }            // Correct / Submitted, in [0, 1]; 0 when none submitted
    public ScoreSegment Add(int submitted, int correct, double loss);
    public static ScoreSegment operator +(ScoreSegment a, ScoreSegment b);   // null-guards both operands
}

public sealed record QuizScore(ScoreSegment PlayDecisions, ScoreSegment DoubleDecisions, ScoreSegment TakeDecisions)
{
    public static QuizScore Empty { get; }
    public ScoreSegment Total { get; }         // derived: PlayDecisions + DoubleDecisions + TakeDecisions
    public QuizScore Plus(SubmittedPlay play);
    public QuizScore Plus(SubmittedCubeAction cube);   // one double + one take decision
}
```

## Pitfalls

- **`GameState.ApplyPlay` is the only turn-boundary primitive.** After a
  successful call, the new on-roll player is the opposite of the player
  whose move was just applied. Callers that read `state.Board.Points`,
  `state.Match.OnRollScore`, or `state.CubeOwner` immediately afterward
  see the new perspective. Bypassing it — invoking `state.Board.ApplyPlay`
  directly — leaves match scores and cube ownership unflipped, producing a
  half-flipped, internally inconsistent substrate. Validation
  (throw-before-mutate) lives inside `MoveGenerator.ApplyPlay`, not in
  `GameState`; an illegal play raises `ArgumentException` and leaves
  every field untouched.
- **`GameSnapshot.Board` is a defensive copy; live `Board` is not.**
  `GameState.Board` aliases the live `BoardState`; consumers that retain it
  alongside a `GameSnapshot` will see live mutations. The snapshot's
  `IReadOnlyList<int>` view is stable.
- **`DoubleCube` is illegal when the opponent owns the cube.** From the
  current perspective, `CubeOwner.Opponent` means the opponent holds the
  cube and only they can offer; calling `DoubleCube` throws
  `InvalidOperationException`. Callers (typically `Referee.ApplyCubeResponse`
  on a Take) only invoke `DoubleCube` when the preconditions hold, but
  external code that constructs scenarios for testing must respect the rule.
- **Pass-side game result has cube size unchanged.** `Referee.ApplyCubeResponse`
  with `CubeAction.Pass` returns a `GameResult` carrying the *pre-double*
  cube size and does not mutate `GameState.CubeSize`. Callers that record
  the result and then continue using the same `GameState` for diagnostics
  or display see the cube at its old value, which is correct — the doubled
  cube would only have applied on Take.
- **`IsCorrect` on `SubmittedPlay` is denormalized.** It encodes the same
  fact as `EquityLoss == 0.0`, but is stored as an explicit bool to keep
  the record self-describing for downstream display. Producers must keep
  the two consistent; consumers should not derive a different correctness
  rule (e.g., "within 0.001 equity").
- **`IProblemSetSource.EnumerateAsync` is contractually re-iterable.** Implementations
  that wrap a one-shot resource (consumed upload, exhausted network stream)
  must throw on a second call rather than silently yielding an empty
  sequence — an empty sequence would look like "no problems" to the consumer.
- **`Match` aggregation, not ownership.** `GameState.Match` is a reference
  to a `MatchState` that may outlive the `GameState`. Mutations to the
  match (via `AwardGame`) persist into the next game's `GameState`. Do not
  treat the `MatchState` as game-scoped state.

## Subproject-internal next steps

None — surface complete for Phase 1 needs. Cross-cutting work (Phase 1
wiring into BgQuiz_Blazor, future Phase 2+ modes) is tracked in the
umbrella `INSTRUCTIONS.md`, not here.
