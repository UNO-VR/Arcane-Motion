using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace ArcaneMotion.Gestures
{
    // Pulses the controller once when a spell's match starts: when the state on show becomes Spell
    // after another status, or changes to another spell.
    [RequireComponent(typeof(GestureCapture))]
    public class GestureHaptics : MonoBehaviour
    {
        [Tooltip("The Haptic Impulse Player on the rig's Left Controller or Right Controller.")]
        [SerializeField] private HapticImpulsePlayer hapticPlayer;

        [Tooltip("The pulse's strength, from 0 to 1.")]
        [SerializeField, Range(0f, 1f)] private float amplitude = 0.3f;

        [Tooltip("The pulse's length in seconds.")]
        [SerializeField, Min(0f)] private float duration = 0.05f;

        private GestureCapture capture;

        private void Awake()
        {
            capture = GetComponent<GestureCapture>();
        }

        private void OnEnable()
        {
            if (hapticPlayer == null)
            {
                enabled = false;
                throw new UnassignedReferenceException(
                    $"{nameof(GestureHaptics)} on '{name}' needs Haptic Player assigned.");
            }
            capture.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            capture.StateChanged -= OnStateChanged;
        }

        // StateChanged is raised only for a new status, or a new shape or spell while lit, so a Spell
        // state here is always a new match.
        private void OnStateChanged(GestureCapture sender, RecognitionState state)
        {
            if (state.Status == RecognitionStatus.Spell)
                hapticPlayer.SendHapticImpulse(amplitude, duration);
        }
    }
}
