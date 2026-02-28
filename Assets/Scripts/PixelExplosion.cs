using UnityEngine;
using System.Collections.Generic;

public class PixelExplosion : MonoBehaviour
{
    [Header("Sprite Settings")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    
    [Header("Explosion Settings")]
    [SerializeField] private float explosionForce = 5f;
    [SerializeField] private float explosionRadius = 2f;
    [SerializeField] private float pixelLifetime = 3f;
    [SerializeField] private float gravity = 9.8f;
    [SerializeField] private bool randomizeForce = true;
    [SerializeField] private float forceVariation = 0.3f;
    
    [Header("Pixel Settings")]
    [SerializeField] private GameObject pixelPrefab;
    [SerializeField] private bool usePooling = true;
    
    private Texture2D spriteTexture;
    private List<GameObject> pixelPool = new List<GameObject>();
    private bool hasExploded = false;

    void Start()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // Create pixel prefab if not assigned
        if (pixelPrefab == null)
        {
            CreateDefaultPixelPrefab();
        }

        // Pre-pool pixels if using pooling
        if (usePooling)
        {
            PreparePixelPool();
        }
    }

    void CreateDefaultPixelPrefab()
    {
        pixelPrefab = new GameObject("Pixel");
        SpriteRenderer sr = pixelPrefab.AddComponent<SpriteRenderer>();
        
        // Create a simple 1x1 white texture
        Texture2D pixelTexture = new Texture2D(1, 1);
        pixelTexture.SetPixel(0, 0, Color.white);
        pixelTexture.Apply();
        pixelTexture.filterMode = FilterMode.Point;
        
        Sprite pixelSprite = Sprite.Create(pixelTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 0.1f);
        sr.sprite = pixelSprite;
        
        pixelPrefab.AddComponent<PixelParticle>();
        pixelPrefab.SetActive(false);
    }

    void PreparePixelPool()
    {
        // Estimate pool size (64x32 = 2048 pixels max)
        int poolSize = 2048;
        
        for (int i = 0; i < poolSize; i++)
        {
            GameObject pixel = Instantiate(pixelPrefab);
            pixel.SetActive(false);
            pixelPool.Add(pixel);
        }
    }

    public void ExplodeOnImpact(Vector2 impactPoint)
    {
        if (hasExploded) return;
        
        hasExploded = true;
        CreatePixelExplosion(impactPoint);
    }

    public void ExplodeOnImpact()
    {
        ExplodeOnImpact(transform.position);
    }

    void CreatePixelExplosion(Vector2 impactPoint)
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            Debug.LogError("No sprite renderer or sprite found!");
            return;
        }

        // Get the texture from the sprite
        spriteTexture = GetReadableTexture(spriteRenderer.sprite);
        
        if (spriteTexture == null)
        {
            Debug.LogError("Could not get readable texture from sprite!");
            return;
        }

        // Get sprite bounds in world space
        Bounds bounds = spriteRenderer.bounds;
        float pixelsPerUnit = spriteRenderer.sprite.pixelsPerUnit;
        
        int poolIndex = 0;

        // Iterate through each pixel in the texture
        for (int x = 0; x < spriteTexture.width; x++)
        {
            for (int y = 0; y < spriteTexture.height; y++)
            {
                Color pixelColor = spriteTexture.GetPixel(x, y);
                
                // Skip transparent pixels
                if (pixelColor.a < 0.1f) continue;

                // Calculate world position for this pixel
                float normalizedX = x / (float)spriteTexture.width;
                float normalizedY = y / (float)spriteTexture.height;
                
                Vector3 pixelWorldPos = new Vector3(
                    bounds.min.x + normalizedX * bounds.size.x,
                    bounds.min.y + normalizedY * bounds.size.y,
                    transform.position.z
                );

                // Create or get pixel from pool
                GameObject pixel;
                if (usePooling && poolIndex < pixelPool.Count)
                {
                    pixel = pixelPool[poolIndex];
                    poolIndex++;
                }
                else
                {
                    pixel = Instantiate(pixelPrefab);
                }

                pixel.transform.position = pixelWorldPos;
                pixel.SetActive(true);

                // Set pixel color
                SpriteRenderer pixelSR = pixel.GetComponent<SpriteRenderer>();
                if (pixelSR != null)
                {
                    pixelSR.color = pixelColor;
                    pixelSR.sortingLayerID = spriteRenderer.sortingLayerID;
                    pixelSR.sortingOrder = spriteRenderer.sortingOrder;
                }

                // Calculate explosion direction and force
                Vector2 direction = (pixelWorldPos - (Vector3)impactPoint).normalized;
                float distance = Vector2.Distance(pixelWorldPos, impactPoint);
                float forceFalloff = Mathf.Clamp01(1 - (distance / explosionRadius));
                
                float finalForce = explosionForce * forceFalloff;
                if (randomizeForce)
                {
                    finalForce *= Random.Range(1f - forceVariation, 1f + forceVariation);
                }

                Vector2 velocity = direction * finalForce;
                
                // Add some random variation to make it more natural
                velocity += new Vector2(
                    Random.Range(-forceVariation, forceVariation),
                    Random.Range(-forceVariation, forceVariation)
                );

                // Initialize the pixel particle
                PixelParticle particle = pixel.GetComponent<PixelParticle>();
                if (particle != null)
                {
                    particle.Initialize(velocity, gravity, pixelLifetime);
                }
            }
        }

        // Hide the original sprite
        spriteRenderer.enabled = false;
        
        // Optionally destroy this game object after explosion
        Destroy(gameObject, pixelLifetime + 0.5f);
    }

    Texture2D GetReadableTexture(Sprite sprite)
    {
        // If the texture is already readable, return it
        Texture2D texture = sprite.texture;
        
        if (texture.isReadable)
        {
            return texture;
        }

        // Otherwise, create a readable copy using RenderTexture
        RenderTexture tmp = RenderTexture.GetTemporary(
            texture.width,
            texture.height,
            0,
            RenderTextureFormat.Default,
            RenderTextureReadWrite.Linear
        );

        Graphics.Blit(texture, tmp);
        
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = tmp;
        
        Texture2D readableTexture = new Texture2D(texture.width, texture.height);
        readableTexture.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
        readableTexture.Apply();
        readableTexture.filterMode = FilterMode.Point;
        
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(tmp);
        
        return readableTexture;
    }

    // Example: Trigger explosion on collision
    void OnCollisionEnter2D(Collision2D collision)
    {
        ExplodeOnImpact(collision.contacts[0].point);
    }

    // Example: Trigger explosion on trigger enter
    void OnTriggerEnter2D(Collider2D other)
    {
        ExplodeOnImpact(transform.position);
    }
}
