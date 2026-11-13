using System;
using System.Globalization;
using Railroader.Modding;
using UnityEngine;

namespace CAG.Sylvrwynd.RailroaderCheatMenu
{
    internal sealed class CheatMenuBehaviour : MonoBehaviour
    {
        private static CheatMenuBehaviour instance;
        private static bool worldLoaded;

        private IModLogger log;
        private bool open;
        private Rect window = new Rect(30f, 30f, 540f, 500f);
        private Vector2 scrollPosition;
        private int tab;
        private string cashAmount = "10000";
        private string materialAmount = "1000000";
        private string fuelAmount = "1000000";
        private string reputationAmount = "100";
        private string impactDamagePercent = "100";
        private string overspeedDamagePercent = "100";
        private string wearMultiplierPercent = "100";
        private string oilUsePercent = "100";
        private string fuelConsumptionPercent = "100";
        private string highSpeedThresholdMph = "50";
        private int trackClass = 5;
        private string status = Localization.Text("ready");
        private bool resizingWindow;
        private Vector2 resizeStartMouse;
        private Vector2 resizeStartSize;

        private const float MinWindowWidth = 480f;
        private const float MinWindowHeight = 320f;
        private const float ResizeGripSize = 18f;

        private GUIStyle windowStyle;
        private GUIStyle buttonStyle;
        private GUIStyle tabStyle;
        private GUIStyle labelStyle;
        private GUIStyle textFieldStyle;
        private GUIStyle closeButtonStyle;
        private GUIStyle resizeGripStyle;
        private Texture2D windowBackground;
        private Texture2D controlBackground;
        private Texture2D controlHoverBackground;
        private Texture2D controlActiveBackground;

        internal void Initialize(IModLogger logger)
        {
            instance = this;
            log = logger;
        }

        internal static void SetWorldLoaded(bool loaded)
        {
            worldLoaded = loaded;
            if (!loaded && instance != null)
            {
                instance.open = false;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10))
            {
                open = !open;
            }
        }

        private void OnGUI()
        {
            if (!open || !worldLoaded)
            {
                return;
            }

            EnsureStyles();
            var previousColor = GUI.color;
            GUI.color = Color.white;
            window = GUI.Window(GetInstanceID(), window, DrawWindow, Localization.Text("menu_title"), windowStyle);
            GUI.color = previousColor;
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.BeginHorizontal();
            GUILayout.Label(Localization.Text("target"), labelStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(
                Localization.Text("language") + ": " + Localization.LanguageName,
                buttonStyle,
                GUILayout.Width(145f),
                GUILayout.Height(24f)))
            {
                Localization.Toggle();
                status = Localization.Text("ready");
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(GameActions.HostStatus(), labelStyle);
            GUILayout.Label(status, labelStyle);
            GUILayout.Space(5f);

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(tab == 0, Localization.Text("company"), tabStyle, GUILayout.Height(25f))) tab = 0;
            if (GUILayout.Toggle(tab == 1, Localization.Text("equipment"), tabStyle, GUILayout.Height(25f))) tab = 1;
            if (GUILayout.Toggle(tab == 2, Localization.Text("track_mow"), tabStyle, GUILayout.Height(25f))) tab = 2;
            if (GUILayout.Toggle(tab == 3, Localization.Text("safety"), tabStyle, GUILayout.Height(25f))) tab = 3;
            if (GUILayout.Toggle(tab == 4, Localization.Text("qol"), tabStyle, GUILayout.Height(25f))) tab = 4;
            GUILayout.EndHorizontal();
            GUILayout.Space(5f);

            if (tab == 0) DrawCompanyTab();
            else if (tab == 1) DrawEquipmentTab();
            else if (tab == 2) DrawTrackTab();
            else if (tab == 3) DrawSafetyTab();
            else DrawQolTab();

            GUILayout.Space(7f);
            if (ActionButton(Localization.Text("close"), 25f)) open = false;
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            if (GUI.Button(new Rect(window.width - 28f, 2f, 24f, 20f), "X", closeButtonStyle))
            {
                open = false;
            }

            DrawResizeGrip();
            GUI.DragWindow(new Rect(0f, 0f, Mathf.Max(0f, window.width - 32f), 24f));
        }

