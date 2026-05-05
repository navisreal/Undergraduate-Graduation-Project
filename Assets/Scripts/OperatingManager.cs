using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Operating Manager — Controls the Sending Room operating flow.
///
/// Responsibilities:
///   • Subscribes to the raw morse stream from SimpleMorseCodeSystem.
///   • Maintains the authoritative segmentation: a list of committed characters
///     and a set of word-break positions, driven entirely by user button clicks
///     (Next Character / Next Word / Send).
///   • Relays events to MorseSenderUI so the UI doesn't talk to the low-level
///     morse system directly.
///
/// Design notes:
///   • Auto-spacing in SimpleMorseCodeSystem is preserved (for the "real telegraph
///     feel" in the raw display area). When the user clicks Next Character, those
///     auto-inserted spaces are stripped and the remaining dots/dashes are
///     committed as one character. The raw buffer is then cleared.
///   • This manager does NOT validate against the morse code table. Invalid
///     patterns are accepted as-is and will be revealed when decoded in the
///     Receiving Room. (Per design: decoding only happens in Receiving Room.)
/// </summary>
public class OperatingManager : MonoBehaviour
{
    // ==================== Inspector ====================
    [Header("Scene Transition")]
    [Tooltip("Scene to load when Send is pressed")]
    public string receivingRoomSceneName = "ReceivingRoom";

    [Header("Key Highlight")]
    [Tooltip("HandleHighlight component on the knob. Enabled while in Operating mode, disabled when sending.")]
    public HandleHighlight knobHighlight;

    [Header("Debug")]
    [Tooltip("Print verbose logs to the console")]
    public bool verboseLogging = true;

    // ==================== Internal State ====================
    private SimpleMorseCodeSystem morseSystem;

    // The authoritative committed sequence
    private List<string> committedCharacters = new List<string>();
    private HashSet<int> wordBreaksAfter = new HashSet<int>();

    // ==================== Events (UI subscribes to these) ====================

    /// <summary>Raw buffer (current in-progress character) changed.
    /// This is just a relay from SimpleMorseCodeSystem.OnSequenceUpdated.</summary>
    public delegate void RawBufferChanged(string rawBuffer);
    public event RawBufferChanged OnRawBufferChanged;

    /// <summary>A character was committed via Next Character (or Next Word, or Send).
    /// `character` is the dot/dash string with spaces stripped.
    /// `index` is its position in the committed list.</summary>
    public delegate void CharacterCommitted(string character, int index);
    public event CharacterCommitted OnCharacterCommitted;

    /// <summary>A word break was added after the given character index.</summary>
    public delegate void WordBreakAdded(int afterIndex);
    public event WordBreakAdded OnWordBreakAdded;

    /// <summary>Everything was wiped (Reset All button).</summary>
    public delegate void SessionReset();
    public event SessionReset OnSessionReset;

    /// <summary>The full message is finalized and we're about to leave the scene.</summary>
    public delegate void MessageSent(string fullSequence);
    public event MessageSent OnMessageSent;

    /// <summary>Status text for the UI status label (e.g. "Character committed").</summary>
    public delegate void StatusMessage(string message);
    public event StatusMessage OnStatusMessage;

    // ==================== Unity Lifecycle ====================
    void Start()
    {
        morseSystem = SimpleMorseCodeSystem.Instance;

        if (morseSystem == null)
        {
            Debug.LogError("[OperatingManager] SimpleMorseCodeSystem.Instance not found! " +
                           "Make sure it exists in the scene and runs Awake before this Start.");
            return;
        }

        // Subscribe to the raw stream so we can relay it to the UI
        morseSystem.OnSequenceUpdated += HandleRawBufferUpdated;

        Log("OperatingManager initialized. Waiting for input.");
        EmitStatus("Ready");
    }

    /// <summary>
    /// Called by TrainingManager when the user transitions from Training to Operating mode
    /// in the same scene. OperatingManager's Start() only runs once at scene load, so this
    /// method re-initializes the "session" state (currently just the knob highlight).
    /// </summary>
    public void BeginOperatingSession()
    {
        if (knobHighlight != null) knobHighlight.enabled = true;
        Log("Operating session begun (knob highlight on).");
        EmitStatus("Ready");
    }


    void OnDestroy()
    {
        if (morseSystem != null)
        {
            morseSystem.OnSequenceUpdated -= HandleRawBufferUpdated;
        }
    }

    // ==================== Raw Stream Relay ====================
    void HandleRawBufferUpdated(string rawBuffer)
    {
        OnRawBufferChanged?.Invoke(rawBuffer);
    }

    // ==================== Public API: Buttons ====================

    /// <summary>
    /// "Next Character" button.
    /// Reads the current raw buffer, strips auto-spaces, commits as one character,
    /// then clears the raw buffer.
    /// </summary>
    public void CommitCharacter()
    {
        if (morseSystem == null) return;

        string raw = morseSystem.GetMorseSequence();
        string cleaned = StripSpaces(raw);

        if (string.IsNullOrEmpty(cleaned))
        {
            Log("CommitCharacter ignored: nothing in raw buffer.");
            EmitStatus("Nothing to commit");
            return;
        }

        committedCharacters.Add(cleaned);
        int newIndex = committedCharacters.Count - 1;

        Log($"Committed character #{newIndex}: [{cleaned}]");

        // Clear the raw buffer for the next character
        morseSystem.ResetInput();

        OnCharacterCommitted?.Invoke(cleaned, newIndex);
        EmitStatus($"Character {newIndex + 1} committed");
    }

