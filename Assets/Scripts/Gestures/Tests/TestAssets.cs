using System.Collections.Generic;
using UnityEngine;

namespace ArcaneMotion.Gestures.Tests
{
    // Creates gesture assets in memory for a test, and destroys every one of them afterwards.
    internal sealed class TestAssets
    {
        private readonly List<Object> created = new List<Object>();

        public GestureShape Shape(string name, bool closed, bool bothDirections, params Vector3[] points)
        {
            var shape = Create<GestureShape>(name);
            shape.Closed = closed;
            shape.BothDirections = bothDirections;
            shape.Points = points;
            return shape;
        }

        public SpellGesture Spell(string name, GestureShape shape, float minSize, float maxSize)
        {
            var spell = Create<SpellGesture>(name);
            spell.Shape = shape;
            spell.MinSize = minSize;
            spell.MaxSize = maxSize;
            return spell;
        }

        public GestureLibrary Library(string name, int pointCount, int startShifts, params SpellGesture[] spells)
        {
            var library = Create<GestureLibrary>(name);
            library.PointCount = pointCount;
            library.StartShifts = startShifts;
            library.Spells.AddRange(spells);
            return library;
        }

        public void DestroyAll()
        {
            foreach (Object asset in created)
                Object.DestroyImmediate(asset);
            created.Clear();
        }

        private T Create<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            created.Add(asset);
            return asset;
        }
    }
}
