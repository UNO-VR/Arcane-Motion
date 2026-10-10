using NUnit.Framework;
using UnityEngine;

namespace ArcaneMotion.Gestures.Tests
{
    internal static class VectorAssert
    {
        // Fails when the vectors are more than tolerance apart, or when either holds NaN. NUnit's
        // Is.LessThanOrEqualTo would let a NaN distance pass, because it orders NaN below every number.
        public static void AreNear(Vector3 expected, Vector3 actual, float tolerance, string message)
        {
            float distance = Vector3.Distance(expected, actual);
            if (!(distance <= tolerance))
                Assert.Fail($"{message}: expected {expected.ToString("F6")}, but got {actual.ToString("F6")}, " +
                            $"which is {distance} away; the tolerance is {tolerance}.");
        }
    }
}
