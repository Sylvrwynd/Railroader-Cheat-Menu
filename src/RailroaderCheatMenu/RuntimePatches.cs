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
        internal static float ImpactDamageMultiplier { get; private set; } = 1f;
        internal static float OverspeedDamageMultiplier { get; private set; } = 1f;
        internal static float FuelConsumptionMultiplier { get; private set; } = 1f;
        internal const float DefaultHighSpeedDamageThresholdMph = 50f;
        internal static float HighSpeedDamageThresholdMph { get; private set; } = DefaultHighSpeedDamageThresholdMph;

        [ThreadStatic]
        private static int collisionDamageDepth;

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
                Patch(
                    patchMethod,
                    harmonyMethodType,
                    typeof(TrainController),
                    "IntegrationSetCarsDidCollide",
                    nameof(IntegrationSetCarsDidCollidePrefix),
                    nameof(IntegrationSetCarsDidCollidePostfix));
                Patch(
                    patchMethod,
                    harmonyMethodType,
                    typeof(Model.Physics.TrainMath),
                    "DamageForSpeed",
                    null,
                    nameof(DamageForSpeedPostfix));
                Patch(
                    patchMethod,
                    harmonyMethodType,
                    typeof(Model.SteamLocomotive),
                    "PeriodicUpdateConsumeFuel",
                    nameof(ScaleSteamFuelConsumptionPrefix));
                Patch(
                    patchMethod,
                    harmonyMethodType,
                    typeof(Model.Physics.DieselPrimeMover),
                    "get_FuelConsumptionRate",
                    null,
                    nameof(ScaleDieselFuelConsumptionPostfix));
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
            ImpactDamageMultiplier = 1f;
            OverspeedDamageMultiplier = 1f;
            FuelConsumptionMultiplier = 1f;
            HighSpeedDamageThresholdMph = DefaultHighSpeedDamageThresholdMph;
            collisionDamageDepth = 0;
        }

        internal static void ToggleDerailmentProtection()
        {
            PreventDerailment = !PreventDerailment;
        }

        internal static void ToggleHighSpeedDamageProtection()
        {
            ProtectHighSpeedDamage = !ProtectHighSpeedDamage;
        }

        internal static void SetImpactDamageMultiplier(float multiplier)
        {
            ImpactDamageMultiplier = ClampMultiplier(multiplier);
        }

        internal static void SetOverspeedDamageMultiplier(float multiplier)
        {
            OverspeedDamageMultiplier = ClampMultiplier(multiplier);
        }

        internal static void SetFuelConsumptionMultiplier(float multiplier)
        {
            FuelConsumptionMultiplier = ClampMultiplier(multiplier);
        }

        internal static void SetHighSpeedDamageThreshold(float mph)
        {
            HighSpeedDamageThresholdMph = Math.Max(1f, Math.Min(200f, mph));
        }

        private static void Patch(
            MethodInfo patchMethod,
            Type harmonyMethodType,
            string targetName,
            string prefixName,
            string postfixName = null)
        {
            Patch(patchMethod, harmonyMethodType, typeof(Car), targetName, prefixName, postfixName);
        }

        private static void Patch(
            MethodInfo patchMethod,
            Type harmonyMethodType,
            Type targetType,
            string targetName,
            string prefixName,
            string postfixName = null)
        {
            var targetMethods = targetType.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var target = targetMethods
                .Where(method => method.Name == targetName)
                .FirstOrDefault();
            var prefix = string.IsNullOrWhiteSpace(prefixName)
                ? null
                : typeof(RuntimePatches).GetMethod(
                    prefixName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var postfix = string.IsNullOrWhiteSpace(postfixName)
                ? null
                : typeof(RuntimePatches).GetMethod(
                    postfixName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (target == null || (!string.IsNullOrWhiteSpace(prefixName) && prefix == null) || postfixName != null && postfix == null)
            {
                throw new MissingMethodException(targetName + " / " + prefixName);
            }

            var harmonyPrefix = prefix == null
                ? null
                : Activator.CreateInstance(harmonyMethodType, new object[] { prefix });
            var harmonyPostfix = postfix == null
                ? null
                : Activator.CreateInstance(harmonyMethodType, new object[] { postfix });
            patchMethod.Invoke(harmony, new object[] { target, harmonyPrefix, harmonyPostfix, null, null });
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

        private static bool ApplyConditionDeltaPrefix(Car __instance, ref float delta)
        {
            if (delta < 0f && collisionDamageDepth > 0)
            {
                delta *= ImpactDamageMultiplier;
            }

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

        private static void IntegrationSetCarsDidCollidePrefix()
        {
            if (ImpactDamageMultiplier != 1f)
            {
                collisionDamageDepth++;
            }
        }

        private static void IntegrationSetCarsDidCollidePostfix()
        {
            if (collisionDamageDepth > 0)
            {
                collisionDamageDepth--;
            }
        }

        private static void DamageForSpeedPostfix(ref float __result)
        {
            __result *= OverspeedDamageMultiplier;
        }

        private static void ScaleSteamFuelConsumptionPrefix(ref float dt)
        {
            dt *= FuelConsumptionMultiplier;
        }

        private static void ScaleDieselFuelConsumptionPostfix(ref float __result)
        {
            __result *= FuelConsumptionMultiplier;
        }

        private static float ClampMultiplier(float multiplier)
        {
            return Math.Max(0f, Math.Min(5f, multiplier));
        }
    }
}
