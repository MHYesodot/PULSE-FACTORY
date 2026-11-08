using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace PulseFactory.Editor
{
    public static class FontAssetBuilder
    {
        private const string SourcePath = "Assets/Fonts/Sources/NotoSansHebrew[wght,wdth].ttf";
        private const string AssetPath = "Assets/Resources/Fonts/NotoSansHebrew SDF.asset";

        [MenuItem("Pulse Factory/Create Licensed Bilingual TMP Font")]
        public static void CreateBilingualFont()
        {
            if (TMP_Settings.instance == null)
            {
                AssetDatabase.importPackageCompleted += OnEssentialsImported;
                AssetDatabase.importPackageFailed += OnEssentialsImportFailed;
                AssetDatabase.importPackageCancelled += OnEssentialsImportCancelled;
                ImportEssentialResources();
                return;
            }

            BuildFontAsset();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void BuildFontAsset()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (source == null) throw new InvalidOperationException("Missing licensed Noto Sans Hebrew source font: " + SourcePath);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetPath);
            bool isNewAsset = font == null;
            if (isNewAsset)
            {
                font = TMP_FontAsset.CreateFontAsset(source, 96, 8, GlyphRenderMode.SDFAA,
                    2048, 2048, AtlasPopulationMode.Dynamic, true);
                if (font == null) throw new InvalidOperationException("Could not create TMP font asset from " + SourcePath);
            }
            else font.atlasPopulationMode = AtlasPopulationMode.Dynamic;

            var characters = new HashSet<char>();
            for (char c = (char)32; c < 127; c++) characters.Add(c);
            for (int c = 0x05D0; c <= 0x05EA; c++) characters.Add((char)c);
            characters.Add('…');
            foreach (var path in new[] { "Assets/Resources/Localization/en.json", "Assets/Resources/Localization/he.json" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset == null) throw new InvalidOperationException("Missing localization source: " + path);
                var locale = JsonUtility.FromJson<LocaleData>(asset.text);
                if (locale?.entries == null) throw new InvalidOperationException("Malformed localization source: " + path);
                foreach (var entry in locale.entries)
                {
                    if (entry?.value == null) throw new InvalidOperationException("Missing localization value in " + path);
                    foreach (char character in entry.value) characters.Add(character);
                }
            }
            if (!font.TryAddCharacters(new string(characters.ToArray()), out string missing))
            {
                UnityEngine.Object.DestroyImmediate(font);
                throw new InvalidOperationException("Font is missing required UI glyphs: " + missing);
            }
            font.atlasPopulationMode = AtlasPopulationMode.Static;

            if (!AssetDatabase.IsValidFolder("Assets/Resources/Fonts"))
                AssetDatabase.CreateFolder("Assets/Resources", "Fonts");
            if (isNewAsset)
            {
                AssetDatabase.CreateAsset(font, AssetPath);
                foreach (var atlas in font.atlasTextures)
                    if (atlas != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(atlas)))
                        AssetDatabase.AddObjectToAsset(atlas, font);
                if (font.material != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(font.material)))
                    AssetDatabase.AddObjectToAsset(font.material, font);
            }
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AssetPath);
            Debug.Log("Created static bilingual TMP font. Required glyph count: " + font.characterTable.Count);
        }

        private static void OnEssentialsImported(string packageName)
        {
            if (packageName != "TMP Essential Resources") return;
            UnsubscribeImportCallbacks();
            try
            {
                if (TMP_Settings.instance == null)
                    throw new InvalidOperationException("TMP Essential Resources imported without a TMP Settings asset.");
                BuildFontAsset();
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void OnEssentialsImportFailed(string packageName, string error)
        {
            if (packageName != "TMP Essential Resources") return;
            UnsubscribeImportCallbacks();
            Debug.LogError("TMP Essential Resources import failed: " + error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static void OnEssentialsImportCancelled(string packageName)
        {
            if (packageName != "TMP Essential Resources") return;
            UnsubscribeImportCallbacks();
            Debug.LogError("TMP Essential Resources import was cancelled.");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        private static void UnsubscribeImportCallbacks()
        {
            AssetDatabase.importPackageCompleted -= OnEssentialsImported;
            AssetDatabase.importPackageFailed -= OnEssentialsImportFailed;
            AssetDatabase.importPackageCancelled -= OnEssentialsImportCancelled;
        }

        [MenuItem("Pulse Factory/Import TMP Essential Resources")]
        public static void ImportEssentialResources()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                "Packages/com.unity.ugui/Runtime/TMP/TMP_Settings.cs");
            if (package == null || string.IsNullOrEmpty(package.resolvedPath))
                throw new InvalidOperationException("Could not resolve the installed uGUI package for TMP essentials.");
            var path = System.IO.Path.Combine(package.resolvedPath, "Package Resources/TMP Essential Resources.unitypackage");
            if (!System.IO.File.Exists(path))
                throw new InvalidOperationException("The installed uGUI package does not contain TMP Essential Resources: " + path);
            AssetDatabase.ImportPackage(path, false);
        }

        [Serializable]
        private sealed class LocaleData
        {
            public string locale;
            public bool rtl;
            public Entry[] entries;
        }

        [Serializable]
        private sealed class Entry
        {
            public string key;
            public string value;
        }
    }
}
