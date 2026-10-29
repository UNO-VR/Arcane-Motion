using System;
using UnityEngine;

namespace ArcaneMotion.Gestures
{
    // A gesture's stored points in drawing order, each with its time and the path length from
    // the first point to it. Positions are in meters and times in seconds. The arrays grow by
    // doubling and are kept when the path is cleared, so a path reused between gestures stops
    // allocating once it has held its longest gesture.
    public sealed class GesturePath
    {
        private const int InitialCapacity = 256;

        private Vector3[] positions = new Vector3[InitialCapacity];
        private float[] times = new float[InitialCapacity];
        // The running total of path length at each point; it never decreases.
        private float[] lengths = new float[InitialCapacity];

        public int Count { get; private set; }

        // The path length from the first point to the last, or 0 for an empty path.
        public float Length => Count == 0 ? 0f : lengths[Count - 1];

        public Vector3 PositionAt(int index)
        {
            CheckIndex(index);
            return positions[index];
        }

        public float TimeAt(int index)
        {
            CheckIndex(index);
            return times[index];
        }

        // The path length from the first point to this one.
        public float LengthAt(int index)
        {
            CheckIndex(index);
            return lengths[index];
        }

        // The path length at the given time, interpolating linearly between the stored points around
        // it: 0 before the first point, and Length after the last. The times must not decrease.
        public float LengthAtTime(float time)
        {
            if (Count == 0 || time <= times[0])
                return 0f;
            if (time >= times[Count - 1])
                return Length;

            // The last point at or before the time. The next point is after it, so the division is safe.
            int low = 0;
            int high = Count - 2;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (times[middle] <= time)
                    low = middle;
                else
                    high = middle - 1;
            }
            float length = Mathf.Lerp(lengths[low], lengths[low + 1], (time - times[low]) / (times[low + 1] - times[low]));
            // Rounding in the interpolation must not reach past the segment, and past the path at its end.
            return Mathf.Min(length, lengths[low + 1]);
        }

        public void Clear()
        {
            Count = 0;
        }

        public void Add(Vector3 position, float time)
        {
            if (Count == positions.Length)
                Grow();

            lengths[Count] = Count == 0 ? 0f : lengths[Count - 1] + Vector3.Distance(positions[Count - 1], position);
            positions[Count] = position;
            times[Count] = time;
            Count++;
        }

        // Writes destination.Length points spaced evenly by path length from fromLength to
        // toLength, both ends included, interpolating linearly between stored points.
        public void Resample(float fromLength, float toLength, Vector3[] destination)
        {
            if (Count < 2)
                throw new InvalidOperationException($"Resampling needs at least 2 stored points, but the path has {Count}.");
            if (destination == null || destination.Length < 2)
                throw new ArgumentException("The destination must hold at least 2 points.", nameof(destination));
            // Written as a negation so that NaN fails it too.
            if (!(0f <= fromLength && fromLength <= toLength && toLength <= Length))
                throw new ArgumentOutOfRangeException(nameof(fromLength),
                    $"The range {fromLength} to {toLength} is not within the path, which runs from 0 to {Length}.");

            // N points gives N-1 segments; N-2 is the index of the last segment
            int lastSegment = Count - 2;
            int segment = FindSegment(fromLength, lastSegment);
            int lastPoint = destination.Length - 1;
            float step = (toLength - fromLength) / lastPoint;

            for (int k = 0; k <= lastPoint; k++)
            {
                // The last point takes toLength itself, so rounding in the steps cannot move the end.
                float target = k == lastPoint ? toLength : Mathf.Min(fromLength + step * k, toLength);

                // Walk forward past every segment that ends at or before the target. This also
                // passes over zero-length segments, which would otherwise divide by zero.
                while (segment < lastSegment && lengths[segment + 1] <= target)
                    segment++;

                float segmentStart = lengths[segment];
                float segmentLength = lengths[segment + 1] - segmentStart;
                // Only the last segment can still be zero-length here, and then the target is
                // the end of the path.
                destination[k] = segmentLength > 0f
                    ? Vector3.LerpUnclamped(positions[segment], positions[segment + 1], (target - segmentStart) / segmentLength)
                    : positions[segment + 1];
            }
        }

        // The last segment, up to lastSegment, that starts at or before the given path length.
        private int FindSegment(float length, int lastSegment)
        {
            int low = 0;
            int high = lastSegment;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (lengths[middle] <= length)
                    low = middle;
                else
                    high = middle - 1;
            }
            return low;
        }

        private void CheckIndex(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"Index {index} is outside the path, which has {Count} {(Count == 1 ? "point" : "points")}.");
        }

        private void Grow()
        {
            int capacity = positions.Length * 2;
            Array.Resize(ref positions, capacity);
            Array.Resize(ref times, capacity);
            Array.Resize(ref lengths, capacity);
        }
    }
}
