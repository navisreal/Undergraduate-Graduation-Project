using UnityEngine;
using System;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Collider))]
public class HandleClickHandler : MonoBehaviour
{
    public event Action OnClicked;

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
        }
    }

    void OnDisable()
    {
        if (xrInteractable != null)
        {
            xrInteractable.selectEntered.RemoveListener(OnXRSelectEntered);
        }
    }

    void Start() { }

    private void OnMouseDown()
    {
        if (!enabled) return;
        Debug.Log("[HandleClick] Handle clicked (mouse)!");
        OnClicked?.Invoke();
    }

    private void OnXRSelectEntered(SelectEnterEventArgs args)
    {
        if (!enabled) return;
        Debug.Log("[HandleClick] Handle selected (VR controller)!");
        OnClicked?.Invoke();
    }
}