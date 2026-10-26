using System;
using System.Collections.Generic;
using ArcaneMotion.Gestures.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Random = System.Random;

namespace ArcaneMotion.Gestures.Tests
{
    public class GestureMatcherTests
    {
        // How closely the port must reproduce the synthetic checks' scores.
        private const float CheckTolerance = 0.005f;
        private const float ExactTolerance = 1e-5f;

        private readonly TestAssets assets = new TestAssets();
        private GestureShape circle;
        private GestureShape triangle;
        private GestureShape diamond;
        private GestureShape vee;
        private GestureShape lineRight;
        private GestureShape lineLeft;

        // The checks' templates, with a radius of 1. The closed shapes start at the top and go
        // clockwise, and the V is the triangle's first two sides.
        [SetUp]
        public void CreateShapes()
        {
            circle = assets.Shape("Circle", true, false,
                GestureShapeGenerator.Generate(ShapePlane.Facing, 0, false, 1f, 90f, ShapeDirection.Clockwise));
            triangle = assets.Shape("Triangle", true, false, SyntheticGestures.Corners(3));
            diamond = assets.Shape("Diamond", true, false, SyntheticGestures.Corners(4));
            vee = assets.Shape("V", false, false, SyntheticGestures.Corners(3));
            lineRight = assets.Shape("Line Right", false, false, Vector3.left, Vector3.right);
            lineLeft = assets.Shape("Line Left", false, false, Vector3.right, Vector3.left);
        }

        [TearDown]
        public void DestroyAssets()
        {
            assets.DestroyAll();
        }

        [Test]
        public void CleanCircle_AgainstTheDiamond_NoCutOrShift()
        {
            GestureMatcher matcher = CheckMatcher(0, 0f, diamond);

            MatchAll(matcher, SyntheticGestures.Sample(SyntheticGestures.CirclePoint, 0, 1, 600));

            Assert.That(ScoreOf(matcher, diamond), Is.EqualTo(0.116f).Within(CheckTolerance));
        }

        [Test]
        public void CleanCircle_AgainstTheTriangle_NoCut()
        {
            GestureMatcher matcher = CheckMatcher(0, 0f, triangle);

            MatchAll(matcher, SyntheticGestures.Sample(SyntheticGestures.CirclePoint, 0, 1, 600));

            Assert.That(ScoreOf(matcher, triangle), Is.EqualTo(0.252f).Within(CheckTolerance));
        }

        [Test]
        public void CircleTilted20Degrees_AgainstTheCircle_NoCut()
        {
            GestureMatcher matcher = CheckMatcher(0, 0f, circle);
            Quaternion tilt = Quaternion.AngleAxis(20f, Vector3.right);

            MatchAll(matcher, SyntheticGestures.Sample(u => tilt * SyntheticGestures.CirclePoint(u), 0, 1, 401));

            Assert.That(ScoreOf(matcher, circle), Is.EqualTo(0.223f).Within(CheckTolerance));
        }

        // One full loop, from 10 degrees after the set start back to there.
        [TestCase(0, 0f, 0.174f)]
        [TestCase(3, 0.4f, 0.025f)]
        public void CircleStarted10DegreesLate_TrimSearchAndShiftsRecoverIt(int startShifts, float maxTotalCut,
            float expected)
        {
            GestureMatcher matcher = CheckMatcher(startShifts, maxTotalCut, circle);

            MatchAll(matcher, SyntheticGestures.Sample(u => SyntheticGestures.CirclePoint(10.0 / 360 + u), 0, 1, 600));

            Assert.That(ScoreOf(matcher, circle), Is.EqualTo(expected).Within(CheckTolerance));
        }

        [Test]
        public void CircleAfterAWindUp_TheStartCutRemovesIt()
        {
            // The check tried no start shifts.
            GestureMatcher matcher = CheckMatcher(0, 0.4f, circle, triangle);
            // 50 points in a straight line from (1.5, -1, 0) to the circle's start: 28% of the path.
            var points = new List<Vector3>();
            for (int t = 0; t < 50; t++)
                points.Add(new Vector3(1.5f - 1.5f * t / 50, -1f + 2f * t / 50, 0f));
            points.AddRange(SyntheticGestures.Sample(SyntheticGestures.CirclePoint, 0, 1, 401));

            MatchAll(matcher, points);

            int circleIndex = IndexOf(matcher, circle);
            Assert.That(matcher.Result.Score(circleIndex), Is.EqualTo(0.098f).Within(CheckTolerance), "Circle");
            Assert.That(matcher.Result.StartCut(circleIndex), Is.EqualTo(0.275f).Within(1e-6f), "Circle start cut");
            Assert.That(matcher.Result.EndCut(circleIndex), Is.EqualTo(0f), "Circle end cut");
            Assert.That(ScoreOf(matcher, triangle), Is.EqualTo(0.307f).Within(CheckTolerance), "Triangle");
        }

