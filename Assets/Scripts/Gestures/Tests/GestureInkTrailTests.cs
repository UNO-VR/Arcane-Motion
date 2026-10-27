using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace ArcaneMotion.Gestures.Tests
{
    public class GestureInkTrailTests
    {
        private static readonly Color Matched = new Color(1f, 0f, 0f);
        private static readonly Color Dimmed = new Color(0.5f, 0.5f, 0.5f, 0.4f);
        private static readonly Color Neutral = new Color(0f, 1f, 1f);

        // Points every 0.25 m along x, so that every path length in these tests is exact in floats.
        private static GesturePath StraightPath()
        {
            var path = new GesturePath();
            for (int i = 0; i <= 4; i++)
                path.Add(new Vector3(0.25f * i, 0f, 0f), i / 72f);
            return path;
        }

        private static RecognitionState Lit(float matchedStart, float matchedEnd, float evaluatedEnd)
        {
            return new RecognitionState(RecognitionStatus.WrongSize, null, null, 0.1f, 0.2f, 0.05f, evaluatedEnd,
                matchedStart, matchedEnd);
        }

        // Writes the ink and checks its vertices against the expected x positions, then checks the
        // colors that LineRenderer gives the vertices at the given indices. In the Stretch texture
        // mode, a vertex takes the gradient at its fraction of the line's length.
        private static void AssertInk(GesturePath path, Vector3? lastSegmentEnd, RecognitionState state, float opacity,
            float[] expectedX, params (int vertex, Color color)[] expectedColors)
        {
            var positions = new Vector3[1];
            var gradient = new Gradient();
            int count = GestureInkTrail.WriteInk(path, lastSegmentEnd, state, Matched, Dimmed, Neutral, opacity,
                ref positions, gradient);

            Assert.That(count, Is.EqualTo(expectedX.Length), "Vertex count");
            for (int i = 0; i < count; i++)
                Assert.That(positions[i], Is.EqualTo(new Vector3(expectedX[i], 0f, 0f)).Using(Vector3EqualityComparer.Instance),
                    $"Vertex {i}");

            var distances = new float[count];
            for (int i = 1; i < count; i++)
                distances[i] = distances[i - 1] + Vector3.Distance(positions[i - 1], positions[i]);
            foreach ((int vertex, Color color) in expectedColors)
            {
                Color expected = new Color(color.r, color.g, color.b, color.a * opacity);
                Assert.That(gradient.Evaluate(distances[vertex] / distances[count - 1]),
                    Is.EqualTo(expected).Using(ColorEqualityComparer.Instance), $"Color at vertex {vertex}");
            }
        }

        // The boundaries at 0.125 and 0.875 are inside segments, so each gets a vertex; the one at 0.5
        // is on a stored point. Vertices on boundaries are not checked: the segment on one side of each
        // blends the two colors.
        [Test]
        public void LitShapeColorsMatchedPartDimsCutsAndLeavesTheRestNeutral()
        {
            AssertInk(StraightPath(), new Vector3(1.5f, 0f, 0f), Lit(0.125f, 0.5f, 0.875f), 1f,
                new[] { 0f, 0.125f, 0.25f, 0.5f, 0.75f, 0.875f, 1f, 1.5f },
                (0, Dimmed), (2, Matched), (4, Dimmed), (6, Neutral), (7, Neutral));
        }

        // With no end cut, the matched part ends where the evaluated part does, and that boundary gets
        // one vertex, not two at the same place.
        [Test]
        public void MatchedEndAtEvaluatedEndAddsOneVertex()
        {
            AssertInk(StraightPath(), null, Lit(0.125f, 0.625f, 0.625f), 1f,
                new[] { 0f, 0.125f, 0.25f, 0.5f, 0.625f, 0.75f, 1f },
                (0, Dimmed), (2, Matched), (3, Matched), (5, Neutral), (6, Neutral));
        }

        [Test]
        public void UnlitInkIsNeutralAtTheGivenOpacity()
        {
            RecognitionState unlit = new RecognitionState(RecognitionStatus.None, null, null, 0.4f, 0.2f, 0.05f,
                0.875f, 0.125f, 0.5f);
            AssertInk(StraightPath(), new Vector3(1.5f, 0f, 0f), unlit, 0.5f,
                new[] { 0f, 0.25f, 0.5f, 0.75f, 1f, 1.5f },
                (0, Neutral), (2, Neutral), (4, Neutral), (5, Neutral));
        }
    }
}
