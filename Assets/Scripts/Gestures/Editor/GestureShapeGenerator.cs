using System;
using UnityEngine;

namespace ArcaneMotion.Gestures.Editor
{
    // The plane that a generated shape lies in, in the gesture frame, and how it is seen.
    public enum ShapePlane
    {
        // Upright, facing the player, as the player sees it.
        Facing,
        // Flat like a tabletop, seen from above, with away from the player at the top.
        Horizontal,
        // Upright and edge-on to the player, seen from the player's right, with away from the
        // player on the right.
        Side,
    }

    public enum ShapeDirection
    {
        Clockwise,
        Counterclockwise,
    }

    // Generates the points of a circle, a regular polygon, or a star polygon for a GestureShape.
    public static class GestureShapeGenerator
    {
        // Enough corners that the polyline stays within 0.01% of the radius of the true circle.
        public const int CirclePointCount = 256;

        // Why these settings cannot generate a shape, or null if they can.
        public static string FindProblem(int sides, bool star, float radius)
        {
            if (sides != 0 && sides < 3)
                return "Sides must be 0 for a circle, or at least 3.";
            // Joining every second corner reaches every corner in one stroke only when the count is odd.
            if (star && (sides < 5 || sides % 2 == 0))
                return "A star needs an odd number of sides, 5 or more.";
            // Written as a negation so that NaN fails it too.
            if (!(radius > 0f))
                return "Radius must be above zero.";
            return null;
        }

        // The corners of a regular polygon with this many sides, or of a circle for 0 sides, all
        // radius meters from the center, in drawing order from the corner at startAngle. A star
        // joins every second corner instead of each next one. Angles are in degrees counterclockwise
        // from the plane's right as the plane is seen, so 90 is the top. Throws if FindProblem finds
        // a problem.
        public static Vector3[] Generate(ShapePlane plane, int sides, bool star, float radius, float startAngle,
            ShapeDirection direction)
        {
            string problem = FindProblem(sides, star, radius);
            if (problem != null)
                throw new ArgumentException(problem);

            int count = sides == 0 ? CirclePointCount : sides;
            float step = (star ? 720f : 360f) / count * (direction == ShapeDirection.Clockwise ? -1f : 1f);
            var points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float angle = (startAngle + step * i) * Mathf.Deg2Rad;
                points[i] = InPlane(plane, radius * Mathf.Cos(angle), radius * Mathf.Sin(angle));
            }
            return points;
        }

        // The gesture frame point at across and up, as the plane is seen.
        private static Vector3 InPlane(ShapePlane plane, float across, float up)
        {
            return plane switch
            {
                ShapePlane.Facing => new Vector3(across, up, 0f),
                ShapePlane.Horizontal => new Vector3(across, 0f, up),
                ShapePlane.Side => new Vector3(0f, up, across),
                _ => throw new ArgumentOutOfRangeException(nameof(plane), plane, null),
            };
        }
    }
}
