using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Morse Sender UI — Operating mode UI controller.
///
/// Responsibilities:
///   • Displays the current raw buffer (in-progress character) as dots/dashes.
///   • Spawns a CharacterCard prefab into CardsContent whenever the user
///     commits a character via OperatingManager.
///   • Wires the five buttons (Next Character / Next Word / Send /
///     Clear Current / Reset All) to OperatingManager methods.
///   • Visualises word breaks by enabling a marker on the card preceding the break.
///
/// This script talks ONLY to OperatingManager, never directly to
/// SimpleMorseCodeSystem. OperatingManager is the single source of truth.
/// </summary>
public class MorseSenderUI : MonoBehaviour
{
    // ==================== Inspector: Current character area ====================
    [Header("Current Character Area")]
    [Tooltip("TMP text that shows the in-progress dot/dash buffer")]
    public TextMeshProUGUI currentBufferText;

    [Tooltip("Placeholder text shown when the current buffer is empty")]
    public string emptyBufferPlaceholder = "<color=#888888><i>[press the key...]</i></color>";

    // ==================== Inspector: Committed cards area ====================
    [Header("Committed Cards Area")]
    [Tooltip("Parent transform where CharacterCard prefabs are spawned. " +
             "Should be the CardsContent object inside the ScrollView.")]
    public Transform cardsContent;

    [Tooltip("The CharacterCard prefab to instantiate per committed character")]
    public GameObject characterCardPrefab;

    [Tooltip("Optional: ScrollRect for auto-scrolling to the newest card")]
    public ScrollRect cardsScrollRect;

    // ==================== Inspector: Buttons ====================
    [Header("Buttons")]
    public Button nextCharButton;
    public Button nextWordButton;
    public Button sendButton;
    public Button clearCurrentButton;
    public Button resetAllButton;

    // ==================== Internal ====================
    private OperatingManager op;
    private List<GameObject> spawnedCards = new List<GameObject>();

    // ==================== Unity Lifecycle ====================

    void Start()
    {
        op = FindObjectOfType<OperatingManager>();

        if (op == null)
        {
            Debug.LogError("[MorseSenderUI] OperatingManager not found in scene!");
            return;
        }

        op.OnRawBufferChanged += HandleRawBufferChanged;
        op.OnCharacterCommitted += HandleCharacterCommitted;
        op.OnWordBreakAdded += HandleWordBreakAdded;
        op.OnSessionReset += HandleSessionReset;
        op.OnMessageSent += HandleMessageSent;

        WireButtons();
        InitializeUI();
    }

    void OnDestroy()
    {
        if (op != null)
        {
            op.OnRawBufferChanged -= HandleRawBufferChanged;
            op.OnCharacterCommitted -= HandleCharacterCommitted;
            op.OnWordBreakAdded -= HandleWordBreakAdded;
            op.OnSessionReset -= HandleSessionReset;
            op.OnMessageSent -= HandleMessageSent;
        }
        UnwireButtons();
    }

    // ==================== Button Wiring ====================

    void WireButtons()
    {
        if (nextCharButton != null) nextCharButton.onClick.AddListener(OnNextCharClicked);
        if (nextWordButton != null) nextWordButton.onClick.AddListener(OnNextWordClicked);
        if (sendButton != null) sendButton.onClick.AddListener(OnSendClicked);
        if (clearCurrentButton != null) clearCurrentButton.onClick.AddListener(OnClearCurrentClicked);
        if (resetAllButton != null) resetAllButton.onClick.AddListener(OnResetAllClicked);
    }

    void UnwireButtons()
    {
        if (nextCharButton != null) nextCharButton.onClick.RemoveListener(OnNextCharClicked);
        if (nextWordButton != null) nextWordButton.onClick.RemoveListener(OnNextWordClicked);
        if (sendButton != null) sendButton.onClick.RemoveListener(OnSendClicked);
        if (clearCurrentButton != null) clearCurrentButton.onClick.RemoveListener(OnClearCurrentClicked);
        if (resetAllButton != null) resetAllButton.onClick.RemoveListener(OnResetAllClicked);
    }

    void OnNextCharClicked() { op?.CommitCharacter(); }
    void OnNextWordClicked() { op?.CommitWord(); }
    void OnSendClicked() { op?.FinalizeAndSend(); }
    void OnClearCurrentClicked() { op?.ClearCurrent(); }
    void OnResetAllClicked() { op?.ResetAll(); }

    // ==================== Initial UI State ====================

    void InitializeUI()
    {
        UpdateCurrentBufferDisplay("");
        ClearAllCards();
    }

    // ==================== Event Handlers ====================

    void HandleRawBufferChanged(string rawBuffer)
    {
        UpdateCurrentBufferDisplay(rawBuffer);
    }

    void HandleCharacterCommitted(string character, int index)
    {
        SpawnCard(character);
    }

    void HandleWordBreakAdded(int afterIndex)
    {
        if (afterIndex < 0 || afterIndex >= spawnedCards.Count) return;

        GameObject card = spawnedCards[afterIndex];
        if (card == null) return;

        Transform mark = card.transform.Find("WordBreakMark");
        if (mark != null)
        {
            mark.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[MorseSenderUI] CharacterCard prefab has no child named 'WordBreakMark'.");
        }
    }

    void HandleSessionReset()
    {
        ClearAllCards();
        UpdateCurrentBufferDisplay("");
    }

    void HandleMessageSent(string fullSequence)
    {
        Debug.Log($"[MorseSenderUI] Message sent: {fullSequence}");
    }

    // ==================== Display Helpers ====================

    void UpdateCurrentBufferDisplay(string buffer)
    {
        if (currentBufferText == null) return;

        if (string.IsNullOrEmpty(buffer))
        {
            currentBufferText.text = emptyBufferPlaceholder;
        }
        else
        {
            string formatted = buffer
                .Replace(".", "<color=#D4A24C>.</color>")
                .Replace("-", "<color=#A84632>-</color>");
            currentBufferText.text = formatted;
        }
    }

    void SpawnCard(string character)
    {
        if (characterCardPrefab == null || cardsContent == null)
        {
            Debug.LogError("[MorseSenderUI] characterCardPrefab or cardsContent not assigned!");
            return;
        }

        GameObject card = Instantiate(characterCardPrefab, cardsContent);
        card.SetActive(true);

        // Find the MorseText child by name (not the WordBreakMark)
        Transform morseTextTr = card.transform.Find("MorseText");
        if (morseTextTr != null)
        {
            var txt = morseTextTr.GetComponent<TextMeshProUGUI>();
            if (txt != null) txt.text = character;
        }
        else
        {
            Debug.LogWarning("[MorseSenderUI] CharacterCard prefab has no child named 'MorseText'.");
        }

        Transform mark = card.transform.Find("WordBreakMark");
        if (mark != null) mark.gameObject.SetActive(false);

        spawnedCards.Add(card);

        if (cardsScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            cardsScrollRect.horizontalNormalizedPosition = 1f;
        }
    }

    void ClearAllCards()
    {
        foreach (var card in spawnedCards)
        {
            if (card != null) Destroy(card);
        }
        spawnedCards.Clear();
    }
}
