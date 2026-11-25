using UnityEditor;
using UnityEngine;
using Turn.View;

namespace Turn.EditorTools
{
    /// <summary>
    /// 编辑器加载 / 脚本编译后自动检查：项目里没有 SubspaceColor Layer 就在第一个空的用户 Layer（8–31）里加上。
    /// 亚空间灰白画面里，放在这个 Layer 上的爆发 / 受击特效保持原色（见 SubspaceView）。
    /// 也可以手动执行菜单 Turn → 添加 SubspaceColor Layer。
    /// </summary>
    [InitializeOnLoad]
    public static class TurnSubspaceLayerSetup
    {
        static TurnSubspaceLayerSetup()
        {
            EditorApplication.delayCall += () => EnsureLayer(false);
        }

        [MenuItem("Turn/添加 SubspaceColor Layer（亚空间保留原色）", false, 30)]
        public static void AddFromMenu()
        {
            EnsureLayer(true);
        }

        private static void EnsureLayer(bool verbose)
        {
            string name = SubspaceView.ColorLayerName;
            if (LayerMask.NameToLayer(name) >= 0)
            {
                if (verbose) Debug.Log("[Turn] Layer " + name + " 已存在。");
                return;
            }
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            SerializedProperty layers = so.FindProperty("layers");
            if (layers == null) return;
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = name;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log("[Turn] 已添加 Layer " + i + "：" + name + "（亚空间里保留原色的特效用）。");
                return;
            }
            Debug.LogWarning("[Turn] 用户 Layer 已满，没法自动添加 " + name + "，请在 Project Settings → Tags and Layers 里手动腾一个。");
        }
    }
}
