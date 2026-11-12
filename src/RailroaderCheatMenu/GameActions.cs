using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Game;
using Game.Messages;
using Game.Mow;
using Game.Reputation;
using Game.State;
using Model;
using Model.Ops;
using Model.Ops.Definition;
using Railroader.Modding;
using Track;
using UI.Console.Commands;

namespace CAG.Sylvrwynd.RailroaderCheatMenu
{
    internal static class GameActions
    {
        private static readonly string[] MowLoadIds = { "rr/ballast", "rr/ties", "rr/rails" };
        private static readonly string[] FuelLoadIds = { "rr/coal", "rr/diesel-fuel" };
        private static Character.PlayerCharacterController speedController;
        private static float originalMaxStableMoveSpeed;
        private static float originalRunMoveSpeed;
        private static float originalCrouchMoveSpeed;
        private static float originalMaxAirMoveSpeed;
        private static float originalLadderSpeedNormal;
        private static float originalLadderSpeedFast;
        private static bool speedValuesCaptured;
        private static float originalTimeScale = 1f;
        private static bool timeScaleCaptured;

        internal static string HostStatus()
        {
            var state = StateManager.Shared;
            if (state == null) return Localization.Text("world_not_ready");
            return StateManager.IsHost ? Localization.Text("host_ready") : Localization.Text("client_disabled");
        }

        internal static string CurrentBalance()
        {
            var state = StateManager.Shared;
            return state == null || state.Storage == null
                ? Localization.Text("unavailable")
                : state.Storage.Balance.ToString("N0", CultureInfo.InvariantCulture);
        }

        internal static string ChangeCash(int amount)
        {
            RequireHost();
            ApplyCashDelta(amount);
            return Localization.Format("balance_changed", amount.ToString("N0", CultureInfo.InvariantCulture), CurrentBalance());
        }

        internal static string SetCash(int amount)
        {
            RequireHost();
            var delta = checked(amount - StateManager.Shared.Storage.Balance);
            ApplyCashDelta(delta);
            return Localization.Format("balance_set", amount.ToString("N0", CultureInfo.InvariantCulture));
        }

        internal static string RepairSelected()
        {
            RequireHost();
            var car = TrainController.Shared?.SelectedCar;
            if (car == null) return Localization.Text("no_selected_car");
            RepairCar(car);
            return Localization.Format("repaired_car", car.DisplayName);
        }

        internal static string RepairAll()
        {
            RequireHost();
            var cars = TrainController.Shared?.Cars;
            if (cars == null) return Localization.Text("controller_not_ready");
            var count = 0;
            foreach (var car in cars)
            {
                RepairCar(car);
                count++;
            }
            return Localization.Format("repaired_cars", count);
        }

        internal static string RefuelSelected()
        {
            RequireHost();
            var selected = TrainController.Shared?.SelectedCar;
            if (selected == null) return Localization.Text("no_selected_car");
            if (!TryResolveFuelCar(selected, out var fuelCar)) return Localization.Text("selected_not_engine");

            if (FillFuelCar(fuelCar) == 0) return Localization.Text("selected_not_engine");
            return Localization.Format("refueled_car", selected.DisplayName);
        }

        internal static string RefuelAll()
        {
            RequireHost();
            var cars = TrainController.Shared?.Cars;
            if (cars == null) return Localization.Text("controller_not_ready");

            var count = 0;
            foreach (var car in cars)
            {
                Car fuelCar;
                if (car is DieselLocomotive)
                {
                    fuelCar = car;
                }
                else if (car is SteamLocomotive steam)
                {
                    fuelCar = InvokeSteamFuelCar(steam);
                    if (fuelCar == null) continue;
                }
                else
                {
                    continue;
                }

                if (FillFuelCar(fuelCar) > 0) count++;
            }
            return Localization.Format("refueled_cars", count);
        }

