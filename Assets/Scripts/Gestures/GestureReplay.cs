using System;

namespace ArcaneMotion.Gestures
{
    // Runs a recording through a new session, as GestureCapture ran the gesture in the headset, so
    // that with the same library the session ends as it did there.
    public static class GestureReplay
    {
        // Begins the session with the recording's head rotation, advances it with each sample in
        // order, and returns it. Its state is then the state on show at release.
        public static GestureSession Run(GestureRecording recording, GestureLibrary library)
        {
            if (recording == null)
                throw new ArgumentNullException(nameof(recording));

            var session = new GestureSession(library);
            session.Begin(recording.HeadRotation);
            for (int i = 0; i < recording.Positions.Count; i++)
                session.Advance(recording.Positions[i], recording.Times[i]);
            return session;
        }
    }
}
