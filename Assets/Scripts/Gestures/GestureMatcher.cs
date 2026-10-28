using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace ArcaneMotion.Gestures
{
    // Scores the evaluated part of a gesture's path against every shape that a library's spells
    // use. For each start and end cut of the evaluated part, it resamples the rest and turns it into
    // the gesture frame once. It normalizes that once for each ignored axis that a shape uses, after
    // dropping the axis, and compares it with every variant of every shape that ignores that axis:
    // the score is the mean distance between corresponding points, plus the cut penalty times the
    // fraction cut. A shape's score is its best over every cut and variant.
    //
    // Its shortcuts change no decision. It abandons a comparison once the distances so far cannot
    // beat the shape's best or the cutoff (see MatchResult), and stops trying cuts once their
    // penalty alone cannot. It tries the previous call's winner and its cut first, which brings the
    // cutoff down early while the gesture changes little between calls. It allocates nothing after
    // it is created.
    public sealed class GestureMatcher
    {
        private static readonly ProfilerMarker MatchMarker = new ProfilerMarker("Gestures.Match");

        private readonly int pointCount;
        private readonly float threshold;
        private readonly float margin;
        // Every cut, in order of total cut and then of start cut, with no cut first: the fractions
        // of the evaluated part cut from its start and from its end, and the cut's penalty.
        private readonly float[] startCuts;
        private readonly float[] endCuts;
        private readonly float[] penalties;
        // The current cut's resampled points, turned into the gesture frame.
        private readonly Vector3[] turned;
        // Indexed by IgnoredAxis: the current cut's points without that axis, normalized, with their size
        // and whether they have a shape. The points are null for an axis that no shape ignores.
        private readonly Vector3[][] gestures;
        private readonly float[] sizes;
        private readonly bool[] hasShape;
        private readonly IgnoredAxis[] axesInUse;

        // Compiles the library's templates and copies its settings, so later edits to the library
        // do not change this matcher. Throws, listing every problem, if the library has any.
        public GestureMatcher(GestureLibrary library)
        {
            Templates = new GestureTemplates(library);
            pointCount = Templates.PointCount;
            threshold = Mathf.Max(library.MatchScore, library.KeepMatchScore);
            margin = library.MinMargin;

            // The tolerance keeps rounding in the division from losing the last step when the max
            // total cut is a whole number of steps.
            int steps = Mathf.FloorToInt(library.MaxTotalCut / library.CutStep + 1e-4f);
            int cutCount = (steps + 1) * (steps + 2) / 2;
            startCuts = new float[cutCount];
            endCuts = new float[cutCount];
            penalties = new float[cutCount];
            int cut = 0;
            for (int total = 0; total <= steps; total++)
            {
                for (int start = 0; start <= total; start++, cut++)
                {
                    startCuts[cut] = start * library.CutStep;
                    endCuts[cut] = (total - start) * library.CutStep;
                    penalties[cut] = library.CutPenalty * total * library.CutStep;
                }
            }

            turned = new Vector3[pointCount];
            int axisCount = Enum.GetValues(typeof(IgnoredAxis)).Length;
            gestures = new Vector3[axisCount][];
            sizes = new float[axisCount];
            hasShape = new bool[axisCount];
            var inUse = new List<IgnoredAxis>();
            foreach (IgnoredAxis axis in Templates.IgnoredAxes)
            {
                if (inUse.Contains(axis))
                    continue;
                inUse.Add(axis);
                gestures[(int)axis] = new Vector3[pointCount];
            }
            axesInUse = inUse.ToArray();
            Result = new MatchResult(Templates.Shapes.Length);
        }

        public GestureTemplates Templates { get; }

        // The last call's result, which every call overwrites.
        public MatchResult Result { get; }

        // Forgets the last call's winner and cut, so that the next call starts as the first did.
        // Call it when a new gesture begins.
        public void Reset()
        {
            Result.Clear();
        }

        // Scores the part of the path from fromLength to toLength, the evaluated part, into Result.
        // rotation turns tracking space into the gesture frame.
        public void Match(GesturePath path, float fromLength, float toLength, Quaternion rotation)
        {
            using (MatchMarker.Auto())
            {
                MatchResult result = Result;
                int firstShape = Mathf.Max(result.Winner, 0);
                int hintCut = result.Winner >= 0 ? result.Cut(result.Winner) : -1;
                result.Clear();

                int shapeCount = result.ShapeCount;
                int[] variantStarts = Templates.VariantStarts;
                IgnoredAxis[] shapeAxes = Templates.IgnoredAxes;
                Vector3[] points = Templates.Points;
                float evaluatedLength = toLength - fromLength;
                float bestOverall = float.PositiveInfinity;
                float cutoff = threshold + margin;

                // Step -1 is the hint cut, if there is one, and the steps after it skip it.
                for (int step = hintCut >= 0 ? -1 : 0; step < startCuts.Length; step++)
                {
                    int cut = step < 0 ? hintCut : step;
                    if (step >= 0 && cut == hintCut)
                        continue;

                    // The cut can improve no shape once its penalty alone reaches every shape's bound.
                    float penalty = penalties[cut];
                    float loosestBound = 0f;
                    for (int s = 0; s < shapeCount; s++)
                        loosestBound = Mathf.Max(loosestBound, result.Score(s));
                    if (penalty >= Mathf.Min(loosestBound, cutoff))
                    {
                        // The hint cut is out of order, but every cut after this one in the order
                        // has at least this penalty.
                        if (step < 0)
                            continue;
                        break;
                    }

                    path.Resample(fromLength + startCuts[cut] * evaluatedLength,
                        toLength - endCuts[cut] * evaluatedLength, turned);
                    for (int k = 0; k < pointCount; k++)
                        turned[k] = rotation * turned[k];
                    // Dropping an axis moves the centroid and changes the size, so each axis normalizes
                    // its own copy.
                    foreach (IgnoredAxis axis in axesInUse)
                    {
                        Vector3[] flat = gestures[(int)axis];
                        for (int k = 0; k < pointCount; k++)
                            flat[k] = GestureNormalization.Flatten(turned[k], axis);
                        hasShape[(int)axis] = GestureNormalization.Normalize(flat, Quaternion.identity, out sizes[(int)axis]);
                    }

                    for (int order = 0; order < shapeCount; order++)
                    {
                        // The last winner first, then the rest in order.
                        int shape = order == 0 ? firstShape : order <= firstShape ? order - 1 : order;
                        // Points all within 1e-6 m of their center have no shape for this shape's
                        // templates to match.
                        int axisIndex = (int)shapeAxes[shape];
                        if (!hasShape[axisIndex])
                            continue;
                        Vector3[] gesture = gestures[axisIndex];
                        float size = sizes[axisIndex];
                        for (int variant = variantStarts[shape]; variant < variantStarts[shape + 1]; variant++)
                        {
                            // Distances adding up to this mean a score that changes nothing.
                            float limit = (Mathf.Min(result.Score(shape), cutoff) - penalty) * pointCount;
                            if (limit <= 0f)
                                break;

                            int offset = variant * pointCount;
                            float sum = 0f;
                            for (int k = 0; k < pointCount; k++)
                            {
                                Vector3 a = gesture[k];
                                Vector3 b = points[offset + k];
                                float dx = a.x - b.x;
                                float dy = a.y - b.y;
                                float dz = a.z - b.z;
                                sum += Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                                if (sum >= limit)
                                    break;
                            }
                            if (sum >= limit)
                                continue;

                            float score = sum / pointCount + penalty;
                            result.Record(shape, score, cut, startCuts[cut], endCuts[cut], size);
                            if (score < bestOverall)
                            {
                                bestOverall = score;
                                cutoff = Mathf.Min(threshold, bestOverall) + margin;
                            }
                        }
                    }
                }

                result.FindWinner();
            }
        }
    }
}
