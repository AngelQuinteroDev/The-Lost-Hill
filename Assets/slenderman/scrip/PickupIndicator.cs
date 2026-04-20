using UnityEngine;
using UnityEngine.UI;

public class PickupIndicator : MonoBehaviour
{
    [Header("UI")]
    public Canvas worldCanvas;
    public Image iconImage;
    public float showRadius = 2.5f;

    [Header("Animaci�n")]
    public float pulseSpeed = 2f;
    public float pulseMinScale = 0.85f;
    public float pulseMaxScale = 1.15f;

    private Transform player;
    private bool isVisible = false;

    void Start()
    {
        if (worldCanvas != null)
            worldCanvas.gameObject.SetActive(false);
    }

    void Update()
    {
        if (player == null)
        {
            var pms = FindObjectsByType<PlayerControllerM>(FindObjectsSortMode.None);
            foreach (var pcmInstance in pms)
            {
                if (pcmInstance.IsLocalPlayer)
                {
                    player = pcmInstance.transform;
                    break;
                }
            }
            if (player == null) return;
        }

        var pcm = player.GetComponent<PlayerControllerM>();
        Camera camRef = pcm != null ? pcm.mainCamera : null;
        float rayLength = pcm != null ? pcm.pickupRayLength : showRadius;

        if (camRef == null) camRef = Camera.main;
        if (camRef == null) return;

        Transform cam = camRef.transform;
        Ray ray = new Ray(cam.position, cam.forward);
        bool aimed = false;

        if (Physics.Raycast(ray, out RaycastHit hit, rayLength))
            aimed = hit.collider.gameObject == gameObject;

        float dist = Vector3.Distance(transform.position, player.position);
        bool inRange = dist <= showRadius && aimed;

        if (inRange != isVisible)
        {
            isVisible = inRange;
            if (worldCanvas != null)
                worldCanvas.gameObject.SetActive(isVisible);
        }

        if (isVisible)
        {
            if (worldCanvas != null)
                worldCanvas.transform.LookAt(cam);

            float scale = Mathf.Lerp(pulseMinScale, pulseMaxScale,
                (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f);

            if (iconImage != null)
                iconImage.transform.localScale = Vector3.one * scale;
        }
    }
}