        private void DrawCompanyTab()
        {
            GUILayout.Label(Localization.Text("cash"), labelStyle);
            cashAmount = GUILayout.TextField(cashAmount, textFieldStyle, GUILayout.Height(23f));
            GUILayout.BeginHorizontal();
            if (ActionButton(Localization.Text("add"))) Run(() => GameActions.ChangeCash(ParseInt(cashAmount)));
            if (ActionButton(Localization.Text("remove"))) Run(() => GameActions.ChangeCash(-ParseInt(cashAmount)));
            if (ActionButton(Localization.Text("set_exact"))) Run(() => GameActions.SetCash(ParseInt(cashAmount)));
            GUILayout.EndHorizontal();
            GUILayout.Label(Localization.Format("current_balance", GameActions.CurrentBalance()), labelStyle);

            GUILayout.Space(8f);
            GUILayout.Label(Localization.Text("company_reputation"), labelStyle);
            reputationAmount = GUILayout.TextField(reputationAmount, textFieldStyle, GUILayout.Height(23f));
            if (ActionButton(Localization.Text("action_set")))
            {
                Run(() => GameActions.SetReputationPercent(ParseFloat(reputationAmount)));
            }
            GUILayout.Label(Localization.Format("current_reputation", GameActions.CurrentReputation()), labelStyle);
        }

        private void DrawEquipmentTab()
        {
            GUILayout.Label(Localization.Text("equipment_repair"), labelStyle);
            DrawActionPair(
                "repair_selected", "action_repair", GameActions.RepairSelected,
                "repair_all", "action_repair_all", GameActions.RepairAll);
            GUILayout.Label(Localization.Text("repair_description"), labelStyle);

            GUILayout.Space(7f);
            GUILayout.Label(Localization.Text("engine_fueling"), labelStyle);
            DrawActionPair(
                "refuel_selected", "action_refuel", GameActions.RefuelSelected,
                "refuel_all", "action_refuel_all", GameActions.RefuelAll);
            GUILayout.Label(Localization.Text("fuel_description"), labelStyle);

            GUILayout.Space(7f);
            GUILayout.Label(Localization.Text("cargo_loading"), labelStyle);
            DrawActionPair(
                "fill_manifested_selected", "action_fill", GameActions.FillManifestedSelected,
                "fill_manifested_all", "action_fill", GameActions.FillManifestedAll);
            GUILayout.Label(Localization.Text("manifested_cargo_description"), labelStyle);

            GUILayout.Space(7f);
            GUILayout.Label(Localization.Text("cargo_emptying"), labelStyle);
            DrawActionPair(
                "empty_selected_cargo", "action_empty", GameActions.EmptySelectedCargo,
                "empty_all_cargo", "action_empty", GameActions.EmptyAllCargo);
            GUILayout.Label(Localization.Text("empty_cargo_description"), labelStyle);

            GUILayout.Space(7f);
            GUILayout.Label(Localization.Text("oiling"), labelStyle);
            DrawActionPair(
                "oil_selected", "action_oil", GameActions.OilSelected,
                "oil_all", "action_oil_all", GameActions.OilAll);
            GUILayout.Label(Localization.Text("oil_description"), labelStyle);
        }

        private void DrawTrackTab()
        {
            GUILayout.Label(Localization.Text("track_condition"), labelStyle);
            GUILayout.BeginHorizontal();
            for (var value = 1; value <= 5; value++)
            {
                if (GUILayout.Toggle(trackClass == value, "C" + value, tabStyle, GUILayout.Height(24f))) trackClass = value;
            }
            GUILayout.EndHorizontal();
            DrawActionPair(
                "repair_track", "action_repair", GameActions.RepairAllTrack,
                "set_track_class", "action_set", () => GameActions.SetAllTrackConditionClass(trackClass),
                null,
                Localization.Format("set_track_class", trackClass));

            GUILayout.Space(7f);
            GUILayout.Label(Localization.Text("mow_yard_materials"), labelStyle);
            DrawAmountActions(
                ref materialAmount,
                "action_fill",
                () => GameActions.RestockMowMaterials(ParseFloat(materialAmount)),
                "action_empty",
                GameActions.ClearMowMaterials);
            GUILayout.Label(Localization.Text("materials"), labelStyle);

            GUILayout.Space(7f);
            GUILayout.Label(Localization.Text("yard_fuel_supplies"), labelStyle);
            DrawAmountActions(
                ref fuelAmount,
                "action_fill",
                () => GameActions.RestockFuelSupplies(ParseFloat(fuelAmount)),
                "action_empty",
                GameActions.ClearFuelSupplies);
            GUILayout.Label(Localization.Text("fuel_materials"), labelStyle);
        }

