using UnityEngine;

// Anima las hélices del dron y le da un pequeño vaivén.
public class DroneVisual : MonoBehaviour
{
    public SpriteRenderer body;
    public Sprite[] frames;
    public float frameTime = 0.05f;
    public float bobAmount = 0.04f;

    private float timer;
    private int frame;
    private float bobSeed;

    void Start() => bobSeed = Random.value * 10f;

    void Update()
    {
        if (body == null || frames == null || frames.Length == 0) return;

        timer += Time.deltaTime;
        if (timer >= frameTime)
        {
            timer = 0f;
            frame = (frame + 1) % frames.Length;
            body.sprite = frames[frame];
        }

        body.transform.localPosition = new Vector3(0f, Mathf.Sin((Time.time + bobSeed) * 6f) * bobAmount, 0f);
    }
}
