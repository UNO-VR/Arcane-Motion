using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArcaneMotion.Gestures
{
    // Draws the gesture being captured as ink: the stored points, then a last segment to the live
    // controller position. While a shape is lit, the part that matched it takes the spell's color, or
    // the wrong-size color, and the parts before and after it are dimmed. On release the ink fades out
    // as it was last drawn, colors included, and a new press starts new ink. The ink is kept in
    // tracking space and converted to world space each frame, so it moves with the player during
    // locomotion, turns, and teleports.
    [RequireComponent(typeof(LineRenderer), typeof(GestureCapture))]
    public class GestureInkTrail : MonoBehaviour
    {
        [Tooltip("The ink's color while no shape is lit, and, while one is, beyond the part that recognition has " +
                 "evaluated: the newest points, and the last segment to the controller.")]
        [SerializeField] private Color neutralColor = new Color(0.4f, 0.8f, 1f);

        [Tooltip("The ink's color, while a shape is lit, before and after the part that matched it: the start " +
                 "trim, and the start and end cuts.")]
        [SerializeField] private Color dimmedColor = new Color(0.5f, 0.5f, 0.5f, 0.35f);

        [Tooltip("The matched part's color while a shape is lit but its size is in the range of none of its " +
                 "spells. A spell's match takes the spell's Ink Color.")]
        [SerializeField] private Color wrongSizeColor = new Color(1f, 0.3f, 0.1f);

        [Tooltip("Seconds the ink takes to fade out after the button is released.")]
        [SerializeField, Min(0f)] private float fadeDuration = 0.5f;

        private readonly Gradient gradient = new Gradient();
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
            // WriteInk sets the gradient's key times as fractions of the line's length, which is how
            // Stretch spaces a gradient. The per-segment texture modes space it by vertex index instead.
            line.textureMode = LineTextureMode.Stretch;
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

        private void OnCompleted(GestureCapture sender, RecognitionState shown)
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

        // Draws the stored points and the end of the last segment, colored by the state on show, at
        // the given opacity. The segment is left out unless the stored points are still the ones it
        // was drawn after.
        private void Draw(float opacity)
        {
            GesturePath path = capture.Path;
            RecognitionState state = capture.State;
            bool drawLastSegment = hasLastSegment && pointsBeforeLastSegment == path.Count;
            Color matchedColor = state.Status == RecognitionStatus.Spell ? state.Spell.InkColor : wrongSizeColor;
            int lineCount = WriteInk(path, drawLastSegment ? lastSegmentEnd : (Vector3?)null, state, matchedColor,
                dimmedColor, neutralColor, opacity, ref linePositions, gradient);

            Matrix4x4 toWorld = capture.TrackingSpace.localToWorldMatrix;
            for (int i = 0; i < lineCount; i++)
                linePositions[i] = toWorld.MultiplyPoint3x4(linePositions[i]);

            line.positionCount = lineCount;
            line.SetPositions(linePositions);
            line.colorGradient = gradient;
        }

        // Writes the ink's line into positions, in tracking space, and its colors into gradient, and
        // returns the number of vertices. The line is the stored points, then lastSegmentEnd if it
        // has a value. While a shape is lit, the line has regions by path length: dimmed before the
        // matched part, matchedColor on it, dimmed after it up to the end of the evaluated part, and
        // neutral beyond. Otherwise it is all neutral. Every color's alpha is scaled by opacity.
        // LineRenderer colors only its vertices, so a vertex is added where a region boundary falls
        // inside a segment, and the gradient has one Fixed key per region, at the fraction of the
        // line's length where the region ends.
        public static int WriteInk(GesturePath path, Vector3? lastSegmentEnd, RecognitionState state,
            Color matchedColor, Color dimmedColor, Color neutralColor, float opacity, ref Vector3[] positions,
            Gradient gradient)
        {
            int count = path.Count;
            float lineLength = path.Length;
            if (lastSegmentEnd.HasValue)
                lineLength += Vector3.Distance(path.PositionAt(count - 1), lastSegmentEnd.Value);

            // Each region's end as a path length, in drawing order, and its color.
            Span<float> ends = stackalloc float[4];
            Span<Color> colors = stackalloc Color[4];
            int regionCount = 0;
            if (state.Status != RecognitionStatus.None)
            {
                ends[0] = state.MatchedStart;
                colors[0] = dimmedColor;
                ends[1] = state.MatchedEnd;
                colors[1] = matchedColor;
                ends[2] = state.EvaluatedEnd;
                colors[2] = dimmedColor;
                regionCount = 3;
            }
            ends[regionCount] = lineLength;
            colors[regionCount] = neutralColor;
            regionCount++;

            // Room for a vertex at each boundary between regions, and for the last segment's end.
            int capacity = count + regionCount;
            if (positions.Length < capacity)
                Array.Resize(ref positions, capacity * 2);

            int lineCount = 0;
            int boundary = 0;
            // The path length at the last vertex written.
            float vertexLength = 0f;
            for (int i = 0; i < count; i++)
            {
                // Boundaries before this point that are past the last vertex lie inside the segment that
                // ends here. A boundary on a vertex, or on another boundary, needs no vertex of its own.
                // At i = 0 the loop does not run, since no boundary is below 0.
                float length = path.LengthAt(i);
                for (; boundary < regionCount - 1 && ends[boundary] < length; boundary++)
                {
                    if (ends[boundary] <= vertexLength)
                        continue;
                    float segmentStart = path.LengthAt(i - 1);
                    positions[lineCount++] = Vector3.Lerp(path.PositionAt(i - 1), path.PositionAt(i),
                        (ends[boundary] - segmentStart) / (length - segmentStart));
                    vertexLength = ends[boundary];
                }
                positions[lineCount++] = path.PositionAt(i);
                vertexLength = length;
            }
            if (lastSegmentEnd.HasValue)
                positions[lineCount++] = lastSegmentEnd.Value;

            Span<GradientColorKey> colorKeys = stackalloc GradientColorKey[4];
            Span<GradientAlphaKey> alphaKeys = stackalloc GradientAlphaKey[4];
            int keyCount = 0;
            float previousEnd = 0f;
            for (int k = 0; k < regionCount; k++)
            {
                // An empty region gets no key, except that the gradient needs one.
                if (ends[k] <= previousEnd && !(k == regionCount - 1 && keyCount == 0))
                    continue;
                // In Fixed mode, a key's color covers the times after the previous key, up to and
                // including its own time. Unity rounds key times to the nearest 1/65535, so each key
                // goes one step past its boundary, or rounding down would give the boundary's vertex
                // the next region's color.
                float time = lineLength > 0f ? Mathf.Min(ends[k] / lineLength + 1f / 65535f, 1f) : 1f;
                colorKeys[keyCount] = new GradientColorKey(colors[k], time);
                alphaKeys[keyCount] = new GradientAlphaKey(colors[k].a * opacity, time);
                keyCount++;
                previousEnd = ends[k];
            }
            gradient.mode = GradientMode.Fixed;
            gradient.SetKeys(colorKeys.Slice(0, keyCount), alphaKeys.Slice(0, keyCount));
            return lineCount;
        }
    }
}
