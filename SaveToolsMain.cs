using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using BepInEx;
using BepInEx.Configuration;
using PolyTechFramework;
using System.Linq;
using System.Threading;
using PolyPhysics;
using System.IO;
using Sirenix.Serialization;

namespace SaveToolsMod
{
    [BepInPlugin(pluginGuid, pluginName, pluginVerson)]
    [BepInProcess("Poly Bridge 2")]
    [BepInDependency(PolyTechMain.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    public class SaveToolsMain : PolyTechMod
    {

        public const string pluginGuid = "polytech.bridgesavetools";

        public const string pluginName = "Bridge Save Tools Mod";

        public const string pluginVerson = "1.0.0";

        public ConfigDefinition modEnableDef = new ConfigDefinition(pluginName, "Enable/Disable Mod");
        public ConfigDefinition SaveBridgeDef = new ConfigDefinition(pluginName, "Save Bridge");
        public ConfigDefinition LoadBridgeDef = new ConfigDefinition(pluginName, "Load Bridge");
        public ConfigDefinition SaveNameDef = new ConfigDefinition(pluginName, "Last Save Name");

        public ConfigEntry<bool> mEnabled;

        public ConfigEntry<KeyboardShortcut> mSaveBridge;
        public ConfigEntry<KeyboardShortcut> mLoadBridge;

        public ConfigEntry<string> mSaveName;

        public static SaveToolsMain instance;

        public string MainPath = "";


        void Awake()
        {
            if (instance == null) instance = this;
            //repositoryUrl = "";
            authors = new string[] { "Bram2323" };

            MainPath = Application.dataPath.Replace("Poly Bridge 2_Data", "BepInEx/plugins/BridgeSaveToolsMod/");

            int order = 0;

            mEnabled = Config.Bind(modEnableDef, true, new ConfigDescription("Controls if the mod should be enabled or disabled", null, new ConfigurationManagerAttributes { Order = order }));
            mEnabled.SettingChanged += onEnableDisable;
            order--;

            mSaveBridge = Config.Bind(SaveBridgeDef, new KeyboardShortcut(KeyCode.None), new ConfigDescription("The button that will save a bridge", null, new ConfigurationManagerAttributes { Order = order }));
            order--;

            mLoadBridge = Config.Bind(LoadBridgeDef, new KeyboardShortcut(KeyCode.None), new ConfigDescription("The button that will load a bridge", null, new ConfigurationManagerAttributes { Order = order }));
            order--;

            mSaveName = Config.Bind(SaveNameDef, "", new ConfigDescription("The name of the last save that was created", null, new ConfigurationManagerAttributes { Order = order }));
            order--;

            Config.SettingChanged += onSettingChanged;


            try
            {
                if (!Directory.Exists(MainPath))
                {
                    Directory.CreateDirectory(MainPath);
                    Debug.Log("BridgeSaveToolsMod folder Created!");
                }

                if (!Directory.Exists(MainPath + "SaveSlots"))
                {
                    Directory.CreateDirectory(MainPath + "SaveSlots");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Something went wrong while creating bridge save tools mod folders!\n" + e);
            }


            Harmony harmony = new Harmony(pluginGuid);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            isCheat = false;
            isEnabled = mEnabled.Value;

            PolyTechMain.registerMod(this);
        }


        public void onEnableDisable(object sender, EventArgs e)
        {
            isEnabled = mEnabled.Value;
        }

        public void onSettingChanged(object sender, EventArgs e)
        {

        }

        public override void enableMod()
        {
            mEnabled.Value = true;
            onEnableDisable(null, null);
        }

        public override void disableMod()
        {
            mEnabled.Value = false;
            onEnableDisable(null, null);
        }

        public override string getSettings()
        {
            return "";
        }

        public override void setSettings(string st)
        {

        }

        public bool CheckForCheating()
        {
            return mEnabled.Value && PolyTechMain.modEnabled.Value;
        }


        void Update()
        {
            if (!instance.CheckForCheating()) return;

            if (mSaveBridge.Value.IsDown())
            {
                if (GameStateManager.GetState() == GameState.BUILD)
                {
                    PopupInputField.Display("Save Bridge", mSaveName.Value, SaveBridge);
                }
                else PopUpWarning.Display("Can only save bridge while in build mode!");
            }

            if (mLoadBridge.Value.IsDown())
            {
                if (GameStateManager.GetState() == GameState.BUILD)
                {
                    PopupInputField.Display("Load Bridge", mSaveName.Value, LoadBridge);
                }
                else PopUpWarning.Display("Can only load bridge while in build mode!");
            }
        }


        public void SaveBridge(string name)
        {
            try
            {
                if (GameStateManager.GetState() == GameState.BUILD)
                {
                    BridgeSaveSlotData slotData = new BridgeSaveSlotData();
                    slotData.m_Version = BridgeSaveSlots.CURRENT_VERSION;
                    slotData.m_PhysicsVersion = GameManager.GetPhysicsEngineVersion();
                    slotData.m_DisplayName = name;
                    slotData.m_SlotID = 2323;
                    slotData.m_SlotFilename = BridgeSaveSlots.AddFileExtension(name);
                    slotData.m_Bridge = BridgeSave.SerializeBinary();
                    slotData.m_Budget = Mathf.RoundToInt(Budget.m_BridgeCost);
                    slotData.m_UsingUnlimitedMaterials = Budget.m_UsingForcedUnlimitedMaterial;
                    slotData.m_UsingUnlimitedBudget = Budget.m_UsingForcedUnlimitedBudget;

                    byte[] data = SerializationUtility.SerializeValue<BridgeSaveSlotData>(slotData, DataFormat.Binary, null);
                    File.WriteAllBytes(MainPath + "SaveSlots/" + name + ".slot", data);
                    mSaveName.Value = name;
                    GameUI.ShowMessage(ScreenMessageLocation.TOP_CENTER, "Bridge Saved!", 3);
                }
                else PopUpWarning.Display("Can only save bridge while in build mode!");
            }
            catch (Exception e)
            {
                Debug.LogWarning("Something went wrong while trying to save a bridge!\n" + e);
                PopUpWarning.Display("Something went wrong while trying to save the bridge!");
            }
        }

        public void LoadBridge(string name)
        {
            try
            {
                if (GameStateManager.GetState() == GameState.BUILD)
                {
                    string path = MainPath + "SaveSlots/" + name + ".slot";
                    if (File.Exists(path))
                    {
                        byte[] data = File.ReadAllBytes(path);
                        BridgeSaveSlotData slotData = SerializationUtility.DeserializeValue<BridgeSaveSlotData>(data, DataFormat.Binary, null);
                        Bridge.ClearAndLoadBinary(slotData.m_Bridge);
                        Budget.MaybeApplyForcedBudgets(slotData.m_UsingUnlimitedBudget, slotData.m_UsingUnlimitedMaterials);
                        mSaveName.Value = name;
                        GameUI.ShowMessage(ScreenMessageLocation.TOP_CENTER, "Bridge Loaded!", 3);
                    }
                    else PopUpWarning.Display("Could not find bridge!");
                }
                else PopUpWarning.Display("Can only load bridge while in build mode!");
            }
            catch (Exception e)
            {
                Debug.LogWarning("Something went wrong while trying to load a bridge!\n" + e);
                PopUpWarning.Display("Something went wrong while trying to load the bridge!");
            }
        }
    }




    /// <summary>
    /// Class that specifies how a setting should be displayed inside the ConfigurationManager settings window.
    /// 
    /// Usage:
    /// This class template has to be copied inside the plugin's project and referenced by its code directly.
    /// make a new instance, assign any fields that you want to override, and pass it as a tag for your setting.
    /// 
    /// If a field is null (default), it will be ignored and won't change how the setting is displayed.
    /// If a field is non-null (you assigned a value to it), it will override default behavior.
    /// </summary>
    /// 
    /// <example> 
    /// Here's an example of overriding order of settings and marking one of the settings as advanced:
    /// <code>
    /// // Override IsAdvanced and Order
    /// Config.AddSetting("X", "1", 1, new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = true, Order = 3 }));
    /// // Override only Order, IsAdvanced stays as the default value assigned by ConfigManager
    /// Config.AddSetting("X", "2", 2, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 1 }));
    /// Config.AddSetting("X", "3", 3, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 2 }));
    /// </code>
    /// </example>
    /// 
    /// <remarks> 
    /// You can read more and see examples in the readme at https://github.com/BepInEx/BepInEx.ConfigurationManager
    /// You can optionally remove fields that you won't use from this class, it's the same as leaving them null.
    /// </remarks>
#pragma warning disable 0169, 0414, 0649
    internal sealed class ConfigurationManagerAttributes
    {
        /// <summary>
        /// Should the setting be shown as a percentage (only use with value range settings).
        /// </summary>
        public bool? ShowRangeAsPercent;

        /// <summary>
        /// Custom setting editor (OnGUI code that replaces the default editor provided by ConfigurationManager).
        /// See below for a deeper explanation. Using a custom drawer will cause many of the other fields to do nothing.
        /// </summary>
        public System.Action<BepInEx.Configuration.ConfigEntryBase> CustomDrawer;

        /// <summary>
        /// Show this setting in the settings screen at all? If false, don't show.
        /// </summary>
        public bool? Browsable;

        /// <summary>
        /// Category the setting is under. Null to be directly under the plugin.
        /// </summary>
        public string Category;

        /// <summary>
        /// If set, a "Default" button will be shown next to the setting to allow resetting to default.
        /// </summary>
        public object DefaultValue;

        /// <summary>
        /// Force the "Reset" button to not be displayed, even if a valid DefaultValue is available. 
        /// </summary>
        public bool? HideDefaultButton;

        /// <summary>
        /// Force the setting name to not be displayed. Should only be used with a <see cref="CustomDrawer"/> to get more space.
        /// Can be used together with <see cref="HideDefaultButton"/> to gain even more space.
        /// </summary>
        public bool? HideSettingName;

        /// <summary>
        /// Optional description shown when hovering over the setting.
        /// Not recommended, provide the description when creating the setting instead.
        /// </summary>
        public string Description;

        /// <summary>
        /// Name of the setting.
        /// </summary>
        public string DispName;

        /// <summary>
        /// Order of the setting on the settings list relative to other settings in a category.
        /// 0 by default, higher number is higher on the list.
        /// </summary>
        public int? Order;

        /// <summary>
        /// Only show the value, don't allow editing it.
        /// </summary>
        public bool? ReadOnly;

        /// <summary>
        /// If true, don't show the setting by default. User has to turn on showing advanced settings or search for it.
        /// </summary>
        public bool? IsAdvanced;

        /// <summary>
        /// Custom converter from setting type to string for the built-in editor textboxes.
        /// </summary>
        public System.Func<object, string> ObjToStr;

        /// <summary>
        /// Custom converter from string to setting type for the built-in editor textboxes.
        /// </summary>
        public System.Func<string, object> StrToObj;
    }
}