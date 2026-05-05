using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Collider))]
public class SimpleLeverPressController : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Minimum hold time (seconds) to count as a DASH. Anything shorter is a DOT.")]
    public float dashMinTime = 0.2f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip dotSound;
    public AudioClip dashSound;

    [Header("Lever Animation")]
    public Transform leverTransform;
    public float pressedAngleX = -5f;
    public float pressSpeed = 15f;
    public float releaseSpeed = 20f;

    private float restAngleX = 0f;
    private float currentAngleX = 0f;

    private bool isKeyPressed = false;
    private float pressStartTime;

    private XRSimpleInteractable xrInteractable;

    void Awake()
    {
        xrInteractable = GetComponent<XRSimpleInteractable>();
    }

    void OnEnable()
    {
        if (xrInteractable != null)
        {
            xrInteractable.selectEntered.AddListener(OnXRSelectEntered);
            xrInteractable.selectExited.AddListener(OnXRSelectExited);
        }
    }

    void OnDisable()
    {
        if (xrInteractable != null)
        {
            xrInteractable.selectEntered.RemoveListener(OnXRSelectEntered);
            xrInteractable.selectExited.RemoveListener(OnXRSelectExited);
        }
    }

    void Update()
    {
        AnimateLever();
        //if (Input.GetKeyDown(KeyCode.Backspace))
        //{
        //    DeleteLastCharacter();
        //}
        //if (Input.GetKeyDown(KeyCode.R))
        //{
        //    ResetInput();
        //}
    }

    public void StartKeyPress()
    {
        if (isKeyPressed) return;
        isKeyPressed = true;
        pressStartTime = Time.time;
        Debug.Log("Key Pressed");
    }

    public void EndKeyPress()
    {
        if (!isKeyPressed) return;
        isKeyPressed = false;

        float pressDuration = Time.time - pressStartTime;
        bool isDash = pressDuration >= dashMinTime;

        string signalType = isDash ? "DASH(-)" : "DOT(.)";
        Debug.Log($"Key Released - Duration: {pressDuration * 1000:F0}ms, Signal: {signalType}");

        SendMorseSignal(isDash);
        PlaySignalSound(isDash);
    }

    void SendMorseSignal(bool isDash)
    {
        SimpleMorseCodeSystem morseSystem = SimpleMorseCodeSystem.Instance;
        if (morseSystem != null)
        {
            morseSystem.AddSignal(isDash);
        }
        else
        {
            Debug.LogError("Morse Code System not found!");
        }
    }

    void PlaySignalSound(bool isDash)
    {
        if (audioSource == null) return;

        AudioClip clip = isDash ? dashSound : dotSound;
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    void ResetInput()
    {
        SimpleMorseCodeSystem morseSystem = SimpleMorseCodeSystem.Instance;
        if (morseSystem != null) morseSystem.ResetInput();
    }

    void DeleteLastCharacter()
    {
        SimpleMorseCodeSystem morseSystem = SimpleMorseCodeSystem.Instance;
        if (morseSystem != null) morseSystem.DeleteLastCharacter();
    }

    void AnimateLever()
    {
        if (leverTransform == null) return;
        float targetAngle = isKeyPressed ? pressedAngleX : restAngleX;
        float speed = isKeyPressed ? pressSpeed : releaseSpeed;
        currentAngleX = Mathf.MoveTowards(currentAngleX, targetAngle, speed * Time.deltaTime);
        Vector3 euler = leverTransform.localEulerAngles;
        euler.x = currentAngleX;
        leverTransform.localEulerAngles = euler;
    }


    // ===== PC mouse path =====
    void OnMouseDown() => StartKeyPress();
    void OnMouseUp() => EndKeyPress();

    // ===== VR controller path =====
    private void OnXRSelectEntered(SelectEnterEventArgs args) => StartKeyPress();
    private void OnXRSelectExited(SelectExitEventArgs args) => EndKeyPress();

    public bool IsKeyPressed() => isKeyPressed;
    public float GetCurrentPressDuration() => isKeyPressed ? Time.time - pressStartTime : 0f;
    public float GetThreshold() => dashMinTime;
}