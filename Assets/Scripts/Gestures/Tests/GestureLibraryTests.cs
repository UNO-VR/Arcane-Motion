using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneMotion.Gestures.Tests
{
    public class GestureLibraryTests
    {
        private readonly TestAssets assets = new TestAssets();
        private GestureShape triangle;
        private GestureShape line;
        private SpellGesture smallTriangle;
        private SpellGesture largeTriangle;
        private GestureLibrary library;

        // A library at the edges of what is valid: a closed shape with the fewest points, 3, and an
        // open shape with the fewest, 2; the most start shifts that 9 resampled points allow; and a
        // keep-match score equal to the match score. The line spell's size range overlaps both
        // triangle spells' ranges, which is allowed because it is another shape.
        [SetUp]
        public void CreateValidLibrary()
        {
            triangle = assets.Shape("Triangle", true, false,
                new Vector3(0f, 0.1f, 0f), new Vector3(0.087f, -0.05f, 0f), new Vector3(-0.087f, -0.05f, 0f));
            line = assets.Shape("Line", false, false, Vector3.zero, new Vector3(0.3f, 0f, 0f));
            smallTriangle = assets.Spell("Small Triangle", triangle, 0.05f, 0.1f);
            largeTriangle = assets.Spell("Large Triangle", triangle, 0.15f, 0.3f);
            SpellGesture lineSpell = assets.Spell("Line Spell", line, 0.05f, 0.5f);
            library = assets.Library("Test Library", 9, 3, smallTriangle, largeTriangle, lineSpell);
            library.MatchScore = 0.25f;
            library.KeepMatchScore = 0.25f;
        }

        [TearDown]
        public void DestroyAssets()
        {
            assets.DestroyAll();
        }

        [Test]
        public void Validate_ValidLibrary_HasNoProblems()
        {
            Assert.That(library.Validate(), Is.Empty);
        }

        [Test]
        public void Validate_EmptySpellElement_IsAProblem()
        {
            library.Spells.Insert(1, null);
            AssertOneProblem("'Test Library'", "element 1");
        }

        [Test]
        public void Validate_SpellWithoutAShape_IsAProblem()
        {
            smallTriangle.Shape = null;
            AssertOneProblem("'Small Triangle'");
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        public void Validate_MinSizeNotAboveZero_IsAProblem(float minSize)
        {
            smallTriangle.MinSize = minSize;
            AssertOneProblem("'Small Triangle'");
        }

        [TestCase(0.05f)]
        [TestCase(0.04f)]
        public void Validate_MaxSizeNotAboveMinSize_IsAProblem(float maxSize)
        {
            smallTriangle.MaxSize = maxSize;
            AssertOneProblem("'Small Triangle'");
        }

        // The small triangle's range is 0.05 to 0.1 m: touching it at 0.1, overlapping it, and
        // containing it.
        [TestCase(0.1f)]
        [TestCase(0.08f)]
        [TestCase(0.01f)]
        public void Validate_OverlappingSizeRangesOnOneShape_AreAProblem(float largeMinSize)
        {
            largeTriangle.MinSize = largeMinSize;
            AssertOneProblem("'Small Triangle'", "'Large Triangle'", "'Triangle'");
        }

        // Two spells use the triangle, and its problem is reported once.
        [TestCase(true, 2)]
        [TestCase(false, 1)]
        [TestCase(false, 0)]
        public void Validate_ShapeWithTooFewPoints_IsAProblem(bool closed, int count)
        {
            GestureShape shape = closed ? triangle : line;
            var points = new Vector3[count];
            Array.Copy(shape.Points, points, count);
            shape.Points = points;
            AssertOneProblem($"'{shape.name}'");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Validate_ShapeWithNoLength_IsAProblem(bool closed)
        {
            GestureShape shape = closed ? triangle : line;
            var point = new Vector3(0.1f, 1.2f, 0.3f);
            shape.Points = closed ? new[] { point, point, point } : new[] { point, point };
            AssertOneProblem($"'{shape.name}'");
        }

        // A closed shape has N - 1 loop points, and 2K + 1 shifts fit in them only while K is below
        // (N - 1) / 2.
        [TestCase(9, 3, false)]
        [TestCase(9, 4, true)]
        [TestCase(8, 3, false)]
        [TestCase(8, 4, true)]
        public void Validate_StartShifts_MustBeBelowHalfTheLoop(int pointCount, int startShifts, bool isProblem)
        {
            library.PointCount = pointCount;
            library.StartShifts = startShifts;
            if (isProblem)
                AssertOneProblem("'Test Library'");
            else
                Assert.That(library.Validate(), Is.Empty);
        }

        [Test]
        public void Validate_KeepMatchScoreBelowMatchScore_IsAProblem()
        {
            library.KeepMatchScore = 0.24f;
            AssertOneProblem("'Test Library'");
        }

        private void AssertOneProblem(params string[] fragments)
        {
            List<string> problems = library.Validate();
            Assert.That(problems, Has.Count.EqualTo(1), string.Join("\n", problems));
            foreach (string fragment in fragments)
                Assert.That(problems[0], Does.Contain(fragment));
        }
    }
}
