#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEngine.U2D;
using UnityEditor.U2D;
using Debug = UnityEngine.Debug;

namespace RV_AssetOptimizerSystem.Editor
{
    public class AssetOptimizer : EditorWindow
    {
        private int selectedTab;
        private GUIContent[] tabContents;
        private readonly string[] tabNames = new string[] { "Settings", "Preview", "Report", 
            "Manual Selection", "Audit History", "Help" };

        private bool optimizeTextures = true;
        private bool optimizeModels = true;
        private bool optimizeAudio = true;
        private bool optimizeMaterials;
        private bool generateAtlas = true;

        private int textureMaxSize = 1024;
        private TextureImporterFormat textureFormat = TextureImporterFormat.ASTC_4x4;
        private bool generateMipMaps = true;

        private float modelPolygonReduction = 0.5f;

        private AudioCompressionFormat audioFormat = AudioCompressionFormat.AAC;
        private int audioQuality = 50;
        private AudioClipLoadType audioLoadType = AudioClipLoadType.Streaming;

        private string folderFilter = "";
        private string labelFilter = "";

        private bool doBackup = true;
        private readonly string backupFolder = "Assets/AssetOptimizerBackups";

        private bool useManualSelection;

        private OptimizationProfiles optimizationProfiles = new OptimizationProfiles();
        private int selectedTextureProfileIndex = -1;
        private int selectedModelProfileIndex = -1;
        private int selectedAudioProfileIndex = -1;
        private string newTextureProfileName = "";
        private string newModelProfileName = "";
        private string newAudioProfileName = "";
        private readonly string profilesFilePath = "Assets/AssetOptimizerProfiles.json";

        private readonly List<string> optimizationLog = new List<string>();
        private int texturesOptimizedCount;
        private int modelsOptimizedCount;
        private int audioOptimizedCount;
        private int materialsOptimizedCount;
        private bool spriteAtlasGeneratedFlag;

        private int previewTextureCount;
        private int previewModelCount;
        private int previewAudioCount;
        private int previewMaterialCount;
        private int previewSpriteCount;

        private int selectedExportFormat;
        private readonly string[] exportFormatOptions = new string[] { "CSV", "JSON", "HTML" };

        private int selectedTheme;
        private readonly string[] themeOptions = new string[] { "Default", "Dark", "Light" };

        private Vector2 settingsScrollPos;
        private Vector2 previewScrollPos;
        private Vector2 reportScrollPos;
        private Vector2 manualSelectionScrollPos;
        private Vector2 auditScrollPos;
        private Vector2 helpScrollPos;

        private readonly Dictionary<string, bool> manualOptimizeSelection = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> manualAtlasSelection = new Dictionary<string, bool>();

        private int selectedAssetTypeIndex;
        private readonly string[] assetTypeOptions = new string[] { "Texture", "Model", "AudioClip", "Sprite", "Material" };

        private readonly string auditLogFilePath = "Assets/AssetOptimizerAuditLog.txt";

        private readonly Dictionary<string, Texture2D> thumbnailCache = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, List<string>> assetListCache = new Dictionary<string, List<string>>();
        private string lastFolderFilter = "";
        private string lastLabelFilter = "";

        private const string WindowLayoutKey = "MobileAssetOptimizer_WindowRect";

        [MenuItem("RV - Template Tool/Asset Optimizer Tool")]
        public static void ShowWindow() => GetWindow<AssetOptimizer>("Asset Optimizer");

        private void OnEnable()
        {
            if (EditorPrefs.HasKey(WindowLayoutKey))
            {
                string rectJson = EditorPrefs.GetString(WindowLayoutKey);
                Rect savedRect = JsonUtility.FromJson<Rect>(rectJson);
                this.position = savedRect;
            }
            LoadProfiles();
            if (!File.Exists(auditLogFilePath))
                File.WriteAllText(auditLogFilePath, "");

            tabContents = new GUIContent[tabNames.Length];
            tabContents[0] = new GUIContent("Settings", EditorGUIUtility.IconContent("d_Settings").image);
            tabContents[1] = new GUIContent("Preview", EditorGUIUtility.IconContent("d_ViewToolOrbit").image);
            tabContents[2] = new GUIContent("Report", EditorGUIUtility.IconContent("d_Profiler.Video").image);
            tabContents[3] = new GUIContent("Manual Selection", EditorGUIUtility.IconContent("d_EditCollider").image);
            tabContents[4] = new GUIContent("Audit History", EditorGUIUtility.IconContent("d_console.infoicon").image);
            tabContents[5] = new GUIContent("Help", EditorGUIUtility.IconContent("d_UnityEditor.ConsoleWindow").image);
        }

        private void OnDisable()
        {
            string rectJson = JsonUtility.ToJson(this.position);
            EditorPrefs.SetString(WindowLayoutKey, rectJson);
        }

        private void OnGUI()
        {
            ApplyTheme();

            selectedTab = GUILayout.Toolbar(selectedTab, tabContents);
            GUILayout.Space(5);

            switch (selectedTab)
            {
                case 0:
                    DrawSettingsTab();
                    break;
                case 1:
                    DrawPreviewTab();
                    break;
                case 2:
                    DrawReportTab();
                    break;
                case 3:
                    DrawManualSelectionTab();
                    break;
                case 4:
                    DrawAuditHistoryTab();
                    break;
                case 5:
                    DrawHelpTab();
                    break;
            }
        }

        #region UI Theme

        private void ApplyTheme()
        {
            switch (selectedTheme)
            {
                case 1:
                    GUI.backgroundColor = Color.gray;
                    break;
                case 2:
                    GUI.backgroundColor = Color.white;
                    break;
                default:
                    GUI.backgroundColor = EditorGUIUtility.isProSkin ? Color.gray : Color.white;
                    break;
            }
        }

        #endregion

        #region Caching Helpers

