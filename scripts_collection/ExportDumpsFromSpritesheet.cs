using UnityEditor;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System;

public class ExportDumpsFromSpritesheet : EditorWindow
{
    private readonly struct RenderDataKey
    {
        readonly uint firstData0;
        readonly uint firstData1;
        readonly uint firstData2;
        readonly uint firstData3;
        readonly long second;
        public RenderDataKey(uint firstData0, uint firstData1, uint firstData2, uint firstData3, long second)
        {
            this.firstData0 = firstData0;
            this.firstData1 = firstData1;
            this.firstData2 = firstData2;
            this.firstData3 = firstData3;
            this.second = second;
        }

        public bool Compare(RenderDataKey other)
        {
            return firstData0 == other.firstData0 &&
                firstData1 == other.firstData1 &&
                firstData2 == other.firstData2 &&
                firstData3 == other.firstData3 &&
                second == other.second;
        }

        public override string ToString()
        {
            return $"\"first\"[{firstData0}, {firstData1}, {firstData2}, {firstData3}], \"second\": {second}";
        }
    }

    private string _outputDumpPath = "";
    private string _sourceDumpsFolderPath = "";
    private Texture2D _targetTexture;
    private string _sourceAtlasFilePath = "";

    [MenuItem("Tools/02 Export Dumps from Spritesheet (Atlas)", priority = 2)]
    public static void ShowWindow()
    {
        GetWindow<ExportDumpsFromSpritesheet>("Export Dumps from Spritesheet");
    }

