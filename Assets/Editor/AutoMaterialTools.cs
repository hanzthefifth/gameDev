using System.IO;
using UnityEditor;
using UnityEngine;

public static class AutoMaterialTools
{
    const string HdrpLitShaderName = "HDRP/Lit";

    // --------- Helpers ---------

    
    private static Texture2D GetBestBaseTextureFromSourceMaterial(Material src)
    {
        if (src == null) return null;

        // Try HDRP, URP, then built-in Standard
        return src.GetTexture("_BaseColorMap") as Texture2D   // HDRP
            ?? src.GetTexture("_BaseMap") as Texture2D        // URP
            ?? src.GetTexture("_MainTex") as Texture2D;       // Built-in
    }

    private static void CreateOrUpdateHdrpMaterialFromSourceMaterial(
        Material src,
        Shader hdrpShader,
        string materialsFolder,
        ref int createdCount,
        ref int updatedCount)
    {
        if (src == null) return;

        string matName = src.name; // e.g. "HandMaterial"
        string matPath = Path.Combine(materialsFolder, matName + ".mat").Replace("\\", "/");

        var dst = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        bool isNew = dst == null;

        if (isNew)
        {
            dst = new Material(hdrpShader);
            AssetDatabase.CreateAsset(dst, matPath);
            createdCount++;
        }
        else
        {
            dst.shader = hdrpShader;
            updatedCount++;
        }

        Texture2D baseTex = GetBestBaseTextureFromSourceMaterial(src);
        ApplyHdrpDefaultsToMaterial(dst, baseTex);
        EditorUtility.SetDirty(dst);
    }

