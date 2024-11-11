using System.Security;
using System.Security.Permissions;
using BepInEx;
using DirectorRework.Cruelty;
using DirectorRework.Config;
using DirectorRework.Hooks;

[module: UnverifiableCode]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace DirectorRework
{
    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class DirectorReworkPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = $"com.{PluginAuthor}.{PluginName}";
        public const string PluginAuthor = "score";
        public const string PluginName = "DirectorReworkPlus";
        public const string PluginVersion = "1.0.5";

        public static DirectorReworkPlugin Instance { get; private set; }

        public void Awake()
        {
            Instance = this;

            Log.Init(Logger);
            PluginConfig.Init(Config);

            CrueltyManager.Init();
            DirectorMain.Init();
            DirectorTweaks.Init();
        }
    }
}
