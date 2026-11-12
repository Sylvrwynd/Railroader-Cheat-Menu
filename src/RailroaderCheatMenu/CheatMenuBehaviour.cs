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
        private bool teleportPending;
        private bool teleportAwaitingRelease;
        private Rect window = new Rect(30f, 30f, 540f, 500f);
        private Vector2 scrollPosition;
        private int tab;
        private string cashAmount = "10000";
        private string materialAmount = "1000000";
        private string fuelAmount = "1000000";
        private string reputationAmount = "100";
        private int trackClass = 5;
        private string status = Localization.Text("ready");

        private GUIStyle windowStyle;
        private GUIStyle buttonStyle;
        private GUIStyle tabStyle;
        private GUIStyle labelStyle;
        private GUIStyle textFieldStyle;
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
                if (teleportPending)
                {
                    teleportPending = false;
                    teleportAwaitingRelease = false;
                    status = Localization.Text("teleport_cancelled");
                }
                open = !open;
            }

            if (!teleportPending) return;
            if (Input.GetMouseButton(0))
            {
                teleportAwaitingRelease = true;
                return;
            }

            if (!teleportAwaitingRelease || !Input.GetMouseButtonDown(0)) return;
            teleportPending = false;
            teleportAwaitingRelease = false;
            Run(GameActions.TeleportPlayerToCursor);
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
            GUILayout.Label(Localization.Text("target"), labelStyle);
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

            if (ActionButton(Localization.Text("language") + ": " + Localization.LanguageName, 24f))
            {
                Localization.Toggle();
                status = Localization.Text("ready");
            }
            GUILayout.Space(6f);

            if (tab == 0) DrawCompanyTab();
            else if (tab == 1) DrawEquipmentTab();
            else if (tab == 2) DrawTrackTab();
            else if (tab == 3) DrawSafetyTab();
            else DrawQolTab();

            GUILayout.Space(7f);
            if (ActionButton(Localization.Text("close"), 25f)) open = false;
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
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
            if (ActionButton(Localization.Format("set_reputation", reputationAmount)))
            {
                Run(() => GameActions.SetReputationPercent(ParseFloat(reputationAmount)));
            }
            GUILayout.Label(Localization.Format("current_reputation", GameActions.CurrentReputation()), labelStyle);
        }

        private void DrawEquipmentTab()
        {
            GUILayout.Label(Localization.Text("equipment_repair"), labelStyle);
            DrawActionPair("repair_selected", GameActions.RepairSelected, "repair_all", GameActions.RepairAll);
            GUILayout.Label(Localization.Text("repair_description"), labelStyle);

            GUILayout.Space(7f);
            GUILayout.Label(Localization.Text("engine_fueling"), labelStyle);
            DrawActionPair("refuel_selected", GameActions.RefuelSelected, "refuel_all", GameActions.RefuelAll);
            GUILayout.Label(Localization.Text("fuel_description"), labelStyle);

            GUILayout.Space(7f);
            GUILayout.Label(Localization.Text("oiling"), labelStyle);
            DrawActionPair("oil_selected", GameActions.OilSelected, "oil_all", GameActions.OilAll);
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
                "repair_track",
                GameActions.RepairAllTrack,
                "set_track_class",
                () => GameActions.SetAllTrackConditionClass(trackClass),
                null,
                Localization.Format("set_track_class", trackClass));

            GUILayout.Space(7f);
            GUILayout.Label(Localization.Text("mow_yard_materials"), labelStyle);
            DrawAmountAction(ref materialAmount, "restock_yards", () => GameActions.RestockMowMaterials(ParseFloat(materialAmount)));
            GUILayout.Label(Localization.Text("materials"), labelStyle);

            GUILayout.Space(7f);
            GUILayout.Label(Localization.Text("yard_fuel_supplies"), labelStyle);
            DrawAmountAction(ref fuelAmount, "restock_fuel_yards", () => GameActions.RestockFuelSupplies(ParseFloat(fuelAmount)));
            GUILayout.Label(Localization.Text("fuel_materials"), labelStyle);
        }

        private void DrawSafetyTab()
        {
            GUILayout.Label(Localization.Text("derailment_tools"), labelStyle);
            if (ActionButton(Localization.Text("rerail_all"), 25f)) Run(GameActions.RerailAll);
            DrawActionPair(
                "derailment_toggle",
                GameActions.ToggleDerailmentProtection,
                "speed_damage_toggle",
                GameActions.ToggleHighSpeedDamageProtection,
                RuntimePatches.PreventDerailment ? Localization.Text("enabled") : Localization.Text("disabled"),
                RuntimePatches.ProtectHighSpeedDamage ? Localization.Text("enabled") : Localization.Text("disabled"));
            GUILayout.Label(Localization.Text("physics_description"), labelStyle);
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

            GUILayout.Space(7f);
            GUILayout.Label(Localization.Text("player_teleport"), labelStyle);
            if (ActionButton(Localization.Text("arm_teleport")))
            {
                teleportPending = true;
                teleportAwaitingRelease = false;
                open = false;
                status = Localization.Text("teleport_armed");
            }
            GUILayout.Label(Localization.Text("teleport_description"), labelStyle);
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

        private void DrawAmountAction(ref string amount, string actionKey, Func<string> action)
        {
            GUILayout.BeginHorizontal();
            amount = GUILayout.TextField(amount, textFieldStyle, GUILayout.Width(140f), GUILayout.Height(23f));
            if (ActionButton(Localization.Text(actionKey))) Run(action);
            GUILayout.EndHorizontal();
        }

        private void DrawActionPair(string leftKey, Func<string> leftAction, string rightKey, Func<string> rightAction)
        {
            DrawActionPair(leftKey, leftAction, rightKey, rightAction, null, null);
        }

        private void DrawActionPair(
            string leftKey,
            Func<string> leftAction,
            string rightKey,
            Func<string> rightAction,
            string leftSuffix,
            string rightSuffix)
        {
            GUILayout.BeginHorizontal();
            var leftLabel = Localization.Text(leftKey);
            var rightLabel = Localization.Text(rightKey);
            if (!string.IsNullOrWhiteSpace(leftSuffix)) leftLabel += ": " + leftSuffix;
            if (!string.IsNullOrWhiteSpace(rightSuffix)) rightLabel += ": " + rightSuffix;
            if (ActionButton(leftLabel)) Run(leftAction);
            if (ActionButton(rightLabel)) Run(rightAction);
            GUILayout.EndHorizontal();
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
