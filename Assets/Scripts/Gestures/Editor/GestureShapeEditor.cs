using UnityEditor;
using UnityEngine;

namespace ArcaneMotion.Gestures.Editor
{
    // The GestureShape inspector: the shape's fields, a generator for circles, regular polygons, and
    // stars, and front and top views of the points with the start point and the drawing direction.
    [CustomEditor(typeof(GestureShape))]
    public class GestureShapeEditor : UnityEditor.Editor
    {
        private const float ViewSize = 180f;
        private const float ViewMargin = 12f;
        private const int ArrowsPerView = 8;
        private const float ArrowLength = 9f;
        private static readonly Color ViewBackground = new Color(0.13f, 0.13f, 0.13f);
        private static readonly Color InkColor = new Color(0.85f, 0.85f, 0.85f);
        private static readonly Color ArrowColor = new Color(1f, 0.75f, 0.2f);
        private static readonly Color StartColor = new Color(0.3f, 0.9f, 0.4f);

        // The generator's settings belong to this tool, not to the shape, so they are not saved.
        private ShapePlane plane = ShapePlane.Facing;
        private int sides;
        private bool star;
        private float radius = 0.15f;
        private float startAngle = 90f;
        private ShapeDirection direction = ShapeDirection.Clockwise;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var shape = (GestureShape)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generate", EditorStyles.boldLabel);
            plane = (ShapePlane)EditorGUILayout.EnumPopup(new GUIContent("Plane",
                "Facing: upright, facing the player, as the player sees it. Horizontal: flat, seen from above, with " +
                "away from the player at the top. Side: upright and edge-on, seen from the player's right, with away " +
                "from the player on the right."), plane);
            sides = EditorGUILayout.IntField(new GUIContent("Sides", "0 for a circle."), sides);
            star = EditorGUILayout.Toggle(new GUIContent("Star",
                "Join every second corner instead of each next one. 5 sides make a five-pointed star."), star);
            radius = EditorGUILayout.FloatField(new GUIContent("Radius", "Meters from the center to each corner."),
                radius);
            startAngle = EditorGUILayout.FloatField(new GUIContent("Start Angle",
                "Degrees counterclockwise from the plane's right as the plane is seen: 90 is the top, -90 the bottom."),
                startAngle);
            direction = (ShapeDirection)EditorGUILayout.EnumPopup(new GUIContent("Direction",
                "The drawing direction as the plane is seen."), direction);

            string problem = GestureShapeGenerator.FindProblem(sides, star, radius);
            if (problem != null)
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
            using (new EditorGUI.DisabledScope(problem != null))
            {
                if (GUILayout.Button("Generate Points"))
                {
                    Undo.RecordObject(shape, "Generate Gesture Shape Points");
                    shape.Points = GestureShapeGenerator.Generate(plane, sides, star, radius, startAngle, direction);
                    shape.Closed = true;
                    EditorUtility.SetDirty(shape);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Views", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Green dot: the start. Arrows: the drawing direction.", EditorStyles.miniLabel);
            Rect row = GUILayoutUtility.GetRect(0f, ViewSize + EditorGUIUtility.singleLineHeight,
                GUILayout.ExpandWidth(true));
            float width = Mathf.Min(ViewSize, (row.width - ViewMargin) / 2f);
            DrawView(new Rect(row.x, row.y, width, row.height), "Front, as the player sees it", shape, false);
            DrawView(new Rect(row.x + width + ViewMargin, row.y, width, row.height), "Top, away at the top", shape,
                true);
        }

        // Draws the points as seen from the front (x right, y up) or from above (x right, z up), at
        // the same scale in both views.
        private static void DrawView(Rect rect, string label, GestureShape shape, bool fromAbove)
        {
            var labelRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            GUI.Label(labelRect, label, EditorStyles.miniLabel);
            if (Event.current.type != EventType.Repaint)
                return;

            var area = new Rect(rect.x, labelRect.yMax, rect.width, rect.width);
            EditorGUI.DrawRect(area, ViewBackground);
            Vector3[] points = shape.Points;
            if (points == null || points.Length == 0)
                return;

            // Centered on the points' bounds, and scaled so that their largest extent fills the view.
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 point in points)
                bounds.Encapsulate(point);
            float extent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            float scale = extent > 0f ? (area.width / 2f - ViewMargin) / extent : 0f;
            Vector2 center = area.center;

            int count = points.Length + (shape.Closed && points.Length > 1 ? 1 : 0);
            var screen = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = points[i % points.Length] - bounds.center;
                // GUI y points down.
                screen[i] = new Vector3(center.x + offset.x * scale,
                    center.y - (fromAbove ? offset.z : offset.y) * scale, 0f);
            }

            Handles.color = InkColor;
            Handles.DrawAAPolyLine(2f, screen);

            Handles.color = ArrowColor;
            int segments = count - 1;
            int every = Mathf.Max(1, segments / ArrowsPerView);
            for (int i = 0; i < segments; i += every)
                DrawArrow(screen[i], screen[i + 1]);

            Handles.color = StartColor;
            Handles.DrawSolidDisc(screen[0], Vector3.forward, 4f);
        }

        // An arrowhead at the middle of the segment, pointing along it. A segment seen end-on has no
        // direction on screen and gets none.
        private static void DrawArrow(Vector3 from, Vector3 to)
        {
            Vector3 along = to - from;
            if (along.sqrMagnitude < 1e-6f)
                return;
            along.Normalize();
            var across = new Vector3(-along.y, along.x, 0f);
            Vector3 middle = (from + to) / 2f;
            Vector3 tip = middle + along * (ArrowLength / 2f);
            Vector3 back = middle - along * (ArrowLength / 2f);
            Handles.DrawAAConvexPolygon(tip, back + across * (ArrowLength / 2f), back - across * (ArrowLength / 2f));
        }
    }
}
