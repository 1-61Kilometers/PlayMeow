using UnityEngine;

public class TurretController : MonoBehaviour
{
    [Header("Targeting")]
    public Transform target;

    [Header("Turret Parts")]
    [Tooltip("Assign the part that rotates Left/Right (Pan). Assumes rotation around LOCAL Z-AXIS.")] // Updated tooltip
    public Transform panPart;

    [Tooltip("Assign the part that rotates Up/Down (Tilt). Assumes rotation around LOCAL X-AXIS.")]
    public Transform tiltPart; // Or its correctly positioned pivot parent

    [Header("Rotation Settings")]
    public float turnSpeed = 180.0f;
    public float minTiltAngle = -30.0f;
    public float maxTiltAngle = 60.0f;

    [Header("Debugging")]
    public bool enableDebugLogs = true;

    private Quaternion targetPanRotation;
    private Quaternion targetTiltRotation;

    void Start()
    {
        if (target == null) { Debug.LogError("TurretController: Target missing!", this); enabled = false; return; }
        if (panPart == null) { Debug.LogError("TurretController: Pan Part missing!", this); enabled = false; return; }
        if (tiltPart == null) { Debug.LogError("TurretController: Tilt Part missing!", this); enabled = false; return; }

        // Initialize rotations based on starting orientation
        targetPanRotation = panPart.localRotation;
        targetTiltRotation = tiltPart.localRotation;
    }

    void Update()
    {
        if (target == null) return;
        AimAtTarget();
        RotateTurret();
    }
    float panAngleOffset = 0.0f; // Initialize pan angle offset
    void AimAtTarget()
    {
        // --- Pan Calculation (AROUND LOCAL Z-AXIS) ---
        Vector3 worldTargetDirectionPan = target.position - panPart.position;
        Vector3 localTargetDirectionPan = panPart.InverseTransformDirection(worldTargetDirectionPan);

        // Calculate angle in the local XY plane using Atan2(y, x) for rotation around Z
        // Calculate the target pan angle around the Z-axis
        float targetPanAngleZ = Mathf.Atan2(localTargetDirectionPan.y, localTargetDirectionPan.x) * Mathf.Rad2Deg;

        
        // Apply an offset to the calculated angle if needed
        if (Input.GetKeyDown(KeyCode.Space))
        {
            panAngleOffset = panAngleOffset == -90.0f ? 0.0f : -90.0f; // Toggle pan angle offset between two values
            Debug.Log($"Pan Angle Offset toggled: {panAngleOffset}");
        }

        targetPanAngleZ += panAngleOffset;

        if (enableDebugLogs)
        {
            Debug.Log($"Target Pan Angle (Z) with Offset: {targetPanAngleZ:F2}");
        }

        // Apply the calculated angle around the Z-axis
        targetPanRotation = Quaternion.Euler(0f, 0f, targetPanAngleZ);

        if (enableDebugLogs)
        {
            Debug.Log($"Pan Local Direction: {localTargetDirectionPan}, Calculated Pan Angle (Z): {targetPanAngleZ:F2}");
            Debug.DrawRay(panPart.position, panPart.right * 3.0f, Color.red);   // Local X (Tilt Axis)
            Debug.DrawRay(panPart.position, panPart.up * 3.0f, Color.green); // Local Y
            Debug.DrawRay(panPart.position, panPart.forward * 5.0f, Color.blue); // Local Z (Aiming Direction)
            
            Debug.DrawLine(panPart.position, target.position, Color.yellow); // Line to target world pos
            }

        // --- Tilt Calculation (AROUND LOCAL X-AXIS) ---
        // This part remains the same as it assumes tilt around the tiltPart's local X-axis
        Vector3 worldTargetDirectionTilt = target.position - tiltPart.position;
        Vector3 localTargetDirectionTilt = tiltPart.InverseTransformDirection(worldTargetDirectionTilt);

        // Use local Z for horizontal distance and local Y for vertical distance relative to tilt part
        float horizontalDist = localTargetDirectionTilt.z;
        float verticalDist = localTargetDirectionTilt.y;

        // Calculate angle around the tilt part's local X-axis
        // Atan2(y, x) but inputs adjusted for desired rotation: Atan2(-vertical, horizontal)
        float targetTiltAngleX_raw = Mathf.Atan2(-verticalDist, horizontalDist) * Mathf.Rad2Deg;
        float targetTiltAngleX_clamped = Mathf.Clamp(targetTiltAngleX_raw, minTiltAngle, maxTiltAngle);

        // Apply the calculated angle around the X-axis
        targetTiltRotation = Quaternion.Euler(targetTiltAngleX_clamped, 0f, 0f);

        if (enableDebugLogs)
        {
            Debug.Log($"Tilt Local Direction: {localTargetDirectionTilt}, Tilt Angle (X - Raw): {targetTiltAngleX_raw:F2}, Clamped: {targetTiltAngleX_clamped:F2}");
            // Draw Tilt Part's Axes and Aim Direction
            Debug.DrawRay(tiltPart.position, target.position, Color.cyan); // Visualize Aim
        }
    }

    void RotateTurret()
    {
        // --- Apply Rotations Smoothly ---
        if (panPart != null)
        {
            panPart.localRotation = Quaternion.RotateTowards(panPart.localRotation, targetPanRotation, turnSpeed * Time.deltaTime);
        }
        if (tiltPart != null)
        {
            tiltPart.localRotation = Quaternion.RotateTowards(tiltPart.localRotation, targetTiltRotation, turnSpeed * Time.deltaTime);
        }
    }

    // OnDrawGizmos can remain the same or be updated similarly to Debug.DrawRay if needed
    void OnDrawGizmos()
     {
         if (tiltPart != null)
         {
             Gizmos.color = Color.blue; // Actual aiming direction (Local Z)
             Gizmos.DrawRay(tiltPart.position, tiltPart.forward * 5.0f);
             Gizmos.color = Color.red; // Tilt Axis (Local X)
             Gizmos.DrawRay(tiltPart.position, tiltPart.right * 1.0f);
         }
         if (panPart != null)
         {
             Gizmos.color = Color.blue; // Pan Axis (Local Z)
             Gizmos.DrawRay(panPart.position, panPart.forward * 1.0f);
         }
         if (target != null && panPart != null)
         {
              Gizmos.color = Color.yellow; // Direct line to target
              Gizmos.DrawLine(panPart.position, target.position);
         }
     }
}