using UnityEngine;
using System;
using System.Collections;

public class ObjectController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float movementSpeed = 2.0f;
    [SerializeField] private float rotationSpeed = 5.0f;
    
    // Movement variables
    [HideInInspector]
    public Vector3 targetPosition;
    
    void Start()
    {
        // Initialize position
        targetPosition = transform.position;
    }
    
    void Update()
    {
        // Move object towards target position
        transform.position = targetPosition;
        
        
    }
    
    // Public method to update the target position from WebSocketAdapter
    public void SetTargetPosition(Vector3 position)
    {
        targetPosition = position;
        Debug.Log($"Object target position updated: X: {position.x}, Y: {position.y}, Z: {position.z}");
    }
}