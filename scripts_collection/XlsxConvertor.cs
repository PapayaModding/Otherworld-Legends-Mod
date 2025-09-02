using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class XlsxConvertor : EditorWindow
{
    private string _xlsxPath;
    private string _textPath;
    private string _savePath = "External/XlsxConvector";

    [MenuItem("Tools/Xlsx Convertor")]
    public static void ShowWindow()
    {
        GetWindow<XlsxConvertor>("Xlsx Convertor");
    }

    private void OnGUI()
    {
        _xlsxPath = DragAndDropFileField("Xlsx", _xlsxPath, "xlsx");
        _textPath = DragAndDropFileField("Text", _textPath, "txt");
        _savePath = DragAndDropFolderField("Save Path", _savePath);

        GUILayout.Space(35);

        GUI.enabled = !string.IsNullOrWhiteSpace(_xlsxPath) || !string.IsNullOrWhiteSpace(_textPath);
        if (GUILayout.Button("Run (运行)") && (!string.IsNullOrWhiteSpace(_xlsxPath) || !string.IsNullOrWhiteSpace(_textPath)))
        {
            Process();
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

    private string DragAndDropFolderField(string label, string path)
    {
        GUILayout.Label(label);
        Rect dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, string.IsNullOrEmpty(path) ? "Drag folder here or browse... \n拖放文件夹或搜索..." : path);

        Event evt = Event.current;
        if ((evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform) && dropArea.Contains(evt.mousePosition))
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var draggedPath in DragAndDrop.paths)
                {
                    if (Directory.Exists(draggedPath))
                    {
                        path = draggedPath;
                        GUI.changed = true;
                        break;
                    }
                }
            }
            evt.Use();
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Browse (搜索)"))
        {
            string selected = EditorUtility.OpenFolderPanel("Select " + label, path, "");
            if (!string.IsNullOrEmpty(selected))
                path = selected;
        }
        GUILayout.EndHorizontal();

        return path;
    }

    private void Process()
    {
        if (!string.IsNullOrWhiteSpace(_textPath))
        {
            DecodeTxtToXLSX(Path.Combine(_savePath, "Decoded_Xlsx", Path.GetFileNameWithoutExtension(_textPath) + ".xlsx"));
        }
        if (!string.IsNullOrWhiteSpace(_xlsxPath))
        {
            EncodeXLSXToTxt(Path.Combine(_savePath, "Encoded_Txt", Path.GetFileNameWithoutExtension(_xlsxPath) + ".txt"));
        }
    }

    private void DecodeTxtToXLSX(string savePath)
    {
        if (!Directory.Exists(Path.GetDirectoryName(savePath)))
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));

        try
        {
            // Read the Base64 text
            string base64Text = File.ReadAllText(_textPath);

            // Decode Base64 to bytes
            byte[] xlsxBytes = Convert.FromBase64String(base64Text);

            // Save as .xlsx
            File.WriteAllBytes(savePath, xlsxBytes);

            Debug.Log($"Decoded XLSX saved to: {savePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to decode XLSX: {e}");
        }
    }

    private void EncodeXLSXToTxt(string savePath)
    {
        if (!Directory.Exists(Path.GetDirectoryName(savePath)))
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));

        if (!File.Exists(_xlsxPath))
        {
            Debug.LogError($"File not found: {_xlsxPath}");
            return;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(_xlsxPath);
            string base64 = Convert.ToBase64String(bytes);
            File.WriteAllText(savePath, base64);

            Debug.Log($"Encoded TXT saved to: {savePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to encode TXT: {e}");
        }
    }
}
