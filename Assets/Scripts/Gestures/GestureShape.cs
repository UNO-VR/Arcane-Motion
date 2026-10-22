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
    }
}
