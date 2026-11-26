using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UI = UnityEngine.UI;

namespace Galleon
{
    public sealed class FleetMenu : MonoBehaviour
    {
        static readonly ShipType[] Ships = (ShipType[])System.Enum.GetValues(typeof(ShipType));
        static readonly Color Ink = new Color32(19, 39, 43, 255);
        static readonly Color Panel = new Color32(26, 49, 53, 255);
        static readonly Color Highlight = new Color32(51, 74, 73, 255);
        static readonly Color Brass = new Color32(227, 189, 127, 255);
        static readonly Color Paper = new Color32(245, 240, 221, 255);
        static readonly Color Muted = new Color32(173, 192, 187, 255);
        static readonly Color Line = new Color32(74, 92, 92, 255);
        static readonly Vector2 TopLeft = new Vector2(0, 1);
        const float SidebarFraction = .34f;
        const float Inset = 38f;

        readonly UI.Button[] buttons = new UI.Button[Ships.Length];
        readonly UI.Image[] marks = new UI.Image[Ships.Length];
        readonly TextMeshProUGUI[] names = new TextMeshProUGUI[Ships.Length];
        public UI.Button StartButton { get; private set; }
        public UI.Button CombatButton { get; private set; }
        public UI.Button GetShipButton(ShipType type) => buttons[System.Array.IndexOf(Ships, type)];

        NavalGame game;
        TMP_FontAsset font;
        TextMeshProUGUI measurements;
        GameObject preview;
        ShipType previewType;
        Bounds previewBounds;
        int lastWidth, lastHeight;
        float voyageFov;

        internal void Initialize(NavalGame source)
        {
            game = source;
            voyageFov = game.MainCamera.fieldOfView;
            font = Resources.Load<TMP_FontAsset>("Typography/SailorText");
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<UI.CanvasScaler>();
            scaler.uiScaleMode = UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 810);
            scaler.screenMatchMode = UI.CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<UI.GraphicRaycaster>();

            var rail = Rect(transform, "SelectionPanel", Vector2.zero, new Vector2(SidebarFraction, 1), Vector2.zero, Vector2.zero);
            Fill(rail, Ink);
            var edge = Rect(rail, "BrassEdge", new Vector2(1, 0), Vector2.one, new Vector2(-2, 0), Vector2.zero);
            Fill(edge, Brass);
            Text(Row(rail, "Title", 59, 76), "风帆海战", 51, Paper).fontStyle = FontStyles.Bold;
            Text(Row(rail, "Mode", 139, 33), "单人海战 · AI 船员协作", 21, Brass);
            Fill(Row(rail, "TitleRule", 201, 1), Line);

            var list = Rect(rail, "FleetList", new Vector2(0, .5f), new Vector2(1, .5f),
                new Vector2(0, -103), new Vector2(0, 103));
            Text(Row(list, "ChooseShip", 0, 32), "选择船只", 17, Muted);
            for (int i = 0; i < Ships.Length; i++)
            {
                var type = Ships[i];
                var row = Row(list, "Select" + type, 45 + i * 85, 76);
                var definition = ShipDefinition.For(type);
                var button = MakeButton(row, Panel);
                button.onClick.AddListener(() => game.SelectShip(type));
                buttons[i] = button;
                marks[i] = Fill(Rect(row, "SelectionMark", Vector2.zero, TopLeft,
                    Vector2.zero, new Vector2(4, 0)), Brass);
                names[i] = Text(Row(row, "ShipName", 8, 33, 20), definition.DisplayName, 23, Paper);
                int gunners = ShipCompany.GetGunnerCount(type);
                int engineers = ShipCompany.GetDamageControlCount(type);
                Text(Row(row, "CrewComplement", 41, 24, 21),
                    $"{1 + gunners + engineers} 人   ·   船长 1   炮手 {gunners}   损管 {engineers}", 16, Muted);
            }

            var combatRect = Rect(rail, "StartCombatTest", Vector2.zero, Vector2.right,
                new Vector2(Inset, 123), new Vector2(-Inset, 171));
            CombatButton = MakeButton(combatRect, Panel);
            CombatButton.onClick.AddListener(game.StartCombatTest);
            Text(Row(combatRect, "Caption", 0, 48, 20), "战斗测试", 21, Paper);
            var launchRect = Rect(rail, "StartVoyage", Vector2.zero, Vector2.right,
                new Vector2(Inset, 52), new Vector2(-Inset, 112));
            StartButton = MakeButton(launchRect, Brass);
            StartButton.onClick.AddListener(game.StartVoyage);
            Text(Row(launchRect, "Caption", 0, 60, 20), "开始航行", 23, Ink).fontStyle = FontStyles.Bold;
            var arrow = Rect(launchRect, "Forward", new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(-39, -11), new Vector2(-17, 11));
            Text(arrow, "›", 28, Ink, TextAlignmentOptions.Center);
            for (int i = 0; i < buttons.Length; i++)
                Link(buttons[i], i == 0 ? StartButton : buttons[i - 1], i == buttons.Length - 1 ? CombatButton : buttons[i + 1]);
            Link(CombatButton, buttons[buttons.Length - 1], StartButton);
            Link(StartButton, CombatButton, buttons[0]);