    [MenuItem("Tools/AutoMaterials/Step 1c: Create HDRP Materials From Model Materials (Choose Models Folder...)")]
    public static void CreateHdrpMaterials_FromModelMaterials()
    {
        var shader = Shader.Find(HdrpLitShaderName);
        if (shader == null)
        {
            Debug.LogError($"Shader '{HdrpLitShaderName}' not found. Make sure HDRP is active.");
            return;
        }

        // Let user pick the Models folder
        string modelsFolderAbs = EditorUtility.OpenFolderPanel(
            "Select Models Folder (inside Assets)",
            Application.dataPath,
            ""
        );

        if (string.IsNullOrEmpty(modelsFolderAbs))
            return;

        if (!TryMakeProjectRelative(modelsFolderAbs, out string modelsFolder))
            return;

        if (!AssetDatabase.IsValidFolder(modelsFolder))
        {
            Debug.LogError($"[AutoMaterials] Folder '{modelsFolder}' is not a valid project folder.");
            return;
        }

        // Pack root = parent of Models folder
        string packRoot = Path.GetDirectoryName(modelsFolder).Replace("\\", "/");
        if (string.IsNullOrEmpty(packRoot))
        {
            Debug.LogError($"[AutoMaterials] Could not determine pack root from '{modelsFolder}'.");
            return;
        }

        string materialsFolder = EnsureSubfolder(packRoot, "Materials");
        if (materialsFolder == null)
            return;

        int createdCount = 0;
        int updatedCount = 0;

        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { modelsFolder });
        foreach (var guid in modelGuids)
        {
            string modelPath = AssetDatabase.GUIDToAssetPath(guid);
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
                continue;

            var renderers = modelAsset.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                continue;

            foreach (var r in renderers)
            {
                var srcMats = r.sharedMaterials;
                if (srcMats == null) continue;

                foreach (var srcMat in srcMats)
                {
                    CreateOrUpdateHdrpMaterialFromSourceMaterial(
                        srcMat,
                        shader,
                        materialsFolder,
                        ref createdCount,
                        ref updatedCount
                    );
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[AutoMaterials] (From Model Materials) Created {createdCount} HDRP material(s), updated {updatedCount} under '{materialsFolder}'.");
    }





    // Convert absolute OS path -> "Assets/..." project-relative path
    private static bool TryMakeProjectRelative(string absolutePath, out string projectRelative)
    {
        projectRelative = null;
        if (string.IsNullOrEmpty(absolutePath))
            return false;

        string dataPath = Application.dataPath.Replace("\\", "/");
        absolutePath = absolutePath.Replace("\\", "/");

        if (!absolutePath.StartsWith(dataPath))
        {
            Debug.LogError("Selected folder is not inside this Unity project (must be under Assets/).");
            return false;
        }

        projectRelative = "Assets" + absolutePath.Substring(dataPath.Length);
        return true;
    }

    private static string EnsureSubfolder(string parentFolder, string childName)
    {
        parentFolder = parentFolder.Replace("\\", "/");
        string sub = Path.Combine(parentFolder, childName).Replace("\\", "/");
        if (!AssetDatabase.IsValidFolder(sub))
        {
            if (!AssetDatabase.IsValidFolder(parentFolder))
            {
                Debug.LogError($"Parent folder '{parentFolder}' does not exist in project.");
                return null;
            }

            AssetDatabase.CreateFolder(parentFolder, childName);
        }
        return sub;
    }

    // =========================================================
    // STEP 1A: CREATE HDRP MATERIALS (NEW ONLY) → Materials/
    // =========================================================

    [MenuItem("Tools/AutoMaterials/Step 1a: Create HDRP Materials (New Only, Choose Textures Folder...)")]
    public static void CreateHdrpMaterials_NewOnly()
    {
        var shader = Shader.Find(HdrpLitShaderName);
        if (shader == null)
        {
            Debug.LogError($"Shader '{HdrpLitShaderName}' not found. Make sure HDRP is active.");
            return;
        }

        string chosen = EditorUtility.OpenFolderPanel(
            "Select Textures Folder (inside Assets)",
            Application.dataPath,
            ""
        );

        if (!TryMakeProjectRelative(chosen, out string texturesFolder))
            return;

        // Pack root is parent of Textures folder
        string packRoot = Path.GetDirectoryName(texturesFolder).Replace("\\", "/");
        string materialsFolder = EnsureSubfolder(packRoot, "Materials");
        if (materialsFolder == null)
            return;

        int createdCount = 0;

        string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { texturesFolder });
        foreach (var guid in texGuids)
        {
            string texPath = AssetDatabase.GUIDToAssetPath(guid);
            CreateMaterialForTexture_NewOnly(texPath, shader, materialsFolder, ref createdCount);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[AutoMaterials] (New Only) Created {createdCount} HDRP material(s) under '{materialsFolder}'.");
    }

    private static void CreateMaterialForTexture_NewOnly(string texPath,
                                                         Shader shader,
                                                         string materialsFolder,
                                                         ref int createdCount)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null)
            return;

        string fileName = Path.GetFileNameWithoutExtension(texPath);
        string lower = fileName.ToLowerInvariant();

        // Skip obvious normal maps; tweak to match your naming
        if (lower.Contains("normal") || lower.Contains("_nrm") || lower.Contains("_nor"))
            return;

        string matPath = Path.Combine(materialsFolder, fileName + ".mat").Replace("\\", "/");

        var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (existingMat != null)
            return; // do not overwrite in this mode

        var mat = new Material(shader);

        ApplyHdrpDefaultsToMaterial(mat, tex);

        AssetDatabase.CreateAsset(mat, matPath);
        createdCount++;
    }

    // =========================================================
    // STEP 1B: UPDATE/OVERWRITE HDRP MATERIALS (KEEP REFERENCES)
    //          For all textures, ensure a mat exists and reset
    //          its properties to our template values.
    // =========================================================

