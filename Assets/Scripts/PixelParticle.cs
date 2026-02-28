using UnityEngine;

public class PixelParticle : MonoBehaviour
{
    private Vector2 velocity;
    private float gravity;
    private float lifetime;
    private float currentLife;
    private bool initialized = false;

    public void Initialize(Vector2 initialVelocity, float gravityStrength, float lifeDuration)
    {
        velocity = initialVelocity;
        gravity = gravityStrength;
        lifetime = lifeDuration;
        currentLife = 0f;
        initialized = true;
    }

    void Update()
    {
        if (!initialized) return;

        // Apply gravity
        velocity.y -= gravity * Time.deltaTime;

        // Move the pixel
        transform.position += (Vector3)velocity * Time.deltaTime;

        // Update lifetime
        currentLife += Time.deltaTime;

        // Fade out near end of life
        if (currentLife > lifetime * 0.7f)
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                float fadeAmount = 1f - ((currentLife - lifetime * 0.7f) / (lifetime * 0.3f));
                Color color = sr.color;
                color.a *= fadeAmount;
                sr.color = color;
            }
        }

        // Destroy or deactivate when lifetime expires
        if (currentLife >= lifetime)
        {
            gameObject.SetActive(false);
            initialized = false;
        }
    }

    // Optional: Add collision with ground
    void OnCollisionEnter2D(Collision2D collision)
    {
        // Bounce with energy loss
        velocity.y = Mathf.Abs(velocity.y) * 0.5f;
        velocity.x *= 0.8f;
    }
}
