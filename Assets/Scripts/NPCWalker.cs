using System.Collections;
using UnityEngine;

public class NPCWalker : MonoBehaviour
{
    public float moveSpeed = 2f;

    public NPCAppearance.ClothingPart clothingPart;
    public bool isTarget = false;

    [Tooltip("Hace pausas aleatorias al caminar")]
    public bool randomStops = true;

    [Tooltip("Sale sangre al morir")]
    public bool bleedOnDeath = true;

    // Mientras sea false el monito se queda quieto (por ejemplo mientras se muestra la card)
    public bool CanMove { get; set; } = true;
    public bool IsDead { get; private set; }
    // Ya desapareció de la pantalla (sin morir)
    public bool IsGone { get; private set; }
    public bool IsPopping { get; private set; }
    public Vector3 WalkDirection { get; private set; } = Vector3.right;

    private bool isStopped = false;
    private Vector3 baseScale;
    private float facing = 1f;

    // Cuerpo visible (cabeza a pies) en espacio local del monito, sin escala
    private Vector2 localBodyCenter;
    private Vector2 localBodySize = new Vector2(0.15f, 0.5f);

    void Awake()
    {
        baseScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        FitBodyToSprites();
    }

    // El collider del prefab solo cubría la cabeza; lo ajustamos a todo el cuerpo (cabeza a pies),
    // un poco más angosto que el dibujo para que solo cuenten golpes "de verdad".
    private void FitBodyToSprites()
    {
        Matrix4x4 toLocal = transform.worldToLocalMatrix;
        bool any = false;
        Vector2 min = Vector2.zero, max = Vector2.zero;

        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr.sprite == null) continue;
            Matrix4x4 m = toLocal * sr.transform.localToWorldMatrix;
            Bounds b = sr.sprite.bounds;
            Vector2[] corners =
            {
                m.MultiplyPoint3x4(new Vector3(b.min.x, b.min.y)), m.MultiplyPoint3x4(new Vector3(b.max.x, b.max.y)),
                m.MultiplyPoint3x4(new Vector3(b.min.x, b.max.y)), m.MultiplyPoint3x4(new Vector3(b.max.x, b.min.y))
            };
            foreach (Vector2 c in corners)
            {
                if (!any) { min = max = c; any = true; }
                min = Vector2.Min(min, c);
                max = Vector2.Max(max, c);
            }
        }
        if (!any) return;

        localBodyCenter = (min + max) / 2f;
        localBodySize = new Vector2((max.x - min.x) * 0.8f, max.y - min.y);

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.offset = localBodyCenter;
            box.size = localBodySize;
        }
    }

    // Rectángulo del cuerpo en el mundo (no depende de la animación de aparecer/desaparecer)
    public Rect BodyRect
    {
        get
        {
            Vector2 size = Vector2.Scale(localBodySize, baseScale);
            Vector2 center = (Vector2)transform.position + new Vector2(localBodyCenter.x * baseScale.x * facing, localBodyCenter.y * baseScale.y);
            return new Rect(center - size / 2f, size);
        }
    }

    public Vector3 BodyCenter => BodyRect.center;

    void Start()
    {
        StartCoroutine(RandomStopRoutine());
    }

    void Update()
    {
        if (!IsDead && !IsGone && CanMove && !isStopped)
        {
            transform.Translate(WalkDirection * moveSpeed * Time.deltaTime, Space.World);
        }
    }

    public void SetWalkDirection(Vector3 direction)
    {
        WalkDirection = direction.normalized;

        // Voltea el sprite si camina hacia la izquierda
        facing = WalkDirection.x < 0 ? -1f : 1f;
        ApplyScale(1f);
    }

    // Aparece "creciendo" en su lugar
    public void PopIn(float duration = 0.25f)
    {
        IsGone = false;
        gameObject.SetActive(true);
        StartCoroutine(PopRoutine(0f, 1f, duration, false));
    }

    // Desaparece "encogiéndose". Si deactivate es true, se apaga al terminar.
    public void PopOut(bool deactivate, float duration = 0.2f)
    {
        if (IsDead || IsPopping) return;
        StartCoroutine(PopRoutine(1f, 0f, duration, deactivate));
    }

    private IEnumerator PopRoutine(float from, float to, float duration, bool deactivateAtEnd)
    {
        IsPopping = true;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            // easeOutBack al aparecer, lineal al desaparecer
            float eased = to > from ? 1f + 2.70158f * Mathf.Pow(k - 1f, 3) + 1.70158f * Mathf.Pow(k - 1f, 2) : k;
            ApplyScale(Mathf.LerpUnclamped(from, to, eased));
            yield return null;
        }
        ApplyScale(to);
        IsPopping = false;

        if (deactivateAtEnd)
        {
            IsGone = true;
            gameObject.SetActive(false);
        }
    }

    private void ApplyScale(float amount)
    {
        transform.localScale = new Vector3(baseScale.x * facing * amount, baseScale.y * amount, baseScale.z);
    }

    IEnumerator RandomStopRoutine()
    {
        while (!IsDead)
        {
            yield return new WaitForSeconds(Random.Range(1.0f, 2.5f));
            if (!randomStops) continue;
            isStopped = true;
            yield return new WaitForSeconds(Random.Range(0.3f, 0.8f));
            isStopped = false;
        }
    }

    public void Kill()
    {
        if (IsDead) return;
        IsDead = true;
        IsPopping = false;
        StopAllCoroutines();

        if (bleedOnDeath)
        {
            Rect body = BodyRect;
            BloodSplatter.Spawn(new Vector3(body.center.x, body.center.y, transform.position.z), transform.parent, body.yMin);
        }

        gameObject.SetActive(false);
    }
}