        private void DrawSafetyTab()
        {
            GUILayout.Label(Localization.Text("derailment_tools"), labelStyle);
            if (ActionButton(Localization.Text("action_rerail"), 23f)) Run(GameActions.RerailAll);
            DrawStatePair(
                Localization.Text("derailment_toggle"),
                GameActions.ToggleDerailmentProtection,
                RuntimePatches.PreventDerailment,
                Localization.Format("speed_damage_toggle", GameActions.CurrentHighSpeedDamageThreshold()),
                GameActions.ToggleHighSpeedDamageProtection,
                RuntimePatches.ProtectHighSpeedDamage);
            GUILayout.Label(
                Localization.Format("physics_description", GameActions.CurrentHighSpeedDamageThreshold()),
                labelStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label(Localization.Text("speed_damage_threshold"), labelStyle, GUILayout.Width(185f));
            highSpeedThresholdMph = GUILayout.TextField(highSpeedThresholdMph, textFieldStyle, GUILayout.Width(75f), GUILayout.Height(23f));
            var enteredThreshold = highSpeedThresholdMph;
            if (ActionButton(Localization.Text("apply")))
            {
                Run(() => GameActions.SetHighSpeedDamageThreshold(ParseFloat(enteredThreshold)));
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(
                Localization.Format("current_speed_threshold", GameActions.CurrentHighSpeedDamageThreshold()),
                labelStyle);

            GUILayout.Space(7f);
            GUILayout.Label(Localization.Text("wear_tools"), labelStyle);
            DrawStatePair(
                Localization.Text("equipment_wear_toggle"),
                GameActions.ToggleEquipmentWear,
                GameActions.EquipmentWearStatus() == Localization.Text("enabled"),
                Localization.Text("oil_feature_toggle"),
                GameActions.ToggleHotboxesAndOiling,
                GameActions.HotboxesAndOilingStatus() == Localization.Text("enabled"));
            GUILayout.Label(Localization.Text("wear_description"), labelStyle);

            GUILayout.Space(7f);
            GUILayout.Label(Localization.Text("damage_tuning"), labelStyle);
            DrawMultiplierControl(
                ref impactDamagePercent,
                "impact_damage_multiplier",
                GameActions.SetImpactDamagePercent,
                GameActions.CurrentImpactDamagePercent);
            DrawMultiplierControl(
                ref overspeedDamagePercent,
                "overspeed_damage_multiplier",
                GameActions.SetOverspeedDamagePercent,
                GameActions.CurrentOverspeedDamagePercent);
            DrawMultiplierControl(
                ref wearMultiplierPercent,
                "wear_multiplier",
                GameActions.SetEquipmentWearPercent,
                GameActions.CurrentEquipmentWearPercent);
            DrawMultiplierControl(
                ref oilUsePercent,
                "oil_use_multiplier",
                GameActions.SetOilUsePercent,
                GameActions.CurrentOilUsePercent);
            DrawMultiplierControl(
                ref fuelConsumptionPercent,
                "fuel_consumption_multiplier",
                GameActions.SetFuelConsumptionPercent,
                GameActions.CurrentFuelConsumptionPercent);
            GUILayout.Label(Localization.Text("multiplier_description"), labelStyle);
        }

        private void DrawQolTab()
        {
            GUILayout.Label(Localization.Text("player_speed"), labelStyle);
            DrawSpeedButtons(
                new[] { 1f, 2f, 5f },
                multiplier => Run(() => GameActions.SetPlayerSpeedMultiplier(multiplier)));
            GUILayout.Label(Localization.Text("player_speed_description"), labelStyle);

            GUILayout.Space(7f);
            GUILayout.Label(Localization.Text("game_speed"), labelStyle);
            DrawSpeedButtons(
                new[] { 0.5f, 1f, 2f, 5f },
                multiplier => Run(() => GameActions.SetGameSpeed(multiplier)));
            GUILayout.Label(Localization.Text("game_speed_description"), labelStyle);

        }

        private void DrawSpeedButtons(float[] multipliers, Action<float> action)
        {
            GUILayout.BeginHorizontal();
            foreach (var multiplier in multipliers)
            {
                var label = multiplier.ToString("0.##", CultureInfo.InvariantCulture) + "x";
                if (ActionButton(label)) action(multiplier);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawAmountActions(
            ref string amount,
            string primaryKey,
            Func<string> primaryAction,
            string secondaryKey,
            Func<string> secondaryAction)
        {
            GUILayout.BeginHorizontal();
            amount = GUILayout.TextField(amount, textFieldStyle, GUILayout.Width(140f), GUILayout.Height(23f));
            if (ActionButton(Localization.Text(primaryKey))) Run(primaryAction);
            if (ActionButton(Localization.Text(secondaryKey))) Run(secondaryAction);
            GUILayout.EndHorizontal();
        }

        private void DrawMultiplierControl(
            ref string value,
            string labelKey,
            Func<float, string> action,
            Func<string> currentValue)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Localization.Text(labelKey), labelStyle, GUILayout.Width(185f));
            value = GUILayout.TextField(value, textFieldStyle, GUILayout.Width(75f), GUILayout.Height(23f));
            var enteredValue = value;
            if (ActionButton(Localization.Text("apply"))) Run(() => action(ParseFloat(enteredValue)));
            GUILayout.EndHorizontal();
            GUILayout.Label(Localization.Format("current_multiplier", currentValue()), labelStyle);
        }

        private void DrawResizeGrip()
        {
            var gripRect = new Rect(
                window.width - ResizeGripSize,
                window.height - ResizeGripSize,
                ResizeGripSize,
                ResizeGripSize);
            GUI.Box(gripRect, string.Empty, resizeGripStyle);

            var currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown
                && currentEvent.button == 0
                && gripRect.Contains(currentEvent.mousePosition))
            {
                resizingWindow = true;
                resizeStartMouse = currentEvent.mousePosition;
                resizeStartSize = new Vector2(window.width, window.height);
                currentEvent.Use();
            }
            else if (resizingWindow && currentEvent.type == EventType.MouseDrag && currentEvent.button == 0)
            {
                var delta = currentEvent.mousePosition - resizeStartMouse;
                window.width = Mathf.Max(MinWindowWidth, resizeStartSize.x + delta.x);
                window.height = Mathf.Max(MinWindowHeight, resizeStartSize.y + delta.y);
                currentEvent.Use();
            }
            else if (resizingWindow && currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
            {
                resizingWindow = false;
                currentEvent.Use();
            }
        }

        private void DrawActionPair(
            string leftLabelKey,
            string leftButtonKey,
            Func<string> leftAction,
            string rightLabelKey,
            string rightButtonKey,
            Func<string> rightAction,
            string leftSuffix = null,
            string rightSuffix = null)
        {
            GUILayout.BeginHorizontal();
            DrawCompactAction(
                Localization.Text(leftLabelKey) + FormatSuffix(leftSuffix),
                Localization.Text(leftButtonKey),
                leftAction);
            DrawCompactAction(
                Localization.Text(rightLabelKey) + FormatSuffix(rightSuffix),
                Localization.Text(rightButtonKey),
                rightAction);
            GUILayout.EndHorizontal();
        }

        private void DrawStatePair(
            string leftLabel,
            Func<string> leftAction,
            bool leftEnabled,
            string rightLabel,
            Func<string> rightAction,
            bool rightEnabled)
        {
            GUILayout.BeginHorizontal();
            DrawCompactAction(leftLabel, leftEnabled ? Localization.Text("state_enabled") : Localization.Text("state_disabled"), leftAction);
            DrawCompactAction(rightLabel, rightEnabled ? Localization.Text("state_enabled") : Localization.Text("state_disabled"), rightAction);
            GUILayout.EndHorizontal();
        }

        private void DrawCompactAction(string label, string buttonText, Func<string> action)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(label, labelStyle);
            if (ActionButton(buttonText, 23f)) Run(action);
            GUILayout.EndVertical();
        }

        private static string FormatSuffix(string suffix)
        {
            return string.IsNullOrWhiteSpace(suffix) ? string.Empty : " (" + suffix + ")";
        }

        private bool ActionButton(string text, float height = 25f)
        {
            return GUILayout.Button(text, buttonStyle, GUILayout.Height(height));
        }

        private void EnsureStyles()
        {
            if (windowStyle != null) return;

            // Match Railroader's stock visual language while keeping this
            // IMGUI window opaque even when the global skin is translucent.
            windowBackground = MakePanelTexture(
                new Color(0.105f, 0.095f, 0.080f, 1f),
                new Color(0.55f, 0.46f, 0.31f, 1f),
                2);
            controlBackground = MakePanelTexture(
                new Color(0.13f, 0.12f, 0.105f, 1f),
                new Color(0.46f, 0.38f, 0.25f, 1f),
                2);
            controlHoverBackground = MakePanelTexture(
                new Color(0.19f, 0.16f, 0.12f, 1f),
                new Color(0.64f, 0.53f, 0.34f, 1f),
                2);
            controlActiveBackground = MakePanelTexture(
                new Color(0.25f, 0.20f, 0.14f, 1f),
                new Color(0.75f, 0.63f, 0.40f, 1f),
                2);

            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.background = windowBackground;
            windowStyle.onNormal.background = windowBackground;
            windowStyle.border = new RectOffset(4, 4, 4, 4);
            windowStyle.normal.textColor = new Color(0.78f, 0.73f, 0.64f, 1f);
            windowStyle.onNormal.textColor = windowStyle.normal.textColor;
            windowStyle.fontSize = 13;
            windowStyle.padding = new RectOffset(10, 10, 28, 9);

            buttonStyle = new GUIStyle(GUI.skin.button);
            ApplyControlStyle(buttonStyle);

            tabStyle = new GUIStyle(GUI.skin.button);
            ApplyControlStyle(tabStyle);

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = new Color(0.77f, 0.72f, 0.63f, 1f);
            labelStyle.wordWrap = true;
            labelStyle.fontSize = 12;

            textFieldStyle = new GUIStyle(GUI.skin.textField);
            textFieldStyle.normal.background = controlBackground;
            textFieldStyle.focused.background = controlActiveBackground;
            textFieldStyle.normal.textColor = new Color(0.86f, 0.82f, 0.74f, 1f);
            textFieldStyle.focused.textColor = textFieldStyle.normal.textColor;
            textFieldStyle.border = new RectOffset(4, 4, 4, 4);
            textFieldStyle.fontSize = 13;
            textFieldStyle.padding = new RectOffset(6, 6, 3, 3);

            closeButtonStyle = new GUIStyle(buttonStyle);
            closeButtonStyle.fontSize = 13;
            closeButtonStyle.padding = new RectOffset(0, 0, 0, 0);

            resizeGripStyle = new GUIStyle(GUI.skin.box);
            resizeGripStyle.normal.background = controlBackground;
            resizeGripStyle.normal.textColor = new Color(0.70f, 0.64f, 0.52f, 1f);
            resizeGripStyle.fontSize = 10;
        }

        private void ApplyControlStyle(GUIStyle style)
        {
            style.normal.background = controlBackground;
            style.hover.background = controlHoverBackground;
            style.active.background = controlActiveBackground;
            style.focused.background = controlActiveBackground;
            style.onNormal.background = controlActiveBackground;
            style.onHover.background = controlActiveBackground;
            style.onActive.background = controlActiveBackground;
            style.border = new RectOffset(4, 4, 4, 4);
            var stockText = new Color(0.84f, 0.79f, 0.70f, 1f);
            style.normal.textColor = stockText;
            style.hover.textColor = stockText;
            style.active.textColor = stockText;
            style.focused.textColor = stockText;
            style.onNormal.textColor = stockText;
            style.onHover.textColor = stockText;
            style.onActive.textColor = stockText;
            style.wordWrap = true;
            style.fontSize = 12;
            style.padding = new RectOffset(6, 6, 4, 4);
        }

        private static Texture2D MakePanelTexture(Color fill, Color border, int borderWidth)
        {
            const int size = 9;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var isBorder = x < borderWidth || y < borderWidth
                        || x >= size - borderWidth || y >= size - borderWidth;
                    texture.SetPixel(x, y, isBorder ? border : fill);
                }
            }
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            DestroyTexture(windowBackground);
            DestroyTexture(controlBackground);
            DestroyTexture(controlHoverBackground);
            DestroyTexture(controlActiveBackground);
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture != null) Destroy(texture);
        }

        private void Run(Func<string> action)
        {
            try
            {
                status = action() ?? Localization.Text("done");
                log?.Info(status);
            }
            catch (Exception ex)
            {
                var cause = ex.InnerException ?? ex;
                status = Localization.Format("error", cause.Message);
                log?.Error(ex, status);
            }
        }

        private int ParseInt(string value)
        {
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : 0;
        }

        private float ParseFloat(string value)
        {
            float parsed;
            return float.TryParse(value, out parsed) ? parsed : 0f;
        }
    }
}
