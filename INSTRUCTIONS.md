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
Directory.Packages.props  — central package-version management (CPM)
spec/
  verifiable-dice-vectors.json — committed cross-language dice vectors (the PROTOCOL.md contract)
BgGame_Lib/
  BgGame_Lib.csproj
  AgentContractViolationException.cs — seat + kind + offending value; thrown by MatchRunner
  AnswerTypeDistribution.cs — immutable per-pool answer-type tally: checker plays + the five cube verdicts
  BgGameJsonContext.cs    — source-generated JsonSerializerContext over the wire surface (see "Source generation & trimming")
  CooperativeYielder.cs   — time-budgeted cooperative-yield gate (TimeProvider seam) for long WASM loops
  DistinctPositionProblemSetSource.cs — IProblemSetSource decorator: one survivor per ProblemKey; duplicate-class telemetry
  DuplicatePositionClass.cs — telemetry record: one multi-copy content class (key + member DecisionIds)
  GameRecord.cs           — one completed game: winner seat + result + transcript
  MixedProblemSetSource.cs — IProblemSetSource decorator: composes a quiz from per-category pools by mix percentages
  MixComposition.cs       — per-enumeration composition telemetry: target (capped or capless) vs drawn + per-entry reports
  MixCompositionEntry.cs  — one entry's telemetry: pool size, requested, drawn
  GameResult.cs           — record + GameResultKind enum (single / gammon / backgammon)
  GameSnapshot.cs         — immutable record (transcript-friendly)
  GameStartContext.cs     — frame-free OnGameStarted payload (seat scores + Crawford)
  GameState.cs            — mutable: Board + cube state; aggregates a MatchState
  ICubeAgent.cs           — two-method interface (offer / response)
  IDiceSource.cs          — dice seam: Roll() → (Die1, Die2); driver-side only
  IMatchObserver.cs       — live per-move view of one RunMatchAsync run (transcript stream + lifecycle)
  IPlayAgent.cs           — async Play decision interface
  IProblemSetSource.cs    — re-iterable IAsyncEnumerable<BgDecisionData>
  MatchParticipant.cs     — one entrant: IPlayAgent + ICubeAgent bundle
  MatchResult.cs          — winner seat (nullable) + seat scores + game records
  MatchRunner.cs          — unattended match loop over two participants
  MatchSeat.cs            — enum One/Two + Other() extension; seat-keyed results
  MatchSnapshot.cs        — immutable record
  MatchState.cs           — mutable: match length, scores, Crawford
  ProblemStats.cs         — immutable per-problem lifetime record: ProblemKey + ScoreSegment tally + last-quizzed
  ProblemStatsDocument.cs — immutable ProblemKey-keyed collection of ProblemStats; the versioned quiz-stats document
  ProblemStatsDocumentJsonConverter.cs — bundled converter (public): pinned v3 wire format, fail-loud reads, retired-version signal
  RetiredStatsSchemaException.cs — the deliberate retired-version signal (a JsonException subtype), carrying that version
  RecordedDiceSource.cs   — replays a fixed roll sequence; throws when exhausted
  SeededDiceSource.cs     — Random(seed)-backed reproducible rolls
  VerifiableDiceSource.cs — key-derived audit-grade rolls (public HMAC-SHA256 stream + rejection sampling)
  DiceKey.cs              — immutable 256-bit key: Generate() + Commit(context)
  DiceCommitment.cs       — immutable SHA-256 commitment: Verifies(key, context)
  QuizCategoryKind.cs     — enum: which lifetime-stats predicate a quiz category applies
  QuizCategory.cs         — kind + validated-parameter DTO; internal BuildPredicate() is the one kind→behavior switch
  QuizCategoryPredicates.cs — internal behavior half: IQuizCategoryPredicate + per-kind predicate types
  QuizMix.cs              — immutable stats-weighted composition config: ordered entries + optional length + random toggle
  QuizMixEntry.cs         — one mix line: category + percent (1–100, validated in its own ctor)
  QuizMixJsonConverter.cs — bundled converter (public): pinned versioned wire format, fail-loud reads
  QuizScore.cs            — immutable cumulative score: play / double / take segments + derived total
  Referee.cs              — skeletal: end-of-game, ApplyCubeResponse
  ScoreSegment.cs         — immutable per-category running tally (submitted / correct / equity loss)
  ShuffledProblemSetSource.cs — IProblemSetSource decorator: Fisher-Yates; re-shuffles per enumeration
  SubmittedCubeAction.cs  — record: user cube decision + per-half (doubler/taker) results
  SubmittedPlay.cs        — record: user play + matched candidate + equity loss
  Transcript.cs           — append-only ordered list of TranscriptEntry
  TranscriptEntry.cs      — abstract record + Play / Cube / GameEnded subtypes
BgGame_Lib.Tests/
  BgGame_Lib.Tests.csproj
  CubeAgentContractTests.cs
  DiceSourceTests.cs
  DiceKeyTests.cs             — key generation / validation / commitment
  DiceCommitmentTests.cs      — commitment round-trip / reveal verification
  VerifiableDiceSourceTests.cs — vector pinning / determinism / rejection sampling
  VerifiableDiceVectors.cs    — loads spec/verifiable-dice-vectors.json (consumed in place)
  GameStateTests.cs
  MatchRunnerTests.cs
  MatchStateTests.cs
  TestAgents.cs           — in-proc baseline bots (random/first/delegate agents)
  PlayAgentContractTests.cs
  ProblemSetSourceContractTests.cs
  CooperativeYielderTests.cs
  ShuffledProblemSetSourceTests.cs
  DistinctPositionProblemSetSourceTests.cs
  ProblemStatsTests.cs
  ProblemStatsDocumentTests.cs
  ProblemStatsDocumentSerializationTests.cs
  QuizCategoryTests.cs
  QuizMixTests.cs
  QuizMixSerializationTests.cs
  MixedProblemSetSourceTests.cs
  QuizScoreTests.cs
  ScoreSegmentTests.cs
  AnswerTypeDistributionTests.cs
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

There is no public *in-place* flip surface on `GameState`, `MatchState`, or
`BoardState`. The board-side `Flip()` is private inside `BoardState`
(BgDataTypes_Lib); `MatchState.SwapPerspective` is internal and reached from
tests via `InternalsVisibleTo("BgGame_Lib.Tests")`. External consumers
reasoning in the on-roll perspective never need to flip directly —
`GameState.ApplyPlay` is the only path that crosses a turn boundary, and
half-flipped intermediate states are unreachable.

Querying is different from advancing: `GameState.OpponentView()` returns a
**detached** copy of the position re-expressed from the current opponent's
frame (board via `BoardState.FlippedCopy()`, scores swapped into a fresh
`MatchState`, cube owner mirrored, cube size / match length / Crawford
preserved). It is the query-time sibling of `ApplyPlay`'s turn-time flip —
the two share one cube-owner mirror rule — and exists so the non-on-roll
player can be queried in its own frame (the `ICubeAgent.ChooseResponseAsync`
contract) without touching the live state. It is a view, not a fork: do not
play on from it.

### Mutability decisions

| Type | Mutability | Rationale |
|---|---|---|
| `MatchState`, `GameState`, `Transcript` | Mutable, append-only or method-driven transitions | Game and match progression naturally mutate; matches BgMoveGen's apply/undo idiom. |
| `MatchSnapshot`, `GameSnapshot`, `TranscriptEntry` and subtypes, `GameResult`, `SubmittedPlay`, `SubmittedCubeAction`, `ScoreSegment`, `QuizScore` | Immutable records, init-only / by-value | Transcript-friendly; safe to share across threads and across history. |

`Snapshot()` methods on `MatchState` and `GameState` produce immutable
records. `GameSnapshot.Board` is a defensive copy of `BoardState.Points`,
so subsequent mutations of the live board do not retroactively change a
snapshot already captured.

### Substrate-enforced cube legality

Consumers must not need match context to avoid an illegal double — the
state itself refuses (encapsulation principle). Two distinct predicates,
deliberately not overloaded onto one flag:

- **Cube availability** — `MatchState.HasCube`: false only for a 1-point
  match. With a single point at stake there is nothing to double for; the
  cube never enters play, so no Crawford game exists either. (Some external
  match-record conventions label 1-point-match games "Crawford"; this
  library deliberately does not — Crawford is a mid-match suspension of a
  *live* cube. `FromScores` fails fast on `matchLength: 1, isCrawford:
  true`; callers importing such records normalize first.)
- **Crawford suspension** — `MatchState.IsCrawford`: event-triggered, only
  ever set by the `AwardGame` transition (a side reaching MatchLength − 1
  in a match of length ≥ 2), cleared when the Crawford game is played.

