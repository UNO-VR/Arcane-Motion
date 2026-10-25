using System.Collections.Generic;
using UnityEngine;

namespace ArcaneMotion.Gestures
{
    // The spells and settings that affect a recognition result, in one asset that both hands share.
    // A session copies what it uses when it is created, so in Play Mode an edit applies when a
    // GestureCapture is next enabled.
    [CreateAssetMenu(fileName = "Gesture Library", menuName = "Arcane Motion/Gesture Library")]
    public class GestureLibrary : ScriptableObject
    {
        [Tooltip("The spells that a gesture can match. Spells that share a shape need size ranges with a gap " +
                 "between them.")]
        [SerializeField] private List<SpellGesture> spells = new List<SpellGesture>();

        [Tooltip("Distance in meters the controller must move from the last stored point before a sample is " +
                 "stored. This keeps tracking jitter while the hand is still from adding length to the path.")]
        [SerializeField, Min(0f)] private float minPointSpacing = 0.005f;

        [Tooltip("N: the number of evenly spaced points that the path and each template are resampled to before " +
                 "they are compared.")]
        [SerializeField, Min(2)] private int pointCount = 64;

        [Tooltip("The step between the cuts that the matcher tries at each end of the path, as a fraction of the " +
                 "evaluated path. Cuts drop a wind-up before the shape or motion after it. Must be above zero.")]
        [SerializeField, Min(0f)] private float cutStep = 0.025f;

        [Tooltip("The largest fraction of the evaluated path that the start and end cuts may remove together. " +
                 "Must be below 1.")]
        [SerializeField, Range(0f, 1f)] private float maxTotalCut = 0.4f;

        [Tooltip("Added to a score for each fraction of the evaluated path that the cuts remove, so that a cut is " +
                 "taken only when it improves the match by more than this.")]
        [SerializeField, Min(0f)] private float cutPenalty = 0.2f;

        [Tooltip("K: the start points tried on each side of a closed shape's set start, each 1/(N - 1) of its " +
                 "perimeter from the next. Must be below (N - 1) / 2.")]
        [SerializeField, Min(0)] private int startShifts = 3;

        [Tooltip("A shape lights when its score is below this. A score of 0 is a perfect match.")]
        [SerializeField, Min(0f)] private float matchScore = 0.25f;

        [Tooltip("A lit shape stays lit while its score is below this, which must not be below Match Score. A " +
                 "higher value stops a shape near the match score from flickering.")]
        [SerializeField, Min(0f)] private float keepMatchScore = 0.25f;

        [Tooltip("How far the runner-up's score must be above the best shape's score for the best shape to " +
                 "light. 0 turns this rule off.")]
        [SerializeField, Min(0f)] private float minMargin;

        public List<SpellGesture> Spells => spells;

        public float MinPointSpacing
        {
            get => minPointSpacing;
            set => minPointSpacing = value;
        }

        public int PointCount
        {
            get => pointCount;
            set => pointCount = value;
        }

        public float CutStep
        {
            get => cutStep;
            set => cutStep = value;
        }

        public float MaxTotalCut
        {
            get => maxTotalCut;
            set => maxTotalCut = value;
        }

        public float CutPenalty
        {
            get => cutPenalty;
            set => cutPenalty = value;
        }

        public int StartShifts
        {
            get => startShifts;
            set => startShifts = value;
        }

        public float MatchScore
        {
            get => matchScore;
            set => matchScore = value;
        }

        public float KeepMatchScore
        {
            get => keepMatchScore;
            set => keepMatchScore = value;
        }

        public float MinMargin
        {
            get => minMargin;
            set => minMargin = value;
        }

        // Every problem that stops templates from being compiled from this library, each naming its
        // asset. The list is empty when there is none.
        public List<string> Validate()
        {
            var problems = new List<string>();
            var shapes = new List<GestureShape>();
            for (int i = 0; i < spells.Count; i++)
            {
                SpellGesture spell = spells[i];
                if (spell == null)
                {
                    problems.Add($"Gesture library '{name}' has no spell in element {i} of its spell list.");
                    continue;
                }

                if (spell.Shape == null)
                    problems.Add($"Spell '{spell.name}' has no shape.");
                else if (!shapes.Contains(spell.Shape))
                    shapes.Add(spell.Shape);
                // Written as negations so that NaN fails them too.
                if (!(spell.MinSize > 0f))
                    problems.Add($"Spell '{spell.name}' has a min size of {spell.MinSize} m, which must be above zero.");
                if (!(spell.MaxSize > spell.MinSize))
                    problems.Add($"Spell '{spell.name}' has a max size of {spell.MaxSize} m, which must be above " +
                                 $"its min size of {spell.MinSize} m.");

                for (int j = 0; j < i; j++)
                {
                    SpellGesture other = spells[j];
                    // Ranges that only touch overlap too, because a size at the shared end would fit both.
                    if (other != null && spell.Shape != null && other.Shape == spell.Shape &&
                        spell.MinSize <= other.MaxSize && other.MinSize <= spell.MaxSize)
                        problems.Add($"Spells '{other.name}' and '{spell.name}' share shape '{spell.Shape.name}', " +
                                     $"but their size ranges, {other.MinSize} to {other.MaxSize} m and " +
                                     $"{spell.MinSize} to {spell.MaxSize} m, overlap. Leave a gap between them.");
                }
            }

            foreach (GestureShape shape in shapes)
            {
                Vector3[] points = shape.Points;
                int count = points?.Length ?? 0;
                int needed = shape.Closed ? 3 : 2;
                if (count < needed)
                {
                    problems.Add($"Shape '{shape.name}' has {count} {(count == 1 ? "point" : "points")}, but " +
                                 $"{(shape.Closed ? "a closed" : "an open")} shape needs at least {needed}.");
                    continue;
                }

                float length = shape.Closed ? Vector3.Distance(points[count - 1], points[0]) : 0f;
                for (int k = 1; k < count; k++)
                    length += Vector3.Distance(points[k - 1], points[k]);
                if (!(length > 0f))
                    problems.Add($"Shape '{shape.name}' has no length.");
            }

            // 2K + 1 shifts of a closed shape must fit in its N - 1 loop points without repeating one.
            if (2 * startShifts + 1 >= pointCount)
                problems.Add($"Gesture library '{name}' has {startShifts} start shifts, but with {pointCount} " +
                             $"resampled points they must be below (N - 1) / 2 = {(pointCount - 1) / 2f}.");
            if (!(cutStep > 0f))
                problems.Add($"Gesture library '{name}' has a cut step of {cutStep}, which must be above zero.");
            // A cut that removed the whole evaluated path would leave nothing to compare.
            if (!(maxTotalCut < 1f))
                problems.Add($"Gesture library '{name}' has a max total cut of {maxTotalCut}, which must be below 1.");
            if (keepMatchScore < matchScore)
                problems.Add($"Gesture library '{name}' has a keep-match score of {keepMatchScore}, which must not be " +
                             $"below its match score of {matchScore}.");
            return problems;
        }

        // Shows the problems while the library is edited. Compiling templates throws on them.
        private void OnValidate()
        {
            foreach (string problem in Validate())
                Debug.LogWarning(problem, this);
        }
    }
}
