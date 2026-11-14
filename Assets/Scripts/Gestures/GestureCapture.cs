using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace ArcaneMotion.Gestures
{
    // Captures one hand's gestures. While the capture button is held, it passes the controller's
    // position in tracking space and the seconds since the press to a GestureSession each frame.
    // Positions in tracking space (the XR Origin's Camera Offset) mean that locomotion, turns, and
    // teleports during a gesture do not bend it. Only this component reads input, transforms, and
    // the clock for recognition; the session it feeds is plain C#.
    public class GestureCapture : MonoBehaviour
    {
        [SerializeField] private GestureLibrary library;

        [Tooltip("The rig's Left Controller or Right Controller.")]
        [SerializeField] private Transform trackedController;

        [Tooltip("The XR Origin's Camera Offset: the space that controller poses are tracked in.")]
        [SerializeField] private Transform trackingSpace;

        [Tooltip("The XR Origin's Main Camera: the player's head.")]
        [SerializeField] private Transform head;

        [Tooltip("The button held while drawing a gesture: this hand's action in Gesture Input.")]
        [SerializeField] private XRInputButtonReader captureButton = new XRInputButtonReader("Capture");

        private GestureSession session;
        private double pressTime;

        // Raised, with this capture, on the first frame the button reads as held, after the session
        // has begun the new gesture.
        public event Action<GestureCapture> Started;

        // Raised, with this capture, each frame while the button is held, after the session has taken
        // the frame's sample: the controller's position in tracking space and the seconds since the
        // press. Every frame's sample is raised, including those the session does not store.
        public event Action<GestureCapture, Vector3, float> Sampled;

        // Raised, with this capture and the new state, in a frame where the session's status
        // changed, or its shape or spell changed while lit, after Sampled.
        public event Action<GestureCapture, RecognitionState> StateChanged;

        // Raised, with this capture and the state on show, on the first frame the button reads as
        // released after a gesture. The spell system casts when the status is Spell. A gesture
        // abandoned by disabling this component raises nothing.
        public event Action<GestureCapture, RecognitionState> Completed;

        public bool IsCapturing { get; private set; }

        // The state on show for the current gesture, or the last gesture's until the next press.
        public RecognitionState State => session.State;

        // The current gesture's stored points in tracking space, or the last gesture's until the
        // next press.
        public GesturePath Path => session.Path;

        // The head's rotation in tracking space at the current gesture's press, or the last
        // gesture's until the next press.
        public Quaternion HeadRotation => session.HeadRotation;

        public Transform TrackedController => trackedController;

        public Transform TrackingSpace => trackingSpace;

        // Creates a new session each time, which compiles the library's templates, so that in Play
        // Mode, toggling this component applies edits to the library.
        private void OnEnable()
        {
            if (library == null || trackedController == null || trackingSpace == null || head == null)
            {
                // Disabled first, so that Update does not run without its references.
                enabled = false;
                throw new UnassignedReferenceException(
                    $"{nameof(GestureCapture)} on '{name}' needs Library, Tracked Controller, Tracking Space, and Head assigned.");
            }

            try
            {
                session = new GestureSession(library);
            }
            catch
            {
                // The library has problems, which the exception lists. Disabled, as above.
                enabled = false;
                throw;
            }
            captureButton.EnableDirectActionIfModeUsed();
        }

        // The rig's InputActionManager enables its actions in its own OnEnable, so the capture action
        // can be checked only after every component in the scene is enabled.
        private void Start()
        {
            string problem = FindCaptureButtonProblem();
            if (problem != null)
            {
                enabled = false;
                throw new InvalidOperationException(
                    $"{nameof(GestureCapture)} on '{name}' cannot read Capture Button: {problem}");
            }
        }

        private void OnDisable()
        {
            // Abandons a gesture in progress, without Completed.
            IsCapturing = false;
            captureButton.DisableDirectActionIfModeUsed();
        }

        private void Update()
        {
            // Reading whether the button is held each frame, rather than when it is pressed and
            // released, means that a missed press or release cannot leave a capture stuck.
            if (!captureButton.ReadIsPerformed())
            {
                if (IsCapturing)
                {
                    IsCapturing = false;
                    Completed?.Invoke(this, session.State);
                }
                return;
            }

            // Recognition follows real hand motion, as the controller trail does, so it ignores
            // Time.timeScale.
            double now = Time.unscaledTimeAsDouble;
            if (!IsCapturing)
            {
                IsCapturing = true;
                pressTime = now;
                // In tracking space, like the positions.
                session.Begin(Quaternion.Inverse(trackingSpace.rotation) * head.rotation);
                Started?.Invoke(this);
            }
            Vector3 position = trackingSpace.InverseTransformPoint(trackedController.position);
            float time = (float)(now - pressTime);
            bool stateChanged = session.Advance(position, time);
            Sampled?.Invoke(this, position, time);
            if (stateChanged)
                StateChanged?.Invoke(this, session.State);
        }

        // Why the capture button cannot read input, or null if no problem that can be checked is found.
        // Each problem would otherwise leave the button reading as released forever, with no error.
        private string FindCaptureButtonProblem()
        {
            switch (captureButton.inputSourceMode)
            {
                case XRInputButtonReader.InputSourceMode.Unused:
                    return "it is set to Unused, so no gesture can start.";

                case XRInputButtonReader.InputSourceMode.InputAction:
                    // OnEnable enables this action, but XRI creates it with no bindings.
                    InputAction action = captureButton.inputActionPerformed;
                    if (action == null || action.bindings.Count == 0)
                        return "it is set to Input Action, but the action has no bindings.";
                    return null;

                case XRInputButtonReader.InputSourceMode.InputActionReference:
                    InputActionReference reference = captureButton.inputActionReferencePerformed;
                    if (reference == null || reference.action == null)
                        return "it is set to Input Action Reference, but no action is assigned.";
                    if (!reference.action.enabled)
                        return $"its action '{reference.action.name}' is not enabled. Add '{reference.asset.name}' " +
                               "to the Action Assets of the Input Action Manager on the XR Origin.";
                    return null;

                case XRInputButtonReader.InputSourceMode.ObjectReference:
                    if (captureButton.GetObjectReference() == null)
                        return "it is set to Object Reference, but no object is assigned.";
                    return null;

                default:
                    // Manual Value is set by hand or by a script, so it has nothing to check.
                    return null;
            }
        }
    }
}
