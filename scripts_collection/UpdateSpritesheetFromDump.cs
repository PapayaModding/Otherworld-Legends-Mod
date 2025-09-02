using UnityEditor;
using UnityEngine;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

public class UpdateSpritesheetFromDump : EditorWindow
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

    public enum GameOption
    {
        战魂铭人,
        元气骑士
    }

    public Dictionary<GameOption, int> GamePPU = new() // Pixel Per Unit setting
    {
        { GameOption.战魂铭人, 100 },
        { GameOption.元气骑士, 16 }
    };

    private string _sourceDumpsFolderPath = "";
    private Texture2D _targetTexture;
    private string _sourceAtlasFilePath = ""; // if used
    private GameOption _gameOption; // Make this persistent across sections

    [MenuItem("Tools/XX01 Build Spritesheet From Dumps", priority = 1)]
    public static void ShowWindow()
    {
        GetWindow<UpdateSpritesheetFromDump>("Build Spritesheet From Dumps");
    }

    private void OnEnable()
    {
        // Change default option here (so that you don't have to change next time you open Unity)
        // 在此处修改初始游戏设置（这样下一次你进Unity就不需要改了）
        _gameOption = (GameOption)EditorPrefs.GetInt("MyEnumWindow_Selected", (int)GameOption.战魂铭人);
    }

    private void OnDisable()
    {
        EditorPrefs.SetInt("MyEnumWindow_Selected", (int)_gameOption);
    }

    private void OnGUI()
    {
        GUIStyle style = new(GUI.skin.label)
        {
            wordWrap = true
        };

        GUILayout.Label("Required \n必填", EditorStyles.boldLabel);

        GUILayout.Label("Target Texture*");
        _targetTexture = (Texture2D)EditorGUILayout.ObjectField("材质文件*", _targetTexture, typeof(Texture2D), false);
        _sourceDumpsFolderPath = DragAndDropFolderField("Source Sprite Dump Folder (Json files)* \n源图像导出文件夹 (Json文件)*", _sourceDumpsFolderPath);

        GUILayout.Space(35);

        GUILayout.Label("Optional \n选填", EditorStyles.boldLabel);

        GUILayout.Label("Don't fill if Source Sprite Dump Folder contains this file. Also don't fill if not using Atlas.", style);
        GUILayout.Label("如果源图像导出文件夹中含有该文件则不用填。如果没有使用自动图集也不要填。", style);

        GUILayout.Space(5);

        _sourceAtlasFilePath = DragAndDropFileField("Source SpriteAtlas File \n源自动图集的导出文件", _sourceAtlasFilePath, "json");

        _gameOption = (GameOption)EditorGUILayout.EnumPopup("游戏选项", _gameOption);

        GUILayout.Space(5);

        GUI.enabled = _targetTexture != null && !string.IsNullOrWhiteSpace(_sourceDumpsFolderPath);

        if (GUILayout.Button("Run (运行)") && _sourceDumpsFolderPath != null)
        {
            _sourceAtlasFilePath = GetSourceAtlasFilePath();
            if (string.IsNullOrWhiteSpace(_sourceAtlasFilePath))
                Debug.LogError("Could not find Source Atlas Dump File. Please make sure you assign one.");

            // Import();
            Debug.Log(GetIndex2PathID().Count);
            Debug.Log(GetIndex2RenderDataKey().Count);

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

    private string GetSourceAtlasFilePath()
    {
        if (!string.IsNullOrWhiteSpace(_sourceAtlasFilePath))
        {
            return _sourceAtlasFilePath;
        }
        string[] jsonFiles = Directory.GetFiles(_sourceDumpsFolderPath, "*.json", SearchOption.TopDirectoryOnly);
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

    private Dictionary<int, RenderDataKey> GetIndex2RenderDataKey()
    {
        Dictionary<long, int> pathID2Index = GetPathID2Index();
        Dictionary<int, RenderDataKey> result = new();
        string[] jsonFiles = Directory.GetFiles(_sourceDumpsFolderPath, "*.json", SearchOption.TopDirectoryOnly);
        int failed = 0;
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

                if (pathID2Index.ContainsKey(pathID))
                {
                    result[pathID2Index[pathID]] = renderDataKey;
                }
                else
                {
                    failed++;
                    Debug.LogWarning($"Path ID {pathID} not found in Atlas.");
                    continue;
                }
            }
        }
        Debug.LogWarning($"Successfully processed {jsonFiles.Length - failed} number of json files; Failed to process {failed} number of json dumps; Total: {jsonFiles.Length}");
        return result;
    }
}
