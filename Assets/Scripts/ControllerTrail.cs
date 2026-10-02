using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Draws a trail behind a tracked controller that tapers and fades out toward its tail.
// Trail points are stored in the XR rig's tracking space, so the trail moves with the player
// during locomotion, snap turns, and teleports, instead of streaking across the world.
[RequireComponent(typeof(LineRenderer))]
public class ControllerTrail : MonoBehaviour
{
    [SerializeField] private Transform trackedController;

    [Tooltip("The XR Origin's Camera Offset: the space that controller poses are tracked in.")]
    [SerializeField] private Transform trackingSpace;

    [Tooltip("Seconds a trail point lasts before it drops off the tail.")]
    [SerializeField, Min(0.01f)] private float duration = 0.5f;

    [Tooltip("Distance in meters the controller must move before a new trail point is added.")]
    [SerializeField, Min(0.001f)] private float minPointSpacing = 0.005f;

    // Oldest first, with positions in tracking space.
    private readonly List<(Vector3 position, float time)> points = new List<(Vector3 position, float time)>();
    private Vector3[] linePositions = new Vector3[64];
    private LineRenderer line;

    // Called by the Editor when the component is added (or reset): gives the LineRenderer a
    // look that tapers and fades from head (start) to tail (end). Tune it on the LineRenderer.
    private void Reset()
    {
        var lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 0;
        lineRenderer.widthMultiplier = 0.015f;
        lineRenderer.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        var trailColor = new Color(0.4f, 0.8f, 1f);
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(trailColor, 0f), new GradientColorKey(trailColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        lineRenderer.colorGradient = gradient;

        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 2;
        lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
    }

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        // Positions are converted from tracking space to world space every frame.
        line.useWorldSpace = true;
    }

    private void OnEnable()
    {
        if (trackedController == null || trackingSpace == null)
            throw new UnassignedReferenceException(
                $"{nameof(ControllerTrail)} on '{name}' needs both Tracked Controller and Tracking Space assigned.");

        // The controller's TrackedPoseDriver moves it again just before rendering, so the trail
        // is rebuilt then as well; otherwise the trail's head lags behind the controller model.
        Application.onBeforeRender += OnBeforeRender;
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRender;
        points.Clear();
        line.positionCount = 0;
    }

    private void OnBeforeRender()
    {
        // The trail follows real hand motion, so its timing ignores Time.timeScale.
        float now = Time.unscaledTime;
        Vector3 controllerPosition = trackingSpace.InverseTransformPoint(trackedController.position);

        int expiredCount = 0;
        while (expiredCount < points.Count && now - points[expiredCount].time > duration)
            expiredCount++;
        points.RemoveRange(0, expiredCount);

        if (points.Count == 0 ||
            (controllerPosition - points[points.Count - 1].position).sqrMagnitude >= minPointSpacing * minPointSpacing)
        {
            points.Add((controllerPosition, now));
        }

        UpdateLine(controllerPosition);
    }

    // Writes the head (the controller) first, so the LineRenderer's width curve and color
    // gradient run from head at the start to tail at the end, as on a TrailRenderer.
    private void UpdateLine(Vector3 controllerPosition)
    {
        // Between added points the controller is ahead of the newest point; draw that gap too,
        // but skip it when the two coincide, since a zero-length segment has no direction.
        bool drawHead = controllerPosition != points[points.Count - 1].position;
        int count = points.Count + (drawHead ? 1 : 0);
        if (linePositions.Length < count)
            Array.Resize(ref linePositions, count * 2);

        int index = 0;
        if (drawHead)
            linePositions[index++] = trackingSpace.TransformPoint(controllerPosition);
        for (int i = points.Count - 1; i >= 0; i--)
            linePositions[index++] = trackingSpace.TransformPoint(points[i].position);

        line.positionCount = count;
        line.SetPositions(linePositions);
    }
}
