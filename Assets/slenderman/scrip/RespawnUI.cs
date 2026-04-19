using UnityEngine;
using TMPro;
using System.Collections;

public class RespawnUI : MonoBehaviour
{
    [Header("UI")]
    public GameObject panel;
    public TMP_Text countdownText;

    public static RespawnUI Instance;

    void Awake()
    {
        Instance = this;

        if (panel != null) panel.SetActive(false);
    }

    public void ShowCountdown(float duration)
    {
        StartCoroutine(CountdownRoutine(duration));
    }

    IEnumerator CountdownRoutine(float duration)
    {
        if (panel != null) panel.SetActive(true);

        float remaining = duration;
        while (remaining > 0f)
        {
            if (countdownText != null)
                countdownText.text = "Reapareciendo en " + Mathf.CeilToInt(remaining) + "...";

            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        if (countdownText != null) countdownText.text = "";
        if (panel != null) panel.SetActive(false);
    }
}