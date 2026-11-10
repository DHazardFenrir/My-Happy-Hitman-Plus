using UnityEngine;

public class RenderCameraFollow : MonoBehaviour
{
    public static RenderCameraFollow Instance;
    private Camera cam;

    [Header("Screen Align")]
    public Vector3 cameraOffset = new Vector3(0f, -3.5f, -10f);
    public float targetFOV = 35f;

    public Camera Cam => cam;

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
    }

    public void FocusOnStage(WarioStage stage)
    {
        if (stage == null) return;
        transform.position = stage.GetCameraPosition() + cameraOffset;

        if (cam != null)
        {
            cam.fieldOfView = targetFOV;
        }
    }

    // World area (on the z = planeZ plane) visible on the phone screen
    public Rect GetVisibleWorldRect(float planeZ = 0f)
    {
        Vector3 pos = transform.position;
        if (cam == null) return new Rect(pos.x - 2.5f, pos.y - 5f, 5f, 10f);

        float halfH = cam.orthographic
            ? cam.orthographicSize
            : Mathf.Abs(planeZ - pos.z) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfW = halfH * cam.aspect;
        return new Rect(pos.x - halfW, pos.y - halfH, halfW * 2f, halfH * 2f);
    }
}
