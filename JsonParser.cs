using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using AddonManager.Forms;

namespace AddonManager
{
    public class ManifestInfo
    {
        public string? pack_folder { get; set; }
        public string? name { get; set; }
        public string? description { get; set; }
        public Guid? pack_id { get; set; }
        public int[]? version { get; set; }
        public Image? pack_icon { get; set; }
        public string? type { get; set; }
    }
    
    public class ResultLists
    {
        // Use thread-safe bags for initial collection from Parallel.ForEach
        public static ConcurrentBag<ManifestInfo> rpList { get; set; } = new ConcurrentBag<ManifestInfo>();
        public static ConcurrentBag<ManifestInfo> bpList { get; set; } = new ConcurrentBag<ManifestInfo>();
        
        public static List<ManifestInfo> currentlyActiveRpList { get; set; } = new List<ManifestInfo>();
        public static List<ManifestInfo> currentlyActiveBpList { get; set; } = new List<ManifestInfo>();
        
        public static List<ManifestInfo> activeRpList { get; set; } = new List<ManifestInfo>();
        public static List<ManifestInfo> activeBpList { get; set; } = new List<ManifestInfo>();
        
        public static List<ManifestInfo> inactiveRpList { get; set; } = new List<ManifestInfo>();
        public static List<ManifestInfo> inactiveBpList { get; set; } = new List<ManifestInfo>();

        public List<ManifestInfo> GetList(string listName)
        {
            return listName switch
            {
                "inactiveRpList" => inactiveRpList,
                "inactiveBpList" => inactiveBpList,
                "activeRpList" => activeRpList,
                "activeBpList" => activeBpList,
                _ => throw new ArgumentException("Invalid list name", nameof(listName)),
            };
        }
    }
    
    public static class ListExtensions
    {
        public static void Move<T>(this List<T> list, int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= list.Count || newIndex < 0 || newIndex >= list.Count)
                return; // Index out of bounds
                
