using System;
using ArcaneMotion.Gestures.Editor;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneMotion.Gestures.Tests
{
    public class GestureShapeGeneratorTests
    {
        private const float Tolerance = 1e-6f;

        [Test]
        public void Circle_StartsAtTheStartAngleAndTurnsClockwise()
        {
            // The recorded Circle: from the bottom, clockwise as the player sees it.
            Vector3[] points = GestureShapeGenerator.Generate(ShapePlane.Facing, 0, false, 0.2f, -90f,
                ShapeDirection.Clockwise);

            Assert.That(points, Has.Length.EqualTo(GestureShapeGenerator.CirclePointCount));
            for (int i = 0; i < points.Length; i++)
            {
                Assert.That(points[i].magnitude, Is.EqualTo(0.2f).Within(Tolerance), $"Radius of point {i}");
                Assert.That(points[i].z, Is.EqualTo(0f).Within(Tolerance), $"Depth of point {i}");
            }
            // Clockwise from the bottom passes the left side, then the top.
            int quarter = points.Length / 4;
            VectorAssert.AreNear(new Vector3(0f, -0.2f, 0f), points[0], Tolerance, "Start");
            VectorAssert.AreNear(new Vector3(-0.2f, 0f, 0f), points[quarter], Tolerance, "A quarter turn on");
            VectorAssert.AreNear(new Vector3(0f, 0.2f, 0f), points[2 * quarter], Tolerance, "A half turn on");
        }

        [Test]
        public void Triangle_StartsAtTheTopAndTurnsClockwise()
        {
            // The recorded Triangle: point up, from the top, clockwise.
            Vector3[] points = GestureShapeGenerator.Generate(ShapePlane.Facing, 3, false, 0.2f, 90f,
                ShapeDirection.Clockwise);

            float halfBase = 0.2f * Mathf.Sqrt(3f) / 2f;
            Assert.That(points, Has.Length.EqualTo(3));
            VectorAssert.AreNear(new Vector3(0f, 0.2f, 0f), points[0], Tolerance, "Top");
            VectorAssert.AreNear(new Vector3(halfBase, -0.1f, 0f), points[1], Tolerance, "Bottom right");
            VectorAssert.AreNear(new Vector3(-halfBase, -0.1f, 0f), points[2], Tolerance, "Bottom left");
        }

        [Test]
        public void Star_JoinsEverySecondCornerInTheRecordedOrder()
        {
            Vector3[] points = GestureShapeGenerator.Generate(ShapePlane.Facing, 5, true, 0.2f, -126f,
                ShapeDirection.Counterclockwise);

            // The recorded Star: from the bottom-left point to the right point, the left point, the
            // bottom-right point, and the top.
            Assert.That(points, Has.Length.EqualTo(5));
            VectorAssert.AreNear(PentagonCorner(234f), points[0], Tolerance, "Bottom left");
            VectorAssert.AreNear(PentagonCorner(18f), points[1], Tolerance, "Right");
            VectorAssert.AreNear(PentagonCorner(162f), points[2], Tolerance, "Left");
            VectorAssert.AreNear(PentagonCorner(306f), points[3], Tolerance, "Bottom right");
            VectorAssert.AreNear(PentagonCorner(90f), points[4], Tolerance, "Top");
        }

        // A square from angle 0 counterclockwise, so that its first two corners lie on the plane's
        // right and up axes.
        [TestCase(ShapePlane.Facing, 1f, 0f, 0f, 0f, 1f, 0f)]
        [TestCase(ShapePlane.Horizontal, 1f, 0f, 0f, 0f, 0f, 1f)]
        [TestCase(ShapePlane.Side, 0f, 0f, 1f, 0f, 1f, 0f)]
        public void Plane_PutsItsRightAndUpInTheGestureFrame(ShapePlane plane, float rightX, float rightY, float rightZ,
            float upX, float upY, float upZ)
        {
            Vector3[] points = GestureShapeGenerator.Generate(plane, 4, false, 1f, 0f, ShapeDirection.Counterclockwise);

            VectorAssert.AreNear(new Vector3(rightX, rightY, rightZ), points[0], Tolerance, "Right");
            VectorAssert.AreNear(new Vector3(upX, upY, upZ), points[1], Tolerance, "Up");
        }

        [TestCase(1, false, 0.1f)]
        [TestCase(2, false, 0.1f)]
        [TestCase(-3, false, 0.1f)]
        [TestCase(0, true, 0.1f)]
        [TestCase(3, true, 0.1f)]
        [TestCase(6, true, 0.1f)]
        [TestCase(5, false, 0f)]
        [TestCase(5, false, float.NaN)]
        public void InvalidSettings_Throw(int sides, bool star, float radius)
        {
            Assert.That(GestureShapeGenerator.FindProblem(sides, star, radius), Is.Not.Null);
            Assert.Throws<ArgumentException>(() =>
                GestureShapeGenerator.Generate(ShapePlane.Facing, sides, star, radius, 90f, ShapeDirection.Clockwise));
        }

        // A corner of a regular pentagon 0.2 m in radius, facing the player, at this angle in degrees
        // counterclockwise from the right.
        private static Vector3 PentagonCorner(float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad;
            return new Vector3(0.2f * Mathf.Cos(angle), 0.2f * Mathf.Sin(angle), 0f);
        }
    }
}
