using System.Collections.Generic;
using UnityEngine;

// Partículas de sangre pixel-art (sprites generados por código, sin ParticleSystem
// para no depender de materiales de URP). Las gotas salen disparadas, caen
// y se quedan como manchas en el suelo junto con un charco que crece.
public class BloodSplatter : MonoBehaviour
{
    public static int sortingOrder = 6;

    [SerializeField] private int dropCount = 16;
    [SerializeField] private float gravity = 9f;

    private static readonly Color BloodDark = new Color(0.45f, 0.02f, 0.03f);
    private static readonly Color BloodBright = new Color(0.85f, 0.05f, 0.06f);

    private class Drop
    {
        public Transform t;
        public Vector3 velocity;
        public float landY;
        public bool landed;
    }

    private readonly List<Drop> drops = new List<Drop>();
    private Transform puddle;
    private float groundY;
    private float age;

    // position: centro del cuerpo. feetY: altura de los pies (donde queda el charco)
    public static BloodSplatter Spawn(Vector3 position, Transform parent, float feetY)
    {
        GameObject go = new GameObject("Blood");
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        go.transform.localScale = Vector3.one;
        go.transform.rotation = Quaternion.identity;
        BloodSplatter blood = go.AddComponent<BloodSplatter>();
        blood.groundY = feetY;
        blood.Burst();
        return blood;
    }

    private void Burst()
    {
        Vector3 origin = transform.position;

        puddle = CreatePixel("Puddle", new Vector3(origin.x, groundY, 0f), BloodDark, sortingOrder).transform;
        puddle.localScale = Vector3.zero;

        for (int i = 0; i < dropCount; i++)
        {
            Color c = Color.Lerp(BloodDark, BloodBright, Random.value);
            GameObject d = CreatePixel("Drop", origin + (Vector3)Random.insideUnitCircle * 0.05f, c, sortingOrder + 1);
            d.transform.localScale = Vector3.one * Random.Range(0.035f, 0.085f);
            drops.Add(new Drop
            {
                t = d.transform,
                velocity = new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(0.8f, 3.2f), 0f),
                landY = groundY + Random.Range(-0.06f, 0.04f)
            });
        }
    }

    private void Update()
    {
        age += Time.deltaTime;

        // El charco crece rápido y luego se queda
        float grow = Mathf.Clamp01(age / 0.5f);
        puddle.localScale = new Vector3(Mathf.Lerp(0f, 0.55f, grow), Mathf.Lerp(0f, 0.08f, grow), 1f);

        foreach (Drop d in drops)
        {
            if (d.landed || d.t == null) continue;

            d.velocity += Vector3.down * gravity * Time.deltaTime;
            d.t.position += d.velocity * Time.deltaTime;

            // Al tocar el suelo se queda como mancha
            if (d.velocity.y < 0f && d.t.position.y <= d.landY)
            {
                d.landed = true;
                Vector3 s = d.t.localScale;
                d.t.localScale = new Vector3(s.x * 1.6f, s.y * 0.6f, 1f);
            }
        }
    }

    private GameObject CreatePixel(string name, Vector3 position, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, true);
        go.transform.position = position;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = RuntimeSprites.Pixel();
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }
}
