# /goal — Establish the authoritative production build and beta baseline

Continue development of YourQuest by first turning the current project into a clearly understood, reproducible production baseline.

Do not add major new gameplay systems during this goal. The purpose is to identify which existing systems actually own runtime behavior, remove ambiguity between production and legacy code, and establish the verification framework that every later beta goal will rely on.

## Work model

Use **Luna High** as the primary work chat and final owner of all decisions.

Luna High may delegate contained tasks to Luna High subagents.

Use **Astra Light only for orchestration and narrowly bounded mechanical work** such as reference searches, file enumeration, log categorization, short comparison tasks, and other work representing no more than roughly 5–10% of an hourly goal.

If Luna High is not sufficient for a difficult implementation/debugging task but Sol High would be excessive, prefer **Luna Max or Luna Extra High** where available.

Reserve **Sol High** for genuinely high-risk reasoning such as persistent-state architecture, difficult cross-system bugs, save corruption, deterministic-world invariants, or architectural decisions affecting several authoritative systems.

All delegated work must be reviewed and integrated by the primary Luna High session.

## Establish the authoritative runtime path

Trace and document the production path for:

- startup and profile selection
    
- player-state loading
    
- world-state loading
    
- origin data
    
- generated world planning
    
- accepted spatial authority
    
- physical world construction
    
- continuous streaming
    
- NPC population
    
- combat
    
- inventory/equipment
    
- progression
    
- quests
    
- dialogue
    
- LLM scheduling
    
- world adaptation
    
- persistence
    
- UI
    

For every major system encountered, classify it as one of:

- active production
    
- active production but historically named
    
- editor/authoring tooling
    
- verification/test infrastructure
    
- compatibility layer
    
- superseded/deprecated
    
- confirmed dead
    

Do not delete anything merely because its name looks old.

Before removing a system, check runtime references, prefabs, scenes, ScriptableObjects, editor tooling, automated tests, reflection usage, save migration, and indirect construction.

## Consolidate obvious duplicate authority

Where two systems currently appear to maintain overlapping truth, determine which one is actually authoritative.

Prefer one durable state owner rather than permanent synchronization between duplicate models.

In particular, audit legacy player/progression/director compatibility paths that were previously identified as likely redundant.

Do not perform broad aesthetic refactoring.

Only remove or migrate duplicate systems when doing so clearly reduces ambiguity and all callers have been accounted for.

## Establish beta diagnostics

Create a developer-facing build/diagnostic identity including:

- game/build version
    
- save schema version
    
- world-generation version
    
- semantic-chunk schema version
    
- active spatial-authority mode
    
- world seed
    
- active profile identifier
    
- generation architecture status
    

Make this information easy to obtain during testing.

## Establish reproducible test fixtures

Create:

- one canonical development profile
    
- one known deterministic development world seed
    
- a reliable clean-profile reset path
    
- a repeatable way to enter the production gameplay scene
    
- a repeatable way to exercise save/reload
    
- a repeatable traversal verification path
    

Do not tune the actual game around this seed. It is a diagnostic fixture.

## Build a beta regression suite

Create or consolidate a single production verification entry point covering at minimum:

- clean profile creation
    
- origin completion
    
- world generation
    
- accepted spatial validation
    
- world materialization
    
- player spawn
    
- NPC population
    
- current-cell terrain readiness
    
- continuous streaming
    
- save
    
- reload
    
- deterministic reconstruction
    
- core gameplay service initialization
    

Existing specialized tests should remain specialized, but the project needs one clear top-level production verification path.

## Remove development noise

Fix or quarantine:

- persistent compiler errors
    
- recurring null-reference spam
    
- misleading warnings generated during healthy gameplay
    
- obsolete debug systems executing in production
    
- duplicated runtime singletons
    
- accidental prototype bootstrap paths
    

Do not hide genuine errors by removing logs.

Healthy runtime should be quiet enough that a new error is obvious.

## Deliverable

At completion, produce an architecture report containing:

- authoritative system map
    
- deprecated/compatibility system map
    
- files removed or migrated
    
- duplicate authorities resolved
    
- unresolved architectural risks
    
- beta regression entry point
    
- exact test profile/seed procedure
    
- current known blockers for beta
    

## Completion gate

Do not complete this goal until:

- a clean profile reliably reaches gameplay
    
- save/reload succeeds
    
- the active world-generation path is unambiguous
    
- there is one known authoritative player-state path
    
- there is one known authoritative world-state path
    
- duplicate production ownership has been reduced where safely possible
    
- the primary regression path passes
    
- subsequent work can modify the project without guessing which implementation is actually live
    

The outcome of this goal should be a **trustworthy foundation**, not new features.