        [Test]
        public void CleanTriangle_TheCapAndPenaltyKeepItAheadOfTheV()
        {
            // The check tried no start shifts.
            GestureMatcher matcher = CheckMatcher(0, 0.4f, triangle, vee);
            var points = new List<Vector3>(SyntheticGestures.Corners(3));
            points.Add(points[0]);

            MatchAll(matcher, points);

            int veeIndex = IndexOf(matcher, vee);
            Assert.That(ScoreOf(matcher, triangle), Is.EqualTo(0f).Within(CheckTolerance), "Triangle");
            Assert.That(matcher.Result.Score(veeIndex), Is.EqualTo(0.094f).Within(CheckTolerance), "V");
            Assert.That(matcher.Result.StartCut(veeIndex), Is.EqualTo(0f), "V start cut");
            Assert.That(matcher.Result.EndCut(veeIndex), Is.EqualTo(0.325f).Within(1e-6f), "V end cut");
        }

        [Test]
        public void CircleThreeQuartersDrawn_IsCloserToTheVThanTheCircle()
        {
            GestureMatcher matcher = CheckMatcher(3, 0.4f, vee, circle);
            // A 30 cm circle away from the tracking origin, drawn from its set start.
            var center = new Vector3(0.4f, 1.2f, 0.5f);

            MatchAll(matcher, SyntheticGestures.Sample(u => center + 0.15f * SyntheticGestures.CirclePoint(u), 0, 0.75, 400));

            Assert.That(ScoreOf(matcher, vee), Is.EqualTo(0.277f).Within(CheckTolerance), "V");
            Assert.That(ScoreOf(matcher, circle), Is.EqualTo(0.442f).Within(CheckTolerance), "Circle");
            Assert.That(matcher.Result.Winner, Is.EqualTo(IndexOf(matcher, vee)), "Winner");
        }

        // The shortcuts must change no decision. Each gesture is matched as the session will match it
        // live, on a path that grows, so that later calls start from the last call's winner and cut.
        [TestCase(0f)]
        [TestCase(0.1f)]
        public void SloppyGestures_AgreeWithTheBruteForceReference(float margin)
        {
            GestureLibrary library = Library(3, 0.4f, circle, triangle, diamond, lineRight, lineLeft, vee);
            // A keep-match score above the match score, because the cutoff must use the larger. 0.4 is
            // the threshold plus margin that the check of the matcher's cost assumed.
            library.KeepMatchScore = 0.4f;
            library.MinMargin = margin;
            var matcher = new GestureMatcher(library);
            var random = new Random(20261025);
            var offset = new Vector3(0.4f, 1.2f, 0.5f);
            int winnersChecked = 0;
            int othersChecked = 0;

            for (int g = 0; g < 50; g++)
            {
                var kind = (SyntheticGestures.Kind)(g / 10);
                Vector3[] drawn = SyntheticGestures.Sloppy(kind, random);
                // About 30 cm across, drawn facing any direction in the room.
                Quaternion rotation =
                    GestureNormalization.GestureFrameRotation(Quaternion.Euler(0f, (float)(360 * random.NextDouble()), 0f));
                var path = new GesturePath();
                foreach (Vector3 point in drawn)
                    path.Add(offset + Quaternion.Inverse(rotation) * (0.15f * point), 0f);

                matcher.Reset();
                foreach (float part in new[] { 0.6f, 0.8f, 1f })
                {
                    float from = 0.01f * path.Length;
                    float to = part * path.Length;
                    matcher.Match(path, from, to, rotation);
                    BruteForceMatcher.ShapeMatch[] reference =
                        BruteForceMatcher.Match(library, matcher.Templates, path, from, to, rotation);

                    (bool winnerChecked, int others) = AssertAgrees(matcher.Result, reference,
                        library.KeepMatchScore, margin, $"{kind} gesture {g}, {part * 100}% drawn");
                    winnersChecked += winnerChecked ? 1 : 0;
                    othersChecked += others;
                }
            }

            // Most calls have a winner under the cutoff, and with a margin many have runners-up under
            // it too, so this test cannot pass by checking nothing.
            Assert.That(winnersChecked, Is.GreaterThanOrEqualTo(75), "Calls with a winner under the cutoff");
            if (margin > 0f)
                Assert.That(othersChecked, Is.GreaterThanOrEqualTo(20), "Other shapes under the cutoff");
        }

