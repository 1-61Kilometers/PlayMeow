using UnityEngine;

/// <summary>
/// PlayMeow Laser Controller
/// 
/// Provides controls for moving a laser pointer in the scene.
/// This can be used with the PlayMeowDataRecorder to generate simulation data.
/// </summary>
public class PlayMeowLaserController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Movement speed when using keyboard")]
    public float moveSpeed = 3.0f;
    
    [Tooltip("Whether to constrain the laser to the play area bounds")]
    public bool constrainToPlayArea = true;

    [Tooltip("Play area boundaries [minX, maxX, minZ, maxZ]")]
    public Vector4 playAreaBounds = new Vector4(-2f, 2f, -2f, 2f);

    [Header("Auto Movement")]
    [Tooltip("Whether to enable automatic movement patterns")]
    public bool enableAutoMovement = false;

    [Tooltip("Movement pattern to use")]
    public MovementPattern pattern = MovementPattern.CircularPattern;

    [Tooltip("Speed of automatic movement")]
    public float autoMoveSpeed = 1.0f;

    // Enum for different movement patterns
    public enum MovementPattern
    {
        CircularPattern,
        FigureEightPattern,
        RandomPattern,
        SineWavePattern
    }

    // Internal variables
    private Vector3 initialPosition;
    private float autoMoveTimer = 0f;
    private Vector3 randomTargetPos;
    private float randomTargetTimer = 0f;

    void Start()
    {
        initialPosition = transform.position;
        
        // Initialize random target if using random pattern
        if (pattern == MovementPattern.RandomPattern)
        {
            SetNewRandomTarget();
        }
    }

    void Update()
    {
        if (enableAutoMovement)
        {
            MoveAutomatically();
        }
        else
        {
            MoveWithInput();
        }

        // Constrain to play area if enabled
        if (constrainToPlayArea)
        {
            ConstrainToPlayArea();
        }
    }

    /// <summary>
    /// Move the laser based on keyboard input
    /// </summary>
    private void MoveWithInput()
    {
        // Get input (can be customized as needed)
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        // Calculate movement vector
        Vector3 moveDirection = new Vector3(horizontalInput, 0, verticalInput).normalized;
        
        // Apply movement
        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }

    /// <summary>
    /// Move the laser automatically according to the selected pattern
    /// </summary>
    private void MoveAutomatically()
    {
        autoMoveTimer += Time.deltaTime * autoMoveSpeed;

        switch (pattern)
        {
            case MovementPattern.CircularPattern:
                ApplyCircularPattern();
                break;
            case MovementPattern.FigureEightPattern:
                ApplyFigureEightPattern();
                break;
            case MovementPattern.RandomPattern:
                ApplyRandomPattern();
                break;
            case MovementPattern.SineWavePattern:
                ApplySineWavePattern();
                break;
        }
    }

    /// <summary>
    /// Apply a circular movement pattern
    /// </summary>
    private void ApplyCircularPattern()
    {
        float radius = Mathf.Min(
            (playAreaBounds.y - playAreaBounds.x) / 2.5f,
            (playAreaBounds.w - playAreaBounds.z) / 2.5f
        );

        float centerX = (playAreaBounds.x + playAreaBounds.y) / 2;
        float centerZ = (playAreaBounds.z + playAreaBounds.w) / 2;

        float x = centerX + radius * Mathf.Cos(autoMoveTimer);
        float z = centerZ + radius * Mathf.Sin(autoMoveTimer);

        transform.position = new Vector3(x, transform.position.y, z);
    }

    /// <summary>
    /// Apply a figure-eight movement pattern
    /// </summary>
    private void ApplyFigureEightPattern()
    {
        float radius = Mathf.Min(
            (playAreaBounds.y - playAreaBounds.x) / 3f,
            (playAreaBounds.w - playAreaBounds.z) / 5f
        );

        float centerX = (playAreaBounds.x + playAreaBounds.y) / 2;
        float centerZ = (playAreaBounds.z + playAreaBounds.w) / 2;

        float x = centerX + radius * Mathf.Sin(autoMoveTimer);
        float z = centerZ + radius * Mathf.Sin(autoMoveTimer * 2);

        transform.position = new Vector3(x, transform.position.y, z);
    }

    /// <summary>
    /// Apply a random movement pattern
    /// </summary>
    private void ApplyRandomPattern()
    {
        // Check if we need a new target
        randomTargetTimer -= Time.deltaTime;
        if (randomTargetTimer <= 0)
        {
            SetNewRandomTarget();
        }

        // Move towards the target
        transform.position = Vector3.MoveTowards(
            transform.position,
            randomTargetPos,
            autoMoveSpeed * Time.deltaTime
        );

        // If we're close to the target, get a new one
        if (Vector3.Distance(transform.position, randomTargetPos) < 0.1f)
        {
            SetNewRandomTarget();
        }
    }

    /// <summary>
    /// Apply a sine wave movement pattern
    /// </summary>
    private void ApplySineWavePattern()
    {
        float width = (playAreaBounds.y - playAreaBounds.x) * 0.8f;
        float height = (playAreaBounds.w - playAreaBounds.z) * 0.8f;

        float centerX = (playAreaBounds.x + playAreaBounds.y) / 2;
        float centerZ = (playAreaBounds.z + playAreaBounds.w) / 2;

        float progress = Mathf.Repeat(autoMoveTimer * 0.5f, 1f);
        float x = centerX + (progress * 2 - 1) * width / 2;
        float z = centerZ + Mathf.Sin(progress * Mathf.PI * 4) * height / 3;

        transform.position = new Vector3(x, transform.position.y, z);
    }

    /// <summary>
    /// Set a new random target position
    /// </summary>
    private void SetNewRandomTarget()
    {
        // Get a random position within the play area
        float x = Random.Range(playAreaBounds.x, playAreaBounds.y);
        float z = Random.Range(playAreaBounds.z, playAreaBounds.w);
        
        randomTargetPos = new Vector3(x, transform.position.y, z);
        
        // Set a random time before picking the next target
        randomTargetTimer = Random.Range(1f, 3f);
    }

    /// <summary>
    /// Constrain the laser pointer to the play area
    /// </summary>
    private void ConstrainToPlayArea()
    {
        Vector3 pos = transform.position;
        
        pos.x = Mathf.Clamp(pos.x, playAreaBounds.x, playAreaBounds.y);
        pos.z = Mathf.Clamp(pos.z, playAreaBounds.z, playAreaBounds.w);
        
        transform.position = pos;
    }

    /// <summary>
    /// Reset the laser pointer to its initial position
    /// </summary>
    public void ResetPosition()
    {
        transform.position = initialPosition;
        autoMoveTimer = 0f;
    }

    /// <summary>
    /// Draw the play area boundaries in the editor
    /// </summary>
    private void OnDrawGizmos()
    {
        // Draw the play area boundaries
        Gizmos.color = Color.green;
        Vector3 center = new Vector3(
            (playAreaBounds.x + playAreaBounds.y) / 2,
            transform.position.y,
            (playAreaBounds.z + playAreaBounds.w) / 2
        );
        
        Vector3 size = new Vector3(
            playAreaBounds.y - playAreaBounds.x,
            0.01f,
            playAreaBounds.w - playAreaBounds.z
        );
        
        Gizmos.DrawWireCube(center, size);
    }
}