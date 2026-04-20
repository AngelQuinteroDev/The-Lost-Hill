using UnityEngine;

public class CollectibleItem : MonoBehaviour
{
    [Header("Rotaci�n")]
    public float rotationSpeed = 90f;
    public Vector3 rotationAxis = Vector3.up;

    [Header("Flotaci�n")]
    public float floatAmplitude = 0.3f;
    public float floatSpeed = 1.5f;

    [Header("Indicador")]
    public GameObject indicatorCanvas;

    private Vector3 startPosition;
    private bool collected = false;
    private int _networkItemId = -1;

    public void InitializeNetworkId(int id)
    {
        _networkItemId = id;
    }

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
        if (collected || ItemCounter.Instance == null || _networkItemId < 0) return;
        
        ItemCounter.Instance.HandleLocalPickupAttempt(_networkItemId);
    }

    public void ConfirmCollection()
    {
        if (collected) return;
        collected = true;

        if (indicatorCanvas != null)
            Destroy(indicatorCanvas);

        Destroy(gameObject);
    }
}