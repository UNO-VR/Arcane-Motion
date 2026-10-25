using System.Collections.Generic;
using ArcaneMotion.Gestures.Editor;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneMotion.Gestures.Tests
{
    public class GestureRecognizerTests
    {
        // Gestures are drawn here, away from the tracking origin, in a gesture frame with no rotation.
        private static readonly Vector3 Center = new Vector3(0.4f, 1.2f, 0.5f);

        private readonly TestAssets assets = new TestAssets();
        private GestureShape circle;
        private GestureShape diamond;

        // The checks' templates, as in GestureMatcherTests: from the top and clockwise.
        [SetUp]
        public void CreateShapes()
        {
            circle = assets.Shape("Circle", true, false,
                GestureShapeGenerator.Generate(ShapePlane.Facing, 0, false, 1f, 90f, ShapeDirection.Clockwise));
            diamond = assets.Shape("Diamond", true, false, SyntheticGestures.Corners(4));
        }

        [TearDown]
        public void DestroyAssets()
        {
            assets.DestroyAll();
        }

        // The circle's spells leave sizes below 0.05 m, from 0.15 to 0.2 m, and from 0.3 m without a
        // spell. The diamond's spell takes every size, so a recognizer that chose by size before shape
        // would cast it.
        [TestCase(0.03f, null)]
        [TestCase(0.075f, "Small Circle")]
        [TestCase(0.125f, "Medium Circle")]
        [TestCase(0.175f, null)]
        [TestCase(0.25f, "Large Circle")]
        [TestCase(0.4f, null)]
        public void Circle_ChoosesOnlyAmongTheCircleSpellsBySize(float radius, string expectedSpell)
        {
            GestureLibrary library = assets.Library("Test Library", 64, 3,
                assets.Spell("Small Circle", circle, 0.05f, 0.1f),
                assets.Spell("Medium Circle", circle, 0.1f, 0.15f),
                assets.Spell("Large Circle", circle, 0.2f, 0.3f),
                assets.Spell("Any Diamond", diamond, 0.001f, 10f));
            var matcher = new GestureMatcher(library);
            var recognizer = new GestureRecognizer(library, matcher.Templates);

            RecognitionState state = Recognize(matcher, recognizer, CirclePath(radius));

            Assert.That(state.Shape, Is.SameAs(circle));
            Assert.That(state.Size, Is.EqualTo(radius).Within(0.01f * radius), "A circle's size is its radius");
            if (expectedSpell == null)
            {
                Assert.That(state.Status, Is.EqualTo(RecognitionStatus.WrongSize));
                Assert.That(state.Spell, Is.Null);
            }
            else
            {
                Assert.That(state.Status, Is.EqualTo(RecognitionStatus.Spell));
                Assert.That(state.Spell.name, Is.EqualTo(expectedSpell));
            }
        }

        [Test]
        public void SizeWhereOneRangeEndsAndTheNextStarts_GivesTheSpellThatStartsThere()
        {
            SpellGesture small = assets.Spell("Small Circle", circle, 0.05f, 0.1f);
            SpellGesture large = assets.Spell("Large Circle", circle, 0.1f, 0.3f);
            GestureLibrary library = assets.Library("Test Library", 64, 3, small, large);
            var matcher = new GestureMatcher(library);
            GesturePath path = CirclePath(0.1f);
            matcher.Match(path, 0f, path.Length, Quaternion.identity);

            // The ranges meet at exactly the size the matcher measured. The recognizer copies them when
            // it is created.
            float size = matcher.Result.Size(0);
            small.MaxSize = size;
            large.MinSize = size;
            var recognizer = new GestureRecognizer(library, matcher.Templates);
            RecognitionState state = recognizer.Recognize(matcher.Result, 0f, path.Length);

            Assert.That(state.Size, Is.EqualTo(size));
            Assert.That(state.Status, Is.EqualTo(RecognitionStatus.Spell));
            Assert.That(state.Spell, Is.SameAs(large));
        }

        // With no cuts and no start shifts, as in the synthetic check, a clean diamond scores 0 against
        // the diamond and a clean circle 0.116. A line scores far above both.
        [TestCase(0.15f, RecognitionStatus.Spell)]
        [TestCase(0.1f, RecognitionStatus.None)]
        public void LitShape_StaysLitWhileBelowTheKeepMatchScore(float keepMatchScore, RecognitionStatus circleWhileLit)
        {
            GestureLibrary library = CheckLibrary(0f, assets.Spell("Diamond Spell", diamond, 0.001f, 10f));
            library.MatchScore = 0.1f;
            library.KeepMatchScore = keepMatchScore;
            var matcher = new GestureMatcher(library);
            var recognizer = new GestureRecognizer(library, matcher.Templates);
            GesturePath cleanDiamond = DiamondPath();
            GesturePath cleanCircle = CirclePath(0.15f);
            GesturePath line = PathOf(new[] { Center + new Vector3(-0.15f, 0f, 0f), Center + new Vector3(0.15f, 0f, 0f) });

            RecognitionState state = Recognize(matcher, recognizer, cleanDiamond);
            Assert.That(state.Status, Is.EqualTo(RecognitionStatus.Spell), "The diamond lights below the match score");
            state = Recognize(matcher, recognizer, cleanCircle);
            Assert.That(state.Status, Is.EqualTo(circleWhileLit), "The circle, while the diamond is lit");
            if (circleWhileLit == RecognitionStatus.Spell)
                Assert.That(state.Score, Is.EqualTo(0.116f).Within(0.005f), "The circle's score");

            // Once the diamond is not lit, the circle must be below the match score to light it.
            state = Recognize(matcher, recognizer, line);
            Assert.That(state.Status, Is.EqualTo(RecognitionStatus.None), "The line");
            state = Recognize(matcher, recognizer, cleanCircle);
            Assert.That(state.Status, Is.EqualTo(RecognitionStatus.None), "The circle, after the line");

            // Reset forgets the lit shape too.
            Recognize(matcher, recognizer, cleanDiamond);
            recognizer.Reset();
            state = Recognize(matcher, recognizer, cleanCircle);
            Assert.That(state.Status, Is.EqualTo(RecognitionStatus.None), "The circle, after a reset");
        }

        // A clean circle scores about 0 against the circle and 0.116 against the diamond.
        [TestCase(0.1f, RecognitionStatus.Spell)]
        [TestCase(0.13f, RecognitionStatus.None)]
        public void BestShape_LightsOnlyWithTheMarginOverTheRunnerUp(float minMargin, RecognitionStatus expected)
        {
            GestureLibrary library = CheckLibrary(0f,
                assets.Spell("Circle Spell", circle, 0.001f, 10f), assets.Spell("Diamond Spell", diamond, 0.001f, 10f));
            library.MinMargin = minMargin;
            var matcher = new GestureMatcher(library);
            var recognizer = new GestureRecognizer(library, matcher.Templates);

            RecognitionState state = Recognize(matcher, recognizer, CirclePath(0.15f));

            Assert.That(state.Status, Is.EqualTo(expected));
            // A state that is not lit still carries the best shape and its score.
            Assert.That(state.Shape, Is.SameAs(circle));
            Assert.That(state.Score, Is.LessThan(0.01f));
        }

        [Test]
        public void MatchedPart_LeavesOutTheWindUpThatTheStartCutRemoved()
        {
            GestureLibrary library = CheckLibrary(0.4f, assets.Spell("Circle Spell", circle, 0.001f, 10f));
            var matcher = new GestureMatcher(library);
            var recognizer = new GestureRecognizer(library, matcher.Templates);
            // The matcher test's wind-up: 50 points in a straight line from (1.5, -1, 0) to the circle's
            // start, 28% of the path, at a radius of 0.15 m.
            var points = new List<Vector3>();
            for (int t = 0; t < 50; t++)
                points.Add(Center + 0.15f * new Vector3(1.5f - 1.5f * t / 50, -1f + 2f * t / 50, 0f));
            points.AddRange(SyntheticGestures.Sample(u => Center + 0.15f * SyntheticGestures.CirclePoint(u), 0, 1, 401));
            GesturePath path = PathOf(points);
            float windUpLength = path.LengthAt(50);
            // An evaluated part that does not start at the path's start, as after the start trim.
            float evaluatedStart = 0.05f * path.Length;

            matcher.Match(path, evaluatedStart, path.Length, Quaternion.identity);
            RecognitionState state = recognizer.Recognize(matcher.Result, evaluatedStart, path.Length);

            Assert.That(state.Status, Is.EqualTo(RecognitionStatus.Spell));
            Assert.That(state.EvaluatedStart, Is.EqualTo(evaluatedStart));
            Assert.That(state.EvaluatedEnd, Is.EqualTo(path.Length));
            // Within one cut step of the evaluated part.
            Assert.That(state.MatchedStart, Is.EqualTo(windUpLength).Within(0.025f * (path.Length - evaluatedStart)));
            Assert.That(state.MatchedEnd, Is.EqualTo(path.Length));
        }

        // A library of these spells with no start shifts, as in the synthetic checks, and the plan's
        // other settings.
        private GestureLibrary CheckLibrary(float maxTotalCut, params SpellGesture[] spells)
        {
            GestureLibrary library = assets.Library("Test Library", 64, 0, spells);
            library.MaxTotalCut = maxTotalCut;
            return library;
        }

        // Matches the whole path and recognizes the result.
        private static RecognitionState Recognize(GestureMatcher matcher, GestureRecognizer recognizer, GesturePath path)
        {
            matcher.Match(path, 0f, path.Length, Quaternion.identity);
            return recognizer.Recognize(matcher.Result, 0f, path.Length);
        }

        // A clean circle from its set start, once around.
        private static GesturePath CirclePath(float radius)
        {
            return PathOf(SyntheticGestures.Sample(u => Center + radius * SyntheticGestures.CirclePoint(u), 0, 1, 600));
        }

        // A clean diamond, 0.3 m from corner to corner, from its set start, once around.
        private static GesturePath DiamondPath()
        {
            var points = new List<Vector3>();
            foreach (Vector3 corner in SyntheticGestures.Corners(4))
                points.Add(Center + 0.15f * corner);
            points.Add(points[0]);
            return PathOf(points);
        }

        private static GesturePath PathOf(IEnumerable<Vector3> points)
        {
            var path = new GesturePath();
            foreach (Vector3 point in points)
                path.Add(point, 0f);
            return path;
        }
    }
}
