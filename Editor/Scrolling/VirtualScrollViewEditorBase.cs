using System.Collections.Generic;
using DreamTech.UICore.Editor.Base;
using DreamTech.UICore.Editor.Styles;
using DreamTech.UICore.Scrolling;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DreamTech.UICore.Editor.Scrolling
{
    /// <summary>
    /// Inspector chung của list / grid ảo hoá: 5 tab (Bố cục · Ô · Chuyển động · Sự kiện · Xem trước), thẻ kiểm tra setup có nút
    /// sửa, xem trước trong Edit mode, gizmo trong Scene, và bảng số liệu + điều khiển khi Play.
    /// </summary>
    internal abstract class VirtualScrollViewEditorBase : UIComponentEditorBase
    {
        private const string PrefPrefix = "DreamTech.UICore.VirtualScroll.";
        private static readonly string[] Tabs = { "Bố cục", "Ô", "Chuyển động", "Sự kiện", "Xem trước" };
        private static readonly string[] SnapModeLabels = { "Tắt", "Ô gần nhất", "Lật trang" };

        private bool _foldDirection = true;
        private bool _foldSpacing = true;
        private bool _foldSpecific = true;
        private bool _foldPerformance = true;
        private bool _foldTemplates = true;
        private bool _foldRecycle = true;
        private bool _foldAnimation = true;
        private bool _foldSnap = true;
        private bool _foldEdges = true;
        private bool _foldCallbacks = true;
        private bool _foldPreview = true;
        private bool _foldGizmos = true;
        private int _playTargetIndex;

        protected VirtualScrollViewBase View => (VirtualScrollViewBase)target;

        protected override string[] TabNames => Tabs;

        protected override string HeaderSubtitle => "Ảo hoá — chỉ dựng view cho ô đang nhìn thấy";

        protected override GUIContent HeaderIcon => EditorGUIUtility.IconContent("d_ScrollRect Icon");

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        // ── Tuỳ chọn xem trước / gizmo (nhớ theo máy) ───────────────────────────────────────────────────────────

        private static int PreviewCount
        {
            get => EditorPrefs.GetInt(PrefPrefix + "PreviewCount", 40);
            set => EditorPrefs.SetInt(PrefPrefix + "PreviewCount", value);
        }

        private static bool PreviewMixTypes
        {
            get => EditorPrefs.GetBool(PrefPrefix + "PreviewMix", true);
            set => EditorPrefs.SetBool(PrefPrefix + "PreviewMix", value);
        }

        private static bool GizmoViewport
        {
            get => EditorPrefs.GetBool(PrefPrefix + "GizmoViewport", true);
            set => EditorPrefs.SetBool(PrefPrefix + "GizmoViewport", value);
        }

        private static bool GizmoOverscan
        {
            get => EditorPrefs.GetBool(PrefPrefix + "GizmoOverscan", true);
            set => EditorPrefs.SetBool(PrefPrefix + "GizmoOverscan", value);
        }

        private static bool GizmoPivot
        {
            get => EditorPrefs.GetBool(PrefPrefix + "GizmoPivot", true);
            set => EditorPrefs.SetBool(PrefPrefix + "GizmoPivot", value);
        }

        private static bool GizmoIndices
        {
            get => EditorPrefs.GetBool(PrefPrefix + "GizmoIndices", true);
            set => EditorPrefs.SetBool(PrefPrefix + "GizmoIndices", value);
        }

        // ── Lớp con ────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Phần bố cục riêng của list / grid (cỡ ô, số cột…).</summary>
        protected abstract void DrawLayoutSpecific();

        /// <summary>Khoảng cách giữa ô (list: một số, grid: hai số).</summary>
        protected abstract void DrawSpacing();

        /// <summary>Dòng tóm tắt bố cục cho tab Xem trước / bảng Play.</summary>
        protected abstract string LayoutSummary();

        // ── Vòng đời inspector ─────────────────────────────────────────────────────────────────────────────────

        protected virtual void OnDisable()
        {
            // Bỏ chọn object / đóng inspector: tắt xem trước, trả Content về như cũ.
            if (!Application.isPlaying && target != null) VirtualScrollPreview.Stop(View);
        }

        protected override void DrawTabContent(int tabIndex)
        {
            DrawStatusRow();
            DrawIssues();

            EditorGUI.BeginChangeCheck();
            ScrollDirection directionBefore = View.Direction;
            switch (tabIndex)
            {
                case 0: DrawLayoutTab(); break;
                case 1: DrawItemsTab(); break;
                case 2: DrawMotionTab(); break;
                case 3: DrawEventsTab(); break;
                default: DrawPreviewTab(); break;
            }
            if (!EditorGUI.EndChangeCheck()) return;

            serializedObject.ApplyModifiedProperties();
            if (View.Direction != directionBefore) OnDirectionChanged();
            OnSettingsChanged();
        }

        private void OnSettingsChanged()
        {
            if (Application.isPlaying)
            {
                View.Rebuild();
                return;
            }
            if (VirtualScrollPreview.IsActive(View)) VirtualScrollPreview.Refresh(View);
        }

        private void OnDirectionChanged()
        {
            // Đổi hướng = đổi cấu hình ScrollRect/Content thật (có Undo). Xem trước chụp lại mốc sau khi áp.
            bool previewing = VirtualScrollPreview.IsActive(View);
            float offset = VirtualScrollPreview.GetOffset(View);
            if (previewing) VirtualScrollPreview.Stop(View);
            if (!Application.isPlaying) VirtualScrollSetup.ApplySetup(View);
            if (previewing) VirtualScrollPreview.Show(View, PreviewCount, PreviewMixTypes, offset);
        }

        // ── Trạng thái + vấn đề ────────────────────────────────────────────────────────────────────────────────

        private void DrawStatusRow()
        {
            EditorGUILayout.BeginHorizontal();
            DrawPill(VirtualScrollSetup.DirectionLabel(View.Direction), UIEditorStyles.Accent);
            DrawPill(View.Templates.Count + " loại ô", UIEditorStyles.AnimationModuleColor);
            if (View.Snap.Enabled) DrawPill("Snap: " + SnapModeLabels[(int)View.Snap.Mode], UIEditorStyles.BehaviorModuleColor);
            if (Application.isPlaying)
            {
                DrawPill(View.ItemCount + " ô", UIEditorStyles.SuccessColor);
                DrawPill(View.ActiveItemCount + " view", UIEditorStyles.SuccessColor);
            }
            else if (VirtualScrollPreview.IsActive(View))
            {
                DrawPill("Đang xem trước", UIEditorStyles.WarningColor);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4f);
        }

        private void DrawIssues()
        {
            List<VirtualScrollSetup.Issue> issues = VirtualScrollSetup.Validate(View);
            foreach (VirtualScrollSetup.Issue issue in issues)
            {
                DrawHelpCard(issue.Message, issue.Level == VirtualScrollSetup.Severity.Danger ? HelpType.Danger : HelpType.Warning);
                if (issue.Fix == null) continue;
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(issue.FixLabel, GUILayout.Width(140f)))
                {
                    bool previewing = VirtualScrollPreview.IsActive(View);
                    if (previewing) VirtualScrollPreview.Stop(View);
                    issue.Fix();
                    if (previewing) VirtualScrollPreview.Show(View, PreviewCount, PreviewMixTypes, 0f);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(4f);
            }
        }

        // ── Tab: Bố cục ────────────────────────────────────────────────────────────────────────────────────────

        private void DrawLayoutTab()
        {
            if (DrawSectionCard("Hướng cuộn", ref _foldDirection)) DrawDirectionPicker();
            EndSectionCard();

            if (DrawSectionCard("Đệm & khoảng cách", ref _foldSpacing))
            {
                DrawPadding();
                EditorGUILayout.Space(2f);
                DrawSpacing();
            }
            EndSectionCard();

            if (DrawSectionCard(SpecificSectionTitle, ref _foldSpecific)) DrawLayoutSpecific();
            EndSectionCard();

            if (DrawSectionCard("Hiệu năng", ref _foldPerformance))
            {
                SerializedProperty overscan = serializedObject.FindProperty("_overscan");
                overscan.floatValue = EditorGUILayout.Slider(new GUIContent("Vùng đệm (px)", overscan.tooltip), overscan.floatValue, 0f, 1200f);
                EditorGUILayout.LabelField("Giữ sẵn view trước và sau khung nhìn — cuộn nhanh không lộ ô trống ở mép.",
                                           UIEditorStyles.MutedLabel);
            }
            EndSectionCard();
        }

        protected virtual string SpecificSectionTitle => "Cỡ ô";

        private void DrawDirectionPicker()
        {
            SerializedProperty direction = serializedObject.FindProperty("_direction");
            string[] labels = { "↓  Trên → Dưới", "↑  Dưới → Trên", "→  Trái → Phải", "←  Phải → Trái" };
            int current = direction.enumValueIndex;
            EditorGUILayout.BeginHorizontal();
            for (int index = 0; index < 2; index++) DirectionButton(direction, labels, index, current);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            for (int index = 2; index < 4; index++) DirectionButton(direction, labels, index, current);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(DirectionHint((ScrollDirection)current), UIEditorStyles.MutedLabel);
        }

        private static void DirectionButton(SerializedProperty property, string[] labels, int index, int current)
        {
            bool selected = index == current;
            Color previous = GUI.backgroundColor;
            if (selected) GUI.backgroundColor = UIEditorStyles.Accent;
            if (GUILayout.Toggle(selected, labels[index], "Button", GUILayout.Height(24f)) && !selected) property.enumValueIndex = index;
            GUI.backgroundColor = previous;
        }

        private static string DirectionHint(ScrollDirection direction)
        {
            switch (direction)
            {
                case ScrollDirection.BottomToTop: return "Ô 0 ở đáy, ô mới chồng lên trên — kiểu khung chat.";
                case ScrollDirection.LeftToRight: return "Ô 0 ở mép trái — carousel, danh sách ngang.";
                case ScrollDirection.RightToLeft: return "Ô 0 ở mép phải — ngôn ngữ viết từ phải sang trái.";
                default: return "Ô 0 ở trên cùng — danh sách thường.";
            }
        }

        private void DrawPadding()
        {
            SerializedProperty padding = serializedObject.FindProperty("_padding");
            (string start, string end, string crossStart, string crossEnd) = VirtualScrollSetup.PaddingLabels(View.Direction);
            EditorGUILayout.LabelField("Đệm", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            FloatField(padding.FindPropertyRelative("Start"), start);
            FloatField(padding.FindPropertyRelative("End"), end);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            FloatField(padding.FindPropertyRelative("CrossStart"), crossStart);
            FloatField(padding.FindPropertyRelative("CrossEnd"), crossEnd);
            EditorGUILayout.EndHorizontal();
        }

        protected static void FloatField(SerializedProperty property, string label)
        {
            float previousWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 44f;
            property.floatValue = EditorGUILayout.FloatField(label, property.floatValue);
            EditorGUIUtility.labelWidth = previousWidth;
        }

        // ── Tab: Ô ─────────────────────────────────────────────────────────────────────────────────────────────

        private void DrawItemsTab()
        {
            if (DrawSectionCard("Template (loại ô)", ref _foldTemplates)) DrawTemplates();
            EndSectionCard();

            if (DrawSectionCard("Cất view trôi khỏi khung", ref _foldRecycle))
            {
                SerializedProperty recycle = serializedObject.FindProperty("_recycleMode");
                EditorGUILayout.PropertyField(recycle, new GUIContent("Cách cất"));
                EditorGUILayout.LabelField(recycle.enumValueIndex == (int)RecycleMode.Park
                                               ? "Giữ view bật, dời ra xa ngoài khung (mask cắt). Cuộn mượt hơn với ô nặng; khung PHẢI có mask."
                                               : "Tắt view. An toàn; bật lại tốn OnEnable (chữ, animator…).",
                                           UIEditorStyles.MutedLabel);
            }
            EndSectionCard();
        }

        private void DrawTemplates()
        {
            SerializedProperty templates = serializedObject.FindProperty("_templates");
            if (templates.arraySize == 0)
            {
                DrawEmptyState("Chưa có loại ô nào. Kéo prefab / object vào ô dưới, hoặc bấm Thêm.", "+ Thêm loại ô",
                               () => templates.InsertArrayElementAtIndex(0));
            }

            int removeAt = -1;
            int moveUp = -1;
            for (int index = 0; index < templates.arraySize; index++)
            {
                SerializedProperty element = templates.GetArrayElementAtIndex(index);
                SerializedProperty template = element.FindPropertyRelative("_template");
                SerializedProperty prewarm = element.FindPropertyRelative("_prewarmCount");
                var reference = template.objectReferenceValue as RectTransform;

                EditorGUILayout.BeginVertical(UIEditorStyles.ModuleCard);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Loại " + index, EditorStyles.boldLabel, GUILayout.Width(56f));
                if (reference == null) DrawPill("Trống", UIEditorStyles.DangerColor);
                else if (reference.gameObject.scene.IsValid()) DrawPill("Trong scene", UIEditorStyles.BehaviorModuleColor);
                else DrawPill("Prefab", UIEditorStyles.AnimationModuleColor);
                if (reference != null) DrawPill($"{reference.rect.width:0}×{reference.rect.height:0}", UIEditorStyles.MutedText);
                GUILayout.FlexibleSpace();
                GUI.enabled = index > 0;
                if (GUILayout.Button("▲", EditorStyles.miniButton, GUILayout.Width(22f))) moveUp = index;
                GUI.enabled = true;
                if (IconButton(UIEditorStyles.IconRemove, "Bỏ loại ô này")) removeAt = index;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(template, new GUIContent("Template"));
                EditorGUILayout.PropertyField(prewarm, new GUIContent("Tạo sẵn", prewarm.tooltip));
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2f);
            }

            if (removeAt >= 0) templates.DeleteArrayElementAtIndex(removeAt);
            if (moveUp > 0) templates.MoveArrayElement(moveUp, moveUp - 1);

            DrawTemplateDropArea(templates);

            if (GUILayout.Button("+ Thêm loại ô")) templates.InsertArrayElementAtIndex(templates.arraySize);
            EditorGUILayout.LabelField("Adapter chọn loại ô bằng GetItemType(index) → chỉ số ở đây. View tự được gắn VirtualItem.",
                                       UIEditorStyles.MutedLabel);
        }

        private static void DrawTemplateDropArea(SerializedProperty templates)
        {
            Rect area = GUILayoutUtility.GetRect(0f, 34f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(area, new Color(UIEditorStyles.Accent.r, UIEditorStyles.Accent.g, UIEditorStyles.Accent.b, 0.08f));
            }
            GUI.Label(area, "Thả prefab / object UI vào đây để thêm loại ô", UIEditorStyles.EmptyStateLabel);

            Event current = Event.current;
            if (!area.Contains(current.mousePosition)) return;
            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (current.type != EventType.DragPerform) return;
            DragAndDrop.AcceptDrag();
            foreach (Object dragged in DragAndDrop.objectReferences)
            {
                RectTransform rect = dragged is GameObject go ? go.GetComponent<RectTransform>() : dragged as RectTransform;
                if (rect == null) continue;
                int slot = templates.arraySize;
                templates.InsertArrayElementAtIndex(slot);
                SerializedProperty element = templates.GetArrayElementAtIndex(slot);
                element.FindPropertyRelative("_template").objectReferenceValue = rect;
                element.FindPropertyRelative("_prewarmCount").intValue = 0;
            }
            current.Use();
        }

        // ── Tab: Chuyển động ───────────────────────────────────────────────────────────────────────────────────

        private void DrawMotionTab()
        {
            if (DrawSectionCard("Hút về ô (snap)", ref _foldSnap)) DrawSnap();
            EndSectionCard();

            if (DrawSectionCard("Cuộn bằng code (ScrollToIndex…)", ref _foldAnimation))
            {
                SerializedProperty animation = serializedObject.FindProperty("_scrollAnimation");
                EditorGUILayout.PropertyField(animation.FindPropertyRelative("_duration"), new GUIContent("Thời lượng (giây)"));
                EditorGUILayout.PropertyField(animation.FindPropertyRelative("_curve"), new GUIContent("Đường cong"));
                SerializedProperty unscaled = animation.FindPropertyRelative("_useUnscaledTime");
                EditorGUILayout.PropertyField(unscaled, new GUIContent("Giờ thật", unscaled.tooltip));
            }
            EndSectionCard();
        }

        private void DrawSnap()
        {
            SerializedProperty snap = serializedObject.FindProperty("_snap");
            SerializedProperty mode = snap.FindPropertyRelative("_mode");
            mode.enumValueIndex = GUILayout.Toolbar(mode.enumValueIndex, SnapModeLabels, GUILayout.Height(24f));
            EditorGUILayout.LabelField(SnapHint((ScrollSnapMode)mode.enumValueIndex), UIEditorStyles.MutedLabel);

            SerializedProperty viewportPivot = snap.FindPropertyRelative("_viewportPivot");
            SerializedProperty itemPivot = snap.FindPropertyRelative("_itemPivot");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Điểm neo (dùng cho snap, ô đang focus, Next/Previous)", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Mép đầu", EditorStyles.miniButtonLeft)) SetPivots(viewportPivot, itemPivot, 0f);
            if (GUILayout.Button("Giữa", EditorStyles.miniButtonMid)) SetPivots(viewportPivot, itemPivot, 0.5f);
            if (GUILayout.Button("Mép cuối", EditorStyles.miniButtonRight)) SetPivots(viewportPivot, itemPivot, 1f);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Slider(viewportPivot, 0f, 1f, new GUIContent("Trên khung nhìn", viewportPivot.tooltip));
            EditorGUILayout.Slider(itemPivot, 0f, 1f, new GUIContent("Trên ô", itemPivot.tooltip));

            bool vertical = View.Direction.IsVertical();
            Rect diagram = GUILayoutUtility.GetRect(0f, vertical ? 150f : 96f, GUILayout.ExpandWidth(true));
            SnapDiagram.Draw(diagram, vertical, View.Direction.IsReversed(), viewportPivot.floatValue, itemPivot.floatValue);

            if (mode.enumValueIndex == (int)ScrollSnapMode.Off) return;
            EditorGUILayout.Space(4f);
            if (mode.enumValueIndex == (int)ScrollSnapMode.Nearest)
            {
                SerializedProperty settle = snap.FindPropertyRelative("_settleSpeed");
                EditorGUILayout.PropertyField(settle, new GUIContent("Hút khi chậm dưới (px/s)", settle.tooltip));
            }
            else
            {
                SerializedProperty flick = snap.FindPropertyRelative("_flickSpeed");
                EditorGUILayout.PropertyField(flick, new GUIContent("Vuốt sang trang từ (px/s)", flick.tooltip));
            }
            EditorGUILayout.PropertyField(snap.FindPropertyRelative("_duration"), new GUIContent("Thời lượng hút (giây)"));
            EditorGUILayout.PropertyField(snap.FindPropertyRelative("_curve"), new GUIContent("Đường cong hút"));
        }

        private static void SetPivots(SerializedProperty viewportPivot, SerializedProperty itemPivot, float value)
        {
            viewportPivot.floatValue = value;
            itemPivot.floatValue = value;
        }

        private static string SnapHint(ScrollSnapMode mode)
        {
            switch (mode)
            {
                case ScrollSnapMode.Nearest: return "Thả tay, quán tính chậm lại thì hút về ô gần điểm neo nhất.";
                case ScrollSnapMode.Paged: return "Mỗi cú vuốt sang đúng một ô — carousel, trang hướng dẫn, chọn map.";
                default: return "Cuộn tự do theo quán tính của ScrollRect.";
            }
        }

        // ── Tab: Sự kiện ───────────────────────────────────────────────────────────────────────────────────────

        private void DrawEventsTab()
        {
            if (DrawSectionCard("Mép đầu / cuối", ref _foldEdges))
            {
                SerializedProperty threshold = serializedObject.FindProperty("_edgeThreshold");
                EditorGUILayout.PropertyField(threshold, new GUIContent("Ngưỡng (px)", threshold.tooltip));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_onReachedStart"), new GUIContent("On Reached Start"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_onReachedEnd"), new GUIContent("On Reached End"));
                EditorGUILayout.LabelField("Reached End bắn cả khi nội dung ngắn hơn khung — dùng để tải trang tiếp theo.",
                                           UIEditorStyles.MutedLabel);
            }
            EndSectionCard();

            if (DrawSectionCard("Focus & snap", ref _foldCallbacks))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_onFocusedIndexChanged"), new GUIContent("On Focused Index Changed"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_onSnapped"), new GUIContent("On Snapped"));
                EditorGUILayout.LabelField("Code: view.ItemBound / view.ItemRecycled (C# event) cho từng view.", UIEditorStyles.MutedLabel);
            }
            EndSectionCard();
        }

        // ── Tab: Xem trước ─────────────────────────────────────────────────────────────────────────────────────

        private void DrawPreviewTab()
        {
            if (DrawSectionCard("Xem trước trong Scene", ref _foldPreview))
            {
                if (Application.isPlaying)
                {
                    DrawHelpCard("Đang Play — dùng bảng Play Mode Tools bên dưới.", HelpType.Info);
                }
                else
                {
                    DrawEditPreviewControls();
                }
            }
            EndSectionCard();

            if (DrawSectionCard("Gizmo trong Scene", ref _foldGizmos))
            {
                EditorGUI.BeginChangeCheck();
                GizmoViewport = EditorGUILayout.ToggleLeft("Khung nhìn", GizmoViewport);
                GizmoOverscan = EditorGUILayout.ToggleLeft("Vùng đệm (overscan)", GizmoOverscan);
                GizmoPivot = EditorGUILayout.ToggleLeft("Đường neo snap", GizmoPivot);
                GizmoIndices = EditorGUILayout.ToggleLeft("Số thứ tự ô", GizmoIndices);
                if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
            }
            EndSectionCard();
        }

        private void DrawEditPreviewControls()
        {
            bool active = VirtualScrollPreview.IsActive(View);
            int count = EditorGUILayout.IntSlider("Số ô", PreviewCount, 1, 500);
            bool mix = View.Templates.Count > 1 && EditorGUILayout.Toggle("Xoay vòng loại ô", PreviewMixTypes);
            if (count != PreviewCount || (View.Templates.Count > 1 && mix != PreviewMixTypes))
            {
                PreviewCount = count;
                if (View.Templates.Count > 1) PreviewMixTypes = mix;
                if (active) VirtualScrollPreview.Show(View, PreviewCount, PreviewMixTypes, VirtualScrollPreview.GetOffset(View));
            }

            if (active)
            {
                float max = View.MaxScrollOffset;
                float offset = VirtualScrollPreview.GetOffset(View);
                float next = EditorGUILayout.Slider("Cuộn tới (px)", offset, 0f, Mathf.Max(1f, max));
                if (!Mathf.Approximately(next, offset)) VirtualScrollPreview.Show(View, PreviewCount, PreviewMixTypes, next);

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(LayoutSummary(), UIEditorStyles.MutedLabel);
                EditorGUILayout.LabelField(
                    $"Nội dung {View.ContentLength:0} px · khung {View.ViewportLength:0} px · {View.ActiveItemCount} view đang hiện / " +
                    $"{View.CreatedItemCount} đã tạo", UIEditorStyles.MutedLabel);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            if (!active)
            {
                if (GUILayout.Button("Bật xem trước", GUILayout.Height(26f))) VirtualScrollPreview.Show(View, PreviewCount, PreviewMixTypes, 0f);
            }
            else
            {
                if (GUILayout.Button("Làm mới", GUILayout.Height(26f))) VirtualScrollPreview.Refresh(View);
                if (GUILayout.Button("Tắt xem trước", GUILayout.Height(26f))) VirtualScrollPreview.Stop(View);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("View xem trước không được lưu; Content trả về như cũ khi tắt, khi lưu scene, khi vào Play.",
                                       UIEditorStyles.MutedLabel);
        }

        // ── Play mode ──────────────────────────────────────────────────────────────────────────────────────────

        protected override void DrawPlayModeContent()
        {
            VirtualScrollViewBase view = View;
            EditorGUILayout.LabelField(LayoutSummary(), UIEditorStyles.MutedLabel);
            EditorGUILayout.LabelField($"Ô: {view.ItemCount} · nhìn thấy {view.FirstVisibleIndex}…{view.LastVisibleIndex} · focus {view.FocusedIndex}");
            EditorGUILayout.LabelField($"View: {view.ActiveItemCount} đang hiện · {view.PooledItemCount} trong pool · {view.CreatedItemCount} đã tạo");
            EditorGUILayout.LabelField($"Cuộn: {view.ScrollOffset:0} / {view.MaxScrollOffset:0} px ({view.NormalizedPosition:P0})" +
                                       (view.IsDragging ? " · đang kéo" : string.Empty) + (view.IsAnimating ? " · đang tự chạy" : string.Empty));

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            _playTargetIndex = EditorGUILayout.IntField("Tới ô", _playTargetIndex);
            if (GUILayout.Button("Cuộn", GUILayout.Width(60f)))
            {
                view.ScrollToIndex(_playTargetIndex, view.Snap.ViewportPivot, view.Snap.ItemPivot);
            }
            if (GUILayout.Button("Nhảy", GUILayout.Width(60f)))
            {
                view.JumpToIndex(_playTargetIndex, view.Snap.ViewportPivot, view.Snap.ItemPivot);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("◀ Trước")) view.ScrollToPrevious();
            if (GUILayout.Button("Sau ▶")) view.ScrollToNext();
            if (GUILayout.Button("Về đầu")) view.ScrollToOffset(0f);
            if (GUILayout.Button("Tới cuối")) view.ScrollToOffset(view.MaxScrollOffset);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Bind lại ô đang hiện")) view.RefreshVisibleItems();
            if (GUILayout.Button("Dừng")) view.StopScrolling();
            EditorGUILayout.EndHorizontal();
        }

        // ── Gizmo ──────────────────────────────────────────────────────────────────────────────────────────────

        protected virtual void OnSceneGUI()
        {
            VirtualScrollViewBase view = View;
            if (view == null) return;
            var scrollRect = view.GetComponent<ScrollRect>();
            if (scrollRect == null || scrollRect.content == null) return;
            RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;
            Rect rect = viewport.rect;
            ScrollDirection direction = view.Direction;
            bool vertical = direction.IsVertical();

            if (GizmoViewport)
            {
                Handles.color = UIEditorStyles.Accent;
                DrawLocalRect(viewport, rect, dotted: false);
            }

            if (GizmoOverscan && view.Overscan > 0f)
            {
                Handles.color = new Color(UIEditorStyles.Accent.r, UIEditorStyles.Accent.g, UIEditorStyles.Accent.b, 0.6f);
                Rect extended = vertical
                    ? new Rect(rect.x, rect.y - view.Overscan, rect.width, rect.height + 2f * view.Overscan)
                    : new Rect(rect.x - view.Overscan, rect.y, rect.width + 2f * view.Overscan, rect.height);
                DrawLocalRect(viewport, extended, dotted: true);
            }

            if (GizmoPivot)
            {
                float pivot = view.Snap.ViewportPivot;
                Vector3 from;
                Vector3 to;
                switch (direction)
                {
                    case ScrollDirection.BottomToTop:
                        from = new Vector3(rect.xMin, rect.yMin + pivot * rect.height);
                        to = new Vector3(rect.xMax, from.y);
                        break;
                    case ScrollDirection.LeftToRight:
                        from = new Vector3(rect.xMin + pivot * rect.width, rect.yMin);
                        to = new Vector3(from.x, rect.yMax);
                        break;
                    case ScrollDirection.RightToLeft:
                        from = new Vector3(rect.xMax - pivot * rect.width, rect.yMin);
                        to = new Vector3(from.x, rect.yMax);
                        break;
                    default:
                        from = new Vector3(rect.xMin, rect.yMax - pivot * rect.height);
                        to = new Vector3(rect.xMax, from.y);
                        break;
                }
                Handles.color = new Color(1f, 0.35f, 0.75f, 1f);
                Handles.DrawAAPolyLine(3f, viewport.TransformPoint(from), viewport.TransformPoint(to));
            }

            if (GizmoIndices)
            {
                int drawn = 0;
                foreach (VirtualItem item in view.ActiveItems)
                {
                    if (item == null || drawn++ > 80) continue;
                    Vector3 center = item.RectTransform.TransformPoint(item.RectTransform.rect.center);
                    Handles.Label(center, "#" + item.Index, EditorStyles.whiteBoldLabel);
                }
            }
        }

        private static void DrawLocalRect(RectTransform space, Rect rect, bool dotted)
        {
            Vector3 a = space.TransformPoint(new Vector3(rect.xMin, rect.yMin));
            Vector3 b = space.TransformPoint(new Vector3(rect.xMin, rect.yMax));
            Vector3 c = space.TransformPoint(new Vector3(rect.xMax, rect.yMax));
            Vector3 d = space.TransformPoint(new Vector3(rect.xMax, rect.yMin));
            if (dotted)
            {
                Handles.DrawDottedLines(new[] { a, b, b, c, c, d, d, a }, 4f);
            }
            else
            {
                Handles.DrawAAPolyLine(2f, a, b, c, d, a);
            }
        }
    }
}