    [MenuItem("Tools/AutoMaterials/Step 1b: Update HDRP Materials (Overwrite Properties, Choose Textures Folder...)")]
    public static void UpdateHdrpMaterials_Overwrite()
    {
        var shader = Shader.Find(HdrpLitShaderName);
        if (shader == null)
        {
            Debug.LogError($"Shader '{HdrpLitShaderName}' not found. Make sure HDRP is active.");
            return;
        }

        string chosen = EditorUtility.OpenFolderPanel(
            "Select Textures Folder (inside Assets)",
            Application.dataPath,
            ""
        );

        if (!TryMakeProjectRelative(chosen, out string texturesFolder))
            return;

        string packRoot = Path.GetDirectoryName(texturesFolder).Replace("\\", "/");
        string materialsFolder = EnsureSubfolder(packRoot, "Materials");
        if (materialsFolder == null)
            return;

        int touchedCount = 0;
        int createdCount = 0;

        string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { texturesFolder });
        foreach (var guid in texGuids)
        {
            string texPath = AssetDatabase.GUIDToAssetPath(guid);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
                continue;

            string fileName = Path.GetFileNameWithoutExtension(texPath);
            string lower = fileName.ToLowerInvariant();

            if (lower.Contains("normal") || lower.Contains("_nrm") || lower.Contains("_nor"))
                continue;

            string matPath = Path.Combine(materialsFolder, fileName + ".mat").Replace("\\", "/");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
                createdCount++;
            }

