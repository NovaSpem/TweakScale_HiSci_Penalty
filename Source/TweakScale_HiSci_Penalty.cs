using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace TweakScale_HiSci_Penalty
{
    public class TweakScaleHiSciPenaltyModule : PartModule, IPartCostModifier
    {
        [KSPField] public float penaltyMultiplier = 1.0f;
        
        public override void OnStart(StartState state)
        {
            if (TweakScaleHiSciPenaltySettings.DebugMode)
                Debug.Log(string.Format("[TweakScale_HiSci_Penalty] Module started on: {0}", part.partInfo.title));
        }
        
        public float GetModuleCost(float defaultCost, ModifierStagingSituation situation)
        {
            if (part == null || part.Modules == null) return 0f;
            
            foreach (PartModule pm in part.Modules)
            {
                if (pm != null && pm.ClassName == "TweakScale")
                {
                    var defaultScaleField = pm.GetType().GetField("defaultScale");
                    var currentScaleField = pm.GetType().GetField("currentScale");
                    var massExponentField = pm.GetType().GetField("MassExponent");
                    
                    if (defaultScaleField != null && currentScaleField != null)
                    {
                        float defaultScale = (float)defaultScaleField.GetValue(pm);
                        float currentScale = (float)currentScaleField.GetValue(pm);
                        
                        float massExponent = 3f;
                        if (massExponentField != null)
                            massExponent = (float)massExponentField.GetValue(pm);
                        
                        if (Math.Abs(currentScale - defaultScale) < 0.001f)
                            return 0f;
                        
                        float scaleRatio = currentScale / defaultScale;
                        float extraCost = 0f;
                        
                        if (scaleRatio < 1f)
                        {
                            float inverseScale = 1f / scaleRatio;
                            extraCost = defaultCost * (float)Math.Pow(inverseScale, massExponent) * penaltyMultiplier;
                        }
                        else
                        {
                            extraCost = defaultCost * (float)Math.Pow(scaleRatio, massExponent);
                        }
                        
                        if (TweakScaleHiSciPenaltySettings.DebugMode)
                            Debug.Log(string.Format("[TweakScale_HiSci_Penalty] Cost for {0}: scale={1:F2}, ratio={2:F2}, exponent={3}, extra={4:F0}", 
                                part.partInfo.title, currentScale, scaleRatio, massExponent, extraCost));
                        
                        return extraCost;
                    }
                    break;
                }
            }
            
            return 0f;
        }
        
        public ModifierChangeWhen GetModuleCostChangeWhen()
        {
            return ModifierChangeWhen.STAGED;
        }
    }

    public static class TweakScaleHiSciPenaltySettings
    {
        public static bool DebugMode = false;
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class TweakScaleHiSciPenaltyInjector : MonoBehaviour
    {
        private static List<string> whiteList = new List<string>();
        private static List<string> blackList = new List<string>();
        private static List<string> categoryList = new List<string>();
        private static List<string> moduleList = new List<string>();
        private static List<string> exclusionKeywords = new List<string>();
        private static float globalPenaltyMultiplier = 1.0f;
        
        private static List<string> debugWhitelisted = new List<string>();
        private static List<string> debugBlacklisted = new List<string>();
        private static List<string> debugCategory = new List<string>();
        private static List<string> debugModule = new List<string>();
        private static List<string> debugComplex = new List<string>();
        private static List<string> debugKeyword = new List<string>();
        private static List<string> debugExcluded = new List<string>();
        
        private static int skippedNoTweakScale = 0;
        
        void Awake()
        {
            DontDestroyOnLoad(this);
            StartCoroutine(InjectModules());
        }
        
        IEnumerator InjectModules()
        {
            yield return new WaitForSeconds(2f);
            
            LoadConfig();
            
            if (TweakScaleHiSciPenaltySettings.DebugMode)
                Debug.Log("[TweakScale_HiSci_Penalty] ===== Starting injection =====");
            
            int injected = 0;
            int totalParts = PartLoader.LoadedPartsList.Count;
            skippedNoTweakScale = 0;
            
            foreach (AvailablePart ap in PartLoader.LoadedPartsList)
            {
                if (ap == null || ap.partPrefab == null) continue;
                if (ap.partPrefab.Modules == null) continue;
                
                if (!HasTweakScaleModule(ap.partPrefab))
                {
                    skippedNoTweakScale++;
                    continue;
                }
                
                string partName = ap.name.ToLower();
                string partTitle = ap.title;
                
                if (IsInList(partName, blackList))
                {
                    debugBlacklisted.Add(string.Format("{0} ({1})", partTitle, ap.name));
                    continue;
                }
                
                if (IsInList(partName, whiteList))
                {
                    InjectModule(ap, "whitelist");
                    debugWhitelisted.Add(string.Format("{0} ({1})", partTitle, ap.name));
                    injected++;
                    continue;
                }
                
                if (IsHighTechCategory(ap.category))
                {
                    InjectModule(ap, "category");
                    debugCategory.Add(string.Format("{0} ({1})", partTitle, ap.name));
                    injected++;
                    continue;
                }
                
                string modName;
                if (HasHighTechModule(ap.partPrefab, out modName))
                {
                    InjectModule(ap, "module");
                    debugModule.Add(string.Format("{0} ({1}) [Module: {2}]", partTitle, ap.name, modName));
                    injected++;
                    continue;
                }
                
                if (IsExcluded(partName))
                {
                    debugKeyword.Add(string.Format("{0} ({1})", partTitle, ap.name));
                    continue;
                }
                
                string complexMod;
                if (IsComplexPart(ap.partPrefab, out complexMod))
                {
                    InjectModule(ap, "complex");
                    debugComplex.Add(string.Format("{0} ({1}) [Module: {2}]", partTitle, ap.name, complexMod));
                    injected++;
                    continue;
                }
                
                debugExcluded.Add(string.Format("{0} ({1})", partTitle, ap.name));
            }
            
            Debug.Log(string.Format("[TweakScale_HiSci_Penalty] Injected: {0} / Skipped no TweakScale: {1} / Total: {2}", 
                injected, skippedNoTweakScale, totalParts));
            
            if (TweakScaleHiSciPenaltySettings.DebugMode)
            {
                Debug.Log(string.Format("[TweakScale_HiSci_Penalty] ===== Injection complete: {0}/{1} parts =====\n", 
                    injected, totalParts));
                
                PrintDebugSection("WHITELISTED", debugWhitelisted);
                PrintDebugSection("BY CATEGORY", debugCategory);
                PrintDebugSection("BY MODULE", debugModule);
                PrintDebugSection("BY COMPLEX MODULE", debugComplex);
                PrintDebugSection("BLACKLISTED", debugBlacklisted);
                PrintDebugSection("BY KEYWORD", debugKeyword);
                PrintDebugSection("EXCLUDED", debugExcluded);
            }
            
            Debug.Log(string.Format("[TweakScale_HiSci_Penalty] Summary: WhiteList={0}, Category={1}, Module={2}, Complex={3}", 
                debugWhitelisted.Count, debugCategory.Count, debugModule.Count, debugComplex.Count));
        }
        
        private void PrintDebugSection(string title, List<string> items)
        {
            if (items.Count == 0) return;
            
            Debug.Log(string.Format("[TweakScale_HiSci_Penalty] --- {0} ({1}) ---", title, items.Count));
            foreach (string item in items)
            {
                Debug.Log(string.Format("[TweakScale_HiSci_Penalty]   + {0}", item));
            }
            Debug.Log("");
        }
        
        private bool HasTweakScaleModule(Part part)
        {
            if (part == null || part.Modules == null) return false;
            
            foreach (PartModule pm in part.Modules)
            {
                if (pm != null && pm.moduleName == "TweakScale")
                    return true;
            }
            return false;
        }
        
        private bool IsComplexPart(Part part, out string foundModule)
        {
            foundModule = "";
            if (part == null || part.Modules == null) return false;
            
            string partName = "";
            if (part.partInfo != null)
                partName = part.partInfo.name.ToLower();
            
            foreach (PartModule pm in part.Modules)
            {
                if (pm == null || pm.moduleName == null) continue;
                
                string moduleName = pm.moduleName.ToLower();
                
                if (moduleName.IndexOf("moduleenginesfx", StringComparison.Ordinal) >= 0 ||
                    moduleName.IndexOf("moduleenginesrf", StringComparison.Ordinal) >= 0)
                {
                    if (!string.IsNullOrEmpty(partName))
                    {
                        if (partName.IndexOf("rcs", StringComparison.Ordinal) >= 0) continue;
                        if (partName.IndexOf("sepmotor", StringComparison.Ordinal) >= 0) continue;
                        if (partName.IndexOf("launchescape", StringComparison.Ordinal) >= 0) continue;
                        if (partName.IndexOf("vernier", StringComparison.Ordinal) >= 0) continue;
                        if (partName.IndexOf("linear", StringComparison.Ordinal) >= 0) continue;
                    }
                    foundModule = pm.moduleName;
                    return true;
                }
                
                if (moduleName.IndexOf("thermalnozzle", StringComparison.Ordinal) >= 0 ||
                    moduleName.IndexOf("magneticnozzle", StringComparison.Ordinal) >= 0)
                { foundModule = pm.moduleName; return true; }
                
                if (moduleName.IndexOf("reactor", StringComparison.Ordinal) >= 0 &&
                    moduleName.IndexOf("generator", StringComparison.Ordinal) < 0)
                { foundModule = pm.moduleName; return true; }
                
                if (moduleName.IndexOf("science", StringComparison.Ordinal) >= 0 &&
                    moduleName.IndexOf("container", StringComparison.Ordinal) < 0)
                { foundModule = pm.moduleName; return true; }
                
                
                if (moduleName.IndexOf("antenna", StringComparison.Ordinal) >= 0 ||
                    moduleName.IndexOf("transmitter", StringComparison.Ordinal) >= 0)
                { foundModule = pm.moduleName; return true; }
                
                if (moduleName.IndexOf("converter", StringComparison.Ordinal) >= 0 ||
                    moduleName.IndexOf("harvester", StringComparison.Ordinal) >= 0 ||
                    moduleName.IndexOf("drill", StringComparison.Ordinal) >= 0)
                { foundModule = pm.moduleName; return true; }
                
                if (moduleName.IndexOf("thermal", StringComparison.Ordinal) >= 0 ||
                    moduleName.IndexOf("electricengine", StringComparison.Ordinal) >= 0 ||
                    moduleName.IndexOf("beamedpower", StringComparison.Ordinal) >= 0 ||
                    moduleName.IndexOf("warp", StringComparison.Ordinal) >= 0)
                { foundModule = pm.moduleName; return true; }
            }
            
            return false;
        }
        
        private bool IsHighTechCategory(PartCategories category)
        {
            foreach (string cat in categoryList)
            {
                if (category.ToString().ToLower() == cat.ToLower()) return true;
            }
            return false;
        }
        
        private bool HasHighTechModule(Part part, out string foundModule)
        {
            foundModule = "";
            if (part == null || part.Modules == null) return false;
            
            foreach (string mod in moduleList)
            {
                foreach (PartModule pm in part.Modules)
                {
                    if (pm == null || pm.moduleName == null) continue;
                    if (pm.moduleName.ToLower() == mod.ToLower())
                    {
                        foundModule = pm.moduleName;
                        return true;
                    }
                }
            }
            return false;
        }
        
        private bool IsExcluded(string partName)
        {
            foreach (string keyword in GetExclusionKeywords())
            {
                if (partName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }
        
        private List<string> GetExclusionKeywords()
        {
            if (exclusionKeywords.Count > 0)
                return exclusionKeywords;
            
            return new List<string>
            {
                "tank", "fuel", "fuselage", "mono", "adapter", "bicoupler",
                "structural", "truss", "girder", "plate", "panel",
                "landing", "leg", "wheel", "gear", "parachute", "chute",
                "nosecone", "nose", "tail", "fin", "wing",
                "decoupler", "separator", "decouple", "launchclamp",
                "stackpoint", "docking", "apu"
            };
        }
        
        private bool IsInList(string partName, List<string> list)
        {
            foreach (string item in list)
            {
                if (partName == item.ToLower()) return true;
            }
            return false;
        }
        
        private void InjectModule(AvailablePart ap, string reason)
        {
            if (ap == null || ap.partPrefab == null || ap.partPrefab.Modules == null) return;
            if (ap.partPrefab.Modules.Contains("TweakScaleHiSciPenaltyModule")) return;
            
            try
            {
                ConfigNode node = new ConfigNode("MODULE");
                node.AddValue("class", "TweakScale_HiSci_Penalty.TweakScaleHiSciPenaltyModule");
                node.AddValue("name", "TweakScaleHiSciPenaltyModule");
                node.AddValue("penaltyMultiplier", globalPenaltyMultiplier.ToString());
                
                ap.partPrefab.AddModule(node, true);
                
                if (TweakScaleHiSciPenaltySettings.DebugMode)
                    Debug.Log(string.Format("[TweakScale_HiSci_Penalty] INJECTED: {0} ({1}) - Reason: {2}", 
                        ap.title, ap.name, reason));
            }
            catch (Exception ex)
            {
                Debug.LogError(string.Format("[TweakScale_HiSci_Penalty] FAILED: {0} ({1}): {2}", 
                    ap.title, ap.name, ex.Message));
            }
        }
        
        private void LoadConfig()
        {
            var cfgNodes = GameDatabase.Instance.GetConfigNodes("TWEAKSCALE_HISCI_PENALTY");
            if (cfgNodes.Length == 0)
            {
                Debug.Log("[TweakScale_HiSci_Penalty] Config not found, using defaults");
                return;
            }
            
            ConfigNode cfg = cfgNodes[0];
            
            if (cfg.HasValue("debugMode"))
            {
                bool debug = false;
                bool.TryParse(cfg.GetValue("debugMode"), out debug);
                TweakScaleHiSciPenaltySettings.DebugMode = debug;
            }
            
            if (cfg.HasValue("penaltyMultiplier"))
            {
                float multiplier = 1.0f;
                float.TryParse(cfg.GetValue("penaltyMultiplier"), out multiplier);
                globalPenaltyMultiplier = multiplier;
            }
            
            List<string> loadedKeywords = LoadListFromConfig(cfg, "ExclusionKeywords", "keyword");
            if (loadedKeywords.Count > 0)
                exclusionKeywords = loadedKeywords;
            
            whiteList = LoadListFromConfig(cfg, "WhiteList", "part");
            blackList = LoadListFromConfig(cfg, "BlackList", "part");
            categoryList = LoadListFromConfig(cfg, "CategoryList", "category");
            moduleList = LoadListFromConfig(cfg, "ModuleList", "module");
            
            if (TweakScaleHiSciPenaltySettings.DebugMode)
            {
                Debug.Log(string.Format("[TweakScale_HiSci_Penalty] Config loaded:"));
                Debug.Log(string.Format("[TweakScale_HiSci_Penalty]   debugMode: {0}", TweakScaleHiSciPenaltySettings.DebugMode));
                Debug.Log(string.Format("[TweakScale_HiSci_Penalty]   penaltyMultiplier: {0}", globalPenaltyMultiplier));
                Debug.Log(string.Format("[TweakScale_HiSci_Penalty]   ExclusionKeywords: {0}", string.Join(", ", exclusionKeywords.ToArray())));
                Debug.Log(string.Format("[TweakScale_HiSci_Penalty]   WhiteList: {0}", string.Join(", ", whiteList.ToArray())));
                Debug.Log(string.Format("[TweakScale_HiSci_Penalty]   BlackList: {0}", string.Join(", ", blackList.ToArray())));
                Debug.Log(string.Format("[TweakScale_HiSci_Penalty]   CategoryList: {0}", string.Join(", ", categoryList.ToArray())));
                Debug.Log(string.Format("[TweakScale_HiSci_Penalty]   ModuleList: {0}", string.Join(", ", moduleList.ToArray())));
            }
        }
        
        private List<string> LoadListFromConfig(ConfigNode cfg, string nodeName, string valueName)
        {
            List<string> result = new List<string>();
            ConfigNode listNode = cfg.GetNode(nodeName);
            if (listNode != null)
            {
                foreach (ConfigNode.Value value in listNode.values)
                {
                    if (value.name == valueName)
                        result.Add(value.value.ToLower());
                }
            }
            return result;
        }
    }
}