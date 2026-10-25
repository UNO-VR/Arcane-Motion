using System;

namespace ArcaneMotion.Gestures
{
    // Turns each MatchResult into a RecognitionState, and remembers which shape is lit between
    // updates. The best-scoring shape lights when its score is below the match score, or the
    // keep-match score while it is already lit, and the runner-up scores at least the min margin
    // higher. Only then is a spell chosen, by size, from that shape's spells: shape before size.
    public sealed class GestureRecognizer
    {
        private readonly GestureShape[] shapes;
        private readonly float matchScore;
        private readonly float keepMatchScore;
        private readonly float minMargin;
        // Each spell with the index of its shape in shapes, and its size range.
        private readonly SpellGesture[] spells;
        private readonly int[] spellShapes;
        private readonly float[] minSizes;
        private readonly float[] maxSizes;
        private int litShape = -1;

        // Copies the library's spells and acceptance settings, so later edits to the library do not
        // change this recognizer. templates gives the shapes in the order of the matcher's results.
        public GestureRecognizer(GestureLibrary library, GestureTemplates templates)
        {
            if (library == null)
                throw new ArgumentNullException(nameof(library));
            if (templates == null)
                throw new ArgumentNullException(nameof(templates));

            shapes = templates.Shapes;
            matchScore = library.MatchScore;
            keepMatchScore = library.KeepMatchScore;
            minMargin = library.MinMargin;

            spells = library.Spells.ToArray();
            spellShapes = new int[spells.Length];
            minSizes = new float[spells.Length];
            maxSizes = new float[spells.Length];
            for (int i = 0; i < spells.Length; i++)
            {
                spellShapes[i] = Array.IndexOf(shapes, spells[i].Shape);
                minSizes[i] = spells[i].MinSize;
                maxSizes[i] = spells[i].MaxSize;
            }
        }

        // Forgets the lit shape. Call it when a new gesture begins.
        public void Reset()
        {
            litShape = -1;
        }

        // The state for a matcher result that scored the path from evaluatedStart to evaluatedEnd.
        public RecognitionState Recognize(MatchResult result, float evaluatedStart, float evaluatedEnd)
        {
            int candidate = result.Winner;
            if (candidate < 0)
            {
                litShape = -1;
                return new RecognitionState(RecognitionStatus.None, null, null, float.PositiveInfinity, 0f,
                    evaluatedStart, evaluatedEnd, 0f, 0f);
            }

            float score = result.Score(candidate);
            float size = result.Size(candidate);
            float limit = candidate == litShape ? keepMatchScore : matchScore;
            // An infinite runner-up score leaves an infinite margin.
            bool lit = score < limit && result.RunnerUpScore - score >= minMargin;
            litShape = lit ? candidate : -1;

            RecognitionStatus status = RecognitionStatus.None;
            SpellGesture spell = null;
            if (lit)
            {
                // Validate rules out overlapping ranges on one shape, so at most one spell fits. A range
                // includes its min size but not its max size.
                for (int i = 0; i < spells.Length && spell == null; i++)
                {
                    if (spellShapes[i] == candidate && size >= minSizes[i] && size < maxSizes[i])
                        spell = spells[i];
                }
                status = spell != null ? RecognitionStatus.Spell : RecognitionStatus.WrongSize;
            }

            // The same arithmetic as the matcher's, so the matched part is the cut it resampled.
            float evaluatedLength = evaluatedEnd - evaluatedStart;
            return new RecognitionState(status, shapes[candidate], spell, score, size, evaluatedStart, evaluatedEnd,
                evaluatedStart + result.StartCut(candidate) * evaluatedLength,
                evaluatedEnd - result.EndCut(candidate) * evaluatedLength);
        }
    }
}