    private void OnGUI()
    {
        GUIStyle style = new(GUI.skin.label)
        {
            wordWrap = true
        };

        GUILayout.Label("Required \n必填", EditorStyles.boldLabel);
        _outputDumpPath = DragAndDropFolderField("Output Dump Path*\n新导出文件夹*", _outputDumpPath);

        GUILayout.Label("Target Texture*");
        _targetTexture = (Texture2D)EditorGUILayout.ObjectField("材质文件*", _targetTexture, typeof(Texture2D), false);
        _sourceDumpsFolderPath = DragAndDropFolderField("Source Sprite Dump Folder (Json files)* \n源图像导出文件夹 (Json文件)*", _sourceDumpsFolderPath);

        GUILayout.Space(35);

        _sourceAtlasFilePath = DragAndDropFileField("Source SpriteAtlas File* \n源自动图集的导出文件*", _sourceAtlasFilePath, "json");

        GUI.enabled = _targetTexture != null &&
                        !string.IsNullOrWhiteSpace(_sourceDumpsFolderPath) &&
                        !string.IsNullOrWhiteSpace(_outputDumpPath) &&
                        !string.IsNullOrWhiteSpace(_sourceAtlasFilePath);

        if (GUILayout.Button("Run (运行)") && _sourceDumpsFolderPath != null)
        {
            EmptyOutputFolder();
            ModifySpritesheet();
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

    // Atlas
    private Dictionary<int, long> GetIndex2PathID()
    {
        Dictionary<int, long> result = new();
        JObject sourceAtlasFileJson = JObject.Parse(File.ReadAllText(_sourceAtlasFilePath));
        var packedSpritesSource = sourceAtlasFileJson["m_PackedSprites"]["Array"];
        for (int i = 0; i < packedSpritesSource.Count(); i++)
        {
            var pathID = long.Parse(packedSpritesSource[i]["m_PathID"].ToString());
            result[i] = pathID;
        }
        return result;
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

    // Atlas
    private Dictionary<long, int> GetPathID2Index()
    {
        Dictionary<int, long> index2PathID = GetIndex2PathID();
        return index2PathID.ToDictionary(pair => pair.Value, pair => pair.Key);
    }

    // Atlas
    private Dictionary<int, RenderDataKey> GetIndex2RenderDataKey()
    {
        Dictionary<long, int> pathID2Index = GetPathID2Index();
        Dictionary<int, RenderDataKey> result = new();
        string[] jsonFiles = Directory.GetFiles(_sourceDumpsFolderPath, "*.json", SearchOption.TopDirectoryOnly);
        foreach (string jsonFile in jsonFiles)
        {
            string jsonFileName = Path.GetFileName(jsonFile);
            (string, long) namePathId = GetNamePathIDFromDumpName(jsonFileName);
            long pathID = namePathId.Item2;
            JObject spriteJson = JObject.Parse(File.ReadAllText(jsonFile));
            if (spriteJson.ContainsKey("m_Rect"))
            {
                var rdk = spriteJson["m_RenderDataKey"];
                uint firstData0 = uint.Parse(rdk["first"]["data[0]"].ToString());
                uint firstData1 = uint.Parse(rdk["first"]["data[1]"].ToString());
                uint firstData2 = uint.Parse(rdk["first"]["data[2]"].ToString());
                uint firstData3 = uint.Parse(rdk["first"]["data[3]"].ToString());
                long second = long.Parse(rdk["second"].ToString());
                RenderDataKey renderDataKey = new(firstData0, firstData1, firstData2, firstData3, second);
                result[pathID2Index[pathID]] = renderDataKey;
            }
        }
        return result;
    }

    // Atlas
    private int SearchIndexOfRenderDataKey(List<RenderDataKey> lst, RenderDataKey target)
    {
        int counter = 0;
        foreach (RenderDataKey rdk in lst)
        {
            if (rdk.Compare(target))
            {
                return counter;
            }
            counter++;
        }
        return -1;
    }

    // Atlas
    private List<RenderDataKey> GetRenderDataKeysFromJObject(JObject jObject)
    {
        var renderDataMap = jObject["m_RenderDataMap"]["Array"];
        List<RenderDataKey> renderDataKeys = new();
        for (int i = 0; i < renderDataMap.Count(); i++)
        {
            var rdk = renderDataMap[i];
            uint firstData0 = uint.Parse(rdk["first"]["first"]["data[0]"].ToString());
            uint firstData1 = uint.Parse(rdk["first"]["first"]["data[1]"].ToString());
            uint firstData2 = uint.Parse(rdk["first"]["first"]["data[2]"].ToString());
            uint firstData3 = uint.Parse(rdk["first"]["first"]["data[3]"].ToString());
            long second = long.Parse(rdk["first"]["second"].ToString());
            RenderDataKey renderDataKey = new(firstData0, firstData1, firstData2, firstData3, second);
            renderDataKeys.Add(renderDataKey);
        }
        return renderDataKeys;
    }

    // Atlas
    private Dictionary<int, int> GetIndex2ActualRenderDataKeyIndex()
    {
        Dictionary<int, int> result = new();
        Dictionary<int, RenderDataKey> index2RenderDataKey = GetIndex2RenderDataKey();
        JObject sourceAtlasFileJson = JObject.Parse(File.ReadAllText(_sourceAtlasFilePath));
        List<RenderDataKey> renderDataKeys = GetRenderDataKeysFromJObject(sourceAtlasFileJson);

        foreach (int index in index2RenderDataKey.Keys)
        {
            if (index2RenderDataKey.TryGetValue(index, out RenderDataKey rdk))
            {
                result[index] = SearchIndexOfRenderDataKey(renderDataKeys, rdk);
            }
        }

        return result;
    }

    private void ModifySpritesheet()
    {
        SpriteMetaData? GetMetaOfName(string name, List<SpriteMetaData> metas)
        {
            foreach (SpriteMetaData meta in metas)
            {
                if (meta.name == name)
                {
                    return meta;
                }
            }
            return null;
        }
        bool IsCloseFloat(float a, float b, float epsilon = 1e-4f)
        {
            return Math.Abs(a - b) <= epsilon;
        }

        List<SpriteMetaData> metas = GetMetasFromSpritesheet();

        // For pivot (m_Pivot) & offset (m_Offset) & border (m_Border), modify Sprite Dumps
        // For texture rect, modify atlas
        Dictionary<int, int> index2ActualRenderDataKeyIndex = GetIndex2ActualRenderDataKeyIndex();
        Dictionary<long, int> pathID2Index = GetPathID2Index();
        JObject sourceAtlasFileJson = JObject.Parse(File.ReadAllText(_sourceAtlasFilePath));
        JToken renderDataMaps = sourceAtlasFileJson["m_RenderDataMap"]["Array"];
        bool hasAnyModified = false;

        int counter = 0;
        HashSet<string> seenNames = new();
        string[] jsonFiles = Directory.GetFiles(_sourceDumpsFolderPath, "*.json", SearchOption.TopDirectoryOnly);
        foreach (string jsonFile in jsonFiles)
        {
            // offset
            bool hasModified = false;
            JObject spriteJson = JObject.Parse(File.ReadAllText(jsonFile));
            if (!spriteJson.ContainsKey("m_Rect")) // Not a Sprite Dump File
                continue;
            (string, long) namePathID = GetNamePathIDFromDumpName(jsonFile);
            string name = namePathID.Item1;
            if (seenNames.Contains(name))
            {
                name = name + " #" + counter.ToString();
            }
            counter++;
            seenNames.Add(name);
            long pathID = namePathID.Item2;
            SpriteMetaData? _meta = GetMetaOfName(name, metas);
            if (_meta == null)
                continue;

            SpriteMetaData meta = (SpriteMetaData)_meta;
            hasModified = !IsCloseFloat(float.Parse(spriteJson["m_Pivot"]["x"].ToString()), meta.pivot.x) ||
                            !IsCloseFloat(float.Parse(spriteJson["m_Pivot"]["y"].ToString()), meta.pivot.y) ||
                            !IsCloseFloat(float.Parse(spriteJson["m_Border"]["x"].ToString()), meta.border.x) ||
                            !IsCloseFloat(float.Parse(spriteJson["m_Border"]["y"].ToString()), meta.border.y) ||
                            !IsCloseFloat(float.Parse(spriteJson["m_Border"]["z"].ToString()), meta.border.z) ||
                            !IsCloseFloat(float.Parse(spriteJson["m_Border"]["w"].ToString()), meta.border.w);
            spriteJson["m_Pivot"]["x"] = meta.pivot.x;
            spriteJson["m_Pivot"]["y"] = meta.pivot.y;
            spriteJson["m_Border"]["x"] = meta.border.x;
            spriteJson["m_Border"]["y"] = meta.border.y;
            spriteJson["m_Border"]["z"] = meta.border.z;
            spriteJson["m_Border"]["w"] = meta.border.w;

            // rect
            int indexInAtlas = pathID2Index[pathID];
            int actualRenderDataKeyIndex = index2ActualRenderDataKeyIndex[indexInAtlas];
            var renderDataMapSource = renderDataMaps[actualRenderDataKeyIndex];

            hasModified = hasModified ||
                            !IsCloseFloat(float.Parse(renderDataMapSource["second"]["textureRect"]["x"].ToString()), meta.rect.x) ||
                            !IsCloseFloat(float.Parse(renderDataMapSource["second"]["textureRect"]["y"].ToString()), meta.rect.y) ||
                            !IsCloseFloat(float.Parse(renderDataMapSource["second"]["textureRect"]["width"].ToString()), meta.rect.width) ||
                            !IsCloseFloat(float.Parse(renderDataMapSource["second"]["textureRect"]["height"].ToString()), meta.rect.height);

            renderDataMapSource["second"]["textureRect"]["x"] = meta.rect.x;
            renderDataMapSource["second"]["textureRect"]["y"] = meta.rect.y;
            renderDataMapSource["second"]["textureRect"]["width"] = meta.rect.width;
            renderDataMapSource["second"]["textureRect"]["height"] = meta.rect.height;

            spriteJson["m_Offset"]["x"] = meta.pivot.x * meta.rect.width;
            spriteJson["m_Offset"]["y"] = meta.pivot.y * meta.rect.height;

            renderDataMaps[actualRenderDataKeyIndex] = renderDataMapSource;
            sourceAtlasFileJson["m_RenderDataMap"]["Array"] = renderDataMaps;

            if (hasModified)
            {
                hasAnyModified = true;
                string savePath = Path.Combine(_outputDumpPath, Path.GetFileName(jsonFile));
                File.WriteAllText(savePath, JsonConvert.SerializeObject(spriteJson, Formatting.Indented));
                // Print the result
                string extraMessage = $"Atlas Index: {indexInAtlas}, Actual RenderDataMap Index in Atlas: {actualRenderDataKeyIndex}.";
                Debug.Log($"Modified {name}, {pathID}, {extraMessage}");
            }
        }

        if (hasAnyModified)
        {
            string savePath = Path.Combine(_outputDumpPath, Path.GetFileName(_sourceAtlasFilePath));
            File.WriteAllText(savePath, JsonConvert.SerializeObject(sourceAtlasFileJson, Formatting.Indented));
        }
        else
        {
            Debug.Log("Did not modify anything.");
        }
    }

    private List<SpriteMetaData> GetMetasFromSpritesheet()
    {
        List<SpriteMetaData> result = new();
        string assetPath = AssetDatabase.GetAssetPath(_targetTexture);
        TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti != null)
        {
            result = ti.spritesheet.ToList();
        }
        else
        {
            Debug.LogError($"Texture not found in {_targetTexture}.");
        }

        return result;
    }

    private void EmptyOutputFolder()
    {
        foreach (string file in Directory.GetFiles(_outputDumpPath))
            File.Delete(file);

        Debug.Log($"Removed all files from {_outputDumpPath}.");
    }
}
