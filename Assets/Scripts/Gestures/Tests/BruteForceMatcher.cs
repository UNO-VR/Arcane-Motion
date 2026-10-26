using UnityEngine;

namespace ArcaneMotion.Gestures.Tests
{
    // The matcher's definition with none of its shortcuts, as the reference for GestureMatcher:
    // every cut against every variant of every shape, adding up every distance. Each shape's score
    // is exact, because there is no cutoff.
    internal static class BruteForceMatcher
    {
        public struct ShapeMatch
        {
            public float Score;
            public float StartCut;
            public float EndCut;
            public float Size;
        }

        public static ShapeMatch[] Match(GestureLibrary library, GestureTemplates templates, GesturePath path,
            float fromLength, float toLength, Quaternion rotation)
        {
            int pointCount = templates.PointCount;
            // Whole steps up to the max total cut, with the matcher's tolerance for rounding.
            int steps = Mathf.FloorToInt(library.MaxTotalCut / library.CutStep + 1e-4f);
            var matches = new ShapeMatch[templates.Shapes.Length];
            for (int s = 0; s < matches.Length; s++)
                matches[s].Score = float.PositiveInfinity;

            var points = new Vector3[pointCount];
            float evaluatedLength = toLength - fromLength;
            // The matcher's cut order, so that a tie goes to the same cut.
            for (int total = 0; total <= steps; total++)
            {
                for (int start = 0; start <= total; start++)
                {
                    float startCut = start * library.CutStep;
                    float endCut = (total - start) * library.CutStep;
                    path.Resample(fromLength + startCut * evaluatedLength, toLength - endCut * evaluatedLength, points);
                    if (!GestureNormalization.Normalize(points, rotation, out float size))
                        continue;

                    float penalty = library.CutPenalty * total * library.CutStep;
                    for (int s = 0; s < matches.Length; s++)
                    {
                        for (int v = templates.VariantStarts[s]; v < templates.VariantStarts[s + 1]; v++)
                        {
                            float sum = 0f;
                            for (int k = 0; k < pointCount; k++)
                                sum += Vector3.Distance(points[k], templates.Points[v * pointCount + k]);
                            float score = sum / pointCount + penalty;
                            if (score < matches[s].Score)
                                matches[s] = new ShapeMatch { Score = score, StartCut = startCut, EndCut = endCut, Size = size };
                        }
                    }
                }
            }
            return matches;
        }
    }
}
