using UnityEngine;
using System;

public class ProximityClickDetector : MonoBehaviour
{
    public float clickDistance = 0.1f;
    public Action OnClicked;

    private bool isNear = false;
    private Transform[] fingertips;

    void Start()
    {
        var all = FindObjectsOfType<Transform>();
        var tips = new System.Collections.Generic.List<Transform>();
        foreach (var t in all)
        {
            if (t.name.Contains("HandIndexFingertip"))
                tips.Add(t);
        }
        fingertips = tips.ToArray();
        Debug.Log($"[ProximityClick] Found {fingertips.Length} fingertips");
    }

    void Update()
    {
        bool handNear = false;
        foreach (var tip in fingertips)
        {
            if (tip == null) continue;
            float dist = Vector3.Distance(tip.position, transform.position);
            if (dist < clickDistance)
            {
                handNear = true;
                break;
            }
        }

        if (handNear && !isNear)
        {
            isNear = true;
            OnClicked?.Invoke();
            Debug.Log($"[ProximityClick] {gameObject.name} clicked!");
        }
        else if (!handNear && isNear)
        {
            isNear = false;
        }
    }
}