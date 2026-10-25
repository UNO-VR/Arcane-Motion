using System;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneMotion.Gestures.Tests
{
    public class GestureTemplatesTests
    {
        private const float Tolerance = 1e-5f;

        // Resampled to 17 points, the rectangle's 0.8 m perimeter has 16 loop points 5 cm apart.
        private const int RectanglePointCount = 17;
        private const int RectangleLoopCount = 16;
        private const float RectangleSpacing = 0.05f;

        // Resampled to 9 points, the 0.4 m L has points 5 cm apart.
        private const int EllPointCount = 9;
        private const float EllLength = 0.4f;
        private const float EllSpacing = 0.05f;

        private readonly TestAssets assets = new TestAssets();

        [TearDown]
        public void DestroyAssets()
        {
            assets.DestroyAll();
        }

        // A 0.3 m by 0.1 m rectangle facing the player, from its top left corner, clockwise as the
        // player sees it. Its sides differ in length, so points spaced by corner index rather than
        // by path length would not land where these expect.
        private GestureShape Rectangle(bool bothDirections)
        {
            return assets.Shape("Rectangle", true, bothDirections,
                new Vector3(-0.15f, 0.05f, 0f), new Vector3(0.15f, 0.05f, 0f),
                new Vector3(0.15f, -0.05f, 0f), new Vector3(-0.15f, -0.05f, 0f));
        }

        // The rectangle's point at path length s from its start, for s from 0 to its 0.8 m perimeter.
        private static Vector3 RectangleAt(float s)
        {
            if (s <= 0.3f)
                return new Vector3(-0.15f + s, 0.05f, 0f);
            if (s <= 0.4f)
                return new Vector3(0.15f, 0.05f - (s - 0.3f), 0f);
            if (s <= 0.7f)
                return new Vector3(0.15f - (s - 0.4f), -0.05f, 0f);
            return new Vector3(-0.15f, -0.05f + (s - 0.7f), 0f);
        }

        // An open L: 0.3 m to the right, then 0.1 m up and away from the player.
        private GestureShape Ell(bool bothDirections)
        {
            return assets.Shape("Ell", false, bothDirections,
                Vector3.zero, new Vector3(0.3f, 0f, 0f), new Vector3(0.3f, 0.06f, 0.08f));
        }

        // The L's point at path length s from its start, for s from 0 to its 0.4 m length.
        private static Vector3 EllAt(float s)
        {
            if (s <= 0.3f)
                return new Vector3(s, 0f, 0f);
            return new Vector3(0.3f, 0.6f * (s - 0.3f), 0.8f * (s - 0.3f));
        }

        private GestureTemplates Compile(int pointCount, int startShifts, params GestureShape[] shapes)
        {
            var spells = new SpellGesture[shapes.Length];
            for (int i = 0; i < shapes.Length; i++)
                spells[i] = assets.Spell($"Spell {i}", shapes[i], 0.05f, 0.5f);
            return new GestureTemplates(assets.Library("Test Library", pointCount, startShifts, spells));
        }

        [Test]
        public void OpenShape_IsOneVariant_TheResampledNormalizedPolyline()
        {
            GestureShape ell = Ell(false);
            // Shifts apply only to closed shapes.
            GestureTemplates templates = Compile(EllPointCount, 3, ell);

            Assert.That(templates.Shapes, Is.EqualTo(new[] { ell }));
            Assert.That(templates.VariantStarts, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(templates.Points, Has.Length.EqualTo(EllPointCount));
            AssertVariant(templates, 0, Expected(EllPointCount, i => EllAt(EllSpacing * i)), "Forward");
        }

        [Test]
        public void OpenShape_BothDirections_ReverseStartsFromTheOtherEnd()
        {
            GestureTemplates templates = Compile(EllPointCount, 3, Ell(true));

            Assert.That(templates.VariantStarts, Is.EqualTo(new[] { 0, 2 }));
            AssertVariant(templates, 0, Expected(EllPointCount, i => EllAt(EllSpacing * i)), "Forward");
            AssertVariant(templates, 1, Expected(EllPointCount, i => EllAt(EllLength - EllSpacing * i)), "Reverse");
        }

        [Test]
        public void ClosedShape_Has2KPlus1Variants_EachShiftedByWholeLoopPoints()
        {
            GestureTemplates templates = Compile(RectanglePointCount, 3, Rectangle(false));

            int[] shifts = { 0, 1, -1, 2, -2, 3, -3 };
            Assert.That(templates.VariantStarts, Is.EqualTo(new[] { 0, shifts.Length }));
            for (int v = 0; v < shifts.Length; v++)
            {
                int shift = shifts[v];
                // Loop point j is j / 16 of the perimeter from the set start, and shift k starts k
                // loop points later. Point 16 is the variant's own start again.
                AssertVariant(templates, v,
                    Expected(RectanglePointCount, i => RectangleAt(RectangleSpacing * Mod(shift + i, RectangleLoopCount))),
                    $"Shift {shift}");
                Assert.That(PointOf(templates, v, RectanglePointCount - 1), Is.EqualTo(PointOf(templates, v, 0)),
                    $"Shift {shift}, last point");
            }
        }

        [Test]
        public void ClosedShape_BothDirections_ReverseKeepsTheSetStart()
        {
            GestureTemplates templates = Compile(RectanglePointCount, 1, Rectangle(true));

            (bool reverse, int shift)[] variants = { (false, 0), (true, 0), (false, 1), (false, -1), (true, 1), (true, -1) };
            Assert.That(templates.VariantStarts, Is.EqualTo(new[] { 0, variants.Length }));
            for (int v = 0; v < variants.Length; v++)
            {
                (bool reverse, int shift) = variants[v];
                // The reverse runs p0, p3, p2, p1, p0, so its loop point j is the forward loop point -j.
                int direction = reverse ? -1 : 1;
                AssertVariant(templates, v,
                    Expected(RectanglePointCount,
                        i => RectangleAt(RectangleSpacing * Mod(direction * (shift + i), RectangleLoopCount))),
                    $"{(reverse ? "Reverse" : "Forward")} shift {shift}");
            }
        }

        [Test]
        public void SpellsSharingAShape_CompileItOnce()
        {
            GestureShape rectangle = Rectangle(false);
            GestureShape ell = Ell(false);
            GestureLibrary library = assets.Library("Test Library", RectanglePointCount, 1,
                assets.Spell("Small Rectangle", rectangle, 0.05f, 0.1f),
                assets.Spell("Ell Spell", ell, 0.05f, 0.5f),
                assets.Spell("Large Rectangle", rectangle, 0.2f, 0.4f));

            var templates = new GestureTemplates(library);

            Assert.That(templates.Shapes, Is.EqualTo(new[] { rectangle, ell }));
            // Three shifts of the rectangle, then the L.
            Assert.That(templates.VariantStarts, Is.EqualTo(new[] { 0, 3, 4 }));
            Assert.That(templates.Points, Has.Length.EqualTo(4 * RectanglePointCount));
            AssertVariant(templates, 3,
                Expected(RectanglePointCount, i => EllAt(EllLength * i / (RectanglePointCount - 1))), "The L");
        }

        [Test]
        public void InvalidLibrary_ThrowsWithEveryProblem()
        {
            GestureLibrary library = assets.Library("Test Library", RectanglePointCount, 1,
                assets.Spell("Shapeless", null, 0.05f, 0.5f));
            library.KeepMatchScore = library.MatchScore - 0.01f;

            var exception = Assert.Throws<InvalidOperationException>(() => new GestureTemplates(library));
            Assert.That(exception.Message, Does.Contain("'Shapeless'"));
            Assert.That(exception.Message, Does.Contain("keep-match score"));
        }

        // The given points, normalized as a template is.
        private static Vector3[] Expected(int pointCount, Func<int, Vector3> pointAt)
        {
            var points = new Vector3[pointCount];
            for (int i = 0; i < pointCount; i++)
                points[i] = pointAt(i);
            Assert.That(GestureNormalization.Normalize(points, Quaternion.identity, out _), Is.True);
            return points;
        }

        private static Vector3 PointOf(GestureTemplates templates, int variant, int index)
        {
            return templates.Points[variant * templates.PointCount + index];
        }

        private static void AssertVariant(GestureTemplates templates, int variant, Vector3[] expected, string message)
        {
            Assert.That(templates.PointCount, Is.EqualTo(expected.Length), "Points per variant");
            for (int i = 0; i < expected.Length; i++)
                VectorAssert.AreNear(expected[i], PointOf(templates, variant, i), Tolerance, $"{message}, point {i}");
        }

        private static int Mod(int value, int divisor)
        {
            return (value % divisor + divisor) % divisor;
        }
    }
}
