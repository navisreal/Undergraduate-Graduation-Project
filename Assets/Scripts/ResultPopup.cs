using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controls the result popup that shows the received morse code
/// and its decoded plain text. Starts hidden. Call Show(morse, plain)
/// to display it. The Close button hides it again.
/// </summary>
public class ResultPopup : MonoBehaviour
{
    [Header("Popup Root")]
    [Tooltip("The GameObject that is enabled/disabled to show/hide the popup. Usually the ResultPopup panel itself.")]
    public GameObject popupRoot;

    [Header("Text Fields")]
    [Tooltip("Text inside the paper tape background — shows the raw morse code.")]
    public TextMeshProUGUI morseText;
    [Tooltip("Text below the paper tape — shows 'Decoded: XXX'.")]
    public TextMeshProUGUI plainText;

    [Header("Close Button")]
    [Tooltip("The Close button that hides the popup when clicked.")]
    public Button closeButton;

    [Header("Format")]
    [Tooltip("Prefix for the decoded text. E.g. 'Decoded:  '")]
    public string decodedPrefix = "Decoded:  ";

    void Awake()
    {
        // Make sure popup is hidden on scene load
        if (popupRoot != null) popupRoot.SetActive(false);

        // Wire up the close button
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
        }
    }

    /// <summary>
    /// Show the popup with the given morse code and decoded plain text.
    /// </summary>
    public void Show(string morse, string plain)
    {
        if (popupRoot == null)
        {
            Debug.LogError("[ResultPopup] popupRoot not assigned!");
            return;
        }

        if (morseText != null) morseText.text = morse;
        if (plainText != null) plainText.text = decodedPrefix + plain;

        popupRoot.SetActive(true);
        Debug.Log($"[ResultPopup] Shown — morse: \"{morse}\", plain: \"{plain}\"");
    }

    /// <summary>
    /// Hide the popup.
    /// </summary>
    public void Hide()
    {
        Debug.Log("[ResultPopup] Hide() called!");
        if (popupRoot != null) popupRoot.SetActive(false);
        Debug.Log("[ResultPopup] Hidden.");
        UnityEngine.SceneManagement.SceneManager.LoadScene("SendingRoom");
    }
}