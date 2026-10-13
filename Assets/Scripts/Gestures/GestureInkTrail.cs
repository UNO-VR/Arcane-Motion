using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArcaneMotion.Gestures
{
    // Draws the gesture being captured as ink: the stored points, then a last segment to the live
    // controller position. On release the ink fades out as it was last drawn, and a new press starts
    // new ink. The ink is kept in tracking space and converted to world space each frame, so it moves
    // with the player during locomotion, turns, and teleports.
    [RequireComponent(typeof(LineRenderer), typeof(GestureCapture))]
    public class GestureInkTrail : MonoBehaviour
    {
        [Tooltip("The ink's color.")]
        [SerializeField] private Color neutralColor = new Color(0.4f, 0.8f, 1f);

        [Tooltip("Seconds the ink takes to fade out after the button is released.")]
        [SerializeField, Min(0f)] private float fadeDuration = 0.5f;

        private readonly Gradient gradient = new Gradient();
        private readonly GradientColorKey[] colorKeys = new GradientColorKey[2];
        private readonly GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
        private Vector3[] linePositions = new Vector3[256];
        private GestureCapture capture;
        private LineRenderer line;
        private bool fading;
        private double fadeStartTime;

        // The last segment as last drawn while capturing: its end in tracking space, and the number
        // of stored points before it. The controller is usually ahead of the newest stored point, so
        // the fade keeps this segment; otherwise release would pull the end of the ink back.
        private bool hasLastSegment;
        private Vector3 lastSegmentEnd;
        private int pointsBeforeLastSegment;

        // Called by the Editor when the component is added (or reset): gives the LineRenderer an even
        // width, rounded ends and corners, and no shadows. Tune it on the LineRenderer; the ink's
        // color is set on this component.
        private void Reset()
        {
            var lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 0;
            lineRenderer.widthMultiplier = 0.01f;
            lineRenderer.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 2;
            lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
        }

        private void Awake()
        {
            capture = GetComponent<GestureCapture>();
            line = GetComponent<LineRenderer>();
            // Positions are converted from tracking space to world space every frame.
            line.useWorldSpace = true;
        }

        private void OnEnable()
        {
            capture.Started += OnStarted;
            capture.Completed += OnCompleted;
            // The controller's TrackedPoseDriver moves it again just before rendering, so the ink is
            // drawn then; otherwise its last segment would lag behind the controller model.
            Application.onBeforeRender += OnBeforeRender;
        }

        private void OnDisable()
        {
            capture.Started -= OnStarted;
            capture.Completed -= OnCompleted;
            Application.onBeforeRender -= OnBeforeRender;
            fading = false;
            hasLastSegment = false;
            line.positionCount = 0;
        }

        // A new press clears the last gesture's ink, including the segment kept for its fade.
        private void OnStarted(GestureCapture sender)
        {
            fading = false;
            hasLastSegment = false;
        }

        private void OnCompleted(GestureCapture sender)
        {
            fading = true;
            fadeStartTime = Time.unscaledTimeAsDouble;
        }

        private void OnBeforeRender()
        {
            if (capture.IsCapturing)
            {
                // The segment is left out when the controller is still at the newest stored point,
                // since a zero-length segment has no direction. While capturing, the press stored a point.
                GesturePath path = capture.Path;
                lastSegmentEnd = capture.TrackingSpace.InverseTransformPoint(capture.TrackedController.position);
                pointsBeforeLastSegment = path.Count;
                hasLastSegment = lastSegmentEnd != path.PositionAt(path.Count - 1);
                Draw(1f);
                return;
            }

            if (fading)
            {
                // Like the gesture, the fade follows real time and ignores Time.timeScale.
                double elapsed = Time.unscaledTimeAsDouble - fadeStartTime;
                if (elapsed < fadeDuration)
                {
                    Draw(1f - (float)(elapsed / fadeDuration));
                    return;
                }
                fading = false;
            }

            // The fade has ended, or the capture abandoned its gesture.
            line.positionCount = 0;
        }

        // Writes the stored points in drawing order, then the end of the last segment, all in the
        // neutral color at the given opacity. The segment is left out unless the stored points are
        // still the ones it was drawn after.
        private void Draw(float opacity)
        {
            GesturePath path = capture.Path;
            int count = path.Count;
            bool drawLastSegment = hasLastSegment && pointsBeforeLastSegment == count;
            int lineCount = count + (drawLastSegment ? 1 : 0);
            if (linePositions.Length < lineCount)
                Array.Resize(ref linePositions, lineCount * 2);

            Matrix4x4 toWorld = capture.TrackingSpace.localToWorldMatrix;
            for (int i = 0; i < count; i++)
                linePositions[i] = toWorld.MultiplyPoint3x4(path.PositionAt(i));
            if (drawLastSegment)
                linePositions[count] = toWorld.MultiplyPoint3x4(lastSegmentEnd);

            line.positionCount = lineCount;
            line.SetPositions(linePositions);

            float alpha = neutralColor.a * opacity;
            colorKeys[0] = new GradientColorKey(neutralColor, 0f);
            colorKeys[1] = new GradientColorKey(neutralColor, 1f);
            alphaKeys[0] = new GradientAlphaKey(alpha, 0f);
            alphaKeys[1] = new GradientAlphaKey(alpha, 1f);
            gradient.SetKeys(colorKeys, alphaKeys);
            line.colorGradient = gradient;
        }
    }
}
