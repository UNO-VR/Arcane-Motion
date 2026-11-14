using System;
using UnityEngine;

namespace ArcaneMotion.Gestures
{
    // One hand's gestures, as plain C# so that tests and recording replays run the same code as the
    // game. GestureCapture gives it the head's rotation at the press, then the controller's position
    // in tracking space each frame, with the seconds since the press. The session keeps the
    // gesture's stored points and, as they grow, matches them against the library's shapes and keeps
    // the state on show. Nothing runs at release: the state on show is the state that is cast.
    public sealed class GestureSession
    {
        private static readonly RecognitionState Unmatched = new RecognitionState(RecognitionStatus.None, null, null,
            float.PositiveInfinity, 0f, 0f, 0f, 0f, 0f);

        private readonly float minPointSpacing;
        private readonly float startTrim;
        private readonly float endTrim;
        private readonly float updateDistance;
        private readonly float updateInterval;
        // An evaluated part shorter than the smallest min size cannot have the RMS size of any spell,
        // so the matcher waits for it. With no spells it is infinity, and the session only records.
        private readonly float minEvaluatedLength = float.PositiveInfinity;
        private readonly GestureMatcher matcher;
        private readonly GestureRecognizer recognizer;
        private Quaternion frameRotation = Quaternion.identity;
        // The end of the part that the matcher last evaluated, and the time of that sample.
        private float lastMatchEnd;
        private float lastMatchTime;

        // Compiles the library's templates and copies its settings, so later edits to the library do
        // not change this session. Throws, listing every problem, if the library has any.
        public GestureSession(GestureLibrary library)
        {
            if (library == null)
                throw new ArgumentNullException(nameof(library));
            matcher = new GestureMatcher(library);
            recognizer = new GestureRecognizer(library, matcher.Templates);
            minPointSpacing = library.MinPointSpacing;
            startTrim = library.StartTrim;
            endTrim = library.EndTrim;
            updateDistance = library.UpdateDistance;
            updateInterval = 1f / library.MaxUpdatesPerSecond;
            foreach (SpellGesture spell in library.Spells)
                minEvaluatedLength = Mathf.Min(minEvaluatedLength, spell.MinSize);
        }

        // The current gesture's stored points, or the last gesture's until the next Begin.
        public GesturePath Path { get; } = new GesturePath();

        // The head's rotation in tracking space at the current gesture's press, or the last
        // gesture's until the next Begin.
        public Quaternion HeadRotation { get; private set; } = Quaternion.identity;

        // The state on show, from the matcher's latest run in the current gesture, or the last
        // gesture's until the next Begin. Until the matcher first runs, it is None with no shape.
        public RecognitionState State { get; private set; } = Unmatched;

        // How many times the matcher has run in the current gesture, or the last gesture until the
        // next Begin.
        public int MatchCount { get; private set; }

        // Starts a new gesture at the button press, given the head's rotation in tracking space then.
        // Forgets the last gesture's state, lit shape, and the matcher's hint.
        public void Begin(Quaternion headRotation)
        {
            Path.Clear();
            HeadRotation = headRotation;
            frameRotation = GestureNormalization.GestureFrameRotation(headRotation);
            matcher.Reset();
            recognizer.Reset();
            State = Unmatched;
            MatchCount = 0;
        }

        // Runs every frame while the button is held. Stores the sample only when it is at least the
        // min point spacing from the last stored point, so that tracking jitter while the hand is
        // still adds no length to the path. Then matches the evaluated part, the path from the start
        // trim after the press to the end trim before this sample, if it is at least as long as the
        // smallest min size, has grown by the update distance since the last run, and the update
        // interval has passed since then. Returns whether the status changed, or the shape or spell
        // changed while lit.
        public bool Advance(Vector3 position, float time)
        {
            if (Path.Count == 0 ||
                (position - Path.PositionAt(Path.Count - 1)).sqrMagnitude >= minPointSpacing * minPointSpacing)
                Path.Add(position, time);

            float evaluatedStart = Path.LengthAtTime(startTrim);
            float evaluatedEnd = Path.LengthAtTime(time - endTrim);
            // While the trims overlap, the length is negative, so this waits too.
            if (evaluatedEnd - evaluatedStart < minEvaluatedLength)
                return false;
            if (MatchCount > 0 && (evaluatedEnd - lastMatchEnd < updateDistance || time - lastMatchTime < updateInterval))
                return false;
            MatchCount++;
            lastMatchEnd = evaluatedEnd;
            lastMatchTime = time;

            matcher.Match(Path, evaluatedStart, evaluatedEnd, frameRotation);
            RecognitionState previous = State;
            State = recognizer.Recognize(matcher.Result, evaluatedStart, evaluatedEnd);
            return State.Status != previous.Status ||
                   (State.Status != RecognitionStatus.None &&
                    (State.Shape != previous.Shape || State.Spell != previous.Spell));
        }
    }
}
