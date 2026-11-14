using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneMotion.Gestures.Tests
{
    public class GestureRecordingTests
    {
        private static readonly DateTime RecordedAt =
            new DateTime(2026, 10, 13, 18, 45, 1, DateTimeKind.Utc).AddTicks(1234567);

        private readonly TestAssets assets = new TestAssets();
        private string folder;

        [SetUp]
        public void ChooseFolder()
        {
            // A new folder for each test, which does not exist until a test writes to it.
            folder = Path.Combine(Path.GetTempPath(), "ArcaneMotion.Gestures.Tests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void DeleteFolder()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
            assets.DestroyAll();
        }

        private static float RandomFloat(System.Random random, float range)
        {
            return (float)((random.NextDouble() * 2.0 - 1.0) * range);
        }

        private static GestureRecording OnePointRecording(string label)
        {
            return new GestureRecording(label, "", "Left", RecordedAt, Quaternion.identity,
                new[] { new Vector3(0.1f, 1.2f, 0.3f) }, new[] { 0f }, default);
        }

        private string WriteFile(string json)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "recording.json");
            File.WriteAllText(path, json);
            return path;
        }

        [Test]
        public void SaveThenLoad_GivesIdenticalData()
        {
            // Random values use every bit of a float, so they come back unchanged only if every digit
            // they need was written. Positions are within meters of the tracking origin, and times
            // within a minute of the press.
            var random = new System.Random(1245);
            var positions = new Vector3[300];
            var times = new float[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = new Vector3(RandomFloat(random, 3f), RandomFloat(random, 3f), RandomFloat(random, 3f));
                times[i] = Mathf.Abs(RandomFloat(random, 60f));
            }
            // Values that are short in decimal but not in binary, and tiny ones.
            positions[0] = new Vector3(0.1f, 1f / 3f, -2f / 3f);
            positions[1] = new Vector3(1.234567e-7f, -4.2e-12f, 0f);
            var headRotation = new Quaternion(
                RandomFloat(random, 1f), RandomFloat(random, 1f), RandomFloat(random, 1f), RandomFloat(random, 1f));
            float score = Mathf.Abs(RandomFloat(random, 1f));
            float size = Mathf.Abs(RandomFloat(random, 0.5f));
            GestureShape shape = assets.Shape("Line Right", false, false, Vector3.left, Vector3.right);
            var shown = new RecognitionState(RecognitionStatus.Spell, shape,
                assets.Spell("PH Line Right", shape, 0.03f, 0.6f), score, size, 0f, 0.4f, 0.1f, 0.4f);

            // The note has characters that JSON must escape.
            var saved = new GestureRecording("Line Right", "sloppy, \"fast\"", "Right", RecordedAt, headRotation,
                positions, times, shown);
            string path = Path.Combine(folder, "Line Right", "take.json");
            saved.Save(path);
            GestureRecording loaded = GestureRecording.Load(path);

            Assert.That(loaded.Label, Is.EqualTo("Line Right"));
            Assert.That(loaded.Note, Is.EqualTo("sloppy, \"fast\""));
            Assert.That(loaded.Hand, Is.EqualTo("Right"));
            Assert.That(loaded.RecordedAtUtc, Is.EqualTo("2026-10-13T18:45:01.1234567Z"));
            // Exact comparisons: NUnit compares floats, and Vector3 and Quaternion through Equals,
            // with no tolerance.
            Assert.That(loaded.HeadRotation, Is.EqualTo(headRotation));
            Assert.That(loaded.Positions, Is.EqualTo(positions));
            Assert.That(loaded.Times, Is.EqualTo(times));
            Assert.That(loaded.Shown.Status, Is.EqualTo("Spell"));
            Assert.That(loaded.Shown.Shape, Is.EqualTo("Line Right"));
            Assert.That(loaded.Shown.Spell, Is.EqualTo("PH Line Right"));
            Assert.That(loaded.Shown.Score, Is.EqualTo(score));
            Assert.That(loaded.Shown.Size, Is.EqualTo(size));
        }

        // Before the matcher first runs, or when no shape scores under the cutoff, there is no shape
        // and the score is infinity.
        [Test]
        public void SaveThenLoad_KeepsAShownStateWithNoShape()
        {
            var shown = new RecognitionState(RecognitionStatus.None, null, null, float.PositiveInfinity, 0f, 0f, 0f,
                0f, 0f);
            var saved = new GestureRecording("None", "", "Left", RecordedAt, Quaternion.identity,
                new[] { new Vector3(0.1f, 1.2f, 0.3f) }, new[] { 0f }, shown);
            string path = Path.Combine(folder, "take.json");
            saved.Save(path);

            GestureRecording loaded = GestureRecording.Load(path);

            Assert.That(loaded.Shown.Status, Is.EqualTo("None"));
            Assert.That(loaded.Shown.Shape, Is.Empty);
            Assert.That(loaded.Shown.Spell, Is.Empty);
            Assert.That(loaded.Shown.Score, Is.EqualTo(float.PositiveInfinity));
        }

        // As the recordings saved before they held the state on show.
        [Test]
        public void Load_RecordingWithoutAShownState_LoadsItEmpty()
        {
            string path = WriteFile("{\"formatVersion\":1,\"label\":\"Circle\",\"note\":\"Clean\",\"hand\":\"Right\"," +
                                    "\"recordedAtUtc\":\"2026-10-21T20:50:17.7921710Z\"," +
                                    "\"headRotation\":{\"x\":0.0,\"y\":0.0,\"z\":0.0,\"w\":1.0}," +
                                    "\"positions\":[{\"x\":0.1,\"y\":1.2,\"z\":0.3}],\"times\":[0.0]}");

            GestureRecording loaded = GestureRecording.Load(path);

            Assert.That(loaded.Shown.Status, Is.Empty);
            Assert.That(loaded.Shown.Shape, Is.Empty);
            Assert.That(loaded.Shown.Spell, Is.Empty);
        }

        [Test]
        public void Save_DoesNotReplaceAnExistingFile()
        {
            string path = Path.Combine(folder, "take.json");
            OnePointRecording("Circle").Save(path);

            Assert.Throws<IOException>(() => OnePointRecording("Triangle").Save(path));
            Assert.That(GestureRecording.Load(path).Label, Is.EqualTo("Circle"));
        }

        // A later version, and no version at all, as in a JSON file that is not a recording.
        [TestCase("{\"formatVersion\":2,\"label\":\"Circle\",\"note\":\"\",\"hand\":\"Left\",\"positions\":[],\"times\":[]}", 2)]
        [TestCase("{\"label\":\"Circle\",\"note\":\"\",\"hand\":\"Left\",\"positions\":[],\"times\":[]}", 0)]
        public void Load_ThrowsOnAnyOtherFormatVersion(string json, int version)
        {
            string path = WriteFile(json);

            var exception = Assert.Throws<InvalidDataException>(() => GestureRecording.Load(path));
            Assert.That(exception.Message, Does.Contain($"format version {version}"));
        }

        [TestCase("{\"formatVersion\":1,\"note\":\"\",\"hand\":\"Left\",\"positions\":[],\"times\":[]}", "no label")]
        [TestCase("{\"formatVersion\":1,\"label\":\"Circle\",\"note\":\"\",\"hand\":\"Left\"," +
                  "\"positions\":[{\"x\":0.1,\"y\":1.2,\"z\":0.3}],\"times\":[]}", "1 position but 0 times")]
        // Without the check, the zero quaternion would replay the gesture facing +z, without an error.
        [TestCase("{\"formatVersion\":1,\"label\":\"Circle\",\"note\":\"\",\"hand\":\"Left\"," +
                  "\"recordedAtUtc\":\"2026-10-13T18:45:01.0000000Z\",\"positions\":[],\"times\":[]}", "no head rotation")]
        public void Load_ThrowsOnARecordingThatCannotBeReplayed(string json, string problem)
        {
            string path = WriteFile(json);

            var exception = Assert.Throws<InvalidDataException>(() => GestureRecording.Load(path));
            Assert.That(exception.Message, Does.Contain(problem));
        }
    }
}
