using UnityEngine;
using UnityEditor;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Newtonsoft.Json;


public class SpriteDumpFixer : EditorWindow
{
    private string _owningSpriteDumpsFolderPath = "";
    private string _sourceSpriteDumpsFolderPath = "";

    [MenuItem("Tools/04 Fix Dump Files (Sprites)", priority = 4)]
    public static void ShowWindow()
    {
        GetWindow<SpriteDumpFixer>("Fix Dump Files");
    }

    private void OnGUI()
    {
        GUIStyle style = new(GUI.skin.label)
        {
            wordWrap = true,
        };
        _owningSpriteDumpsFolderPath = DragAndDropFolderField("My Sprite Dumps Folder*\n我的图像导出文件夹*", _owningSpriteDumpsFolderPath);
        _sourceSpriteDumpsFolderPath = DragAndDropFolderField("Source Sprite Dumps Folder*\n源图像导出文件夹*", _sourceSpriteDumpsFolderPath);
        GUILayout.Space(5);

        GUI.enabled = !string.IsNullOrWhiteSpace(_owningSpriteDumpsFolderPath) &&
                        !string.IsNullOrWhiteSpace(_sourceSpriteDumpsFolderPath);

        if (GUILayout.Button("Run (运行)"))
        {
            FixNamesOfSpriteDumps();
            FixPathID();
        }

        GUI.enabled = true;

        GUILayout.Space(35);

        string messageEn = "";
        string messageZh = "";

        GUILayout.Label(messageEn, style);
        GUILayout.Label(messageZh, style);
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

    private (string, string) GetBaseNameFullName(string dumpName)
    {
        static (string, string) SplitByLastRegex(string input, string pattern)
        {
            MatchCollection matches = Regex.Matches(input, pattern);
            if (matches.Count == 0)
                return (input, ""); // No match, return full string and empty

            Match lastMatch = matches[^1];
            int splitIndex = lastMatch.Index;

            return (input[..splitIndex], input[splitIndex..]);
        }

        (string, string) splitCab = SplitByLastRegex(Path.GetFileName(dumpName).Replace(".json", ""), "-CAB-");
        return new(splitCab.Item1, dumpName);
    }

    private void FixNamesOfSpriteDumps()
    {
        int numOfSuccess = 0;
        int numOfError = 0;
        Dictionary<string, string> sourceBaseFullName = new();
        string[] sourceJsonFiles = Directory.GetFiles(_sourceSpriteDumpsFolderPath, "*.json", SearchOption.TopDirectoryOnly);
        foreach (string jsonFile in sourceJsonFiles)
        {
            JObject spriteJson = JObject.Parse(File.ReadAllText(jsonFile));
            if (spriteJson.ContainsKey("m_Rect")) // Make sure it's a Sprite Dump File
            {
                string fileName = Path.GetFileName(jsonFile);
                (string, string) baseNameFullName = GetBaseNameFullName(fileName);
                string baseName = baseNameFullName.Item1;
                string fullName = baseNameFullName.Item2;
                sourceBaseFullName.Add(baseName, fullName);
            }
        }

        string[] owningJsonFiles = Directory.GetFiles(_owningSpriteDumpsFolderPath, "*.json", SearchOption.TopDirectoryOnly);
        foreach (string jsonFile in owningJsonFiles)
        {
            JObject spriteJson = JObject.Parse(File.ReadAllText(jsonFile));
            if (spriteJson.ContainsKey("m_Rect")) // Make sure it's a Sprite Dump File
            {
                string fileName = Path.GetFileName(jsonFile);
                (string, string) baseNameFullName = GetBaseNameFullName(fileName);
                string baseName = baseNameFullName.Item1;
                if (sourceBaseFullName.ContainsKey(baseName))
                {
                    numOfSuccess++;
                    AssetDatabase.RenameAsset(jsonFile, sourceBaseFullName[baseName]);
                }
                else
                {
                    numOfError++;
                    Debug.LogError($"Missing Source Sprite Dump File for {baseName}.");
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Fixed {numOfSuccess} names of Sprite Dump Files in {_owningSpriteDumpsFolderPath}. Found {numOfError} error(s).");
    }

    private void FixPathID()
    {
        string[] sourceJsonFiles = Directory.GetFiles(_sourceSpriteDumpsFolderPath, "*.json", SearchOption.TopDirectoryOnly);
        long? replacePathID = null;
        foreach (string jsonFile in sourceJsonFiles)
        {
            JObject spriteJson = JObject.Parse(File.ReadAllText(jsonFile));
            if (spriteJson.ContainsKey("m_Rect")) // Make sure it's a Sprite Dump File
            {
                replacePathID = long.Parse(spriteJson["m_RD"]["texture"]["m_PathID"].ToString());
                break;
            }
        }

        if (replacePathID == null)
        {
            Debug.LogError($"No Source Sprite Dump File found in {_sourceSpriteDumpsFolderPath}");
            return;
        }

        int numOfSuccess = 0;
        string[] jsonFiles = Directory.GetFiles(_owningSpriteDumpsFolderPath, "*.json", SearchOption.TopDirectoryOnly);
        foreach (string jsonFile in jsonFiles)
        {
            JObject spriteJson = JObject.Parse(File.ReadAllText(jsonFile));
            if (spriteJson.ContainsKey("m_Rect")) // Make sure it's a Sprite Dump File
            {
                spriteJson["m_RD"]["texture"]["m_PathID"] = replacePathID;
                numOfSuccess++;
            }
            File.WriteAllText(jsonFile, JsonConvert.SerializeObject(spriteJson, Formatting.Indented));
        }

        Debug.Log($"Fixed Path ID of {numOfSuccess} Sprite Dump Files in {_owningSpriteDumpsFolderPath}.");
    }
}