            // Ensure correct shader and reset properties
            mat.shader = shader;
            ApplyHdrpDefaultsToMaterial(mat, tex);
            EditorUtility.SetDirty(mat);
            touchedCount++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[AutoMaterials] (Overwrite) Updated {touchedCount} material(s), created {createdCount} new under '{materialsFolder}'.");
    }


    /// <summary>
    /// Central place for your "style": smoothness, metallic, emissive, etc.
    /// Changing this and running Step 1b effectively updates all mats.
    /// </summary>
    private static void ApplyHdrpDefaultsToMaterial(Material mat, Texture2D baseTex)
    {
         // Base color
        if (baseTex != null)
        {
            mat.SetTexture("_BaseColorMap", baseTex);
            mat.SetTexture("_EmissiveColorMap", baseTex);
        }
        else
        {
            mat.SetTexture("_BaseColorMap", null);
            mat.SetTexture("_EmissiveColorMap", null);
        }
       
        mat.SetFloat("_Smoothness", 0.0f);    // adjust for HL2/PSX flavor
        mat.SetFloat("_Metallic", 0.0f);      // mostly non-metallic

        // Emissive from same texture (low "self-lit" look)
        mat.SetTexture("_EmissiveColorMap", baseTex);
        mat.SetColor("_EmissiveColor", Color.white);
        mat.SetFloat("_EmissiveIntensity", 0.1f); // tweak globally as needed
        mat.EnableKeyword("_EMISSIVE_COLOR_MAP");
        mat.EnableKeyword("_EMISSIVE");
    }

    // =========================================================
    // STEP 2: BUILD PREFABS FROM MODELS → Prefabs/
    //         Uses Materials/ folder to assign HDRP mats by name
    // =========================================================

    [MenuItem("Tools/AutoMaterials/Step 2: Build Prefabs From Models Folder (Mirror Structure)")]
    public static void BuildPrefabsFromModelsFolder()
    {
        // Let the user pick the Models folder (must be inside Assets)
        string modelsFolderAbs = EditorUtility.OpenFolderPanel(
            "Select Models Folder (inside Assets)",
            Application.dataPath,
            ""
        );

        if (string.IsNullOrEmpty(modelsFolderAbs))
            return; // user cancelled

        if (!TryMakeProjectRelative(modelsFolderAbs, out string modelsFolder))
        {
            Debug.LogError($"[AutoMaterials] Selected folder '{modelsFolderAbs}' is not under this project's Assets folder.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(modelsFolder))
        {
            Debug.LogError($"[AutoMaterials] Folder '{modelsFolder}' is not a valid project folder.");
            return;
        }

        // Pack root = parent of Models folder
        // e.g. Assets/psxAssets/.../DEMO - PSX Mega Pack II
        string packRoot = Path.GetDirectoryName(modelsFolder).Replace("\\", "/");
        if (string.IsNullOrEmpty(packRoot))
        {
            Debug.LogError($"[AutoMaterials] Could not determine pack root from '{modelsFolder}'.");
            return;
        }

        // Prefabs root under the pack
        string prefabsRoot = Path.Combine(packRoot, "Prefabs").Replace("\\", "/");
        EnsureFolderExists(prefabsRoot);

        int createdCount = 0;
        int updatedCount = 0;
        int totalModels  = 0;

        // Find all model assets (FBX etc.) under Models
        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { modelsFolder });
        foreach (var guid in modelGuids)
        {
            string modelPath = AssetDatabase.GUIDToAssetPath(guid);
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (modelAsset == null)
                continue;

            totalModels++;

            // --- MIRROR FOLDER STRUCTURE HERE ---

            // modelPath:   Assets/.../Models/Decals/Graffiti.fbx
            // modelsFolder:Assets/.../Models
            // relative:    Decals/Graffiti.fbx
            string relativePath = modelPath.Substring(modelsFolder.Length).TrimStart('/', '\\');

            string relativeDir    = Path.GetDirectoryName(relativePath) ?? string.Empty;          // "Decals"
            string prefabFileName = Path.GetFileNameWithoutExtension(relativePath) + ".prefab";  // "Graffiti.prefab"

            // Build prefab directory under Prefabs root, mirroring the relative dir
            string prefabDir = string.IsNullOrEmpty(relativeDir)
                ? prefabsRoot
                : Path.Combine(prefabsRoot, relativeDir).Replace("\\", "/");

            EnsureFolderExists(prefabDir);

            string prefabPath = Path.Combine(prefabDir, prefabFileName).Replace("\\", "/");

            // -------------------------------------

            // Create or update prefab
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab == null)
            {
                // New prefab
                var newPrefab = PrefabUtility.SaveAsPrefabAsset(modelAsset, prefabPath);
                if (newPrefab != null)
                {
                    createdCount++;

                    // If you had logic to assign materials or tweak components on new prefabs,
                    // put it here; e.g.:
                    // AssignMaterialsOnPrefab(newPrefab);
                }
            }
            else
            {
                // Update existing prefab if you want to sync from model
                // For now, we simply ensure it exists; you can add your previous updating logic.
                // Example of a simple refresh:
                // PrefabUtility.SaveAsPrefabAssetAndConnect(modelAsset, prefabPath, InteractionMode.AutomatedAction);

                updatedCount++;

                // If you had logic that traversed children and swapped materials, call it here:
                // UpdatePrefabMaterials(existingPrefab);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[AutoMaterials] Built/updated {totalModels} model(s): created {createdCount} prefab(s), updated {updatedCount} prefab(s) under '{prefabsRoot}'.");
    }


    private static void EnsureFolderExists(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        // AssetDatabase.CreateFolder works one level at a time
        // so we split and build incrementally
        string[] parts = path.Split(new[] { '/', '\\' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        string current = parts[0];
        if (!current.StartsWith("Assets"))
            current = "Assets/" + current;

        for (int i = 1; i < parts.Length; i++)
        {
            string parent = current;
            current = parent + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(current))
            {
                AssetDatabase.CreateFolder(parent, parts[i]);
            }
        }
    }

    // =========================================================
    // STEP 3: UPDATE PREFABS' MATERIAL SLOTS FROM Materials/
    //         (after you change materials via Step 1b)
    // =========================================================

    [MenuItem("Tools/AutoMaterials/Step 3: Update Prefabs Materials From Pack Materials (Choose Prefabs Folder...)")]
    public static void UpdatePrefabsMaterialsFromPack()
    {
        string chosen = EditorUtility.OpenFolderPanel(
            "Select Prefabs Folder (inside Assets)",
            Application.dataPath,
            ""
        );

        if (!TryMakeProjectRelative(chosen, out string prefabsFolder))
            return;

        string packRoot = Path.GetDirectoryName(prefabsFolder).Replace("\\", "/");
        string materialsFolder = Path.Combine(packRoot, "Materials").Replace("\\", "/");

        if (!AssetDatabase.IsValidFolder(materialsFolder))
        {
            Debug.LogError($"Materials folder '{materialsFolder}' not found next to '{prefabsFolder}'. " +
                           "Cannot update prefab materials from pack.");
            return;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabsFolder });
        if (prefabGuids == null || prefabGuids.Length == 0)
        {
            Debug.LogWarning($"No prefab assets found under '{prefabsFolder}'.");
            return;
        }

        int prefabCount = 0;
        int totalSlotsChanged = 0;

        foreach (var guid in prefabGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefabRoot == null)
                continue;

            int changedForThis = ReplaceMaterialsByNameOnPrefab(prefabRoot, materialsFolder);
            if (changedForThis > 0)
            {
                PrefabUtility.SavePrefabAsset(prefabRoot);
                prefabCount++;
                totalSlotsChanged += changedForThis;
                Debug.Log($"[AutoMaterials] Updated prefab: {assetPath} (material slots changed: {changedForThis})");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[AutoMaterials] Updated {prefabCount} prefab(s), changed {totalSlotsChanged} material slot(s) under '{prefabsFolder}'.");
    }

    // ---------- Core material matching logic ----------

    /// Replace on a temporary instance (for prefab building)
    private static int ReplaceMaterialsByNameOnInstance(GameObject root, string materialsFolder)
    {
        return ReplaceMaterialsByName(root, materialsFolder);
    }

    /// Replace on an existing prefab asset (Step 3)
    private static int ReplaceMaterialsByNameOnPrefab(GameObject prefabRoot, string materialsFolder)
    {
        return ReplaceMaterialsByName(prefabRoot, materialsFolder);
    }

    /// Shared logic: replace any Renderer materials whose name matches a .mat asset.
    /// If materialsFolder is non-null, search there first; otherwise / additionally use global search.
    private static int ReplaceMaterialsByName(GameObject root, string materialsFolder)
    {
        int slotChanges = 0;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return 0;

        foreach (var renderer in renderers)
        {
            var mats = renderer.sharedMaterials;
            bool rendererModified = false;

            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null)
                    continue;

                string targetName = m.name; // e.g. "cash_1", "pistols_mp_1"
                Material replacement = null;

                // 1) Prefer matching .mat inside this pack's Materials/ folder
                if (!string.IsNullOrEmpty(materialsFolder) && AssetDatabase.IsValidFolder(materialsFolder))
                {
                    string[] localGuids = AssetDatabase.FindAssets(targetName + " t:Material", new[] { materialsFolder });
                    foreach (var guid in localGuids)
                    {
                        string candidatePath = AssetDatabase.GUIDToAssetPath(guid);
                        replacement = AssetDatabase.LoadAssetAtPath<Material>(candidatePath);
                        if (replacement != null)
                            break;
                    }
                }

                // 2) Fallback: global search by name
                if (replacement == null)
                {
                    string[] globalGuids = AssetDatabase.FindAssets(targetName + " t:Material");
                    foreach (var guid in globalGuids)
                    {
                        string candidatePath = AssetDatabase.GUIDToAssetPath(guid);
                        replacement = AssetDatabase.LoadAssetAtPath<Material>(candidatePath);
                        if (replacement != null)
                            break;
                    }
                }

                if (replacement != null && replacement != m)
                {
                    mats[i] = replacement;
                    rendererModified = true;
                    slotChanges++;
                }
            }

            if (rendererModified)
            {
                renderer.sharedMaterials = mats;
            }
        }

        return slotChanges;
    }
}
