using UnityEngine;

public class CarCrumple : MonoBehaviour
{
    [Header("Sprite Settings")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    
    [Header("Deformation Settings")]
    [SerializeField] private float maxDeformationAmount = 0.3f; // How much the car can crumple (0-1)
    [SerializeField] private float deformationRadius = 0.5f; // Radius of crumple effect in world units
    [SerializeField] private float impactThreshold = 2f; // Minimum velocity to cause damage
    [SerializeField] private bool permanentDamage = true;
    [SerializeField] private float damageDecaySpeed = 0.5f; // If not permanent, how fast damage recovers
    
    [Header("Visual Feedback")]
    [SerializeField] private bool showDamageColor = true;
    [SerializeField] private Color damageColor = new Color(0.5f, 0.5f, 0.5f, 1f); // Gray tint for damage
    [SerializeField] private float maxDamageTint = 0.5f;
    
    private Mesh spriteMesh;
    private Vector3[] originalVertices;
    private Vector3[] deformedVertices;
    private MeshFilter meshFilter;
    private float currentDamageLevel = 0f;
    private Color originalColor;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        originalColor = spriteRenderer.color;
        
        // Create a mesh from the sprite for deformation
        CreateDeformableMesh();
    }

    void CreateDeformableMesh()
    {

        // Create mesh from sprite
        spriteMesh = new Mesh();
        spriteMesh.name = "Deformable Car Mesh";

        Sprite sprite = spriteRenderer.sprite;
        
        // Create a higher resolution mesh for better deformation
        // We'll subdivide the sprite into a grid
        int subdivisions = 8; // 8x8 grid for smooth deformation
        CreateSubdividedMesh(sprite, subdivisions);

        // Store original vertices
        originalVertices = new Vector3[spriteMesh.vertices.Length];
        deformedVertices = new Vector3[spriteMesh.vertices.Length];
        System.Array.Copy(spriteMesh.vertices, originalVertices, spriteMesh.vertices.Length);
        System.Array.Copy(spriteMesh.vertices, deformedVertices, spriteMesh.vertices.Length);
        
        // Hide the original sprite renderer
        spriteRenderer.enabled = false;
    }