`GameState.CanDouble` composes them with ownership: has-cube ∧ not-Crawford
∧ cube centered-or-on-roll-owned. `DoubleCube` enforces the same rule
(throws `InvalidOperationException` with a reason-specific message);
`CanDouble` is the queryable form so drivers gate the pre-roll cube window
without try/catch. The rule and its messages are single-sourced in a
private `DoubleRefusalReason` helper on `GameState`. Turn-sequencing
legality (no double before a game's opening roll) is a driver concern, not
encoded in the state.

### Dice abstraction

The library contains no ambient randomness — all dice enter through the
`IDiceSource` seam, and rolling is strictly a driver/server-side concern:
agents never roll and never see the source (server-authoritative dice for
the tournament arcs). `Roll()` returns a named tuple `(int Die1, int Die2)`
matching the codebase-wide two-int dice convention rather than introducing
a dice struct.

Three implementations ship here:

- `SeededDiceSource` — `Random(seed)`-backed; the same seed yields the same
  sequence within a .NET runtime version (seeded `Random` uses the
  framework's compat algorithm — stable in practice, not contractually
  guaranteed across major versions). Sufficient for deterministic tests and
  replayable in-proc matches. An explicit seed means the caller already knows
  the sequence, so it stays as-is — committing to it would be theater;
  audit-grade dice are `VerifiableDiceSource`'s job.
- `RecordedDiceSource` — replays a fixed sequence (deterministic tests now;
  duplicate-dice tournament pairings later). Validates eagerly at
  construction, copies its input, and throws `InvalidOperationException`
  when exhausted rather than wrapping around — recycled dice would corrupt
  the determinism the source exists for.
- `VerifiableDiceSource` — audit-grade dice for commit-and-reveal fairness
  (BgTournament Arc 6). Its whole roll sequence is fixed by a secret 256-bit
  `DiceKey`; the server commits to the key before roll one (publishing
  `key.Commit(matchId)`) and reveals the key at match end, so either player
  re-derives every roll and confirms the dice were fixed in advance and never
  adapted. The derivation is deliberately **public and language-neutral**
  (Kerckhoffs — verification requires third parties to re-implement it), so it
  is specified precisely rather than optimized:
  - **Keystream** = `HMAC-SHA256(key, BE64(blockIndex))` for
    `blockIndex = 0, 1, 2, …`, 32-byte blocks concatenated.
  - **Die** = read the next keystream byte `b`; if `b ≥ 252` reject and read
    the next, else `(b % 6) + 1`. 252 is the largest multiple of 6 ≤ 256, so
    0–251 map uniformly onto six faces (42 each) and 252–255 are discarded —
    removing the modulo bias a naïve `(b % 6)` over all 256 values would add.
  - **Roll** = the next two accepted dice as `(Die1, Die2)`; rejected bytes are
    consumed but never surface, and the stream stays aligned across them.
  Unpredictability lives solely in the key: observing rolls yields no
  predictive power without breaking HMAC-SHA256. The keystream is effectively
  unbounded (2^64 blocks); the block counter is a checked increment so the
  astronomically-unreachable wrap fails fast rather than silently repeating the
  stream.

`DiceKey` and `DiceCommitment` are immutable 32-byte value wrappers (hex
round-trip, by-value equality). They are deliberately **distinct types** even
though both are 32 bytes: a key must stay secret until reveal and a commitment
is safe to publish, so the type system refuses to interchange them (publishing a
key where a commitment is expected would reveal the dice early). `DiceKey.Commit`
is a pure `key → context → DiceCommitment` function single-sourcing the
`SHA-256(key ‖ UTF-8(context))` rule; `DiceCommitment.Verifies(key, context)`
single-sources the reveal check (recompute-and-compare, fixed-time). The
committed `spec/verifiable-dice-vectors.json` (independent Python reference,
cross-checked against openssl) pins commitment + first-block + roll stream for
several keys — including keys whose stream exercises the rejection branch — and
is the cross-language contract session 2's PROTOCOL.md references.

None is thread-safe; use one instance per driver.

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
- **One frame rule.** The queried player always sees its own frame: every
  agent method receives a `GameState` in which the deciding agent is the
  on-roll-labeled player. Drivers querying the non-on-roll player (the cube
  response) pass a detached `GameState.OpponentView()`; agents never reason
  about "which side am I this call". This is the epic-wide perspective
  unification — the same rule the wire protocol (Arc 2) speaks.

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

The driver loop lives on `MatchRunner` (see "Match runner"), and
legal-to-offer validation is substrate-enforced on `GameState` (see
"Substrate-enforced cube legality") — neither belongs to the Referee. Still
out of scope (later arcs): time controls and money-session automatic-doubling
rules. The umbrella INSTRUCTIONS.md tracks which features arrive with which
mode.

### Match runner

`MatchRunner` is the unattended match loop for bot-vs-bot play (BgTournament
epic Arc 1): it drives two `MatchParticipant`s from `MatchState.NewMatch` to
completion. Nothing in it is transport-specific — a tournament server (Arc 2)
adapts remote engines onto `IPlayAgent`/`ICubeAgent` and hands them to this
same loop.

**Division of labor.** The runner owns turn *sequencing* — opening roll,
pre-roll cube window, rolling (via its `IDiceSource`; agents never roll),
game/match transitions — and delegates all rule *legality* to where it
already lives: play validation in `GameState.ApplyPlay`, cube legality in
`GameState.CanDouble`/`DoubleCube`, game end and cube responses in `Referee`.

**Seats.** All substrate state is on-roll-relative, so the runner maintains
the one fact the substrate deliberately doesn't hold: which `MatchSeat`
currently owns the on-roll perspective (flipped on every `ApplyPlay`,
realigned at each opening roll via the internal `MatchState.SwapPerspective` —
one reason the runner lives in this library). Results (`MatchResult`,
`GameRecord`) are seat-keyed, never participant-keyed, so mirror matches
(same agent instances on both seats) stay unambiguous.

**Game protocol.** Opening roll: `Die1` is seat One's die, `Die2` seat Two's;
ties re-roll (consumed from the source, not transcripted); the winner plays
the winning pair with no cube window before it. Each later turn: cube window
gated on `CanDouble` (never opens in Crawford or a 1-point match), then roll
and play. On a double offer the responder is queried with a detached
`GameState.OpponentView()` — the responder sees its own frame — while the
live `GameState` stays in the offerer's frame and is what
`Referee.ApplyCubeResponse` applies the response to. Dance turns (sole legal play is the empty play) are applied
automatically without querying the agent — there is no decision to make, and
agents are stateless by contract — but are transcripted as normal play
entries with an empty play.

**Termination.** Match play runs to `MatchState.IsMatchOver`; an optional
`maxGames` caps the run (required for money sessions, which have no match
end). `MatchResult.Winner` is null when the run ended without a match winner.

**Observation.** An optional per-match `IMatchObserver` (a `RunMatchAsync`
parameter, not runner state) receives the run live. Its vocabulary is the
transcript itself: `OnEntryRecorded` delivers the same `TranscriptEntry`
instances the transcript records — single source of truth, no parallel
event family — through the loop's single append path (`Record`), so the
observed stream and the recorded transcript cannot diverge by construction.
Three lifecycle events bracket it: `OnGameStarted(context)` (before the opening
roll — no board, since the frame is indeterminate until the opening roll and the
first play entry carries the starting position; the frame-free facts that *are*
settled — the seats' seat-absolute entering scores and the Crawford flag — ride
in a `GameStartContext` so a live consumer folds them without re-deriving from
completed games), `OnGameEnded(n, record)`
(after the game-end entry and `AwardGame`), `OnMatchEnded(result)` (the
returned instance, only on a completed run). Callbacks are synchronous,
serialized, and fail-fast: an observer exception propagates out of
`RunMatchAsync` unwrapped — a spectator adapter that must not disrupt the
match enqueues to a channel and is non-throwing by construction. On abort
(agent violation, cancellation) the stream simply stops — no terminal
callback (the observer's owner is the caller that catches the throw) — and
everything up to the failure point has already been delivered, so a forfeit
no longer discards completed games' transcripts.

**Failure semantics.** An out-of-contract agent value (illegal play,
wrong-half cube action) aborts the run with
`AgentContractViolationException` carrying the offending seat, a
`AgentContractViolationKind`, the offending value, and any inner substrate
exception. The runner is deliberately policy-free: converting a violation
into a forfeit is tournament-layer logic (Arc 2's server catches and
translates). Cancellation propagates as `OperationCanceledException` (token
passed to every agent call, checked at turn/game boundaries). Substrate
state is never left half-mutated (throw-before-mutate holds throughout).

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

**Seat identity.** Every entry is stamped at append time with
`OnRollSeat` — the seat whose perspective its snapshot is expressed in (the
frame seat, not "the actor"). This is the one fact that makes every
perspective-relative snapshot field absolutely interpretable; frame
conventions themselves are unchanged (snapshots stay on-roll-frame; both
cube entries stay offerer-frame — the stamp does not flip between an offer
and its response). Attribution rules are *derived* on the subtypes that own
them, single-sourcing what consumers previously re-derived by walking
runner sequencing conventions: a play's mover is its `OnRollSeat`;
`CubeTranscriptEntry.ActingSeat` resolves offer-side actions to the on-roll
offerer and response-side actions (`Take`/`Pass`) to the other seat;
`GameEndedTranscriptEntry.Winner` resolves `Result.OnRollWon` against the
frame seat (and is what the runner uses to construct `GameRecord.Winner` —
one home for win attribution). The frame-seat semantic was chosen over
stamping the actor because it is uniform across all subtypes (a game-end
entry has no actor) and because board-rendering consumers need the frame
for every entry; each actor is a pure derivation from it. Derived
properties do not participate in record equality.

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

`ShuffledProblemSetSource` is the one concrete `IProblemSetSource` shipping
here — a **decorator**, not a leaf source. It wraps any inner source and
presents that source's decisions in a randomized (Fisher-Yates) order,
composing over server-disk, uploaded-file, or future bundle sources because
it speaks only the interface. The consumer wires it in: BgQuiz_Blazor's
`Program.cs` wraps the picked source only when the user toggles shuffle on.

- **Materialization.** Shuffling needs the whole item set up front, so each
  `EnumerateAsync` drains the inner source into a list before yielding
  anything. For a lazily streamed, uncounted inner source this trades that
  source's streaming/lazy-count behavior for one full materialization per
  enumeration — a deliberate cost of shuffling, not a free wrapper.
- **Name / Count passthrough.** Order is the only thing shuffling changes;
  cardinality is untouched and the decorator knows no more about the inner
  size than the inner does. Both properties pass through unchanged (`Count`
  stays null when the inner source streams).
- **Re-shuffle per enumeration.** The decorator caches no shuffled list. Each
  `EnumerateAsync` re-materializes from the inner source's current state
  (itself required to be re-iterable) and re-shuffles using this instance's
  shared `Random`, so successive calls — a quiz controller's Restart — draw a
  *new* permutation rather than replaying the last one. That matches what
  "restart a shuffled quiz" means to a user, and keeps the decorator stateless
  beyond the RNG.
- **Two constructors.** The unseeded ctor seeds `Random` non-deterministically
  — production use where reproducibility isn't wanted (the path `Program.cs`
  takes). The seeded ctor fixes the shuffle sequence for deterministic tests
  and replayable sessions; reproducibility scope mirrors `SeededDiceSource`
  (stable within a .NET runtime version via the framework's compatibility
  `Random`, not an audit-grade guarantee). Not thread-safe — `Random` isn't,
  and the wrapper adds no synchronization.

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

### Per-problem lifetime stats

`ProblemStats` / `ProblemStatsDocument` are the storage-agnostic model
behind BgQuiz's persistent per-problem stats file (a versioned JSON document
kept beside the quizzed `.xg`/`.xgp` corpus). The library does no I/O — the
consumer loads bytes, deserializes, folds submissions in, and serializes back.
Merge/concurrency machinery is deliberately absent: single-user,
single-writer.

