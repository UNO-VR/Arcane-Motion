using System.Collections.Generic;
using ArcaneMotion.Gestures.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ArcaneMotion.Gestures.Tests
{
    public class GestureSessionTests
    {
        // Larger than the default of 5 mm, so that a session that ignored the library's setting fails.
        private const float Spacing = 0.02f;

        // 72 Hz, as the Quest ran in the recordings. On the line at 1 m/s, a time in seconds is then a
        // path length in meters, and each frame moves the hand 1.4 cm, past the default spacing.
        private const float FrameTime = 1f / 72f;

        // The samples lie on a line through a point about where a hand would be, in a direction that
        // changes every axis. The direction has unit length, so a distance along it is in meters.
        private static readonly Vector3 LineStart = new Vector3(0.3f, 1.2f, -0.4f);
        private static readonly Vector3 LineDirection = new Vector3(2f, 1f, -2f) / 3f;

        private readonly TestAssets assets = new TestAssets();
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
            Object.DestroyImmediate(library);
            assets.DestroyAll();
        }

        private static Vector3 OnLine(float distance)
        {
            return LineStart + LineDirection * distance;
        }

        // A library with the plan's settings and one spell for each min size, each on its own line
        // shape, so that the size ranges cannot overlap.
        private GestureLibrary MatchingLibrary(params float[] minSizes)
        {
            var spells = new SpellGesture[minSizes.Length];
            for (int i = 0; i < minSizes.Length; i++)
            {
                GestureShape line = assets.Shape($"Line {i}", false, false, Vector3.zero, Vector3.right);
                spells[i] = assets.Spell($"Line Spell {i}", line, minSizes[i], 10f);
            }
            return assets.Library("Test Library", 64, 3, spells);
        }

        // Turns off the update distance and the update rate, so the matcher runs on every frame
        // whose evaluated part is long enough.
        private static void UpdateEveryFrame(GestureLibrary matching)
        {
            matching.UpdateDistance = 0f;
            matching.MaxUpdatesPerSecond = 1e6f;
        }

        // Moves the hand along the line at 1 m/s for the given frames, from the press.
        private static void DrawLine(GestureSession session, int frames)
        {
            for (int frame = 0; frame < frames; frame++)
                session.Advance(OnLine(frame * FrameTime), frame * FrameTime);
        }

        // Draws a clean circle with a radius of 15 cm at about 1 m/s, 68 frames to a loop, from its set
        // start, facing the player. Adds each frame's status to statuses, and returns the frames in
        // which Advance reported a change.
        private static List<int> DrawCircle(GestureSession session, float loops, List<RecognitionStatus> statuses)
        {
            var center = new Vector3(0.2f, 1.3f, 0.4f);
            var reports = new List<int>();
            for (int frame = 0; frame <= loops * 68; frame++)
            {
                if (session.Advance(center + 0.15f * SyntheticGestures.CirclePoint(frame / 68.0), frame * FrameTime))
                    reports.Add(frame);
                statuses.Add(session.State.Status);
            }
            return reports;
        }

        private GestureLibrary CircleLibrary()
        {
            GestureShape circle = assets.Shape("Circle", true, false,
                GestureShapeGenerator.Generate(ShapePlane.Facing, 0, false, 0.15f, 90f, ShapeDirection.Clockwise));
            return assets.Library("Test Library", 64, 3, assets.Spell("Circle Spell", circle, 0.01f, 1f));
        }

        [Test]
        public void Advance_StoresOnlySamplesAtTheSpacingFromTheLastStoredPoint()
        {
            // Distances along the line, and whether each sample is stored.
            (float distance, bool stored)[] samples =
            {
                (0f, true),
                (0.012f, false),
                (0.019f, false),
                // 2.1 cm from the last stored point, though only 2 mm from the last sample: a slow
                // drift is still recorded.
                (0.021f, true),
                // Jitter while the hand is almost still.
                (0.03f, false),
                (0.025f, false),
                (0.035f, false),
                (0.04f, false),
                (0.042f, true),
            };

            var session = new GestureSession(library);
            session.Begin(Quaternion.identity);
            int expectedCount = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                session.Advance(OnLine(samples[i].distance), 0.011f * i);
                if (samples[i].stored)
                    expectedCount++;
            }

            Assert.That(session.Path.Count, Is.EqualTo(expectedCount));
            int stored = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                if (!samples[i].stored)
                    continue;
                Assert.That(session.Path.PositionAt(stored), Is.EqualTo(OnLine(samples[i].distance)),
                    $"Stored point {stored}");
                stored++;
            }
            // The jitter added no length.
            Assert.That(session.Path.Length, Is.EqualTo(0.042f).Within(1e-6f));
        }

        [Test]
        public void Advance_StoredPointsKeepTheirTimesSinceThePress()
        {
            // The hand moves at 1 m/s and is sampled 90 times a second, so it moves 1.1 cm between
            // samples, and every second sample is 2.2 cm from the last stored point.
            const float frameTime = 1f / 90f;
            var session = new GestureSession(library);
            session.Begin(Quaternion.identity);
            for (int frame = 0; frame < 10; frame++)
                session.Advance(OnLine(frame * frameTime), frame * frameTime);

            Assert.That(session.Path.Count, Is.EqualTo(5));
            for (int i = 0; i < session.Path.Count; i++)
                Assert.That(session.Path.TimeAt(i), Is.EqualTo(2 * i * frameTime), $"Time of stored point {i}");
        }

        [Test]
        public void Begin_StartsANewGestureAtTheNextSample()
        {
            var session = new GestureSession(library);
            session.Begin(new Quaternion(0.1f, 0.7f, -0.1f, 0.7f));
            session.Advance(OnLine(0f), 0f);
            session.Advance(OnLine(0.1f), 0.2f);

            var headRotation = new Quaternion(-0.2f, 0.3f, 0.1f, 0.9f);
            session.Begin(headRotation);
            Assert.That(session.Path.Count, Is.Zero);
            Assert.That(session.Path.Length, Is.Zero);
            Assert.That(session.HeadRotation, Is.EqualTo(headRotation));

            // Within the spacing of the last gesture's newest point, but stored, because it is the
            // new gesture's first sample.
            session.Advance(OnLine(0.105f), 0f);
            Assert.That(session.Path.Count, Is.EqualTo(1));
            Assert.That(session.Path.PositionAt(0), Is.EqualTo(OnLine(0.105f)));
            Assert.That(session.Path.TimeAt(0), Is.Zero);
            Assert.That(session.Path.Length, Is.Zero);
        }

        [Test]
        public void Begin_ForgetsTheLastGesturesState()
        {
            var session = new GestureSession(CircleLibrary());
            session.Begin(Quaternion.identity);
            DrawCircle(session, 1.1f, new List<RecognitionStatus>());
            Assert.That(session.State.Status, Is.EqualTo(RecognitionStatus.Spell), "The first gesture");

            session.Begin(Quaternion.identity);

            Assert.That(session.State.Status, Is.EqualTo(RecognitionStatus.None));
            Assert.That(session.State.Shape, Is.Null);
            Assert.That(session.State.Score, Is.EqualTo(float.PositiveInfinity));
            Assert.That(session.MatchCount, Is.Zero);
        }

        [Test]
        public void Advance_EvaluatesFromTheStartTrimAfterThePressToTheEndTrimBeforeNow()
        {
            GestureLibrary matching = MatchingLibrary(0.01f);
            // Trims that end between frames, so that the evaluated part starts and ends mid-segment.
            matching.StartTrim = 0.05f;
            matching.EndTrim = 0.1f;
            UpdateEveryFrame(matching);
            var session = new GestureSession(matching);
            session.Begin(Quaternion.identity);

            DrawLine(session, 30);

            Assert.That(session.MatchCount, Is.GreaterThan(0));
            Assert.That(session.State.EvaluatedStart, Is.EqualTo(0.05f).Within(1e-5f), "The start trim leaves out the first 5 cm");
            Assert.That(session.State.EvaluatedEnd, Is.EqualTo(29 * FrameTime - 0.1f).Within(1e-5f),
                "The end trim leaves out the newest 10 cm");
        }

        // The smallest min size is 5 cm, which the line passes in frame 4.
        [Test]
        public void Advance_DoesNotMatchBelowTheSmallestMinSize()
        {
            GestureLibrary matching = MatchingLibrary(0.08f, 0.05f);
            matching.StartTrim = 0f;
            matching.EndTrim = 0f;
            UpdateEveryFrame(matching);
            var session = new GestureSession(matching);
            session.Begin(Quaternion.identity);

            for (int frame = 0; frame < 8; frame++)
            {
                session.Advance(OnLine(frame * FrameTime), frame * FrameTime);
                Assert.That(session.MatchCount, Is.EqualTo(frame < 4 ? 0 : frame - 3), $"Frame {frame}");
            }
        }

        // The first run is in frame 1, at 1.4 cm. Two frames later the path has grown by 2.8 cm, and
        // three frames later by 4.2 cm.
        [Test]
        public void Advance_MatchesAgainOnlyOnceThePathHasGrownByTheUpdateDistance()
        {
            GestureLibrary matching = MatchingLibrary(0.01f);
            matching.StartTrim = 0f;
            matching.EndTrim = 0f;
            UpdateEveryFrame(matching);
            matching.UpdateDistance = 0.03f;
            var session = new GestureSession(matching);
            session.Begin(Quaternion.identity);

            for (int frame = 0; frame < 30; frame++)
            {
                session.Advance(OnLine(frame * FrameTime), frame * FrameTime);
                Assert.That(session.MatchCount, Is.EqualTo(frame < 1 ? 0 : 1 + (frame - 1) / 3), $"Frame {frame}");
            }
        }

        // The first run is in frame 1. 0.1 s later is 7.2 frames later, so the next run is 8 frames on.
        [Test]
        public void Advance_MatchesNoMoreOftenThanTheMaxUpdatesPerSecond()
        {
            GestureLibrary matching = MatchingLibrary(0.01f);
            matching.StartTrim = 0f;
            matching.EndTrim = 0f;
            UpdateEveryFrame(matching);
            matching.MaxUpdatesPerSecond = 10f;
            var session = new GestureSession(matching);
            session.Begin(Quaternion.identity);

            for (int frame = 0; frame < 40; frame++)
            {
                session.Advance(OnLine(frame * FrameTime), frame * FrameTime);
                Assert.That(session.MatchCount, Is.EqualTo(frame < 1 ? 0 : 1 + (frame - 1) / 8), $"Frame {frame}");
            }
        }

        // With the plan's settings. After the hand stops, one run catches up with the end trim and the
        // update rate, and then jitter below the spacing runs nothing for a second.
        [Test]
        public void Advance_DoesNotMatchWhileTheControllerIsStill()
        {
            var session = new GestureSession(MatchingLibrary(0.01f));
            session.Begin(Quaternion.identity);
            DrawLine(session, 20);

            var random = new System.Random(5);
            int countAfterCatchUp = -1;
            for (int frame = 20; frame < 20 + 72; frame++)
            {
                var jitter = 0.001f * new Vector3((float)random.NextDouble(), (float)random.NextDouble(),
                    (float)random.NextDouble());
                session.Advance(OnLine(19 * FrameTime) + jitter, frame * FrameTime);
                if (frame == 30)
                    countAfterCatchUp = session.MatchCount;
            }

            Assert.That(session.Path.Count, Is.EqualTo(20), "The jitter stored no points");
            Assert.That(session.State.EvaluatedEnd, Is.EqualTo(session.Path.Length).Within(0.001f),
                "The run after the hand stopped evaluated the whole path");
            Assert.That(session.MatchCount, Is.EqualTo(countAfterCatchUp));
        }

        // The circle lights partway through its loop and stays lit past it, while later runs change
        // only its score.
        [Test]
        public void Advance_ReportsEachChangeOnce()
        {
            var session = new GestureSession(CircleLibrary());
            session.Begin(Quaternion.identity);
            var statuses = new List<RecognitionStatus>();

            List<int> reports = DrawCircle(session, 1.3f, statuses);

            int lit = statuses.IndexOf(RecognitionStatus.Spell);
            Assert.That(lit, Is.GreaterThan(0), "The circle lit");
            Assert.That(statuses.LastIndexOf(RecognitionStatus.None), Is.EqualTo(lit - 1), "It stayed lit");
            Assert.That(reports, Is.EqualTo(new[] { lit }));
            Assert.That(session.MatchCount, Is.GreaterThan(10), "Many runs, each changing the score");
        }

        [Test]
        public void Advance_ThatMatches_AfterAWarmUpGesture_AllocatesNothing()
        {
            GestureLibrary matching = CircleLibrary();
            GestureShape line = assets.Shape("Line", false, false, Vector3.left, Vector3.right);
            matching.Spells.Add(assets.Spell("Line Spell", line, 0.01f, 10f));
            UpdateEveryFrame(matching);
            var session = new GestureSession(matching);
            var offset = new Vector3(0.4f, 1.2f, 0.5f);
            Vector3[] points = SyntheticGestures.Sloppy(SyntheticGestures.Kind.Circle, new System.Random(1));
            for (int i = 0; i < points.Length; i++)
                points[i] = offset + 0.15f * points[i];

            session.Begin(Quaternion.identity);
            for (int i = 0; i < points.Length; i++)
                session.Advance(points[i], i * FrameTime);
            session.Begin(Quaternion.identity);
            for (int i = 0; i < points.Length - 1; i++)
                session.Advance(points[i], i * FrameTime);
            int countBefore = session.MatchCount;
            Vector3 position = points[points.Length - 1];
            float time = (points.Length - 1) * FrameTime;

            // A statement body, so that the constraint gets a TestDelegate rather than one returning a value.
            Assert.That(() => { session.Advance(position, time); }, Is.Not.AllocatingGCMemory());
            Assert.That(session.MatchCount, Is.GreaterThan(countBefore), "The call ran the matcher");
        }
    }
}
