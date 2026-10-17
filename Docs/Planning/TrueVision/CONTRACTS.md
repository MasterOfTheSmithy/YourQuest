# Proposed C# contracts for the YourQuest rework

This is a design companion to [REWORK_PLAN.md](C:/Users/Garri/YourQuest/Docs/Planning/TrueVision/REWORK_PLAN.md), section D. Everything below is **PROPOSED**, not implemented or compiled. Sketches show fields and invariants; production implementation must add constructors, serialization tests and validation. Use C#/Newtonsoft JSON already in the project. Do not serialize UnityEngine.Object, delegates, arbitrary JObject operations, or executable code into these contracts.

## Identity and versions

```csharp
// note: Preserve current string IDs via adapters; persistent references always include their world scope.
[Serializable] public struct EntityRef {
    public string worldId;
    public string entityId;
    public EntityKind kind;
}
public enum EntityKind { Player, Npc, Enemy, Location, Resource, Item, Container, Faction, Device }
[Serializable] public struct DefinitionRef {
    public string definitionId;
    public int contentVersion;
    public string contentHash;
}
[Serializable] public struct RevisionGuard {
    public EntityRef entity;
    public long expectedRevision;
    public bool mustExist;
}
[Serializable] public struct CausalRef {
    public string observationId;
    public string evidencePacketId;
    public string parentChangeSetId;
    public string worldEventId;
}
```

IDs are opaque, normalized, bounded strings. No inference from their words. A reference may resolve to an unloaded persisted entity; it does not require a GameObject. A quest requiring immediate physical access also needs an area access certificate. A destroyed entity remains a tombstone; ID reuse is forbidden. Definition hashes cover canonical serialized data with stable ordering and explicit float/number rules.

## Attempts, evidence, hypotheses and discovery

```csharp
public enum ActionKind { Attack, Interact, UseTool, Inspect, Move, Jump, Defend, Trade, Cast }
public enum AttemptOutcome {
    Succeeded, CapabilityLocked, UnsupportedOperation, InvalidTool,
    OutOfRange, Blocked, NoTarget, InsufficientResource, FailedExecution
}
public enum AffordanceKind { Damage, Gather, Inspect, Excavate, Repair, Unlock, Trade }
[Serializable] public sealed class AttemptObservation {
    public int schemaVersion;
    public string observationId;
    public long sequence;
    public string sessionId;
    public EntityRef actor, target, location;
    public bool hasTarget;
    public ActionKind action;
    public AffordanceKind attemptedAffordance;
    public AttemptOutcome outcome;
    public DefinitionRef toolDefinition;
    public string toolInstanceId;
    public string[] targetSemanticTags, toolFunctionTags, contextTags;
    public long worldTick;
    public long utcUnix;
    public float activeSeconds, durationSeconds, distance, damage;
    public bool authoritativeContact, riskyContext, countersApplied;
    public string classifierVersion;
}
[Serializable] public sealed class BehavioralAggregate {
    public string aggregateId, familyKey, playerId;
    public long firstSequence, lastSequence;
    public float activeSecondsObserved, effectiveUnits, redundantUnits;
    public int attempts, successes, lockedAttempts, meaningfulVisits;
    public string[] distinctTargetIds, distinctLocationIds, distinctToolFunctions;
    public string[] evidencePacketIds;
    public long revision;
}
[Serializable] public sealed class EvidencePacket {
    public string evidencePacketId, familyKey, aggregationVersion;
    public long firstSequence, lastSequence;
    public string[] representativeObservationIds;
    public AttemptObservation[] durableExemplars;
    public int effectiveAttempts, meaningfulVisits, diverseTargets, diverseTools;
    public float contextFit, redundancyFraction;
    public bool retainForCommittedCause;
}
[Serializable] public sealed class InterestHypothesis {
    public string hypothesisId, playerId, familyKey;
    public string[] evidencePacketIds;
    public float score, uncertainty, lastEvaluatedActiveTime;
    public string qualificationVersion;
    public bool playerSuppressed;
    public long revision;
}
public enum DiscoveryState {
    Hypothesized, Qualified, Proposed, Validated, Granted,
    Revealed, Used, Remembered, Deferred, Declined, Rejected, Expired
}
[Serializable] public sealed class DiscoveryCandidate {
    public string candidateId, playerId, familyKey;
    public string[] hypothesisIds, evidencePacketIds, supportingFactIds;
    public DefinitionRef mechanic;
    public EntityRef opportunityLocation;
    public DiscoveryState state;
    public float interest, relevance, novelty, worldNeed, uncertainty, generationCost;
    public long earliestWorldTick, expiresWorldTick;
    public RevisionGuard[] preconditions;
    public string rejectionCode;
}
[Serializable] public sealed class CapabilityGrant {
    public string grantId, discoveryId, playerId, capabilityKey;
    public DefinitionRef mechanic;
    public CausalRef[] causes;
    public string changeSetId, revealReceiptId;
    public long grantedWorldTick, firstUsedWorldTick;
    public bool revealed, enabled;
}
```

