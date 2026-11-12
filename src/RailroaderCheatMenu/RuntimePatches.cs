using System;
using System.Linq;
using System.Reflection;
using Model;
using Railroader.Modding;

namespace CAG.Sylvrwynd.RailroaderCheatMenu
{
    internal static class RuntimePatches
    {
        private const string HarmonyId = "cag-sylvrwynd.railroader-cheat-menu";

        private static object harmony;
        private static MethodInfo unpatchAll;

        internal static bool IsInstalled { get; private set; }
        internal static string FailureReason { get; private set; }
        internal static bool PreventDerailment { get; private set; }
        internal static bool ProtectHighSpeedDamage { get; private set; }
        internal const float HighSpeedDamageThresholdMph = 70f;

        internal static void Install(IModLogger log)
        {
            try
            {
                var harmonyType = FindType("HarmonyLib.Harmony");
                var harmonyMethodType = FindType("HarmonyLib.HarmonyMethod");
                if (harmonyType == null || harmonyMethodType == null)
                {
                    throw new MissingMethodException("Harmony runtime types are unavailable.");
                }

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                var patchMethod = harmonyType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(method => method.Name == "Patch")
                    .Where(method => method.GetParameters().Length == 5)
                    .FirstOrDefault();
                unpatchAll = harmonyType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(method => method.Name == "UnpatchAll")
                    .Where(method => method.GetParameters().Length == 1)
                    .FirstOrDefault();
                if (patchMethod == null || unpatchAll == null)
                {
                    throw new MissingMethodException("Harmony patch methods are unavailable.");
                }

                Patch(patchMethod, harmonyMethodType, "ApplyDerailmentDelta", nameof(ApplyDerailmentDeltaPrefix));
                Patch(patchMethod, harmonyMethodType, "ApplyConditionDelta", nameof(ApplyConditionDeltaPrefix));
                Patch(patchMethod, harmonyMethodType, "SetCondition", nameof(SetConditionPrefix));
                IsInstalled = true;
                FailureReason = null;
                log?.Info("Runtime protection hooks installed.");
            }
            catch (Exception ex)
            {
                try
                {
                    unpatchAll?.Invoke(harmony, new object[] { HarmonyId });
                }
                catch
                {
                    // Keep the mod load failure from becoming a game load failure.
                }
                IsInstalled = false;
                FailureReason = ex.ToString();
                harmony = null;
                unpatchAll = null;
                log?.Error(ex, "Runtime protection hooks could not be installed.");
            }
        }

        internal static void Uninstall()
        {
            if (harmony == null) return;
            unpatchAll?.Invoke(harmony, new object[] { HarmonyId });
            harmony = null;
            unpatchAll = null;
            IsInstalled = false;
            PreventDerailment = false;
            ProtectHighSpeedDamage = false;
        }

        internal static void ToggleDerailmentProtection()
        {
            PreventDerailment = !PreventDerailment;
        }

        internal static void ToggleHighSpeedDamageProtection()
        {
            ProtectHighSpeedDamage = !ProtectHighSpeedDamage;
        }

        private static void Patch(MethodInfo patchMethod, Type harmonyMethodType, string targetName, string prefixName)
        {
            var target = typeof(Car).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(method => method.Name == targetName)
                .FirstOrDefault();
            var prefix = typeof(RuntimePatches).GetMethod(
                prefixName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (target == null || prefix == null)
            {
                throw new MissingMethodException(targetName + " / " + prefixName);
            }

            var harmonyMethod = Activator.CreateInstance(harmonyMethodType, new object[] { prefix });
            patchMethod.Invoke(harmony, new object[] { target, harmonyMethod, null, null, null });
        }

        private static Type FindType(string fullName)
        {
            var qualified = Type.GetType(fullName + ", 0Harmony", false);
            if (qualified != null) return qualified;
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

        private static bool ApplyDerailmentDeltaPrefix(float delta)
        {
            return !PreventDerailment || delta <= 0f;
        }

        private static bool ApplyConditionDeltaPrefix(Car __instance, float delta)
        {
            return !ProtectHighSpeedDamage || delta >= 0f || !IsAboveHighSpeed(__instance);
        }

        private static bool SetConditionPrefix(Car __instance, float condition)
        {
            return !ProtectHighSpeedDamage || condition >= __instance.Condition || !IsAboveHighSpeed(__instance);
        }

        private static bool IsAboveHighSpeed(Car car)
        {
            return car != null && car.VelocityMphAbs >= HighSpeedDamageThresholdMph;
        }
    }
}
