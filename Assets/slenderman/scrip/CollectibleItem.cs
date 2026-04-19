using UnityEngine;

public class CollectibleItem : MonoBehaviour
{
    [Header("Rotación")]
    public float rotationSpeed = 90f;
    public Vector3 rotationAxis = Vector3.up;

    [Header("Flotación")]
    public float floatAmplitude = 0.3f;
    public float floatSpeed = 1.5f;

    [Header("Indicador")]
    public GameObject indicatorCanvas;

    private Vector3 startPosition;
    private bool collected = false;

    void Start()
    {
        startPosition = transform.position;

        if (indicatorCanvas != null)
        {
            indicatorCanvas.transform.SetParent(null);
            indicatorCanvas.SetActive(false);
        }
    }

    void Update()
    {
        if (collected) return;

        transform.Rotate(rotationAxis * rotationSpeed * Time.deltaTime);

        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    public void Collect()
    {
        if (collected) return;
        collected = true;

        if (indicatorCanvas != null)
            Destroy(indicatorCanvas);

        if (ItemCounter.Instance != null)
            ItemCounter.Instance.OnItemCollected();

        Destroy(gameObject);
    }
}