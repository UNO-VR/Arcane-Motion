namespace ArcaneMotion.Gestures
{
    public enum RecognitionStatus
    {
        // No shape is lit.
        None,
        // A shape is lit, and its size is in the range of one of its spells.
        Spell,
        // A shape is lit, but its size is in the range of none of its spells.
        WrongSize,
    }

    // What recognition shows for a gesture after the matcher's latest run. Release casts the state on
    // show, so this is also what a completed gesture produced. Lengths are path lengths in meters
    // along the session's stored points.
    public readonly struct RecognitionState
    {
        public RecognitionState(RecognitionStatus status, GestureShape shape, SpellGesture spell, float score,
            float size, float evaluatedStart, float evaluatedEnd, float matchedStart, float matchedEnd)
        {
            Status = status;
            Shape = shape;
            Spell = spell;
            Score = score;
            Size = size;
            EvaluatedStart = evaluatedStart;
            EvaluatedEnd = evaluatedEnd;
            MatchedStart = matchedStart;
            MatchedEnd = matchedEnd;
        }

        public RecognitionStatus Status { get; }

        // The best-scoring shape, lit or not, or null if no shape scored under the matcher's cutoff.
        public GestureShape Shape { get; }

        // The spell to cast, for status Spell; otherwise null.
        public SpellGesture Spell { get; }

        // The shape's score, or infinity if there is no shape. 0 is a perfect match.
        public float Score { get; }

        // The size in meters of the matched part: the RMS distance of its resampled points from their
        // center. Like the matched part, it is meaningful only when there is a shape.
        public float Size { get; }

        // The part of the path that the matcher scored: the stored points after the start trim, up to
        // the end trim before the run.
        public float EvaluatedStart { get; }

        public float EvaluatedEnd { get; }

        // The part of the evaluated part that the shape's best start and end cuts kept.
        public float MatchedStart { get; }

        public float MatchedEnd { get; }
    }
}
