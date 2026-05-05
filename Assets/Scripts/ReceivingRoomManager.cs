using System.Collections;
using UnityEngine;

/// <summary>
/// Receiving Room main controller.
/// 
/// Phase 1: reads the morse sequence from SentMessageData and decodes it.
/// Phase 2: prompt + highlight handle → click → rotate handle.
/// Phase 3: flywheel spin + paper tape extend.
/// Phase 4: highlight tape + new prompt → click tape → show result popup.
/// </summary>
public class ReceivingRoomManager : MonoBehaviour
{
    [Header("Debug")]
    public bool useTestSequence = false;
    public string testSequence = "... --- ...   -.-";

    [Header("Phase 2 — Handle Interaction")]
    public Transform handleTransform;
    public ProximityClickDetector handleClick;
    public HandleHighlight handleHighlight;
    [Tooltip("UI GameObject shown as the 'click the handle' prompt.")]
    public GameObject promptUI;

    [Header("Handle Rotation")]
    public float handleRotationDuration = 1.5f;
    public float handleRotationDegrees = 360f;
    public Vector3 handleRotationAxis = new Vector3(0, 0, 1);

    [Header("Phase 3 — Flywheel & Paper Tape")]
    public Transform flywheelPivot;
    public Transform paperTapeExtending;
    public float flywheelSpinSpeed = 60f;
    public Vector3 flywheelAxis = new Vector3(0, -1, 0);
    public float tapeExtendDuration = 2.5f;
    public float tapeHiddenScaleZ = 0.3f;
    public float tapeFinalScaleZ = 1.3f;

    [Header("Audio")]
    [Tooltip("AudioSource for the handle winding sound. Plays during Phase 2 handle rotation.")]
    public AudioSource handleAudio;
    [Tooltip("AudioSource for the flywheel spinning sound. Plays during Phase 3.")]
    public AudioSource flywheelAudio;

    [Header("Phase 4 — Tape Click & Result Popup")]
    [Tooltip("Click handler on the PaperTape_Extending object.")]
    public ProximityClickDetector tapeClick;
    [Tooltip("Highlight on the PaperTape_Extending object.")]
    public HandleHighlight tapeHighlight;
    [Tooltip("UI GameObject shown as the 'click the tape' prompt. Reuses PromptText or a second prompt.")]
    public GameObject tapePromptUI;
    [Tooltip("TMP text inside the tape prompt, used to change the prompt message for Phase 4.")]
    public TMPro.TextMeshProUGUI tapePromptText;
    [Tooltip("Message shown when waiting for the user to click the paper tape.")]
    public string tapePromptMessage = "Click the tape to read the message";
    [Tooltip("The result popup controller.")]
    public ResultPopup resultPopup;

    // Runtime state
    private string rawMorse = "";
    private string decodedText = "";
    private Vector3 tapeFullScale;

    void Start()
    {
        // ===== Force ambient lighting to a known-good state =====
        // Workaround: when loaded from Sending Room, environment lighting
        // sometimes ends up disabled. Force-enable it here.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.5f;
        DynamicGI.UpdateEnvironment();

        // ===== Phase 1: Data =====
        if (useTestSequence)
        {
            rawMorse = testSequence;
            Debug.Log("[ReceivingRoom] Using TEST sequence (SentMessageData ignored).");
        }
        else
        {
            rawMorse = SentMessageData.LastSentSequence;
        }

        if (string.IsNullOrEmpty(rawMorse))
        {
            Debug.LogWarning("[ReceivingRoom] No morse sequence received! " +
                "Enable 'Use Test Sequence' on the manager to test in isolation.");
            return;
        }

        decodedText = MorseDecoder.Decode(rawMorse);
        Debug.Log($"[ReceivingRoom] Raw morse:    \"{rawMorse}\"");
        Debug.Log($"[ReceivingRoom] Decoded text: \"{decodedText}\"");

        // ===== Setup: hide the extending tape =====
        if (paperTapeExtending != null)
        {
            tapeFullScale = paperTapeExtending.localScale;
            Vector3 hidden = tapeFullScale;
            hidden.z = tapeFullScale.z * tapeHiddenScaleZ;
            paperTapeExtending.localScale = hidden;
        }
        else
        {
            Debug.LogWarning("[ReceivingRoom] paperTapeExtending not assigned in Inspector.");
        }

        // ===== Setup: make sure tape interaction is OFF at start =====
        if (tapeClick != null) tapeClick.enabled = false;
        if (tapeHighlight != null) tapeHighlight.enabled = false;
        if (tapePromptUI != null) tapePromptUI.SetActive(false);

        // ===== Phase 2: Start handle interaction =====
        StartPhase2();
    }

