using NUnit.Framework;
using UnityEngine;

namespace ArcaneMotion.Gestures.Tests
{
    public class GestureSessionTests
    {
        // Larger than the default of 5 mm, so that a session that ignored the library's setting fails.
        private const float Spacing = 0.02f;

        // The samples lie on a line through a point about where a hand would be, in a direction that
        // changes every axis. The direction has unit length, so a distance along it is in meters.
        private static readonly Vector3 LineStart = new Vector3(0.3f, 1.2f, -0.4f);
        private static readonly Vector3 LineDirection = new Vector3(2f, 1f, -2f) / 3f;

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
        }

        private static Vector3 OnLine(float distance)
        {
            return LineStart + LineDirection * distance;
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
            session.Begin();
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
            session.Begin();
            for (int frame = 0; frame < 10; frame++)
                session.Advance(OnLine(frame * frameTime), frame * frameTime);

            Assert.That(session.Path.Count, Is.EqualTo(5));
            for (int i = 0; i < session.Path.Count; i++)
                Assert.That(session.Path.TimeAt(i), Is.EqualTo(2 * i * frameTime), $"Time of stored point {i}");
        }

        [Test]
        public void Begin_StartsANewPathAtTheNextSample()
        {
            var session = new GestureSession(library);
            session.Begin();
            session.Advance(OnLine(0f), 0f);
            session.Advance(OnLine(0.1f), 0.2f);

            session.Begin();
            Assert.That(session.Path.Count, Is.Zero);
            Assert.That(session.Path.Length, Is.Zero);

            // Within the spacing of the last gesture's newest point, but stored, because it is the
            // new gesture's first sample.
            session.Advance(OnLine(0.105f), 0f);
            Assert.That(session.Path.Count, Is.EqualTo(1));
            Assert.That(session.Path.PositionAt(0), Is.EqualTo(OnLine(0.105f)));
            Assert.That(session.Path.TimeAt(0), Is.Zero);
            Assert.That(session.Path.Length, Is.Zero);
        }
    }
}
