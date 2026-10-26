using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace ArcaneMotion.Gestures.Tests
{
    // Gestures built as the synthetic checks in project-overview.md, Findings, built them: in the
    // gesture frame, with a radius of 1.
    internal static class SyntheticGestures
    {
        public enum Kind
        {
            Circle,
            Triangle,
            Diamond,
            Line,
            V,
        }

        // The circle's point u turns along it, from the top and clockwise as the player sees it.
        public static Vector3 CirclePoint(double u)
        {
            double angle = Math.PI / 2 - 2 * Math.PI * u;
            return new Vector3((float)Math.Cos(angle), (float)Math.Sin(angle), 0f);
        }

        // The regular polygon's point u turns along its perimeter. Its corners are the circle's
        // points at each 1 / sides of a turn, joined by straight sides.
        public static Vector3 PolygonPoint(int sides, double u)
        {
            double along = (u - Math.Floor(u)) * sides;
            int side = (int)along;
            return Vector3.LerpUnclamped(CirclePoint((double)side / sides), CirclePoint((double)(side + 1) / sides),
                (float)(along - side));
        }

        // The polygon's corners, as a shape's points: from the top, clockwise.
        public static Vector3[] Corners(int sides)
        {
            var corners = new Vector3[sides];
            for (int j = 0; j < sides; j++)
                corners[j] = CirclePoint((double)j / sides);
            return corners;
        }

        // count points at evenly spaced values of u from from to to, both ends included.
        public static Vector3[] Sample(Func<double, Vector3> pointAt, double from, double to, int count)
        {
            var points = new Vector3[count];
            for (int i = 0; i < count; i++)
                points[i] = pointAt(from + (to - from) * i / (count - 1));
            return points;
        }

        // A sloppy gesture, drawn as the checks drew one. A closed shape starts within 15 degrees of
        // its set start and overshoots it by up to 8%. A line goes right or left at random, and the V
        // is the triangle's first two sides. All of them are ovals, with a slow wobble on each axis,
        // jitter, and a tilt; most start with a straight wind-up into the start point, and all end
        // with a twitch at the release.
        public static Vector3[] Sloppy(Kind kind, Random random)
        {
            const int count = 600;
            Vector3[] shape;
            switch (kind)
            {
                case Kind.Line:
                    float direction = random.NextDouble() < 0.5 ? 1f : -1f;
                    shape = Sample(u => new Vector3(direction * (float)(2 * u - 1), 0f, 0f), 0, 1, count);
                    break;
                case Kind.V:
                    shape = Sample(u => PolygonPoint(3, u), 0, 2.0 / 3, count);
                    break;
                default:
                    double start = Uniform(random, -15, 15) / 360;
                    double overshoot = Uniform(random, 0, 0.08);
                    shape = Sample(u => ClosedPoint(kind, start + u), 0, 1 + overshoot, count);
                    break;
            }

            float oval = (float)Uniform(random, 0.8, 1);
            for (int i = 0; i < count; i++)
                shape[i].y *= oval;
            for (int axis = 0; axis < 3; axis++)
            {
                for (int wave = 0; wave < 2; wave++)
                {
                    double amplitude = Uniform(random, 0, 0.04);
                    double cycles = Uniform(random, 0.5, 2.5);
                    double phase = random.NextDouble();
                    for (int i = 0; i < count; i++)
                        shape[i][axis] += (float)(amplitude * Math.Sin(2 * Math.PI * (cycles * i / (count - 1) + phase)));
                }
            }
            Quaternion tilt = Quaternion.AngleAxis((float)Uniform(random, -10, 10), Vector3.up) *
                              Quaternion.AngleAxis((float)Uniform(random, -15, 15), Vector3.right);
            float length = 0f;
            for (int i = 0; i < count; i++)
            {
                var jitter = new Vector3((float)Normal(random), (float)Normal(random), (float)Normal(random));
                shape[i] = tilt * (shape[i] + 0.004f * jitter);
                if (i > 0)
                    length += Vector3.Distance(shape[i - 1], shape[i]);
            }

            var points = new List<Vector3>();
            if (random.NextDouble() < 0.6)
            {
                Vector3 windUp = (float)Uniform(random, 0.05, 0.25) * length * Direction(random);
                for (int i = 0; i < 60; i++)
                    points.Add(shape[0] + (1f - i / 60f) * windUp);
            }
            points.AddRange(shape);
            Vector3 twitch = (float)Uniform(random, 0, 0.03) * length * Direction(random);
            for (int i = 1; i <= 9; i++)
                points.Add(shape[count - 1] + i / 9f * twitch);
            return points.ToArray();
        }

        private static Vector3 ClosedPoint(Kind kind, double u)
        {
            return kind switch
            {
                Kind.Circle => CirclePoint(u),
                Kind.Triangle => PolygonPoint(3, u),
                Kind.Diamond => PolygonPoint(4, u),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a closed shape."),
            };
        }

        private static double Uniform(Random random, double min, double max)
        {
            return min + (max - min) * random.NextDouble();
        }

        // A standard normal value, by the Box-Muller transform. 1 - NextDouble() is above zero, so
        // its logarithm is finite.
        private static double Normal(Random random)
        {
            return Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());
        }

        // A direction uniformly at random.
        private static Vector3 Direction(Random random)
        {
            return new Vector3((float)Normal(random), (float)Normal(random), (float)Normal(random)).normalized;
        }
    }
}
