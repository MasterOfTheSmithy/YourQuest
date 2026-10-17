// Assets/Assets/Scripts/Dialogue/NpcDialogueAgent.cs
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcDialogueAgent : MonoBehaviour
{
    private static readonly List<NpcDialogueAgent> Active = new List<NpcDialogueAgent>();
    private static readonly IReadOnlyList<NpcDialogueAgent> ActiveView = Active.AsReadOnly();
    public static IReadOnlyList<NpcDialogueAgent> ActiveAgents => ActiveView;

    private void OnEnable()
    {
        // note: Hearing uses the existing active dialogue agents; it does not repeatedly search scenes or invent creature intelligence.
        if (!Active.Contains(this)) Active.Add(this);
    }

    [Header("Identity (fallbacks to EntityInfo)")]
    public string npcId = string.Empty;
    public string npcName = string.Empty;
    [TextArea(2, 5)] public string personaSummary = "A grounded in-world NPC with practical knowledge of the surrounding region.";
    public List<string> tagsOverride = new List<string>();
    [Min(4)] public int maxMemoryLines = 160;
    public bool persistTranscriptAcrossSessions = true;

    private EntityInfo _entityInfo;
    private NpcDialogueSession _session;
    private bool _sessionLoaded;
    private string _loadedSessionNpcId = string.Empty;
    private PlayerState _loadedSessionPlayer;
    // note: Failed publication retains the exact accepted delivery token for an explicit retry; successful delivery retires it before any reply/save side effects.
    private YQDispositionDomain.Admission _pendingDisposition;
    private string _pendingDispositionText;
    private string _pendingDispositionNpcId;
    private YQNpcSocialInteraction _socialInteraction;
    private int _replySequence;
    private readonly HashSet<string> _heardDeliveries = new HashSet<string>(StringComparer.Ordinal);
    private readonly Queue<string> _heardDeliveryOrder = new Queue<string>();
    private double _replyStartedAt;
    public double LastReplyLatencySeconds { get; private set; }
    private float _nextRewardConsideration;
    internal string TurnOfferId { get; private set; }
    internal string TurnOfferTerms { get; private set; }
    internal string ProposedOfferDecision { get; set; }
    internal bool CanConsiderReward => Time.unscaledTime >= _nextRewardConsideration;
    internal void MarkRewardConsidered() => _nextRewardConsideration = Time.unscaledTime + 60f;

    private void OnDisable()
    {
        Active.Remove(this);
        // note: Streaming out an NPC retires its pending reply; it must not revive or write memory after removal.
        _replySequence++;
        IsThinking = false;
    }

    public string NpcId => !string.IsNullOrWhiteSpace(npcId) ? npcId.Trim() : (_entityInfo != null ? _entityInfo.entityId : "npc_unknown");
    public string NpcName => !string.IsNullOrWhiteSpace(npcName) ? npcName.Trim() : (_entityInfo != null ? _entityInfo.displayName : "NPC");
    public string LastNpcLine { get; private set; }
    public bool IsThinking { get; private set; }
    // note: Operational failures are transient UI status, never NPC speech or persisted memory.
    public string LastReplyStatus { get; private set; } = string.Empty;
    public bool LastReplyGraphicsMemoryUnavailable { get; private set; }
    public bool LastReplyWasFallback { get; private set; }
    // note: Delivery belongs to the accepted reply and resets with its turn; it never chooses a different speaker.
    public string LastReplyDelivery { get; internal set; } = "measured";
    private int _fallbackReplySequence;
    internal void ReportReplyUnavailable(string status, bool graphicsMemoryUnavailable = false)
    {
        // note: Recovery uses a transient failure reason rather than interpreting player-facing wording.
        LastReplyStatus = status;
        LastReplyGraphicsMemoryUnavailable = graphicsMemoryUnavailable;
    }
    internal string CreateFallbackReply(string message)
    {
        // note: Outage/rejection flavour remains presentation-only for every dialogue-capable actor, including intelligent creatures.
        LastReplyWasFallback = true;
        return BuildFallback(message);
    }
    public string TagsCsv => string.Join(", ", GetTags());

    // note: Presentation listens to the authoritative persisted session instead of guessing when an async reply changed it.
    public event Action TranscriptChanged;

    private void Awake()
    {
        ResolveIdentity();
        EnsureSessionLoaded();
    }

    private void Start()
    {
        ResolveIdentity();
        RebindSessionIfNeeded();
    }

    public void RefreshIdentityAndSession()
    {
        ResolveIdentity();
        RebindSessionIfNeeded();
    }

    public void CancelPendingTurn()
    {
        // note: Cancellation retires callbacks and releases this NPC without altering accepted conversation or world state.
        _replySequence++;
        IsThinking = false;
    }

    public void SendPlayerMessage(string playerText, Action<string> onNpcReplyText, Func<bool> interactionCurrent = null)
    {
        SendPlayerMessageCore(playerText, onNpcReplyText, interactionCurrent, null, null);
    }

    // note: The real caller supplies its immutable displayed order and current presentation proof. Old callers retain the single-offer route.
    public void SendPlayerMessageWithDisplayedOffers(string playerText, Action<string> onNpcReplyText, Func<bool> interactionCurrent,
        YQNpcConversationRewards.DisplayedOfferList displayedOffers, Func<bool> offersStillDisplayed, string deliveryId = null)
    {
        SendPlayerMessageCore(playerText, onNpcReplyText, interactionCurrent, displayedOffers, offersStillDisplayed, deliveryId);
    }

    private void SendPlayerMessageCore(string playerText, Action<string> onNpcReplyText, Func<bool> interactionCurrent,
        YQNpcConversationRewards.DisplayedOfferList displayedOffers, Func<bool> offersStillDisplayed, string deliveryId = null)
    {
        EnsureSessionLoaded();
        // note: One NPC owns one in-flight turn; overlapping submissions used to reorder its transcript.
        if (IsThinking || !isActiveAndEnabled || (interactionCurrent != null && !interactionCurrent()))
            return;
        if (string.IsNullOrWhiteSpace(playerText))
        {
            onNpcReplyText?.Invoke(null);
            return;
        }

        string trimmed = playerText.Trim();
        // note: Duplicate finalized ASR deliveries cannot append another turn, propose another gift, or multiply a heard insult. New captures intentionally receive new identities.
        string heardKey = string.IsNullOrEmpty(deliveryId) ? null : (_loadedSessionPlayer?.playerId + "|" + NpcId + "|" + deliveryId);
        if (heardKey != null && _heardDeliveries.Contains(heardKey)) { onNpcReplyText?.Invoke(null); return; }
        _replyStartedAt = Time.realtimeSinceStartupAsDouble;
        LastReplyStatus = string.Empty;
        LastReplyGraphicsMemoryUnavailable = false;
        LastReplyWasFallback = false;
        LastReplyDelivery = "measured";
        TurnOfferId = YQNpcConversationRewards.PresentedOfferId(_loadedSessionPlayer, NpcId);
        TurnOfferTerms = YQProfileSaveSystem.CaptureOfferTerms(_loadedSessionPlayer?.FindOfferById(TurnOfferId));
        if (displayedOffers != null) {
            bool shown = offersStillDisplayed != null && offersStillDisplayed() &&
                displayedOffers.Matches(_loadedSessionPlayer, NpcId, YQServiceLifecycle.RequestEpoch);
            TurnOfferId = shown && displayedOffers.Count == 1 ? displayedOffers.OfferIdAt(0) : null;
            TurnOfferTerms = shown && displayedOffers.Count == 1 ? displayedOffers.TermsAt(0) : null;
        }
        ProposedOfferDecision = "none";
        // note: These explicit whole-utterance interactions have independent accepted requests; generic yes and generated speech cannot give items or report deeds.
        if (interactionCurrent != null && YQNpcSocialInteraction.IsSupported(trimmed)) {
            _pendingDisposition = null; _pendingDispositionText = null; _pendingDispositionNpcId = null;
            IsThinking = true;
            int socialSequence = ++_replySequence; PlayerState socialPlayer = _loadedSessionPlayer; string socialNpcId = NpcId;
            Func<bool> socialCurrent = () => this != null && isActiveAndEnabled && socialSequence == _replySequence &&
                ReferenceEquals(socialPlayer, PlayerStateManager.Instance?.state) && socialNpcId == NpcId && interactionCurrent();
            _socialInteraction ??= new YQNpcSocialInteraction(this);
            _socialInteraction.Handle(trimmed, socialCurrent, (speech, status) => {
                if (socialSequence != _replySequence) return;
                IsThinking = false;
                if (!socialCurrent()) return;
                if (!string.IsNullOrEmpty(status)) ReportReplyUnavailable(status);
                if (string.IsNullOrEmpty(speech)) { onNpcReplyText?.Invoke(null); return; }
                if (YQProfileSaveSystem.Instance?.RequiresProfileReload != true) {
                    // note: A transcript listener can fail after the gift/report was saved; preserve the committed success for reply delivery.
                    try { RememberDelivery(heardKey); CommitPlayerLine(trimmed); }
                    catch (Exception exception) { Debug.LogException(exception); }
                }
                DeliverCompletedReply(speech, socialPlayer, socialNpcId, onNpcReplyText, preserveCommittedReply: true);
            });
            return;
        }
        // note: This method is the existing directed-text delivery boundary. Only its actual player span and receiving NPC are admitted; no nearby/witness audience is invented.
        if (interactionCurrent != null && YQDispositionDomain.TryClassifyDirectInsult(trimmed, out _)) {
            var deliveredPlayer = _loadedSessionPlayer;
            var deliveredWorld = WorldStateManager.Instance?.State;
            if (_pendingDisposition == null || _pendingDispositionText != trimmed ||
                !ReferenceEquals(_pendingDisposition.player, deliveredPlayer) || !ReferenceEquals(_pendingDisposition.world, deliveredWorld) ||
                _pendingDispositionNpcId != NpcId) {
                _pendingDisposition = null;
                YQDispositionDomain.TryCaptureDeliveredInsult(deliveredPlayer, deliveredWorld, NpcId, trimmed,
                    "speech:" + (heardKey == null ? Guid.NewGuid().ToString("N") : YQStateContract.Sha256Hex(heardKey)), out _pendingDisposition);
                _pendingDispositionText = trimmed;
                _pendingDispositionNpcId = NpcId;
            }
            var save = YQProfileSaveSystem.Instance;
            if (_pendingDisposition == null || save == null ||
                !save.TryCommitDisposition(deliveredPlayer, deliveredWorld, _pendingDisposition, out var socialReceipt, out _, out _) ||
                socialReceipt == null || !socialReceipt.published) {
                ReportReplyUnavailable("Your words could not be saved. Please try again."); onNpcReplyText?.Invoke(null); return;
            }
            _pendingDisposition = null; _pendingDispositionText = null; _pendingDispositionNpcId = null;
            if (save.RequiresProfileReload) { ReportReplyUnavailable("Your words were saved. Reload this journey before continuing."); onNpcReplyText?.Invoke(null); return; }
        } else { _pendingDisposition = null; _pendingDispositionText = null; _pendingDispositionNpcId = null; }

        RememberDelivery(heardKey);
        CommitPlayerLine(trimmed);
        IsThinking = true;
        int sequence = ++_replySequence;
        PlayerState player = _loadedSessionPlayer;
        string identity = NpcId;
        Func<bool> stillCurrent = () => this != null && isActiveAndEnabled &&
            sequence == _replySequence && ReferenceEquals(player, PlayerStateManager.Instance?.state) &&
            string.Equals(identity, NpcId, StringComparison.OrdinalIgnoreCase) &&
            (interactionCurrent == null || interactionCurrent());


        // note: Ordinals are resolved only against the exact UI snapshot delivered with this turn; models never choose list positions.
        if (YQNpcConversationRewards.HasOrdinalReference(trimmed) ||
            (displayedOffers != null && YQNpcConversationRewards.ClassifyOfferReply(trimmed) != "none")) {
            if (!stillCurrent()) { IsThinking = false; return; }
            string orderedReceipt = "Please open this NPC's numbered offer list before choosing an offer by number.";
            string decision = "none"; int index = -1; bool orderedCommitted = false;
            if (stillCurrent() && offersStillDisplayed != null && offersStillDisplayed() &&
                YQNpcConversationRewards.TryResolveDisplayedDecision(displayedOffers, player, identity, YQServiceLifecycle.RequestEpoch,
                    trimmed, out index, out decision, out orderedReceipt)) {
                TurnOfferId = displayedOffers.OfferIdAt(index); TurnOfferTerms = displayedOffers.TermsAt(index);
                orderedCommitted = YQNpcConversationRewards.TryApplyDisplayedDecision(displayedOffers, index, player, identity, decision, trimmed,
                    () => stillCurrent() && offersStillDisplayed(), out orderedReceipt);
            }
            DeliverCompletedReply(orderedCommitted ? BuildOfferDecisionReply(player, TurnOfferId, decision, orderedReceipt) : orderedReceipt, player, identity,
                onNpcReplyText, preserveCommittedReply: true);
            return;
        }

        // note: Explicit acceptance uses only this speaker's previously presented offer; the generated reply cannot grant anything.
        if (displayedOffers == null && stillCurrent() && YQNpcConversationRewards.TryAccept(player, identity, trimmed, out string receipt, TurnOfferTerms))
        {
            DeliverCompletedReply(BuildOfferDecisionReply(player, TurnOfferId, YQNpcConversationRewards.ClassifyOfferReply(trimmed), receipt), player, identity, onNpcReplyText);
            return;
        }

        DialogueThinkService service = DialogueThinkService.Instance;
        if (service == null)
        {
            ReportReplyUnavailable("Dialogue is unavailable. Please try again shortly.");
            IsThinking = false;
            ReportReplyUnavailable("The dialogue service is not ready. Please try again shortly.");
            onNpcReplyText?.Invoke(null);
            return;
        }

        service.RequestNpcReply(this, trimmed, npcReply =>
        {
            if (!stillCurrent())
                return;
            if (ProposedOfferDecision == "accept" || ProposedOfferDecision == "decline")
            {
                // note: A structured intent is bound to the offer captured at utterance start. Discard model prose and speak only the actual mutation receipt.
                string choiceReceipt; bool displayedCommitted = false;
                if (displayedOffers != null) {
                    choiceReceipt = "Which numbered offer are you accepting or declining?";
                    if (displayedOffers.Count == 1)
                        displayedCommitted = YQNpcConversationRewards.TryApplyDisplayedDecision(displayedOffers, 0, player, identity, ProposedOfferDecision, trimmed,
                            () => stillCurrent() && offersStillDisplayed != null && offersStillDisplayed(), out choiceReceipt);
                } else if (!YQNpcConversationRewards.TryApplyDecision(player, identity, TurnOfferId, ProposedOfferDecision, out choiceReceipt, TurnOfferTerms, trimmed))
                    choiceReceipt = "That offer is no longer available.";
                DeliverCompletedReply(displayedOffers != null && !displayedCommitted ? choiceReceipt :
                    BuildOfferDecisionReply(player, TurnOfferId, ProposedOfferDecision, choiceReceipt), player, identity,
                    onNpcReplyText, preserveCommittedReply: displayedOffers != null);
                return;
            }
            string final = string.IsNullOrWhiteSpace(npcReply) ? null : npcReply.Trim();
            // note: A missing reply cannot replay the last line or manufacture an answer to the current turn.
            if (string.IsNullOrWhiteSpace(final) || LastReplyWasFallback)
            {
                IsThinking = false;
                onNpcReplyText?.Invoke(final);
                return;
            }
            YQNpcConversationRewards.PrepareOffer(this, player, stillCurrent, offerLine =>
            {
                if (!stillCurrent()) return;
                DeliverCompletedReply(final + (string.IsNullOrEmpty(offerLine) ? string.Empty : " " + offerLine),
                    player, identity, onNpcReplyText);
            });
        }, stillCurrent);
    }

    private void RememberDelivery(string key)
    {
        // note: Retain only the bounded callback window; durable disposition deduplication remains in the paired world commit store.
        if (key == null || !_heardDeliveries.Add(key)) return;
        _heardDeliveryOrder.Enqueue(key);
        if (_heardDeliveryOrder.Count > 64) _heardDeliveries.Remove(_heardDeliveryOrder.Dequeue());
    }

    // note: The separate editor verification assembly calls this same production reply builder.
    public string BuildOfferDecisionReply(PlayerState player, string offerId, string decision, string receipt)
    {
        // note: Emotion follows a real completed choice. Failure or a retired offer cannot produce a celebration or an invented grant.
        if (YQProfileSaveSystem.Instance != null && YQProfileSaveSystem.Instance.RequiresProfileReload) return receipt;
        var offer = player?.FindOfferById(offerId);
        if (offer == null || offer.offerState != (decision == "accept" ? "accepted" : "declined")) return receipt;
        bool own = offer.context == YQNpcConversationRewards.OfferContext(NpcId);
        bool harsh = HasDisposition("rude", "irritable", "abrasive", "impatient", "hotheaded");
        bool stern = harsh || HasTag("guard") || HasDisposition("stern", "gruff", "blunt");
        string reaction;
        if (decision == "accept")
            reaction = stern ? "Good. I like a clear answer." : HasTag("merchant") ? "A fine choice." :
                HasTag("scholar") || HasTag("lorekeeper") ? "Splendid. A willing mind is welcome." : "I'm glad to hear it.";
        else
            reaction = harsh ? "Hmph. Then we're done with that." : stern ? "Disappointing. Your choice." :
                own ? "A pity. I had hoped you'd say yes." : "A pity. You seemed interested.";
        // note: The source NPC may react personally; a listener to an unattributed offer only acknowledges the player's decision.
        return reaction + " " + receipt;
    }

    private void DeliverCompletedReply(string reply, PlayerState player, string identity, Action<string> completed, bool preserveCommittedReply = false)
    {
        // note: Exactly one current, substantive reply counts as a conversation; fallback, cancellation and panel opening do not.
        IsThinking = false;
        LastReplyLatencySeconds = Time.realtimeSinceStartupAsDouble - _replyStartedAt;
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, this,
            "[NpcConversation] NPC={0} replyReadySeconds={1:F3}", identity, LastReplyLatencySeconds);
        // note: A durable decision requiring canonical reload must not be overwritten by conversation counters, raw projections, or completion callbacks.
        if (YQProfileSaveSystem.Instance != null && YQProfileSaveSystem.Instance.RequiresProfileReload)
        { completed?.Invoke(reply); return; }
        // note: Optional conversation notifications cannot suppress a published social outcome. Keep the actual reply callback outside the catch so a throwing consumer is never retried.
        try {
            YQQuestCompletionDirector.RecordCompletedDialogue(player, identity);
            CommitNpcLine(reply);
            PlayerStateManager.Instance?.Save();
            FindFirstObjectByType<YQInvestorDirector>()?.NotifyDialogueCompleted(identity);
        } catch (Exception exception) when (preserveCommittedReply) { Debug.LogException(exception); }
        completed?.Invoke(reply);
    }

    internal bool TryReceiveSocialReport(NpcDialogueAgent reporter, PlayerState player, WorldState world, string incidentId, string sourceKnowledgeId,
        Func<bool> stillCurrent, out YQSocialAidDomain.ReportDelivery delivery, out string failure)
    {
        // note: Only an active receiving agent accepts this explicit one-recipient NPC auditory route. Persistence and knowledge are published together afterward.
        return YQNpcSocialInteraction.Receive(this, reporter, player, world, incidentId, sourceKnowledgeId, stillCurrent, out delivery, out failure);
    }
    public void CommitPlayerLine(string text)
    {
        AppendTurn("player", text);
    }

    public void CommitNpcLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        LastNpcLine = text.Trim();
        AppendTurn("npc", LastNpcLine);
    }

    public void ClearRecent()
    {
        // note: Clearing a transcript also cancels ownership of a reply built from that transcript.
        _replySequence++;
        EnsureSessionLoaded();
        _session.recentTurns.Clear();
        LastNpcLine = string.Empty;
        IsThinking = false;
        SaveSession();
        TranscriptChanged?.Invoke();
    }

    public string RenderRecentDialogue(int maxLines = 10)
    {
        List<DialogueTurn> turns = GetRecentTurnsSnapshot(maxLines);
        if (turns.Count == 0)
            return "<none>\n";

        StringBuilder sb = new StringBuilder(256);
        for (int i = 0; i < turns.Count; i++)
        {
            DialogueTurn turn = turns[i];
            if (turn == null || string.IsNullOrWhiteSpace(turn.text))
                continue;

            sb.Append(string.IsNullOrWhiteSpace(turn.speaker) ? "npc" : turn.speaker);
            sb.Append(": ");
            sb.Append(turn.text);
            sb.Append('\n');
        }

        return sb.ToString();
    }

    public List<DialogueTurn> GetRecentTurnsSnapshot(int maxTurns = 24)
    {
        EnsureSessionLoaded();
        int take = Mathf.Clamp(maxTurns, 1, 256);
        List<DialogueTurn> result = new List<DialogueTurn>(take);
        int count = _session.recentTurns != null ? _session.recentTurns.Count : 0;
        int start = Mathf.Max(0, count - take);

        for (int i = start; i < count; i++)
        {
            DialogueTurn turn = _session.recentTurns[i];
            if (turn == null || string.IsNullOrWhiteSpace(turn.text))
                continue;

            result.Add(new DialogueTurn
            {
                speaker = string.IsNullOrWhiteSpace(turn.speaker) ? "npc" : turn.speaker.Trim().ToLowerInvariant(),
                text = turn.text.Trim()
            });
        }

        return result;
    }

    public IReadOnlyList<DialogueTurn> GetRecentTurnsReadOnly()
    {
        EnsureSessionLoaded();
        return _session.recentTurns;
    }

    public List<string> GetRecentNpcLines(int maxLines)
    {
        return GetRecentBySpeaker("npc", maxLines);
    }

    public List<string> GetRecentPlayerLines(int maxLines)
    {
        return GetRecentBySpeaker("player", maxLines);
    }

    public bool HasTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        List<string> tags = GetTags();
        for (int i = 0; i < tags.Count; i++)
        {
            if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public string GetPrimaryRoleLabel()
    {
        if (HasTag("merchant")) return "merchant";
        if (HasTag("warden") || HasTag("guard")) return "warden";
        if (HasTag("lorekeeper") || HasTag("scholar")) return "lorekeeper";
        if (HasTag("quest_giver") || HasTag("guide") || HasTag("mentor")) return "guide";
        if (HasTag("healer")) return "healer";
        return "local resident";
    }

    public string BuildPersonaBlock()
    {
        // note: This provider intentionally exposes only public identity and role facts; session transcript and private NPC knowledge never enter the model prompt here.
        StringBuilder sb = new StringBuilder(640);
        sb.AppendLine("name: " + NpcName);
        sb.AppendLine("id: " + NpcId);
        sb.AppendLine("job: " + GetPrimaryRoleLabel());
        if (!string.IsNullOrWhiteSpace(personaSummary))
            sb.AppendLine("persona: " + personaSummary.Trim());

        List<string> tags = GetTags();
        sb.AppendLine("tags: " + (tags.Count > 0 ? string.Join(", ", tags) : "<none>"));

        if (_entityInfo != null)
        {
            sb.AppendLine("faction: " + Safe(_entityInfo.factionId, "none"));
            sb.AppendLine("hostility: " + _entityInfo.hostility);
            sb.AppendLine("level: " + _entityInfo.level);
        }

        sb.AppendLine("tone_rules: " + BuildToneRules());
        // note: CaptureFacts below already carries this recipient's saved affinity and admitted knowledge; do not duplicate that evidence or expose other NPCs' memories.
        sb.AppendLine("available_conversation_actions: verbal_response_only; no speech-triggered combat, service lock, guard dispatch or retreat is registered.");
        sb.AppendLine("conversation_facts: " + YQNpcConversationRewards.CaptureFacts(PlayerStateManager.Instance?.state,
            WorldStateManager.Instance?.State, NpcId));
        var choice = PlayerStateManager.Instance?.state?.FindOfferById(TurnOfferId);
        if (choice != null && choice.IsPending)
            sb.AppendLine("presented_choice: " + Newtonsoft.Json.JsonConvert.SerializeObject(new { choice.offerKind, choice.name, choice.description, state = "pending" }));
        sb.AppendLine("identity_rules: Always speak as " + NpcName + ", the " + GetPrimaryRoleLabel() + ". Never sound like narration, UI, or an assistant.");
        return sb.ToString();
    }

    public string BuildFallback(string playerMessage)
    {
        // note: These bounded outage lines use public identity, tone and the current message only. They never assert lore, inventory, quests or unseen events.
        string lower = (playerMessage ?? string.Empty).ToLowerInvariant();
        int variation = (int)((uint)_fallbackReplySequence++ % 3u);
        bool harsh = HasDisposition("rude", "irritable", "abrasive", "impatient", "hotheaded");
        bool stern = harsh || HasTag("warden") || HasTag("guard") || HasDisposition("stern", "gruff", "blunt");
        bool scholarly = HasTag("lorekeeper") || HasTag("scholar");
        bool merchant = HasTag("merchant");
        if (lower.Contains("who are you") || lower.Contains("your name"))
            return "I'm " + NpcName + ". " + (stern ? "Speak your mind." : scholarly ? "Curiosity makes a fine introduction." : "And what should I call you?");
        if (lower.Contains("thank"))
            return variation == 0 ? "You're welcome." : variation == 1 ? "Gladly." : "No need for thanks.";
        if (lower == "hi" || lower == "hello" || lower.StartsWith("hello ") || lower.StartsWith("hey ") || lower == "hey")
            return stern ? "I'm listening. What is it?" : merchant ? "Well met. What catches your interest?" : "Well met. What's on your mind?";
        if (lower.Contains("afraid") || lower.Contains("scared") || lower.Contains("sad") || lower.Contains("lonely"))
            return stern ? "Take a breath. You don't have to swallow every feeling at once." : "That sounds heavy. You can speak plainly with me.";
        if (lower.Contains("goodbye") || lower == "bye") return stern ? "Keep your wits about you." : "Until we speak again.";
        // note: An unavailable model supplies no evidence of misunderstood words. Callers display technical status instead of manufacturing clarification speech.
        return string.Empty;
    }

    private bool HasDisposition(params string[] words)
    {
        // note: Outage reactions use the accepted persona as well as tags; an NPC's job alone does not make them rude or gentle.
        foreach (string word in words)
            if (HasTag(word) || System.Text.RegularExpressions.Regex.IsMatch(personaSummary ?? string.Empty, @"\b" + word + @"\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return true;
        return false;
    }

    private string BuildToneRules()
    {
        if (HasTag("warden") || HasTag("stern") || HasTag("rude"))
            return "Blunt, disciplined, short answers, no softness, no jokes.";
        if (HasTag("lorekeeper") || HasTag("scholar"))
            return "Precise, calm, grounded, informative without breaking character.";
        if (HasTag("guide") || HasTag("quest_giver") || HasTag("mentor"))
            return "Practical and concise; match the accepted temperament without turning every remark into an instruction.";
        if (HasTag("merchant"))
            return "Transactional, sharp, practical, never poetic.";
        return "Grounded, concise, in-world; keep the accepted species, temperament and speaking style.";
    }

    private void ResolveIdentity()
    {
        if (_entityInfo == null)
        {
            _entityInfo = GetComponent<EntityInfo>();
            if (_entityInfo == null)
                _entityInfo = GetComponentInChildren<EntityInfo>();
        }

        if (_entityInfo != null)
        {
            if (string.IsNullOrWhiteSpace(npcId) || string.Equals(npcId, "npc_unknown", StringComparison.OrdinalIgnoreCase))
                npcId = _entityInfo.entityId;
            if (string.IsNullOrWhiteSpace(npcName) || string.Equals(npcName, "NPC", StringComparison.OrdinalIgnoreCase))
                npcName = _entityInfo.displayName;
        }

        if (string.IsNullOrWhiteSpace(npcId))
            npcId = "npc_unknown";
        if (string.IsNullOrWhiteSpace(npcName))
            npcName = "NPC";
    }

    private void RebindSessionIfNeeded()
    {
        string currentId = NpcId;
        if (!_sessionLoaded)
            return;
        if (string.Equals(_loadedSessionNpcId, currentId, StringComparison.OrdinalIgnoreCase) &&
            ReferenceEquals(_loadedSessionPlayer, PlayerStateManager.Instance?.state))
            return;

        _sessionLoaded = false;
        _session = null;
        LastNpcLine = string.Empty;
        EnsureSessionLoaded();
    }

    private void AppendTurn(string speaker, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        EnsureSessionLoaded();
        _session.npcEntityId = NpcId;
        _session.maxTurns = Mathf.Clamp(maxMemoryLines, 4, 256);
        _session.recentTurns ??= new List<DialogueTurn>(16);
        _session.recentTurns.Add(new DialogueTurn
        {
            speaker = string.IsNullOrWhiteSpace(speaker) ? "npc" : speaker.Trim().ToLowerInvariant(),
            text = text.Trim()
        });

        int overflow = _session.recentTurns.Count - _session.maxTurns;
        if (overflow > 0)
            _session.recentTurns.RemoveRange(0, overflow);

        SaveSession();
        TranscriptChanged?.Invoke();
    }

    private List<string> GetRecentBySpeaker(string speaker, int maxLines)
    {
        EnsureSessionLoaded();
        List<string> output = new List<string>(Mathf.Clamp(maxLines, 1, 32));
        if (_session.recentTurns == null)
            return output;

        for (int i = _session.recentTurns.Count - 1; i >= 0 && output.Count < maxLines; i--)
        {
            DialogueTurn turn = _session.recentTurns[i];
            if (turn == null || string.IsNullOrWhiteSpace(turn.text))
                continue;
            if (string.Equals(turn.speaker, speaker, StringComparison.OrdinalIgnoreCase))
                output.Add(turn.text.Trim());
        }

        output.Reverse();
        return output;
    }

    private void EnsureSessionLoaded()
    {
        ResolveIdentity();
        string currentId = NpcId;
        PlayerState currentPlayer = PlayerStateManager.Instance?.state;
        if (_sessionLoaded && string.Equals(_loadedSessionNpcId, currentId, StringComparison.OrdinalIgnoreCase) &&
            ReferenceEquals(_loadedSessionPlayer, currentPlayer))
            return;

        // note: The same authored Vey object may survive a profile load; discard its previous in-memory conversation too.
        _replySequence++;
        IsThinking = false;
        _loadedSessionPlayer = currentPlayer;
        _heardDeliveries.Clear(); _heardDeliveryOrder.Clear();
        LastReplyDelivery = "measured";
        _nextRewardConsideration = 0f;
        _sessionLoaded = true;
        _loadedSessionNpcId = currentId;

        if (persistTranscriptAcrossSessions && currentPlayer != null && NpcDialogueSessionStore.TryLoad(currentId, out NpcDialogueSession loaded) && loaded != null)
        {
            _session = loaded;
            _session.npcEntityId = currentId;
            _session.maxTurns = Mathf.Clamp(maxMemoryLines, 4, 256);
            _session.recentTurns ??= new List<DialogueTurn>(16);
        }
        else
        {
            _session = new NpcDialogueSession
            {
                npcEntityId = currentId,
                maxTurns = Mathf.Clamp(maxMemoryLines, 4, 256),
                recentTurns = new List<DialogueTurn>(16)
            };
        }

        LastNpcLine = string.Empty;
        for (int i = _session.recentTurns.Count - 1; i >= 0; i--)
        {
            DialogueTurn turn = _session.recentTurns[i];
            if (turn != null && string.Equals(turn.speaker, "npc", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(turn.text))
            {
                LastNpcLine = turn.text.Trim();
                break;
            }
        }
    }

    private void SaveSession()
    {
        if (!persistTranscriptAcrossSessions || _session == null || _loadedSessionPlayer == null ||
            !ReferenceEquals(_loadedSessionPlayer, PlayerStateManager.Instance?.state))
            return;

        _session.npcEntityId = NpcId;
        _session.maxTurns = Mathf.Clamp(maxMemoryLines, 4, 256);
        _session.recentTurns ??= new List<DialogueTurn>(16);
        NpcDialogueSessionStore.TrySave(NpcId, _session);
    }

    private List<string> GetTags()
    {
        List<string> tags = new List<string>();

        if (tagsOverride != null)
        {
            for (int i = 0; i < tagsOverride.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(tagsOverride[i]))
                    tags.Add(tagsOverride[i].Trim());
            }
        }

        if (tags.Count == 0 && _entityInfo != null && _entityInfo.tags != null)
        {
            for (int i = 0; i < _entityInfo.tags.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(_entityInfo.tags[i]))
                    tags.Add(_entityInfo.tags[i].Trim());
            }
        }

        return tags;
    }

    private static string Safe(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