        internal static string OilSelected()
        {
            RequireHost();
            var car = TrainController.Shared?.SelectedCar;
            if (car == null) return Localization.Text("no_selected_car");
            if (!car.EnableOiling) return Localization.Text("selected_not_oilable");

            InvokeSetOiled(car, 1f);
            return Localization.Format("oiled_car", car.DisplayName);
        }

        internal static string OilAll()
        {
            RequireHost();
            var cars = TrainController.Shared?.Cars;
            if (cars == null) return Localization.Text("controller_not_ready");

            var count = 0;
            foreach (var car in cars)
            {
                if (car == null || !car.EnableOiling) continue;
                InvokeSetOiled(car, 1f);
                count++;
            }

            return Localization.Format("oiled_cars", count);
        }

        internal static string ToggleDerailmentProtection()
        {
            RequireHost();
            if (!RuntimePatches.IsInstalled) return Localization.Text("runtime_patch_missing");

            RuntimePatches.ToggleDerailmentProtection();
            return Localization.Format(
                "derailment_toggle_status",
                RuntimePatches.PreventDerailment ? Localization.Text("enabled") : Localization.Text("disabled"));
        }

        internal static string ToggleHighSpeedDamageProtection()
        {
            RequireHost();
            if (!RuntimePatches.IsInstalled) return Localization.Text("runtime_patch_missing");

            RuntimePatches.ToggleHighSpeedDamageProtection();
            return Localization.Format(
                "speed_damage_toggle_status",
                RuntimePatches.ProtectHighSpeedDamage ? Localization.Text("enabled") : Localization.Text("disabled"));
        }

        internal static string SetPlayerSpeedMultiplier(float multiplier)
        {
            RequireHost();
            var controller = LocalPlayerCharacter();
            if (controller == null) return Localization.Text("player_not_ready");

            multiplier = Math.Max(0.1f, Math.Min(10f, multiplier));
            CaptureSpeedValues(controller);
            controller.maxStableMoveSpeed = originalMaxStableMoveSpeed * multiplier;
            controller.runMoveSpeed = originalRunMoveSpeed * multiplier;
            controller.crouchMoveSpeed = originalCrouchMoveSpeed * multiplier;
            controller.maxAirMoveSpeed = originalMaxAirMoveSpeed * multiplier;
            controller.ladderSpeedNormal = originalLadderSpeedNormal * multiplier;
            controller.ladderSpeedFast = originalLadderSpeedFast * multiplier;
            return Localization.Format("player_speed_set", multiplier.ToString("0.##", CultureInfo.InvariantCulture));
        }

        internal static string SetGameSpeed(float multiplier)
        {
            RequireHost();
            multiplier = Math.Max(0.1f, Math.Min(20f, multiplier));
            if (!timeScaleCaptured)
            {
                originalTimeScale = UnityEngine.Time.timeScale;
                timeScaleCaptured = true;
            }

            UnityEngine.Time.timeScale = multiplier;
            return Localization.Format("game_speed_set", multiplier.ToString("0.##", CultureInfo.InvariantCulture));
        }

