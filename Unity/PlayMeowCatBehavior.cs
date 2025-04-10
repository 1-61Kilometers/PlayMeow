using UnityEngine;

/// <summary>
/// PlayMeow Cat Behavior
/// 
/// This script provides a simple implementation of cat behavior to use with the PlayMeowDataRecorder.
/// It simulates a cat being attracted to and following a laser pointer with realistic movement.
/// </summary>
public class PlayMeowCatBehavior : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the laser pointer object")]
    public Transform laserPointer;

    [Tooltip("Reference to the PlayMeowDataRecorder component")]
    public PlayMeowDataRecorder dataRecorder;

    [Header("Movement Settings")]
    [Tooltip("Maximum movement speed")]
    public float maxSpeed = 3.0f;

    [Tooltip("Movement acceleration")]
    public float acceleration = 5.0f;

    [Tooltip("Deceleration when stopping")]
    public float deceleration = 8.0f;

    [Tooltip("How quickly the cat turns")]
    public float turnSpeed = 720f;

    [Tooltip("Distance at which cat is considered to be 'engaged' with the laser")]
    public float engagementDistance = 0.5f;

    [Header("Behavior Settings")]
    [Tooltip("Chance the cat will ignore the laser (0-1)")]
    [Range(0, 1)]
    public float distractionChance = 0.1f;

    [Tooltip("How long the cat stays distracted (seconds)")]
    public float distractionDuration = 2.0f;

    [Tooltip("Maximum random offset from direct path to laser")]
    public float pathRandomness = 0.5f;

    [Tooltip("How often to update the random path offset")]
    public float pathRandomizationInterval = 0.8f;

    // Internal state
    private Vector3 currentVelocity = Vector3.zero;
    private Vector3 targetPosition;
    private bool isDistracted = false;
    private float distractionTimer = 0f;
    private Vector3 randomOffset = Vector3.zero;
    private float randomOffsetTimer = 0f;
    private bool wasEngaged = false;

    void Start()
    {
        if (laserPointer == null)
        {
            Debug.LogError("PlayMeow Cat Behavior: Laser pointer reference not set!");
            enabled = false;
            return;
        }

        if (dataRecorder == null)
        {
            Debug.LogWarning("PlayMeow Cat Behavior: Data recorder reference not set - engagement tracking will not work!");
        }

        // Initial target is current position
        targetPosition = transform.position;
    }

    void Update()
    {
        UpdateBehaviorState();
        UpdateMovement();
        UpdateEngagementState();
    }

    /// <summary>
    /// Update the cat's behavioral state (following or distracted)
    /// </summary>
    private void UpdateBehaviorState()
    {
        // Handle distraction state
        if (isDistracted)
        {
            distractionTimer -= Time.deltaTime;
            if (distractionTimer <= 0)
            {
                isDistracted = false;
            }
        }
        else
        {
            // Random chance to become distracted
            if (Random.value < distractionChance * Time.deltaTime)
            {
                isDistracted = true;
                distractionTimer = distractionDuration;
                
                // Choose a random position nearby when distracted
                Vector3 randomDirection = Random.insideUnitSphere;
                randomDirection.y = 0;
                targetPosition = transform.position + randomDirection.normalized * 2f;
            }
            else
            {
                // Update path randomization
                randomOffsetTimer -= Time.deltaTime;
                if (randomOffsetTimer <= 0)
                {
                    randomOffset = new Vector3(
                        Random.Range(-pathRandomness, pathRandomness),
                        0,
                        Random.Range(-pathRandomness, pathRandomness)
                    );
                    randomOffsetTimer = pathRandomizationInterval;
                }

                // Follow the laser with some randomness
                targetPosition = laserPointer.position + randomOffset;
            }
        }
    }

    /// <summary>
    /// Update the cat's movement
    /// </summary>
    private void UpdateMovement()
    {
        // Calculate direction to target
        Vector3 directionToTarget = targetPosition - transform.position;
        directionToTarget.y = 0; // Keep the cat on the ground

        // Calculate target rotation
        Quaternion targetRotation = Quaternion.identity;
        float distanceToTarget = directionToTarget.magnitude;

        if (distanceToTarget > 0.01f)
        {
            targetRotation = Quaternion.LookRotation(directionToTarget);
        }

        // Smoothly rotate towards the target
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime
        );

        // Calculate target speed based on distance
        float targetSpeed = 0;
        if (distanceToTarget > 0.1f)
        {
            targetSpeed = Mathf.Min(maxSpeed, distanceToTarget);
        }

        // Calculate current speed
        float currentSpeed = currentVelocity.magnitude;

        // Apply acceleration or deceleration
        float newSpeed;
        if (targetSpeed > currentSpeed)
        {
            newSpeed = Mathf.Min(targetSpeed, currentSpeed + acceleration * Time.deltaTime);
        }
        else
        {
            newSpeed = Mathf.Max(targetSpeed, currentSpeed - deceleration * Time.deltaTime);
        }

        // Apply the new velocity
        currentVelocity = transform.forward * newSpeed;
        
        // Move the cat
        transform.position += currentVelocity * Time.deltaTime;
    }

    /// <summary>
    /// Update the engagement state with the laser and notify the data recorder
    /// </summary>
    private void UpdateEngagementState()
    {
        if (dataRecorder == null || laserPointer == null)
            return;

        float distanceToLaser = Vector3.Distance(transform.position, laserPointer.position);
        bool isEngaged = distanceToLaser <= engagementDistance;

        // Only update when state changes to avoid unnecessary calls
        if (isEngaged != wasEngaged)
        {
            dataRecorder.SetEngagement(isEngaged);
            wasEngaged = isEngaged;
        }
    }
}