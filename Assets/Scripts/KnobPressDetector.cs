using UnityEngine;

public class KnobPressDetector : MonoBehaviour
{
    public SimpleLeverPressController leverController;
    public Transform knobTransform;
    public float pressDistance = 0.05f;

    private bool isPressed = false;
    private Transform[] fingertips;

    void Start()
    {
        // 找所有手指指尖
        var all = FindObjectsOfType<Transform>();
        var tips = new System.Collections.Generic.List<Transform>();
        foreach (var t in all)
        {
            if (t.name.Contains("HandIndexFingertip"))
                tips.Add(t);
        }
        fingertips = tips.ToArray();
        Debug.Log($"[KnobDetector] Found {fingertips.Length} fingertips");
    }

    void Update()
    {
        bool handNear = false;
        foreach (var tip in fingertips)
        {
            if (tip == null) continue;
            float dist = Vector3.Distance(tip.position, knobTransform.position);
            if (dist < pressDistance)
            {
                handNear = true;
                break;
            }
        }

        if (handNear && !isPressed)
        {
            isPressed = true;
            leverController?.StartKeyPress();
            Debug.Log("[KnobDetector] Key pressed!");
        }
        else if (!handNear && isPressed)
        {
            isPressed = false;
            leverController?.EndKeyPress();
            Debug.Log("[KnobDetector] Key released!");
        }
    }
}