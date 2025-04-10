using UnityEngine;

/// <summary>
/// Inverse Kinematics solver for a 2-joint turret.
/// Joint 1 (Base) rotates around its local Y-axis (Yaw).
/// Joint 2 (Pivot) rotates around its local X-axis (Pitch).
/// Uses the Cyclic Coordinate Descent (CCD) method to aim the EndEffector towards the Target.
/// </summary>
public class TurretIKController : MonoBehaviour
{
    [Header("IK Chain (Assign in Editor)")]
    [Tooltip("The base transform that rotates horizontally (Yaw).")]
    public Transform joint1_BaseYaw;
    [Tooltip("The pivoting transform that rotates vertically (Pitch). Child of BaseYaw.")]
    public Transform joint2_HeadPitch;
    [Tooltip("The point where the laser originates. Child of HeadPitch. Its forward direction should align with the laser beam.")]
    public Transform endEffector_LaserOrigin;

    [Header("IK Target")]
    [Tooltip("The object the turret should aim at.")]
    public Transform target;

    [Header("Solver Parameters")]
    [Range(1, 50)]
    [Tooltip("Maximum iterations per frame to solve the IK.")]
    public int iterations = 15;
    [Tooltip("How close the end effector needs to be to the target line-of-sight (squared distance). Smaller values are more accurate but may cause jitter.")]
    public float tolerance = 0.01f; // Using squared distance for efficiency later

    [Header("Joint Limits (Optional)")]
    public bool useYawLimits = false;
    [Range(-180f, 180f)] public float minYaw = -90f;
    [Range(-180f, 180f)] public float maxYaw = 90f;

    public bool usePitchLimits = false;
    [Range(-90f, 90f)] public float minPitch = -45f;
    [Range(-90f, 90f)] public float maxPitch = 60f;


    // --- Private Variables ---
    private float length1; // Length from joint1 to joint2
    private float length2; // Length from joint2 to end effector
    private float totalLength;
    private Vector3 joint1LocalAxis = Vector3.up;    // Local Y-axis for Joint 1 (Yaw)
    private Vector3 joint2LocalAxis = Vector3.right; // Local X-axis for Joint 2 (Pitch)

    // Store initial rotations for limit calculations relative to setup pose
    private Quaternion initialLocalRot1;
    private Quaternion initialLocalRot2;

    void Start() // Changed from Awake to ensure initial rotations are captured reliably
    {
        // Basic validation
        if (joint1_BaseYaw == null || joint2_HeadPitch == null || endEffector_LaserOrigin == null)
        {
            Debug.LogError("IK Chain Transforms (BaseYaw, HeadPitch, LaserOrigin) must be assigned.", this);
            enabled = false; // Disable script if setup is incorrect
            return;
        }
        if (target == null)
        {
            Debug.LogWarning("IK Target is not assigned. Script will do nothing until a target is assigned.", this);
            // Don't disable, target might be assigned later
        }

        // Calculate bone lengths based on initial pose
        CalculateLengths();

        // Store initial local rotations
        initialLocalRot1 = joint1_BaseYaw.localRotation;
        initialLocalRot2 = joint2_HeadPitch.localRotation;

        // Ensure Min/Max limits are logical
        if(minYaw > maxYaw) { float temp = minYaw; minYaw = maxYaw; maxYaw = temp; }
        if(minPitch > maxPitch) { float temp = minPitch; minPitch = maxPitch; maxPitch = temp; }

        // Calculate tolerance squared for efficient distance checks
        tolerance *= tolerance;
    }

    void LateUpdate()
    {
        if (target == null) return; // Do nothing if no target

        SolveIK();
    }

