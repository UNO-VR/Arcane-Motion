using System;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneMotion.Gestures.Tests
{
    public class GesturePathTests
    {
        // 0.1 mm.
        private const float Tolerance = 1e-4f;

        private const float Side = 0.2f;

        // Path lengths of the staircase's stored points: spaced unevenly, with every corner included.
        private static readonly float[] StoredLengths =
        {
            0f, 0.013f, 0.02f, 0.14f, 0.2f,
            0.21f, 0.37f, 0.4f,
            0.401f, 0.5f, 0.6f,
            0.63f, 0.71f, 0.77f, 0.8f,
        };

        // A staircase of four sides, along +x, +y, +z, and then back along -x, so that the path
        // turns about every axis. Returns its point at path length s, from that definition.
        private static Vector3 StaircaseAt(float s)
        {
            if (s <= Side)
                return new Vector3(s, 0f, 0f);
            if (s <= 2f * Side)
                return new Vector3(Side, s - Side, 0f);
            if (s <= 3f * Side)
                return new Vector3(Side, Side, s - 2f * Side);
            return new Vector3(4f * Side - s, Side, Side);
        }

        private static GesturePath Staircase()
        {
            var path = new GesturePath();
            for (int i = 0; i < StoredLengths.Length; i++)
                path.Add(StaircaseAt(StoredLengths[i]), 0.01f * i);
            return path;
        }

        [Test]
        public void Resample_WholePath_SpacesPointsEvenlyAndKeepsBothEnds()
        {
            GesturePath path = Staircase();
            var points = new Vector3[17];
            path.Resample(0f, path.Length, points);

            VectorAssert.AreNear(path.PositionAt(0), points[0], 1e-6f, "First point");
            VectorAssert.AreNear(path.PositionAt(path.Count - 1), points[16], 1e-6f, "Last point");
            for (int k = 0; k < points.Length; k++)
                VectorAssert.AreNear(StaircaseAt(4f * Side * k / 16), points[k], Tolerance, $"Point {k}");
            // Every fourth point is a corner, so neighbors share a side, and the straight-line
            // distance between them is the path length between them.
            for (int k = 1; k < points.Length; k++)
                Assert.That(Vector3.Distance(points[k - 1], points[k]), Is.EqualTo(Side / 4f).Within(Tolerance),
                    $"Spacing before point {k}");
        }

        // Each range starts and ends between stored points; the second lies within one stored segment.
        [TestCase(0.05f, 0.65f, 13)]
        [TestCase(0.03f, 0.1f, 8)]
        [TestCase(0.25f, 0.79f, 20)]
        public void Resample_RangeStartingAndEndingMidSegment_FollowsThePath(float fromLength, float toLength, int count)
        {
            GesturePath path = Staircase();
            var points = new Vector3[count];
            path.Resample(fromLength, toLength, points);

            for (int k = 0; k < count; k++)
                VectorAssert.AreNear(StaircaseAt(fromLength + (toLength - fromLength) * k / (count - 1)), points[k],
                    Tolerance, $"Point {k}");
        }

        [Test]
        public void Resample_ZeroLengthSegments_AreSkipped()
        {
            // Repeated points at the start, in the middle, and at the end of a 0.2 m line.
            float[] xs = { 0f, 0f, 0.1f, 0.1f, 0.1f, 0.2f, 0.2f };
            var path = new GesturePath();
            for (int i = 0; i < xs.Length; i++)
                path.Add(new Vector3(xs[i], 0f, 0f), 0.01f * i);

            // The middle resampled point lands exactly on the repeated middle point.
            var whole = new Vector3[5];
            path.Resample(0f, path.Length, whole);
            for (int k = 0; k < whole.Length; k++)
                VectorAssert.AreNear(new Vector3(0.05f * k, 0f, 0f), whole[k], Tolerance, $"Whole path, point {k}");

            var fromMiddle = new Vector3[3];
            path.Resample(path.LengthAt(2), path.Length, fromMiddle);
            for (int k = 0; k < fromMiddle.Length; k++)
                VectorAssert.AreNear(new Vector3(0.1f + 0.05f * k, 0f, 0f), fromMiddle[k], Tolerance,
                    $"From the middle, point {k}");
        }

        [TestCase(-0.01f, 0.5f)]
        [TestCase(0.1f, 0.81f)]
        [TestCase(0.5f, 0.4f)]
        [TestCase(float.NaN, 0.5f)]
        public void Resample_RangeOutsideThePath_Throws(float fromLength, float toLength)
        {
            GesturePath path = Staircase();
            Assert.Throws<ArgumentOutOfRangeException>(() => path.Resample(fromLength, toLength, new Vector3[8]));
        }

        [Test]
        public void LengthAtTime_InterpolatesBetweenStoredPointsByTime()
        {
            GesturePath path = Staircase();

            // Stored points 3 and 4 are at 0.03 and 0.04 s, and at 0.14 and 0.2 m.
            Assert.That(path.LengthAtTime(0.0325f), Is.EqualTo(0.155f).Within(1e-6f), "A quarter of the way between them");
            Assert.That(path.LengthAtTime(0.04f), Is.EqualTo(0.2f).Within(1e-6f), "At a stored point");
            Assert.That(path.LengthAtTime(-0.01f), Is.Zero, "Before the first point");
            Assert.That(path.LengthAtTime(1f), Is.EqualTo(path.Length), "After the last point");
        }

        [Test]
        public void Add_PastTheInitialCapacity_KeepsEveryPoint()
        {
            const int count = 1000;
            var path = new GesturePath();
            for (int i = 0; i < count; i++)
                path.Add(new Vector3(0.001f * i, 0f, 0f), 0.01f * i);

            Assert.That(path.Count, Is.EqualTo(count));
            for (int i = 0; i < count; i++)
            {
                Assert.That(path.PositionAt(i), Is.EqualTo(new Vector3(0.001f * i, 0f, 0f)), $"Position {i}");
                Assert.That(path.TimeAt(i), Is.EqualTo(0.01f * i), $"Time {i}");
                Assert.That(path.LengthAt(i), Is.EqualTo(0.001f * i).Within(Tolerance), $"Length at {i}");
            }
        }

        [Test]
        public void Clear_ThenAdd_StartsANewPath()
        {
            GesturePath path = Staircase();
            path.Clear();
            Assert.That(path.Count, Is.Zero);
            Assert.That(path.Length, Is.Zero);

            path.Add(new Vector3(1f, 1f, 1f), 0f);
            path.Add(new Vector3(1f, 1.3f, 1f), 0.02f);
            Assert.That(path.Count, Is.EqualTo(2));
            Assert.That(path.LengthAt(0), Is.Zero);
            Assert.That(path.Length, Is.EqualTo(0.3f).Within(1e-6f));
        }
    }
}
