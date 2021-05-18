using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using System.Text.RegularExpressions;
using System.Linq;

namespace UnityEditor
{
    class AssetBundleDataBase
    {
        class AssetEntry
        {
            string path;
            HashSet<AssetEntry> references;
            HashSet<AssetEntry> referencedBy;
        }

        static Dictionary<string, HashSet<string>> raw_deps_ = new Dictionary<string, HashSet<string>>();
        // static readonly string kStaticAssetPattern = @"^Assets/((Resources/)|(Editor/).*)|((\.cs$)|(\.xxx$))";

        // TODO: Cache
        static string staticAssetPattern
        {
            get
            {
                string pattern = @"^Assets/";
                if (staticAssetsFolder.Count > 0)
                {
                    pattern += "(";
                    int folderIndex = 0;
                    foreach (var folder in staticAssetsFolder)
                    {
                        pattern += $"({folder}/)";
                        folderIndex++;
                        if (folderIndex < staticAssetsFolder.Count)
                        {
                            pattern += "|";
                        }
                    }
                    pattern += ".*)";
                }

                if (staticAssetsSuffix.Count > 0)
                {
                    pattern += "|(";
                    int suffixIndex = 0;
                    foreach (var suffix in staticAssetsSuffix)
                    {
                        pattern += @"(\." + suffix + @"$)";
                        suffixIndex++;
                        if (suffixIndex < staticAssetsSuffix.Count)
                        {
                            pattern += "|";
                        }
                    }
                    pattern += ")";
                }

                return pattern;
            }
        }

        static HashSet<string> staticAssetsFolder = new HashSet<string>();
        static HashSet<string> staticAssetsSuffix = new HashSet<string>();
        static readonly string kBundleNamePatternOfPackTogether = @"(?<groupName>.*)_assets_all";

        // Warning: aa group cannot has 'assets' in name
        static readonly string kBundleNamePatternOfPackSeparately = @"(?<groupName>.*)_assets_(?<bundleName>.*)";

        static Dictionary<string, string[]> dynamicDeps = new Dictionary<string, string[]>();

        /// bundleName: no suffix
        public static string[] GetDynamicDependences(string bundleName)
        {
            if (dynamicDeps.ContainsKey(bundleName))
            {
                return dynamicDeps[bundleName];
            }
            else
            {
                // SetGroupDependencesCache()
                var groupNameMatch = MatchBundleName(bundleName);
                if (groupNameMatch.Success)
                {
                    var groupName = groupNameMatch.Groups["groupName"].Value;
                    BuildGroup(groupName);
                    if (dynamicDeps.ContainsKey(bundleName))
                    {
                        return dynamicDeps[bundleName];
                    }
                }

                return null;
            }
        }

        public static void BuildAllGroupsDenpendences(AddressableAssetSettings aaSettings)
        {
            if (null == aaSettings)
            {
                UnityEngine.Debug.LogError("no aa setttings");
                return;
            }
            foreach (var group in aaSettings.groups)
            {
                BuildGroup(group);
            }
        }

        public static bool IsDynamicAsset(string path)
        {
            return !Regex.Match(path, staticAssetPattern).Success;
        }

        /// exclude '/'
        public static void AddStaticAssetsFolder(string[] paths)
        {
            foreach (var path in paths)
            {
                staticAssetsFolder.Add(path);
            }
        }

        /// exclude '.'
        public static void AddStaticAssetsSuffix(string[] suffixes)
        {
            foreach (var suffix in suffixes)
            {
                staticAssetsSuffix.Add(suffix);
            }
        }

        private static Match MatchBundleName(string bundleName)
        {
            var match1 = Regex.Match(bundleName, kBundleNamePatternOfPackTogether);
            if (match1.Success)
            {
                return match1;
            }

            return Regex.Match(bundleName, kBundleNamePatternOfPackSeparately);
        }

        private static void UpdateDynamicDependences(string bundleName)
        {
            if (raw_deps_.TryGetValue(bundleName, out var deps))
            {
                var dyDeps = new List<string>();
                foreach (var dep in deps)
                {
                    if (IsDynamicAsset(dep))
                    {
                        dyDeps.Add(dep);
                    }
                }
                var array = dyDeps.ToArray();
                Array.Sort(array);
                dynamicDeps[bundleName] = array;
            }
        }

        private static void BuildGroup(AddressableAssetGroup group)
        {
            foreach (var schema in group.Schemas)
            {
                if (schema is BundledAssetGroupSchema bas)
                {
                    if (bas.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackTogether)
                    {
                        // pack together
                        var allPaths = new List<string>();
                        foreach (var entry in group.entries)
                        {
                            allPaths.Add(entry.AssetPath);
                        }
                        var bundleName = $"{group.Name.ToLower()}_assets_all";
                        var deps = new HashSet<string>(AssetDatabase.GetDependencies(allPaths.ToArray()));
                        raw_deps_[bundleName] = deps;
                        UpdateDynamicDependences(bundleName);
                    }
                    else if (bas.BundleMode == BundledAssetGroupSchema.BundlePackingMode.PackSeparately)
                    {
                        // pack separately
                        foreach (var entry in group.entries)
                        {
                            var bundleName = $"{group.Name.ToLower()}_assets_{entry.address.ToLower()}";
                            var deps = new HashSet<string>(AssetDatabase.GetDependencies(new string[] { entry.AssetPath }));
                            raw_deps_[bundleName] = deps;
                            UpdateDynamicDependences(bundleName);
                        }
                    }
                }
            }
        }

        private static void BuildGroup(string groupName)
        {
            var aaSettings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (null == aaSettings)
            {
                UnityEngine.Debug.LogError("no aa setttings");
                return;
            }
            var group = aaSettings.FindGroup(groupName);
            if (group != null)
            {
                BuildGroup(group);
            }
        }
    }
}
