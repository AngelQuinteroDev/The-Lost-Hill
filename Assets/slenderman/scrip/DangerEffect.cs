using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public class DangerEffect : MonoBehaviour
{
    [Header("Referencia")]
    public PostProcessVolume volume;

    [Header("Distancias")]
    public float dangerRadius = 8f;
    public float maxEffectRadius = 3f;

    [Header("Intensidades máximas")]
    public float maxVignette = 0.6f;
    public float maxChromaticAberr = 1f;
    public float maxLensDistortion = -30f;

    private Vignette vignette;
    private ChromaticAberration chromatic;
    private LensDistortion lensDistortion;
    private Transform player;
    private float intensity = 0f;

    void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) player = p.transform;

        volume.profile.TryGetSettings(out vignette);
        volume.profile.TryGetSettings(out chromatic);
        volume.profile.TryGetSettings(out lensDistortion);
    }

    void Update()
    {
        if (player == null) return;

        GameObject enemy = GameObject.FindWithTag("Enemy");
        if (enemy == null) { SetIntensity(0f); return; }

        float dist = Vector3.Distance(player.position, enemy.transform.position);
        float target = 1f - Mathf.Clamp01((dist - maxEffectRadius) / (dangerRadius - maxEffectRadius));
        intensity = Mathf.Lerp(intensity, target, Time.deltaTime * 2f);

        SetIntensity(intensity);
    }

    void SetIntensity(float t)
    {
        if (vignette != null) vignette.intensity.value = Mathf.Lerp(0f, maxVignette, t);
        if (chromatic != null) chromatic.intensity.value = Mathf.Lerp(0f, maxChromaticAberr, t);
        if (lensDistortion != null) lensDistortion.intensity.value = Mathf.Lerp(0f, maxLensDistortion, t);
    }
}