        internal static string TeleportPlayerToCursor()
        {
            RequireHost();
            var selector = CameraSelector.shared;
            if (selector == null || selector.character == null) return Localization.Text("player_not_ready");

            var method = typeof(CameraSelector).GetMethod(
                "TeleportToMouse",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (method == null) throw new MissingMethodException(Localization.Text("teleport_method_missing"));

            method.Invoke(selector, null);
            return Localization.Text("teleport_requested");
        }

        internal static void ResetSessionOverrides()
        {
            if (speedController != null && speedValuesCaptured)
            {
                speedController.maxStableMoveSpeed = originalMaxStableMoveSpeed;
                speedController.runMoveSpeed = originalRunMoveSpeed;
                speedController.crouchMoveSpeed = originalCrouchMoveSpeed;
                speedController.maxAirMoveSpeed = originalMaxAirMoveSpeed;
                speedController.ladderSpeedNormal = originalLadderSpeedNormal;
                speedController.ladderSpeedFast = originalLadderSpeedFast;
            }

            speedController = null;
            speedValuesCaptured = false;

            if (timeScaleCaptured)
            {
                UnityEngine.Time.timeScale = originalTimeScale;
            }

            timeScaleCaptured = false;
            originalTimeScale = 1f;
        }

        internal static string RerailAll()
        {
            RequireHost();
            var cars = TrainController.Shared?.Cars;
            if (cars == null) return Localization.Text("controller_not_ready");

            var carIds = cars
                .Where(car => car != null && car.IsDerailed && !string.IsNullOrWhiteSpace(car.id))
                .Select(car => car.id)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (carIds.Length == 0) return Localization.Text("no_derailed_cars");

            StateManager.ApplyLocal(new Rerail(carIds, 1f));
            return Localization.Format("rerail_done", carIds.Length);
        }

        internal static string CurrentReputation()
        {
            var tracker = ReputationTracker.Shared;
            return tracker == null
                ? Localization.Text("unavailable")
                : (tracker.Reputation * 100f).ToString("0.##", CultureInfo.InvariantCulture) + "%";
        }

        internal static string SetReputationPercent(float percent)
        {
            RequireHost();
            var tracker = ReputationTracker.Shared;
            if (tracker == null) return Localization.Text("unavailable");

            percent = Math.Max(0f, Math.Min(100f, percent));
            var property = typeof(ReputationTracker).GetProperty(
                "Reputation",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var setter = property?.GetSetMethod(true);
            if (setter == null) throw new MissingMethodException(Localization.Text("reputation_method_missing"));
            setter.Invoke(tracker, new object[] { percent / 100f });
            return Localization.Format("reputation_set", percent.ToString("0.##", CultureInfo.InvariantCulture));
        }

        internal static string RepairAllTrack()
        {
            RequireHost();
            var controller = TrackConditionController.Shared;
            var graph = Graph.Shared;
            if (controller == null || graph == null) return Localization.Text("track_controller_not_ready");

            var now = new GameDateTime((double)StateManager.Now);
            var count = 0;
            foreach (var segment in graph.AllSegments)
            {
                if (segment == null || !segment.Available || segment.ConstructionState != TrackConstructionState.Complete) continue;
                controller.SetCondition(segment, 1f, now);
                count++;
            }
            return count == 0
                ? Localization.Text("track_controller_not_ready")
                : Localization.Text("track_repaired");
        }

        internal static string SetAllTrackConditionClass(int desiredClass)
        {
            RequireHost();
            desiredClass = Math.Max(1, Math.Min(5, desiredClass));
            var controller = TrackConditionController.Shared;
            var graph = Graph.Shared;
            if (controller == null || graph == null) return Localization.Text("track_controller_not_ready");

            var count = 0;
            foreach (var segment in graph.AllSegments)
            {
                if (segment == null || !segment.Available || segment.ConstructionState != TrackConstructionState.Complete) continue;
                InvokeDebugSetConditionForTrackClass(controller, segment, desiredClass);
                count++;
            }
            return Localization.Format("track_class_set", count, desiredClass);
        }

        internal static string RestockMowMaterials(float quantity)
        {
            RequireHost();
            if (quantity < 0f) quantity = 0f;
            var loads = MowLoadIds.Select(CarLoadLibrary.GetLoad).Where(load => load != null).ToArray();
            if (loads.Length == 0) return Localization.Text("loads_unavailable");

            var ops = OpsController.Shared;
            if (ops == null || ops.AllIndustries == null) return Localization.Text("operations_not_ready");

            var yards = 0;
            var writes = 0;
            foreach (var industry in ops.AllIndustries)
            {
                if (industry == null || !LooksLikeMowYard(industry)) continue;
                var storage = industry.Storage;
                if (storage == null) continue;

                var prefixes = new HashSet<string>(StringComparer.Ordinal);
                foreach (var component in industry.Components ?? new List<IndustryComponent>())
                {
                    var prefix = GetStoragePrefix(component);
                    if (prefix != null) prefixes.Add(prefix);
                }
                if (prefixes.Count == 0) prefixes.Add(string.Empty);

                foreach (var prefix in prefixes)
                {
                    foreach (var load in loads)
                    {
                        storage.SetStorage(load, quantity, prefix);
                        writes++;
                    }
                }
                yards++;
            }

            return yards == 0
                ? Localization.Text("no_mow_yards")
                : Localization.Format("restocked", writes, yards);
        }

        internal static string RestockFuelSupplies(float quantity)
        {
            RequireHost();
            if (quantity < 0f) quantity = 0f;
            var loads = FuelLoadIds.Select(CarLoadLibrary.GetLoad).Where(load => load != null).ToArray();
            if (loads.Length == 0) return Localization.Text("loads_unavailable");

            var ops = OpsController.Shared;
            if (ops == null || ops.AllIndustries == null) return Localization.Text("operations_not_ready");

            var yards = 0;
            var writes = 0;
            foreach (var industry in ops.AllIndustries)
            {
                if (industry == null || !LooksLikeFuelYard(industry)) continue;
                var storage = industry.Storage;
                if (storage == null) continue;

                var prefixes = new HashSet<string>(StringComparer.Ordinal);
                foreach (var component in industry.Components ?? new List<IndustryComponent>())
                {
                    var prefix = GetStoragePrefix(component);
                    if (prefix != null) prefixes.Add(prefix);
                }
                if (prefixes.Count == 0) prefixes.Add(string.Empty);

                foreach (var prefix in prefixes)
                {
                    foreach (var load in loads)
                    {
                        storage.SetStorage(load, quantity, prefix);
                        writes++;
                    }
                }
                yards++;
            }

            return yards == 0
                ? Localization.Text("no_fuel_yards")
                : Localization.Format("fuel_restocked", writes, yards);
        }

        private static void RepairCar(Car car)
        {
            car.SetCondition(1f);
            if (car.EnableOiling) InvokeSetOiled(car, 1f);
        }

        private static Character.PlayerCharacterController LocalPlayerCharacter()
        {
            var selector = CameraSelector.shared;
            var player = selector == null ? null : selector.character;
            return player == null ? null : player.character;
        }

        private static void CaptureSpeedValues(Character.PlayerCharacterController controller)
        {
            if (speedValuesCaptured && speedController == controller) return;

            speedController = controller;
            originalMaxStableMoveSpeed = controller.maxStableMoveSpeed;
            originalRunMoveSpeed = controller.runMoveSpeed;
            originalCrouchMoveSpeed = controller.crouchMoveSpeed;
            originalMaxAirMoveSpeed = controller.maxAirMoveSpeed;
            originalLadderSpeedNormal = controller.ladderSpeedNormal;
            originalLadderSpeedFast = controller.ladderSpeedFast;
            speedValuesCaptured = true;
        }

        private static bool TryResolveFuelCar(Car selected, out Car fuelCar)
        {
            if (selected is DieselLocomotive)
            {
                fuelCar = selected;
                return true;
            }

            if (selected is SteamLocomotive steam)
            {
                fuelCar = InvokeSteamFuelCar(steam);
                return fuelCar != null;
            }

            foreach (var car in TrainController.Shared?.Cars ?? new List<Car>())
            {
                if (!(car is SteamLocomotive parentSteam)) continue;
                if (InvokeSteamFuelCar(parentSteam) != selected) continue;
                fuelCar = selected;
                return true;
            }

            fuelCar = null;
            return false;
        }

        private static int FillFuelCar(Car car)
        {
            if (car?.Definition?.LoadSlots == null) return 0;

            var filled = 0;
            for (var slotIndex = 0; slotIndex < car.Definition.LoadSlots.Count; slotIndex++)
            {
                var slot = car.Definition.LoadSlots[slotIndex];
                var loadId = slot.RequiredLoadIdentifier;
                if (string.IsNullOrWhiteSpace(loadId)) continue;

                var maximum = LoadSlotExtensions.CalculateMaximumCapacity(slot, loadId, car);
                var info = new CarLoadInfo(loadId, maximum, null);
                CarOpsExtensions.SetLoadInfo(car, slotIndex, info);
                filled++;
            }
            return filled;
        }

        private static bool LooksLikeMowYard(Industry industry)
        {
            var text = ((industry.DisplayName ?? string.Empty) + " " + industry.EntityReference).ToLowerInvariant();
            if (text.Contains("mow") || text.Contains("maintenance")) return true;

            foreach (var component in industry.Components ?? new List<IndustryComponent>())
            {
                var componentText = ((component.DisplayName ?? string.Empty) + " " + component.Identifier).ToLowerInvariant();
                if (componentText.Contains("mow") || componentText.Contains("maintenance")) return true;
            }
            return false;
        }

        private static bool LooksLikeFuelYard(Industry industry)
        {
            var text = ((industry.DisplayName ?? string.Empty) + " " + industry.EntityReference).ToLowerInvariant();
            if (text.Contains("coal") || text.Contains("diesel") || text.Contains("fuel")
                || text.Contains("engine service") || text.Contains("coaling")) return true;

            foreach (var component in industry.Components ?? new List<IndustryComponent>())
            {
                var componentText = ((component.DisplayName ?? string.Empty) + " " + component.Identifier).ToLowerInvariant();
                if (componentText.Contains("coal") || componentText.Contains("diesel") || componentText.Contains("fuel")
                    || componentText.Contains("engine service") || componentText.Contains("coaling")) return true;
            }
            return false;
        }

        private static void RequireHost()
        {
            var state = StateManager.Shared;
            if (state == null) throw new InvalidOperationException(Localization.Text("world_not_ready"));
            if (!StateManager.IsHost) throw new InvalidOperationException(Localization.Text("host_only"));
        }

        private static void ApplyCashDelta(int amount)
        {
            var state = StateManager.Shared;
            if (state == null) throw new InvalidOperationException(Localization.Text("world_not_ready"));

            state.ApplyToBalance(
                amount,
                Game.History.Ledger.Category.Bank,
                default(Nullable<EntityReference>),
                "Manual Adjustment",
                0,
                false);
        }

        private static string InvokeMowSetCondition(float condition, float days, string group)
        {
            var method = typeof(MOWCommand).GetMethod(
                "SetCondition",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(float), typeof(float), typeof(string) },
                null);
            if (method == null) throw new MissingMethodException(Localization.Text("mow_command_missing"));
            return method.Invoke(null, new object[] { condition, days, group }) as string;
        }

        private static void InvokeDebugSetConditionForTrackClass(TrackConditionController controller, TrackSegment segment, int desiredClass)
        {
            var method = typeof(TrackConditionController).GetMethod(
                "DebugSetConditionForTrackClass",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(TrackSegment), typeof(int) },
                null);
            if (method == null) throw new MissingMethodException(Localization.Text("debug_method_missing"));
            method.Invoke(controller, new object[] { segment, desiredClass });
        }

        private static void InvokeSetOiled(Car car, float amount)
        {
            var method = typeof(Car).GetMethod(
                "SetOiled",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(float) },
                null);
            method?.Invoke(car, new object[] { amount });
        }

        private static Car InvokeSteamFuelCar(SteamLocomotive steam)
        {
            var method = typeof(SteamLocomotive).GetMethod(
                "FuelCar",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (method == null) throw new MissingMethodException(Localization.Text("fuel_method_missing"));
            return method.Invoke(steam, null) as Car;
        }

        private static string GetStoragePrefix(IndustryComponent component)
        {
            if (component == null) return null;
            var property = typeof(IndustryComponent).GetProperty(
                "StoragePrefix",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property?.GetValue(component, null) as string;
        }
    }
}

