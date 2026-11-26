using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Galleon
{
    public sealed class NavalGame : MonoBehaviour
    {
        public NavalShip Pirate { get; private set; }
        public NavalShip Navy { get; private set; }
        public CrewPlayer Crew { get; private set; }
        readonly List<CombatDummy> testDummies = new();
        public IReadOnlyList<CombatDummy> TestDummies => testDummies;
        public bool IsCombatTest { get; private set; }
        public Camera MainCamera { get; private set; }
        public bool IsGameOver { get; private set; }
        public bool WonVoyage { get; private set; }
        public bool IsInMenu => Menu && Menu.gameObject.activeSelf;
        public FleetMenu Menu { get; private set; }
        public ShipType SelectedType { get; private set; } = ShipType.Sloop;
        NavalWorld world;
        NavalAudio sound;
        VoyageHud hud;
        bool helpOpen;
        readonly Dictionary<CrewDamageTarget, bool> friendlyCrew = new();
        bool verify;

        void Start()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            MainCamera = Camera.main;
            if (!MainCamera.TryGetComponent<AudioListener>(out _)) MainCamera.gameObject.AddComponent<AudioListener>();
            sound = gameObject.AddComponent<NavalAudio>();
            world = gameObject.AddComponent<NavalWorld>();
            world.Initialize(MainCamera, RenderSettings.sun);
            if (!EventSystem.current)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().deselectOnBackgroundClick = false;
            }

            var menuObject = new GameObject("FleetMenu", typeof(RectTransform));
            menuObject.transform.SetParent(transform, false);
            Menu = menuObject.AddComponent<FleetMenu>();
            Menu.Initialize(this);
            hud = gameObject.AddComponent<VoyageHud>();
            hud.Initialize(this);
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-naval-check");
            int waterCheck = Array.IndexOf(args, "-naval-water-check");
            int fleetCheck = Array.IndexOf(args, "-fleet-check");
            int sailorCheck = Array.IndexOf(args, "-sailor-check");
            int boardingCheck = Array.IndexOf(args, "-boarding-check");
            int lifeCheck = Array.IndexOf(args, "-life-check");
            int combatCheck = Array.IndexOf(args, "-combat-check");
            int medicineCheck = Array.IndexOf(args, "-medicine-check");
            int cannonImpactCheck = Array.IndexOf(args, "-cannon-impact-check");
            int floodedFireCheck = Array.IndexOf(args, "-flooded-fire-check");
            int killFeedCheck = Array.IndexOf(args, "-kill-feed-check");
            int helmSailCheck = Array.IndexOf(args, "-helm-sail-check");
            int companyCheck = Array.IndexOf(args, "-company-check");
            int contactCheck = Array.IndexOf(args, "-hull-contact-check");
            int audioCheck = Array.IndexOf(args, "-audio-check");
            int liningCheck = Array.IndexOf(args, "-lining-check");
            if (option >= 0 && option + 1 < args.Length)
            {
                verify = true;
                SelectedType = ShipType.Brigantine;
                StartVoyage();
                gameObject.AddComponent<VoyageCheck>().Begin(this, args[option + 1]);
            }
            else if (waterCheck >= 0 && waterCheck + 1 < args.Length)
            {
                verify = true;
                SelectedType = ShipType.Brigantine;
                StartVoyage();
                gameObject.AddComponent<HoldWaterViewCheck>().Begin(this, args[waterCheck + 1]);
            }
            else if (liningCheck >= 0 && liningCheck + 1 < args.Length)
            {
                verify = true;
                SelectedType = ShipType.Brigantine;
                StartVoyage();
                gameObject.AddComponent<HullLiningCheck>().Begin(this, args[liningCheck + 1]);
            }
            else
            {
                ReturnToMenu();
                if (fleetCheck >= 0 && fleetCheck + 1 < args.Length)
                    gameObject.AddComponent<FleetCheck>().Begin(this, args[fleetCheck + 1]);
                else if (sailorCheck >= 0 && sailorCheck + 1 < args.Length)
                    gameObject.AddComponent<FleetCheck>().Begin(this, args[sailorCheck + 1], sailorOnly: true);
                else if (boardingCheck >= 0 && boardingCheck + 1 < args.Length)
                    FleetCheck.BeginBoarding(this, args[boardingCheck + 1]);
                else if (lifeCheck >= 0 && lifeCheck + 1 < args.Length)
                    FleetCheck.BeginLife(this, args[lifeCheck + 1]);
                else if (combatCheck >= 0 && combatCheck + 1 < args.Length)
                    FleetCheck.BeginCombat(this, args[combatCheck + 1]);
                else if (medicineCheck >= 0 && medicineCheck + 1 < args.Length)
                    FleetCheck.BeginMedicine(this, args[medicineCheck + 1]);
                else if (cannonImpactCheck >= 0 && cannonImpactCheck + 1 < args.Length)
                    FleetCheck.BeginCannonImpact(this, args[cannonImpactCheck + 1]);
                else if (floodedFireCheck >= 0 && floodedFireCheck + 1 < args.Length)
                    FleetCheck.BeginFloodedFire(this, args[floodedFireCheck + 1]);
                else if (killFeedCheck >= 0 && killFeedCheck + 1 < args.Length)
                    FleetCheck.BeginKillFeed(this, args[killFeedCheck + 1]);
                else if (helmSailCheck >= 0 && helmSailCheck + 1 < args.Length)
                    FleetCheck.BeginHelmSail(this, args[helmSailCheck + 1]);
                else if (companyCheck >= 0 && companyCheck + 1 < args.Length)
                    FleetCheck.BeginCompany(this, args[companyCheck + 1]);
                else if (contactCheck >= 0 && contactCheck + 1 < args.Length)
                    FleetCheck.BeginHullContact(this, args[contactCheck + 1]);
                else if (audioCheck >= 0 && audioCheck + 1 < args.Length)
                    gameObject.AddComponent<NavalAudioCheck>().Begin(this, args[audioCheck + 1]);
            }
        }

        internal void SelectShip(ShipType type)
        {
            SelectedType = type;
            Menu.ShowShip(type);
        }

        public void StartVoyage() => StartSession(false);
        public void StartCombatTest() => StartSession(true);

        void StartSession(bool combatTest)
        {
            ClearVoyage();
            Menu.Hide();
            IsCombatTest = combatTest;
            ShipType type = combatTest ? ShipType.Brigantine : SelectedType;
            Pirate = CreateShip("海盗船", true, new Vector3(0, 0, 15), 0, type);
            if (combatTest)
            {
                float deck = Pirate.Definition.Data.mainDeck;
                Pirate.Rig.Spawn.localPosition = new Vector3(0f, deck, 8.85f);
                testDummies.Add(CombatDummy.Create(Pirate.transform, "左侧假人", new Vector3(-1.80f, deck, 12.10f)));
                testDummies.Add(CombatDummy.Create(Pirate.transform, "右侧假人", new Vector3(1.80f, deck, 12.10f)));
            }
            else
                Navy = CreateShip("海军战舰", false, new Vector3(40, 0, 16), 180, type);
            world.Pirate = Pirate;
            world.Navy = Navy;
            Crew = new GameObject("玩家船长").AddComponent<CrewPlayer>();
            Crew.Initialize(Pirate, MainCamera, world);
            if (Navy)
            {
                Pirate.SetContactPartner(Navy);
                Pirate.Company = Pirate.gameObject.AddComponent<ShipCompany>();
                Navy.Company = Navy.gameObject.AddComponent<ShipCompany>();
                Pirate.Company.Initialize(Pirate, Navy, Crew);
                Navy.Company.Initialize(Navy, Pirate, null);
                foreach (var mate in Pirate.Company.Mates) WatchCrew(mate, true);
                foreach (var mate in Navy.Company.Mates) WatchCrew(mate, false);
                Navy.Flooding.Sunk += WinVoyage;
            }
            WatchCrew(Crew, true);
            foreach (var dummy in testDummies) WatchCrew(dummy, false);
            hud.BeginVoyage(Pirate.Definition.DisplayName);
            Pirate.Flooding.Sunk += EndVoyage;
            sound.BeginVoyage(Pirate);
        }

        void WatchCrew(CrewDamageTarget member, bool friendly)
        {
            friendlyCrew.Add(member, friendly);
            member.Killed += RecordKill;
        }

        void RecordKill(CrewKill kill)
        {
            if (!IsGameOver && friendlyCrew.TryGetValue(kill.Attacker, out bool attackerFriendly) &&
                friendlyCrew.TryGetValue(kill.Victim, out bool victimFriendly))
                hud.ShowKill(kill, attackerFriendly, victimFriendly);
        }

        void StopWatchingCrew()
        {
            foreach (var member in friendlyCrew.Keys)
                if (member) member.Killed -= RecordKill;
            friendlyCrew.Clear();
        }

        public void ReturnToMenu()
        {
            ClearVoyage();
            hud.Hide();
            Menu.Show(SelectedType);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void ClearVoyage()
        {
            sound.EndVoyage();
            Time.timeScale = 1;
            helpOpen = false;
            IsGameOver = false;
            WonVoyage = false;
            IsCombatTest = false;
            StopWatchingCrew();
            testDummies.Clear();
            world.Pirate = world.Navy = null;
            if (Crew) Retire(Crew.gameObject);
            if (Pirate)
            {
                Pirate.Flooding.Sunk -= EndVoyage;
                foreach (var shot in FindObjectsByType<RoundShot>(FindObjectsSortMode.None))
                    Retire(shot.gameObject);
                Retire(Pirate.gameObject);
            }
            if (Navy)
            {
                Navy.Flooding.Sunk -= WinVoyage;
                Retire(Navy.gameObject);
            }
            Crew = null;
            Pirate = Navy = null;
        }

        static void Retire(GameObject item)
        {
            item.SetActive(false);
            Destroy(item);
        }

        public void StartDamageDrill()
        {
            if (!Pirate || IsGameOver) return;
            Pirate.Flooding.StartDrill();
        }

        NavalShip CreateShip(string label, bool pirate, Vector3 centre, float heading, ShipType type)
        {
            var ship = new GameObject(label).AddComponent<NavalShip>();
            ship.transform.SetParent(transform, false);
            ship.Initialize(pirate, centre, heading, type);
            return ship;
        }

        void EndVoyage() => FinishVoyage(false);
        void WinVoyage() => FinishVoyage(true);

        void FinishVoyage(bool won)
        {
            if (IsGameOver) return;
            IsGameOver = true;
            WonVoyage = won;
            sound.EndVoyage();
            Crew.EndVoyage(won ? Navy : Pirate);
            helpOpen = false;
            hud.ShowOutcome(won);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void SetPaused(bool paused)
        {
            if (!Crew || IsGameOver) return;
            helpOpen = paused;
            sound.SetPaused(paused);
            Time.timeScale = paused ? 0 : 1;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
            hud.SetHelp(paused);
        }

        void Update()
        {
            if (!Crew || verify) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f8Key.wasPressedThisFrame)
            {
                StartSession(IsCombatTest);
                return;
            }
            if (IsGameOver) return;
            if (keyboard.f1Key.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                SetPaused(!helpOpen);
            if (helpOpen) return;
            if (keyboard.f6Key.wasPressedThisFrame) StartDamageDrill();
        }

        void OnDestroy()
        {
            if (Pirate && Pirate.Flooding) Pirate.Flooding.Sunk -= EndVoyage;
            if (Navy && Navy.Flooding) Navy.Flooding.Sunk -= WinVoyage;
            StopWatchingCrew();
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
