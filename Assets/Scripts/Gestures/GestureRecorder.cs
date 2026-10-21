using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace ArcaneMotion.Gestures
{
    // A development tool that saves each gesture its capture completes as a GestureRecording, in the
    // label's folder in GestureRecordings/. It is disabled by default: set Label, Note, and Hand, then
    // enable it before drawing the gestures to record. A gesture already in progress when it is
    // enabled, or one that the capture abandons, is not saved.
    [RequireComponent(typeof(GestureCapture))]
    public class GestureRecorder : MonoBehaviour
    {
        public enum Hand
        {
            Unassigned,
            Left,
            Right,
        }

        [Tooltip("The shape the gestures are meant to be, as its shape asset is named, or None for motion that " +
                 "should not match. Recordings are saved in a folder of this name.")]
        [SerializeField] private string label = "";

        [Tooltip("Free text saved with each recording, such as clean, sloppy, or wind-up.")]
        [SerializeField] private string note = "";

        [Tooltip("The hand whose controller the capture follows.")]
        [SerializeField] private Hand hand;

        private readonly List<Vector3> positions = new List<Vector3>();
        private readonly List<float> times = new List<float>();
        private GestureCapture capture;
        // True from a press that this recorder saw until its release, or until the recorder is disabled.
        private bool recordingGesture;
        private DateTime pressedAtUtc;
        private Quaternion headRotation;

        private void Awake()
        {
            capture = GetComponent<GestureCapture>();
        }

        private void OnEnable()
        {
            string problem = FindSetupProblem();
            if (problem != null)
            {
                enabled = false;
                throw new InvalidOperationException($"{nameof(GestureRecorder)} on '{name}' cannot record: {problem}");
            }
            capture.Started += OnStarted;
            capture.Sampled += OnSampled;
            capture.Completed += OnCompleted;
        }

        private void OnDisable()
        {
            capture.Started -= OnStarted;
            capture.Sampled -= OnSampled;
            capture.Completed -= OnCompleted;
            recordingGesture = false;
        }

        private void OnStarted(GestureCapture sender)
        {
            recordingGesture = true;
            pressedAtUtc = DateTime.UtcNow;
            headRotation = capture.HeadRotation;
            positions.Clear();
            times.Clear();
        }

        private void OnSampled(GestureCapture sender, Vector3 position, float time)
        {
            if (!recordingGesture)
                return;
            positions.Add(position);
            times.Add(time);
        }

        private void OnCompleted(GestureCapture sender)
        {
            if (!recordingGesture)
                return;
            recordingGesture = false;

            // Label can be edited in Play Mode after OnEnable checked it.
            string problem = FindSetupProblem();
            if (problem != null)
                throw new InvalidOperationException($"{nameof(GestureRecorder)} on '{name}' did not save the gesture: {problem}");

            var recording = new GestureRecording(label, note, hand.ToString(), pressedAtUtc, headRotation,
                positions.ToArray(), times.ToArray());
            string stamp = pressedAtUtc.ToString("yyyyMMdd'T'HHmmss.fff'Z'", CultureInfo.InvariantCulture);
            string path = Path.Combine(GestureRecording.Folder, label, $"{stamp}-{hand}.json");
            recording.Save(path);

            // The capture raises a sample in the frame of the press, so there is always at least one.
            string noteText = note.Length == 0 ? "" : $" ({note})";
            Debug.Log($"Recorded {label}{noteText} with the {hand} hand: {times.Count} frames over " +
                      $"{times[times.Count - 1]:F2} s, {capture.Path.Length:F2} m of path. Saved {path}", this);
        }

        // Why the recorder cannot save recordings, or null if it can.
        private string FindSetupProblem()
        {
            if (hand == Hand.Unassigned)
                return "Hand is not set.";
            if (string.IsNullOrWhiteSpace(label))
                return "Label is empty.";
            // Windows drops spaces and dots from the end of a folder name, so the folder would not match
            // the label. The dot check also rejects . and .., which would save outside the label's folder.
            if (label != label.Trim() || label.EndsWith("."))
                return $"Label '{label}' has spaces at its start or end, or ends with a dot.";
            if (label.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return $"Label '{label}' has a character that a folder name cannot have.";
            return null;
        }
    }
}
