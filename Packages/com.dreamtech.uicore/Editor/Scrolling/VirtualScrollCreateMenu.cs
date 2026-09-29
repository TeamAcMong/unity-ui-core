using System;
using DreamTech.UICore.Scrolling;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DreamTech.UICore.Editor.Scrolling
{
    /// <summary>
    /// Menu tạo sẵn cả cụm: ScrollRect + Viewport (RectMask2D) + Content + template (tắt, nằm ngoài Content) + list/grid đã cấu
    /// hình + bộ đổ dữ liệu mẫu — bấm Play là cuộn được ngay. Có Undo, tự tạo Canvas / EventSystem khi chưa có.
    /// </summary>
    internal static class VirtualScrollCreateMenu
    {
        private const string Root = "GameObject/DreamTech UI Core/";

        [MenuItem(Root + "Virtual List (Vertical)", false, 10)]
        private static void CreateVerticalList(MenuCommand command)
        {
            Create<VirtualListView>(command, "Virtual List", ScrollDirection.TopToBottom, new Vector2(600f, 900f), new Vector2(0f, 140f),
                                    configure: null);
        }

        [MenuItem(Root + "Virtual List (Horizontal)", false, 11)]
        private static void CreateHorizontalList(MenuCommand command)
        {
            Create<VirtualListView>(command, "Virtual List (Horizontal)", ScrollDirection.LeftToRight, new Vector2(900f, 260f),
                                    new Vector2(220f, 0f), configure: null);
        }

        [MenuItem(Root + "Virtual Grid", false, 12)]
        private static void CreateGrid(MenuCommand command)
        {
            Create<VirtualGridView>(command, "Virtual Grid", ScrollDirection.TopToBottom, new Vector2(700f, 900f), new Vector2(200f, 200f),
                                    configure: view =>
                                    {
                                        var serialized = new SerializedObject(view);
                                        serialized.FindProperty("_cellSizeFromTemplate").boolValue = true;
                                        serialized.ApplyModifiedPropertiesWithoutUndo();
                                    });
        }

        [MenuItem(Root + "Page View (Carousel)", false, 13)]
        private static void CreatePageView(MenuCommand command)
        {
            Create<VirtualListView>(command, "Page View", ScrollDirection.LeftToRight, new Vector2(800f, 500f), new Vector2(800f, 0f),
                                    configure: view =>
                                    {
                                        var serialized = new SerializedObject(view);
                                        serialized.FindProperty("_spacing").floatValue = 0f;
                                        SerializedProperty padding = serialized.FindProperty("_padding");
                                        padding.FindPropertyRelative("Start").floatValue = 0f;
                                        padding.FindPropertyRelative("End").floatValue = 0f;
                                        SerializedProperty snap = serialized.FindProperty("_snap");
                                        snap.FindPropertyRelative("_mode").enumValueIndex = (int)ScrollSnapMode.Paged;
                                        snap.FindPropertyRelative("_viewportPivot").floatValue = 0.5f;
                                        snap.FindPropertyRelative("_itemPivot").floatValue = 0.5f;
                                        serialized.ApplyModifiedPropertiesWithoutUndo();
                                        var pageScroll = view.GetComponent<ScrollRect>();
                                        pageScroll.inertia = true;
                                        pageScroll.decelerationRate = 0.05f;
                                    });
        }

        [MenuItem(Root + "Chat List (Bottom → Top)", false, 14)]
        private static void CreateChatList(MenuCommand command)
        {
            Create<VirtualListView>(command, "Chat List", ScrollDirection.BottomToTop, new Vector2(600f, 900f), new Vector2(0f, 120f),
                                    configure: view =>
                                    {
                                        var serialized = new SerializedObject(view);
                                        serialized.FindProperty("_sizeMode").enumValueIndex = (int)ListItemSizeMode.Measure;
                                        serialized.ApplyModifiedPropertiesWithoutUndo();
                                    });
        }

        /// <param name="templateSize">Cỡ template; 0 theo trục phụ = kéo giãn hết khung (list dọc) — template lấy bề rộng khung.</param>
        private static void Create<TView>(MenuCommand command, string name, ScrollDirection direction, Vector2 size, Vector2 templateSize,
                                          Action<TView> configure)
            where TView : VirtualScrollViewBase
        {
            RectTransform parent = ResolveParent(command);

            var rootObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            Undo.RegisterCreatedObjectUndo(rootObject, "Tạo " + name);
            var root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.sizeDelta = size;
            rootObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

            RectTransform viewport = CreateChild("Viewport", root, typeof(RectMask2D));
            Stretch(viewport);
            RectTransform content = CreateChild("Content", viewport);
            direction.ConfigureContent(content);

            bool vertical = direction.IsVertical();
            RectTransform template = CreateChild("Item Template", root, typeof(Image));
            template.anchorMin = template.anchorMax = new Vector2(0.5f, 0.5f);
            template.sizeDelta = new Vector2(templateSize.x > 0f ? templateSize.x : size.x - 40f,
                                             templateSize.y > 0f ? templateSize.y : size.y - 40f);
            template.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.9f);
            TryAddLabel(template);
            template.gameObject.SetActive(false);

            var scrollRect = rootObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.vertical = vertical;
            scrollRect.horizontal = !vertical;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 30f;

            var view = rootObject.AddComponent<TView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_direction").enumValueIndex = (int)direction;
            SerializedProperty templates = serialized.FindProperty("_templates");
            templates.arraySize = 1;
            templates.GetArrayElementAtIndex(0).FindPropertyRelative("_template").objectReferenceValue = template;
            templates.GetArrayElementAtIndex(0).FindPropertyRelative("_prewarmCount").intValue = 8;
            SerializedProperty padding = serialized.FindProperty("_padding");
            padding.FindPropertyRelative("Start").floatValue = 20f;
            padding.FindPropertyRelative("End").floatValue = 20f;
            padding.FindPropertyRelative("CrossStart").floatValue = 20f;
            padding.FindPropertyRelative("CrossEnd").floatValue = 20f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            configure?.Invoke(view);

            rootObject.AddComponent<VirtualScrollDemoFiller>();
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                foreach (Transform child in rootObject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = uiLayer;
            }
            EnsureEventSystem();
            Selection.activeGameObject = rootObject;
        }

        private static RectTransform CreateChild(string name, Transform parent, params Type[] components)
        {
            var types = new Type[components.Length + 1];
            types[0] = typeof(RectTransform);
            Array.Copy(components, 0, types, 1, components.Length);
            var child = new GameObject(name, types).GetComponent<RectTransform>();
            child.SetParent(parent, false);
            return child;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void TryAddLabel(RectTransform template)
        {
            // Chỉ khi TMP đã có tài nguyên — tránh bật hộp thoại nhập TMP Essentials.
            if (Resources.Load<TMP_Settings>("TMP Settings") == null) return;
            RectTransform label = CreateChild("Label", template);
            Stretch(label);
            var text = label.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = "#0";
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.15f, 0.17f, 0.25f, 1f);
            text.fontSize = 36f;
            text.raycastTarget = false;
        }

        private static RectTransform ResolveParent(MenuCommand command)
        {
            var context = command.context as GameObject;
            if (context != null && context.GetComponentInParent<Canvas>() != null) return (RectTransform)context.transform;

            Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas != null) return (RectTransform)canvas.transform;

            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Tạo Canvas");
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.layer = LayerMask.NameToLayer("UI");
            return (RectTransform)canvasObject.transform;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(eventSystem, "Tạo EventSystem");
            // Project dùng Input System mới thì dùng module của nó, không thì module cũ.
            Type inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null) eventSystem.AddComponent(inputSystemModule);
            else eventSystem.AddComponent<StandaloneInputModule>();
        }
    }
}