**Content-keyed (SPEC-stats-identity.md; halheinrich/backgammon#95).** The
key is `ProblemKey` — the content identity derived from the decision's facts
— not the file-relative `DecisionId`, so every content-equal copy of a
problem across files and matches folds into and reads one lifetime record.
Submissions carry a `ProblemKey?` stamped at construction (the consumer
derives it once via `ProblemKey.TryDerive`); a null key is the ruled
**no-key rung**: the submission still scores the session (`QuizScore` never
reads the key) but is not recorded — `ProblemStatsDocument.Plus` returns the
document unchanged, and that document-level skip is the *single* no-key
filter (degrade, never block).

**`ProblemStats`** is one problem's lifetime record: its `ProblemKey`, a
composed `ScoreSegment` tally, and the last-quizzed date. Composing
`ScoreSegment` (rather than local correct/wrong fields) keeps the library's
single accumulation primitive single-sourced; the wrong count is the derived
`Wrong` (`Submitted − Correct`), never stored. The fold rule **agrees with
`QuizScore`** on how many decisions a cube position is worth: two — the
doubler half and the taker half — so a cube submission adds 2 submitted and
one correct per half that was right, with both halves' equity losses
accumulated. One quizzed position still produces exactly one lifetime record
keyed by its `ProblemKey`; only the counting granularity inside that record
is per-half. Folding a submission whose key is null or differs from the
record's throws `ArgumentException` (wrong-key folds are data corruption) —
and since a play key and a cube key are distinct by grammar (the dice
discriminant), **kind purity is structural**: a record only ever folds one
decision kind, which keeps per-kind tally derivations (sightings' cube
divide-by-two) exact. The static `From` factories are defined as an empty
seed plus `Plus`, so the fold rule has one definition. Skips and off-list
plays never reach a fold — the consumer doesn't call it for them.

**`ProblemStatsDocument`** is the immutable document: a key-keyed collection
of `ProblemStats` whose `Plus` overloads return a new document with the
submission folded into its problem's record (created on first sight). It is
a **class, not a record** — it wraps an `ImmutableDictionary`, where record
equality would silently be reference equality; instances compare by
reference. The clock enters here and only here: document folds take a
`TimeProvider` and resolve `GetUtcNow()` themselves, so the model never reads
ambient time and folding stays deterministic under a fake provider. The
asymmetry with `ProblemStats.Plus` (which takes the already-resolved
`DateTimeOffset`) is deliberate: the document is the consumer's entry point,
so holding the seam there makes ambient-time misuse impossible at the type
level, while the record-level fold stays a pure value computation.

**Wire format (schema v4 — the halheinrich/backgammon#95 clean break,
re-broken for halheinrich/backgammon#120's money keys, re-broken again
for halheinrich/backgammon#86's answer kinds).** JSON via the bundled internal
`ProblemStatsDocumentJsonConverter` (type-level `[JsonConverter]`, same
pattern as BgDataTypes_Lib's `DecisionIdJsonConverter` — consumers register
nothing): a `schemaVersion` field (`CurrentSchemaVersion`, currently 4)
followed by a `problems` **object** keyed by canonical `ProblemKey` strings,
each value an object holding **exactly one answer-kind record** —
`"checkerPlay"` or `"cubePair"` (SPEC-scoring.md §4's kinds; the
equity-guess kind is reserved there, not in this grammar) — whose body is
the nested tally object plus the ISO 8601 last-quizzed date. The kind is
derivable from the key's own grammar today (dice ride on play keys and only
there), so the writer derives the token from `ProblemKey.IsCubeDecision`
and the reader rejects a record whose token disagrees with its key; the
token is carried anyway because the seam exists for the future where it
stops being derivable — halheinrich/backgammon#62's equity-guess records
arrive as sibling kind entries under the same key, extending this grammar
rather than re-keying the document (SPEC-stats-identity.md §3, 2026-08-26
amendment). The
key grammar itself is BgDataTypes_Lib's — see `ProblemKey`'s type docs for
the single authoritative statement of it, including the money-only Jacoby
suffix that v3 introduced; never restate it here. The map shape is what
`ProblemKeyJsonConverter`'s property-name support exists for (v1's
array-of-elements existed only because `DecisionId` lacked it). The
converter hand-writes the whole tree with fixed property names, ordered by
canonical key (ordinal), so the persisted format cannot vary with the
consumer's `JsonSerializerOptions`. `schemaVersion` is **contractually the
first property** — the version gates how the rest is parsed; every file any
of this library's writers ever produced satisfies this, and anything else
reads as corrupt/foreign.