        private List<string> GetCachedAssetList(string assetTypeFilter)
        {
            if (folderFilter != lastFolderFilter || labelFilter != lastLabelFilter)
            {
                assetListCache.Clear();
                lastFolderFilter = folderFilter;
                lastLabelFilter = labelFilter;
            }
            string key = assetTypeFilter + "|" + folderFilter + "|" + labelFilter;
            if (assetListCache.TryGetValue(key, out var list))
            {
                return list;
            }
            List<string> assetPaths = new List<string>();
            string[] guids = AssetDatabase.FindAssets(assetTypeFilter, null);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(folderFilter) && !path.StartsWith(folderFilter))
                    continue;
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (asset != null && !MatchesLabelFilter(asset))
                    continue;
                assetPaths.Add(path);
            }
            assetListCache[key] = assetPaths;
            return assetPaths;
        }

        #endregion

        #region Tab Drawing Methods

        private void DrawSettingsTab()
        {
            settingsScrollPos = EditorGUILayout.BeginScrollView(settingsScrollPos);
            
            GUILayout.Label(new GUIContent("Mobile Asset Optimizer Settings",
                "Configure optimization options, backups, profiles, and UI customization."),
                EditorStyles.boldLabel);

            folderFilter = EditorGUILayout.TextField(new GUIContent("Folder Filter (Path)",
                "Only assets within this path will be processed."), folderFilter);
            labelFilter = EditorGUILayout.TextField(new GUIContent("Label Filter",
                "For advanced filtering, enter multiple comma-separated labels."), labelFilter);
            doBackup = EditorGUILayout.Toggle(new GUIContent("Make Backup Before Optimizing",
                "Creates a backup of assets before applying changes."), doBackup);
            useManualSelection = EditorGUILayout.Toggle(new GUIContent("Use Manual Selection",
                "Only process assets manually selected in the Manual Selection tab."), useManualSelection);

            GUILayout.Space(10);

            GUILayout.Label(new GUIContent("UI Customization",
                "Customize the look and feel of the tool."), EditorStyles.boldLabel);
            selectedTheme = EditorGUILayout.Popup(new GUIContent("UI Theme",
                "Select a theme for the interface."), selectedTheme, themeOptions);

            GUILayout.Space(10);

            GUILayout.Label(new GUIContent("Texture Optimization Profile",
                "Load and save profiles for texture optimization."), EditorStyles.boldLabel);
            if (optimizationProfiles.textureProfiles.Count > 0)
            {
                string[] textureProfileNames = optimizationProfiles.textureProfiles
                    .Select(p => p.profileName).ToArray();
                selectedTextureProfileIndex = EditorGUILayout.Popup(new GUIContent("Select Texture Profile",
                    "Choose a saved texture profile."), selectedTextureProfileIndex, textureProfileNames);
                if (GUILayout.Button(new GUIContent("Load Texture Profile",
                        "Apply the selected texture profile's settings.")))
                {
                    if (selectedTextureProfileIndex >= 0 
                        && selectedTextureProfileIndex < optimizationProfiles.textureProfiles.Count)
                        LoadTextureProfile(optimizationProfiles.textureProfiles[selectedTextureProfileIndex]);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No texture profiles saved.", MessageType.Info);
            }
            newTextureProfileName = EditorGUILayout.TextField(new GUIContent("New Texture Profile Name",
                "Enter a name to save the current texture settings as a profile."), newTextureProfileName);
            if (GUILayout.Button(new GUIContent("Save Current Texture Profile",
                    "Save the current texture settings as a new profile.")))
            {
                SaveCurrentTextureProfile();
            }

            GUILayout.Space(10);

            GUILayout.Label(new GUIContent("Model Optimization Profile",
                "Load and save profiles for model optimization."), EditorStyles.boldLabel);
            if (optimizationProfiles.modelProfiles.Count > 0)
            {
                string[] modelProfileNames = optimizationProfiles.modelProfiles
                    .Select(p => p.profileName).ToArray();
                selectedModelProfileIndex = EditorGUILayout.Popup(new GUIContent("Select Model Profile",
                    "Choose a saved model profile."), selectedModelProfileIndex, modelProfileNames);
                if (GUILayout.Button(new GUIContent("Load Model Profile",
                        "Apply the selected model profile's settings.")))
                {
                    if (selectedModelProfileIndex >= 0 
                        && selectedModelProfileIndex < optimizationProfiles.modelProfiles.Count)
                        LoadModelProfile(optimizationProfiles.modelProfiles[selectedModelProfileIndex]);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No model profiles saved.", MessageType.Info);
            }
            newModelProfileName = EditorGUILayout.TextField(new GUIContent("New Model Profile Name",
                "Enter a name to save the current model settings as a profile."), newModelProfileName);
            if (GUILayout.Button(new GUIContent("Save Current Model Profile",
                    "Save the current model settings as a new profile.")))
            {
                SaveCurrentModelProfile();
            }

            GUILayout.Space(10);

            GUILayout.Label(new GUIContent("Audio Optimization Profile",
                "Load and save profiles for audio optimization."), EditorStyles.boldLabel);
            if (optimizationProfiles.audioProfiles.Count > 0)
            {
                string[] audioProfileNames = optimizationProfiles.audioProfiles
                    .Select(p => p.profileName).ToArray();
                selectedAudioProfileIndex = EditorGUILayout.Popup(new GUIContent("Select Audio Profile",
                    "Choose a saved audio profile."), selectedAudioProfileIndex, audioProfileNames);
                if (GUILayout.Button(new GUIContent("Load Audio Profile",
                        "Apply the selected audio profile's settings.")))
                {
                    if (selectedAudioProfileIndex >= 0 
                        && selectedAudioProfileIndex < optimizationProfiles.audioProfiles.Count)
                        LoadAudioProfile(optimizationProfiles.audioProfiles[selectedAudioProfileIndex]);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No audio profiles saved.", MessageType.Info);
            }
            newAudioProfileName = EditorGUILayout.TextField(new GUIContent("New Audio Profile Name",
                "Enter a name to save the current audio settings as a profile."), newAudioProfileName);
            if (GUILayout.Button(new GUIContent("Save Current Audio Profile",
                    "Save the current audio settings as a new profile.")))
            {
                SaveCurrentAudioProfile();
            }

            GUILayout.Space(10);

            optimizeTextures = EditorGUILayout.Toggle(new GUIContent("Optimize Textures",
                "Apply optimization settings to texture assets."), optimizeTextures);
            if (optimizeTextures)
            {
                GUILayout.Label(new GUIContent("Texture Settings",
                    "Configure texture optimization parameters."), EditorStyles.boldLabel);
                textureMaxSize = EditorGUILayout.IntField(new GUIContent("Max Size",
                    "Maximum texture resolution."), textureMaxSize);
                textureFormat = (TextureImporterFormat)EditorGUILayout.EnumPopup(new GUIContent("Format",
                    "Compression format for textures."), textureFormat);
                generateMipMaps = EditorGUILayout.Toggle(new GUIContent("Generate MipMaps",
                    "Generate mipmaps for textures."), generateMipMaps);
            }

            optimizeModels = EditorGUILayout.Toggle(new GUIContent("Optimize Models",
                "Process 3D models for mobile optimization."), optimizeModels);
            if (optimizeModels)
            {
                GUILayout.Label(new GUIContent("Model Settings", 
                    "Configure model optimization parameters."), EditorStyles.boldLabel);
                modelPolygonReduction = EditorGUILayout.Slider(new GUIContent("Polygon Reduction",
                    "Percentage of polygon reduction (target ratio of vertices to keep)."),
                    modelPolygonReduction, 0.1f, 1f);
                EditorGUILayout.HelpBox(
                    "A mesh simplification algorithm based on Quadric Error Metrics is applied.",
                    MessageType.Info);
            }

            optimizeAudio = EditorGUILayout.Toggle(new GUIContent("Optimize Audio",
                "Apply optimization settings to audio assets."), optimizeAudio);
            if (optimizeAudio)
            {
                GUILayout.Label(new GUIContent("Audio Settings",
                    "Configure audio optimization parameters."), EditorStyles.boldLabel);
                audioFormat = (AudioCompressionFormat)EditorGUILayout.EnumPopup(new GUIContent("Audio Format",
                    "Select audio compression format."), audioFormat);
                audioQuality = EditorGUILayout.IntSlider(new GUIContent("Audio Quality",
                    "Quality level for audio compression."), audioQuality, 0, 100);
                audioLoadType = (AudioClipLoadType)EditorGUILayout.EnumPopup(new GUIContent("Load Type",
                    "Determine how audio is loaded."), audioLoadType);
            }

            optimizeMaterials = EditorGUILayout.Toggle(new GUIContent("Optimize Materials",
                "Apply mobile-friendly optimizations to material assets."), optimizeMaterials);
            if (optimizeMaterials)
            {
                GUILayout.Label(new GUIContent("Material Optimization Settings",
                    "Materials will be optimized by assigning a mobile-friendly shader if applicable."), 
                    EditorStyles.boldLabel);
            }

            generateAtlas = EditorGUILayout.Toggle(new GUIContent("Generate Sprite Atlas",
                "Automatically create a sprite atlas."), generateAtlas);
            if (generateAtlas)
            {
                GUILayout.Label(new GUIContent("Sprite Atlas Settings",
                    "Additional settings for generating a sprite atlas."), EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("The atlas system uses Unity's SpriteAtlas (requires Unity 2017.1+).",
                    MessageType.Info);
            }

            GUILayout.Space(10);
            if (GUILayout.Button(new GUIContent("Execute Optimization",
                    "Run the optimization process with the current settings.")))
            {
                ExecuteOptimization();
            }
            if (GUILayout.Button(new GUIContent("Revert Optimization (Restore Backup)",
                    "Revert all changes using the backup files.")))
            {
                if (EditorUtility.DisplayDialog("Revert Optimization",
                        "Are you sure you want to revert all changes using the backups?", 
                        "Yes", "No"))
                {
                    RevertOptimization();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPreviewTab()
        {
            GUILayout.Label(new GUIContent("Preview Changes",
                "Review the assets that will be processed and the changes to be applied."),
                EditorStyles.boldLabel);
            BuildPreviewData();

            EditorGUILayout.HelpBox(
                "This preview shows the number of assets that will be processed and the parameter changes" +
                " to be applied.", MessageType.Info);

            GUILayout.Label($"Textures to Optimize: {previewTextureCount}");
            if (optimizeTextures)
                GUILayout.Label($"   • New Max Size: {textureMaxSize}, Format: {textureFormat}," +
                                $" MipMaps: {(generateMipMaps ? "Yes" : "No")}");
            GUILayout.Label($"Models to Process: {previewModelCount}");
            if (optimizeModels)
                GUILayout.Label($"   • Polygon Reduction: {modelPolygonReduction * 100}%");
            GUILayout.Label($"Audio Clips to Optimize: {previewAudioCount}");
            if (optimizeAudio)
                GUILayout.Label($"   • New Format: {audioFormat}, Quality: {audioQuality}, Load Type: {audioLoadType}");
            if (optimizeMaterials)
                GUILayout.Label($"Materials to Optimize: {previewMaterialCount}");
            GUILayout.Label($"Sprites for Atlas: {previewSpriteCount}");
            if (generateAtlas)
                GUILayout.Label("   • A new Sprite Atlas will be generated.");

            GUILayout.Space(10);
            GUILayout.Label("Examples (first 5 per category):", EditorStyles.boldLabel);
            previewScrollPos = EditorGUILayout.BeginScrollView(previewScrollPos, GUILayout.Height(150));
            if (previewTextureCount > 0)
            {
                GUILayout.Label("Textures:");
                foreach (string s in GetAssetExamples("t:Texture", 5))
                    GUILayout.Label("   " + s);
            }
            if (previewModelCount > 0)
            {
                GUILayout.Label("Models:");
                foreach (string s in GetAssetExamples("t:Model", 5))
                    GUILayout.Label("   " + s);
            }
            if (previewAudioCount > 0)
            {
                GUILayout.Label("Audio Clips:");
                foreach (string s in GetAssetExamples("t:AudioClip", 5))
                    GUILayout.Label("   " + s);
            }
            if (previewMaterialCount > 0)
            {
                GUILayout.Label("Materials:");
                foreach (string s in GetAssetExamples("t:Material", 5))
                    GUILayout.Label("   " + s);
            }
            if (previewSpriteCount > 0)
            {
                GUILayout.Label("Sprites:");
                foreach (string s in GetAssetExamples("t:Sprite", 5))
                    GUILayout.Label("   " + s);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawReportTab()
        {
            GUILayout.Label(new GUIContent("Optimization Report",
                "View a summary and detailed log of the optimization process."), EditorStyles.boldLabel);

            GUILayout.Label("Summary:");
            GUILayout.Label($"Textures Optimized: {texturesOptimizedCount}");
            GUILayout.Label($"Models Processed: {modelsOptimizedCount}");
            GUILayout.Label($"Audio Clips Optimized: {audioOptimizedCount}");
            GUILayout.Label($"Materials Optimized: {materialsOptimizedCount}");
            GUILayout.Label($"Sprite Atlas Generated: {(spriteAtlasGeneratedFlag ? "Yes" : "No")}");

            GUILayout.Space(10);
            GUILayout.Label("Detailed Log:", EditorStyles.boldLabel);
            reportScrollPos = EditorGUILayout.BeginScrollView(reportScrollPos, GUILayout.Height(200));
            foreach (string log in optimizationLog)
                GUILayout.Label(log);
            EditorGUILayout.EndScrollView();

            GUILayout.Space(10);
            GUILayout.Label("Optimization Statistics", EditorStyles.boldLabel);
            DrawStatisticsGraph();

            GUILayout.Space(10);
            GUILayout.Label("Export Report", EditorStyles.boldLabel);
            selectedExportFormat = EditorGUILayout.Popup(new GUIContent("Export Format",
                "Select the export format for the report."), selectedExportFormat, exportFormatOptions);
            if (GUILayout.Button(new GUIContent("Export Report",
                    "Export the current report to a file.")))
                ExportReport();
        }

        private void DrawManualSelectionTab()
        {
            GUILayout.Label(new GUIContent("Manual Asset Selection",
                "Manually choose which assets to optimize and, for sprites, which to include in the atlas."),
                EditorStyles.boldLabel);
            GUILayout.Label("Manual Selection Mode is " + (useManualSelection ? "Enabled" : "Disabled"),
                EditorStyles.miniBoldLabel);
            GUILayout.Space(5);

            selectedAssetTypeIndex = EditorGUILayout.Popup(new GUIContent("Asset Type",
                "Select the asset type to display."), selectedAssetTypeIndex, assetTypeOptions);
            string assetTypeFilter = "";
            switch (assetTypeOptions[selectedAssetTypeIndex])
            {
                case "Texture": assetTypeFilter = "t:Texture"; break;
                case "Model": assetTypeFilter = "t:Model"; break;
                case "AudioClip": assetTypeFilter = "t:AudioClip"; break;
                case "Sprite": assetTypeFilter = "t:Sprite"; break;
                case "Material": assetTypeFilter = "t:Material"; break;
            }

            List<string> assetPaths = GetCachedAssetList(assetTypeFilter);

            manualSelectionScrollPos = EditorGUILayout.BeginScrollView(manualSelectionScrollPos, 
                GUILayout.Height(300));
            foreach (string path in assetPaths)
            {
                GUILayout.BeginHorizontal();
                Object assetObj = AssetDatabase.LoadMainAssetAtPath(path);
                if (!thumbnailCache.TryGetValue(path, out var thumbnail))
                {
                    thumbnail = AssetPreview.GetMiniThumbnail(assetObj);
                    thumbnailCache[path] = thumbnail;
                }
                if (thumbnail != null)
                    GUILayout.Label(thumbnail, GUILayout.Width(20), GUILayout.Height(20));

                bool currentOptimize = manualOptimizeSelection.ContainsKey(path) ? manualOptimizeSelection[path] : false;
                bool newOptimize = EditorGUILayout.ToggleLeft(Path.GetFileName(path), 
                    currentOptimize, GUILayout.Width(200));
                if (newOptimize != currentOptimize)
                    manualOptimizeSelection[path] = newOptimize;
                if (assetTypeOptions[selectedAssetTypeIndex] == "Sprite")
                {
                    bool currentAtlas = manualAtlasSelection.ContainsKey(path) ? manualAtlasSelection[path] : false;
                    bool newAtlas = EditorGUILayout.ToggleLeft("Include in Atlas",
                        currentAtlas, GUILayout.Width(140));
                    if (newAtlas != currentAtlas)
                        manualAtlasSelection[path] = newAtlas;
                }
                GUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button(new GUIContent("Clear Manual Selections", "Clear all manual selections.")))
            {
                manualOptimizeSelection.Clear();
                manualAtlasSelection.Clear();
            }
        }

        private void DrawAuditHistoryTab()
        {
            GUILayout.Label(new GUIContent("Audit History",
                "Review the detailed audit log of each optimization."), EditorStyles.boldLabel);
            
            auditScrollPos = EditorGUILayout.BeginScrollView(auditScrollPos, GUILayout.Height(200));
            if (File.Exists(auditLogFilePath))
            {
                string auditContent = File.ReadAllText(auditLogFilePath);
                GUILayout.TextArea(auditContent, GUILayout.ExpandHeight(true));
            }
            else
            {
                GUILayout.Label("No audit history found.");
            }
            EditorGUILayout.EndScrollView();
            
            if (GUILayout.Button(new GUIContent("Clear Audit History", "Erase the audit log.")))
            {
                if (EditorUtility.DisplayDialog("Clear Audit History", 
                        "Are you sure you want to clear the audit history?", "Yes", "No"))
                {
                    File.WriteAllText(auditLogFilePath, "");
                    EditorUtility.DisplayDialog("Audit History Cleared", 
                        "The audit history has been cleared.", "OK");
                }
            }
        }

        private void DrawHelpTab()
        {
            GUILayout.Label(new GUIContent("Help & Documentation",
                "Instructions and tips for using the tool."), EditorStyles.boldLabel);
            
            helpScrollPos = EditorGUILayout.BeginScrollView(helpScrollPos);
            GUILayout.Label(
                "Welcome to the Mobile Asset Optimizer Tool.\n\n" +
                "This tool automates asset optimization for mobile platforms in Unity.\n\n" +
                "Tabs:\n" +
                " - Settings: Configure optimization options, backups, profiles, and UI customization.\n" +
                " - Preview: View a summary of the assets that will be processed and the changes to be applied.\n" +
                " - Report: View detailed logs and statistics of the optimization process, and export the report.\n" +
                " - Manual Selection: Manually select which assets to process and which sprites to include in the atlas.\n" +
                " - Audit History: View a persistent log of every optimization performed (date, asset, details).\n" +
                " - Help: This documentation and tips.\n\n" +
                "For CI/CD integration, use the static method RunOptimizationCI.\n\n" +
                "Hover over controls to see additional information via tooltips.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        #endregion

        #region Optimization Execution

        private void ExecuteOptimization()
        {
            texturesOptimizedCount = 0;
            modelsOptimizedCount = 0;
            audioOptimizedCount = 0;
            materialsOptimizedCount = 0;
            spriteAtlasGeneratedFlag = false;
            optimizationLog.Clear();

            if (doBackup && !Directory.Exists(backupFolder))
                Directory.CreateDirectory(backupFolder);

            if (optimizeTextures)
                OptimizeAllTextures();
            if (optimizeModels)
                OptimizeAllModels();
            if (optimizeAudio)
                OptimizeAllAudio();
            if (optimizeMaterials)
                OptimizeAllMaterials();
            if (generateAtlas)
                GenerateSpriteAtlas();

            AssetDatabase.Refresh();
            Debug.Log("Optimization completed.");
            optimizationLog.Add("Optimization completed successfully.");
        }

        private void OptimizeAllTextures()
        {
            List<string> eligiblePaths = useManualSelection ?
                manualOptimizeSelection.Where(kv => kv.Value && IsTexture(kv.Key)).Select(kv => kv.Key).ToList() :
                GetCachedAssetList("t:Texture");
            texturesOptimizedCount = eligiblePaths.Count;
            int count = eligiblePaths.Count;
            for (int i = 0; i < count; i++)
            {
                string path = eligiblePaths[i];
                if (EditorUtility.DisplayCancelableProgressBar("Optimizing Textures", 
                        $"Processing {Path.GetFileName(path)}", (float)i / count))
                {
                    optimizationLog.Add("Texture optimization canceled by user.");
                    break;
                }
                if (doBackup)
                    BackupAsset(path);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.maxTextureSize = textureMaxSize;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.crunchedCompression = true;
                    importer.compressionQuality = 50;
                    importer.mipmapEnabled = generateMipMaps;
                    importer.textureFormat = textureFormat;
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                    optimizationLog.Add($"[Texture] Optimized: {path}");
                    LogAuditEntry("Texture", path, $"Optimized to max size {textureMaxSize}, " +
                                                   $"format {textureFormat}, mipmaps: {generateMipMaps}");
                }
            }
            EditorUtility.ClearProgressBar();
        }

        private void OptimizeAllModels()
        {
            List<string> eligiblePaths = useManualSelection ?
                manualOptimizeSelection.Where(kv => kv.Value && IsModel(kv.Key)).Select(kv => kv.Key).ToList() :
                GetCachedAssetList("t:Model");
            modelsOptimizedCount = eligiblePaths.Count;
            int count = eligiblePaths.Count;
            for (int i = 0; i < count; i++)
            {
                string path = eligiblePaths[i];
                if (EditorUtility.DisplayCancelableProgressBar("Optimizing Models", 
                        $"Processing {Path.GetFileName(path)}", (float)i / count))
                {
                    optimizationLog.Add("Model optimization canceled by user.");
                    break;
                }
                OptimizeModelAsset(path, modelPolygonReduction);
            }
            EditorUtility.ClearProgressBar();
        }

        private void OptimizeModelAsset(string path, float keepRatio)
        {
            if (doBackup)
                BackupAsset(path);
            Mesh originalMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (originalMesh != null)
            {
                Mesh simplifiedMesh = SimplifyMeshAdvanced(originalMesh, keepRatio);
                if (simplifiedMesh != null)
                {
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(simplifiedMesh, path);
                    AssetDatabase.SaveAssets();
                    optimizationLog.Add($"[Model] Optimized: {path} - New vertex count: {simplifiedMesh.vertexCount}");
                    LogAuditEntry("Model", path, $"Optimized mesh: new vertex count {simplifiedMesh.vertexCount}");
                }
                else
                {
                    optimizationLog.Add($"[Model] Simplification failed for: {path}");
                    LogAuditEntry("Model", path, "Simplification failed.");
                }
            }
            else
            {
                optimizationLog.Add($"[Model] Could not load mesh for: {path}");
                LogAuditEntry("Model", path, "Failed to load mesh asset.");
            }
        }

        private Mesh SimplifyMeshAdvanced(Mesh originalMesh, float targetRatio)
        {
            Vector3[] vertices = originalMesh.vertices;
            int[] triangles = originalMesh.triangles;
            int targetCount = Mathf.Max(1, Mathf.RoundToInt(vertices.Length * targetRatio));

            SymmetricMatrix[] quadrics = new SymmetricMatrix[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                quadrics[i] = new SymmetricMatrix();

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i0 = triangles[i];
                int i1 = triangles[i + 1];
                int i2 = triangles[i + 2];
                Vector3 v0 = vertices[i0];
                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];
                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                float d = -Vector3.Dot(normal, v0);
                SymmetricMatrix q = new SymmetricMatrix(normal.x, normal.y, normal.z, d);
                quadrics[i0] += q;
                quadrics[i1] += q;
                quadrics[i2] += q;
            }

            Dictionary<(int, int), EdgeData> edgeDict = new Dictionary<(int, int), EdgeData>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int[] face = new int[] { triangles[i], triangles[i + 1], triangles[i + 2] };
                for (int j = 0; j < 3; j++)
                {
                    int a = face[j];
                    int b = face[(j + 1) % 3];
                    var key = a < b ? (a, b) : (b, a);
                    if (!edgeDict.ContainsKey(key))
                    {
                        EdgeData ed = new EdgeData(key.Item1, key.Item2);
                        ed.ComputeCost(quadrics, vertices);
                        edgeDict[key] = ed;
                    }
                }
            }

            List<EdgeData> edgeList = edgeDict.Values.ToList();
            edgeList.Sort((a, b) => a.error.CompareTo(b.error));

            int[] vertexMap = new int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                vertexMap[i] = i;

            int currentVertexCount = vertices.Length;
            foreach (EdgeData edge in edgeList)
            {
                if (currentVertexCount <= targetCount)
                    break;
                int vA = Find(vertexMap, edge.v1);
                int vB = Find(vertexMap, edge.v2);
                if (vA == vB) continue;

                vertices[vA] = edge.optimal;
                quadrics[vA] += quadrics[vB];
                vertexMap[vB] = vA;
                currentVertexCount--;
            }

            List<int> newTriangles = new List<int>();
            for (int i = 0; i < triangles.Length; i++)
            {
                int newIndex = Find(vertexMap, triangles[i]);
                newTriangles.Add(newIndex);
            }

            Dictionary<int, int> remap = new Dictionary<int, int>();
            List<Vector3> newVertices = new List<Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                int newIndex = Find(vertexMap, i);
                if (!remap.ContainsKey(newIndex))
                {
                    remap[newIndex] = newVertices.Count;
                    newVertices.Add(vertices[newIndex]);
                }
            }
            for (int i = 0; i < newTriangles.Count; i++)
            {
                newTriangles[i] = remap[Find(vertexMap, newTriangles[i])];
            }

            Mesh simplifiedMesh = new Mesh();
            simplifiedMesh.vertices = newVertices.ToArray();
            simplifiedMesh.triangles = newTriangles.ToArray();
            simplifiedMesh.RecalculateNormals();
            simplifiedMesh.RecalculateBounds();
            return simplifiedMesh;
        }

        private int Find(int[] vertexMap, int i)
        {
            if (vertexMap[i] != i)
                vertexMap[i] = Find(vertexMap, vertexMap[i]);
            return vertexMap[i];
        }

        private struct EdgeData
        {
            public int v1;
            public int v2;
            public float error;
            public Vector3 optimal;

            public EdgeData(int v1, int v2)
            {
                this.v1 = v1;
                this.v2 = v2;
                error = 0;
                optimal = Vector3.zero;
            }

            public void ComputeCost(SymmetricMatrix[] quadrics, Vector3[] vertices)
            {
                SymmetricMatrix q = quadrics[v1] + quadrics[v2];
                if (q.ComputeOptimal(vertices[v1], vertices[v2], out optimal))
                {
                    error = q.CalculateError(optimal);
                }
                else
                {
                    optimal = (vertices[v1] + vertices[v2]) * 0.5f;
                    error = q.CalculateError(optimal);
                }
            }
        }

        private struct SymmetricMatrix
        {
            public float m00, m01, m02, m03;
            public float m11, m12, m13;
            public float m22, m23;
            public float m33;

            public SymmetricMatrix(float a, float b, float c, float d)
            {
                m00 = a * a;
                m01 = a * b;
                m02 = a * c;
                m03 = a * d;
                m11 = b * b;
                m12 = b * c;
                m13 = b * d;
                m22 = c * c;
                m23 = c * d;
                m33 = d * d;
            }

            public static SymmetricMatrix operator +(SymmetricMatrix q1, SymmetricMatrix q2)
            {
                return new SymmetricMatrix
                {
                    m00 = q1.m00 + q2.m00,
                    m01 = q1.m01 + q2.m01,
                    m02 = q1.m02 + q2.m02,
                    m03 = q1.m03 + q2.m03,
                    m11 = q1.m11 + q2.m11,
                    m12 = q1.m12 + q2.m12,
                    m13 = q1.m13 + q2.m13,
                    m22 = q1.m22 + q2.m22,
                    m23 = q1.m23 + q2.m23,
                    m33 = q1.m33 + q2.m33
                };
            }

            public float CalculateError(Vector3 v)
            {
                float x = v.x, y = v.y, z = v.z;
                return m00 * x * x + 2 * m01 * x * y + 2 * m02 * x * z + 2 * m03 * x +
                       m11 * y * y + 2 * m12 * y * z + 2 * m13 * y +
                       m22 * z * z + 2 * m23 * z +
                       m33;
            }

            public bool ComputeOptimal(Vector3 v1, Vector3 v2, out Vector3 optimal)
            {
                float det = m00 * (m11 * m22 - m12 * m12) -
                            m01 * (m01 * m22 - m12 * m02) +
                            m02 * (m01 * m12 - m11 * m02);
                if (Mathf.Abs(det) > 1e-6f)
                {
                    float invDet = 1.0f / det;
                    float x = -invDet * (m03 * (m11 * m22 - m12 * m12) -
                                          m01 * (m13 * m22 - m12 * m23) +
                                          m02 * (m13 * m12 - m11 * m23));
                    float y = -invDet * (m00 * (m13 * m22 - m12 * m23) -
                                          m03 * (m01 * m22 - m02 * m12) +
                                          m02 * (m01 * m23 - m13 * m02));
                    float z = -invDet * (m00 * (m11 * m23 - m13 * m12) -
                                          m01 * (m01 * m23 - m13 * m02) +
                                          m03 * (m01 * m12 - m11 * m02));
                    optimal = new Vector3(x, y, z);
                    return true;
                }
                optimal = Vector3.zero;
                return false;
            }

            public Vector3 GetAverage()
            {
                return Vector3.zero;
            }
        }

        private void OptimizeAllAudio()
        {
            List<string> eligiblePaths = useManualSelection ?
                manualOptimizeSelection.Where(kv => kv.Value && IsAudio(kv.Key)).Select(kv => kv.Key).ToList() :
                GetCachedAssetList("t:AudioClip");
            audioOptimizedCount = eligiblePaths.Count;
            int count = eligiblePaths.Count;
            for (int i = 0; i < count; i++)
            {
                string path = eligiblePaths[i];
                if (EditorUtility.DisplayCancelableProgressBar("Optimizing Audio", 
                        $"Processing {Path.GetFileName(path)}", (float)i / count))
                {
                    optimizationLog.Add("Audio optimization canceled by user.");
                    break;
                }
                if (doBackup)
                    BackupAsset(path);
                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer != null)
                {
                    AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                    settings.compressionFormat = audioFormat;
                    settings.quality = audioQuality / 100f;
                    settings.loadType = audioLoadType;
                    importer.defaultSampleSettings = settings;
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                    optimizationLog.Add($"[Audio] Optimized: {path}");
                    LogAuditEntry("Audio", path, $"Optimized to format {audioFormat}, " +
                                                 $"quality {audioQuality}, load type {audioLoadType}");
                }
            }
            EditorUtility.ClearProgressBar();
        }

        private void OptimizeAllMaterials()
        {
            List<string> eligiblePaths = useManualSelection ?
                manualOptimizeSelection.Where(kv => kv.Value && IsMaterial(kv.Key)).Select(kv => kv.Key).ToList() :
                GetCachedAssetList("t:Material");
            materialsOptimizedCount = eligiblePaths.Count;
            int count = eligiblePaths.Count;
            for (int i = 0; i < count; i++)
            {
                string path = eligiblePaths[i];
                if (EditorUtility.DisplayCancelableProgressBar("Optimizing Materials", 
                        $"Processing {Path.GetFileName(path)}", (float)i / count))
                {
                    optimizationLog.Add("Material optimization canceled by user.");
                    break;
                }
                if (doBackup)
                    BackupAsset(path);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                {
                    if (mat.shader != null && mat.shader.name == "Standard")
                    {
                        Shader mobileShader = Shader.Find("Mobile/Diffuse");
                        if (mobileShader != null)
                        {
                            mat.shader = mobileShader;
                            EditorUtility.SetDirty(mat);
                            optimizationLog.Add($"[Material] Optimized: {path} - Shader changed to Mobile/Diffuse");
                            LogAuditEntry("Material", path, "Shader changed to Mobile/Diffuse for mobile optimization.");
                        }
                        else
                        {
                            optimizationLog.Add($"[Material] Skipped: {path} - Mobile/Diffuse shader not found.");
                            LogAuditEntry("Material", path, "Mobile/Diffuse shader not found.");
                        }
                    }
                    else
                    {
                        optimizationLog.Add($"[Material] No optimization needed: {path}");
                        LogAuditEntry("Material", path, "No optimization needed (non-Standard shader).");
                    }
                }
            }
            EditorUtility.ClearProgressBar();
        }

        private void GenerateSpriteAtlas()
        {
#if UNITY_2017_1_OR_NEWER
            SpriteAtlas atlas = new SpriteAtlas();
            string atlasPath = "Assets/Atlases/AutoGeneratedSpriteAtlas.spriteatlas";
            string directory = Path.GetDirectoryName(atlasPath);
            if (!AssetDatabase.IsValidFolder(directory))
                if (directory != null)
                    Directory.CreateDirectory(directory);
            AssetDatabase.CreateAsset(atlas, atlasPath);

            List<Object> sprites = new List<Object>();
            if (useManualSelection)
            {
                var selectedSprites = manualAtlasSelection
                    .Where(kv => kv.Value && IsSprite(kv.Key))
                                                          .Select(kv => kv.Key).ToList();
                foreach (string path in selectedSprites)
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite != null)
                        sprites.Add(sprite);
                }
                previewSpriteCount = sprites.Count;
            }
            else
            {
                List<string> spritePaths = GetCachedAssetList("t:Sprite");
                foreach (string path in spritePaths)
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite != null)
                        sprites.Add(sprite);
                }
                previewSpriteCount = sprites.Count;
            }
            SpriteAtlasExtensions.Add(atlas, sprites.ToArray());
            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            spriteAtlasGeneratedFlag = true;
            optimizationLog.Add("[SpriteAtlas] Generated new atlas.");
            LogAuditEntry("SpriteAtlas", "Multiple", 
                $"Generated new atlas with {previewSpriteCount} sprites.");
#else
            Debug.LogWarning("SpriteAtlas requires Unity 2017.1 or higher.");
#endif
        }

        #endregion

        #region Backup and Revert Methods

        private void BackupAsset(string assetPath)
        {
            string relativePath = assetPath.Substring("Assets/".Length);
            string backupPath = Path.Combine(backupFolder, relativePath);
            string backupDir = Path.GetDirectoryName(backupPath);
            if (!Directory.Exists(backupDir))
                if (backupDir != null)
                    Directory.CreateDirectory(backupDir);
            if (!File.Exists(backupPath))
                File.Copy(assetPath, backupPath, true);
            string metaPath = assetPath + ".meta";
            string backupMetaPath = backupPath + ".meta";
            if (File.Exists(metaPath) && !File.Exists(backupMetaPath))
                File.Copy(metaPath, backupMetaPath, true);
        }

        private void RevertOptimization()
        {
            if (!Directory.Exists(backupFolder))
            {
                Debug.LogWarning("No backups found to revert.");
                return;
            }
            string[] backupFiles = Directory.GetFiles(backupFolder, "*.*", SearchOption.AllDirectories);
            foreach (string backupFile in backupFiles)
            {
                if (backupFile.EndsWith(".meta"))
                    continue;
                string relativePath = backupFile.Substring(backupFolder.Length + 1);
                string originalPath = Path.Combine("Assets", relativePath);
                File.Copy(backupFile, originalPath, true);
                string backupMetaFile = backupFile + ".meta";
                string originalMetaFile = originalPath + ".meta";
                if (File.Exists(backupMetaFile))
                    File.Copy(backupMetaFile, originalMetaFile, true);
            }
            AssetDatabase.Refresh();
            optimizationLog.Add("Reverted optimization. Backups restored.");
            Debug.Log("Optimization reverted. Backups restored.");
        }

        #endregion

        #region Profile Management

        private void LoadProfiles()
        {
            if (File.Exists(profilesFilePath))
            {
                string json = File.ReadAllText(profilesFilePath);
                OptimizationProfiles loadedProfiles = JsonUtility.FromJson<OptimizationProfiles>(json);
                if (loadedProfiles != null)
                    optimizationProfiles = loadedProfiles;
            }
        }

        private void SaveProfiles()
        {
            string json = JsonUtility.ToJson(optimizationProfiles, true);
            File.WriteAllText(profilesFilePath, json);
            AssetDatabase.Refresh();
        }

        private void LoadTextureProfile(TextureOptimizationProfile profile)
        {
            textureMaxSize = profile.textureMaxSize;
            textureFormat = profile.textureFormat;
            generateMipMaps = profile.generateMipMaps;
            optimizationLog.Add($"Loaded texture profile: {profile.profileName}");
            Debug.Log($"Texture profile '{profile.profileName}' loaded.");
        }

        private void LoadModelProfile(ModelOptimizationProfile profile)
        {
            modelPolygonReduction = profile.modelPolygonReduction;
            optimizationLog.Add($"Loaded model profile: {profile.profileName}");
            Debug.Log($"Model profile '{profile.profileName}' loaded.");
        }

        private void LoadAudioProfile(AudioOptimizationProfile profile)
        {
            audioFormat = profile.audioFormat;
            audioQuality = profile.audioQuality;
            audioLoadType = profile.audioLoadType;
            optimizationLog.Add($"Loaded audio profile: {profile.profileName}");
            Debug.Log($"Audio profile '{profile.profileName}' loaded.");
        }

        private void SaveCurrentTextureProfile()
        {
            if (string.IsNullOrEmpty(newTextureProfileName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a texture profile name.", "OK");
                return;
            }
            TextureOptimizationProfile newProfile = new TextureOptimizationProfile()
            {
                profileName = newTextureProfileName,
                textureMaxSize = textureMaxSize,
                textureFormat = textureFormat,
                generateMipMaps = generateMipMaps
            };
            int index = optimizationProfiles.textureProfiles
                .FindIndex(p => p.profileName == newTextureProfileName);
            if (index >= 0)
                optimizationProfiles.textureProfiles[index] = newProfile;
            else
            {
                optimizationProfiles.textureProfiles.Add(newProfile);
                selectedTextureProfileIndex = optimizationProfiles.textureProfiles.Count - 1;
            }
            SaveProfiles();
            EditorUtility.DisplayDialog("Profile Saved", 
                $"Texture profile '{newTextureProfileName}' saved.", "OK");
        }

        private void SaveCurrentModelProfile()
        {
            if (string.IsNullOrEmpty(newModelProfileName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a model profile name.", "OK");
                return;
            }
            ModelOptimizationProfile newProfile = new ModelOptimizationProfile()
            {
                profileName = newModelProfileName,
                modelPolygonReduction = modelPolygonReduction
            };
            int index = optimizationProfiles.modelProfiles
                .FindIndex(p => p.profileName == newModelProfileName);
            if (index >= 0)
                optimizationProfiles.modelProfiles[index] = newProfile;
            else
            {
                optimizationProfiles.modelProfiles.Add(newProfile);
                selectedModelProfileIndex = optimizationProfiles.modelProfiles.Count - 1;
            }
            SaveProfiles();
            EditorUtility.DisplayDialog("Profile Saved", 
                $"Model profile '{newModelProfileName}' saved.", "OK");
        }

        private void SaveCurrentAudioProfile()
        {
            if (string.IsNullOrEmpty(newAudioProfileName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter an audio profile name.", "OK");
                return;
            }
            AudioOptimizationProfile newProfile = new AudioOptimizationProfile()
            {
                profileName = newAudioProfileName,
                audioFormat = audioFormat,
                audioQuality = audioQuality,
                audioLoadType = audioLoadType
            };
            int index = optimizationProfiles.audioProfiles
                .FindIndex(p => p.profileName == newAudioProfileName);
            if (index >= 0)
                optimizationProfiles.audioProfiles[index] = newProfile;
            else
            {
                optimizationProfiles.audioProfiles.Add(newProfile);
                selectedAudioProfileIndex = optimizationProfiles.audioProfiles.Count - 1;
            }
            SaveProfiles();
            EditorUtility.DisplayDialog("Profile Saved", 
                $"Audio profile '{newAudioProfileName}' saved.", "OK");
        }

        #endregion

        #region Preview Data

        private void BuildPreviewData()
        {
            if (useManualSelection)
            {
                previewTextureCount = manualOptimizeSelection.Count(kv => kv.Value && IsTexture(kv.Key));
                previewModelCount = manualOptimizeSelection.Where(kv => kv.Value && IsModel(kv.Key)).Count();
                previewAudioCount = manualOptimizeSelection.Where(kv => kv.Value && IsAudio(kv.Key)).Count();
                previewMaterialCount = manualOptimizeSelection.Where(kv => kv.Value && IsMaterial(kv.Key)).Count();
                previewSpriteCount = manualAtlasSelection.Where(kv => kv.Value && IsSprite(kv.Key)).Count();
            }
            else
            {
                previewTextureCount = CountEligibleAssets("t:Texture");
                previewModelCount = CountEligibleAssets("t:Model");
                previewAudioCount = CountEligibleAssets("t:AudioClip");
                previewMaterialCount = CountEligibleAssets("t:Material");
                previewSpriteCount = CountEligibleAssets("t:Sprite");
            }
        }

        private int CountEligibleAssets(string filter)
        {
            return GetCachedAssetList(filter).Count;
        }

        private List<string> GetAssetExamples(string filter, int max)
        {
            List<string> list = GetCachedAssetList(filter);
            return list.Take(max).Select(x => Path.GetFileName(x)).ToList();
        }

        private bool MatchesLabelFilter(Object asset)
        {
            if (string.IsNullOrEmpty(labelFilter))
                return true;
            string[] requiredLabels = labelFilter.Split(',').Select(l => l.Trim()).ToArray();
            string[] assetLabels = AssetDatabase.GetLabels(asset);
            foreach (string label in requiredLabels)
            {
                if (assetLabels.Contains(label))
                    return true;
            }
            return false;
        }

        #endregion

        #region Statistics Graph

        private void DrawStatisticsGraph()
        {
            int maxCount = Mathf.Max(texturesOptimizedCount, modelsOptimizedCount, audioOptimizedCount, 
                (spriteAtlasGeneratedFlag ? 1 : 0), materialsOptimizedCount);
            if (maxCount == 0)
            {
                GUILayout.Label("No data available for statistics.");
                return;
            }
            GUILayout.BeginVertical("box");
            DrawStatBar("Textures", texturesOptimizedCount, maxCount);
            DrawStatBar("Models", modelsOptimizedCount, maxCount);
            DrawStatBar("Audio", audioOptimizedCount, maxCount);
            DrawStatBar("Materials", materialsOptimizedCount, maxCount);
            DrawStatBar("Sprite Atlas", spriteAtlasGeneratedFlag ? 1 : 0, maxCount);
            GUILayout.EndVertical();
        }

        private void DrawStatBar(string label, int value, int maxValue)
        {
            float fraction = (float)value / maxValue;
            Rect rect = GUILayoutUtility.GetRect(50, 20);
            EditorGUI.ProgressBar(rect, fraction, $"{label}: {value}");
            GUILayout.Space(5);
        }

        #endregion

        #region Helper Methods

        private bool IsTexture(string path)
        {
            return AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(Texture2D);
        }

        private bool IsModel(string path)
        {
            return AssetImporter.GetAtPath(path) is ModelImporter;
        }

        private bool IsAudio(string path)
        {
            return AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(AudioClip);
        }

        private bool IsSprite(string path)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset is Sprite)
                return true;
            if (asset is Texture2D)
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.spriteImportMode != SpriteImportMode.None)
                    return true;
            }
            return false;
        }

        private bool IsMaterial(string path)
        {
            return AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(Material);
        }

        #endregion

        #region Report Exportation

        private void ExportReport()
        {
            string report = "";
            string format = exportFormatOptions[selectedExportFormat];
            if (format == "CSV")
            {
                report += "Category,Count\n";
                report += $"Textures,{texturesOptimizedCount}\n";
                report += $"Models,{modelsOptimizedCount}\n";
                report += $"Audio,{audioOptimizedCount}\n";
                report += $"Materials,{materialsOptimizedCount}\n";
                report += $"Sprite Atlas,{(spriteAtlasGeneratedFlag ? 1 : 0)}\n";
                report += "\nDetailed Log\n";
                foreach (string log in optimizationLog)
                    report += "\"" + log.Replace("\"", "\"\"") + "\"\n";
            }
            else if (format == "JSON")
            {
                var reportData = new {
                    Summary = new {
                        TexturesOptimized = texturesOptimizedCount,
                        ModelsProcessed = modelsOptimizedCount,
                        AudioOptimized = audioOptimizedCount,
                        MaterialsOptimized = materialsOptimizedCount,
                        SpriteAtlasGenerated = spriteAtlasGeneratedFlag
                    },
                    Log = optimizationLog
                };
                report = JsonUtility.ToJson(reportData, true);
            }
            else if (format == "HTML")
            {
                report += "<html><head><title>Optimization Report</title></head><body>";
                report += "<h1>Optimization Summary</h1>";
                report += $"<p>Textures Optimized: {texturesOptimizedCount}</p>";
                report += $"<p>Models Processed: {modelsOptimizedCount}</p>";
                report += $"<p>Audio Clips Optimized: {audioOptimizedCount}</p>";
                report += $"<p>Materials Optimized: {materialsOptimizedCount}</p>";
                report += $"<p>Sprite Atlas Generated: {(spriteAtlasGeneratedFlag ? "Yes" : "No")}</p>";
                report += "<h2>Detailed Log</h2><ul>";
                foreach (string log in optimizationLog)
                    report += $"<li>{log}</li>";
                report += "</ul></body></html>";
            }

            string path = EditorUtility.SaveFilePanel("Save Report", "", 
                "OptimizationReport." + format.ToLower(), format.ToLower());
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, report);
                EditorUtility.DisplayDialog("Export Report", "Report exported successfully.", "OK");
            }
        }

        #endregion

        #region Audit Logging

        private void LogAuditEntry(string category, string assetPath, string details)
        {
            string entry = string.Format("{0:yyyy-MM-dd HH:mm:ss} | {1} | {2} | {3}",
                System.DateTime.Now, category, assetPath, details);
            File.AppendAllText(auditLogFilePath, entry + "\n");
        }

        #endregion

        #region CI/CD Integration

        public static void RunOptimizationCI()
        {
            AssetOptimizer window = CreateInstance<AssetOptimizer>();
            Stopwatch sw = new Stopwatch();

            sw.Start();
            window.ExecuteOptimization();
            sw.Stop();

            double elapsedSeconds = sw.Elapsed.TotalSeconds;
            Debug.Log($"[CI] Optimization completed in {elapsedSeconds:F2} seconds.");
            if (elapsedSeconds > 30.0)
            {
                Debug.LogError("[CI] Optimization exceeded the time threshold. Please investigate performance issues.");
                EditorApplication.Exit(1);
            }
            else
            {
                EditorApplication.Exit(0);
            }
        }

        #endregion
    }
}
#endif