            T item = list[oldIndex];
            list.RemoveAt(oldIndex);
            list.Insert(newIndex, item);
        }
    }
    
    public class JsonParser
    {
        public void ParseWorldJson()
        {
            string rpJsonFilePath = Path.Combine(DirectoryForm.worldLocation, "world_resource_packs.json");
            string bpJsonFilePath = Path.Combine(DirectoryForm.worldLocation, "world_behavior_packs.json");

            EnsureFileExists(rpJsonFilePath, "world_resource_packs.json");
            EnsureFileExists(bpJsonFilePath, "world_behavior_packs.json");

            string rpJsonContent = File.ReadAllText(rpJsonFilePath);
            string bpJsonContent = File.ReadAllText(bpJsonFilePath);

            ResultLists.currentlyActiveRpList = ParseSimpleManifestJson(rpJsonContent);
            ResultLists.currentlyActiveBpList = ParseSimpleManifestJson(bpJsonContent);

            Logger.Log("Active world pack configurations have been parsed.");
        }

        private void EnsureFileExists(string path, string fileName)
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "[]");
                Logger.Log($"Created '{fileName}' as it was not found.");
            }
        }

        private List<ManifestInfo> ParseSimpleManifestJson(string jsonContent)
        {
            var list = new List<ManifestInfo>();
            try
            {
                var manifestJson = JsonDocument.Parse(jsonContent);
                foreach (var entry in manifestJson.RootElement.EnumerateArray())
                {
                    if (entry.TryGetProperty("pack_id", out var idElement) && idElement.TryGetGuid(out var guid) &&
                        entry.TryGetProperty("version", out var versionElement))
                    {
                        var versionArray = versionElement.EnumerateArray().Select(e => e.GetInt32()).ToArray();
                        list.Add(new ManifestInfo { pack_id = guid, version = versionArray });
                    }
                }
            }
            catch(JsonException ex)
            {
                Logger.Log($"Failed to parse simple manifest JSON. Error: {ex.Message}", "ERROR");
            }
            return list;
        }

        public void ParsePackFolder(string path, ConcurrentBag<ManifestInfo> list)
        {
            if (!Directory.Exists(path))
            {
                Logger.Log($"Pack directory not found: {path}", "ERROR");
                return;
            }
            
            Parallel.ForEach(Directory.GetDirectories(path), (directory) =>
            {
                var manifestPath = Path.Combine(directory, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var manifestContent = File.ReadAllText(manifestPath);
                        var manifestJson = JsonDocument.Parse(manifestContent);
                        Image fallbackIcon = Properties.Resources.pack_icon_fallback;

                        if (manifestJson.RootElement.TryGetProperty("header", out var header))
                        {
                            var manifestInfo = new ManifestInfo
                            {
                                pack_folder = directory,
                                name = header.TryGetProperty("name", out var n) ? n.GetString() : "Unknown Name",
                                description = header.TryGetProperty("description", out var d) ? d.GetString() : "No description.",
                                pack_id = header.TryGetProperty("uuid", out var u) && u.TryGetGuid(out var guid) ? guid : Guid.Empty,
                                version = header.TryGetProperty("version", out var v) ? v.EnumerateArray().Select(e => e.GetInt32()).ToArray() : new int[0]
                            };

                            if (manifestJson.RootElement.TryGetProperty("modules", out var modules))
                            {
                                manifestInfo.type = modules.EnumerateArray()
                                    .Select(m => m.TryGetProperty("type", out var t) ? t.GetString() : null)
                                    .FirstOrDefault();
                            }
                            
                            try
                            {
                                using (Image temp = Image.FromFile(Path.Combine(directory, "pack_icon.png")))
                                {
                                    manifestInfo.pack_icon = new Bitmap(temp);
                                }
                            }
                            catch (FileNotFoundException)
                            {
                                manifestInfo.pack_icon = fallbackIcon;
                            }
                            catch (Exception)
                            {
                                manifestInfo.pack_icon = fallbackIcon;
                                Logger.Log($"Could not load pack icon for '{manifestInfo.name}'. Using fallback.");
                            }
                            
                            // No lock needed as we're adding to a ConcurrentBag
                            list.Add(manifestInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error parsing manifest in {directory}: {ex.Message}");
                        Logger.Log($"Invalid manifest file found in '{Path.GetFileName(directory)}'. It could not be parsed.", "WARN");
                    }
                }
            });
            
            // This part should be called AFTER both ParsePackFolder calls are complete.
            // It's better to move this logic out of ParsePackFolder.
        }

        // This method should be called after both resource and behavior pack folders have been parsed.
        public void FinalizePackLists()
        {
            var rpListFinal = ResultLists.rpList.ToList();
            var bpListFinal = ResultLists.bpList.ToList();
            
            StringCleaner(rpListFinal);
            StringCleaner(bpListFinal);

            GetInactivePacks(rpListFinal, bpListFinal);
            GetActivePacks(rpListFinal, bpListFinal);
        }

        private void StringCleaner(List<ManifestInfo> list)
        {
            if (SettingsForm.disableStringCleaner)
            {
                Logger.Log("Pack name cleaning has been disabled.");
                return;
            }

            foreach (var manifestInfo in list)
            {
                if (manifestInfo.name != null)
                    manifestInfo.name = Regex.Replace(manifestInfo.name, @"§.", string.Empty);
                if (manifestInfo.description != null)
                    manifestInfo.description = Regex.Replace(manifestInfo.description, @"§.", string.Empty);
            }
            Logger.Log("Removed Bedrock color code modifiers from pack names.");
        }

        private void GetInactivePacks(List<ManifestInfo> allRp, List<ManifestInfo> allBp)
        {
            var activeRpSet = ResultLists.currentlyActiveRpList.Select(p => p.pack_id).ToHashSet();
            var activeBpSet = ResultLists.currentlyActiveBpList.Select(p => p.pack_id).ToHashSet();

            ResultLists.inactiveRpList = allRp.Where(rp => !activeRpSet.Contains(rp.pack_id)).ToList();
            ResultLists.inactiveBpList = allBp.Where(bp => !activeBpSet.Contains(bp.pack_id)).ToList();
            Logger.Log("Inactive pack lists populated.");
        }

        private void GetActivePacks(List<ManifestInfo> allRp, List<ManifestInfo> allBp)
        {
            ResultLists.activeRpList = SortAndFilterActiveList(allRp, ResultLists.currentlyActiveRpList);
            ResultLists.activeBpList = SortAndFilterActiveList(allBp, ResultLists.currentlyActiveBpList);
            Logger.Log("Active pack lists populated and sorted.");
        }

        // Highly optimized sorting method
        private List<ManifestInfo> SortAndFilterActiveList(List<ManifestInfo> allPacks, List<ManifestInfo> activePackOrder)
        {
            if (activePackOrder == null || !activePackOrder.Any())
                return new List<ManifestInfo>();

            // Create a dictionary for O(1) lookup of pack order.
            var orderLookup = activePackOrder
                .Select((pack, index) => new { pack.pack_id, index })
                .ToDictionary(p => p.pack_id, p => p.index);
            
            return allPacks
                .Where(pack => pack.pack_id.HasValue && orderLookup.ContainsKey(pack.pack_id.Value))
                .OrderBy(pack => orderLookup[pack.pack_id.Value])
                .ToList();
        }
        
        public static void SaveToJson()
        {
            var rpListToSave = ResultLists.activeRpList.Select(p => new { p.pack_id, p.version }).ToList();
            var bpListToSave = ResultLists.activeBpList.Select(p => new { p.pack_id, p.version }).ToList();

            string rpJsonPath = Path.Combine(DirectoryForm.worldLocation, "world_resource_packs.json");
            string bpJsonPath = Path.Combine(DirectoryForm.worldLocation, "world_behavior_packs.json");
            
            var options = new JsonSerializerOptions { WriteIndented = true };

            try
            {
                File.WriteAllText(rpJsonPath, JsonSerializer.Serialize(rpListToSave, options));
                Logger.Log("world_resource_packs.json has been written to disk.");
                
                File.WriteAllText(bpJsonPath, JsonSerializer.Serialize(bpListToSave, options));
                Logger.Log("world_behavior_packs.json has been written to disk.");
            }
            catch (Exception ex)
            {
                // Rethrow the exception so the UI can catch it and display a message.
                throw new IOException($"Failed to write to JSON files. Please check file permissions. Details: {ex.Message}", ex);
            }
        }
    }
}