            var plaque = Rect(transform, "Dimensions", new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-329, 32), new Vector2(-30, 80));
            Fill(plaque, new Color(.055f, .112f, .12f, .91f));
            measurements = Text(Rect(plaque, "HullMeasurements", Vector2.zero, Vector2.one,
                new Vector2(10, 0), new Vector2(-10, 0)), "", 17, Paper, TextAlignmentOptions.Center);
        }

        internal void Show(ShipType type)
        {
            gameObject.SetActive(true);
            ShowShip(type);
            GetShipButton(type).Select();
        }

        internal void ShowShip(ShipType type)
        {
            var definition = ShipDefinition.For(type);
            for (int i = 0; i < Ships.Length; i++)
            {
                bool chosen = Ships[i] == type;
                marks[i].enabled = chosen;
                buttons[i].image.color = chosen ? Highlight : Panel;
                names[i].color = chosen ? Brass : Paper;
            }
            measurements.text = $"全长  {definition.Length:0.#} m     船宽  {definition.Beam:0.#} m";
            if (preview && previewType == type) return;
            ClearPreview();
            var model = Resources.Load<GameObject>(definition.ModelPath);
            if (!model) throw new System.InvalidOperationException("Missing fleet preview " + definition.ModelPath);
            preview = Instantiate(model, game.transform, false);
            preview.name = type + " Preview";
            previewType = type;
            var secondFlag = preview.transform.Find("FlagsBritish");
            if (secondFlag) secondFlag.gameObject.SetActive(false);
            // Keep the preview off the voyage's former hull cutouts on the same ocean.
            preview.transform.position = new Vector3(83, -definition.DryDraft, 16);
            var renderers = preview.GetComponentsInChildren<MeshRenderer>();
            previewBounds = renderers[0].bounds;
            foreach (var mesh in renderers) previewBounds.Encapsulate(mesh.bounds);
            FrameShip();
        }

        internal void Hide()
        {
            ClearPreview();
            var camera = game.MainCamera;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.fieldOfView = voyageFov;
            camera.ResetAspect();
            gameObject.SetActive(false);
        }

        void ClearPreview()
        {
            if (!preview) return;
            preview.SetActive(false);
            Destroy(preview);
            preview = null;
        }

        void LateUpdate()
        {
            if (preview && (lastWidth != Screen.width || lastHeight != Screen.height)) FrameShip();
        }

        void FrameShip()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            var camera = game.MainCamera;
            camera.rect = new Rect(SidebarFraction, 0, 1 - SidebarFraction, 1);
            camera.fieldOfView = 36f;
            camera.aspect = Mathf.Max(.1f, lastWidth * (1 - SidebarFraction) / Mathf.Max(1, lastHeight));
            var look = Quaternion.LookRotation(new Vector3(-1f, -.23f, -.49f), Vector3.up);
            var inverse = Quaternion.Inverse(look);
            float tangent = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
            float distance = 0f;
            var target = previewBounds.center;
            target.y = (previewBounds.max.y + Mathf.Max(0, previewBounds.min.y)) * .5f;
            foreach (var mesh in preview.GetComponentsInChildren<MeshRenderer>())
            {
                var bounds = mesh.bounds;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                    corner.y = Mathf.Max(corner.y, 0);
                    var local = inverse * (corner - target);
                    float fitX = Mathf.Abs(local.x) / (tangent * camera.aspect * .88f);
                    float fitY = Mathf.Abs(local.y) / (tangent * .78f);
                    distance = Mathf.Max(distance, Mathf.Max(fitX, fitY) - local.z);
                }
            }
            camera.transform.SetPositionAndRotation(target - look * Vector3.forward * distance, look);
        }

        static void Link(UI.Button button, UI.Button previous, UI.Button next)
        {
            button.navigation = new UI.Navigation { mode = UI.Navigation.Mode.Explicit,
                selectOnUp = previous, selectOnDown = next, selectOnLeft = previous, selectOnRight = next };
        }

        internal static UI.Button MakeButton(RectTransform rect, Color background)
        {
            var image = Fill(rect, background);
            image.raycastTarget = true;
            var outline = rect.gameObject.AddComponent<UI.Outline>();
            outline.effectColor = Paper;
            outline.effectDistance = new Vector2(1.7f, -1.7f);
            outline.enabled = false;
            rect.gameObject.AddComponent<FleetButtonFocus>().Outline = outline;
            var button = rect.gameObject.AddComponent<UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.73f, .86f, .88f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.77f, .81f, .79f);
            button.colors = colors;
            return button;
        }

        static UI.Image Fill(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        TextMeshProUGUI Text(RectTransform rect, string value, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = value;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        static RectTransform Row(Transform parent, string name, float top, float height, float inset = Inset) =>
            Rect(parent, name, TopLeft, Vector2.one, new Vector2(inset, -top - height), new Vector2(-inset, -top));

        static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max, Vector2 lo, Vector2 hi)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = lo;
            rect.offsetMax = hi;
            return rect;
        }
    }

    internal sealed class FleetButtonFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        internal UI.Outline Outline;
        public void OnSelect(BaseEventData data) => Outline.enabled = true;
        public void OnDeselect(BaseEventData data) => Outline.enabled = false;
        void OnDisable() { if (Outline) Outline.enabled = false; }
    }
}
