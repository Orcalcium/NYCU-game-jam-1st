// File: UI/SkillCooldownUI2D.cs
using UnityEngine;
using UnityEngine.UI;

public class SkillCooldownUI2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerSkillCaster2D caster;

    [Header("Sliders (value = remaining %)")]
    [SerializeField] private Slider blinkSlider;
    [SerializeField] private Slider aoeSlider;

    [Header("Optional: hide when ready")]
    [SerializeField] private bool hideWhenReady = false;

    void Awake()
    {
        if (!caster) caster = FindFirstObjectByType<PlayerSkillCaster2D>();
        InitSlider(blinkSlider);
        InitSlider(aoeSlider);
    }

    void InitSlider(Slider s)
    {
        if (!s) return;
        s.minValue = 0f;
        s.maxValue = 1f;
        s.value = 0f;
    }

    void Update()
    {
        if (!caster) return;
        if (blinkSlider)
        {
            float v = caster.BlinkRemaining01;
            blinkSlider.value = v;
            if (hideWhenReady) blinkSlider.gameObject.SetActive(v > 0f);
        }

        if (aoeSlider)
        {
            float v = caster.AoERemaining01;
            aoeSlider.value = v;
            if (hideWhenReady) aoeSlider.gameObject.SetActive(v > 0f);
        }
    }
}
