using UnityEngine;

namespace ArcaneMotion.Gestures
{
    // The settings that affect a recognition result, in one asset that both hands share. A session
    // copies them when it is created, so in Play Mode an edit applies when a GestureCapture is next
    // enabled.
    [CreateAssetMenu(fileName = "Gesture Library", menuName = "Arcane Motion/Gesture Library")]
    public class GestureLibrary : ScriptableObject
    {
        [Tooltip("Distance in meters the controller must move from the last stored point before a sample is " +
                 "stored. This keeps tracking jitter while the hand is still from adding length to the path.")]
        [SerializeField, Min(0f)] private float minPointSpacing = 0.005f;

        public float MinPointSpacing
        {
            get => minPointSpacing;
            set => minPointSpacing = value;
        }
    }
}
