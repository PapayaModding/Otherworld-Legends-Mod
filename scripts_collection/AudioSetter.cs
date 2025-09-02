using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;


public class AudioSetter : EditorWindow
{
    private AudioClip _clip;
    private string _sourceDumpPath = "";

    [MenuItem("Tools/07 Set Audio", priority = 7)]
    public static void ShowWindow()
    {
        GetWindow<AudioSetter>("Audio Setter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Required \n必填");

        _sourceDumpPath = DragAndDropFileField("Source Dump File* \n源导出文件*", _sourceDumpPath, "json");
        GUILayout.Space(5);
        GUILayout.Label("AudioClip*");
        _clip = (AudioClip)EditorGUILayout.ObjectField("音频文件*", _clip, typeof(AudioClip), false);

        // GUI.enabled = IsValidPath(_sourceDumpPath) && _clip != null;
        GUI.enabled = !string.IsNullOrWhiteSpace(_sourceDumpPath) && _clip != null;

        GUILayout.Space(35);

        if (GUILayout.Button("Run (运行)"))
        {
            Config();
        }
        GUI.enabled = true;
    }

    private string DragAndDropFileField(string label, string path, string requiredExtension = null)
    {
        GUILayout.Label(label);
        Rect dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, string.IsNullOrEmpty(path) ? "Drag file here or browse... \n拖放文件或搜索..." : path);

        Event evt = Event.current;
        if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && dropArea.Contains(evt.mousePosition))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
                {
                    string draggedPath = AssetDatabase.GetAssetPath(obj);
                    string fullPath = Path.GetFullPath(draggedPath);

                    if (File.Exists(fullPath))
                    {
                        if (requiredExtension == null ||
                        fullPath.ToLower().EndsWith("." + requiredExtension))
                        {
                            path = fullPath;
                            GUI.changed = true;
                            break;
                        }
                    }
                }
            }
            evt.Use();
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Browse (搜索)"))
        {
            string directory = string.IsNullOrEmpty(path) ? "" : Path.GetDirectoryName(path);
            string selected = EditorUtility.OpenFilePanel("Select " + label, directory, "");
            if (!string.IsNullOrEmpty(selected))
                path = selected;
        }
        GUILayout.EndHorizontal();

        return path;
    }

    private void Config()
    {
        /*
        "m_Name": source,
        "m_LoadType": source,
        "m_Channels": my,
        "m_Frequency": my,
        "m_BitsPerSample": my,
        "m_Length": my,
        "m_IsTrackerFormat": unapplicable,
        "m_Ambisonic": source,
        "m_SubsoundIndex": my,
        "m_PreloadAudioData": source,
        "m_LoadInBackground": source,
        "m_Legacy3D": unapplicable,
        "m_Resource": {
            "m_Source": "unapplicable",
            "m_Offset": unapplicable,
            "m_Size": unapplicable
        },
        "m_CompressionFormat": source
        */

        JObject sourceDumpJson = JObject.Parse(File.ReadAllText(_sourceDumpPath));
        _clip.name = sourceDumpJson["m_Name"].ToString();

        string path = AssetDatabase.GetAssetPath(_clip);
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer != null)
        {
            importer.ambisonic = (bool)sourceDumpJson["m_Ambisonic"];
            importer.preloadAudioData = (bool)sourceDumpJson["m_PreloadAudioData"];
            importer.loadInBackground = (bool)sourceDumpJson["m_LoadInBackground"];

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = (AudioClipLoadType)int.Parse(sourceDumpJson["m_LoadType"].ToString());
            settings.compressionFormat = (AudioCompressionFormat)int.Parse(sourceDumpJson["m_CompressionFormat"].ToString());
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }
}