Observation is an execution result, never model output. One physical attempt may affect several entities: keep one attempt ID plus bounded per-target results rather than awarding evidence once per collider. NoTarget cannot carry a fabricated entity. Store capture order independently of wall-clock time. Aggregate cursors are monotonic and consumer-specific; a model callback cannot clear observations belonging to another consumer or later action. Compaction retains durable exemplars and the computed packet cited by a grant. A hypothesis is provisional, not a declaration of player identity.

Capability uniqueness is `(playerId, capabilityKey, compatible mechanic family/version)`; an upgrade is an explicit transition with predecessor ID. Multiple generated names cannot evade uniqueness. Decline/reveal/use states persist separately from definition generation. Abilities can be equipped after discovery; discovery does not require auto-equipping or changing the player's style.

## Authoritative facts, NPC knowledge and relationships

```csharp
public enum FactPredicate { LocatedAt, Owns, MemberOf, Alive, Depleted, CapabilityLearned, QuestResolved, EventOccurred }
public enum FactValueKind { Entity, Boolean, Integer, Definition }
[Serializable] public struct FactValue {
    public FactValueKind kind;
    public EntityRef entity;
    public bool boolean;
    public long integer;
    public DefinitionRef definition;
}
[Serializable] public sealed class AuthoritativeFact {
    public string factId;
    public EntityRef subject;
    public FactPredicate predicate;
    public FactValue value;
    public string committedChangeSetId;
    public CausalRef[] causes;
    public long validFromTick, validUntilTick, revision;
}
public enum KnowledgeKind { WitnessedFact, ReportedFact, Belief, Rumor }
[Serializable] public sealed class KnowledgeEntry {
    public string knowledgeId;
    public string factId, claimId;
    public KnowledgeKind kind;
    public EntityRef learnedFrom;
    public float certainty;
    public long learnedTick, expiresTick;
}
[Serializable] public sealed class ClaimRecord {
    public string claimId;
    public EntityRef subject, claimant;
    public FactPredicate predicate;
    public FactValue claimedValue;
    public string presentationText;
    public CausalRef[] sources;
}
[Serializable] public sealed class NpcState {
    public EntityRef identity, faction, home, currentLocation;
    public DefinitionRef persona;
    public long revision;
    public bool alive;
    public string serviceSocketId, currentActivityId, inventoryId;
    public KnowledgeEntry[] knowledge;
    public string[] relationshipIds, unresolvedCommitmentIds;
    public string transcriptRevisionId;
}
[Serializable] public sealed class Relationship {
    public string relationshipId;
    public EntityRef from, to;
    public float trust, affinity, fear, obligation;
    public CausalRef[] causes;
    public long revision;
}
```

