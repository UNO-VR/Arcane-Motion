using System;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneMotion.Gestures.Tests
{
    public class GestureReplayTests
    {
        // Larger than the default of 5 mm, so that a replay that ignored the library's setting fails.
        private const float Spacing = 0.02f;

        private const int Frames = 150;
        private const float FrameTime = 1f / 90f;
        private static readonly Vector3 Center = new Vector3(0.2f, 1.3f, 0.4f);

        private GestureLibrary library;

        [SetUp]
        public void CreateLibrary()
        {
            library = ScriptableObject.CreateInstance<GestureLibrary>();
            library.MinPointSpacing = Spacing;
        }

        [TearDown]
        public void DestroyLibrary()
        {
            UnityEngine.Object.DestroyImmediate(library);
        }

        [Test]
        public void Run_RebuildsTheSessionThatWasRecorded()
        {
            var headRotation = new Quaternion(0.05f, 0.92f, -0.12f, 0.37f);
            var positions = new Vector3[Frames];
            var times = new float[Frames];
            var random = new System.Random(3);

            // A live session, fed as GestureCapture feeds it, while every frame's sample is kept, as
            // GestureRecorder keeps them. The hand draws a circle of radius 15 cm, and halfway it
            // pauses for a third of a second while tracking jitter of up to 1 mm moves the controller.
            var live = new GestureSession(library);
            live.Begin(headRotation);
            for (int frame = 0; frame < Frames; frame++)
            {
                int framesMoved = frame < 60 ? frame : frame < 90 ? 60 : frame - 30;
                float angle = 2f * Mathf.PI * framesMoved / 120f;
                Vector3 jitter = frame >= 60 && frame < 90
                    ? 0.001f * new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble())
                    : Vector3.zero;
                positions[frame] = Center + 0.15f * new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) + jitter;
                times[frame] = frame * FrameTime;
                live.Advance(positions[frame], times[frame]);
            }
            var recording = new GestureRecording("Circle", "", "Left",
                new DateTime(2026, 10, 13, 18, 45, 1, DateTimeKind.Utc), headRotation, positions, times);

            GestureSession replayed = GestureReplay.Run(recording, library);

            // The live session left samples out, so the replay must apply the spacing rule as it did.
            Assert.That(live.Path.Count, Is.LessThan(Frames));
            Assert.That(replayed.HeadRotation, Is.EqualTo(headRotation));
            Assert.That(replayed.Path.Count, Is.EqualTo(live.Path.Count));
            for (int i = 0; i < live.Path.Count; i++)
            {
                Assert.That(replayed.Path.PositionAt(i), Is.EqualTo(live.Path.PositionAt(i)), $"Position of stored point {i}");
                Assert.That(replayed.Path.TimeAt(i), Is.EqualTo(live.Path.TimeAt(i)), $"Time of stored point {i}");
            }
            Assert.That(replayed.Path.Length, Is.EqualTo(live.Path.Length));
        }
    }
}
