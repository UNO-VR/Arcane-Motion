using System;
using System.IO;
using ArcaneMotion.Gestures.Editor;
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

        private readonly TestAssets assets = new TestAssets();
        private GestureLibrary library;
        private string folder;

        [SetUp]
        public void CreateLibrary()
        {
            library = ScriptableObject.CreateInstance<GestureLibrary>();
            library.MinPointSpacing = Spacing;
            // A new folder for each test, which does not exist until a test writes to it.
            folder = Path.Combine(Path.GetTempPath(), "ArcaneMotion.Gestures.Tests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void DestroyLibrary()
        {
            UnityEngine.Object.DestroyImmediate(library);
            assets.DestroyAll();
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
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
                new DateTime(2026, 10, 13, 18, 45, 1, DateTimeKind.Utc), headRotation, positions, times, live.State);

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

        // Saved as GestureRecorder saves a gesture, with the state on show at release, and loaded
        // back from its file.
        [Test]
        public void Run_ARecordingReplaysToItsShownState()
        {
            GestureShape circle = assets.Shape("Circle", true, false,
                GestureShapeGenerator.Generate(ShapePlane.Facing, 0, false, 0.15f, 90f, ShapeDirection.Clockwise));
            GestureLibrary circles = assets.Library("Test Library", 64, 3,
                assets.Spell("Small Circle", circle, 0.03f, 0.1f), assets.Spell("Large Circle", circle, 0.1f, 0.6f));
            // The player faces away from the tracking space's +z, and draws a circle of radius 15 cm
            // facing them, from its set start at the top, a little past one loop at 72 Hz.
            Quaternion headRotation = Quaternion.Euler(15f, 140f, 0f);
            Quaternion toTracking = Quaternion.Inverse(GestureNormalization.GestureFrameRotation(headRotation));
            const int frames = 75;
            var positions = new Vector3[frames];
            var times = new float[frames];
            var live = new GestureSession(circles);
            live.Begin(headRotation);
            for (int frame = 0; frame < frames; frame++)
            {
                positions[frame] = Center + toTracking * (0.15f * SyntheticGestures.CirclePoint(frame / 68.0));
                times[frame] = frame / 72f;
                live.Advance(positions[frame], times[frame]);
            }
            Assert.That(live.State.Status, Is.EqualTo(RecognitionStatus.Spell), "The live session lit a spell");
            string path = Path.Combine(folder, "take.json");
            new GestureRecording("Circle", "Clean", "Right", new DateTime(2026, 10, 25, 20, 0, 0, DateTimeKind.Utc),
                headRotation, positions, times, live.State).Save(path);

            GestureRecording loaded = GestureRecording.Load(path);
            GestureSession replayed = GestureReplay.Run(loaded, circles);

            RecognitionState state = replayed.State;
            Assert.That(state.Status.ToString(), Is.EqualTo(loaded.Shown.Status));
            Assert.That(state.Shape.name, Is.EqualTo(loaded.Shown.Shape));
            Assert.That(state.Spell.name, Is.EqualTo(loaded.Shown.Spell));
            Assert.That(state.Score, Is.EqualTo(loaded.Shown.Score));
            Assert.That(state.Size, Is.EqualTo(loaded.Shown.Size));
        }
    }
}
