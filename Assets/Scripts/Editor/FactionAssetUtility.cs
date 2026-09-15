using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class FactionAssetUtility
{
    public const string DataRoot = "Assets/Data/Factions";
    public const string PrefabRoot = "Assets/Prefabs";
    public static string DataFolder(string name) => DataRoot + "/" + name;
    public static string PrefabFolder(string name) => PrefabRoot + "/" + name;
    private static string PathOf(Object asset) => AssetDatabase.GetAssetPath(asset);
    private static string Parent(string path) => Path.GetDirectoryName(path).Replace('\\', '/');

    public static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.EndsWith(".") ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('/') || name.Contains('\\') ||
            name == "." || name == "..")
            throw new InvalidOperationException("Use a non-empty name without path characters, leading/trailing spaces, or a trailing dot.");
        string stem = name.Split('.')[0].ToUpperInvariant();
        if (new[] { "CON", "PRN", "AUX", "NUL" }.Contains(stem) ||
            (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] >= '1' && stem[3] <= '9'))
            throw new InvalidOperationException("That name is reserved by Windows.");
    }

    private static FactionUnit[] Entries(Faction faction) =>
        (faction.availableUnits ?? Array.Empty<FactionUnit>()).Concat(faction.units ?? Array.Empty<FactionUnit>())
            .Append(faction.startingUnit).Where(e => e != null).Distinct().ToArray();

    private static void Free(string path, string current = null)
    {
        if (current != null && string.Equals(path, current, StringComparison.OrdinalIgnoreCase)) return;
        if (File.Exists(path) || Directory.Exists(path) || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
            throw new InvalidOperationException("An asset or folder already exists at " + path);
    }

    private static void NewName(string name, Faction except = null)
    {
        ValidateName(name);
        foreach (string guid in AssetDatabase.FindAssets("t:Faction"))
        {
            var other = AssetDatabase.LoadAssetAtPath<Faction>(AssetDatabase.GUIDToAssetPath(guid));
            if (other != except && string.Equals(other.name, name, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A faction named " + name + " already exists.");
        }
    }

    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        Folder(Parent(path));
        if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(Parent(path), Path.GetFileName(path))))
            throw new InvalidOperationException("Could not create " + path);
    }

    private static void Prefab(GameObject prefab)
    {
        if (prefab == null || !PathOf(prefab).EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Assign saved prefabs for the city and units before duplicating.");
    }

    private static void ValidateEntries(Faction faction)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "City" };
        foreach (var entry in Entries(faction))
        {
            if (entry.unitData == null) throw new InvalidOperationException(entry.name + " has no UnitData.");
            ValidateName(entry.unitData.name);
            if (!names.Add(entry.unitData.name))
                throw new InvalidOperationException("Unit names must be unique and cannot be City: " + entry.unitData.name);
            Prefab(entry.prefab);
            if (entry.prefab.GetComponent<Unit>() == null)
                throw new InvalidOperationException(entry.name + " needs a Unit component on its prefab root.");
        }
        if (faction.cityPrefab != null) Prefab(faction.cityPrefab);
    }

    private static GameObject CopyPrefab(GameObject source, string path)
    {
        Free(path);
        if (!AssetDatabase.CopyAsset(PathOf(source), path))
            throw new InvalidOperationException("Could not copy prefab to " + path);
        NamePrefab(path, Path.GetFileNameWithoutExtension(path));
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static void NamePrefab(string path, string name)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = name;
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            if (!success) throw new InvalidOperationException("Could not save " + path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static Faction Create(string name, Faction source = null)
    {
        NewName(name);
        Free(DataFolder(name));
        Free(PrefabFolder(name));
        if (source != null) ValidateEntries(source);
        Folder(DataFolder(name));
        Folder(PrefabFolder(name));
        try
        {
            var faction = ScriptableObject.CreateInstance<Faction>();
            if (source != null) EditorUtility.CopySerialized(source, faction);
            faction.name = name;
            AssetDatabase.CreateAsset(faction, DataFolder(name) + "/" + name + ".asset");
            faction.aiProfile = source != null && source.aiProfile != null
                ? UnityEngine.Object.Instantiate(source.aiProfile) : ScriptableObject.CreateInstance<AIProfile>();
            faction.aiProfile.name = name + " AI Profile";
            AssetDatabase.CreateAsset(faction.aiProfile, DataFolder(name) + "/" + faction.aiProfile.name + ".asset");
            if (source != null)
            {
                var map = new Dictionary<FactionUnit, FactionUnit>();
                foreach (var entry in Entries(source))
                    map[entry] = CreateEntry(faction, entry.unitData, entry.prefab);
                faction.availableUnits = (source.availableUnits ?? Array.Empty<FactionUnit>()).Select(e => e != null ? map[e] : null).ToArray();
                faction.units = (source.units ?? Array.Empty<FactionUnit>()).Select(e => e != null ? map[e] : null).ToArray();
                faction.startingUnit = source.startingUnit != null ? map[source.startingUnit] : null;
                faction.cityPrefab = source.cityPrefab != null
                    ? CopyPrefab(source.cityPrefab, PrefabFolder(name) + "/" + name + " City.prefab") : null;
            }
            EditorUtility.SetDirty(faction);
            AssetDatabase.SaveAssetIfDirty(faction);
            return faction;
        }
        catch
        {
            // These two paths were absent before this operation.
            AssetDatabase.DeleteAsset(DataFolder(name));
            AssetDatabase.DeleteAsset(PrefabFolder(name));
            throw;
        }
    }

    public static FactionUnit AddUnit(Faction faction, UnitData data, GameObject prefab)
    {
        if (faction == null || string.IsNullOrEmpty(PathOf(faction)) || data == null)
            throw new InvalidOperationException("Choose a saved faction and unit data.");
        if (Entries(faction).Any(e => e.unitData != null && string.Equals(e.unitData.name, data.name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("This faction already has a unit named " + data.name);
        var entry = CreateEntry(faction, data, prefab);
        faction.availableUnits = (faction.availableUnits ?? Array.Empty<FactionUnit>()).Append(entry).ToArray();
        if (faction.startingUnit == null) faction.startingUnit = entry;
        EditorUtility.SetDirty(faction);
        AssetDatabase.SaveAssetIfDirty(faction);
        return entry;
    }

    public static void SetCity(Faction faction, GameObject template)
    {
        Prefab(template);
        string path = PrefabFolder(faction.name) + "/" + faction.name + " City.prefab";
        if (PathOf(template) != path)
        {
            Free(path);
            Folder(PrefabFolder(faction.name));
            template = CopyPrefab(template, path);
        }
        faction.cityPrefab = template;
        EditorUtility.SetDirty(faction);
        AssetDatabase.SaveAssetIfDirty(faction);
    }

    private static FactionUnit CreateEntry(Faction faction, UnitData data, GameObject prefab)
    {
        ValidateName(data.name);
        if (string.Equals(data.name, "City", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("City is reserved for the city prefab.");
        Prefab(prefab);
        if (prefab.GetComponent<Unit>() == null) throw new InvalidOperationException("The prefab needs a Unit component on its root.");
        string name = faction.name + " " + data.name;
        string dataPath = DataFolder(faction.name) + "/" + name + ".asset";
        string prefabPath = PrefabFolder(faction.name) + "/" + name + ".prefab";
        Free(dataPath);
        Free(prefabPath);
        Folder(DataFolder(faction.name));
        Folder(PrefabFolder(faction.name));
        var copy = CopyPrefab(prefab, prefabPath);
        var entry = ScriptableObject.CreateInstance<FactionUnit>();
        entry.faction = faction;
        entry.unitData = data;
        entry.prefab = copy;
        AssetDatabase.CreateAsset(entry, dataPath);
        return entry;
    }

    public static void Rename(Faction faction, string name, bool renameRelated)
    {
        if (faction == null) throw new InvalidOperationException("Choose a faction.");
        NewName(name, faction);
        ValidateEntries(faction);
        var entries = Entries(faction);
        var prefabs = entries.Select(e => e.prefab).Append(faction.cityPrefab).Where(p => p != null).ToArray();
        if (prefabs.Distinct().Count() != prefabs.Length)
            throw new InvalidOperationException("Multiple units share one prefab. Duplicate this faction first to give each unit its own prefab.");
        // Never rename assets used by another faction.
        foreach (string guid in AssetDatabase.FindAssets("t:Faction"))
        {
            var other = AssetDatabase.LoadAssetAtPath<Faction>(AssetDatabase.GUIDToAssetPath(guid));
            if (other == faction) continue;
            if (Entries(other).Any(e => entries.Contains(e) || prefabs.Contains(e.prefab)) ||
                (other.cityPrefab != null && prefabs.Contains(other.cityPrefab)))
                throw new InvalidOperationException("Assets are shared with " + other.name + ". Duplicate this faction first.");
        }
        if (entries.Any(e => e.faction != faction))
            throw new InvalidOperationException("A roster entry belongs to another faction.");

        string oldData = Parent(PathOf(faction));
        string oldPrefab = prefabs.Length > 0 ? Parent(PathOf(prefabs[0])) : PrefabFolder(faction.name);
        bool moveDataFolder = Parent(oldData) == DataRoot;
        bool movePrefabFolder = Parent(oldPrefab) == PrefabRoot && AssetDatabase.IsValidFolder(oldPrefab) &&
            prefabs.All(p => Parent(PathOf(p)) == oldPrefab);
        foreach (string guid in AssetDatabase.FindAssets("t:FactionUnit"))
        {
            var other = AssetDatabase.LoadAssetAtPath<FactionUnit>(AssetDatabase.GUIDToAssetPath(guid));
            if (entries.Contains(other)) continue;
            if ((moveDataFolder && PathOf(other).StartsWith(oldData + "/", StringComparison.OrdinalIgnoreCase) && other.faction != faction) ||
                (other.prefab != null && (prefabs.Contains(other.prefab) ||
                (movePrefabFolder && other.faction != faction && PathOf(other.prefab).StartsWith(oldPrefab + "/", StringComparison.OrdinalIgnoreCase)))))
                throw new InvalidOperationException("The assets or folders are also used by " + other.name);
        }
        string newData = DataFolder(name), newPrefab = PrefabFolder(name);
        Free(newData, moveDataFolder ? oldData : null);
        Free(newPrefab, movePrefabFolder ? oldPrefab : null);

        var assets = new List<(Object asset, string path, string newName)> { (faction, newData + "/" + name + ".asset", name) };
        foreach (var entry in entries)
        {
            string unitName = name + " " + entry.unitData.name;
            assets.Add((entry, newData + "/" + (renameRelated ? unitName : Path.GetFileNameWithoutExtension(PathOf(entry))) + ".asset", renameRelated ? unitName : entry.name));
            assets.Add((entry.prefab, newPrefab + "/" + (renameRelated ? unitName : Path.GetFileNameWithoutExtension(PathOf(entry.prefab))) + ".prefab", renameRelated ? unitName : entry.prefab.name));
        }
        if (faction.cityPrefab != null)
            assets.Add((faction.cityPrefab, newPrefab + "/" + (renameRelated ? name + " City" : Path.GetFileNameWithoutExtension(PathOf(faction.cityPrefab))) + ".prefab", renameRelated ? name + " City" : faction.cityPrefab.name));

        // Check destinations against the current folder contents before any moves.
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in assets)
        {
            if (!targets.Add(item.path)) throw new InvalidOperationException("Conflicting destination: " + item.path);
            string checkPath = item.path.StartsWith(newData + "/") && moveDataFolder ? oldData + item.path.Substring(newData.Length)
                : item.path.StartsWith(newPrefab + "/") && movePrefabFolder ? oldPrefab + item.path.Substring(newPrefab.Length) : item.path;
            Free(checkPath, PathOf(item.asset));
        }
        // A folder move must not sweep another faction's data along with it.
        foreach (string guid in AssetDatabase.FindAssets("t:Faction"))
        {
            var other = AssetDatabase.LoadAssetAtPath<Faction>(AssetDatabase.GUIDToAssetPath(guid));
            if (other == faction) continue;
            if (moveDataFolder && PathOf(other).StartsWith(oldData + "/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The data folder also contains " + other.name);
        }
        var moves = new List<(string from, string to)>();
        var names = assets.Select(a => a.asset.name).ToArray();
        bool madeData = false, madePrefab = false;
        try
        {
            if (moveDataFolder) Move(oldData, newData, moves);
            else { Folder(newData); madeData = true; }
            if (movePrefabFolder) Move(oldPrefab, newPrefab, moves);
            else { Folder(newPrefab); madePrefab = true; }
            foreach (var item in assets)
            {
                Move(PathOf(item.asset), item.path, moves);
                if (item.asset is GameObject) NamePrefab(item.path, item.newName);
                else { item.asset.name = item.newName; EditorUtility.SetDirty(item.asset); AssetDatabase.SaveAssetIfDirty(item.asset); }
            }
        }
        catch
        {
            for (int i = moves.Count - 1; i >= 0; i--) AssetDatabase.MoveAsset(moves[i].to, moves[i].from);
            for (int i = 0; i < assets.Count; i++)
            {
                if (assets[i].asset is GameObject) NamePrefab(PathOf(assets[i].asset), names[i]);
                else { assets[i].asset.name = names[i]; EditorUtility.SetDirty(assets[i].asset); AssetDatabase.SaveAssetIfDirty(assets[i].asset); }
            }
            if (madeData) AssetDatabase.DeleteAsset(newData);
            if (madePrefab) AssetDatabase.DeleteAsset(newPrefab);
            throw;
        }
    }

    private static void Move(string from, string to, List<(string from, string to)> moves)
    {
        if (from == to) return;
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            string temp = Parent(from) + "/__FactionRename_" + Guid.NewGuid().ToString("N") + Path.GetExtension(from);
            Move(from, temp, moves);
            Move(temp, to, moves);
            return;
        }
        string error = AssetDatabase.MoveAsset(from, to);
        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        moves.Add((from, to));
    }
}