    void CreateSubdividedMesh(Sprite sprite, int subdivisions)
    {
        // Create a subdivided quad mesh for the sprite
        float pixelsPerUnit = sprite.pixelsPerUnit;
        Rect rect = sprite.rect;
        Vector2 pivot = sprite.pivot;
        
        // Calculate size in world units
        float width = rect.width / pixelsPerUnit;
        float height = rect.height / pixelsPerUnit;
        
        // Calculate offset based on pivot
        float offsetX = -pivot.x / pixelsPerUnit;
        float offsetY = -pivot.y / pixelsPerUnit;

        int vertexCount = (subdivisions + 1) * (subdivisions + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        
        // Generate vertices and UVs
        for (int y = 0; y <= subdivisions; y++)
        {
            for (int x = 0; x <= subdivisions; x++)
            {
                int index = y * (subdivisions + 1) + x;
                
                float xPos = offsetX + (x / (float)subdivisions) * width;
                float yPos = offsetY + (y / (float)subdivisions) * height;
                
                vertices[index] = new Vector3(xPos, yPos, 0);
                uvs[index] = new Vector2(x / (float)subdivisions, y / (float)subdivisions);
            }
        }

        // Generate triangles
        int[] triangles = new int[subdivisions * subdivisions * 6];
        int triIndex = 0;
        
        for (int y = 0; y < subdivisions; y++)
        {
            for (int x = 0; x < subdivisions; x++)
            {
                int bottomLeft = y * (subdivisions + 1) + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + (subdivisions + 1);
                int topRight = topLeft + 1;

                triangles[triIndex++] = bottomLeft;
                triangles[triIndex++] = topLeft;
                triangles[triIndex++] = bottomRight;

                triangles[triIndex++] = bottomRight;
                triangles[triIndex++] = topLeft;
                triangles[triIndex++] = topRight;
            }
        }

        spriteMesh.vertices = vertices;
        spriteMesh.uv = uvs;
        spriteMesh.triangles = triangles;
        spriteMesh.RecalculateNormals();
        spriteMesh.RecalculateBounds();
    }

    public void ApplyImpactDeformation(Vector2 impactPoint, Vector2 impactVelocity)
    {
        float impactMagnitude = impactVelocity.magnitude;
        
        if (impactMagnitude < impactThreshold)
            return;

        // Convert impact point to local space
        Vector2 localImpactPoint = transform.InverseTransformPoint(impactPoint);
        
        // Calculate damage amount based on impact velocity
        float damageAmount = Mathf.Clamp01((impactMagnitude - impactThreshold) / 10f);
        currentDamageLevel = Mathf.Clamp01(currentDamageLevel + damageAmount * 0.3f);

        // Deform vertices near impact point
        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 vertexPos = deformedVertices[i]; // Use current deformed position
            float distance = Vector2.Distance(new Vector2(vertexPos.x, vertexPos.y), localImpactPoint);
            
            if (distance < deformationRadius)
            {
                // Calculate deformation amount (stronger at impact point)
                float deformFactor = 1f - (distance / deformationRadius);
                deformFactor = Mathf.Pow(deformFactor, 2); // Squared falloff for more natural look
                
                // Calculate deformation direction (push inward from impact)
                Vector2 impactDirection = impactVelocity.normalized;
                Vector2 deformDirection;
                
                // Deform inward toward impact direction
                if (Vector2.Dot(impactDirection, (Vector2)vertexPos - localImpactPoint) > 0)
                {
                    deformDirection = impactDirection;
                }
                else
                {
                    deformDirection = ((Vector2)vertexPos - localImpactPoint).normalized * -1f;
                }
                
                // Apply deformation
                float deformAmount = deformFactor * maxDeformationAmount * damageAmount;
                Vector3 deformation = new Vector3(deformDirection.x, deformDirection.y, 0) * deformAmount;
                
                deformedVertices[i] += deformation;
            }
        }

        // Update mesh
        spriteMesh.vertices = deformedVertices;
        spriteMesh.RecalculateBounds();
        spriteMesh.RecalculateNormals();

        // Update damage color
        if (showDamageColor)
        {
            UpdateDamageColor();
        }
    }

    void UpdateDamageColor()
    {
    }

    void Update()
    {
        // Optional: Decay damage over time if not permanent
        if (!permanentDamage && currentDamageLevel > 0)
        {
            currentDamageLevel -= damageDecaySpeed * Time.deltaTime;
            currentDamageLevel = Mathf.Max(0, currentDamageLevel);
            
            // Restore vertices toward original positions
            for (int i = 0; i < deformedVertices.Length; i++)
            {
                deformedVertices[i] = Vector3.Lerp(deformedVertices[i], originalVertices[i], 
                    damageDecaySpeed * Time.deltaTime);
            }
            
            spriteMesh.vertices = deformedVertices;
            spriteMesh.RecalculateBounds();
            
            if (showDamageColor)
            {
                UpdateDamageColor();
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (rb != null)
        {
            // Get relative velocity at impact
            Vector2 relativeVelocity = rb.linearVelocity;
            if (collision.rigidbody != null)
            {
                relativeVelocity -= collision.rigidbody.linearVelocity;
            }

            // Apply deformation at first contact point
            if (collision.contacts.Length > 0)
            {
                ApplyImpactDeformation(collision.contacts[0].point, relativeVelocity);
            }
        }
    }

    // Public method to reset damage
    public void ResetDamage()
    {
        System.Array.Copy(originalVertices, deformedVertices, originalVertices.Length);
        spriteMesh.vertices = deformedVertices;
        spriteMesh.RecalculateBounds();
        currentDamageLevel = 0f;
    }

    // Get current damage level (0-1)
    public float GetDamageLevel()
    {
        return currentDamageLevel;
    }
}
