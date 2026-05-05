using UnityEngine;
using System.Collections;
using System.Text;

/// <summary>
/// Simple Morse Code System
/// Recognizes and displays: dot(.) / dash(-) / character space / word space
/// Output example: ...---... or ... --- ...
/// </summary>
public class SimpleMorseCodeSystem : MonoBehaviour
{
    public static SimpleMorseCodeSystem Instance { get; private set; }

    // ==================== Speed Settings (Inspector Only) ====================
    [Header("Speed Settings")]
    [Tooltip("Transmission speed (Words Per Minute)")]
    [Range(5, 25)]
    public float wordsPerMinute = 10f;

    [Header("Calculated Timing Parameters (Read Only)")]
    [SerializeField] private float unitTime;
    [SerializeField] private float dotMaxTime;
    [SerializeField] private float dashMinTime;
    [SerializeField] private float charSpaceTime;
    [SerializeField] private float wordSpaceTime;

    [Header("Timeout Settings")]
    [Tooltip("Input timeout duration (seconds)")]
    public float inputTimeoutDuration = 10f;

    // ==================== Internal State ====================
    private StringBuilder morseSequence = new StringBuilder();

    private float lastSignalTime = 0f;
    private float lastActivityTime = 0f;
    private Coroutine spacingCoroutine;
    private Coroutine timeoutCoroutine;

    private bool isInputActive = false;
    private bool hasCharSpace = false;
    private bool isKeyCurrentlyPressed = false;  // NEW: Track if key is being pressed

    // ==================== Events ====================
    public delegate void MorseSequenceUpdated(string sequence);
    public event MorseSequenceUpdated OnSequenceUpdated;

    public delegate void SignalAdded(string signalType);
    public event SignalAdded OnSignalAdded;

    public delegate void SpaceAdded(SpaceType spaceType);
    public event SpaceAdded OnSpaceAdded;

    public delegate void ErrorOccurred(ErrorType errorType, string message);
    public event ErrorOccurred OnErrorOccurred;

    public delegate void StatusChanged(string status);
    public event StatusChanged OnStatusChanged;

    public enum SpaceType
    {
        CharacterSpace,
        WordSpace
    }

    public enum ErrorType
    {
        Timeout
    }

    // ==================== Unity Lifecycle ====================
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        CalculateTimings();
        lastActivityTime = Time.time;
        
