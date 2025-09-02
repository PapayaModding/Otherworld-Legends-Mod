using UnityEditor;
using UnityEngine;
using System.IO;


public class AssetBundleBuilder : EditorWindow
{
    private BuildTarget _buildPlatform = BuildTarget.StandaloneWindows64;

    [MenuItem("Tools/03 Build Bundles", priority = 3)]
    public static void ShowWindow()
    {
        GetWindow<AssetBundleBuilder>("Build Bundles");
    }

    private void OnGUI()
    {
        GUIStyle style = new(GUI.skin.label)
        {
            wordWrap = true
        };

        GUILayout.Label("AssetBundle Build Settings", EditorStyles.boldLabel);
        GUILayout.Label("资源包设置", EditorStyles.boldLabel);
        GUILayout.Space(35);

        // Dropdown to change build target
        GUILayout.Label("Build Target", EditorStyles.boldLabel);
        _buildPlatform = (BuildTarget)EditorGUILayout.EnumPopup("对象平台", _buildPlatform);
        GUILayout.Space(5);

        if (GUILayout.Button("Run (运行)"))
        {
            BuildAllAssetBundles(_buildPlatform);
        }
        GUILayout.Space(35);

        string messageEn = "Pack *all* AssetBundles in the project. The best practice is to tag only " +
                            "AssetBundles that are necessary. In this way you can save some time and " +
                            "resources.";
        string messageZh = "为项目中所有的资源包进行打包。请只标注你目前所需的资源包。这样可以节省" +
                            "时间和资源。";

        GUILayout.Label(messageEn, style);
        GUILayout.Label(messageZh, style);
    }

    public static void BuildAllAssetBundles(BuildTarget target)
    {
        string path = "Assets/AssetBundles";
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        BuildPipeline.BuildAssetBundles(path, BuildAssetBundleOptions.None, target);

        Debug.Log("All assets has been built to Assets/AssetBundles.");
        Debug.Log("Please don't forget to assign bundle name.");
    }
}
