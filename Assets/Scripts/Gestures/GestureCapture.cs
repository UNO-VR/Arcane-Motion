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

        [Tooltip("The button held while drawing a gesture: this hand's action in Gesture Input.")]
        [SerializeField] private XRInputButtonReader captureButton = new XRInputButtonReader("Capture");

        private GestureSession session;
        private double pressTime;

        // Raised, with this capture, on the first frame the button reads as held, after the session
        // has begun the new gesture.
        public event Action<GestureCapture> Started;

        // Raised, with this capture, on the first frame the button reads as released after a
        // gesture. A gesture abandoned by disabling this component raises nothing.
        public event Action<GestureCapture> Completed;

        public bool IsCapturing { get; private set; }

        // The current gesture's stored points in tracking space, or the last gesture's until the
        // next press.
        public GesturePath Path => session.Path;

        public Transform TrackedController => trackedController;

        public Transform TrackingSpace => trackingSpace;

        // Creates a new session each time, so that in Play Mode, toggling this component applies
        // edits to the library.
        private void OnEnable()
        {
            if (library == null || trackedController == null || trackingSpace == null)
            {
                // Disabled first, so that Update does not run without its references.
                enabled = false;
                throw new UnassignedReferenceException(
                    $"{nameof(GestureCapture)} on '{name}' needs Library, Tracked Controller, and Tracking Space assigned.");
            }

            session = new GestureSession(library);
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
                    Completed?.Invoke(this);
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
                session.Begin();
                Started?.Invoke(this);
            }
            session.Advance(trackingSpace.InverseTransformPoint(trackedController.position), (float)(now - pressTime));
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