Reads are fail-loud with one deliberate signal, and the signal covers
**every recognised version below the current one** — the rule is the range
`[1, CurrentSchemaVersion)`, deliberately not a list, so a version bump
retires its predecessor with no second edit. A genuine retired document
(its version plus a shallow shape check — exactly one further property
holding that version's body container, `decisions` array for v1 and
`problems` object for v2 onward, whose contents are skipped and never
parsed; no migration exists) throws `RetiredStatsSchemaException`, a
`JsonException` subtype carrying **its own** version, so the consumer can
retire the file honestly under a per-version name (rename aside, seed fresh,
notice) instead of surfacing a generic load error. v1 is the `DecisionId`-keyed
format; v2 is the `ProblemKey`-keyed format from before Jacoby entered money
keys — its match keys are spelled exactly as later versions spell them, but
selective carry-forward was weighed and rejected (SPEC-stats-identity.md §3),
so a v2 file is set aside whole; v3 is the `ProblemKey`-keyed format from
before answer kinds entered the per-problem records — retired both for the
shape and because its cube tallies accrued doubler-half correctness under
action-vs-action scoring, which halheinrich/backgammon#86's claim-vs-claim
model makes incomparable with what folds after (blending the regimes in one
lifetime tally would be quietly wrong; losing the counts is ruled
acceptable, SPEC-scoring.md §4). Everything else — newer or unrecognised
schema versions (distinguished "newer than this library supports" message),
unknown or duplicate properties, missing required properties, invalid or
duplicate or non-canonically-spelled keys (a v2 money key is now one of
these), malformed dates, impossible tallies, and every violation of the
answer-kind layer (no kind record, a second one, an unknown or reserved
token, a token disagreeing with its key's grammar) — throws plain
`JsonException`; a schema-version bump is the format's only evolution
mechanism.

### Stats-weighted quiz categories

`QuizCategoryKind` / `QuizCategory` are the selectable decision categories
behind stats-weighted quiz composition ("present the decisions I get wrong
more often"): each category is a predicate over a decision's lifetime stats —
never-seen, got-wrong, seen-fewer-than-n, not-seen-in-days-n,
avg-equity-loss-over-x, wrong-rate-over-r — plus the residual EverythingElse.
`QuizMix` (below) selects and weights them; `MixedProblemSetSource` (below)
composes quizzes from them (BgQuiz Phase 2+ selection).

- **Data/behavior split (the `FilterConfig.Build` pattern).** `QuizCategory`
  is the serializable, value-equal DTO: a `QuizCategoryKind` plus one
  `double?` parameter slot, constructible only through validating factories
  (parameterless kinds are cached singletons; parameterized kinds fail fast on
  out-of-bound values, so an invalid kind/parameter combination is
  unrepresentable from outside; integer parameters store exactly). The
  matching behavior lives in internal predicate types
  (`IQuizCategoryPredicate` + one sealed class per kind), materialized by the
  internal `QuizCategory.BuildPredicate()` — a single switch that is the one
  home of the kind→behavior mapping. Adding a category is additive: one enum
  member, one factory, one switch arm.
- **The predicate context is the (decision, stats) pair** — the live
  `BgDecisionData` alongside its problem's `ProblemStats` (null = never
  quizzed; the by-key lookup is the composing decorator's job) — because
  sightings are derived per decision kind: a checker play submits one
  decision per sighting, a cube position two (the two-half fold), so cube
  sightings are `Tally.Submitted / 2` — always exact, since one key only ever
  folds one kind (kind purity is structural — see the stats section). No
  stats-schema change; "seen fewer than n" means sightings, not raw
  submissions.
- **Per-half measures need no cube adjustment.** `AvgEquityLossOver` compares
  `Tally.AverageEquityLoss`, whose numerator (both halves' losses) and
  denominator (2 per sighting) both count per-half — `Total/Submitted` is
  already the per-half average, comparable across decision kinds under one
  threshold. `WrongRateOver` likewise compares the per-half wrong fraction
  (`1 − Accuracy`, in [0, 1]): a half-right cube reads 0.5, consistent with
  the ratified 1-of-2 fold. Thresholds are fractions — percentage rendering
  stays a display concern, per the `ScoreSegment.Accuracy` convention.
- **EverythingElse is residual, not a predicate.** It matches exactly the
  decisions matched by no other entry selected in the same mix — contextual
  by definition. `BuildPredicate()` throws for it; composers gate on
  `QuizCategory.IsResidual` and compute the residual pool themselves. The
  type-level split keeps a throwing `Matches` out of the predicate contract.
- **Time enters as a parameter.** `NotSeenInDays` compares against an `asOf`
  timestamp passed into `Matches` — resolved once per classification pass by
  the caller (the composing decorator holds the `TimeProvider` seam), so the
  predicates stay pure and deterministic under test. Never-seen counts as
  not-seen (the literal reading; overlap with NeverSeen is safe because
  composition presents each decision at most once).

### Stats-weighted quiz mix config

`QuizMix` / `QuizMixEntry` are the composition config the composing decorator
reads: an **ordered** list of (category, percent) lines, an optional quiz
length, and the random toggle (default true). Immutable; both are value-equal:
`QuizMixEntry` is a record, and `QuizMix` is a class with **hand-written value
equality over the full member surface** — `Entries` as an ordered sequence
plus `QuizLength` and `RandomOrder` (a record would compare the wrapped list
by reference and report identical configs unequal, so the class hand-rolls
`IEquatable<QuizMix>`, `==`/`!=`, and a consistent hash).

- **Validation split.** The per-entry rule (percent 1–100) lives in
  `QuizMixEntry`'s own constructor — the earliest construction point, so an
  out-of-range entry never exists un-failed. `QuizMix` owns only the
  set-level rules: non-empty entries sum to exactly 100; no duplicate
  categories (same kind *and* parameter — the same kind with different
  parameters is two distinct categories and allowed); length null or ≥ 1;
  length-without-entries rejected. All fail fast.
- **Blank = passthrough, and nothing else is blank-ish.** `QuizMix.Empty`
  (no entries, no length) makes the composition layer fully inert. A length
  without entries is rejected rather than treated as an implicit
  100%-everything mix — "cap at N without weighting" is expressed losslessly
  as a single `EverythingElse` entry at 100 plus a length.
- **Entry order is contractual.** Under overlapping categories the composer
  draws entries in declared order, so contested decisions go to the earlier
  entry. The wire format preserves order; consumers rendering or persisting
  entry lists must too.
- **Wire format.** Versioned JSON via the bundled internal
  `QuizMixJsonConverter` (type-level `[JsonConverter]`, hand-written fixed
  property names, `schemaVersion` currently 1) — the
  `ProblemStatsDocumentJsonConverter` posture: all properties always written
  (`quizLength` as JSON `null` when unset) and always required; reads
  fail-loud on any other schema version (distinguished "newer" message),
  unknown or missing properties at any level, invalid kinds (exact
  declaration-name match — numeric strings and case variants rejected),
  kind/value pairing violations (value forbidden on parameterless kinds,
  required on parameterized ones, integral for integer kinds), and any
  construction-rule violation — category reconstruction routes through the
  internal `QuizCategory.Create` seam and the mix through the public
  constructor, so wire-level bounds have the same single definition as
  in-memory ones. Persistence is the `FilterConfig` trio: `ToJson` /
  `FromJson` (fail loud) / `TryFromJson` (absent or corrupt restores to
  `Empty` + `false` — the localStorage path).

### Stats-weighted composing source

`MixedProblemSetSource` is the third `IProblemSetSource` decorator-family
member (alongside `ShuffledProblemSetSource`): it composes each enumeration's
quiz from an inner source by classifying decisions against a `QuizMix`'s
categories and drawing per-entry pools to the percentage composition.
**Composition, not ordering** — the mix decides *which* decisions form the
quiz. Constructor: (inner, stats-provider, mix, clock) plus a seeded overload
for deterministic tests (the shuffled-source pattern).

- **Passthrough.** A blank mix makes the layer fully inert: the inner source
  streams through unchanged (no materialization, no RNG consumption, no
  dedupe), `Count` passes through, `LastComposition` is null. With an active
  mix, `Count` is null — a composition's size depends on the stats document
  and clock at enumeration time — and `Name` still passes through.
- **Per-enumeration pipeline.** Resolve the stats document (provider seam)
  and one `GetUtcNow()` timestamp (every predicate sees the same "now");
  materialize the inner source, deduping by `DecisionId` (first occurrence
  wins — a quiz presents each decision at most once; deliberately *record*
  identity, not the content key — collapsing content-equal copies is
  `DistinctPositionProblemSetSource`'s job and the consumer's wiring choice);
  classify into per-entry pools in source order, looking each decision's
  lifetime record up **by its derived `ProblemKey`** (SPEC-stats-identity.md
  §4: the classifier judges the problem's full record wherever the copy came
  from; an underivable key means no record can exist, so the decision
  classifies as never-seen) — the residual pool collects decisions matched
  by no non-residual entry, reachable only when an EverythingElse entry is
  selected (otherwise unmatched decisions are simply not drawn); target =
  `QuizLength ?? union of selected pools` (deduped); apportion the target by
  percent via largest remainder (floor, leftover to largest fractional
  remainders, ties to the earlier entry); draw in declared entry order with
  the global once-per-quiz dedupe; redistribute shortfall proportionally (by
  percent, largest remainder again) across entries that still have supply —
  terminates because the draw goal is capped at the union.
- **The random toggle.** `RandomOrder` on: uniform draws without replacement,
  then a Fisher-Yates shuffle of the drawn set for presentation. Off: fully
  deterministic — pools draw in source order, presentation is source order,
  and the RNG is never consumed (even an unseeded instance is exactly
  reproducible).
- **Provider seam (Restart semantics).** The stats input is a
  current-document getter, resolved fresh per enumeration: the document is
  immutable and replaced fold-by-fold, so a quiz controller's Restart
  re-composes against the lifetime record as it stands, including decisions
  answered this session. A null return is a consumer wiring bug and throws
  `InvalidOperationException` — the no-stats case is "don't wire the
  decorator", and silently composing as all-never-seen would mask the bug as
  a weird quiz. An *empty document* is the legitimate everything-never-seen
  input and needs no special-casing.
- **Telemetry.** `LastComposition` (a `MixComposition`) carries the most
  recent enumeration's requested-vs-actual: overall `TargetCount` /
  `HasRequestedLength` / `DrawnCount` plus per-entry (category, percent, pool
  size, requested, drawn) in declared order. Assigned before the first yield,
  so the consumer can render an honest shortfall notice at quiz start.
  `DrawnCount < TargetCount` means the requested length exceeded reachable
  supply; per-entry `Drawn < Requested` means that entry's pool ran dry and
  its share was redistributed. `HasRequestedLength` says which source
  `TargetCount` came from — the mix's length (capped) or the reachable union
  (capless) — because the counts cannot: a capless target *is* the union
  count, and a capped mix may ask for exactly that number, so the two are
  indistinguishable in the case a notice most needs to get right. The split is
  the producer's to state, and `LastComposition` is the whole answer about one
  enumeration (halheinrich/backgammon#12).

### Position-distinct source (content dedupe)

`DistinctPositionProblemSetSource` is the fourth `IProblemSetSource`
decorator-family member: it presents each distinct problem exactly once,
wherever the copies came from. The dedupe key is the derived `ProblemKey`
(SPEC-stats-identity.md §4; halheinrich/backgammon#95) — content identity,
not the file-relative `DecisionId` and **not the raw XGID string** (the
pre-#95 key, now display/provenance only, an identity nowhere). The key
collapses strictly more than XGID did: same-away positions from different
match lengths unify, mirror-turn duplicates unify — the ratified consequence.

- **First occurrence survives, unconditionally.** With stats content-keyed,
  every copy folds into and reads the same lifetime record, so survivor
  choice no longer affects stats at all; the surviving copy matters only for
  display/provenance. The former survivor-preference constructor/predicate —
  a seam that existed solely to keep id-keyed stats reachable — is deleted,
  not bypassed.
- **Fail-open on the no-key rung.** Items with no derivable key pass through
  unmerged, even when several are otherwise identical: an underivable key
  carries no content claim, and treating absence as equality would collapse
  unrelated problems.
- **Duplicate-class telemetry.** `LastDuplicateClasses` exposes the most
  recent enumeration's content-equivalence classes (one
  `DuplicatePositionClass` — key + member `DecisionId`s, survivor first — per
  key with ≥ 2 copies; singletons never appear), assigned before the first
  yield. The ruled producer-side seam for a future "which files hold the same
  position" report (#104 related work); no report UI exists anywhere yet.
- **Contract plumbing.** `Name` passes through; `Count` is always null (how
  many problems collapse is unknowable before enumeration); each enumeration
  re-drains the inner source and recomputes survivors and classes fresh.
  Materializes per enumeration (the shuffled tradeoff — first-occurrence
  placement plus the class inventory need the whole set). Wires closest to
  the raw source, beneath shuffle/mix, by consumer convention. Stateful
  telemetry ⇒ not thread-safe (the `MixedProblemSetSource` caveat class).

### Cooperative yielding on the enumeration hot path

The materializing decorators (shuffled / mixed / distinct) drain their inner
source and then present a large drawn set. `CooperativeYielder` single-sources
how those presentation loops yield to the scheduler so a Blazor WASM Start
stays responsive without paying a per-item cost.

- **The problem.** An `await Task.Yield()` after *every* presented item pays one
  event-loop round-trip per item — tens of thousands for a large corpus — which
  dominated a shuffled/mixed quiz Start (~7.5 s for ~25k decisions) without
  letting the browser do anything useful between items.
- **Time-budgeted, not per-item.** `YieldIfDueAsync()` yields only once a budget
  (`DefaultBudget`, ~50 ms) has elapsed since the last yield, then resets the
  window; within the budget it returns `ValueTask.CompletedTask` — synchronous,
  no allocation — so a caller can `await` it every iteration cheaply. Often
  enough that the browser repaints a busy cursor during a long materialization,
  rare enough that the yields are not themselves the bottleneck.
- **Class, owning the await.** The gate owns the `await Task.Yield()` itself, so
  the yield *mechanism* — not merely the "is it due?" decision — is
  single-sourced across callers. An `async` method on a struct mutates a copy of
  `this` and would lose the budget-reset across the await, so owning the await
  requires a reference type.
- **Clock seam.** Timing runs through a `TimeProvider`'s monotonic
  `GetTimestamp()` / `GetElapsedTime()` (the testable `Stopwatch` analog), so the
  policy is deterministically unit-tested with a fake clock; production passes
  `TimeProvider.System`. This pacing clock is deliberately kept **separate** from
  `MixedProblemSetSource`'s semantic `_clock` (the `GetUtcNow` classification
  timestamp): the two encode independent decisions, and reusing the semantic
  clock would silently disable yielding whenever a test injects a fixed clock.
  The pacing never affects which items flow or in what order — enumeration
  semantics (items, order, cancellation) are unchanged.

### Answer-type distribution over a decision pool

`AnswerTypeDistribution` counts a pool of decisions by the kind of answer each
one calls for: checker plays, plus one bucket per cube verdict from
SPEC-scoring §3's table (`NoDoubleTake`, `DoubleTake`, `DoublePass`,
`TooGoodPass`, `TooGoodTake` — halheinrich/backgammon#86's claim vocabulary),
with a derived `Total`. It answers a **collection-scoped** question — "what is
my saved corpus actually made of?", the curation-bias check a beta tester
asked for (over-saved takes, no too-good positions at all) — not a
session-scoped one. Immutable, `ScoreSegment`'s shape throughout: `Empty`, a
fold returning a new instance, and `operator +` as the single definition of how
instances combine.

- **The classification consumes the producer verdict.** `Add(DecisionData)`
  branches on `IsCube` first (the cube verdict surface throws on a checker
  play), then keys the bucket by the decision's `BestClaimPair` — the one
  derivation site of the truth claim, beside `BestDoublerAction` /
  `BestTakerAction` — and matches its `(Claim, Taker)` halves. Nothing here
  re-derives a verdict from equities (SPEC-scoring §3, "Answer-type
  classification consumes the claim"), and consumers stream decisions in
  without owning the bucketing rule. The private one-hot `Classify` is what
  makes the fold contract structural rather than merely asserted. The 3×2's
  sixth cell — the incoherent `NoDoublePass` the ruled tie-breaks compose as
  derived truth at the exact `NoDoubleEquity == 1` boundary — is **just too
  good** and houses in `TooGoodPass` (SPEC-scoring §3, ruled 2026-09-01):
  the too-good posture's degenerate point, not a sixth bucket, so the match
  is total over the closed 3×2 and a legal corpus can never fail to fold.
- **The fold contract leg 2 depends on: exactly one bucket per `Add`.** Hence
  `Total` equals the number of decisions folded. That is what lets BgQuiz's
  Home derive its "N decisions matched" count from `Total` instead of running a
  second count-only pass over the same pool — `CountMatchesAsync` enumerates
  that pool anyway, and two computations of "what matches" would drift.
- **A cube decision buckets once**, keyed by its best pair. Deliberately *not*
  the two-half convention of `QuizScore.Plus(SubmittedCubeAction)` /
  `ProblemStats.Plus(SubmittedCubeAction, …)`, where a cube position is two
  scored decisions: those count answers *given*, this counts problems
  *present*, and pool membership counts rows. The two conventions measure
  different things and are not required to reconcile.
- **Not a score.** Nothing here records how the user answered; that axis stays
  on `ScoreSegment` / `QuizScore`. Keeping the two apart is why this record
  needs no submission type and no clock.

### Source generation & trimming

`BgGameJsonContext` (halheinrich/backgammon#129 leg 3) is the public
source-generated `JsonSerializerContext` over this library's wire surface:
trim-safe serializer metadata produced at compile time, byte-identical to the
reflection path (pinned by `BgGameJsonContextTests`), both bundled converters
honored. Its `[JsonSerializable]` roots are this library's two wire units, and
both are document roots — `ProblemStatsDocument` and `QuizMix`.

**The closure is the roots and nothing else,** by construction rather than by
luck: both converters write and read their whole trees by hand against a
`Utf8JsonWriter` / `Utf8JsonReader`, so the serializer is never asked to
resolve a nested type. `ProblemStats`, `ScoreSegment`, `QuizMixEntry` and
`QuizCategory` never reach a resolver, and neither does `ProblemKey` — its
canonical string is written as a raw JSON property name and re-parsed on read.
That is why this context chains no other, BgDataTypes_Lib's included.

**Public, and load-bearing so.** The arc's standing shape is one public
context per producer repo; here the consumer is the party that needs it.
BgQuiz's `QuizStatsStore` names `ProblemStatsDocument` to `JsonSerializer`
directly, so leg 5 resolves it by chaining, most-derived-first:

```csharp
var options = new JsonSerializerOptions
{
    TypeInfoResolver = JsonTypeInfoResolver.Combine(
        TheConsumersOwnContext.Default, BgGameJsonContext.Default)
};
```

`QuizMix` is declared for the opposite reason: no consumer names it, but its
own `ToJson` / `FromJson` / `TryFromJson` trio does — those three are this
library's trim-safe entry points and resolve their metadata here instead of by
runtime reflection.

The arc's two binding rules both land:

1. **Converters named by a type-level `[JsonConverter]` are public.** A
   downstream context that declares the annotated type instantiates the
   converter from its own generated code, so an internal converter fails the
   *consumer's* compile (SYSLIB1220, then SYSLIB1030 declining the type
   outright — measured against both converters here). Both are public, sealed
   and stateless; the public attributes already named them, so nothing became
   contractual that was not already.
2. **`GenerationMode = JsonSourceGenerationMode.Metadata`.** Default-mode
   fast-path serialize handlers bind nested type resolution to the declaring
   context's own private options and bypass the resolver chain.
   BgDataTypes_Lib's chained-consumer test pair demonstrates the failure and
   owns the rule; a test here pins this link's declaration.

No options-level converter registration exists to express, so leg 2's
containment problem — a parameterized registration
`[JsonSourceGenerationOptions]` cannot carry, making a context's own options a
trap — has no instance in this repo.

**Trim posture.** The library declares `IsTrimmable` and runs
`EnableTrimAnalyzer` in its own build, so under `TreatWarningsAsErrors` a
reflection-serialization regression is a build error *here* rather than a
publish-time warning in BgQuiz. Both declarations are pinned by test.

A completeness test keeps the declarations honest, in the
halheinrich/backgammon#144 intersection pattern: the wire surface derived from
the assembly (every type carrying a type-level `[JsonConverter]`) must equal
the roots derived from the context's generated `JsonTypeInfo<T>` properties. A
third wire unit lands in the first set the moment it is written; a stale
declaration fails the other way.

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
    public bool HasCube { get; }               // false only for 1-point matches
    public bool IsMatchOver { get; }
    public static MatchState NewMatch(int matchLength);
    public static MatchState FromScores(int matchLength, int onRollScore, int opponentScore, bool isCrawford);
        // throws on isCrawford with matchLength 0 (money) or 1 (no cube to suspend),
        // and on any score ≥ a nonzero matchLength (a finished match is not a resumable state)
    public void AwardGame(GameResult result);
    public MatchSnapshot Snapshot();
}

public sealed class GameState
{
    public BoardState Board { get; }
    public MatchState Match { get; }
    public int CubeSize { get; }
    public CubeOwner CubeOwner { get; }
    public bool CanDouble { get; }             // has-cube ∧ not-Crawford ∧ centered-or-on-roll-owned
    public static GameState NewGame(MatchState match);
    public static GameState FromPosition(MatchState match, BoardState board, int cubeSize, CubeOwner cubeOwner);
    public void ApplyPlay(Play play, int die1, int die2);   // unified turn-transition primitive
    public void DoubleCube();                  // throws unless CanDouble; checked multiply (OverflowException past 2^30)
    public GameState OpponentView();           // detached query view in the opponent's frame
    public GameSnapshot Snapshot();
}

public sealed record MatchSnapshot(int MatchLength, int OnRollScore, int OpponentScore, bool IsCrawford);
public sealed record GameSnapshot(IReadOnlyList<int> Board, int CubeSize, CubeOwner CubeOwner, MatchSnapshot Match);

// Result types
public enum GameResultKind { WinSingle = 1, WinGammon = 2, WinBackgammon = 3 }
public sealed record GameResult(GameResultKind Kind, bool OnRollWon, int CubeSize) { public int Points { get; } }
// CubeAction (NoDouble | Double | Take | Pass) is defined in BgDataTypes_Lib;
// the signatures below consume it.

// Dice
public interface IDiceSource
{
    (int Die1, int Die2) Roll();   // each die 1–6
}

public sealed class SeededDiceSource : IDiceSource
{
    public SeededDiceSource(int seed);
    public (int Die1, int Die2) Roll();
}

public sealed class RecordedDiceSource : IDiceSource
{
    public RecordedDiceSource(IEnumerable<(int Die1, int Die2)> rolls);   // eager validation; copies input
    public int Remaining { get; }
    public (int Die1, int Die2) Roll();   // InvalidOperationException when exhausted
}

// Verifiable dice (commit-and-reveal fairness) — public, language-neutral algorithm
public sealed class DiceKey : IEquatable<DiceKey>
{
    public const int SizeInBytes = 32;
    public static DiceKey Generate();                    // RandomNumberGenerator
    public static DiceKey FromBytes(ReadOnlySpan<byte> bytes);   // ArgumentException on wrong length
    public static DiceKey FromHex(string hex);           // FormatException/ArgumentException on bad input
    public string ToHex();
    public byte[] ToBytes();                             // defensive copy
    public DiceCommitment Commit(string context);        // SHA-256(key ‖ UTF-8(context))
}

public sealed class DiceCommitment : IEquatable<DiceCommitment>
{
    public const int SizeInBytes = 32;
    public static DiceCommitment FromBytes(ReadOnlySpan<byte> bytes);
    public static DiceCommitment FromHex(string hex);
    public string ToHex();
    public byte[] ToBytes();
    public bool Verifies(DiceKey key, string context);   // recompute-and-compare, fixed-time
}

public sealed class VerifiableDiceSource : IDiceSource
{
    public VerifiableDiceSource(DiceKey key);
    public (int Die1, int Die2) Roll();   // HMAC-SHA256 keystream + byte-rejection sampling
}

// Agents
public interface IPlayAgent
{
    ValueTask<Play> ChoosePlayAsync(GameState state, int die1, int die2, CancellationToken cancellationToken = default);
}

public interface ICubeAgent
{
    ValueTask<CubeAction> ChooseOfferAsync(GameState state, CancellationToken cancellationToken = default);
    ValueTask<CubeAction> ChooseResponseAsync(GameState state, CancellationToken cancellationToken = default);
        // state is in the RESPONDER's frame (drivers pass GameState.OpponentView()) — see Pitfalls
}

// Referee
public sealed class Referee
{
    public GameResult? IsGameOver(GameState state);
    public GameResult? ApplyCubeResponse(GameState state, CubeAction response);
}

// Match runner
public enum MatchSeat { One = 1, Two = 2 }
public static class MatchSeatExtensions { public static MatchSeat Other(this MatchSeat seat); }

public sealed record MatchParticipant
{
    public IPlayAgent PlayAgent { get; }
    public ICubeAgent CubeAgent { get; }
    public MatchParticipant(IPlayAgent playAgent, ICubeAgent cubeAgent);
    public static MatchParticipant From<TAgent>(TAgent agent) where TAgent : IPlayAgent, ICubeAgent;
}

public sealed record GameRecord(MatchSeat Winner, GameResult Result, Transcript Transcript);
public sealed record MatchResult(
    MatchSeat? Winner,               // null: money session or maxGames cap hit first
    int SeatOneScore, int SeatTwoScore,
    IReadOnlyList<GameRecord> Games);

public enum AgentContractViolationKind { IllegalPlay = 1, IllegalCubeOffer = 2, IllegalCubeResponse = 3 }
public sealed class AgentContractViolationException : Exception
{
    public MatchSeat Seat { get; }
    public AgentContractViolationKind Kind { get; }
    public Play? OffendingPlay { get; }
    public CubeAction? OffendingCubeAction { get; }
    // + public ctors and static factories (ForPlay / ForCubeOffer / ForCubeResponse)
}

public sealed class MatchRunner
{
    public MatchRunner(IDiceSource diceSource);
    public Task<MatchResult> RunMatchAsync(
        MatchParticipant seatOne, MatchParticipant seatTwo,
        int matchLength,                       // ≥ 1, or 0 = money session (then maxGames required)
        int? maxGames = null,
        IMatchObserver? observer = null,
        CancellationToken cancellationToken = default);
}

// Live observation (per-match; synchronous; fail-fast — see Pitfalls)
public interface IMatchObserver
{
    void OnGameStarted(GameStartContext context);         // before the opening roll; frame-free game context
    void OnEntryRecorded(TranscriptEntry entry);          // the instance the transcript holds
    void OnGameEnded(int gameNumber, GameRecord record);  // the instance MatchResult.Games holds
    void OnMatchEnded(MatchResult result);                // the returned instance; completed runs only
}

// The frame-free facts settled at game construction (no board — the frame is
// indeterminate until the opening roll). Scores are SEAT-ABSOLUTE, sourced from
// the runner's seat-keyed tallies; IsCrawford is the substrate's own value
// (MatchState.IsCrawford), the same one GameState.NewGame reasons about.
public sealed record GameStartContext(int GameNumber, int SeatOneScore, int SeatTwoScore, bool IsCrawford);

// Transcript — OnRollSeat is the FRAME seat: the seat whose perspective State
// is expressed in (see Architecture / Transcript model / Seat identity)
public abstract record TranscriptEntry(GameSnapshot State, MatchSeat OnRollSeat);
public sealed record PlayTranscriptEntry(GameSnapshot State, MatchSeat OnRollSeat, int Die1, int Die2, Play ChosenPlay) : TranscriptEntry(State, OnRollSeat);
public sealed record CubeTranscriptEntry(GameSnapshot State, MatchSeat OnRollSeat, CubeAction Action) : TranscriptEntry(State, OnRollSeat)
{
    public MatchSeat ActingSeat { get; }   // derived: offer-side → OnRollSeat, Take/Pass → Other()
}
public sealed record GameEndedTranscriptEntry(GameSnapshot State, MatchSeat OnRollSeat, GameResult Result) : TranscriptEntry(State, OnRollSeat)
{
    public MatchSeat Winner { get; }       // derived: Result.OnRollWon resolved against OnRollSeat
}

public sealed class Transcript
{
    public IReadOnlyList<TranscriptEntry> Entries { get; }
    public void Append(TranscriptEntry entry);
}

// Cooperative yielding (time-budgeted; single-sources the decorators' yield policy)
public sealed class CooperativeYielder
{
    public static readonly TimeSpan DefaultBudget;                 // ~50 ms
    public CooperativeYielder(TimeProvider clock);                 // DefaultBudget; prod passes TimeProvider.System
    public CooperativeYielder(TimeProvider clock, TimeSpan budget);// budget must be > TimeSpan.Zero
    public ValueTask YieldIfDueAsync();                            // yields (+ resets) only once the budget elapses
}

// Problem set
public interface IProblemSetSource
{
    string Name { get; }
    int? Count { get; }
    IAsyncEnumerable<BgDecisionData> EnumerateAsync(CancellationToken cancellationToken = default);
}

public sealed class ShuffledProblemSetSource : IProblemSetSource   // decorator: Fisher-Yates over an inner source
{
    public ShuffledProblemSetSource(IProblemSetSource inner);              // production: non-deterministic seed
    public ShuffledProblemSetSource(IProblemSetSource inner, int seed);   // deterministic tests / replay
    public string Name { get; }    // passthrough from inner
    public int? Count { get; }     // passthrough from inner (null when inner streams)
    public IAsyncEnumerable<BgDecisionData> EnumerateAsync(CancellationToken cancellationToken = default);
        // drains + re-shuffles the inner source each call → a fresh permutation per enumeration
}

// Quiz scoring. ProblemKey is the content identity stamped at construction
// (via ProblemKey.TryDerive); null = the no-key rung — scores the session,
// never recorded in lifetime stats.
public sealed record SubmittedPlay(
    ProblemKey? ProblemKey,
    Play UserPlay, int? MatchedCandidateIndex, double EquityLoss, bool IsCorrect);
public sealed record SubmittedCubeAction(
    ProblemKey? ProblemKey,
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

// Per-problem lifetime stats (the BgQuiz persistent stats-file model, content-keyed)
public sealed record ProblemStats(ProblemKey Key, ScoreSegment Tally, DateTimeOffset LastQuizzed)
{
    public int Wrong { get; }                  // derived: Tally.Submitted − Tally.Correct; never stored
    public static ProblemStats From(SubmittedPlay play, DateTimeOffset quizzedAt);       // first-ever fold; throws on null key
    public static ProblemStats From(SubmittedCubeAction cube, DateTimeOffset quizzedAt);
    public ProblemStats Plus(SubmittedPlay play, DateTimeOffset quizzedAt);       // ArgumentException on null/mismatched key
    public ProblemStats Plus(SubmittedCubeAction cube, DateTimeOffset quizzedAt); // TWO decisions (per half), as QuizScore
}

[JsonConverter(typeof(ProblemStatsDocumentJsonConverter))]    // bundled (public converter); consumers register nothing
public sealed class ProblemStatsDocument                      // immutable; reference equality (see Pitfalls)
{
    public const int CurrentSchemaVersion = 4;   // every recognised version below it is retired
    public static ProblemStatsDocument Empty { get; }
    public static ProblemStatsDocument FromStats(IEnumerable<ProblemStats> stats);   // ArgumentException on duplicate key
    public int Count { get; }
    public IReadOnlyDictionary<ProblemKey, ProblemStats> Problems { get; }
    public ProblemStatsDocument Plus(SubmittedPlay play, TimeProvider clock);        // clock resolved here — the model
    public ProblemStatsDocument Plus(SubmittedCubeAction cube, TimeProvider clock);  //   never reads ambient time;
                                                                                     //   null-key submission → same document
}

// The deliberate retired-version signal (see the stats wire-format section):
// catch BEFORE the general JsonException to retire an old file honestly.
public sealed class RetiredStatsSchemaException : JsonException
{
    public int SchemaVersion { get; }          // the retired version the document declared (1 or 2);
    public RetiredStatsSchemaException(int schemaVersion, string message);   // name the set-aside file from it
}

// Stats-weighted quiz categories (predicates over a decision's lifetime stats)
public enum QuizCategoryKind
{
    NeverSeen = 1, GotWrong, SeenFewerThan, NotSeenInDays,
    AvgEquityLossOver, WrongRateOver, EverythingElse,
}

public sealed record QuizCategory   // immutable DTO; value equality; factory-only construction
{
    public QuizCategoryKind Kind { get; }
    public double? Value { get; }     // null iff parameterless kind; int parameters stored exactly
    public bool IsResidual { get; }   // Kind == EverythingElse — see Pitfalls
    public static QuizCategory NeverSeen { get; }                  // cached singletons
    public static QuizCategory GotWrong { get; }
    public static QuizCategory EverythingElse { get; }
    public static QuizCategory SeenFewerThan(int times);           // times ≥ 1; sightings, not submissions
    public static QuizCategory NotSeenInDays(int days);            // days ≥ 1; never-seen matches too
    public static QuizCategory AvgEquityLossOver(double loss);     // finite, ≥ 0.0; per-half average
    public static QuizCategory WrongRateOver(double rate);         // fraction in [0.0, 1.0); per-half
}

// Stats-weighted quiz mix config (persisted by the consumer à la FilterConfig)
public sealed record QuizMixEntry
{
    public QuizMixEntry(QuizCategory category, int percent);   // percent 1–100, validated here
    public QuizCategory Category { get; }
    public int Percent { get; }
}

[JsonConverter(typeof(QuizMixJsonConverter))]   // bundled (public converter); consumers register nothing
public sealed class QuizMix : IEquatable<QuizMix>   // immutable; hand-written value equality — Entries
{                                                   // as an ordered sequence + both scalars (see Pitfalls)
    public const int CurrentSchemaVersion = 1;
    public static QuizMix Empty { get; }        // blank: composition layer fully inert
    public QuizMix(IEnumerable<QuizMixEntry> entries, int? quizLength = null, bool randomOrder = true);
        // fail fast: sum == 100, no duplicate categories, length ≥ 1, no length without entries
    public IReadOnlyList<QuizMixEntry> Entries { get; }   // declared order — contractual (see Pitfalls)
    public int? QuizLength { get; }             // null = size to the deduped union of selected pools
    public bool RandomOrder { get; }            // default true; false = fully deterministic
    public bool IsPassthrough { get; }          // Entries.Count == 0
    public string ToJson();
    public static QuizMix FromJson(string json);                    // fail loud
    public static bool TryFromJson(string? json, out QuizMix mix);  // absent/corrupt → Empty + false
}

// The source-generated serializer metadata over the two wire units above.
// Consumers that serialize a BgGame_Lib type themselves chain it, rather than
// declaring the type in their own context — see "Source generation & trimming".
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ProblemStatsDocument))]
[JsonSerializable(typeof(QuizMix))]
public sealed partial class BgGameJsonContext : JsonSerializerContext { }

// Stats-weighted composing source (decorator; see Pitfalls for the contracts).
// Classification looks lifetime records up by each item's derived ProblemKey.
public sealed class MixedProblemSetSource : IProblemSetSource
{
    public MixedProblemSetSource(IProblemSetSource inner,
        Func<ProblemStatsDocument> statsProvider,    // current document, resolved fresh per enumeration;
        QuizMix mix, TimeProvider clock);            //   must never return null
    public MixedProblemSetSource(..., int seed);     // deterministic random-mode draws/shuffle for tests
    public string Name { get; }                      // passthrough
    public int? Count { get; }                       // inner.Count when mix.IsPassthrough; else null
    public MixComposition? LastComposition { get; }  // most recent enumeration; assigned before first yield
    public IAsyncEnumerable<BgDecisionData> EnumerateAsync(CancellationToken cancellationToken = default);
}

// Position-distinct source (decorator: one survivor per ProblemKey; see Pitfalls)
public sealed class DistinctPositionProblemSetSource : IProblemSetSource
{
    public DistinctPositionProblemSetSource(IProblemSetSource inner);   // first occurrence survives
    public string Name { get; }                      // passthrough
    public int? Count { get; }                       // always null — collapse count unknowable pre-enumeration
    public IReadOnlyList<DuplicatePositionClass>? LastDuplicateClasses { get; }
        // most recent enumeration's multi-copy classes (≥2 members, survivor first,
        // first-occurrence order; empty when nothing collapsed); null before the
        // first enumeration; assigned before the first yield
    public IAsyncEnumerable<BgDecisionData> EnumerateAsync(CancellationToken cancellationToken = default);
        // dedupes by derived ProblemKey; no-key items pass through unmerged (fail-open)
}

public sealed record DuplicatePositionClass(      // wraps a list → effectively reference equality
    ProblemKey Key,
    IReadOnlyList<DecisionId> Members);           // every copy's id, occurrence order, survivor first; ≥ 2

public sealed record MixComposition(
    int TargetCount,                                 // QuizLength ?? deduped union of selected pools
    bool HasRequestedLength,                         // which of the two — capped vs capless; not inferable from the counts
    int DrawnCount,                                  // min(TargetCount, union) — what was actually drawn
    IReadOnlyList<MixCompositionEntry> Entries);     // declared entry order
public sealed record MixCompositionEntry(
    QuizCategory Category, int Percent,
    int PoolSize,                                    // matches for this entry, pre-dedupe
    int Requested,                                   // initial largest-remainder share of TargetCount
    int Drawn);                                      // actual, incl. redistribution top-ups

// Answer-type distribution over a decision pool (collection-scoped, not a score).
// The five cube buckets are named for the CubeClaimPair instances they count.
public sealed record AnswerTypeDistribution(
    int CheckerPlays,
    int NoDoubleTake, int DoubleTake, int DoublePass, int TooGoodPass, int TooGoodTake)
{
    public static AnswerTypeDistribution Empty { get; }
    public int Total { get; }                        // derived: sum of the six buckets
    public AnswerTypeDistribution Add(DecisionData decision);
        // increments EXACTLY ONE bucket → Total == decisions folded (see Pitfalls);
        // cube decisions keyed by BestClaimPair (the producer verdict); the
        // boundary-composed incoherent NoDoublePass houses in TooGoodPass by ruling
    public static AnswerTypeDistribution operator +(
        AnswerTypeDistribution a, AnswerTypeDistribution b);   // null-guards both operands
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
- **`DoubleCube` legality is substrate-enforced — three refusal cases.**
  It throws `InvalidOperationException` when the match has no cube (1-point
  match), during the Crawford game, or when the opponent owns the cube.
  Gate on `GameState.CanDouble` instead of catching. Note the model: a
  1-point match is *cubeless*, not Crawford — `NewMatch(1)` has
  `HasCube == false` and `IsCrawford` stays false for the whole match, and
  `FromScores(matchLength: 1, ..., isCrawford: true)` throws. External
  data that labels 1-point-match games "Crawford" must be normalized
  before construction.
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
- **`ProblemStats` and `QuizScore` count a cube position the same way: TWO
  decisions.** Both fold the doubler half and the taker half as independent
  submissions, so a half-right cube reads **1-of-2, not 0-of-1** — a
  deliberate alignment, since two counting rules for the same event are
  user-visible the moment lifetime stats sit beside a session score. The
  difference is only in shape: `QuizScore` splits them across its
  `DoubleDecisions` / `TakeDecisions` segments, while `ProblemStats` sums
  both halves into the one tally of the one record keyed by that position's
  `ProblemKey`. A document's tallies therefore reconcile with a
  `QuizScore.Total` over the same submissions **only when every submission
  carried a key** — no-key submissions score the session but never the
  document (the ruled degrade rung), so a corpus with underivable records can
  legitimately leave the document short; that is the no-key rung, not a fold
  bug.
- **`AnswerTypeDistribution` counts a cube position as ONE — the opposite of
  the bullet above, deliberately.** It buckets pool *membership* (one row per
  decision, keyed by its best pair), while `QuizScore` / `ProblemStats` count
  *answers given* (two per cube, one per half). Do not "fix" the discrepancy by
  aligning them: they measure different things and are not required to
  reconcile. The one-bucket-per-`Add` rule is load-bearing, not incidental —
  `Total` is contractually the number of decisions folded in, which is what lets
  a consumer use it as the pool's matched count instead of counting separately.
  Anything that made an `Add` increment zero or two buckets (a "skip
  unclassifiable decisions" escape hatch, a per-half cube fold) would silently
  break that count. `Add` takes the `DecisionData` — `BgDecisionData.Decision`,
  since `BestClaimPair` lives there, not on the composite.
- **`ProblemStatsDocument` JSON pins names and ordering, not whitespace.**
  The bundled converter hand-writes fixed property names ordered by canonical
  key, so the consumer's naming policy cannot change the format — but
  `WriteIndented` lives on the writer the serializer creates, so indentation
  is still options-controlled. Byte-stable files (diff-friendliness) require
  the consumer to serialize with fixed options. Reads are strict: unknown
  properties, missing required ones, an unknown schema version, invalid or
  duplicate keys, malformed dates, and impossible tallies all throw
  `JsonException` — a version bump is the format's only evolution mechanism;
  do not add tolerant-read behavior. The one carve-out is deliberate: a
  genuine document in **any** retired version (every recognised version below
  `CurrentSchemaVersion`) throws `RetiredStatsSchemaException`, and a consumer
  that wants the honest retire-and-restart path must catch it **before** the
  general `JsonException` (it derives from it — catch order matters) and must
  name the file it sets aside from `SchemaVersion`, not from a constant —
  otherwise a tester carrying two retired formats loses one to the other.
- **A new wire unit is not done until `BgGameJsonContext` declares it.** A
  type serialized through a bundled type-level `[JsonConverter]` resolves fine
  by reflection and fails only once trimming removes what it needed — so the
  completeness test is the gate, not the build: it derives the wire surface
  from the assembly and requires the context's declarations to equal it. Two
  rules come with the declaration. The converter must be **public** (a
  downstream context instantiates it from generated code; an internal one
  fails the *consumer's* compile with SYSLIB1220/SYSLIB1030, not this repo's),
  and it must be declarable — a non-public wire type cannot be declared in a
  public context at all (CS0053), which is the fork leg 2 hit and resolved by
  going internal. Adding a wire unit whose converter delegates a nested type
  to the active options means declaring that type too: nothing here does today
  (see "Source generation & trimming"), which is why this context chains none.
- **No-key submissions vanish from lifetime stats silently — by ruling.**
  `ProblemStatsDocument.Plus` with a null `ProblemKey` returns the *same*
  document (degrade, never block); nothing throws and no counter ticks. That
  document-level skip is the single filter — `ProblemStats.From`/`Plus`
  treat a keyless fold as a caller bug (`ArgumentException`). Do not "fix"
  either end: producer-stamped corpora make the rung unreachable in
  practice, and a stricter fail-loud was explicitly rejected
  (SPEC-stats-identity.md §2).
- **`QuizCategory` measures are per-half and its thresholds are fractions;
  "seen" means sightings.** `SeenFewerThan` counts sightings derived from the
  tally (`Submitted / 2` for a cube decision — the two-half fold), so the
  predicate needs the live decision's kind, not stats alone.
  `AvgEquityLossOver` and `WrongRateOver` compare per-half measures
  (`AverageEquityLoss` is already per-half for cubes — no adjustment) and
  their thresholds are fractions in line with `ScoreSegment.Accuracy`, not
  0–100 points. `EverythingElse` has no standalone predicate — the internal
  `BuildPredicate()` throws for it; gate on `IsResidual` (it means "matched by
  no other selected entry", computable only with the whole mix in hand).
- **`QuizMix` entry order is semantically meaningful — and participates in
  equality.** Under overlapping categories, composition draws entries in
  declared order — contested decisions go to the earlier entry — so reordering
  a mix's entries changes the quiz it composes; preserve order when rendering
  or persisting (the JSON form does). Equality honours the same contract:
  `QuizMix` has hand-written value equality over `Entries` as an **ordered
  sequence** plus `QuizLength` and `RandomOrder`, so the same entries
  reordered are a *different* mix — one `==` answers "does the draft differ
  from the committed mix?". Unlike `ProblemStatsDocument` (which stays
  reference-equal), the class hand-rolls the comparison because the
  record-generated form would compare the wrapped list by reference. Reads
  are strict per the same converter posture — a corrupt or foreign mix throws
  `JsonException` rather than loading quietly different; `TryFromJson` is the
  restore-to-default path and yields `Empty`, never a partially-read mix.
- **`MixedProblemSetSource` contracts differ between blank and active
  mixes.** Blank (passthrough): streams the inner source unchanged — no
  dedupe (duplicate ids stream through), `Count` passthrough,
  `LastComposition` null. Active: materializes per enumeration (the shuffled
  tradeoff), dedupes by `DecisionId` (first occurrence wins — record
  identity, deliberately not the content key), classifies **by each item's
  derived `ProblemKey`** (a content-equal copy under a different id
  classifies by the problem's record; a no-key item classifies as
  never-seen), `Count` is null (read `LastComposition.DrawnCount` after
  enumeration starts instead), and decisions matched by no selected entry
  are unreachable unless an `EverythingElse` entry is in the mix. The stats
  provider is resolved fresh per enumeration — Restart composes against the
  *current* document, including this session's folds — and must never return
  null: that throws `InvalidOperationException` (wiring bug), while an empty
  document is the valid everything-never-seen input. `LastComposition` is
  per-instance mutable state and the `Random` is shared — not thread-safe,
  same caveat class as `ShuffledProblemSetSource`. With `RandomOrder` on,
  re-enumeration is a fresh draw and shuffle, not a replay.
- **`DistinctPositionProblemSetSource` dedupes by content, fails open on
  no-key, and is now stateful.** The key is the derived `ProblemKey` — the
  raw XGID string is an identity nowhere, so two items with different XGIDs
  can and should collapse (same-away across match lengths, mirror-turn).
  No-key items pass through unmerged and never appear in
  `LastDuplicateClasses`; do not "tighten" that into a merge or an error
  (fail-open is the ratified rung). There is no survivor preference any more
  — first occurrence survives, full stop; a consumer that wants a specific
  copy to survive is holding it wrong, because stats no longer key on the
  survivor. `LastDuplicateClasses` makes the instance stateful per
  enumeration (null → telemetry after the first pass; replaced each pass) —
  not thread-safe, the `MixedProblemSetSource` caveat class.
- **`ProblemStatsDocument` has no value equality.** It is a class wrapping an
  `ImmutableDictionary` (record equality would silently be reference
  equality), so two documents with identical contents are not `Equals`.
  Compare `Problems` content — the `ProblemStats` values are records with
  full value equality — not document instances.
- **`IProblemSetSource.EnumerateAsync` is contractually re-iterable.** Implementations
  that wrap a one-shot resource (consumed upload, exhausted network stream)
  must throw on a second call rather than silently yielding an empty
  sequence — an empty sequence would look like "no problems" to the consumer.
- **`ShuffledProblemSetSource` re-shuffles every enumeration — same items,
  new order.** It honors the re-iterable contract (each `EnumerateAsync`
  yields the full set from the start), but the order differs between calls:
  the decorator caches no shuffled list and advances its shared `Random` each
  pass, so a Restart is a fresh permutation, not a replay. Do not assume the
  order is stable across enumerations (e.g. caching indices from one pass to
  address items in the next). It also materializes the inner source in full
  per enumeration, forfeiting a streaming inner's laziness — wrap only when
  shuffling is actually wanted.
- **`Match` aggregation, not ownership.** `GameState.Match` is a reference
  to a `MatchState` that may outlive the `GameState`. Mutations to the
  match (via `AwardGame`) persist into the next game's `GameState`. Do not
  treat the `MatchState` as game-scoped state.
- **SEMANTIC BREAK (2026-07): cube responders now see their OWN frame.**
  `ICubeAgent.ChooseResponseAsync` receives a detached responder-frame
  `GameState.OpponentView()` — the responder is the positive/on-roll-labeled
  player (board positives, `OnRollScore` are its own; the offerer's cube reads
  `CubeOwner.Opponent`, a centered cube stays `Centered`). Before this change
  the responder received the live state in the *offerer's* frame. The signature
  did not change, so **every pre-existing `ICubeAgent` implementor breaks
  silently** — no compile error, the state's meaning inverts. Any implementor
  written against the old convention must drop its mental flip (known external:
  BgInference's `ThresholdCubeAgent`, adapted in the epic's P2 session; the
  umbrella gates the coordinated pointer bump on that adapt). The view is a
  query snapshot: mutating it does not affect the live game, and the live
  state stays in the offerer's frame for `Referee.ApplyCubeResponse`.
- **BREAKING (2026-07): `TranscriptEntry` gained `OnRollSeat` — and it is
  the frame seat, not the actor.** All three subtypes' constructors gained
  the positional param (any consumer constructing entries breaks at
  compile). `OnRollSeat` means "the seat whose perspective `State` is
  expressed in": a play entry's mover *is* its `OnRollSeat`, but a cube
  *response* entry keeps the **offerer's** stamp (the live state never
  flips across the responder query) — the responder is the derived
  `CubeTranscriptEntry.ActingSeat`, and a finished game's winner is the
  derived `GameEndedTranscriptEntry.Winner`. Reading `OnRollSeat` as "who
  acted" is wrong for exactly those two derived cases; use the derived
  properties instead of re-encoding the rules. Consumers should stop
  walking opening-die / flip-per-play conventions to attribute entries —
  the stamp is authoritative.
- **`IMatchObserver` callbacks are synchronous and fail-fast.** They run
  inline on the match loop between agent queries: a slow observer slows the
  match, and an observer exception propagates out of `RunMatchAsync`
  unwrapped, aborting the run. Adapters intended to be non-disruptive must
  enqueue-and-return and be non-throwing by construction. `OnMatchEnded`
  fires only for a completed run — when the run aborts (agent violation,
  cancellation) the stream just stops, with everything up to the failure
  point already delivered; there is no terminal abort callback, by design
  (the observer's owner is the same caller that catches the throw).
- **`GameResult.OnRollWon` is perspective-relative; `GameRecord.Winner` and
  `GameEndedTranscriptEntry.Winner` are absolute.** Consumers attributing
  wins to entrants should read the seat off `GameRecord`/`MatchResult` (or
  the terminal entry's derived `Winner`, which is where the attribution
  rule lives), not re-derive it from a snapshot's perspective-relative
  fields.
- **A scripted match that never plays checkers still hits Crawford.** With
  double→pass bots, the game before a side reaches match length is the
  Crawford game — the window doesn't open and the game must be decided by
  checker play. Exact-transcript scripts should use a money session
  (`matchLength: 0` + `maxGames`) to stay Crawford-free.
- **A `DiceCommitment` is context-bound; verify under the exact commit
  context.** `key.Commit(ctx)` and `commitment.Verifies(key, ctx)` must use the
  same `context` string or verification fails — a mismatch is silent (returns
  false), not an exception. Session 2 binds the match id; a commitment made for
  one match is deliberately unusable for another. Keep a `DiceKey` secret until
  reveal — the derivation algorithm is public, so the key is the only secret;
  the distinct `DiceKey`/`DiceCommitment` types exist so a key is never
  published where a commitment is expected.
- **`SeededDiceSource` is not fair-mode.** Its `Random(seed)` state is
  recoverable in principle from observed rolls and nothing proves
  non-adaptation; use it only for tests / seed-replay. Unseeded, audit-grade
  matches use `VerifiableDiceSource`.
- **`VerifiableDiceSource` implements a frozen cross-language contract.** The
  HMAC-SHA256 keystream, `BE64` block counter, and `b ≥ 252` rejection rule are
  pinned by `spec/verifiable-dice-vectors.json` and re-implemented by third
  parties; changing any of them breaks every external verifier. If the vectors
  ever change, regenerate them from the **independent** reference (not by
  pasting this implementation's output) so the file stays a genuine
  cross-check, and coordinate with session 2's PROTOCOL.md spec.

## Subproject-internal next steps

Surface complete for Phase 1 needs. Cross-cutting work (Phase 1 wiring into
BgQuiz_Blazor, future Phase 2+ modes) is tracked in the umbrella
`INSTRUCTIONS.md`, not here.

- **One British stem to normalize at the next code touch** — `amortised`, in
  the `Transcript.cs:22` doc comment. This repo is otherwise American; the
  wider code-side spelling sweep is tracked in the umbrella.