        Debug.Log("Morse Code System Initialized");
        Debug.Log($"  Speed: {wordsPerMinute} WPM");
        Debug.Log($"  Unit time: {unitTime * 1000:F0}ms");
        Debug.Log($"  Dot: < {dotMaxTime * 1000:F0}ms");
        Debug.Log($"  Dash: >= {dashMinTime * 1000:F0}ms");
        Debug.Log($"  Char space: > {charSpaceTime * 1000:F0}ms");
        Debug.Log($"  Word space: > {wordSpaceTime * 1000:F0}ms");
    }

    void OnValidate()
    {
        CalculateTimings();
    }

    // ==================== Timing Calculation ====================
    void CalculateTimings()
    {
        // Formula: 1 unit = 1200 / WPM (milliseconds)
        unitTime = 1.2f / wordsPerMinute;

        dotMaxTime = unitTime * 2f;
        dashMinTime = unitTime * 2f;
        charSpaceTime = unitTime * 2.5f;
        wordSpaceTime = unitTime * 5f;
    }

    // ==================== Public Interface ====================

    public float GetDotMaxTime() => dotMaxTime;
    public float GetDashMinTime() => dashMinTime;
    public float GetUnitTime() => unitTime;

    /// <summary>
    /// Called when key is pressed down (before signal is determined)
    /// Stops spacing detection to prevent unwanted spaces
    /// </summary>
    public void OnKeyPressed()
    {
        isKeyCurrentlyPressed = true;
        
        // Stop spacing detection immediately when user starts pressing
        if (spacingCoroutine != null)
        {
            StopCoroutine(spacingCoroutine);
            spacingCoroutine = null;
        }
        
        // Update activity time
        lastActivityTime = Time.time;
        
        Debug.Log("Key pressed - spacing detection paused");
    }

    /// <summary>
    /// Called when key is released (signal is now determined)
    /// </summary>
    public void OnKeyReleased()
    {
        isKeyCurrentlyPressed = false;
        Debug.Log("Key released");
    }

    /// <summary>
    /// Add a signal (dot or dash)
    /// </summary>
    public void AddSignal(bool isDash)
    {
        lastActivityTime = Time.time;
        isInputActive = true;
        hasCharSpace = false;
        isKeyCurrentlyPressed = false;

        // Stop any existing spacing coroutine
        if (spacingCoroutine != null)
        {
            StopCoroutine(spacingCoroutine);
            spacingCoroutine = null;
        }

        RestartTimeoutCheck();

        string symbol = isDash ? "-" : ".";
        morseSequence.Append(symbol);

        string signalName = isDash ? "DASH" : "DOT";
        Debug.Log($"Signal: {signalName} | Sequence: {morseSequence}");

        OnSignalAdded?.Invoke(symbol);
        OnSequenceUpdated?.Invoke(morseSequence.ToString());
        OnStatusChanged?.Invoke($"Input: {signalName}");

        lastSignalTime = Time.time;
        spacingCoroutine = StartCoroutine(CheckForSpacing());
    }

    /// <summary>
    /// Spacing detection coroutine
    /// </summary>
    IEnumerator CheckForSpacing()
    {
        // Wait for character spacing time
        yield return new WaitForSeconds(charSpaceTime);

        // Check if key is currently being pressed - if so, don't add space
        if (isKeyCurrentlyPressed)
        {
            Debug.Log("Key is pressed - skipping space detection");
            yield break;
        }

        float timeSinceLastSignal = Time.time - lastSignalTime;

        if (timeSinceLastSignal >= charSpaceTime && !hasCharSpace)
        {
            AddCharacterSpace();

            // Continue waiting to check for word space
            yield return new WaitForSeconds(wordSpaceTime - charSpaceTime);

            // Check again if key is pressed
            if (isKeyCurrentlyPressed)
            {
                yield break;
            }

            timeSinceLastSignal = Time.time - lastSignalTime;
            if (timeSinceLastSignal >= wordSpaceTime && isInputActive)
            {
                AddWordSpace();
            }
        }
    }

    void AddCharacterSpace()
    {
        if (morseSequence.Length > 0 && !hasCharSpace)
        {
            char lastChar = morseSequence[morseSequence.Length - 1];
            if (lastChar != ' ')
            {
                morseSequence.Append(" ");
                hasCharSpace = true;

                Debug.Log($"Character space | Sequence: [{morseSequence}]");

                OnSpaceAdded?.Invoke(SpaceType.CharacterSpace);
                OnSequenceUpdated?.Invoke(morseSequence.ToString());
                OnStatusChanged?.Invoke("Character Space");
            }
        }
    }

    void AddWordSpace()
    {
        if (morseSequence.Length > 0)
        {
            string current = morseSequence.ToString();
            if (!current.EndsWith("  "))
            {
                if (!current.EndsWith(" "))
                {
                    morseSequence.Append(" ");
                }
                morseSequence.Append(" ");

                Debug.Log($"Word space | Sequence: [{morseSequence}]");

                OnSpaceAdded?.Invoke(SpaceType.WordSpace);
                OnSequenceUpdated?.Invoke(morseSequence.ToString());
                OnStatusChanged?.Invoke("Word Space");
            }
        }
    }

    // ==================== Timeout Detection ====================

    void RestartTimeoutCheck()
    {
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
        }
        timeoutCoroutine = StartCoroutine(TimeoutCheck());
    }

    IEnumerator TimeoutCheck()
    {
        yield return new WaitForSeconds(inputTimeoutDuration);

        if (isInputActive)
        {
            float timeSinceActivity = Time.time - lastActivityTime;
            if (timeSinceActivity >= inputTimeoutDuration)
            {
                Debug.LogWarning($"Input timeout ({inputTimeoutDuration}s)");
                OnErrorOccurred?.Invoke(ErrorType.Timeout, "Input Timeout\nTry Again");
                OnStatusChanged?.Invoke("Timeout - Try Again");
                isInputActive = false;
            }
        }
    }

    // ==================== Manual Controls ====================

    public void ManualAddCharacterSpace()
    {
        hasCharSpace = false;
        AddCharacterSpace();
    }

    public void ManualAddWordSpace()
    {
        AddWordSpace();
    }

    public string GetMorseSequence()
    {
        return morseSequence.ToString();
    }

    public void ResetInput()
    {
        morseSequence.Clear();
        lastSignalTime = Time.time;
        lastActivityTime = Time.time;
        isInputActive = false;
        hasCharSpace = false;
        isKeyCurrentlyPressed = false;

        if (spacingCoroutine != null) StopCoroutine(spacingCoroutine);
        if (timeoutCoroutine != null) StopCoroutine(timeoutCoroutine);

        Debug.Log("Input Reset");
        OnSequenceUpdated?.Invoke("");
        OnStatusChanged?.Invoke("Reset");
    }

    public void DeleteLastCharacter()
    {
        if (morseSequence.Length > 0)
        {
            morseSequence.Length--;
            
            if (morseSequence.Length == 0 || morseSequence[morseSequence.Length - 1] != ' ')
            {
                hasCharSpace = false;
            }

            Debug.Log($"Deleted | Sequence: [{morseSequence}]");
            OnSequenceUpdated?.Invoke(morseSequence.ToString());
            OnStatusChanged?.Invoke("Deleted");
        }
    }

    public int GetSequenceLength()
    {
        return morseSequence.Length;
    }
}
