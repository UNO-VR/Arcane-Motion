using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneMotion.Gestures
{
    // The templates that gestures are compared against, compiled once from a library for each
    // distinct shape that its spells use. A shape has a variant for each way of drawing it that the
    // matcher accepts: each direction and, for a closed shape, each start point near the set start.
    // A variant is N points evenly spaced along the stroke, without the shape's ignored axis, and
    // normalized. Every variant is in one flat array, so that matching reads them without
    // allocating.
    public sealed class GestureTemplates
    {
        // Throws, listing every problem, if the library has any.
        public GestureTemplates(GestureLibrary library)
        {
            if (library == null)
                throw new ArgumentNullException(nameof(library));
            List<string> problems = library.Validate();
            if (problems.Count > 0)
                throw new InvalidOperationException(
                    $"Gesture library '{library.name}' has {problems.Count} " +
                    $"{(problems.Count == 1 ? "problem" : "problems")}:\n{string.Join("\n", problems)}");

            PointCount = library.PointCount;
            var shapes = new List<GestureShape>();
            foreach (SpellGesture spell in library.Spells)
            {
                if (!shapes.Contains(spell.Shape))
                    shapes.Add(spell.Shape);
            }

            var points = new List<Vector3>();
            var path = new GesturePath();
            var forward = new Vector3[PointCount];
            var reverse = new Vector3[PointCount];
            var variant = new Vector3[PointCount];
            VariantStarts = new int[shapes.Count + 1];
            for (int s = 0; s < shapes.Count; s++)
            {
                GestureShape shape = shapes[s];
                VariantStarts[s] = points.Count / PointCount;
                Resample(shape, false, path, forward);
                if (shape.BothDirections)
                    Resample(shape, true, path, reverse);

                int maxShift = shape.Closed ? library.StartShifts : 0;
                for (int distance = 0; distance <= maxShift; distance++)
                {
                    AddShifts(shape, forward, distance, variant, points);
                    if (shape.BothDirections)
                        AddShifts(shape, reverse, distance, variant, points);
                }
            }
            VariantStarts[shapes.Count] = points.Count / PointCount;

            Shapes = shapes.ToArray();
            IgnoredAxes = shapes.ConvertAll(shape => shape.IgnoredAxis).ToArray();
            MatchScoreScales = shapes.ConvertAll(shape => shape.MatchScoreScale).ToArray();
            Points = points.ToArray();
        }

        // N, the number of points in each variant.
        public int PointCount { get; }

        // The distinct shapes that the library's spells use, in the order of their first spell.
        public GestureShape[] Shapes { get; }

        // Each shape's ignored axis, in the order of Shapes, as it was when the templates were compiled.
        public IgnoredAxis[] IgnoredAxes { get; }

        // Each shape's match score scale, in the order of Shapes, as it was when the templates were
        // compiled, so that the matcher and the recognizer use the same values.
        public float[] MatchScoreScales { get; }

        // Shape s's variants are VariantStarts[s] up to, but not including, VariantStarts[s + 1],
        // likeliest first: the set start, then start points shifted from nearest to farthest. At each
        // shift, the set direction comes before the reverse, and a later start before an earlier one.
        public int[] VariantStarts { get; }

        // Every variant's points, without its shape's ignored axis, normalized with no rotation.
        // Variant v's points are Points[v * PointCount] up to, but not including,
        // Points[(v + 1) * PointCount].
        public Vector3[] Points { get; }

        // Resamples the shape's stroke to destination.Length points, with the closing segment if the
        // shape is closed. Reversing keeps a closed shape's start, as p0, p(n-1), ..., p1, p0, and
        // starts an open shape from its other end.
        private static void Resample(GestureShape shape, bool reversed, GesturePath path, Vector3[] destination)
        {
            Vector3[] points = shape.Points;
            int count = points.Length;
            path.Clear();
            for (int i = 0; i < count; i++)
            {
                int index = i;
                if (reversed)
                    index = shape.Closed ? (count - i) % count : count - 1 - i;
                path.Add(points[index], 0f);
            }
            if (shape.Closed)
                path.Add(points[0], 0f);
            path.Resample(0f, path.Length, destination);
        }

        // Adds the variants that start distance loop points after the resampled stroke's start and
        // then before it, or the one variant at the start for a distance of 0.
        private static void AddShifts(GestureShape shape, Vector3[] resampled, int distance, Vector3[] variant,
            List<Vector3> points)
        {
            AddVariant(shape, resampled, distance, variant, points);
            if (distance > 0)
                AddVariant(shape, resampled, -distance, variant, points);
        }

        // A closed stroke's last resampled point is its first again, so its loop is the N - 1 points
        // before that. A closed variant runs once around the loop from its own start and repeats that
        // start at the end. An open variant is the resampled stroke.
        private static void AddVariant(GestureShape shape, Vector3[] resampled, int shift, Vector3[] variant,
            List<Vector3> points)
        {
            int loopCount = resampled.Length - 1;
            for (int i = 0; i < variant.Length; i++)
            {
                // Validate keeps the shift above -loopCount / 2, so the index stays positive.
                Vector3 point = shape.Closed ? resampled[(shift + i + loopCount) % loopCount] : resampled[i];
                variant[i] = GestureNormalization.Flatten(point, shape.IgnoredAxis);
            }
            if (!GestureNormalization.Normalize(variant, Quaternion.identity, out _))
                throw new InvalidOperationException(
                    $"Shape '{shape.name}' is too small to compare: its resampled points, without its ignored " +
                    "axis, are all within 1e-6 m of their center.");
            points.AddRange(variant);
        }
    }
}
