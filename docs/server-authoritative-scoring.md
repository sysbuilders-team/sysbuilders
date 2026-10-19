\# ADR: Server-Authoritative Scoring (Tier-2)



\- \*\*Status:\*\* Proposed — Deferred to post-MVP

\- \*\*Date:\*\* 2026-10-19

\- \*\*Related:\*\* C3 (client-authoritative scoring), B10 (server trusts client scores),

&#x20; B11 (two score rules), H21 (MatchSyncBridge scope)



\## Context



Match evaluation is computed on the client (`EvaluationEngine.Evaluate`) and

relayed to the server via `MatchSyncBridge.CmdSendEvaluationResult`. The server

currently validates \*\*shape only\*\*:



\- Slot authorization (`IsSenderAuthorizedForSlot`)

\- Turn and phase match the server's `TurnManager`

\- Bounds on `rawScore`, `penaltyScore`, and smell arrays (`EvaluationPayloadValidator`)

\- Array length caps (`SmellWireCodec.MaxSmellsPerResult = 50`)



The \*\*values are trusted\*\*. A modified client can send any in-bounds score for

any turn and win. This is the current trust boundary, stated in

`MatchSyncBridge`'s XML doc:



> Trust model: the server validates shape (authorization, bounds, sequence),

> not values.



Acceptable for the current target — classroom LAN, non-competitive matches.

Not acceptable for public or ranked play.



\## Decision



Split C3 into two tiers:



\- \*\*Tier-1 (shipped):\*\* shape validation. Server rejects malformed,

&#x20; out-of-sequence, or unauthorized payloads. Does not verify the score was

&#x20; earned.

\- \*\*Tier-2 (deferred):\*\* the server recomputes `EvaluationResult` from the

&#x20; client's diagram and ignores the client's reported numbers.



\*\*This ADR records tier-2 as post-MVP.\*\* No implementation until public/ranked

play enters scope.



\## Why tier-2 is a feature, not a fix



Tier-2 requires the server to have the diagram the client used. That means a

new wire channel:



\- `CmdUploadDiagram(turnNumber, serializedNodes, serializedMethods, serializedRelations)`

&#x20; with the same authorization, turn, and phase checks as `CmdSendEvaluationResult`

\- Server-side storage of each player's diagram snapshot per turn

\- Server-side `EvaluationEngine.Evaluate` against the snapshot using the same

&#x20; `\_activeRequirements` slice the client used

\- Server broadcasts its `EvaluationResult` as authoritative; clients render it,

&#x20; not their own



None of these exist today. XL effort, not a commit.



\## Prerequisites, in dependency order



1\. \*\*Scoring rule unification (B11).\*\* `MatchScoreTracker.CalculateScore` can

&#x20;  return negative (`lastRaw - sum(penalties)`). `EvaluationResult.TotalScore`

&#x20;  clamps at 0 (`Math.Max(0, raw - penalty)`). If the server recomputes while

&#x20;  these disagree, server and client scores diverge silently — the exact bug

&#x20;  tier-2 exists to prevent. \*\*Must ship before tier-2\*\*, not alongside it.



2\. \*\*Diagram sync channel.\*\* New wire format, new server-side data structure,

&#x20;  new authorization check. Same threat model as `CmdSendEvaluationResult`:

&#x20;  validate turn, phase, slot; bound every array length and every string.



3\. \*\*Server-side evaluation.\*\* Reuse `EvaluationEngine.Evaluate`. The engine is

&#x20;  already pure and testable; the server must use the same turn-1..N window

&#x20;  the client used (`MatchSession.GetRequirementsUpToCurrentTurn`). The server

&#x20;  already owns the `MatchSession`, so the slice is available.



4\. \*\*Trust boundary doc.\*\* Update `MatchSyncBridge`'s XML doc to describe

&#x20;  tier-2 and remove the "not values" caveat.



\## Non-goals



\- \*\*Anti-cheat on the diagram itself.\*\* A modified client can upload a fake

&#x20; diagram. Tier-2 makes the \*score\* trustworthy \*given\* a diagram; it does not

&#x20; make the diagram trustworthy. Server-authoritative diagram editing and

&#x20; replay verification are separate problems, out of scope even for tier-2.

\- \*\*Protocol versioning.\*\* `RoomAdvertisement.CurrentVersion` gates the room

&#x20; protocol; tier-2 only needs a version bump if the diagram wire format

&#x20; changes. Decided at implementation time.



\## Consequences



\*\*Positive:\*\*

\- Trust boundary documented. Future maintainers do not rediscover this hole.

\- Dependency graph explicit: B11 → channel → evaluation → doc.

\- No premature implementation, no half-committed branch, no false "in progress"

&#x20; signal on the board.



\*\*Negative:\*\*

\- In-bounds score forgery remains possible until tier-2 ships. Acceptable for

&#x20; LAN, not for public play.

\- Tier-2 must be scheduled explicitly. It will not "come along for free" with

&#x20; any other refactor.



\## When to revisit



\- When ranked matchmaking is scheduled.

\- When the game targets any network beyond a trusted LAN.

\- When B11 (scoring semantics) is fixed — the earliest prerequisite becomes

&#x20; available.



\## References



\- `Assets/Scripts/Network/MatchSyncBridge.cs` — current shape-validation boundary

\- `Assets/Scripts/Network/EvaluationPayloadValidator.cs` — tier-1 bounds

\- `Assets/Scripts/Network/ReadyQuorum.cs` — turn/phase gating

\- `Assets/Scripts/Core/Evaluation/EvaluationEngine.cs` — evaluator to reuse

\- `Assets/Scripts/Core/Session/MatchScoreTracker.cs` — scoring rule that must

&#x20; be unified with `EvaluationResult.TotalScore` (B11)

