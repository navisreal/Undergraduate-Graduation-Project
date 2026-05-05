using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Training Manager - Controls the full training mode flow
/// States: WORD_SELECT → INSTRUCTION(+input) → SUCCESS / FAIL
/// </summary>
public class TrainingManager : MonoBehaviour
{
    public enum TrainingState
    {
        WORD_SELECT,
        INSTRUCTION,
        SUCCESS,
        FAIL
    }

    private TrainingState currentState = TrainingState.WORD_SELECT;
    private string currentWord = "";
    private string correctMorseSequence = "";

    // Morse code lookup for individual letters
    private Dictionary<char, string> morseTable = new Dictionary<char, string>()
    {
        {'A', ".-"},   {'B', "-..."}, {'C', "-.-."},
        {'D', "-.."},  {'E', "."},    {'F', "..-."},
        {'G', "--."},  {'H', "...."}, {'I', ".."},
        {'J', ".---"}, {'K', "-.-"},  {'L', ".-.."},
        {'M', "--"},   {'N', "-."},   {'O', "---"},
        {'P', ".--."}, {'Q', "--.-"}, {'R', ".-."},
        {'S', "..."},  {'T', "-"},    {'U', "..-"},
        {'V', "...-"}, {'W', ".--"},  {'X', "-..-"},
        {'Y', "-.--"}, {'Z', "--.."}
    };

    // ==================== Inspector References ====================

    [Header("Panels")]
    public GameObject wordSelectPanel;
    public GameObject instructionPanel;
    public GameObject successPanel;
    public GameObject failPanel;
    public GameObject backgroundPanel;

    [Header("Word Select Panel")]
    public Button sosButton;
    public Button helloButton;
    public Button byeButton;

    [Header("Instruction Panel")]
    public TextMeshProUGUI instructionWordText;
    public TextMeshProUGUI instructionMorseText;
    public Button playAudioButton;
    public TextMeshProUGUI playButtonLabel;
    public TextMeshProUGUI userInputText;
    public Button exitButton;                   // ← new: exit training
    public TextMeshProUGUI recentSignalText;
    public float signalFlashDuration = 0.5f;

    [Header("Morse Align Display")]
    [Tooltip("The MorseAlignDisplay container with Horizontal Layout Group")]
    public RectTransform morseAlignDisplay;
    [Tooltip("The LetterBlock prefab")]
    public GameObject letterBlockPrefab;

    [Header("Success Panel")]
    public Button trainAnotherButton;
    public Button goToOperatingButton;

    [Header("Fail Panel")]
    public TextMeshProUGUI failMessageText;

    [Header("Key Highlight")]
    [Tooltip("HandleHighlight component on the knob. Enable/disable to highlight the key.")]
    public HandleHighlight knobHighlight;

    [Header("Mode Transition")]
    [Tooltip("Reference to the OperatingManager in the same scene. Used to notify it when the user transitions from Training to Operating mode.")]
    public OperatingManager operatingManager;

    [Header("Audio")]
    public AudioClip sosAudio;
    public AudioClip helloAudio;
    public AudioClip byeAudio;
    private AudioSource audioSource;
    private bool isPlayingAudio = false;

    [Header("Timing")]
    [Tooltip("Seconds after last input before auto-judging")]
    public float judgeDelay = 3f;
    [Tooltip("Seconds to show FAIL message before returning to instruction")]
    public float failDisplayDuration = 2f;

    private SimpleMorseCodeSystem morseSystem;
    private Coroutine judgeCoroutine;
    private bool userHasPressedOnce = false;

    // ==================== Unity Lifecycle ====================

    void Start()
    {
        morseSystem = SimpleMorseCodeSystem.Instance;

        if (morseSystem != null)
        {
            morseSystem.OnSequenceUpdated += OnUserSequenceUpdated;
            morseSystem.OnSignalAdded += ShowRecentSignal;
        }
        else
            Debug.LogError("TrainingManager: SimpleMorseCodeSystem not found!");

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;


        SetupButtons();
        EnterState(TrainingState.WORD_SELECT);
    }

    void SetupButtons()
    {
        if (sosButton != null)
            sosButton.onClick.AddListener(() => SelectWord("SOS"));

        if (helloButton != null)
            helloButton.onClick.AddListener(() => SelectWord("HELLO"));

        if (byeButton != null)
            byeButton.onClick.AddListener(() => SelectWord("BYE"));

        if (playAudioButton != null)
            playAudioButton.onClick.AddListener(OnPlayAudioClicked);

        // Disable keyboard/gamepad navigation on the play button
        if (playAudioButton != null)
        {
            Navigation nav = playAudioButton.navigation;
            nav.mode = Navigation.Mode.None;
            playAudioButton.navigation = nav;
        }

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitClicked);

