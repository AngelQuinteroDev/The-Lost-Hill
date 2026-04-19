using UnityEngine;
using UnityEngine.UI;

public class ItemCounter : MonoBehaviour
{
    [Header("UI")]
    public Text counterText;

    private int total = 0;
    private int collected = 0;

    public static ItemCounter Instance;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        total = FindObjectsOfType<CollectibleItem>().Length;
        UpdateUI();
    }

    public void OnItemCollected()
    {
        collected++;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (counterText != null)
            counterText.text = collected + " de " + total;
    }
}