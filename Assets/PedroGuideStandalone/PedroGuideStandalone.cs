using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace ArcaneMotion.PedroGuide
{
    /// <summary>
    /// Standalone VR gesture reference. All visual objects are created when the scene
    /// starts; there is no prefab, serialized asset GUID, or gesture-library dependency.
    /// Press G in the focused Unity Game view to show / hide it.
    /// This component changes no gameplay, spells, or controller bindings.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PedroGuideStandalone : MonoBehaviour
    {
        private const float PanelDistance = 2.15f;
        private const float PanelScale = 0.00125f;

        private GameObject panelRoot;
        private Camera guideCamera;
        private bool isVisible = true;

        public bool IsVisible { get { return isVisible; } }

        private static readonly Color Backdrop = new Color(0.055f, 0.080f, 0.135f, 0.96f);
        private static readonly Color CardColor = new Color(0.102f, 0.135f, 0.214f, 0.98f);
        private static readonly Color White = new Color(0.97f, 0.98f, 1f, 1f);
        private static readonly Color Muted = new Color(0.79f, 0.83f, 0.91f, 1f);
        // Colors are from the active PH Large Circle, PH Triangle, PH Star and PH Line Right assets.
        private static readonly Color CircleColor = new Color(0.6585526f, 0.023495764f, 0.8301887f, 1f);
        private static readonly Color TriangleColor = new Color(0.00925581f, 0.12614733f, 0.9811321f, 1f);
        private static readonly Color StarColor = new Color(0.9763613f, 1f, 0f, 1f);
        private static readonly Color LineColor = new Color(0.6981132f, 0.26124355f, 0.5924211f, 1f);

        private void Start()
        {
            TryCreatePanel();
        }

        private void Update()
        {
            if (panelRoot == null)
                TryCreatePanel();

            bool gPressed = false;
#if ENABLE_INPUT_SYSTEM
            gPressed = Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            gPressed = Input.GetKeyDown(KeyCode.G);
#endif
            if (gPressed)
                ToggleGuide();
        }

        // Also callable from a future VR controller action or UnityEvent.
        public void ToggleGuide()
        {
            SetVisible(!isVisible);
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;
            if (panelRoot != null)
                panelRoot.SetActive(visible);
        }

        private void OnDestroy()
        {
            if (panelRoot != null)
                Destroy(panelRoot);
        }

        private bool TryCreatePanel()
        {
            if (panelRoot != null)
                return true;

            Camera candidate = Camera.main;
            if (candidate == null)
            {
                Camera[] cameras = Camera.allCameras;
                for (int i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i] != null && cameras[i].isActiveAndEnabled)
                    {
                        candidate = cameras[i];
                        break;
                    }
                }
            }
            if (candidate == null)
                return false; // XR camera may be enabled a few frames after the scene loads.

            guideCamera = candidate;
            panelRoot = new GameObject("Pedro_GestureGuide_WorldCanvas", typeof(RectTransform), typeof(Canvas));
            panelRoot.transform.SetParent(guideCamera.transform, false);
            panelRoot.transform.localPosition = new Vector3(0f, -0.02f, PanelDistance);
            panelRoot.transform.localRotation = Quaternion.identity;
            panelRoot.transform.localScale = Vector3.one * PanelScale;

            Canvas canvas = panelRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = guideCamera;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 300;

            RectTransform rootRect = (RectTransform)panelRoot.transform;
            rootRect.sizeDelta = new Vector2(1140f, 730f);

            DrawPanel(rootRect);
            panelRoot.SetActive(isVisible);
            Debug.Log("[Pedro Guide] Ready. Focus the Game window and press G to open/close the guide.");
            return true;
        }

        private static void DrawPanel(RectTransform root)
        {
            RectTransform background = AddImage(root, "Background", new Vector2(0f, 0f),
                new Vector2(1140f, 730f), Backdrop);
            AddImage(background, "Top Accent", new Vector2(0f, 354f),
                new Vector2(1110f, 5f), CircleColor);

            AddText(background, "Title", "ARCANE MOTION  /  GESTURE GUIDE", 45f,
                new Vector2(0f, 317f), new Vector2(1030f, 62f), White, TextAlignmentOptions.Center, FontStyles.Bold);
            AddText(background, "Subtitle", "Hold A or X  •  Draw from START  •  Release to cast", 24f,
                new Vector2(0f, 270f), new Vector2(1040f, 43f), Muted, TextAlignmentOptions.Center);

            MakeCard(background, "CIRCLE", CircleColor, new Vector2(-277f, 120f),
                "START: bottom\nGo left, up and around.\nReturn to the bottom.", 0);
            MakeCard(background, "TRIANGLE", TriangleColor, new Vector2(277f, 120f),
                "START: top\nDown to bottom right,\nacross left, then up.", 1);
            MakeCard(background, "STAR", StarColor, new Vector2(-277f, -147f),
                "START: bottom left\nTop right → Top left\nBottom right → Top center\nBack to bottom left", 2);
            MakeCard(background, "LINE RIGHT", LineColor, new Vector2(277f, -147f),
                "START: left\nDraw straight to the right.\nRight-to-left is inactive.", 3);

            RectTransform footer = AddImage(background, "Footer", new Vector2(0f, -339f),
                new Vector2(1080f, 45f), new Color(0.16f, 0.19f, 0.27f, 0.75f));
            AddText(footer, "KeyboardHint", "PRESS  G  TO HIDE / SHOW THIS GUIDE  (desktop)", 22f,
                Vector2.zero, new Vector2(1010f, 38f), White, TextAlignmentOptions.Center, FontStyles.Bold);
        }

        private static void MakeCard(RectTransform root, string title, Color accent,
            Vector2 pos, string instructions, int shape)
        {
            RectTransform card = AddImage(root, title + " Card", pos, new Vector2(537f, 250f), CardColor);
            AddImage(card, "Accent", new Vector2(0f, 121f), new Vector2(515f, 5f), accent);
            AddText(card, title + " Title", title, 32f, new Vector2(0f, 92f),
                new Vector2(500f, 45f), accent, TextAlignmentOptions.Center, FontStyles.Bold);
            RectTransform diagram = AddEmptyRect(card, title + " Diagram", new Vector2(-158f, -13f),
                new Vector2(180f, 175f));
            DrawShape(diagram, accent, shape);
            AddText(card, title + " Instructions", instructions, 23f,
                new Vector2(100f, -20f), new Vector2(302f, 168f), White,
                TextAlignmentOptions.MidlineLeft);
            AddText(card, title + " Start Label", "START", 17f,
                new Vector2(-159f, -108f), new Vector2(180f, 27f), Muted,
                TextAlignmentOptions.Center, FontStyles.Bold);
        }

        private static void DrawShape(RectTransform diagram, Color color, int shape)
        {
            Vector2[] points;
            if (shape == 0)
            {
                points = new Vector2[33];
                for (int i = 0; i < points.Length; i++)
                {
                    float angle = (-90f - i * 360f / 32f) * Mathf.Deg2Rad;
                    points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
            }
            else if (shape == 1)
            {
                points = new[] { new Vector2(0f, 1f), new Vector2(1f, -0.87f),
                    new Vector2(-1f, -0.87f), new Vector2(0f, 1f) };
            }
            else if (shape == 2)
            {
                points = new[] { new Vector2(-1f, -0.70f), new Vector2(1f, 0.70f),
                    new Vector2(-1f, 0.70f), new Vector2(1f, -0.70f),
                    new Vector2(0f, 1.10f), new Vector2(-1f, -0.70f) };
            }
            else
            {
                points = new[] { new Vector2(-1f, 0f), new Vector2(1f, 0f) };
            }

            const float shapeRadius = 67f;
            for (int i = 1; i < points.Length; i++)
                AddSegment(diagram, points[i - 1] * shapeRadius, points[i] * shapeRadius, color);

            RectTransform start = AddImage(diagram, "WHITE START", points[0] * shapeRadius,
                new Vector2(17f, 17f), White);
            start.SetAsLastSibling();
        }

        private static void AddSegment(RectTransform parent, Vector2 from, Vector2 to, Color color)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            RectTransform line = AddImage(parent, "Path Segment", (from + to) * 0.5f,
                new Vector2(length + 1f, 6f), color);
            line.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private static RectTransform AddEmptyRect(Transform parent, string name, Vector2 center, Vector2 size)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)child.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform AddImage(Transform parent, string name, Vector2 center, Vector2 size, Color color)
        {
            RectTransform rect = AddEmptyRect(parent, name, center, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static void AddText(Transform parent, string name, string text, float fontSize,
            Vector2 center, Vector2 size, Color color, TextAlignmentOptions alignment,
            FontStyles style = FontStyles.Normal)
        {
            RectTransform rect = AddEmptyRect(parent, name, center, size);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                label.font = font;
            label.text = text;
            label.color = color;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = alignment;
            label.enableAutoSizing = false;
            label.raycastTarget = false;
        }
    }

#if UNITY_EDITOR
    /// <summary>Creates a fresh scene from the currently open Emily scene; no prefabs are generated.</summary>
    public static class PedroGuideSceneMenu
    {
        private const string OutputFolder = "Assets/PedroGuideStandalone";

        [MenuItem("Tools/Arcane Motion/Pedro NEW/1 - Create Guide Demo (G Toggle)")]
        public static void CreateDemo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Pedro Guide", "Stop Play Mode before creating your guide scene.", "OK");
                return;
            }

            Scene current = SceneManager.GetActiveScene();
            if (!current.IsValid() || string.IsNullOrEmpty(current.path) || current.name != "EmilyTestScene")
            {
                EditorUtility.DisplayDialog("Pedro Guide", "First open EmilyTestScene from the original Assets/Scenes folder, then run this menu again.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets", "PedroGuideStandalone");

            string path = AssetDatabase.GenerateUniqueAssetPath(OutputFolder + "/PedroGuideDemo.unity");
            if (!EditorSceneManager.SaveScene(current, path, true))
            {
                EditorUtility.DisplayDialog("Pedro Guide", "Unity could not save a copy of EmilyTestScene.", "OK");
                return;
            }

            Scene demo = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            GameObject host = new GameObject("Pedro_Gesture_Guide_G_Toggle");
            host.AddComponent<PedroGuideStandalone>();

            bool introHidden = false;
            GameObject[] roots = demo.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform intro = FindNamedChild(roots[i].transform, "UI_IntroGuide");
                if (intro != null)
                {
                    intro.gameObject.SetActive(false);
                    introHidden = true;
                }
            }

            EditorSceneManager.MarkSceneDirty(demo);
            if (!EditorSceneManager.SaveScene(demo))
            {
                Debug.LogError("[Pedro Guide] Scene was created but could not be saved: " + path);
                return;
            }
            AssetDatabase.Refresh();
            Debug.Log("[Pedro Guide] CREATED " + path + ". Intro hidden=" + introHidden +
                ". Press Play, focus Game, press G to toggle the guide.");
            EditorUtility.DisplayDialog("Pedro Guide Ready",
                "Created a separate scene:\n" + path +
                "\n\nThe introduction is hidden in this demo scene.\n\nPress Play to see the guide. Press G to hide/show it.\n\nEmilyTestScene was not overwritten.", "OK");
        }

        [MenuItem("Tools/Arcane Motion/Pedro NEW/2 - Verify Installed Script")]
        public static void Verify()
        {
            Debug.Log("[Pedro Guide] Runtime component compiled: " + typeof(PedroGuideStandalone).FullName +
                ". Open EmilyTestScene and run menu 1 to make the demo scene.");
            EditorUtility.DisplayDialog("Pedro Guide", "Standalone component compiled successfully.\nOpen EmilyTestScene and choose menu 1.", "OK");
        }

        private static Transform FindNamedChild(Transform parent, string name)
        {
            if (parent.name == name)
                return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform hit = FindNamedChild(parent.GetChild(i), name);
                if (hit != null)
                    return hit;
            }
            return null;
        }
    }
#endif
}
