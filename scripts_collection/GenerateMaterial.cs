using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class OneByOneMaterialWindow : EditorWindow
{
#if UNITY_EDITOR
    private Material selectedMaterial;

    [MenuItem("Tools/1x1 Material Generator")]
    public static void ShowWindow()
    {
        GetWindow<OneByOneMaterialWindow>("1x1 Material Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Convert Material to 1x1 Texture", EditorStyles.boldLabel);

        // Drag-and-drop field for the Material
        selectedMaterial = (Material)EditorGUILayout.ObjectField("Material", selectedMaterial, typeof(Material), false);

        GUILayout.Space(10);

        // Button to convert
        if (GUILayout.Button("Convert to 1x1 Texture"))
        {
            if (selectedMaterial == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a Material first!", "OK");
            }
            else
            {
                ConvertMaterialTo1x1(selectedMaterial);
            }
        }
    }

    private void ConvertMaterialTo1x1(Material original)
    {
        // Get main color
        Color color = Color.white;
        if (original.HasProperty("_Color"))
            color = original.color;

        // Create 1x1 Texture
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();

        // Backup original material
        string originalPath = AssetDatabase.GetAssetPath(original);
        string folderPath = System.IO.Path.GetDirectoryName(originalPath).Replace("\\", "/");
        string backupPath = AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + original.name + "_Backup.mat");
        AssetDatabase.CopyAsset(originalPath, backupPath);

        // Replace original material with 1x1 texture
        original.mainTexture = tex;
        if (original.HasProperty("_Color"))
            original.color = Color.white;

        // Save the texture in the same folder
        string texPath = AssetDatabase.GenerateUniqueAssetPath(folderPath + "/" + original.name + "_1x1Tex.asset");
        AssetDatabase.CreateAsset(tex, texPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Success", $"Original material backed up as '{System.IO.Path.GetFileName(backupPath)}' and replaced with 1x1 texture.", "OK");
    }
#endif
}