        if (trainAnotherButton != null)
            trainAnotherButton.onClick.AddListener(OnTrainAnotherClicked);

        if (goToOperatingButton != null)
            goToOperatingButton.onClick.AddListener(OnGoToOperatingClicked);
    }

    void OnDestroy()
    {
        if (morseSystem != null)
            morseSystem.OnSequenceUpdated -= OnUserSequenceUpdated;
            morseSystem.OnSignalAdded -= ShowRecentSignal;
    }

    // ==================== State Machine ====================

    void EnterState(TrainingState newState)
    {
        currentState = newState;
        Debug.Log($"TrainingManager → {newState}");

        SetAllPanelsInactive();

        switch (newState)
        {
            case TrainingState.WORD_SELECT:
                if (wordSelectPanel != null) wordSelectPanel.SetActive(true);
                break;

            case TrainingState.INSTRUCTION:
                if (instructionPanel != null) instructionPanel.SetActive(true);
                if (instructionWordText != null)
                    instructionWordText.text = string.Join("\u2003", currentWord.ToUpper().ToCharArray());
                // Display morse with spaces for readability
                if (instructionMorseText != null) instructionMorseText.text = correctMorseSequence;
                if (playButtonLabel != null) playButtonLabel.text = "Play Standard Audio";
                if (userInputText != null) userInputText.text = "Your input: ";
                GenerateMorseAlignDisplay(currentWord);
                HighlightKnob(true);
                userHasPressedOnce = false;
                morseSystem?.ResetInput();
                break;

            case TrainingState.SUCCESS:
                if (successPanel != null) successPanel.SetActive(true);
                break;

            case TrainingState.FAIL:
                if (failPanel != null) failPanel.SetActive(true);
                if (failMessageText != null) failMessageText.text = "Try Again~";
                StartCoroutine(ReturnToInstructionAfterDelay());
                break;
        }
    }

    void SetAllPanelsInactive()
    {
        HighlightKnob(false);
        if (wordSelectPanel != null) wordSelectPanel.SetActive(false);
        if (instructionPanel != null) instructionPanel.SetActive(false);
        if (successPanel != null) successPanel.SetActive(false);
        if (failPanel != null) failPanel.SetActive(false);
        if (backgroundPanel != null) backgroundPanel.SetActive(false);
    }

    // ==================== Word Selection ====================

    void SelectWord(string word)
    {
        currentWord = word;
        // Build correct morse sequence (no spaces) from the dictionary
        correctMorseSequence = BuildMorseSequence(word);
        Debug.Log($"Selected: {word} = {correctMorseSequence}");
        EnterState(TrainingState.INSTRUCTION);
    }

    string BuildMorseSequence(string word)
    {
        string result = "";
        foreach (char c in word.ToUpper())
        {
            if (morseTable.ContainsKey(c))
                result += morseTable[c];
        }
        return result;
    }

    // ==================== Morse Align Display ====================

    void GenerateMorseAlignDisplay(string word)
    {
        if (morseAlignDisplay == null || letterBlockPrefab == null) return;

        // Clear existing blocks
        foreach (Transform child in morseAlignDisplay)
            Destroy(child.gameObject);

        // Create one block per letter
        foreach (char c in word.ToUpper())
        {
            if (!morseTable.ContainsKey(c)) continue;

            string morse = morseTable[c];

            GameObject block = Instantiate(letterBlockPrefab, morseAlignDisplay);

            // Find LetterText and MorseText inside the prefab
            TextMeshProUGUI letterText = block.transform.Find("LetterText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI morseText = block.transform.Find("MorseText")?.GetComponent<TextMeshProUGUI>();

            if (letterText != null) letterText.text = c.ToString();
            if (morseText != null) morseText.text = morse;
        }
    }

    // ==================== Audio ====================

    void OnPlayAudioClicked()
    {
        if (isPlayingAudio) return;

        AudioClip clip = GetClipForCurrentWord();
        if (clip != null)
            StartCoroutine(PlayAudio(clip));
        else
            Debug.LogWarning($"No audio clip assigned for {currentWord}");
    }

    AudioClip GetClipForCurrentWord()
    {
        if (currentWord == "SOS") return sosAudio;
        if (currentWord == "HELLO") return helloAudio;
        if (currentWord == "BYE") return byeAudio;
        return null;
    }

    float GetVolumeForCurrentWord()
    {
        if (currentWord == "SOS") return 1.5f;
        if (currentWord == "HELLO") return 3.5f;  // modify volume
        if (currentWord == "BYE") return 4f;  // modify volume
        return 1f;
    }


    IEnumerator PlayAudio(AudioClip clip)
    {
        isPlayingAudio = true;
        if (playButtonLabel != null) playButtonLabel.text = "Playing...";

        audioSource.PlayOneShot(clip, GetVolumeForCurrentWord());
        yield return new WaitForSeconds(clip.length);

        isPlayingAudio = false;
        if (playButtonLabel != null) playButtonLabel.text = "Play Again";
    }

    // ==================== User Input ====================

    void OnUserSequenceUpdated(string sequence)
    {
        if (currentState != TrainingState.INSTRUCTION) return;

        // Display with colors and spaces
        if (userInputText != null)
        {
            if (string.IsNullOrEmpty(sequence.Trim()))
            {
                userInputText.text = "<color=#888888><i>[Waiting for input...]</i></color>";
            }
            else
            {
                string formatted = sequence
                    .Replace(".", "<color=#FFD700>.</color>")
                    .Replace("-", "<color=#FF6B6B>-</color>");
                userInputText.text = $"<size=48>{formatted}</size>";
            }
        }
     
        if (!userHasPressedOnce && sequence.Trim().Length > 0)
        {
            userHasPressedOnce = true;
        }

        // Restart judge timer
        if (judgeCoroutine != null)
            StopCoroutine(judgeCoroutine);

        if (sequence.Trim().Length > 0)
            judgeCoroutine = StartCoroutine(JudgeAfterDelay());
    }

    void ShowRecentSignal(string signal)
    {
        if (recentSignalText != null)
        {
            StopCoroutine("FlashSignal");
            StartCoroutine(FlashSignal(signal));
        }
    }

    IEnumerator FlashSignal(string signal)
    {
        Color signalColor = signal == "-"
            ? new Color(1f, 0.42f, 0.42f)
            : new Color(1f, 0.84f, 0f);

        recentSignalText.text = $"<size=72>{signal}</size>";
        recentSignalText.color = signalColor;

        float elapsed = 0f;
        while (elapsed < signalFlashDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / signalFlashDuration);
            recentSignalText.color = new Color(signalColor.r, signalColor.g, signalColor.b, alpha);
            yield return null;
        }

        recentSignalText.text = "";
    }

    IEnumerator JudgeAfterDelay()
    {
        yield return new WaitForSeconds(judgeDelay);

        if (currentState == TrainingState.INSTRUCTION)
            Judge();
    }

    // ==================== Judging ====================

    void Judge()
    {
        string userInput = morseSystem != null
            ? morseSystem.GetMorseSequence().Replace(" ", "")
            : "";
        string correct = correctMorseSequence.Replace(" ", "");

        Debug.Log($"Judging: user='{userInput}' correct='{correct}'");

        if (userInput == correct)
            EnterState(TrainingState.SUCCESS);
        else
            EnterState(TrainingState.FAIL);
    }

    IEnumerator ReturnToInstructionAfterDelay()
    {
        yield return new WaitForSeconds(failDisplayDuration);
        EnterState(TrainingState.INSTRUCTION);
    }

    // ==================== Button Handlers ====================

    void OnExitClicked()
    {
        // Cancel any ongoing judgment
        if (judgeCoroutine != null)
            StopCoroutine(judgeCoroutine);

        EnterState(TrainingState.WORD_SELECT);
    }

    void OnTrainAnotherClicked()
    {
        EnterState(TrainingState.WORD_SELECT);
    }

    void OnGoToOperatingClicked()
    {
        SetAllPanelsInactive();
        if (backgroundPanel != null) backgroundPanel.SetActive(true);
        morseSystem?.ResetInput();

        // Notify OperatingManager that we're entering operating mode in the same scene.
        // This re-enables things that Start() would have set up (like knob highlight).
        if (operatingManager != null) operatingManager.BeginOperatingSession();
    }

    // ==================== Key Highlight ====================

    void HighlightKnob(bool active)
    {
        if (knobHighlight == null) return;
        knobHighlight.enabled = active;
    }

    // ==================== Public ====================

    public void NotifyKeyPressed()
    {
        if (currentState == TrainingState.INSTRUCTION && !userHasPressedOnce)
        {
            userHasPressedOnce = true;
        }
    }

    public void GoToWordSelect()
    {
        if (judgeCoroutine != null)
            StopCoroutine(judgeCoroutine);
        morseSystem?.ResetInput();
        EnterState(TrainingState.WORD_SELECT);
    }
}