Only committed operations can write authoritative facts. Knowledge can be wrong; false beliefs/rumors do not overwrite facts. Each NPC prompt receives a projection of known facts/claims, not the full world snapshot. Dialogue text cannot directly set `Alive`, inventory or quest completion. Introducing a claim from dialogue requires a validated speech/knowledge operation, with attribution. Canon prose remains a presentation/indexing aid alongside typed world constraints; not every literary sentence needs a machine fact, but mechanically consequential claims do.

## Quests, items and executable mechanics

```csharp
public enum ObjectiveKind { TalkToNpc, DefeatEnemy, GatherResource, TransferItem, InspectClue, ExcavateCache, ReachLocation, UseCapability }
public enum ProgressScope { SinceAcceptance, LifetimeAccomplishment, CurrentState }
public enum QuestStatus { Offered, Accepted, Completed, Failed, Abandoned, Expired }
public enum QuestFailureKind { TargetDestroyed, DeadlineElapsed, RequiredItemLost, ParticipantHostile }
[Serializable] public sealed class QuestObjective {
    public string objectiveId;
    public ObjectiveKind kind;
    public EntityRef target;
    public DefinitionRef requiredDefinition;
    public string requiredCapabilityKey;
    public ProgressScope scope;
    public long acceptedAfterSequence;
    public int requiredCount, currentCount;
    public string progressReceiptCursor;
    public bool completed;
}
[Serializable] public sealed class QuestFailureRule {
    public QuestFailureKind kind;
    public EntityRef subject;
    public long deadlineWorldTick;
}
[Serializable] public sealed class QuestDefinition {
    public DefinitionRef identity;
    public string name, description;
    public EntityRef issuer;
    public QuestObjective[] objectives;
    public QuestFailureRule[] failureRules;
    public RewardDefinition reward;
    public string[] supportingFactIds;
    public string requiredAccessCertificateId;
}
[Serializable] public sealed class QuestState {
    public string questId;
    public DefinitionRef definition;
    public QuestStatus status;
    public QuestObjective[] progress;
    public long acceptedTick, resolvedTick, revision;
    public string rewardReceiptId;
    public CausalRef[] causes;
}
[Serializable] public sealed class RewardDefinition {
    public int xp, currency;
    public DefinitionRef[] itemDefinitions;
    public int[] itemQuantities;
    public RelationshipChange[] relationshipChanges;
}
[Serializable] public sealed class ItemDefinition {
    public DefinitionRef identity;
    public string name, description, itemKind, equipSlot;
    public string[] approvedToolFunctions;
    public int maxStack;
    public string prefabKey, iconKey, materialKey;
    public EffectSpec[] passiveEffects;
}
[Serializable] public sealed class ItemInstance {
    public EntityRef identity, owner;
    public DefinitionRef definition;
    public int quantity;
    public float durability;
    public string equippedSlot;
    public long revision;
    public CausalRef[] acquisitionCauses;
}
public enum TargetMode { Self, Contact, Projectile, Radius, Location }
public enum ResourceKind { None, Mana, Stamina, ItemCharge }
public enum EffectKind { Damage, Recover, TimedModifier, GatherResource, RevealClue, ExcavateCache }
public enum StackPolicy { Replace, Refresh, Maximum, CappedAdd }
public enum PowerFormulaKind { Constant, BasePlusStat, FractionOfMaximum }
[Serializable] public sealed class EffectSpec {
    public EffectKind primitive;
    public int primitiveVersion;
    public PowerFormulaKind formula;
    public string allowedStatKey;
    public float baseMagnitude, coefficient, durationSeconds;
    public StackPolicy stackPolicy;
    public int maximumStacks;
    public DefinitionRef outputItem;
    public int outputQuantity;
}
[Serializable] public sealed class MechanicDefinition {
    public DefinitionRef identity;
    public int executionSchemaVersion;
    public string name, description, capabilityKey;
    public TargetMode targeting;
    public AffordanceKind affordance;
    public string[] allowedTargetTags, requiredToolFunctions;
    public ResourceKind resource;
    public float resourceCost, cooldownSeconds, range, radius;
    public int maximumTargets, maximumTriggerDepth;
    public EffectSpec[] effects;
    public string animationIntent, vfxFamily, sfxFamily;
    public string environmentPermissionKey;
    public string[] prerequisiteCapabilityKeys;
}
```

