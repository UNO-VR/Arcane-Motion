namespace ArcaneMotion.Gestures
{
    // A GestureMatcher's scores for the evaluated part of a path, one per shape in its templates, in
    // the order of GestureTemplates.Shapes. The matcher reuses one result for every call.
    //
    // A shape's score is its best over every cut and variant, and it is exact while it is below the
    // cutoff: min(threshold, the winner's score) + margin, where the threshold is the larger of the
    // match and keep-match scores. At or above the cutoff no decision can change, so the matcher
    // stops looking: such a score may be above the shape's true best, and it is infinity if the
    // shape never got under the cutoff.
    public sealed class MatchResult
    {
        private readonly float[] scores;
        private readonly int[] cuts;
        private readonly float[] startCuts;
        private readonly float[] endCuts;
        private readonly float[] sizes;

        internal MatchResult(int shapeCount)
        {
            scores = new float[shapeCount];
            cuts = new int[shapeCount];
            startCuts = new float[shapeCount];
            endCuts = new float[shapeCount];
            sizes = new float[shapeCount];
            Clear();
        }

        public int ShapeCount => scores.Length;

        // The shape with the lowest score, or -1 if no shape got under the cutoff.
        public int Winner { get; private set; }

        // The lowest score of any shape but the winner, or infinity if there is none.
        public float RunnerUpScore { get; private set; }

        public float Score(int shape) => scores[shape];

        // The fraction of the evaluated part that the shape's best cut removed from its start. Like
        // EndCut and Size, it is meaningful only while the shape's score is not infinity.
        public float StartCut(int shape) => startCuts[shape];

        public float EndCut(int shape) => endCuts[shape];

        // The RMS distance in meters of the best cut's resampled points from their center: the size
        // that normalization removed.
        public float Size(int shape) => sizes[shape];

        // The index of the shape's best cut in the matcher's cut order.
        internal int Cut(int shape) => cuts[shape];

        internal void Clear()
        {
            for (int s = 0; s < scores.Length; s++)
            {
                scores[s] = float.PositiveInfinity;
                cuts[s] = -1;
                startCuts[s] = 0f;
                endCuts[s] = 0f;
                sizes[s] = 0f;
            }
            Winner = -1;
            RunnerUpScore = float.PositiveInfinity;
        }

        internal void Record(int shape, float score, int cut, float startCut, float endCut, float size)
        {
            scores[shape] = score;
            cuts[shape] = cut;
            startCuts[shape] = startCut;
            endCuts[shape] = endCut;
            sizes[shape] = size;
        }

        // Sets the winner and the runner-up's score from the scores. On a tie, the earlier shape wins.
        internal void FindWinner()
        {
            Winner = -1;
            float best = float.PositiveInfinity;
            RunnerUpScore = float.PositiveInfinity;
            for (int s = 0; s < scores.Length; s++)
            {
                if (scores[s] < best)
                {
                    RunnerUpScore = best;
                    best = scores[s];
                    Winner = s;
                }
                else if (scores[s] < RunnerUpScore)
                {
                    RunnerUpScore = scores[s];
                }
            }
        }
    }
}
