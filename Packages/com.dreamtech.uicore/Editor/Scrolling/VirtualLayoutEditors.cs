using DreamTech.UICore.Editor.Styles;
using DreamTech.UICore.Scrolling;
using DreamTech.UICore.Scrolling.Layout;
using UnityEditor;
using UnityEngine;

namespace DreamTech.UICore.Editor.Scrolling
{
    [CustomEditor(typeof(VirtualListView))]
    internal sealed class VirtualListViewEditor : VirtualScrollViewEditorBase
    {
        private static readonly string[] SizeModeLabels = { "Cố định", "Theo template", "Adapter báo", "Tự đo" };
        private static readonly string[] CrossModeLabels = { "Kéo giãn", "Cỡ template" };
        private static readonly string[] AlignmentLabels = { "Đầu", "Giữa", "Cuối" };

        protected override string HeaderTitle => "Virtual List View";

        protected override void DrawSpacing()
        {
            SerializedProperty spacing = serializedObject.FindProperty("_spacing");
            EditorGUILayout.PropertyField(spacing, new GUIContent("Khoảng cách giữa ô", spacing.tooltip));
        }

        protected override void DrawLayoutSpecific()
        {
            SerializedProperty sizeMode = serializedObject.FindProperty("_sizeMode");
            EditorGUILayout.LabelField("Cỡ theo trục cuộn", EditorStyles.miniBoldLabel);
            sizeMode.enumValueIndex = GUILayout.Toolbar(sizeMode.enumValueIndex, SizeModeLabels, GUILayout.Height(22f));
            EditorGUILayout.LabelField(SizeModeHint((ListItemSizeMode)sizeMode.enumValueIndex), UIEditorStyles.MutedLabel);

            switch ((ListItemSizeMode)sizeMode.enumValueIndex)
            {
                case ListItemSizeMode.Fixed:
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_fixedSize"), new GUIContent("Cỡ mỗi ô (px)"));
                    break;
                case ListItemSizeMode.Measure:
                    SerializedProperty estimate = serializedObject.FindProperty("_estimatedSize");
                    EditorGUILayout.PropertyField(estimate, new GUIContent("Cỡ ước lượng (px)", estimate.tooltip));
                    DrawHelpCard("Gốc của template cần LayoutGroup / LayoutElement / chữ để báo cỡ ưu tiên. Ảnh nền (Image) không được tính. " +
                                 "Nội dung ô đổi cỡ sau khi hiện thì gọi item.NotifySizeChanged().", HelpType.Info);
                    break;
            }

            EditorGUILayout.Space(4f);
            SerializedProperty crossMode = serializedObject.FindProperty("_crossSizeMode");
            EditorGUILayout.LabelField("Cỡ theo trục phụ", EditorStyles.miniBoldLabel);
            crossMode.enumValueIndex = GUILayout.Toolbar(crossMode.enumValueIndex, CrossModeLabels, GUILayout.Height(22f));
            if (crossMode.enumValueIndex == (int)ListCrossSizeMode.Template)
            {
                SerializedProperty alignment = serializedObject.FindProperty("_crossAlignment");
                alignment.enumValueIndex = GUILayout.Toolbar(alignment.enumValueIndex, AlignmentLabels);
            }
        }

        protected override string LayoutSummary()
        {
            var list = (VirtualListView)target;
            return $"{list.ItemCount} ô · cỡ: {SizeModeLabels[(int)list.SizeMode]} · dài {list.ContentLength:0} px";
        }

        private static string SizeModeHint(ListItemSizeMode mode)
        {
            switch (mode)
            {
                case ListItemSizeMode.Fixed: return "Mọi ô cùng cỡ — nhanh nhất.";
                case ListItemSizeMode.Template: return "Cỡ của template ứng với loại ô (tiêu đề nhóm + dòng, mỗi loại một cỡ).";
                case ListItemSizeMode.Adapter: return "Adapter báo cỡ từng ô (IVirtualItemSizeSource / sizeOf); báo ≤ 0 thì dùng cỡ template.";
                default: return "Đo khi ô hiện ra; list giữ nguyên chỗ đang xem khi ô phía trên đổi cỡ.";
            }
        }
    }

