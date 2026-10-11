using System;
using UnityEngine;

namespace ArcaneMotion.Gestures
{
    // Removes position, facing, and size from resampled points, so that gestures and templates
    // compare by shape alone, and defines the gesture frame they are compared in.
    public static class GestureNormalization
    {
        // Points closer than this to their centroid, as an RMS distance in meters, have no shape.
        private const float MinSize = 1e-6f;
        private const float MinFlatForwardLength = 0.01f;

        // In place, moves the points so that their centroid is at the origin, turns them by
        // rotation, and divides them by their RMS distance from the centroid. size is that
        // distance in meters. Returns false, leaving the points unchanged, when size is below 1e-6 m.
        public static bool Normalize(Vector3[] points, Quaternion rotation, out float size)
        {
            if (points == null || points.Length == 0)
                throw new ArgumentException("Normalizing needs at least one point.", nameof(points));

            // Summed in double to avoid floating point rounding errors.
            double sumX = 0, sumY = 0, sumZ = 0;
            for (int i = 0; i < points.Length; i++)
            {
                sumX += points[i].x;
                sumY += points[i].y;
                sumZ += points[i].z;
            }
            var centroid = new Vector3(
                (float)(sumX / points.Length), (float)(sumY / points.Length), (float)(sumZ / points.Length));

            // A second pass, rather than mean square minus squared mean, so that small shapes far
            // from the tracking origin do not lose their size to cancellation.
            float sumOfSquares = 0f;
            for (int i = 0; i < points.Length; i++)
                sumOfSquares += (points[i] - centroid).sqrMagnitude;
            size = Mathf.Sqrt(sumOfSquares / points.Length);
            if (size < MinSize)
                return false;

            float scale = 1f / size;
            for (int i = 0; i < points.Length; i++)
                points[i] = rotation * (points[i] - centroid) * scale;
            return true;
        }

        // The rotation from tracking space into the gesture frame: tracking space turned about
        // the vertical axis so that the direction the head faces is +z. In the gesture frame, x
        // is the player's right, y is up, and z points away from the player.
        public static Quaternion GestureFrameRotation(Quaternion headRotation)
        {
            Vector3 forward = headRotation * Vector3.forward;
            var facing = new Vector3(forward.x, 0f, forward.z);
            if (facing.magnitude < MinFlatForwardLength)
            {
                // Looking straight down, the top of the head points where the face is turned;
                // looking straight up, it points the opposite way.
                Vector3 up = headRotation * Vector3.up * -Mathf.Sign(forward.y);
                facing = new Vector3(up.x, 0f, up.z);
            }
            return Quaternion.Inverse(Quaternion.LookRotation(facing));
        }
    }
}