    void CalculateLengths()
    {
        // Ensure transforms are valid before calculating
        if (joint1_BaseYaw && joint2_HeadPitch)
            length1 = Vector3.Distance(joint1_BaseYaw.position, joint2_HeadPitch.position);
        else
            length1 = 0;

        if (joint2_HeadPitch && endEffector_LaserOrigin)
            length2 = Vector3.Distance(joint2_HeadPitch.position, endEffector_LaserOrigin.position);
        else
            length2 = 0;

        totalLength = length1 + length2;

        if (totalLength <= 0) {
             Debug.LogWarning("IK chain length is zero or negative. Check joint assignments and hierarchy.", this);
             // Consider disabling if length is essential, though aiming might still partially work
        }
    }

    void SolveIK()
    {
        Vector3 targetPosition = target.position;
        float targetDistSqr = (targetPosition - joint1_BaseYaw.position).sqrMagnitude;
        float totalLengthSqr = totalLength * totalLength;

        // --- Reachability Check (Optional but good for stability) ---
        // If the target is further than the arm can reach, clamp the target position
        // used for calculation to the maximum reach. This makes the turret point directly at it.
        if (targetDistSqr > totalLengthSqr && totalLength > 0)
        {
            Vector3 directionToTarget = (targetPosition - joint1_BaseYaw.position).normalized;
            targetPosition = joint1_BaseYaw.position + directionToTarget * totalLength; // Clamp target to max reach
        }
        // --- Add check for target too close? ---
        // Could implement similar clamping or specific behavior if needed.

        // --- CCD Iterations ---
        Vector3 currentEndEffectorPos = endEffector_LaserOrigin.position;
        for (int i = 0; i < iterations; i++)
        {
            // --- Joint 2 (Head Pitch - Local X Axis) ---
            bool rotated2 = RotateJoint(joint2_HeadPitch, joint2LocalAxis, currentEndEffectorPos, targetPosition, usePitchLimits, minPitch, maxPitch, initialLocalRot2);
            if(rotated2) currentEndEffectorPos = endEffector_LaserOrigin.position; // Update position only if rotation happened


            // --- Joint 1 (Base Yaw - Local Y Axis) ---
             bool rotated1 = RotateJoint(joint1_BaseYaw, joint1LocalAxis, currentEndEffectorPos, targetPosition, useYawLimits, minYaw, maxYaw, initialLocalRot1);
             if(rotated1) currentEndEffectorPos = endEffector_LaserOrigin.position; // Update position only if rotation happened


            // --- Check Tolerance ---
            // Use squared distance for efficiency
            if (Vector3.SqrMagnitude(currentEndEffectorPos - targetPosition) < tolerance)
            {
                // Debug.Log($"IK Converged in {i + 1} iterations.");
                break; // Solution found
            }
        }

        // --- Final Enforcement of Limits (Euler method, simpler but can have issues) ---
        // Apply limits directly after CCD loop for robustness, especially if CCD didn't converge perfectly within limits
        if (useYawLimits) ClampJointEuler(joint1_BaseYaw, joint1LocalAxis, minYaw, maxYaw);
        if (usePitchLimits) ClampJointEuler(joint2_HeadPitch, joint2LocalAxis, minPitch, maxPitch);

    }

