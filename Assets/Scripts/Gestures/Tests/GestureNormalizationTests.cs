using System;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneMotion.Gestures.Tests
{
    public class GestureNormalizationTests
    {
        private const float Tolerance = 1e-5f;
        private const float DirectionTolerance = 1e-4f;

        [Test]
        public void Normalize_CentersScalesAndTurnsThePoints()
        {
            // Points scattered over about 15 cm, away from the tracking origin the way a real gesture is.
            var random = new System.Random(1234);
            var original = new Vector3[40];
            for (int i = 0; i < original.Length; i++)
            {
                var offset = new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                original[i] = new Vector3(0.4f, 1.3f, -0.7f) + 0.15f * (offset - 0.5f * Vector3.one);
            }
            (_, _, _, double expectedSize) = CentroidAndRms(original);
            Quaternion rotation = Quaternion.Euler(0f, 70f, 0f);

            var points = (Vector3[])original.Clone();
            Assert.That(GestureNormalization.Normalize(points, rotation, out float size), Is.True);

            Assert.That(size, Is.EqualTo(expectedSize).Within(1e-6), "Size in meters");
            (double x, double y, double z, double rms) = CentroidAndRms(points);
            Assert.That(x, Is.EqualTo(0).Within(Tolerance), "Centroid x");
            Assert.That(y, Is.EqualTo(0).Within(Tolerance), "Centroid y");
            Assert.That(z, Is.EqualTo(0).Within(Tolerance), "Centroid z");
            Assert.That(rms, Is.EqualTo(1).Within(Tolerance), "RMS distance from the centroid");
            // Turned as well as moved and scaled: each point's offset from the first point is the
            // original offset, turned by the rotation and divided by the size.
            for (int i = 1; i < points.Length; i++)
                VectorAssert.AreNear(rotation * (original[i] - original[0]) / (float)expectedSize, points[i] - points[0],
                    Tolerance, $"Offset of point {i}");
        }

        // Positions across a room-scale tracking space. A float sum for the centroid gave the last
        // two a size above 1e-6 m.
        [TestCase(0.3f, 1.2f, 0.5f)]
        [TestCase(0.3f, 1.8f, 0.5f)]
        [TestCase(-1.91f, 2.41f, -1.9f)]
        public void Normalize_DegeneratePath_ReturnsFalseAndLeavesThePointsUnchanged(float x, float y, float z)
        {
            // The controller held still: every stored point is the same.
            var path = new GesturePath();
            for (int i = 0; i < 5; i++)
                path.Add(new Vector3(x, y, z), 0.01f * i);
            var points = new Vector3[64];
            path.Resample(0f, path.Length, points);
            var before = (Vector3[])points.Clone();

            Assert.That(GestureNormalization.Normalize(points, Quaternion.identity, out _), Is.False);
            Assert.That(points, Is.EqualTo(before));
        }

        // Euler angles of the head: pitch (positive looks down), yaw, and roll. The last case is
        // just outside the range where the up vector takes over.
        [TestCase(0f, 0f, 0f)]
        [TestCase(0f, 90f, 0f)]
        [TestCase(0f, 180f, 0f)]
        [TestCase(0f, -90f, 0f)]
        [TestCase(20f, -135f, 10f)]
        [TestCase(-60f, 200f, -25f)]
        [TestCase(80f, 45f, 30f)]
        [TestCase(-89f, 300f, 0f)]
        public void GestureFrameRotation_TurnsTheHeadsFacingToPlusZ(float pitch, float yaw, float roll)
        {
            Quaternion rotation = GestureNormalization.GestureFrameRotation(Quaternion.Euler(pitch, yaw, roll));

            // Pitch and roll do not change the direction the face is turned; the yaw alone sets it.
            VectorAssert.AreNear(Vector3.forward, rotation * Facing(yaw), DirectionTolerance, "Facing direction");
            VectorAssert.AreNear(Vector3.up, rotation * Vector3.up, DirectionTolerance, "Up");
        }

        [TestCase(90f, 30f)]
        [TestCase(90f, -120f)]
        [TestCase(-90f, 30f)]
        [TestCase(-90f, -120f)]
        public void GestureFrameRotation_LookingStraightDownOrUp_UsesTheUpVector(float pitch, float yaw)
        {
            Quaternion rotation = GestureNormalization.GestureFrameRotation(Quaternion.Euler(pitch, yaw, 0f));

            VectorAssert.AreNear(Vector3.forward, rotation * Facing(yaw), DirectionTolerance, "Facing direction");
            VectorAssert.AreNear(Vector3.up, rotation * Vector3.up, DirectionTolerance, "Up");
        }

        // The horizontal direction that a head with this yaw faces.
        private static Vector3 Facing(float yawDegrees)
        {
            float yaw = yawDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        }

        // Accumulated in double precision, as a reference independent of the code under test.
        private static (double x, double y, double z, double rms) CentroidAndRms(Vector3[] points)
        {
            double x = 0, y = 0, z = 0;
            foreach (Vector3 p in points)
            {
                x += p.x;
                y += p.y;
                z += p.z;
            }
            x /= points.Length;
            y /= points.Length;
            z /= points.Length;

            double sumOfSquares = 0;
            foreach (Vector3 p in points)
                sumOfSquares += (p.x - x) * (p.x - x) + (p.y - y) * (p.y - y) + (p.z - z) * (p.z - z);
            return (x, y, z, Math.Sqrt(sumOfSquares / points.Length));
        }
    }
}
