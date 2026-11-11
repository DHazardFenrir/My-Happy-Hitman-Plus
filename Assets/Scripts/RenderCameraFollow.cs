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

    // The part of that area that is really on the monitor: at 16:9 the bottom of the phone is
    // off-screen, so minigames that fill the screen (drones, captcha) lay themselves out in here
    public Rect GetPhoneVisibleWorldRect(float planeZ = 0f)
    {
        RectTransform screen = GameManager.Instance != null ? GameManager.Instance.gameScreen : null;
        if (cam == null || screen == null) return GetVisibleWorldRect(planeZ);

        Vector3[] s = new Vector3[4], m = new Vector3[4];
        screen.GetWorldCorners(s);
        ((RectTransform)screen.GetComponentInParent<Canvas>().rootCanvas.transform).GetWorldCorners(m);
        float u0 = Mathf.InverseLerp(s[0].x, s[2].x, Mathf.Max(s[0].x, m[0].x));
        float u1 = Mathf.InverseLerp(s[0].x, s[2].x, Mathf.Min(s[2].x, m[2].x));
        float v0 = Mathf.InverseLerp(s[0].y, s[2].y, Mathf.Max(s[0].y, m[0].y));
        float v1 = Mathf.InverseLerp(s[0].y, s[2].y, Mathf.Min(s[2].y, m[2].y));

        float depth = planeZ - cam.transform.position.z;
        Vector3 a = cam.ViewportToWorldPoint(new Vector3(u0, v0, depth));
        Vector3 b = cam.ViewportToWorldPoint(new Vector3(u1, v1, depth));
        return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
    }
}
