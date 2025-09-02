using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;


public class SpriteAtlasDumpFixer : EditorWindow
{
    private string _sourceAtlasFilePath = "";
    private string _owningAtlasFilePath = "";
    private string _owningFolderPath = "";
    private string _sourceFolderPath = "";

    [MenuItem("Tools/06 Fix Dump Files (Atlas)", priority = 6)]
    public static void ShowWindow()
    {
        GetWindow<SpriteAtlasDumpFixer>("SpriteAtlas Dump Fixer");
    }

    private void OnGUI()
    {
        GUIStyle style = new(GUI.skin.label)
        {
            wordWrap = true
        };
        GUIStyle boldWrapStyle = new(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        _owningFolderPath = DragAndDropFolderField("My Sprite Dump Folder*\n我的图像导出文件夹*", _owningFolderPath); // Required
        _sourceFolderPath = DragAndDropFolderField("Source Sprite Dump Folder*\n源图像导出文件夹*", _sourceFolderPath);
        GUILayout.Space(35);

        GUILayout.Label("Don't fill if Source Sprite Dump Folder contains this file\n如果源图像导出文件夹里包含这个文件则不用填", boldWrapStyle);
        _sourceAtlasFilePath = DragAndDropFileField("Source Atlas Dump\n源自动图集导出", _sourceAtlasFilePath, "json");
        GUILayout.Label("Don't fill if My Sprite Dump Folder contains this file\n如果我的图像导出文件夹里包含这个文件则不用填", boldWrapStyle);
        _owningAtlasFilePath = DragAndDropFileField("My Atlas Dump\n我的自动图集导出", _owningAtlasFilePath, "json");
        GUILayout.Space(5);

        GUI.enabled = !string.IsNullOrWhiteSpace(_owningFolderPath) && !string.IsNullOrWhiteSpace(_sourceFolderPath);

        if (GUILayout.Button("Run (运行)"))
        {
            _owningAtlasFilePath = GetOwningAtlasFilePath();
            if (string.IsNullOrWhiteSpace(_owningAtlasFilePath))
                Debug.LogError("Could not find My Atlas Dump File. Please make sure you assign one.");

            _sourceAtlasFilePath = GetSourceAtlasFilePath();
            if (string.IsNullOrWhiteSpace(_sourceAtlasFilePath))
                Debug.LogError("Could not find Source Atlas Dump File. Please make sure you assign one.");

            ReplaceRenderDataMapPathID();
            FixSpriteAtlasPathIDInDump();
            FixSpriteDumpNames();
            CopyIDNameFromSource();
        }

        GUI.enabled = true;

        GUILayout.Space(35);

        string messageEn = "";
        string messageZh = "";

        GUILayout.Label(messageEn, style);
        GUILayout.Label(messageZh, style);
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

    private string GetOwningAtlasFilePath()
    {
        if (!string.IsNullOrWhiteSpace(_owningAtlasFilePath))
        {
            return _owningAtlasFilePath;
        }
        string[] jsonFiles = Directory.GetFiles(_owningFolderPath, "*.json", SearchOption.TopDirectoryOnly);
        foreach (string jsonFile in jsonFiles)
        {
            JObject spriteJson = JObject.Parse(File.ReadAllText(jsonFile));
            if (spriteJson.ContainsKey("m_PackedSprites")) // is SpriteAtlas Dump File
            {
                return jsonFile;
            }
        }
        return "";
    }

    private string GetSourceAtlasFilePath()
    {
        if (!string.IsNullOrWhiteSpace(_sourceAtlasFilePath))
        {
            return _sourceAtlasFilePath;
        }
        string[] jsonFiles = Directory.GetFiles(_sourceFolderPath, "*.json", SearchOption.TopDirectoryOnly);
        foreach (string jsonFile in jsonFiles)
        {
            JObject spriteJson = JObject.Parse(File.ReadAllText(jsonFile));
            if (spriteJson.ContainsKey("m_PackedSprites")) // is SpriteAtlas Dump File
            {
                return jsonFile;
            }
        }
        return "";
    }

    private void CopyIDNameFromSource()
    {
        JObject _sourceAtlasFileJson = JObject.Parse(File.ReadAllText(_sourceAtlasFilePath));
        JObject _owningAtlasFileJson = JObject.Parse(File.ReadAllText(_owningAtlasFilePath));
        _owningAtlasFileJson["m_PackedSprites"] = _sourceAtlasFileJson["m_PackedSprites"];
        _owningAtlasFileJson["m_PackedSpriteNamesToIndex"] = _sourceAtlasFileJson["m_PackedSpriteNamesToIndex"];
        File.WriteAllText(_owningAtlasFilePath, JsonConvert.SerializeObject(_owningAtlasFileJson, Formatting.Indented));
        Debug.Log($"Finished copying m_PackedSprites and m_PackedSpriteNamesToIndex fields from {_owningAtlasFilePath} to {_sourceAtlasFilePath}");
    }

    private void ReplaceRenderDataMapPathID()
    {
        JObject _sourceAtlasFileJson = JObject.Parse(File.ReadAllText(_sourceAtlasFilePath));
        JObject _owningAtlasFileJson = JObject.Parse(File.ReadAllText(_owningAtlasFilePath));

        var renderDataMapSource = _sourceAtlasFileJson["m_RenderDataMap"]["Array"];
        var pathIDToChange = renderDataMapSource[0]["second"]["texture"]["m_PathID"];

        var renderDataMapOwning = _owningAtlasFileJson["m_RenderDataMap"]["Array"];
        for (int i = 0; i < renderDataMapOwning.Count(); i++)
        {
            renderDataMapOwning[i]["second"]["texture"]["m_PathID"] = pathIDToChange;
        }

        _owningAtlasFileJson["m_RenderDataMap"]["Array"] = renderDataMapOwning;
        File.WriteAllText(_owningAtlasFilePath, JsonConvert.SerializeObject(_owningAtlasFileJson, Formatting.Indented));
        Debug.Log($"Fixed Render Data Map Path ID in {_owningAtlasFilePath}.");
    }

    private (string, long) GetNamePathIDFromDumpName(string dumpName)
    {
        string[] splitCab = Path.GetFileName(dumpName).Replace(".json", "").Split("-CAB-");
        string name = splitCab[0];
        string[] splitted = splitCab[1].Split("-");
        long absPathID = long.Parse(splitted[splitted.Length - 1].ToString());
        long pathID = absPathID;
        if (splitted.Length == 3)
        {  // This Path ID is negative
            pathID = -absPathID;
        }
        else if (splitted.Length != 2)
        {
            Debug.LogError($"{name} does not fit format.");
            pathID = -1;
        }
        return new(name, pathID);
    }

    private void FixSpriteAtlasPathIDInDump()
    {
        (string, long) sourceAtlasNamePathID = GetNamePathIDFromDumpName(Path.GetFileName(_sourceAtlasFilePath));
        long sourceAtlasPathID = sourceAtlasNamePathID.Item2;
        string[] jsonFiles = Directory.GetFiles(_owningFolderPath, "*.json", SearchOption.TopDirectoryOnly);
        int counter = 0;
        foreach (string jsonFile in jsonFiles)
        {
            JObject spriteJson = JObject.Parse(File.ReadAllText(jsonFile));
            if (spriteJson.ContainsKey("m_Rect")) // Make sure it's a Sprite Dump File
            {
                spriteJson["m_SpriteAtlas"]["m_PathID"] = sourceAtlasPathID;
                counter++;
            }
            File.WriteAllText(jsonFile, JsonConvert.SerializeObject(spriteJson, Formatting.Indented));
        }
        Debug.Log($"Fixed SpriteAtlas Path ID in {counter} dump files");
    }

    // Atlas
    // private Dictionary<int, long> GetSourceIndex2PathID()
    // {
    //     Dictionary<int, long> result = new();
    //     JObject sourceAtlasFileJson = JObject.Parse(File.ReadAllText(_sourceAtlasFilePath));
    //     var packedSpritesSource = sourceAtlasFileJson["m_PackedSprites"]["Array"];
    //     for (int i = 0; i < packedSpritesSource.Count(); i++)
    //     {
    //         var pathID = long.Parse(packedSpritesSource[i]["m_PathID"].ToString());
    //         result[i] = pathID;
    //     }
    //     return result;
    // }

    // private Dictionary<int, long> GetOwningIndex2PathID()
    // {
    //     Dictionary<int, long> result = new();
    //     JObject owningAtlasFileJson = JObject.Parse(File.ReadAllText(_owningAtlasFilePath));
    //     var packedSpritesSource = owningAtlasFileJson["m_PackedSprites"]["Array"];
    //     for (int i = 0; i < packedSpritesSource.Count(); i++)
    //     {
    //         var pathID = long.Parse(packedSpritesSource[i]["m_PathID"].ToString());
    //         result[i] = pathID;
    //     }
    //     return result;
    // }

    // private Dictionary<long, int> GetOwningPathID2Index()
    // {
    //     Dictionary<int, long> index2PathID = GetOwningIndex2PathID();
    //     return index2PathID.ToDictionary(pair => pair.Value, pair => pair.Key);
    // }

    private (string, string) GetBaseNameFullNameSplitCab(string dumpName)
    {
        (string, string) splitCab = SplitByLastRegex(Path.GetFileName(dumpName).Replace(".json", ""), "-CAB-");
        return new(splitCab.Item1, dumpName);
    }

    private (string, string) GetBaseNameFullNameSplitHashtag(string fileName)
    {
        (string, string) splitHashtag = SplitByLastRegex(fileName, " #");
        return new(splitHashtag.Item1, splitHashtag.Item2);
    }

    private static (string, string) SplitByLastRegex(string input, string pattern)
    {
        MatchCollection matches = Regex.Matches(input, pattern);
        if (matches.Count == 0)
            return (input, ""); // No match, return full string and empty

        Match lastMatch = matches[^1];
        int splitIndex = lastMatch.Index;

        return (input[..splitIndex], input[splitIndex..]);
    }

    // Known bug: Cannot handle Special situation: multiple Sprites with the same name
    private void FixSpriteDumpNames()
    {
        int numOfSuccess = 0;
        int numOfError = 0;

        // Get Source Project ID
        string projectID = "";
        string pattern = @"-CAB-([a-fA-F0-9]{32})-";
        Match match = Regex.Match(_sourceAtlasFilePath, pattern);
        if (match.Success)
        {
            projectID = match.Groups[1].Value;
        }
        if (projectID == "")
        {
            Debug.LogError($"{_sourceAtlasFilePath} is an invalid Source Atlas Dump Path.");
        }

        Dictionary<string, List<string>> sourceBaseFullNames = new();
        string[] sourceJsonFiles = Directory.GetFiles(_sourceFolderPath, "*.json", SearchOption.TopDirectoryOnly);
        int counter = 0;
        foreach (string jsonFile in sourceJsonFiles)
        {
            JObject spriteJson = JObject.Parse(File.ReadAllText(jsonFile));
            if (!spriteJson.ContainsKey("m_Rect")) // Not a Sprite Dump File
                continue;
            string fileName = Path.GetFileName(jsonFile);
            (string, string) baseNameFullName = GetBaseNameFullNameSplitCab(fileName);
            string baseName = baseNameFullName.Item1;
            string fullName = baseNameFullName.Item2;
            if (!sourceBaseFullNames.ContainsKey(baseName))
            {
                sourceBaseFullNames.Add(baseName, new() { fullName });
            }
            else
            {
                sourceBaseFullNames[baseName].Add(fullName);
            }
            counter++;
        }

        // Dictionary<long, int> owningPathID2Index = GetOwningPathID2Index();
        // Dictionary<int, long> sourceIndex2PathID = GetSourceIndex2PathID();

        // Issue: two json files might have the same basename

        string[] owningJsonFiles = Directory.GetFiles(_owningFolderPath, "*.json", SearchOption.TopDirectoryOnly);
        foreach (string jsonFile in owningJsonFiles)
        {
            JObject spriteJson = JObject.Parse(File.ReadAllText(jsonFile));
            if (!spriteJson.ContainsKey("m_Rect")) // Not a Sprite Dump File
                continue;
            string jsonFileName = Path.GetFileName(jsonFile);
            (string, long) namePathId = GetNamePathIDFromDumpName(jsonFileName);
            string owningName = namePathId.Item1;
            long owningPathID = namePathId.Item2;

            (string, string) baseNameFullName = GetBaseNameFullNameSplitHashtag(owningName);
            string baseName = baseNameFullName.Item1;

            // int owningIndex = owningPathID2Index[owningPathID];
            // long sourcePathID = sourceIndex2PathID[owningIndex];
            // string newFileName = owningName + "-CAB-" + projectID.ToString() + "-" + sourcePathID.ToString() + ".json";
            // Debug.Log(newFileName);
            // AssetDatabase.RenameAsset(jsonFile, newFileName);

            if (sourceBaseFullNames.ContainsKey(baseName))
            {
                string newFileName = sourceBaseFullNames[baseName][0];
                sourceBaseFullNames[baseName].RemoveAt(0);
                numOfSuccess++;
                AssetDatabase.RenameAsset(jsonFile, newFileName);
            }
            else
            {
                Debug.LogError($"Failed to process {owningName}, Path ID: {owningPathID} (my Sprite Dump).");
                numOfError++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Fixed {numOfSuccess} names of Sprite Dump Files in {_owningFolderPath}. Found {numOfError} error(s).");
    }
}
