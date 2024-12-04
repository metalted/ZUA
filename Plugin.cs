using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ZUA
{
    [BepInPlugin(pluginGUID, pluginName, pluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string pluginGUID = "com.metalted.zeepkist.zua";
        public const string pluginName = "ZUA";
        public const string pluginVersion = "1.0";
        public static Plugin Instance;

        public ConfigEntry<string> scriptPath;        

        private void Awake()
        {   
            Harmony harmony = new Harmony(pluginGUID);
            harmony.PatchAll();

            Instance = this;
            Zua.Initialize();

            // Plugin startup logic
            Logger.LogInfo($"Plugin {pluginName} is loaded!");
        }
    }
}
