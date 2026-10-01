using UnityEngine;

public class ControllerMotionTracker : MonoBehaviour
{
    [SerializeField] private Transform trackedController;

    public Vector3 Position { get; private set; }
    public Quaternion Rotation { get; private set; }
    public Vector3 Velocity { get; private set; }

    private Vector3 previousPosition;

    private void Start()
    {
        previousPosition = trackedController.position;
        Position = trackedController.position;
    }

    private void Update()
    {
        previousPosition = Position;

        Position = trackedController.position;
        Rotation = trackedController.rotation;
        Velocity = (Position - previousPosition) / Time.deltaTime;

        Debug.Log($"{trackedController.name}: {Position}");
    }
}