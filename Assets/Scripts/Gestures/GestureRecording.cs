using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ArcaneMotion.Gestures
{
    // One gesture as GestureCapture captured it, saved as JSON so that tests and tools can replay it
    // through a new session. It keeps every frame's sample, including those the session did not
    // store, so that a replay applies the library's spacing rule itself.
    [Serializable]
    public sealed class GestureRecording
    {
        public const int FormatVersion = 1;

        // Saved by JsonUtility under these names. They have no initializers, so that a key missing
        // from a file loads as 0 or null and fails the checks in Load.
        [SerializeField] private int formatVersion;
        [SerializeField] private string label;
        [SerializeField] private string note;
        [SerializeField] private string hand;
        [SerializeField] private string recordedAtUtc;
        [SerializeField] private Quaternion headRotation;
        [SerializeField] private Vector3[] positions;
        [SerializeField] private float[] times;

        public GestureRecording(string label, string note, string hand, DateTime recordedAtUtc,
            Quaternion headRotation, Vector3[] positions, float[] times)
        {
            if (recordedAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("The time must be in UTC.", nameof(recordedAtUtc));

            formatVersion = FormatVersion;
            this.label = label;
            this.note = note ?? throw new ArgumentNullException(nameof(note));
            this.hand = hand ?? throw new ArgumentNullException(nameof(hand));
            this.recordedAtUtc = recordedAtUtc.ToString("o", CultureInfo.InvariantCulture);
            this.headRotation = headRotation;
            this.positions = positions;
            this.times = times;

            string problem = FindProblem();
            if (problem != null)
                throw new ArgumentException($"The recording is not valid: {problem}");
        }

        // For Load, which fills the fields from JSON.
        private GestureRecording()
        {
        }

        // The folder that holds recordings, GestureRecordings/ at the project root, outside Assets/
        // so that Unity does not import them. Each label has a folder in it.
        public static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "GestureRecordings"));

        // The shape the player meant to draw, as its shape asset is named, or None for motion that
        // should not match.
        public string Label => label;

        // Free text about the take, such as clean, sloppy, or wind-up. It can be empty.
        public string Note => note;

        // The hand that drew the gesture, Left or Right.
        public string Hand => hand;

        // When the button was pressed, in ISO 8601 round-trip format.
        public string RecordedAtUtc => recordedAtUtc;

        // The head's rotation in tracking space at the press.
        public Quaternion HeadRotation => headRotation;

        // The controller's position in tracking space, in meters, each frame while the button was held.
        public IReadOnlyList<Vector3> Positions => positions;

        // Each position's seconds since the press.
        public IReadOnlyList<float> Times => times;

        // Writes the recording to path as JSON, creating its folder. Throws instead of replacing an
        // existing file.
        public void Save(string path)
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using var stream = new FileStream(fullPath, FileMode.CreateNew);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(JsonUtility.ToJson(this));
            writer.Write('\n');
        }

        public static GestureRecording Load(string path)
        {
            // Filled in place, so that keys missing from the file leave the fields at their defaults.
            var recording = new GestureRecording();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(path), recording);

            if (recording.formatVersion != FormatVersion)
                throw new InvalidDataException(
                    $"'{path}' has format version {recording.formatVersion}, but only version {FormatVersion} can be loaded.");
            string problem = recording.FindProblem();
            if (problem != null)
                throw new InvalidDataException($"'{path}' is not a valid recording: {problem}");
            return recording;
        }

        // Why the recording cannot be replayed or sorted by its label, or null if it can.
        private string FindProblem()
        {
            if (string.IsNullOrWhiteSpace(label))
                return "it has no label.";
            if (positions == null || times == null)
                return "it has no positions or no times.";
            if (positions.Length != times.Length)
                return $"it has {positions.Length} {(positions.Length == 1 ? "position" : "positions")} " +
                       $"but {times.Length} {(times.Length == 1 ? "time" : "times")}.";
            if (note == null)
                return "it has no note.";
            if (hand == null)
                return "it has no hand.";
            if (recordedAtUtc == null)
                return "it has no recording time.";
            // Equals, because Unity's == compares by dot product and never finds the zero quaternion equal.
            if (headRotation.Equals(default(Quaternion)))
                return "it has no head rotation.";
            return null;
        }
    }
}