    // ==================== Phase 2 ====================

    void StartPhase2()
    {
        if (promptUI != null) promptUI.SetActive(true);
        if (handleHighlight != null) handleHighlight.enabled = true;

        if (handleClick != null)
        {
            handleClick.enabled = true;
            handleClick.OnClicked += OnHandleClicked;
        }
        else
        {
            Debug.LogError("[ReceivingRoom] handleClick reference not set in Inspector!");
        }

        Debug.Log("[ReceivingRoom] Phase 2 started — waiting for handle click.");
    }

    void OnHandleClicked()
    {
        handleClick.OnClicked -= OnHandleClicked;
        handleClick.enabled = false;

        if (promptUI != null) promptUI.SetActive(false);
        if (handleHighlight != null) handleHighlight.enabled = false;

        StartCoroutine(RotateHandleCoroutine());
    }

    IEnumerator RotateHandleCoroutine()
    {
        if (handleTransform == null)
        {
            Debug.LogError("[ReceivingRoom] handleTransform reference not set!");
            yield break;
        }

        Debug.Log("[ReceivingRoom] Rotating handle...");
        if (handleAudio != null) handleAudio.Play();

        float elapsed = 0f;
        float rotatedSoFar = 0f;
        while (elapsed < handleRotationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / handleRotationDuration);
            float targetRotation = handleRotationDegrees * progress;
            float delta = targetRotation - rotatedSoFar;
            handleTransform.Rotate(handleRotationAxis, delta, Space.Self);
            rotatedSoFar = targetRotation;
            yield return null;
        }

        Debug.Log("[ReceivingRoom] Handle rotation complete.");
        yield return StartCoroutine(Phase3Coroutine());
    }

    // ==================== Phase 3 ====================

    IEnumerator Phase3Coroutine()
    {
        if (flywheelPivot == null || paperTapeExtending == null)
        {
            Debug.LogError("[ReceivingRoom] Phase 3 refs not set!");
            yield break;
        }

        Debug.Log("[ReceivingRoom] Phase 3 started — flywheel + paper tape.");
        if (flywheelAudio != null) flywheelAudio.Play();

        Vector3 startScale = paperTapeExtending.localScale;
        Vector3 endScale = tapeFullScale;
        endScale.z = tapeFullScale.z * tapeFinalScaleZ;

        float elapsed = 0f;
        while (elapsed < tapeExtendDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / tapeExtendDuration);

            flywheelPivot.Rotate(flywheelAxis, flywheelSpinSpeed * Time.deltaTime, Space.Self);
            paperTapeExtending.localScale = Vector3.Lerp(startScale, endScale, progress);

            yield return null;
        }

        paperTapeExtending.localScale = endScale;
        if (flywheelAudio != null) flywheelAudio.Stop();

        Debug.Log("[ReceivingRoom] Phase 3 complete.");

        // Chain into Phase 4
        StartPhase4();
    }

    // ==================== Phase 4 ====================

    void StartPhase4()
    {
        if (tapePromptText != null) tapePromptText.text = tapePromptMessage;
        if (tapePromptUI != null) tapePromptUI.SetActive(true);
        if (tapeHighlight != null) tapeHighlight.enabled = true;

        if (tapeClick != null)
        {
            tapeClick.enabled = true;
            tapeClick.OnClicked += OnTapeClicked;
        }
        else
        {
            Debug.LogError("[ReceivingRoom] tapeClick reference not set in Inspector!");
        }

        Debug.Log("[ReceivingRoom] Phase 4 started — waiting for tape click.");
    }

    void OnTapeClicked()
    {
        tapeClick.OnClicked -= OnTapeClicked;
        tapeClick.enabled = false;

        if (tapePromptUI != null) tapePromptUI.SetActive(false);
        if (tapeHighlight != null) tapeHighlight.enabled = false;

        if (resultPopup != null)
        {
            resultPopup.Show(rawMorse, decodedText);
        }
        else
        {
            Debug.LogError("[ReceivingRoom] resultPopup not assigned in Inspector!");
        }

        Debug.Log("[ReceivingRoom] Phase 4 complete — popup shown.");
    }

    // Public accessors
    public string RawMorse => rawMorse;
    public string DecodedText => decodedText;
}