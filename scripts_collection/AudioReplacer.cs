using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public class AudioReplacer : EditorWindow
{
    private string _owningDumpPath = "";
    private string _sourceDumpPath = "";

    [MenuItem("Tools/08 Replace Audio", priority = 8)]
    public static void ShowWindow()
    {
        GetWindow<AudioReplacer>("Audio Replacer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Required \n必填", EditorStyles.boldLabel);

        _sourceDumpPath = DragAndDropFileField("Source Dump File* \n源导出文件*", _sourceDumpPath, "json");
        GUILayout.Space(5);
        _owningDumpPath = DragAndDropFileField("My Dump File* \n我的导出文件*", _owningDumpPath, "json");

        GUILayout.Space(35);

        GUI.enabled = !string.IsNullOrWhiteSpace(_sourceDumpPath) && !string.IsNullOrWhiteSpace(_owningDumpPath);
        if (GUILayout.Button("Run (运行)"))
        {
            ReplaceDump();
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

    private void ReplaceDump()
    {
        /*
        "m_Name": source,
        "m_LoadType": source,
        "m_Channels": my,
        "m_Frequency": my,
        "m_BitsPerSample": my,
        "m_Length": my,
        "m_IsTrackerFormat": source,
        "m_Ambisonic": source,
        "m_SubsoundIndex": my,
        "m_PreloadAudioData": source,
        "m_LoadInBackground": source,
        "m_Legacy3D": source,
        "m_Resource": {
            "m_Source": "archive:/CAB-[source]/[my].resource",
            "m_Offset": my,
            "m_Size": my
        },
        "m_CompressionFormat": source
        */

        JObject sourceDumpJson = JObject.Parse(File.ReadAllText(_sourceDumpPath));
        JObject owningDumpJson = JObject.Parse(File.ReadAllText(_owningDumpPath));

        owningDumpJson["m_Name"] = sourceDumpJson["m_Name"];
        owningDumpJson["m_LoadType"] = sourceDumpJson["m_LoadType"];
        owningDumpJson["m_IsTrackerFormat"] = sourceDumpJson["m_IsTrackerFormat"];
        owningDumpJson["m_Ambisonic"] = sourceDumpJson["m_Ambisonic"];
        owningDumpJson["m_PreloadAudioData"] = sourceDumpJson["m_PreloadAudioData"];
        owningDumpJson["m_LoadInBackground"] = sourceDumpJson["m_LoadInBackground"];
        owningDumpJson["m_Legacy3D"] = sourceDumpJson["m_Legacy3D"];
        owningDumpJson["m_CompressionFormat"] = sourceDumpJson["m_CompressionFormat"];
        owningDumpJson["m_Resource"]["m_Source"] = GetNewReS(
            Path.GetFileNameWithoutExtension(sourceDumpJson["m_Resource"]["m_Source"].ToString()),
            Path.GetFileNameWithoutExtension(owningDumpJson["m_Resource"]["m_Source"].ToString())
        );

        File.WriteAllText(_owningDumpPath, owningDumpJson.ToString());
    }

    private string GetNewReS(string sourceCabString, string owningCabString)
    {
        return $"archive:/{sourceCabString}/{owningCabString}.resource";
    }
}
