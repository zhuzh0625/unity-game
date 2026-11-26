using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Galleon
{
    [DefaultExecutionOrder(130)]
    public sealed partial class VoyageHud : MonoBehaviour
    {
        static readonly Color Ink = new Color(.033f, .068f, .08f, .85f);
        static readonly Color Brass = new Color(.96f, .79f, .51f);
        static readonly Color Paper = new Color(.98f, .96f, .89f);
        static readonly Color HealthGreen = new Color(.57f, .79f, .65f);
        static readonly Color Danger = new Color(1f, .61f, .51f);
        const int SightSteps = 6;
        const float HealthTrackWidth = 184f;
        const float SwimWarningSeconds = 5f;
        static readonly Vector2 Centre = new Vector2(.5f, .5f);
        static readonly Vector2[] SightAxes = { Vector2.right, Vector2.left, Vector2.up, Vector2.down };
        readonly RectTransform[] sightRails = new RectTransform[4];
        readonly RectTransform[] sightMarks = new RectTransform[4 * SightSteps];
        readonly Vector3[] sightDirections = new Vector3[4 * SightSteps];
        Matrix4x4 sightProjection;
        Rect sightViewport;
        int displayedGunYaw = int.MinValue, displayedGunPitch = int.MinValue;
        TextMeshProUGUI gunAngles;
        NavalGame game;
        TMP_FontAsset font;
        RectTransform root, gameplay, promptPanel, help, reticle, defeat;
        RectTransform crewHealth, respawnCountdown, swimCountdown;
        RectTransform weaponPanel, pistolSight, targetPanel;
        RectTransform companyPanel;
        TextMeshProUGUI companyCount, helmStatus, outcomeTitle, outcomeReason;
        TextMeshProUGUI leakCount, floodPercent;
        RectTransform floodFill;
        int displayedCrewCount = -1, displayedLeaks = -1, displayedFloodPercent = -1;
        TextMeshProUGUI shipName, statusText, promptText;
        TextMeshProUGUI weaponText, reloadText, keyHint;
        readonly TextMeshProUGUI[] targetValues = new TextMeshProUGUI[2];
        readonly Image[] targetFills = new Image[2];
        readonly int[] displayedTargets = { -1, -1 };
        const float TargetTrackWidth = 190f;
        int displayedReload = -1;
        CrewWeapon? displayedWeapon;
        bool displayedDrinkingMedicine;
        TextMeshProUGUI healthValue, respawnCountdownText, swimCountdownText;
        UnityEngine.UI.Image healthFill;
        UnityEngine.UI.Image progress;
        int displayedHealth = -1, displayedRespawnSeconds = -1, displayedSwimSeconds = -1;
        bool helpVisible;
        float voyageStart;

        public bool IsGunSightVisible => reticle && reticle.gameObject.activeInHierarchy;
        public bool IsDefeatVisible => defeat && defeat.gameObject.activeInHierarchy && !game.WonVoyage;
        public bool IsVictoryVisible => defeat && defeat.gameObject.activeInHierarchy && game.WonVoyage;
        public UnityEngine.UI.Button PauseMenuButton { get; private set; }
        public UnityEngine.UI.Button DefeatMenuButton { get; private set; }

        internal void Initialize(NavalGame source)
        {
            game = source;
            font = Resources.Load<TMP_FontAsset>("Typography/SailorText");
            var canvasObject = new GameObject("航海界面", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 810);
            scaler.matchWidthOrHeight = .5f;
            root = canvasObject.GetComponent<RectTransform>();
            gameplay = Box(root, "航行界面", Vector2.zero, Vector2.zero, Vector2.zero);
            gameplay.anchorMax = Vector2.one;
            var status = Panel(gameplay, "航行状态", new Vector2(0,1), new Vector2(24,-23), new Vector2(334,62), Ink);
            Rule(status, new Vector2(0,1), new Vector2(0,0), new Vector2(3,62), Brass);
            shipName = Label(status, "船名", new Vector2(0,1), new Vector2(15,-4), new Vector2(312,25), 17, Brass);
            statusText = Label(status, "船况", new Vector2(0,1), new Vector2(15,-29), new Vector2(312,28), 17, Paper);
            BuildCrewHealth();
            BuildCombatHud();
            var keys = Panel(gameplay, "训练按键", Vector2.one, new Vector2(-24,-23), new Vector2(234,34), Ink);
            keyHint = Label(keys, "提示", Centre, Vector2.zero, new Vector2(228,32), 15, Paper,"",TextAlignmentOptions.Center);
            var topLeft = new Vector2(0, 1);
            companyPanel = Panel(gameplay, "船员编制", Vector2.one, new Vector2(-24, -67), new Vector2(234, 119), Ink);
            companyCount = Label(companyPanel, "在船人数", topLeft, new Vector2(12, -3), new Vector2(210, 26), 17, Brass);
            Label(companyPanel, "破口标签", topLeft, new Vector2(12, -29), new Vector2(125, 25), 16, Paper, "船体破口");
            leakCount = Label(companyPanel, "破口数", Vector2.one, new Vector2(-12, -29), new Vector2(80, 25), 16,
                Paper, "", TextAlignmentOptions.MidlineRight);
            Label(companyPanel, "进水标签", topLeft, new Vector2(12, -54), new Vector2(125, 25), 16, Paper, "进水进度");
            floodPercent = Label(companyPanel, "进水百分比", Vector2.one, new Vector2(-12, -54), new Vector2(80, 25), 16,
                Paper, "", TextAlignmentOptions.MidlineRight);
            var floodTrack = Panel(companyPanel, "进水条", topLeft, new Vector2(12, -81), new Vector2(210, 6), new Color(.20f, .26f, .27f));
            floodFill = Rule(floodTrack, topLeft, Vector2.zero, new Vector2(0, 6), new Color(.43f, .81f, .89f)).rectTransform;
            helmStatus = Label(companyPanel, "值舵", topLeft, new Vector2(12, -89), new Vector2(210, 25), 16, Paper);
            BuildKillFeed();

            promptPanel = Panel(gameplay,"操作提示",new Vector2(.5f,0),new Vector2(0,30),new Vector2(415,42),Ink);
            promptText = Label(promptPanel,"当前操作",new Vector2(.5f,.5f),Vector2.zero,new Vector2(412,40),19,Paper,"",TextAlignmentOptions.Center);
            progress = Rule(promptPanel,Vector2.zero,Vector2.zero,new Vector2(415,3),Brass);
            BuildGunSight();
            help = Panel(gameplay,"暂停和操作",Centre,Vector2.zero,new Vector2(526,626),new Color(.023f,.058f,.068f,.97f));
            Rule(help,new Vector2(0,1),Vector2.zero,new Vector2(526,3),Brass);
            Label(help,"标题",new Vector2(0,1),new Vector2(28,-20),new Vector2(465,45),30,Brass,"航海手册");
            string instructions = "WASD   移动 / 游泳        Shift   奔跑        空格   跳跃\n鼠标   环顾        滚轮   调整镜头距离\nE   接手岗位，再按一次可中断并离开\n"
                + "1   手枪      2   砍刀      3   药剂      再按同键收起\n手枪   右键举枪或放低，瞄准时可移动、点左键开火\n砍刀   点左键挥砍，按住可连续挥砍\n药剂   点左键喝药，1 秒后恢复 50 点生命\n"
                + "登船梯   接触梯面后 W / S 上下，A / D 左右\n松键停住，移出梯面下落。下梯时面向船内，按 S 后退\n"
                + "舵轮   A/D 转舵，W 起锚放帆，S 收帆至下锚\n船长阵亡时，其他船员可临时掌舵\n舷炮   左键或空格发射，炮膛露出水面后可用\n破口   面向闪红裂纹按 E 修补\n舱底泵   在泵柄旁按 E 排水，补好且排干后离岗\n上甲板完全被淹时船只沉没。\n\nF6   双漏点演练        F8   重新开局\nEsc / F1   继续游戏";
            Label(help,"键位",new Vector2(0,1),new Vector2(28,-74),new Vector2(480,477),18,Paper,instructions,TextAlignmentOptions.TopLeft);
            PauseMenuButton = ReturnButton(help, new Vector2(0, 22));

            var bottom = new Vector2(.5f,0);
            var top = new Vector2(.5f,1);
            defeat = Panel(root,"沉没结果",bottom,new Vector2(0,44),new Vector2(430,201),Ink);
            Rule(defeat,top,Vector2.zero,new Vector2(430,3),Brass);
            outcomeTitle = Label(defeat,"标题",top,new Vector2(0,-14),new Vector2(400,44),32,Brass,"",TextAlignmentOptions.Center);
            outcomeReason = Label(defeat,"原因",top,new Vector2(0,-61),new Vector2(400,28),20,Paper,"",TextAlignmentOptions.Center);
            Label(defeat,"重新开局",top,new Vector2(0,-93),new Vector2(400,29),18,Brass,"F8  重新开局",TextAlignmentOptions.Center);
            DefeatMenuButton = ReturnButton(defeat, new Vector2(0, 22));
            defeat.gameObject.SetActive(false);
            SetHelp(false);
        }

        void BuildCrewHealth()
        {
            var topLeft = new Vector2(0, 1);
            var topRight = new Vector2(1, 1);
            crewHealth = Panel(gameplay, "CrewHealth", topLeft, new Vector2(24, -94), new Vector2(208, 50), Ink);
            Rule(crewHealth, topLeft, Vector2.zero, new Vector2(3, 50), Brass);
            Label(crewHealth, "船员生命", topLeft, new Vector2(12, -4), new Vector2(77, 25), 16, Brass, "船员生命");
            healthValue = Label(crewHealth, "CrewHealthValue", topRight, new Vector2(-12, -4), new Vector2(104, 25), 20,
                Paper, "", TextAlignmentOptions.MidlineRight);
            healthValue.fontStyle = FontStyles.Bold;

            var trackSize = new Vector2(HealthTrackWidth, 7);
            var track = Panel(crewHealth, "CrewHealthTrack", topLeft, new Vector2(12, -32), trackSize,
                new Color(.20f, .24f, .24f));
            healthFill = Rule(track, topLeft, Vector2.zero, trackSize, HealthGreen);
            healthFill.gameObject.name = "CrewHealthFill";

            var countdownPosition = new Vector2(0, -57);
            var countdownSize = new Vector2(208, 31);
            respawnCountdown = Panel(crewHealth, "CrewRespawnCountdown", topLeft, countdownPosition, countdownSize, Ink);
            swimCountdown = Panel(crewHealth, "CrewSwimCountdown", topLeft, countdownPosition, countdownSize, Ink);
            respawnCountdownText = Label(respawnCountdown, "CountdownText", Centre, Vector2.zero, new Vector2(184, 31), 16,
                Danger, "", TextAlignmentOptions.Center);
            swimCountdownText = Label(swimCountdown, "CountdownText", Centre, Vector2.zero, new Vector2(184, 31), 16,
                Danger, "", TextAlignmentOptions.Center);
            respawnCountdownText.fontStyle = swimCountdownText.fontStyle = FontStyles.Bold;
        }

        void BuildCombatHud()
        {
            var topLeft = new Vector2(0, 1);
            var topRight = Vector2.one;
            weaponPanel = Panel(gameplay, "CrewWeapon", Vector2.zero, new Vector2(24, 30), new Vector2(210, 64), Ink);
            Rule(weaponPanel, topLeft, Vector2.zero, new Vector2(3, 64), Brass);
            weaponText = Label(weaponPanel, "EquippedWeapon", topLeft, new Vector2(13, -4), new Vector2(188, 30), 20, Paper);
            reloadText = Label(weaponPanel, "PistolReload", topLeft, new Vector2(13, -34), new Vector2(188, 24), 15, Brass);

            pistolSight = Box(gameplay, "PistolSight", Centre, Vector2.zero, new Vector2(24, 24));
            foreach (var axis in SightAxes)
            {
                var mark = SightStroke(pistolSight, "PistolSightMark",
                    axis.x == 0f ? new Vector2(4, 8) : new Vector2(8, 4), Paper);
                mark.anchoredPosition = axis * 8f;
            }

            targetPanel = Box(gameplay, "TestTargetHealth", topRight, new Vector2(-24, -67), new Vector2(214, 112));
            for (int i = 0; i < targetValues.Length; i++)
            {
                var row = Panel(targetPanel, "TestTarget" + i, topRight, new Vector2(0, -56f * i), new Vector2(214, 48), Ink);
                Label(row, "TargetName", topLeft, new Vector2(12, -4), new Vector2(92, 24), 16, Brass,
                    i == 0 ? "左侧假人" : "右侧假人");
                targetValues[i] = Label(row, "TargetHealthValue", topRight, new Vector2(-12, -4), new Vector2(95, 24), 17,
                    Paper, "", TextAlignmentOptions.MidlineRight);
                var size = new Vector2(TargetTrackWidth, 6);
                var track = Panel(row, "TargetHealthTrack", topLeft, new Vector2(12, -33), size, new Color(.20f, .24f, .24f));
                targetFills[i] = Rule(track, topLeft, Vector2.zero, size, HealthGreen);
                targetFills[i].gameObject.name = "TargetHealthFill";
            }
        }

        void RefreshCombatHud()
        {
            var crew = game.Crew;
            bool visible = crew && !crew.IsDead && !helpVisible && !game.IsGameOver;
            weaponPanel.gameObject.SetActive(visible);
            bool showSight = visible && crew.IsPistolAiming;
            pistolSight.gameObject.SetActive(showSight);
            bool showTargets = !helpVisible && !game.IsGameOver && game.TestDummies.Count == targetValues.Length;
            targetPanel.gameObject.SetActive(showTargets);
            if (showTargets)
            {
                for (int i = 0; i < targetValues.Length; i++)
                {
                    int value = game.TestDummies[i].Health;
                    if (value == displayedTargets[i]) continue;
                    displayedTargets[i] = value;
                    targetValues[i].SetText("{0} / {1}", value, CrewHealth.Maximum);
                    targetFills[i].color = value <= CrewHealth.Maximum / 4 ? Danger : HealthGreen;
                    targetFills[i].rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                        TargetTrackWidth * value / CrewHealth.Maximum);
                }
            }
            if (!visible) return;
            bool weaponChanged = displayedWeapon != crew.Weapon;
            if (weaponChanged || displayedDrinkingMedicine != crew.IsDrinkingMedicine)
            {
                displayedWeapon = crew.Weapon;
                displayedDrinkingMedicine = crew.IsDrinkingMedicine;
                weaponText.text = crew.Weapon switch
                {
                    CrewWeapon.Pistol => "武器   燧发手枪",
                    CrewWeapon.Cutlass => "武器   砍刀",
                    CrewWeapon.Medicine => crew.IsDrinkingMedicine ? "药剂   正在喝药" : "药剂   左键喝药",
                    _ => "武器   已收起"
                };
            }
            int seconds = Mathf.CeilToInt(crew.PistolCooldownRemaining);
            if (!weaponChanged && seconds == displayedReload) return;
            displayedReload = seconds;
            if (seconds == 0) reloadText.text = "手枪已装填";
            else reloadText.SetText(crew.Weapon == CrewWeapon.Pistol ? "手枪装填中   {0} 秒" : "手枪装填暂停   {0} 秒", seconds);
        }

        void RefreshCrewHealth()
        {
            var crew = game.Crew;
            bool visible = crew && !helpVisible && !game.IsGameOver;
            if (crewHealth.gameObject.activeSelf != visible) crewHealth.gameObject.SetActive(visible);
            if (!visible) return;

            int health = crew.Health;
            if (health != displayedHealth)
            {
                displayedHealth = health;
                float fraction = health / (float)CrewPlayer.MaxHealth;
                healthValue.SetText("{0} / {1}", health, CrewPlayer.MaxHealth);
                healthValue.color = fraction <= .25f ? Danger : Paper;
                healthFill.color = fraction <= .25f ? Danger : HealthGreen;
                healthFill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, HealthTrackWidth * fraction);
            }

            float respawnSeconds = crew.RespawnSecondsRemaining;
            RefreshCountdown(respawnCountdown, respawnCountdownText, crew.IsDead, respawnSeconds,
                respawnSeconds > 0f ? "已阵亡 · {0} 秒后复活" : "已阵亡 · 等待复活", ref displayedRespawnSeconds);
            float swimSeconds = crew.SwimSecondsRemaining;
            RefreshCountdown(swimCountdown, swimCountdownText, !crew.IsDead && crew.IsSwimming && swimSeconds <= SwimWarningSeconds,
                swimSeconds, "溺水危险 · 剩余 {0} 秒", ref displayedSwimSeconds);
        }

        static void RefreshCountdown(RectTransform panel, TextMeshProUGUI label, bool visible, float remaining,
            string format, ref int displayedSeconds)
        {
            if (panel.gameObject.activeSelf != visible) panel.gameObject.SetActive(visible);
            if (!visible) return;

            int seconds = Mathf.CeilToInt(remaining);
            if (seconds == displayedSeconds) return;
            displayedSeconds = seconds;
            label.SetText(format, seconds);
        }

        void BuildGunSight()
        {
            reticle = Box(gameplay, "炮位瞄点", Vector2.zero, Vector2.zero, Vector2.zero);
            reticle.anchorMax = Vector2.one;
            for (int axis = 0; axis < SightAxes.Length; axis++)
            {
                bool horizontal = SightAxes[axis].x != 0f;
                var railSize = horizontal ? new Vector2(0f, 3f) : new Vector2(3f, 0f);
                sightRails[axis] = SightStroke(reticle, "标线", railSize, new Color(.87f, .77f, .57f, .8f));
                var direction = SightAxes[axis];
                var rotationAxis = new Vector3(direction.y, -direction.x, 0f);
                for (int step = 1; step <= SightSteps; step++)
                {
                    bool major = step % 3 == 0;
                    float length = step == SightSteps ? 15f : major ? 12f : 7f;
                    var size = horizontal ? new Vector2(3.5f, length) : new Vector2(length, 3.5f);
                    var mark = SightStroke(reticle, "刻度", size, major ? Brass : Paper);
                    int index = axis * SightSteps + step - 1;
                    sightMarks[index] = mark;
                    sightDirections[index] = Quaternion.AngleAxis(step, rotationAxis) * Vector3.back;
                    if (major && (SightAxes[axis] == Vector2.right || SightAxes[axis] == Vector2.down))
                    {
                        var offset = horizontal ? new Vector2(0f, -16f) : new Vector2(17f, 0f);
                        var badge = Panel(mark, "读数", Centre, offset, new Vector2(20f, 16f), Ink);
                        Label(badge, "数值", Centre, Vector2.zero, new Vector2(20f, 16f), 13f, Paper,
                            step.ToString() + "°", TextAlignmentOptions.Center);
                    }
                }
            }
            var strip = Panel(reticle, "炮位转角", new Vector2(.5f, 0f), new Vector2(0f, 79f), new Vector2(172f, 24f), Ink);
            gunAngles = Label(strip, "读数", Centre, Vector2.zero, new Vector2(168f, 23f), 15f, Paper, "", TextAlignmentOptions.Center);
            reticle.gameObject.SetActive(false);
        }

        static RectTransform SightStroke(Transform parent, string name, Vector2 size, Color color)
        {
            var backing = Panel(parent, name, Centre, Vector2.zero, size, Ink);
            var face = Rule(backing, Vector2.zero, Vector2.zero, Vector2.zero, color).rectTransform;
            face.anchorMax = Vector2.one;
            face.offsetMin = Vector2.one;
            face.offsetMax = -Vector2.one;
            return backing;
        }

        void RefreshGunSight()
        {
            bool visible = !helpVisible && !game.IsGameOver && game.Crew && !game.Crew.IsDead && game.Crew.IsAiming && game.MainCamera;
            if (reticle.gameObject.activeSelf != visible) reticle.gameObject.SetActive(visible);
            if (!visible) return;

            int yaw = Mathf.RoundToInt(game.Crew.GunYaw);
            int pitch = Mathf.RoundToInt(game.Crew.GunPitch);
            if (yaw != displayedGunYaw || pitch != displayedGunPitch)
            {
                displayedGunYaw = yaw;
                displayedGunPitch = pitch;
                gunAngles.SetText("左右  {0}°     上下  {1}°", yaw, pitch);
            }

            var camera = game.MainCamera;
            var projection = camera.projectionMatrix;
            var viewport = camera.rect;
            if (projection == sightProjection && viewport == sightViewport) return;
            sightProjection = projection;
            sightViewport = viewport;
            var centre = SightAnchor(Vector3.back);
            for (int axis = 0; axis < SightAxes.Length; axis++)
            {
                int first = axis * SightSteps;
                for (int index = first; index < first + SightSteps; index++)
                {
                    var mark = sightMarks[index];
                    mark.anchorMin = mark.anchorMax = SightAnchor(sightDirections[index]);
                }
                var start = Vector2.Lerp(centre, sightMarks[first].anchorMin, .45f);
                var end = sightMarks[first + SightSteps - 1].anchorMin;
                sightRails[axis].anchorMin = Vector2.Min(start, end);
                sightRails[axis].anchorMax = Vector2.Max(start, end);
            }
        }

        Vector2 SightAnchor(Vector3 cameraDirection)
        {
            Vector2 projected = sightProjection.MultiplyPoint(cameraDirection);
            return sightViewport.min + Vector2.Scale(sightViewport.size, Centre + .5f * projected);
        }

        UnityEngine.UI.Button ReturnButton(Transform parent, Vector2 position)
        {
            var box = Box(parent, "ReturnToFleet", new Vector2(.5f, 0), position, new Vector2(190, 40));
            var button = FleetMenu.MakeButton(box, Brass);
            button.onClick.AddListener(game.ReturnToMenu);
            Label(box, "Caption", new Vector2(.5f, .5f), Vector2.zero, new Vector2(186, 40), 18,
                new Color(.07f, .14f, .15f), "返回选船", TextAlignmentOptions.Center);
            return button;
        }

        internal void BeginVoyage(string displayName)
        {
            displayedCrewCount = displayedLeaks = displayedFloodPercent = -1;
            shipName.text = (game.IsCombatTest ? "战斗测试  ·  " : "海盗船  ·  ") + displayName;
            keyHint.text = game.IsCombatTest ? "F1  操作说明     F8  重开测试" : "F1  操作说明     F6  损管演练";
            voyageStart = Time.time;
            ClearKillFeed();
            root.gameObject.SetActive(true);
            gameplay.gameObject.SetActive(true);
            defeat.gameObject.SetActive(false);
            SetHelp(false);
        }

        internal void Hide() => root.gameObject.SetActive(false);

        public void ShowOutcome(bool won)
        {
            outcomeTitle.text = won ? "海战胜利" : "船只沉没";
            outcomeReason.text = won ? "敌舰上甲板已被海水完全淹没" : "上甲板已被海水完全淹没";
            gameplay.gameObject.SetActive(false);
            defeat.gameObject.SetActive(true);
            DefeatMenuButton.Select();
        }

        public void SetHelp(bool value)
        {
            helpVisible=value;
            help.gameObject.SetActive(value);
            killFeed.gameObject.SetActive(!value);
            RefreshCompany();
            RefreshCrewHealth();
            RefreshCombatHud();
            RefreshGunSight();
            if (value) PauseMenuButton.Select();
        }

        void LateUpdate()
        {
            if (!game || !game.Crew || game.IsGameOver) return;
            var ship = game.Pirate;
            string sail = ship.SailStep == 0 ? "收帆" : ship.SailStep == 1 ? "半帆" : "满帆";
            statusText.text = (ship.Anchored ? "锚已落" : "锚已起") + "    ·    " + sail + "    ·    " + (ship.Speed * 1.944f).ToString("0.0") + " 节";
            ExpireKills();
            RefreshCrewHealth();
            RefreshCompany();
            RefreshCombatHud();
            string prompt = game.Crew.Prompt;
            if (string.IsNullOrEmpty(prompt))
            {
                if (game.Crew.Weapon == CrewWeapon.Pistol)
                    prompt = game.Crew.IsPistolAiming ? "WASD  移动     左键  开火     右键  放低" : "右键  举枪瞄准";
                else if (game.Crew.Weapon == CrewWeapon.Cutlass)
                    prompt = "左键  挥砍      按住左键  连砍";
                else if (game.Crew.Weapon == CrewWeapon.None && Time.time - voyageStart < 13f)
                    prompt = game.IsCombatTest ? "1  手枪      2  砍刀      3  药剂" : "WASD  移动      空格  跳跃      E  使用岗位";
            }
            promptPanel.gameObject.SetActive(!helpVisible && !game.Crew.IsDead && !string.IsNullOrEmpty(prompt));
            promptText.text=prompt;
            float fill=Mathf.Clamp01(game.Crew.HoldProgress);
            progress.enabled=fill > .001f;
            progress.rectTransform.sizeDelta=new Vector2(415*fill,3);
            RefreshGunSight();
        }

        void RefreshCompany()
        {
            var company = game.Pirate ? game.Pirate.Company : null;
            bool visible = company && !helpVisible && !game.IsGameOver;
            if (companyPanel.gameObject.activeSelf != visible) companyPanel.gameObject.SetActive(visible);
            if (!visible) return;
            int alive = game.Crew.IsDead ? 0 : 1;
            for (int i = 0; i < company.Mates.Count; i++)
                if (company.Mates[i].Health > 0) alive++;
            if (alive != displayedCrewCount)
            {
                displayedCrewCount = alive;
                companyCount.SetText("存活船员   {0} / {1}", alive, company.Capacity);
            }
            var flooding = game.Pirate.Flooding;
            if (flooding.ActiveLeaks != displayedLeaks)
            {
                displayedLeaks = flooding.ActiveLeaks;
                leakCount.SetText("{0} 处", displayedLeaks);
                leakCount.color = displayedLeaks > 0 ? Danger : Paper;
            }
            float level = flooding.WaterLevel01;
            if (floodFill.anchorMax.x != level) floodFill.anchorMax = new Vector2(level, 1f);
            int percent = Mathf.FloorToInt(level * 100f);
            if (percent == 0) floodPercent.text = level > 0f ? "<1%" : "0%";
            else if (percent != displayedFloodPercent) floodPercent.SetText("{0}%", percent);
            displayedFloodPercent = percent;

            if (!game.Crew.IsDead)
                helmStatus.text = game.Crew.IsAtHelm ? "你正在掌舵" : "你是船长 · 可接舵";
            else if (company.ActingCaptain is CrewMate acting)
                helmStatus.text = DeputyHelmStatus(acting, game.Pirate);
            else helmStatus.text = "舵轮暂无接替者";
        }

        static string DeputyHelmStatus(CrewMate acting, NavalShip ship)
        {
            if (acting.Job == CrewPlayer.Role.Gun) return "代理舵手 · 支援炮位";
            if (acting.Job is CrewPlayer.Role.Leak or CrewPlayer.Role.Pump) return "代理舵手 · 支援损管";
            if (acting.Job != CrewPlayer.Role.Helm || !acting.AtPost) return "代理舵手正在就位";
            if (ship.Anchored) return "代理掌舵 · 已下锚";
            if (ship.Rudder < -.05f) return "代理掌舵 · 左舵";
            if (ship.Rudder > .05f) return "代理掌舵 · 右舵";
            return "代理掌舵 · 正舵";
        }

        static RectTransform Box(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size)
        {
            var box = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            box.SetParent(parent,false);
            box.anchorMin=box.anchorMax=box.pivot=anchor;
            box.anchoredPosition=position;
            box.sizeDelta=size;
            return box;
        }

        static UnityEngine.UI.Image Rule(Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Color color)
        {
            var item=Box(parent,"线",anchor,pos,size).gameObject.AddComponent<UnityEngine.UI.Image>();
            item.color=color;
            item.raycastTarget=false;
            return item;
        }

        static RectTransform Panel(Transform parent,string name,Vector2 anchor,Vector2 pos,Vector2 size,Color color)
        {
            var box=Box(parent,name,anchor,pos,size);
            var image=box.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color=color;
            image.raycastTarget=false;
            return box;
        }

        TextMeshProUGUI Label(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size,float point,Color color,string value="",TextAlignmentOptions alignment=TextAlignmentOptions.MidlineLeft)
        {
            var text=Box(parent,name,anchor,position,size).gameObject.AddComponent<TextMeshProUGUI>();
            text.font=font;
            text.fontSize=point;
            text.color=color;
            text.alignment=alignment;
            text.textWrappingMode=TextWrappingModes.NoWrap;
            text.raycastTarget=false;
            text.text=value;
            return text;
        }
    }
}