Quest definition and state are distinct; accepting a new quest captures a scope/sequence baseline, not an implicit lifetime counter. Every accepted quest progresses whether pinned in HUD or not. Rewards are explicit values and operations; the names “treasure” or “contract” do not alter payment. Parallel reward arrays must have equal length, or implement a typed `(definition, quantity)` record instead. Inventory mutation uses definition compatibility and revision checks; stacking does not merge unique enchanted instances blindly.

A generated mechanic is data over enumerated primitives, not an executable script. Every enum value in an advertised registry must have a tested executor; the sketch's enum is a proposal, not present-day support. Preconditions include current tool, target, capability, cooldown, resource, permitted environment mutation and ownership. Effect-specific validators reject irrelevant fields and unsupported combinations. A recovery effect never falls back to damage. Trigger graph/node references, if added later, must be explicit DAG data; v1 can prohibit composition nesting altogether.

## Events, proposals, commitment and save metadata

```csharp
public enum WorldEventKind { ScheduledVisit, ResourceDemand, LocalConflict, DiscoveryResponse }
[Serializable] public sealed class WorldEvent {
    public string eventId;
    public WorldEventKind kind;
    public EntityRef[] participants;
    public EntityRef location;
    public long dueTick, resolvedTick;
    public bool independentOfPlayerInterest;
    public CausalRef[] causes;
    public string committedChangeSetId;
}
[Serializable] public sealed class PersistentConsequence {
    public string consequenceId, sourceChangeSetId;
    public EntityRef[] affectedEntities;
    public string[] factIds, pendingWorldEventIds;
    public CausalRef[] causes;
    public long revision;
}
[Serializable] public sealed class GenerationRequest {
    public string requestId, candidateId, worldId, sessionEpoch;
    public string contextHash, promptVersion, modelProfile;
    public long snapshotGeneration;
    public RevisionGuard[] readSet;
    public string[] allowedPrimitiveKeys, supportingFactIds, allowedKnowledgeIds;
    public int inputTokenBudget, outputTokenBudget;
    public long expiresWorldTick;
}
public enum OperationKind { GrantCapability, CreateItem, TransferItem, ChangeRelationship, AddQuest, SetFact, ScheduleEvent, ConsumeResource }
[Serializable] public sealed class RelationshipChange {
    public string relationshipId;
    public float trustDelta, affinityDelta, fearDelta, obligationDelta;
}
[Serializable] public sealed class ItemTransfer {
    public EntityRef item, from, to;
    public int quantity;
}
[Serializable] public sealed class ResourceConsumption {
    public EntityRef source;
    public int amount;
}
[Serializable] public sealed class ChangeOperation {
    public OperationKind kind;
    // note: Exactly one typed arm is populated; the validator rejects wrong/multiple arms.
    public CapabilityGrant capability;
    public ItemInstance item;
    public ItemTransfer transfer;
    public RelationshipChange relationship;
    public QuestDefinition quest;
    public AuthoritativeFact fact;
    public WorldEvent worldEvent;
    public ResourceConsumption consumption;
}
[Serializable] public sealed class GenerationProposal {
    public string proposalId, requestId, worldId, sessionEpoch;
    public int schemaVersion;
    public RevisionGuard[] preconditions;
    public MechanicDefinition[] mechanics;
    public ItemDefinition[] itemDefinitions;
    public ChangeOperation[] operations;
    public string[] supportingFactIds, evidencePacketIds;
    public string narrativeExplanation;
}
public enum ValidationCode {
    UnsupportedSchema, UnsupportedPrimitive, InvalidRange, InvalidComposition,
    MissingReference, StaleRevision, ContradictoryFact, UnknownToNpc,
    UnreachableTarget, Duplicate, BudgetExceeded, IncompatibleContent
}
[Serializable] public sealed class ValidationIssue {
    public ValidationCode code;
    public string fieldPath, entityId, detail;
    public bool repairable;
}
[Serializable] public sealed class ValidationResult {
    public bool accepted;
    public string validatorVersion, normalizedProposalHash;
    public ValidationIssue[] issues;
}
[Serializable] public sealed class CommittedChangeSet {
    public string changeSetId, proposalId, worldId;
    public long priorGeneration, resultingGeneration;
    public string normalizedProposalHash, validatorVersion;
    public ChangeOperation[] operations;
    public string[] operationReceiptIds;
    public CausalRef[] causes;
}
[Serializable] public sealed class SnapshotFile {
    public string relativePath, sha256;
    public long byteLength;
}
[Serializable] public sealed class SaveVersionMetadata {
    public string profileId, worldId, generationId;
    public int saveSchemaVersion, primitiveRegistryVersion;
    public long generationSequence, lastObservationSequence, lastCommitSequence;
    public string generatorVersion, validatorVersion, assetContractHash;
    public string[] requiredContentHashes;
    public SnapshotFile[] files;
    public string previousCompleteGenerationId;
}
```

