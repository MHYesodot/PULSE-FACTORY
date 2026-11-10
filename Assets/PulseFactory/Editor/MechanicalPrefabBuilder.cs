using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PulseFactory.Presentation;
using UnityEditor;
using UnityEngine;

namespace PulseFactory.Editor
{
    public static class MechanicalPrefabBuilder
    {
        private const string Root = "Assets/PulseFactory/Mechanical";
        private const string ModelPath = Root + "/Models/";
        private const string PrefabPath = Root + "/Prefabs/";
        private const string MaterialPath = Root + "/Materials/";
        private const string ResourcesPath = Root + "/Resources/PulseFactoryMechanical/";
        private const string ReportPath = "Artifacts/Phase4DUnityIntegration/unity-import-report.json";

        [MenuItem("Pulse Factory/Build Mechanical Prefabs")]
        public static void Build()
        {
            Directory.CreateDirectory(PrefabPath);
            Directory.CreateDirectory(MaterialPath);
            Directory.CreateDirectory(ResourcesPath);
            AssetDatabase.Refresh();

            var report = new ImportReport { UnityVersion = Application.unityVersion };
            var conveyorModel = LoadModel("conveyor_module.fbx", report);
            var diverterModel = LoadModel("diverter_module.fbx", report);
            var redModel = LoadModel("cargo_red.fbx", report);
            var blueModel = LoadModel("cargo_blue.fbx", report);

            var conveyor = CreateModelPrefab("PF_ConveyorModule", conveyorModel, "conveyor_module.fbx");
            var diverter = CreateModelPrefab("PF_DiverterAssembly", diverterModel, "diverter_module.fbx");
            var redCrate = CreateModelPrefab("PF_CargoRed", redModel, "cargo_red.fbx");
            var blueCrate = CreateModelPrefab("PF_CargoBlue", blueModel, "cargo_blue.fbx");

            Material red = CreateMaterial("CargoRed", new Color(0.78f, 0.16f, 0.13f), 0.12f);
            Material blue = CreateMaterial("CargoBlue", new Color(0.12f, 0.35f, 0.62f), 0.12f);
            Material neutral = CreateMaterial("FactoryNeutral", new Color(0.12f, 0.18f, 0.21f), 0.42f);
            Material steel = CreateMaterial("FactorySteel", new Color(0.38f, 0.49f, 0.53f), 0.58f);
            Material active = CreateMaterial("RouteActive", new Color(0.24f, 0.88f, 0.64f), 0.08f);
            Material inactive = CreateMaterial("RouteInactive", new Color(0.11f, 0.16f, 0.18f), 0.04f);
            Material board = CreateMaterial("BoardDeck", new Color(0.12f, 0.16f, 0.17f), 0.24f);

            AddCargoSymbols(redCrate, red, blueCrate, blue);
            var source = CreateStationPrefab("PF_SourceStation", steel, neutral, null);
            var redDestination = CreateStationPrefab("PF_DestinationRed", steel, neutral, red);
            var blueDestination = CreateStationPrefab("PF_DestinationBlue", steel, neutral, blue);

            var assets = LoadOrCreateAsset<MechanicalBoardAssets>(
                ResourcesPath + "MechanicalBoardAssets.asset");
            assets.ConveyorModule = conveyor;
            assets.DiverterAssembly = diverter;
            assets.RedPackage = redCrate;
            assets.BluePackage = blueCrate;
            assets.SourceStation = source;
            assets.RedDestination = redDestination;
            assets.BlueDestination = blueDestination;
            assets.BoardDeck = board;
            assets.ActiveRoute = active;
            assets.InactiveRoute = inactive;
            assets.PackageRed = red;
            assets.PackageBlue = blue;
            EditorUtility.SetDirty(assets);

            var settings = LoadOrCreateAsset<MechanicalBoardSettings>(
                ResourcesPath + "MechanicalBoardSettings.asset");
            settings.Mode = BoardPresentationMode.Mechanical3DLevel2;
            settings.Assets = assets;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.Prefabs = new[]
            {
                AssetDatabase.GetAssetPath(conveyor),
                AssetDatabase.GetAssetPath(diverter),
                AssetDatabase.GetAssetPath(redCrate),
                AssetDatabase.GetAssetPath(blueCrate),
                AssetDatabase.GetAssetPath(source),
                AssetDatabase.GetAssetPath(redDestination),
                AssetDatabase.GetAssetPath(blueDestination)
            };
            report.DiverterArmPivot = FindChildPath(diverter.transform, "ASSET_DIVERTER_ArmPivot");
            report.DiverterRoutingArm = FindChildPath(diverter.transform, "ASSET_DIVERTER_RoutingArm");
            if (string.IsNullOrEmpty(report.DiverterArmPivot) || string.IsNullOrEmpty(report.DiverterRoutingArm))
                throw new InvalidOperationException("The Unity-imported diverter prefab does not preserve the arm pivot hierarchy.");

            string reportPath = Path.GetFullPath(ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            AssetDatabase.Refresh();
            Debug.Log("Mechanical prefabs generated. Unity FBX import report: " + reportPath);
        }

        private static GameObject LoadModel(string fileName, ImportReport report)
        {
            string path = ModelPath + fileName;
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                throw new FileNotFoundException("Unity did not import the expected FBX model.", path);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
                throw new InvalidOperationException("Unity could not load FBX model: " + path);
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("Imported FBX has no renderers: " + path);
            if (renderers.Any(renderer => renderer.sharedMaterials.Length == 0
                || renderer.sharedMaterials.Any(material => material == null)))
                throw new InvalidOperationException("Imported FBX contains a renderer without materials: " + path);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            var bounds = BoundsOf(instance.GetComponentsInChildren<Renderer>(true));
            report.Models.Add(new ImportedModel
            {
                File = path,
                RootName = model.name,
                RendererCount = renderers.Length,
                MaterialCount = renderers.SelectMany(renderer => renderer.sharedMaterials).Distinct().Count(),
                BoundsMeters = new[] { bounds.size.x, bounds.size.y, bounds.size.z },
                MeshNames = instance.GetComponentsInChildren<MeshFilter>(true)
                    .Select(filter => filter.sharedMesh.name).Distinct().ToArray(),
                Hierarchy = DescribeHierarchy(instance.transform)
            });
            UnityEngine.Object.DestroyImmediate(instance);
            return model;
        }

        private static GameObject CreateModelPrefab(string name, GameObject model, string source)
        {
            var root = new GameObject(name);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = model.name;
            instance.transform.SetParent(root.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
            var sourceGuid = AssetDatabase.AssetPathToGUID(ModelPath + source);
            var prefabPath = AssetDatabase.GetAssetPath(prefab);
            var prefabImporter = AssetImporter.GetAtPath(prefabPath);
            if (string.IsNullOrEmpty(sourceGuid) || prefabImporter == null)
                throw new InvalidOperationException("Failed to create a prefab from " + source);
            return prefab;
        }

        private static void AddCargoSymbols(GameObject redPrefab, Material red, GameObject bluePrefab, Material blue)
        {
            AddSymbolToPrefab(redPrefab, "Package red diamond", PrimitiveType.Cube, red,
                new Vector3(0, 0.492f, 0), new Vector3(0.12f, 0.018f, 0.12f),
                Quaternion.Euler(0, 45, 0));
            AddSymbolToPrefab(bluePrefab, "Package blue circle", PrimitiveType.Cylinder, blue,
                new Vector3(0, 0.492f, 0), new Vector3(0.12f, 0.018f, 0.12f), Quaternion.identity);
        }

        private static void AddSymbolToPrefab(GameObject prefab, string symbolName, PrimitiveType shape,
            Material material, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
            var symbol = GameObject.CreatePrimitive(shape);
            symbol.name = symbolName;
            symbol.transform.SetParent(root.transform, false);
            symbol.transform.localPosition = position;
            symbol.transform.localScale = scale;
            symbol.transform.localRotation = rotation;
            RemoveCollider(symbol);
            symbol.GetComponent<Renderer>().sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(root, AssetDatabase.GetAssetPath(prefab));
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static GameObject CreateStationPrefab(string name, Material frame, Material surface, Material accepted)
        {
            var root = new GameObject(name);
            AddPrimitive(root.transform, "Station base", PrimitiveType.Cube,
                new Vector3(0, 0.15f, 0), new Vector3(0.92f, 0.3f, 0.82f), frame);
            if (accepted == null)
            {
                AddPrimitive(root.transform, "Source well", PrimitiveType.Cylinder,
                    new Vector3(0, 0.34f, 0), new Vector3(0.46f, 0.08f, 0.46f), surface);
                AddPrimitive(root.transform, "Source throat", PrimitiveType.Cylinder,
                    new Vector3(0, 0.39f, 0), new Vector3(0.22f, 0.035f, 0.22f), frame);
            }
            else
            {
                AddPrimitive(root.transform, "Destination receiver", PrimitiveType.Cube,
                    new Vector3(0, 0.34f, 0), new Vector3(0.72f, 0.09f, 0.62f), accepted);
                AddPrimitive(root.transform, "Destination inset", PrimitiveType.Cube,
                    new Vector3(0, 0.395f, 0), new Vector3(0.42f, 0.035f, 0.36f), surface);
                AddPrimitive(root.transform, "Destination identity", accepted == null
                        ? PrimitiveType.Cube
                        : accepted.name.IndexOf("CargoRed", StringComparison.Ordinal) >= 0
                            ? PrimitiveType.Cube : PrimitiveType.Cylinder,
                    new Vector3(0.15f, 0.42f, 0.08f), new Vector3(0.14f, 0.025f, 0.14f), accepted);
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject AddPrimitive(Transform parent, string name, PrimitiveType type,
            Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            RemoveCollider(item);
            return item;
        }

        private static void RemoveCollider(GameObject target)
        {
            foreach (var component in target.GetComponents<Component>())
                if (component.GetType().Name.IndexOf("Collider", StringComparison.Ordinal) >= 0)
                    UnityEngine.Object.DestroyImmediate(component);
        }

        private static Material CreateMaterial(string name, Color color, float metallic)
        {
            string path = MaterialPath + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("Unity Standard shader is unavailable.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.name = name;
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", 0.35f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            if (File.Exists(path) && !AssetDatabase.DeleteAsset(path))
                throw new IOException("Could not replace the stale generated asset: " + path);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Bounds BoundsOf(Renderer[] renderers)
        {
            if (renderers.Length == 0) return new Bounds();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static string FindChildPath(Transform root, string name)
        {
            if (root.name.IndexOf(name, StringComparison.Ordinal) >= 0) return root.name;
            for (int i = 0; i < root.childCount; i++)
            {
                string path = FindChildPath(root.GetChild(i), name);
                if (!string.IsNullOrEmpty(path)) return root.name + "/" + path;
            }
            return null;
        }

        private static string[] DescribeHierarchy(Transform root)
        {
            var result = new List<string>();
            void Visit(Transform current, string prefix)
            {
                result.Add(prefix + current.name);
                for (int i = 0; i < current.childCount; i++)
                    Visit(current.GetChild(i), prefix + "  ");
            }
            Visit(root, "");
            return result.ToArray();
        }

        [Serializable]
        private sealed class ImportReport
        {
            public string UnityVersion;
            public List<ImportedModel> Models = new List<ImportedModel>();
            public string[] Prefabs;
            public string DiverterArmPivot;
            public string DiverterRoutingArm;
        }

        [Serializable]
        private sealed class ImportedModel
        {
            public string File;
            public string RootName;
            public int RendererCount;
            public int MaterialCount;
            public float[] BoundsMeters;
            public string[] MeshNames;
            public string[] Hierarchy;
        }
    }
}
