using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IronBastion
{
    public enum BattleState { Menu, Playing, Paused, StageClear, GameOver, Victory }
    public enum PickupKind { Star, Shield, Bomb, Freeze, Life }

    public sealed class BattleGame : MonoBehaviour
    {
        public const float RespawnProtectionSeconds = 3.5f;
        public static BattleGame Instance { get; private set; }
        public BattleState State { get; private set; } = BattleState.Menu;
        public int Stage { get; private set; } = 1;
        public int Lives { get; private set; } = 3;
        public int Score { get; private set; }
        public int Best { get; private set; }
        public int Kills { get; private set; }
        public int Spawned { get; private set; }
        public int Total => 12 + (Stage - 1) * 4;
        public int EnemyLimit => Stage == 1 ? 3 : 4;
        public float FirstSpawnDelay => Stage == 1 ? 5f : 2.5f;
        public float EnemySpawnInterval => Stage == 1 ? 4.5f : Mathf.Max(1.7f, 3.4f - Stage * .4f);
        public int Remaining => Total - Kills;
        public float FrozenUntil { get; private set; }
        public string ResultReason { get; private set; } = "";
        public string Notice { get; private set; } = "";
        public float NoticeUntil { get; private set; }
        public TankActor Player { get; private set; }
        public readonly List<TankActor> Enemies = new();
        public Transform DynamicRoot { get; private set; }
        public BattleHud Hud { get; private set; }
        public Transform Preview;
        Transform board;
        float nextSpawn, respawnAt;
        bool waitingRespawn;
        int weaponLevel;
        public bool Muted { get; private set; }
        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            Best = PlayerPrefs.GetInt("IronBastion.Best", 0);
            Muted = PlayerPrefs.GetInt("IronBastion.Muted", 0) == 1;
            AudioListener.volume = Muted ? 0 : .65f;
        }
        void Start()
        {
            if (Preview) { Preview.gameObject.SetActive(false); Destroy(Preview.gameObject); }
            BattleEffects.Initialize();
            Hud = gameObject.AddComponent<BattleHud>();
            LoadStage(false);
        }
        void Update()
        {
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.mKey.wasPressedThisFrame) ToggleSound();
                if (Hud && Hud.HandleLibraryInput(k)) return;
                if (k.escapeKey.wasPressedThisFrame || k.pKey.wasPressedThisFrame) TogglePause();
                if (k.enterKey.wasPressedThisFrame)
                {
                    if (State == BattleState.Menu || State == BattleState.GameOver || State == BattleState.Victory) StartCampaign();
                    else if (State == BattleState.StageClear) NextStage();
                    else if (State == BattleState.Paused) TogglePause();
                }
                if (k.rKey.wasPressedThisFrame && (State == BattleState.GameOver || State == BattleState.Victory || State == BattleState.Paused)) StartCampaign();
            }
            if (State != BattleState.Playing) return;
            Enemies.RemoveAll(e => !e);
            if (Spawned < Total && Enemies.Count < EnemyLimit && Time.time >= nextSpawn) SpawnEnemy();
            if (waitingRespawn && Time.time >= respawnAt) SpawnPlayer();
            if (Kills >= Total && Enemies.Count == 0)
            {
                if (Stage == 3) Finish(true, "全部战区已肃清，指挥部安全");
                else { State = BattleState.StageClear; Time.timeScale = 0; BattleEffects.Tone(720, .25f); SaveBest(); }
            }
        }
        public void StartCampaign()
        {
            Stage = 1; Score = 0; Lives = 3; weaponLevel = 0;
            LoadStage(true);
        }
        public void NextStage() { if (State != BattleState.StageClear) return; Stage++; LoadStage(true); }
        void LoadStage(bool play)
        {
            Time.timeScale = 1;
            if (board) { board.gameObject.SetActive(false); Destroy(board.gameObject); }
            if (DynamicRoot) { DynamicRoot.gameObject.SetActive(false); Destroy(DynamicRoot.gameObject); }
            board = WorldBuilder.Build(Stage);
            DynamicRoot = new GameObject("Battle actors and effects").transform;
            Kills = 0; Spawned = 0; Enemies.Clear(); FrozenUntil = 0; waitingRespawn = false; ResultReason = "";
            State = play ? BattleState.Playing : BattleState.Menu;
            SpawnPlayer(); nextSpawn = Time.time + FirstSpawnDelay;
            if (!play)
            {
                for (int i = 0; i < 3; i++) CreateTank(false, new Vector3((i - 1) * 10, 0, 12), i, false);
            }
            Notify("战区 " + Stage.ToString("00") + "  /  守住指挥部");
        }
        void SpawnPlayer()
        {
            Physics.SyncTransforms();
            Vector3 pos = new(-4,0,-12);
            if (Physics.CheckBox(pos + Vector3.up * .6f, new Vector3(.67f,.45f,.67f), Quaternion.identity, 1 << WorldBuilder.TankLayer))
            { respawnAt = Time.time + .3f; waitingRespawn = true; return; }
            Player = CreateTank(true, pos, 0, false);
            Player.Upgrade = weaponLevel;
            Player.ShieldUntil = Time.time + RespawnProtectionSeconds;
            if(waitingRespawn && State==BattleState.Playing) Notify("重新部署 · 无敌保护 " + RespawnProtectionSeconds.ToString("F1") + " 秒");
            waitingRespawn = false;
        }
        void SpawnEnemy()
        {
            Physics.SyncTransforms();
            for (int attempt = 0; attempt < 3; attempt++)
            {
                int slot = (Spawned + attempt) % 3;
                Vector3 pos = new((slot - 1) * 10, 0, 12);
                if (Physics.CheckBox(pos + Vector3.up * .6f, new Vector3(.8f,.5f,.8f), Quaternion.identity, WorldBuilder.MovementMask)) continue;
                int type = Stage == 1 ? (Spawned < 4 ? 0 : (Spawned - 3) % 4) : (Spawned + Stage - 1) % 4;
                bool starterSupply = Stage == 1 && Spawned == 0;
                bool carrier = Stage == 1 ? starterSupply || Spawned == 7 || Spawned == 11 : Spawned % 4 == 3;
                var enemy = CreateTank(false, pos, type, carrier);
                if(starterSupply) enemy.FixedPickup = PickupKind.Star;
                enemy.ShieldUntil = Time.time + 1;
                Enemies.Add(enemy); Spawned++; nextSpawn = Time.time + EnemySpawnInterval;
                return;
            }
            nextSpawn = Time.time + .5f;
        }
        public TankActor CreateTank(bool player, Vector3 pos, int type, bool carrier)
        {
            var go = new GameObject(player ? "Player Tank" : "Enemy Tank " + type);
            go.layer = WorldBuilder.TankLayer; go.transform.SetParent(DynamicRoot); go.transform.position = pos;
            var actor = go.AddComponent<TankActor>(); actor.Configure(player, type, carrier);
            return actor;
        }
        public void TankDestroyed(TankActor tank)
        {
            if (State != BattleState.Playing) return;
            if (tank.IsPlayer)
            {
                Player = null; Lives--; weaponLevel = 0;
                if (Lives <= 0) Finish(false, "坦克已全部损失");
                else { waitingRespawn = true; respawnAt = Time.time + 1.4f; Notify("坦克受损 · 准备重新部署"); }
            }
            else
            {
                Enemies.Remove(tank); Kills++; Score += (tank.Type + 1) * 100;
                if (tank.Carrier) DropPickup(tank.FixedPickup);
            }
        }
        void DropPickup(PickupKind? fixedKind = null)
        {
            Physics.SyncTransforms();
            // Spawn on a verified open cell, never inside an obstacle or outside the arena.
            for (int i = 0; i < 100; i++)
            {
                Vector3 p = WorldBuilder.Cell(Random.Range(1,12), Random.Range(4,12));
                if (Physics.CheckBox(p + Vector3.up * .5f, new Vector3(.68f,.4f,.68f), Quaternion.identity, WorldBuilder.MovementMask)) continue;
                var go = new GameObject("Supply pickup"); go.transform.SetParent(DynamicRoot); go.transform.position = p;
                go.AddComponent<BattlePickup>().Configure(fixedKind ?? (PickupKind)Random.Range(0,5));
                return;
            }
        }
        public void ApplyPickup(PickupKind kind)
        {
            if (State != BattleState.Playing || !Player) return;
            Score += 50;
            switch (kind)
            {
                case PickupKind.Star: weaponLevel = Mathf.Min(2, Player.Upgrade + 1); Player.Upgrade = weaponLevel; Notify(weaponLevel == 2 ? "穿甲升级 · 可击穿钢墙" : "火力升级 · 射速提高"); break;
                case PickupKind.Shield: Player.ShieldUntil = Time.time + 10; Notify("能量护盾 · 10 秒保护"); break;
                case PickupKind.Bomb:
                    foreach (var e in Enemies.ToArray()) if(e) while(e.Health>0) e.Hit(true);
                    Notify("空中支援 · 清除当前敌军"); break;
                case PickupKind.Freeze: FrozenUntil = Time.time + 8; Notify("电磁干扰 · 敌军冻结 8 秒"); break;
                case PickupKind.Life: Lives++; Notify("增援抵达 · 坦克 +1"); break;
            }
            BattleEffects.Tone(920,.16f);
        }
        public void Notify(string message) { Notice = message; NoticeUntil = Time.unscaledTime + 3; }
        public void TogglePause()
        {
            if (State == BattleState.Playing) { State = BattleState.Paused; Time.timeScale = 0; }
            else if (State == BattleState.Paused) { State = BattleState.Playing; Time.timeScale = 1; }
        }
        public void ReturnToMenu() { Stage = 1; weaponLevel = 0; LoadStage(false); }
        public void ToggleSound() { Muted = !Muted; AudioListener.volume = Muted ? 0 : .65f; PlayerPrefs.SetInt("IronBastion.Muted", Muted ? 1 : 0); }
        public void Finish(bool win, string reason)
        {
            if (State != BattleState.Playing) return;
            State = win ? BattleState.Victory : BattleState.GameOver;
            ResultReason = reason; SaveBest(); Time.timeScale = 0;
            BattleEffects.Tone(win ? 800 : 110, .45f);
        }
        void SaveBest() { if (Score > Best) { Best = Score; PlayerPrefs.SetInt("IronBastion.Best", Best); PlayerPrefs.Save(); } }
        void OnDestroy() { Time.timeScale = 1; if (Instance == this) Instance = null; }
    }
}