    /// <summary>
    /// "Next Word" button.
    /// Commits the current character (if any) and marks a word break after it.
    /// </summary>
    public void CommitWord()
    {
        // Try to commit any pending character first
        if (morseSystem != null && !string.IsNullOrEmpty(StripSpaces(morseSystem.GetMorseSequence())))
        {
            CommitCharacter();
        }

        if (committedCharacters.Count == 0)
        {
            Log("CommitWord ignored: no characters yet.");
            EmitStatus("Nothing to end word");
            return;
        }

        int afterIndex = committedCharacters.Count - 1;

        if (wordBreaksAfter.Contains(afterIndex))
        {
            Log($"CommitWord ignored: word break already exists after index {afterIndex}.");
            EmitStatus("Word break already here");
            return;
        }

        wordBreaksAfter.Add(afterIndex);
        Log($"Word break added after character #{afterIndex}");

        OnWordBreakAdded?.Invoke(afterIndex);
        EmitStatus("Word ended");
    }

    /// <summary>
    /// "Send" button.
    /// Commits any pending character, builds the full sequence, stores it
    /// in SentMessageData, and loads the receiving room scene.
    /// </summary>
    public void FinalizeAndSend()
    {
        if (morseSystem != null && !string.IsNullOrEmpty(StripSpaces(morseSystem.GetMorseSequence())))
        {
            CommitCharacter();
        }

        if (committedCharacters.Count == 0)
        {
            Log("FinalizeAndSend ignored: nothing to send.");
            EmitStatus("Nothing to send");
            return;
        }

        string fullSequence = BuildFullSequence();
        Log($"SENDING: [{fullSequence}]");

        // Store everything for the receiving room
        SentMessageData.LastSentSequence = fullSequence;
        SentMessageData.LastSentCharacters = new List<string>(committedCharacters);
        SentMessageData.LastSentWordBreaks = new HashSet<int>(wordBreaksAfter);

        OnMessageSent?.Invoke(fullSequence);
        EmitStatus("Sending...");

        if (string.IsNullOrEmpty(receivingRoomSceneName))
        {
            Debug.LogError("[OperatingManager] receivingRoomSceneName is not set! Cannot transition.");
            return;
        }

        // Turn off the key highlight before leaving the scene.
        if (knobHighlight != null) knobHighlight.enabled = false;

        SceneManager.LoadScene(receivingRoomSceneName);
    }

    /// <summary>
    /// "Clear Current" button (next to the current-character area).
    /// Wipes only the in-progress raw buffer. Committed characters are untouched.
    /// </summary>
    public void ClearCurrent()
    {
        if (morseSystem == null) return;

        morseSystem.ResetInput();
        Log("Current character cleared.");
        EmitStatus("Current cleared");
    }

    /// <summary>
    /// "Reset All" button (next to the committed-cards area).
    /// Wipes everything: committed characters, word breaks, and the raw buffer.
    /// </summary>
    public void ResetAll()
    {
        committedCharacters.Clear();
        wordBreaksAfter.Clear();

        if (morseSystem != null)
        {
            morseSystem.ResetInput();
        }

        Log("Session reset. All data cleared.");
        OnSessionReset?.Invoke();
        EmitStatus("Reset");
    }

    // ==================== Public Queries (for the UI) ====================
    public IReadOnlyList<string> GetCommittedCharacters() => committedCharacters;
    public bool HasWordBreakAfter(int index) => wordBreaksAfter.Contains(index);
    public int GetCommittedCount() => committedCharacters.Count;

    /// <summary>
    /// Build the canonical full sequence string for the receiving room.
    /// Format: characters separated by single space, word breaks by triple space.
    /// Example: "... --- ...   -.-"  (SOS [word] K)
    /// </summary>
    public string BuildFullSequence()
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < committedCharacters.Count; i++)
        {
            sb.Append(committedCharacters[i]);
            if (i < committedCharacters.Count - 1)
            {
                sb.Append(wordBreaksAfter.Contains(i) ? "   " : " ");
            }
        }
        return sb.ToString();
    }

    // ==================== Helpers ====================
    static string StripSpaces(string s)
    {
        return string.IsNullOrEmpty(s) ? "" : s.Replace(" ", "");
    }

    void EmitStatus(string msg)
    {
        OnStatusMessage?.Invoke(msg);
    }

    void Log(string msg)
    {
        if (verboseLogging) Debug.Log($"[OperatingManager] {msg}");
    }
}

/// <summary>
/// Static holder used to pass the finalized message from the Sending Room
/// to the Receiving Room across a SceneManager.LoadScene call.
/// </summary>
public static class SentMessageData
{
    public static string LastSentSequence = "";
    public static List<string> LastSentCharacters = new List<string>();
    public static HashSet<int> LastSentWordBreaks = new HashSet<int>();

    public static void Clear()
    {
        LastSentSequence = "";
        LastSentCharacters = new List<string>();
        LastSentWordBreaks = new HashSet<int>();
    }
}
