using System;
using UnityEngine;

namespace ArcaneMotion.Gestures
{
    // One hand's gestures, as plain C# so that tests and recording replays run the same code as the
    // game. GestureCapture gives it the head's rotation at the press, then the controller's position
    // in tracking space each frame, with the seconds since the press, and the session keeps the
    // gesture's stored points.
    public sealed class GestureSession
    {
        private readonly float minPointSpacing;

        // Copies the library's settings, so later edits to the library do not change this session.
        public GestureSession(GestureLibrary library)
        {
            if (library == null)
                throw new ArgumentNullException(nameof(library));
            minPointSpacing = library.MinPointSpacing;
        }

        // The current gesture's stored points, or the last gesture's until the next Begin.
        public GesturePath Path { get; } = new GesturePath();

        // The head's rotation in tracking space at the current gesture's press, or the last
        // gesture's until the next Begin.
        public Quaternion HeadRotation { get; private set; } = Quaternion.identity;

        // Starts a new gesture at the button press, given the head's rotation in tracking space then.
        public void Begin(Quaternion headRotation)
        {
            Path.Clear();
            HeadRotation = headRotation;
        }

        // Runs every frame while the button is held. Stores the sample only when it is at least the
        // min point spacing from the last stored point, so that tracking jitter while the hand is
        // still adds no length to the path.
        public void Advance(Vector3 position, float time)
        {
            if (Path.Count > 0 &&
                (position - Path.PositionAt(Path.Count - 1)).sqrMagnitude < minPointSpacing * minPointSpacing)
                return;
            Path.Add(position, time);
        }
    }
}