The operation union uses explicit typed arms and enum-discriminated validation. No unrestricted string “command,” arbitrary JSON operation, reflection type name or script callback is accepted. Economic resource gain/loss is checked as a whole: a gather change set consumes resource availability and creates/transfers its output together. Effects that touch multiple targets prepare all affected revisions and operations first, with declared all-or-nothing or per-target execution semantics; never claim an atomic multi-target result if only some effects applied.

Snapshot `relativePath` is generated by trusted persistence code, constrained under its generation directory, never chosen by the LLM. Active manifest references only fully written, hash-validated files. Definitions are immutable/versioned; runtime deltas and relationship revisions live in state. Expired transport work does not survive as a live Task/coroutine; persist intent for reevaluation. Receipt and generation retention must prevent duplicate permanent grants even after raw journal compaction.

## Integration interfaces and failure behavior

```csharp
// note: Proposed signatures show explicit ownership boundaries, not required new services.
public interface IAttemptJournal {
    long Append(AttemptObservation observation);
    AttemptObservation[] ReadAfter(long exclusiveSequence, int maximumCount);
}
public interface ICapabilityRegistry {
    ValidationResult Validate(MechanicDefinition definition);
    bool CanExecute(EntityRef actor, EntityRef target, DefinitionRef mechanic, out AttemptOutcome failure);
}
public interface IProposalValidator {
    ValidationResult Validate(GenerationProposal proposal, long expectedSnapshotGeneration);
}
public interface IChangeCommitter {
    // Prepared changes hold immutable data, validated asset handles and revision guards.
    // CommitResult must distinguish rejected, already committed, durably committed and I/O failure.
    System.Threading.Tasks.Task<CommitResult> CommitAsync(GenerationProposal validatedProposal);
}
public enum CommitStatus { Rejected, AlreadyCommitted, Committed, PersistenceFailed }
public sealed class CommitResult {
    public CommitStatus status;
    public string changeSetId;
    public ValidationIssue[] issues;
}
```

`Validate` returning accepted does not eliminate commit-time revalidation. The coordinator performs the final main-thread state transition; it must not hold a lock while awaiting model output or disk writes. Prepared snapshot persistence and live publication are ordered so profile switching cannot publish another world's changes. Introduce a session epoch check after each await and before every presentation callback. If newer ordinary state changes occur during saving, serialize commits or merge only declared disjoint fields; never overwrite live state with an old cloned player record.

The first implementation should prove these invariants with gather and excavation, including concurrent quest/reward state. Do not expose all proposed types as new public managers or pre-implement every content family before that proof.