    [CustomEditor(typeof(VirtualGridView))]
    internal sealed class VirtualGridViewEditor : VirtualScrollViewEditorBase
    {
        private static readonly string[] ConstraintLabels = { "Tự vừa bề rộng", "Số ô cố định" };
        private static readonly string[] AlignmentLabels = { "Đầu", "Giữa", "Cuối" };

        protected override string HeaderTitle => "Virtual Grid View";

        protected override string SpecificSectionTitle => "Lưới";

        protected override void DrawSpacing()
        {
            SerializedProperty spacing = serializedObject.FindProperty("_spacing");
            EditorGUILayout.PropertyField(spacing, new GUIContent("Khoảng cách (ngang × dọc)", spacing.tooltip));
        }

        protected override void DrawLayoutSpecific()
        {
            var grid = (VirtualGridView)target;
            SerializedProperty fromTemplate = serializedObject.FindProperty("_cellSizeFromTemplate");
            EditorGUILayout.PropertyField(fromTemplate, new GUIContent("Cỡ ô theo template 0", fromTemplate.tooltip));
            using (new EditorGUI.DisabledScope(fromTemplate.boolValue))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_cellSize"), new GUIContent("Cỡ ô (rộng × cao)"));
            }
            if (fromTemplate.boolValue)
            {
                Vector2 cell = grid.EffectiveCellSize;
                EditorGUILayout.LabelField($"Đang dùng {cell.x:0} × {cell.y:0}", UIEditorStyles.MutedLabel);
            }

            EditorGUILayout.Space(4f);
            SerializedProperty constraint = serializedObject.FindProperty("_constraint");
            EditorGUILayout.LabelField(grid.Direction.IsVertical() ? "Số ô mỗi hàng" : "Số ô mỗi cột", EditorStyles.miniBoldLabel);
            constraint.enumValueIndex = GUILayout.Toolbar(constraint.enumValueIndex, ConstraintLabels, GUILayout.Height(22f));
            if (constraint.enumValueIndex == (int)GridConstraint.FixedCount)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_constraintCount"), new GUIContent("Số ô"));
            }
            else
            {
                EditorGUILayout.LabelField($"≈ {PredictItemsPerLine(grid)} ô với bề rộng hiện tại {grid.CrossLength:0} px",
                                           UIEditorStyles.MutedLabel);
            }

            EditorGUILayout.Space(4f);
            SerializedProperty alignment = serializedObject.FindProperty("_alignment");
            EditorGUILayout.LabelField("Căn cả khối", EditorStyles.miniBoldLabel);
            alignment.enumValueIndex = GUILayout.Toolbar(alignment.enumValueIndex, AlignmentLabels);
            SerializedProperty lastLine = serializedObject.FindProperty("_lastLineAlignment");
            EditorGUILayout.LabelField("Căn dòng cuối (khi thiếu ô)", EditorStyles.miniBoldLabel);
            lastLine.enumValueIndex = GUILayout.Toolbar(lastLine.enumValueIndex, AlignmentLabels);
        }

        protected override string LayoutSummary()
        {
            var grid = (VirtualGridView)target;
            return $"{grid.ItemCount} ô · {grid.ItemsPerLine} ô mỗi dòng · {grid.LineTotal} dòng · dài {grid.ContentLength:0} px";
        }

        private static int PredictItemsPerLine(VirtualGridView grid)
        {
            var model = new GridLayoutModel();
            Vector2 cell = grid.EffectiveCellSize;
            bool vertical = grid.Direction.IsVertical();
            ScrollPadding padding = grid.Padding;
            model.Configure(vertical ? cell.y : cell.x, vertical ? cell.x : cell.y, vertical ? grid.Spacing.y : grid.Spacing.x,
                            vertical ? grid.Spacing.x : grid.Spacing.y, padding.Start, padding.End, padding.CrossStart,
                            padding.CrossEnd, grid.Constraint, grid.ConstraintCount, grid.Alignment, grid.LastLineAlignment);
            model.SetCrossLength(grid.CrossLength);
            return model.ItemsPerLine;
        }
    }
}
