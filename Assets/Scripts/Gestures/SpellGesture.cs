using UnityEngine;

namespace ArcaneMotion.Gestures
{
    // One spell's gesture: a shape drawn within a size range. Releasing on a match hands this asset
    // to the rest of the game, which maps it to spell behavior. Gesture code never references spell
    // code.
    [CreateAssetMenu(fileName = "Spell Gesture", menuName = "Arcane Motion/Spell Gesture")]
    public class SpellGesture : ScriptableObject
    {
        [Tooltip("The shape the player draws.")]
        [SerializeField] private GestureShape shape;

        [Tooltip("The smallest accepted size in meters: the RMS distance of the matched part's points from their " +
                 "center. For a circle it is the radius, for a straight line 0.29 times its length, and for a square " +
                 "0.58 times its side.")]
        [SerializeField, Min(0f)] private float minSize;

        [Tooltip("The size in meters, measured as for Min Size, where the accepted range ends. The range includes " +
                 "Min Size but not Max Size, so a spell that shares this shape may start its range here. Ranges " +
                 "on one shape must not overlap.")]
        [SerializeField, Min(0f)] private float maxSize;

        [Tooltip("The ink trail's color while this spell is the match.")]
        [SerializeField] private Color inkColor = Color.white;

        public GestureShape Shape
        {
            get => shape;
            set => shape = value;
        }

        public float MinSize
        {
            get => minSize;
            set => minSize = value;
        }

        public float MaxSize
        {
            get => maxSize;
            set => maxSize = value;
        }

        public Color InkColor => inkColor;
    }
}
