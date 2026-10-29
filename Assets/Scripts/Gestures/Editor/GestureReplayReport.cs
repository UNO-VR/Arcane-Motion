using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ArcaneMotion.Gestures.Editor
{
    // Tools > Arcane Motion > Replay Gesture Recordings: replays every recording against the selected
    // library, as the headset would have recognized it with that library. It logs a summary for each
    // label and every miss, and writes one row per recording to Logs/gesture-replay.csv.
    //
    // A take is correct when release would cast a spell of the shape named by its label. A take
    // whose label names no shape in the library, such as None, is correct when nothing is lit at
    // release.
    public static class GestureReplayReport
    {
        private const string MenuPath = "Tools/Arcane Motion/Replay Gesture Recordings";
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        [MenuItem(MenuPath)]
        private static void ReplayWithSelectedLibrary()
        {
            var library = (GestureLibrary)Selection.activeObject;
            string csvPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "gesture-replay.csv"));
            Debug.Log(Run(library, GestureRecording.Folder, csvPath), library);
        }

        [MenuItem(MenuPath, true)]
        private static bool SelectionIsALibrary()
        {
            return Selection.activeObject is GestureLibrary;
        }

        // Replays every recording under folder against the library, writes the rows to csvPath, and
        // returns the summary. Throws if the library has problems or the folder has no recordings.
        public static string Run(GestureLibrary library, string folder, string csvPath)
        {
            if (library == null)
                throw new ArgumentNullException(nameof(library));
            string[] files = Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories);
            if (files.Length == 0)
                throw new InvalidOperationException($"There are no recordings in {folder}.");
            Array.Sort(files, StringComparer.Ordinal);

            // With an infinite margin the matcher's cutoff never stops it, so every shape's score is
            // exact, not only those that could change a decision.
            GestureLibrary scoringLibrary = Object.Instantiate(library);
            var rows = new List<Row>();
            try
            {
                scoringLibrary.MinMargin = float.PositiveInfinity;
                var scorer = new GestureMatcher(scoringLibrary);
                foreach (string file in files)
                    rows.Add(Replay(file, folder, library, scorer));
            }
            finally
            {
                Object.DestroyImmediate(scoringLibrary);
            }

            WriteCsv(rows, csvPath);
            return Summarize(library, rows, csvPath);
        }

        private sealed class Row
        {
            public string File;
            public GestureRecording Recording;
            // The label's shape, or null if no shape in the library has the label's name.
            public GestureShape Expected;
            public RecognitionState Shown;
            public bool Correct;
            // Each state that Advance reported as a change, in order.
            public readonly List<RecognitionState> Changes = new List<RecognitionState>();
            // From the exact scoring of the part that the session last evaluated. Without an expected
            // shape, the label's score is infinity and the other shape is the best of all.
            public float LabelScore = float.PositiveInfinity;
            public float LabelSize;
            public float LabelStartCut;
            public float LabelEndCut;
            public GestureShape Other;
            public float OtherScore = float.PositiveInfinity;

            public float Margin => OtherScore - LabelScore;
        }

        private static Row Replay(string file, string folder, GestureLibrary library, GestureMatcher scorer)
        {
            var row = new Row
            {
                File = Path.GetRelativePath(folder, file).Replace('\\', '/'),
                Recording = GestureRecording.Load(file),
            };
            GestureShape[] shapes = scorer.Templates.Shapes;
            row.Expected = shapes.FirstOrDefault(shape => shape.name == row.Recording.Label);

            GestureSession session = GestureReplay.Run(row.Recording, library, row.Changes.Add);
            row.Shown = session.State;
            row.Correct = row.Expected != null
                ? row.Shown.Status == RecognitionStatus.Spell && row.Shown.Shape == row.Expected
                : row.Shown.Status == RecognitionStatus.None;

            // A session whose matcher never ran has no evaluated part to score.
            if (session.MatchCount == 0)
                return row;
            scorer.Reset();
            scorer.Match(session.Path, row.Shown.EvaluatedStart, row.Shown.EvaluatedEnd,
                GestureNormalization.GestureFrameRotation(session.HeadRotation));
            MatchResult result = scorer.Result;
            for (int s = 0; s < shapes.Length; s++)
            {
                if (shapes[s] == row.Expected)
                {
                    row.LabelScore = result.Score(s);
                    row.LabelSize = result.Size(s);
                    row.LabelStartCut = result.StartCut(s);
                    row.LabelEndCut = result.EndCut(s);
                }
                else if (result.Score(s) < row.OtherScore)
                {
                    row.Other = shapes[s];
                    row.OtherScore = result.Score(s);
                }
            }
            return row;
        }

        private static void WriteCsv(List<Row> rows, string csvPath)
        {
            var csv = new StringBuilder();
            csv.AppendLine("file,label,note,hand,expected,shown,correct,shown score,shown size,label score,label size," +
                           "label start cut,label end cut,other shape,other score,margin,lit during the gesture");
            foreach (Row row in rows)
            {
                bool expected = row.Expected != null;
                string[] fields =
                {
                    row.File, row.Recording.Label, row.Recording.Note, row.Recording.Hand,
                    expected ? row.Expected.name : "None",
                    Describe(row.Shown), row.Correct ? "yes" : "no",
                    Number(row.Shown.Score), row.Shown.Shape != null ? Number(row.Shown.Size) : "",
                    expected ? Number(row.LabelScore) : "", expected ? Number(row.LabelSize) : "",
                    expected ? Number(row.LabelStartCut) : "", expected ? Number(row.LabelEndCut) : "",
                    row.Other != null ? row.Other.name : "", Number(row.OtherScore),
                    expected ? Number(row.Margin) : "",
                    string.Join(" > ", row.Changes.Select(Describe)),
                };
                csv.AppendLine(string.Join(",", fields.Select(CsvField)));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(csvPath));
            File.WriteAllText(csvPath, csv.ToString(), new UTF8Encoding(false));
        }

        private static string Summarize(GestureLibrary library, List<Row> rows, string csvPath)
        {
            var summary = new StringBuilder();
            summary.AppendLine($"Replayed {rows.Count} {(rows.Count == 1 ? "recording" : "recordings")} against " +
                               $"'{library.name}': {rows.Count(row => row.Correct)} correct. Rows are in {csvPath}.");

            foreach (IGrouping<string, Row> group in rows.GroupBy(row => row.Recording.Label).OrderBy(g => g.Key))
            {
                List<Row> takes = group.ToList();
                GestureShape expected = takes[0].Expected;
                int correct = takes.Count(row => row.Correct);
                summary.AppendLine();
                summary.Append($"{group.Key} ({takes.Count} {(takes.Count == 1 ? "take" : "takes")}): {correct} ");
                summary.AppendLine(expected != null ? $"cast a {expected.name} spell." : "unlit at release.");
                List<Row> misses = takes.Where(row => !row.Correct).ToList();
                if (misses.Count > 0)
                    summary.AppendLine("  Shown on the misses: " + Tally(misses.Select(row => Describe(row.Shown))) + ".");

                if (expected != null)
                {
                    List<float> scores = takes.Select(row => row.LabelScore).ToList();
                    summary.AppendLine($"  Score against {expected.name}: median {Number(Median(scores))}, " +
                                       $"worst {Number(scores.Max())}.");
                    Row closest = takes.OrderBy(row => row.Margin).First();
                    summary.AppendLine($"  Margin over the next shape: smallest {Number(closest.Margin)} " +
                                       $"({(closest.Other != null ? closest.Other.name : "none")}), median " +
                                       $"{Number(Median(takes.Select(row => row.Margin).ToList()))}.");
                    List<float> sizes = takes.Where(row => !float.IsPositiveInfinity(row.LabelScore))
                        .Select(row => row.LabelSize).ToList();
                    if (sizes.Count > 0)
                        summary.AppendLine($"  Size: {Number(sizes.Min())} to {Number(sizes.Max())} m, median " +
                                           $"{Number(Median(sizes))}.");
                    summary.AppendLine($"  Cuts: start median {Percent(Median(takes.Select(row => row.LabelStartCut).ToList()))}, " +
                                       $"largest {Percent(takes.Max(row => row.LabelStartCut))}; end median " +
                                       $"{Percent(Median(takes.Select(row => row.LabelEndCut).ToList()))}, largest " +
                                       $"{Percent(takes.Max(row => row.LabelEndCut))}.");
                }
                else
                {
                    List<float> scores = takes.Select(row => row.OtherScore).ToList();
                    summary.AppendLine($"  Closest shape: median {Number(Median(scores))}, lowest {Number(scores.Min())}.");
                }

                // Another shape lit while drawing is what the ink showed, even if release cast the right spell.
                List<string> others = takes
                    .SelectMany(row => row.Changes
                        .Where(state => state.Status != RecognitionStatus.None && state.Shape != expected)
                        .Select(state => state.Shape.name)
                        .Distinct())
                    .ToList();
                int litOthers = takes.Count(row =>
                    row.Changes.Any(state => state.Status != RecognitionStatus.None && state.Shape != expected));
                if (litOthers > 0)
                    summary.AppendLine($"  Another shape lit during {litOthers} {(litOthers == 1 ? "take" : "takes")}: " +
                                       $"{Tally(others)}.");
            }

            List<Row> allMisses = rows.Where(row => !row.Correct).ToList();
            if (allMisses.Count > 0)
            {
                summary.AppendLine();
                summary.AppendLine("Misses:");
                foreach (Row row in allMisses)
                {
                    string note = string.IsNullOrEmpty(row.Recording.Note) ? "" : $" ({row.Recording.Note})";
                    string detail = row.Expected != null
                        ? $"{row.Expected.name} scored {Number(row.LabelScore)} at size {Number(row.LabelSize)} m"
                        : $"closest shape {(row.Other != null ? row.Other.name : "none")} at {Number(row.OtherScore)}";
                    summary.AppendLine($"  {row.File}{note}: showed {Describe(row.Shown)}; {detail}");
                }
            }
            return summary.ToString();
        }

        private static string Describe(RecognitionState state)
        {
            switch (state.Status)
            {
                case RecognitionStatus.Spell:
                    return state.Spell.name;
                case RecognitionStatus.WrongSize:
                    return $"WrongSize {state.Shape.name}";
                default:
                    return "None";
            }
        }

        // Each distinct value with its count, most common first, such as "None 3, PH Line Right 1".
        private static string Tally(IEnumerable<string> values)
        {
            return string.Join(", ", values.GroupBy(value => value).OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}"));
        }

        private static float Median(List<float> values)
        {
            List<float> sorted = values.OrderBy(value => value).ToList();
            int middle = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2f;
        }

        private static string Number(float value)
        {
            return float.IsInfinity(value) ? "inf" : value.ToString("0.000", Invariant);
        }

        private static string Percent(float fraction)
        {
            return (fraction * 100f).ToString("0.#", Invariant) + "%";
        }

        private static string CsvField(string value)
        {
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
                return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