    // Returns true if a rotation was applied, false otherwise
    bool RotateJoint(Transform joint, Vector3 localAxis, Vector3 currentEffectorPos, Vector3 targetPos,
                     bool useLimits, float minAngle, float maxAngle, Quaternion initialLocalRot)
    {
        // 1. Get vectors from the joint to the current end effector position and the target position
        Vector3 toEffector = currentEffectorPos - joint.position;
        Vector3 toTarget = targetPos - joint.position;

        // Ignore if vectors are zero length
        if (toEffector.sqrMagnitude < 0.0001f || toTarget.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        // 2. Calculate the rotation axis in world space
        Vector3 worldAxis = joint.TransformDirection(localAxis);

        // 3. Calculate the angle needed to rotate around the worldAxis
        float angle = Vector3.SignedAngle(toEffector, toTarget, worldAxis);

        // 4. Small angle optimization
        if (Mathf.Abs(angle) < 0.01f)
        {
             return false; // Avoid tiny rotations
        }


        // --- Apply Rotation ---
        // Apply the rotation first
        Quaternion deltaRotation = Quaternion.AngleAxis(angle, localAxis);
        joint.localRotation = joint.localRotation * deltaRotation; // Apply delta rotation

        return true; // Indicate rotation happened
    }

    // Clamps the joint's rotation based on Euler angles relative to its initial local rotation.
    // NOTE: Euler angles can be tricky (gimbal lock, wrapping). This is a simpler approach.
    // More robust methods might involve tracking angle accumulation or using different rotation representations.
    void ClampJointEuler(Transform joint, Vector3 localAxis, float minAngle, float maxAngle)
    {
        Vector3 currentLocalEuler = joint.localEulerAngles;
        float currentAngle = 0f;

        // Convert Euler angle range to 0-360 or find the relevant axis value
        // This needs careful handling based on the axis
        if (localAxis == Vector3.right) // Pitch (X)
        {
            currentAngle = ConvertEulerAngle(currentLocalEuler.x);
            currentAngle = Mathf.Clamp(currentAngle, minAngle, maxAngle);
            currentLocalEuler.x = currentAngle; // Assign clamped value back
        }
        else if (localAxis == Vector3.up) // Yaw (Y)
        {
            currentAngle = ConvertEulerAngle(currentLocalEuler.y);
            currentAngle = Mathf.Clamp(currentAngle, minAngle, maxAngle);
             currentLocalEuler.y = currentAngle; // Assign clamped value back
        }
        else if (localAxis == Vector3.forward) // Roll (Z)
        {
             currentAngle = ConvertEulerAngle(currentLocalEuler.z);
             currentAngle = Mathf.Clamp(currentAngle, minAngle, maxAngle);
             currentLocalEuler.z = currentAngle; // Assign clamped value back
        }
        else {
            Debug.LogWarning("Clamping not implemented for this local axis: " + localAxis, joint);
            return;
        }

        // Apply the clamped Euler angles
        joint.localEulerAngles = currentLocalEuler;
    }

    // Helper to convert Unity's 0-360 Euler range to a more usable -180 to 180 range
    float ConvertEulerAngle(float angle)
    {
        if (angle > 180)
            angle -= 360;
        return angle;
    }


    // Optional: Draw Gizmos for visualization
    void OnDrawGizmos()
    {
        if (joint1_BaseYaw && joint2_HeadPitch && endEffector_LaserOrigin)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(joint1_BaseYaw.position, joint2_HeadPitch.position);
            Gizmos.color = Color.red;
            Gizmos.DrawLine(joint2_HeadPitch.position, endEffector_LaserOrigin.position);

            // Draw Joint Axes
            float axisLength = 0.2f; // Increased length for visibility
            // Joint 1 Y (Green) - Yaw
            Gizmos.color = Color.green;
            Gizmos.DrawLine(joint1_BaseYaw.position, joint1_BaseYaw.position + joint1_BaseYaw.TransformDirection(Vector3.up) * axisLength);
            // Joint 2 X (Red) - Pitch
            Gizmos.color = Color.red;
            Gizmos.DrawLine(joint2_HeadPitch.position, joint2_HeadPitch.position + joint2_HeadPitch.TransformDirection(Vector3.right) * axisLength);
            // End Effector Forward (Blue) - Aim Direction
            Gizmos.color = Color.blue;
             Gizmos.DrawLine(endEffector_LaserOrigin.position, endEffector_LaserOrigin.position + endEffector_LaserOrigin.forward * axisLength * 1.5f); // Draw aim direction
        }
        if (target)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(target.position, 0.1f); // Slightly larger gizmo

            // Draw line from effector to target
            if(endEffector_LaserOrigin) {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(endEffector_LaserOrigin.position, target.position);
            }
        }


    }
}