        [Test]
        public void Match_AfterAWarmUpGesture_AllocatesNothing()
        {
            var matcher = new GestureMatcher(Library(3, 0.4f, circle, triangle, diamond, lineRight, lineLeft, vee));
            GesturePath path = PathOf(SyntheticGestures.Sloppy(SyntheticGestures.Kind.Circle, new Random(1)));
            matcher.Match(path, 0f, path.Length, Quaternion.identity);

            Assert.That(() => matcher.Match(path, 0f, path.Length, Quaternion.identity), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Match_StillController_MatchesNothing()
        {
            var matcher = new GestureMatcher(Library(3, 0.4f, circle, lineRight));
            var point = new Vector3(0.4f, 1.2f, 0.5f);

            MatchAll(matcher, new[] { point, point });

            Assert.That(matcher.Result.Winner, Is.EqualTo(-1));
            Assert.That(ScoreOf(matcher, circle), Is.EqualTo(float.PositiveInfinity));
            Assert.That(ScoreOf(matcher, lineRight), Is.EqualTo(float.PositiveInfinity));
        }

        // A library with one spell for each shape, in order, and the plan's settings apart from these.
        private GestureLibrary Library(int startShifts, float maxTotalCut, params GestureShape[] shapes)
        {
            var spells = new SpellGesture[shapes.Length];
            for (int i = 0; i < shapes.Length; i++)
                spells[i] = assets.Spell($"{shapes[i].name} Spell", shapes[i], 0.01f, 10f);
            GestureLibrary library = assets.Library("Test Library", 64, startShifts, spells);
            library.MaxTotalCut = maxTotalCut;
            return library;
        }

        // The checks reported each template's exact score. A margin this large keeps every shape under
        // the cutoff, so that the matcher's scores are exact too.
        private GestureMatcher CheckMatcher(int startShifts, float maxTotalCut, params GestureShape[] shapes)
        {
            GestureLibrary library = Library(startShifts, maxTotalCut, shapes);
            library.MinMargin = 10f;
            return new GestureMatcher(library);
        }

        private static GesturePath PathOf(IEnumerable<Vector3> points)
        {
            var path = new GesturePath();
            foreach (Vector3 point in points)
                path.Add(point, 0f);
            return path;
        }

        // Matches the whole path, in a gesture frame with no rotation.
        private static void MatchAll(GestureMatcher matcher, IEnumerable<Vector3> points)
        {
            GesturePath path = PathOf(points);
            matcher.Match(path, 0f, path.Length, Quaternion.identity);
        }

        private static int IndexOf(GestureMatcher matcher, GestureShape shape)
        {
            return Array.IndexOf(matcher.Templates.Shapes, shape);
        }

        private static float ScoreOf(GestureMatcher matcher, GestureShape shape)
        {
            return matcher.Result.Score(IndexOf(matcher, shape));
        }

        // Checks the result against the reference within the cutoff, min(threshold, best) + margin.
        // Below it, scores must agree, and the winner's cut and size too. At or above it, the result
        // must not report a score below the cutoff. Returns whether the winner was under the cutoff
        // and how many other shapes were.
        private static (bool winnerChecked, int othersChecked) AssertAgrees(MatchResult result,
            BruteForceMatcher.ShapeMatch[] reference, float threshold, float margin, string message)
        {
            int winner = 0;
            for (int s = 1; s < reference.Length; s++)
            {
                if (reference[s].Score < reference[winner].Score)
                    winner = s;
            }
            BruteForceMatcher.ShapeMatch best = reference[winner];
            float cutoff = Mathf.Min(threshold, best.Score) + margin;

            bool winnerChecked = best.Score < threshold + margin;
            if (winnerChecked)
            {
                Assert.That(result.Winner, Is.EqualTo(winner), $"{message}: winner");
                Assert.That(result.Score(winner), Is.EqualTo(best.Score).Within(ExactTolerance), $"{message}: score");
                Assert.That(result.StartCut(winner), Is.EqualTo(best.StartCut), $"{message}: start cut");
                Assert.That(result.EndCut(winner), Is.EqualTo(best.EndCut), $"{message}: end cut");
                Assert.That(result.Size(winner), Is.EqualTo(best.Size).Within(1e-6f), $"{message}: size");
            }
            else
            {
                Assert.That(result.Winner, Is.EqualTo(-1), $"{message}: the best score, {best.Score}, is not under the cutoff");
            }

            int othersChecked = 0;
            float runnerUp = float.PositiveInfinity;
            for (int s = 0; s < reference.Length; s++)
            {
                if (s == winner)
                    continue;
                runnerUp = Mathf.Min(runnerUp, reference[s].Score);
                if (reference[s].Score < cutoff)
                {
                    othersChecked++;
                    Assert.That(result.Score(s), Is.EqualTo(reference[s].Score).Within(ExactTolerance),
                        $"{message}: score of shape {s}");
                }
                else
                {
                    Assert.That(result.Score(s), Is.GreaterThanOrEqualTo(cutoff - ExactTolerance),
                        $"{message}: score of shape {s}, above the cutoff");
                }
            }
            if (runnerUp < cutoff)
                Assert.That(result.RunnerUpScore, Is.EqualTo(runnerUp).Within(ExactTolerance), $"{message}: runner-up");
            else
                Assert.That(result.RunnerUpScore, Is.GreaterThanOrEqualTo(cutoff - ExactTolerance),
                    $"{message}: runner-up, above the cutoff");
            return (winnerChecked, othersChecked);
        }
    }
}
