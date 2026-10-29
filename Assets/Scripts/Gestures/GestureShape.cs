using UnityEngine;

namespace ArcaneMotion.Gestures
{
    // A template shape: the ideal stroke that a player draws. Spells that differ only in size share
    // one shape, and the size ranges on their SpellGesture assets tell them apart.
    [CreateAssetMenu(fileName = "Gesture Shape", menuName = "Arcane Motion/Gesture Shape")]
    public class GestureShape : ScriptableObject
    {
        [Tooltip("The ideal stroke as a polyline in the gesture frame, in meters, from the set start point in the set " +
                 "drawing direction. x is the player's right, y is up, and z points away from the player. Draw it at " +
                 "a typical size; size does not affect matching.")]
        [SerializeField] private Vector3[] points = new Vector3[0];

        [Tooltip("The stroke returns to its start, as a circle or triangle does. The closing segment is implied: do " +
                 "not repeat the first point. Closed shapes also try start points near the set start.")]
        [SerializeField] private bool closed;

        [Tooltip("Also accept the stroke drawn in reverse. A closed shape keeps its start point; an open shape starts " +
                 "from its other end.")]
        [SerializeField] private bool bothDirections;

        [Tooltip("An axis of the gesture frame that matching drops from the gesture and the template, for a shape " +
                 "drawn flat in the plane of the other two: Z (depth) for a shape drawn facing the player. Tilting " +
                 "out of that plane, and a line's bow away from the player, then cost nothing, and the size is " +
                 "measured without the axis. Motion along the axis is invisible to this shape.")]
        [SerializeField] private IgnoredAxis ignoredAxis;

        [Tooltip("Multiplies the library's match and keep-match scores for this shape. Below 1 makes this shape " +
                 "stricter, and above 1 easier. A shape lights only while it is also the best-scoring shape. Must " +
                 "be above zero.")]
        [SerializeField, Min(0f)] private float matchScoreScale = 1f;

        public Vector3[] Points
        {
            get => points;
            set => points = value;
        }

        public bool Closed
        {
            get => closed;
            set => closed = value;
        }

        public bool BothDirections
        {
            get => bothDirections;
            set => bothDirections = value;
        }

        public IgnoredAxis IgnoredAxis
        {
            get => ignoredAxis;
            set => ignoredAxis = value;
        }

        public float MatchScoreScale
        {
            get => matchScoreScale;
            set => matchScoreScale = value;
        }
    }

    // An axis of the gesture frame: x is the player's right, y is up, and z points away from the player.
    public enum IgnoredAxis
    {
        None,
        X,
        Y,
        Z,
    }
}
