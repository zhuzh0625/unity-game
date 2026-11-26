using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Galleon
{
    /// <summary>Standalone acceptance driver for the fleet and deckhand checks.</summary>
    public sealed partial class FleetCheck : MonoBehaviour
    {
        const float Step = .05f;
        static int CannonLoadFrames => Mathf.CeilToInt(CrewPlayer.CannonLoadSeconds / Step) + 1;
        static int HullRepairFrames => Mathf.CeilToInt(CrewPlayer.HullRepairSeconds / Step) + 1;
        NavalGame game;
        NavalShip Ship => game.Pirate;
        CrewPlayer Crew => game.Crew;
        string output, report;
        ShipType type;
        Keyboard testKeyboard;
        public void Begin(NavalGame session, string directory, bool sailorOnly = false)
        {
            StartCheck(session, directory, sailorOnly ? "sailor-check" : "fleet-check",
                sailorOnly ? "Deckhand R06" : "Fleet", sailorOnly ? RunSailor() : Run());
        }

        public static void BeginBoarding(NavalGame session, string directory)
        {
            var check = session.gameObject.AddComponent<FleetCheck>();
            check.StartCheck(session, directory, "boarding-check", "R06 boarding", check.RunBoarding());
        }

        void StartCheck(NavalGame session, string directory, string filename, string title, IEnumerator suite)
        {
            game = session;
            output = Path.GetFullPath(directory);
            Directory.CreateDirectory(output);
            report = Path.Combine(output, filename + ".txt");
            File.WriteAllText(report, title + " standalone acceptance  " + DateTime.UtcNow.ToString("O") + Environment.NewLine);
            Time.captureDeltaTime = Step;
            StartCoroutine(Guard(suite));
        }

        IEnumerator Guard(IEnumerator suite)
        {
            // The desktop may focus another window while the standalone suite sends its E presses.
            var previousFocus = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testKeyboard = InputSystem.AddDevice<Keyboard>();
            var routines = new Stack<IEnumerator>();
            routines.Push(suite);
            string failure = null;
            while (routines.Count > 0)
            {
                object next;
                try
                {
                    var current = routines.Peek();
                    if (!current.MoveNext()) { routines.Pop(); continue; }
                    next = current.Current;
                }
                catch (Exception error) { failure = error.ToString(); break; }
                if (next is IEnumerator child) routines.Push(child);
                else yield return next;
            }
            foreach (var routine in routines) (routine as IDisposable)?.Dispose();
            Note(failure == null ? "RESULT PASS" : "RESULT FAIL " + failure);
            if (failure != null && Crew && !game.IsInMenu)
            {
                yield return new WaitForEndOfFrame();
                var image = ScreenCapture.CaptureScreenshotAsTexture();
                if (image)
                {
                    File.WriteAllBytes(Path.Combine(output, "failure-" + type + ".png"), image.EncodeToPNG());
                    Destroy(image);
                }
            }
            if (!game.IsInMenu) game.ReturnToMenu();
            InputSystem.RemoveDevice(testKeyboard);
            InputSystem.settings.backgroundBehavior = previousFocus;
            Time.captureDeltaTime = 0;
            Time.timeScale = 1;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(failure == null ? 0 : 3);
#endif
        }

        IEnumerator Run()
        {
            yield return null;
            Require(game.IsInMenu && !game.Pirate && !game.Crew, "starts at fleet selection");
            yield return KeyboardMenu();
            foreach (var choice in new[] { ShipType.Sloop, ShipType.Brigantine })
            {
                type = choice;
                Click(game.Menu.GetShipButton(type));
                Require(game.SelectedType == type && game.IsInMenu, "select existing model " + type);
                yield return Photograph("menu-" + type);
                Click(game.Menu.StartButton);
                Note("FIXTURE legacy route, cannon and flooding cases preserve the stationary target and manual repair measurements.");
                using var calibration = new AcceptanceCompanyFixture(Ship, game.Navy);
                yield return null;
                CheckVoyagePair();
                CheckNewVoyageGuns();
                Crew.ReadDeviceInput = false;
                string source = Small ? "Small_Sloop_R06.blend" : "Medium_Brigantine_R06.blend";
                foreach (var vessel in new[] { Ship, game.Navy })
                {
                    var data = vessel.Definition.Data;
                    Require(data.sourceModel == source && data.sourceSha256?.Length == 64,
                        $"{vessel.name} model provenance {data.sourceModel} sha256={data.sourceSha256}");
                }
                if (!Small)
                {
                    var glazing = Ship.Rig.transform.Find("ClearGlazing");
                    Require(glazing && glazing.TryGetComponent<MeshRenderer>(out var glass) && glass.enabled &&
                        glass.sharedMaterial && glass.sharedMaterial.renderQueue >= 3000,
                        "R06 window glazing uses a transparent material");
                }
                Require(Flag(Ship, "FlagsPirate") && !Flag(Ship, "FlagsBritish") &&
                    Flag(game.Navy, "FlagsBritish") && !Flag(game.Navy, "FlagsPirate"), "source meshes show their faction flags");
                yield return Frames(3);
                yield return Photograph("voyage-" + type);
                yield return HelmAndWalk();
                if (!Small)
                {
                    yield return Photograph("gun-deck-Brigantine");
                    yield return Gunport("下层左舷炮饰 04", "gunport-Brigantine-port");
                    yield return Gunport("下层右舷炮饰 04", "gunport-Brigantine-starboard");
                }
                yield return Cannon();
                yield return SailAndAnchor();
                yield return DamageControl();
                yield return Defeat();
                Click(game.GetComponent<VoyageHud>().DefeatMenuButton);
                yield return null;
                Require(game.IsInMenu && !Ship && !game.Crew && game.SelectedType == type, "defeat button returns to menu with selection");
                Click(game.Menu.StartButton);
                yield return null;
                CheckVoyagePair();
                var old = Ship;
                Require(old && old.Definition.Type == type && old.Flooding.ActiveLeaks == 0 && old.Anchored && old.SailStep == 0,
                    "fresh same-type voyage from menu starts anchored with furled sails");
                Crew.ReadDeviceInput = false;
                Note("FIXTURE repeated-impact calibration preserves each breach until its measured manual repair.");
                using (new AcceptanceCompanyFixture(Ship, game.Navy))
                    yield return RepeatedImpact();
                yield return KeyTap(Key.F8);
                Require(Ship && Ship != old && Ship.Definition.Type == type && Ship.Flooding.WaterLevel01 == 0, "F8 restarts selected ship");
                CheckVoyagePair();
                CheckNewVoyageGuns();
                game.SetPaused(true);
                yield return null;
                Click(game.GetComponent<VoyageHud>().PauseMenuButton);
                yield return null;
                Require(game.IsInMenu && Time.timeScale == 1, "pause button returns to selectable fleet");
            }
        }

        IEnumerator KeyboardMenu()
        {
            type = ShipType.Sloop;
            var focused = EventSystem.current;
            var choices = game.Menu.GetShipButton(type).transform.parent.GetComponentsInChildren<Button>(true);
            Require(choices.Length == 2 && Array.Exists(choices, b => b == game.Menu.GetShipButton(ShipType.Sloop)) &&
                Array.Exists(choices, b => b == game.Menu.GetShipButton(ShipType.Brigantine)), "menu contains exactly Sloop and Brigantine");
            Require(game.SelectedType == type && focused.currentSelectedGameObject == game.Menu.GetShipButton(type).gameObject,
                "default selection and keyboard focus belong to the sloop");
            yield return KeyTap(Key.DownArrow);
            yield return KeyTap(Key.Enter);
            Require(game.IsInMenu && game.SelectedType == ShipType.Brigantine,
                "down arrow and Enter select the brigantine");
            yield return KeyTap(Key.UpArrow);
            yield return KeyTap(Key.Enter);
            Require(game.SelectedType == ShipType.Sloop, "keyboard switches the preview back to the sloop");
            yield return KeyTap(Key.UpArrow);
            Require(focused.currentSelectedGameObject == game.Menu.StartButton.gameObject,
                "keyboard navigation reaches Start Voyage");
            yield return KeyTap(Key.Enter);
            CheckVoyagePair();
            Crew.ReadDeviceInput = false;
            yield return Gunport("P2");
            yield return KeyTap(Key.Escape);
            var button = game.GetComponent<VoyageHud>().PauseMenuButton;
            Require(Time.timeScale == 0 && button.isActiveAndEnabled, "Escape opens the pause menu");
            Click(button);
            yield return null;
            Require(game.IsInMenu && !Ship, "returns from sloop to fleet selection");
        }

        IEnumerator KeyTap(Key key)
        {
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
            yield return null;
        }

        bool Small => type == ShipType.Sloop;

        void CheckVoyagePair()
        {
            var navy = game.Navy;
            Require(!game.IsInMenu && !game.IsCombatTest && game.SelectedType == type &&
                Ship && Crew && navy && Ship != navy && Ship.IsPirate && !navy.IsPirate,
                "ordinary voyage started from the fleet selection with both factions");
            int guns = Small ? 8 : 18;
            int crewCount = Small ? 6 : 12;
            foreach (var vessel in new[] { Ship, navy })
            {
                int actualCrew = vessel.GetComponentsInChildren<CrewDamageTarget>().Length;
                Require(vessel.Definition.Type == type && vessel.Rig.Guns.Length == guns &&
                    vessel.Company && vessel.Company.Capacity == crewCount && actualCrew == crewCount,
                    $"{vessel.name} spawned as {vessel.Definition.Type} with {vessel.Rig.Guns.Length}/{guns} gun stations and {actualCrew}/{crewCount} crew");
            }
            Require(Ship.Company.Captain == Crew && navy.Company.Captain is CrewMate captain &&
                captain.Duty == CrewDuty.Captain && captain.transform.parent == navy.transform &&
                navy.Company.CanSteer(captain),
                "player is pirate captain and the navy has its own AI captain with helm authority");
        }

        void CheckNewVoyageGuns()
        {
            foreach (var vessel in new[] { Ship, game.Navy })
            {
                int loaded = 0;
                for (int gun = 0; gun < vessel.Rig.Guns.Length; gun++)
                    if (vessel.IsLoaded(gun)) loaded++;
                Require(vessel.Rig.Guns.Length > 0 && loaded == 0,
                    $"{vessel.name} starts with {loaded}/{vessel.Rig.Guns.Length} cannons loaded");
            }
        }
        void Note(string message) => File.AppendAllText(report, message + Environment.NewLine);
        void Require(bool ok, string evidence)
        {
            if (!ok) throw new InvalidOperationException(type + " " + evidence);
            Note("PASS " + type + " " + evidence);
        }

        void Click(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            var target = hits.Count > 0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) : null;
            Require(target == button.gameObject && button.IsInteractable(),
                $"uGUI screen hit {button.name} at {pointer.position} top={(hits.Count > 0 ? hits[0].gameObject.name : "none")}");
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
        }

        IEnumerator Photograph(string label)
        {
            // Screen capture includes the screen-space overlay as well as the ship camera.
            yield return new WaitForEndOfFrame();
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            if (!image) throw new InvalidOperationException("GPU capture failed: " + label);
            var colors = new HashSet<Color32>();
            var pixels = image.GetPixels32();
            for (int i = 0; i < pixels.Length; i += 283) colors.Add(pixels[i]);
            Require(colors.Count > 30, $"rendered {label} {image.width}x{image.height} sampled colors={colors.Count}");
            File.WriteAllBytes(Path.Combine(output, label + ".png"), image.EncodeToPNG());
            Destroy(image);
        }

        static bool Flag(NavalShip ship, string label)
        {
            var node = ship.Rig.transform.Find(label);
            if (!node || !node.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh || filter.sharedMesh.vertexCount < 3)
                throw new InvalidOperationException("Missing source flag " + label);
            return node.gameObject.activeInHierarchy;
        }

        void Tick(CrewCommand command = default) => Crew.Tick(command, Step);
        IEnumerator Frames(int count, CrewCommand command = default)
        {
            for (int i = 0; i < count; i++) { Tick(command); yield return null; }
        }
        void Enter(string station)
        {
            var hint = Crew.Prompt;
            Tick(new CrewCommand { UsePressed = true });
            Require(Crew.StationName == station, $"E enters {station}; hint={hint} actual={Crew.StationName} foot={Crew.transform.localPosition:F2}");
        }
        void Leave()
        {
            if (Crew.StationName.Length != 0) Tick(new CrewCommand { UsePressed = true });
        }

        IEnumerator HelmAndWalk()
        {
            Enter("舵轮");
            yield return Frames(10, new CrewCommand { Move = Vector2.right });
            var wheel = Ship.Rig.transform.Find("Wheel");
            Require(Ship.Rudder > .2f && Quaternion.Angle(wheel.localRotation, Quaternion.identity) > 5, "wheel and rudder turn");
            Leave();
            yield return HoldRouteCheck.Walk(game, (ok, label, detail) => Require(ok, label + " " + detail));
            yield return HoldRouteCheck.WalkRepairSites(game, (ok, label, detail) => Require(ok, label + " " + detail));
        }

        void Fixture(Vector3 local)
        {
            Leave();
            var controller = Crew.GetComponent<CharacterController>();
            controller.enabled = false;
            Crew.transform.position = Ship.transform.TransformPoint(local + Vector3.up*.026f);
            controller.enabled = true;
            Physics.SyncTransforms();
            Tick();
            Note("FIXTURE independent station case, crew foot " + Crew.transform.localPosition.ToString("F2"));
        }

        int GunIndex(string name)
        {
            int index = Array.FindIndex(Ship.Rig.Guns, gun => gun && gun.name == name);
            Require(index >= 0, "source gun marker " + name);
            return index;
        }

        IEnumerator UseKey(Key key = Key.E)
        {
            Crew.ReadDeviceInput = true;
            yield return KeyTap(key);
            Crew.ReadDeviceInput = false;
            yield return new WaitForEndOfFrame();
        }

        void ApproachGun(Transform gun)
        {
            var foot = Ship.transform.InverseTransformPoint(gun.position - gun.forward * (Small ? .32f : .55f));
            foot.y = Small ? gun.localPosition.y - .28f : Ship.Definition.Data.lowerDeck;
            Fixture(foot);
        }

        bool ReticleVisible => game.GetComponent<VoyageHud>().IsGunSightVisible;

        void CheckGunView(Transform gun, float yaw, float pitch)
        {
            var camera = game.MainCamera;
            Vector3 ray = NavalShip.ShotDirection(gun, yaw, pitch).normalized;
            Vector3 offset = camera.transform.position - gun.position;
            Vector3 eye = Ship.Rig.transform.InverseTransformPoint(camera.transform.position);
            float clearance = Mathf.Abs(eye.x) - Ship.Definition.HalfBreadth(eye.z, eye.y);
            float angle = Vector3.Angle(camera.transform.forward, ray);
            float miss = Vector3.Cross(offset, ray).magnitude;
            var renderers = Crew.GetComponentsInChildren<Renderer>(true);
            bool sailorDrawn = Array.Exists(renderers, r => r.enabled && r.gameObject.activeInHierarchy && !r.forceRenderingOff &&
                (camera.cullingMask & (1 << r.gameObject.layer)) != 0 && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly);
            Require(Crew.IsAiming && ReticleVisible && Mathf.Abs(Crew.GunYaw - yaw) < .08f && Mathf.Abs(Crew.GunPitch - pitch) < .08f &&
                    angle < .12f && miss < .02f && Vector3.Dot(offset, ray) > 0 && clearance > camera.nearClipPlane &&
                    renderers.Length > 0 && !sailorDrawn,
                $"{gun.name} rendered sight yaw={Crew.GunYaw:F1} pitch={Crew.GunPitch:F1} error={angle:F3}° lateral={miss:F3}m outside={clearance:F3}m sailorDrawn={sailorDrawn} reticle={ReticleVisible}");
        }

        void CheckOrbit(Vector3 priorHeading)
        {
            var camera = game.MainCamera.transform;
            Vector3 pivot = Crew.transform.position + Ship.transform.up * 1.34f;
            Vector3 heading = Ship.transform.InverseTransformDirection(camera.forward);
            Require(!Crew.IsAiming && Crew.StationName.Length == 0 && !ReticleVisible &&
                    Vector3.Dot(camera.position - pivot, camera.forward) < -.02f && Vector3.Angle(heading, priorHeading) < .2f,
                $"third-person orbit restored at crew, reticle={ReticleVisible} heading error={Vector3.Angle(heading, priorHeading):F3}°");
        }

        IEnumerator Gunport(string name, string photograph = null)
        {
            var gun = Ship.Rig.Guns[GunIndex(name)];
            ApproachGun(gun);
            yield return new WaitForEndOfFrame();
            Vector3 orbit = Ship.transform.InverseTransformDirection(game.MainCamera.transform.forward);
            string side = Vector3.Dot(gun.forward, Ship.transform.right) > 0 ? "右舷炮" : "左舷炮";
            yield return UseKey();
            Require(Crew.StationName == side, $"keyboard E entered {side} at {name}");
            CheckGunView(gun, 0f, 4f);
            Tick(new CrewCommand { Look = new Vector2(90f, -70f) });
            yield return new WaitForEndOfFrame();
            CheckGunView(gun, 9f, -1.95f);
            if (photograph != null) yield return Photograph(photograph);
            foreach (float sign in new[] { 1f, -1f })
            {
                Tick(new CrewCommand { Look = new Vector2(sign * 1000f, sign * 1000f) });
                yield return new WaitForEndOfFrame();
                CheckGunView(gun, sign * 24f, sign > 0 ? 26f : -5f);
            }
            if (name == "下层右舷炮饰 04")
            {
                yield return KeyTap(Key.Escape);
                yield return new WaitForEndOfFrame();
                Require(Time.timeScale == 0 && Crew.IsAiming && !ReticleVisible, "Escape hides the occupied gun sight");
                yield return KeyTap(Key.Escape);
                yield return new WaitForEndOfFrame();
                Require(Time.timeScale > 0 && Crew.IsAiming && ReticleVisible, "resume restores the gun sight");
            }
            yield return UseKey();
            CheckOrbit(orbit);
        }

        int ApproachWeatherGun()
        {
            var guns = Ship.Rig.Guns;
            int index = -1;
            float score = float.MaxValue;
            for (int i = 0; i < guns.Length; i++)
            {
                var g = guns[i];
                var local = Ship.transform.InverseTransformPoint(g.position);
                if (Vector3.Dot(g.forward,Ship.transform.right)<.9f) continue;
                float cost = Mathf.Abs(local.z - Ship.Definition.Length*.50f) - .30f*local.y;
                if (cost < score && (Small || local.y>4.15f)) { score=cost; index=i; }
            }
            Require(index>=0,"starboard weather-deck gun available");
            var gun=guns[index];
            var foot=Ship.transform.InverseTransformPoint(gun.position - gun.forward*(Small?.32f:.64f));
            foot.y=Small?gun.localPosition.y-.28f:Ship.Definition.Data.mainDeck;
            Fixture(foot);
            return index;
        }

        IEnumerator Cannon()
        {
            int index = ApproachWeatherGun();
            var gun = Ship.Rig.Guns[index];
            yield return Frames(3);
            Enter("右舷炮");
            yield return Frames(CannonLoadFrames);
            Require(Ship.IsLoaded(index) && Crew.IsAiming,"selected gun loaded through crew action " + gun.name);
            yield return new WaitForEndOfFrame();
            CheckGunView(gun, 0f, 4f);
            var navy = game.Navy;
            float targetZ = navy.Definition.Length * .5f;
            float targetY = Mathf.Lerp(navy.Definition.DryDraft, navy.Definition.Data.mainDeck, .55f);
            var target = navy.transform.TransformPoint(navy.Definition.HalfBreadth(targetZ, targetY), targetY, targetZ);
            Vector3 offset = target - gun.position;
            float flight = Mathf.Max(0f, Vector3.ProjectOnPlane(offset, Vector3.up).magnitude - NavalShip.ShotSpawnOffset) / NavalShip.ShotSpeed;
            Vector3 aim = gun.InverseTransformDirection(offset + Vector3.up * (.5f * NavalShip.ShotDrop * flight * flight));
            float yaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg;
            float pitch=Mathf.Atan2(aim.y,new Vector2(aim.x,aim.z).magnitude)*Mathf.Rad2Deg;
            Require(navy.Anchored && navy.Speed < .01f && Mathf.Abs(yaw) <= 24f && pitch >= -5f && pitch <= 26f,
                $"stationary target is within the calibrated gun arc: anchor={navy.Anchored} speed={navy.Speed:F2} yaw={yaw:F1} pitch={pitch:F1}");
            Tick(new CrewCommand {Look=new Vector2(yaw/.10f,(pitch-Crew.GunPitch)/.085f)});
            yield return UseKey(Key.Space);
            Require(Crew.IsAiming && !Crew.IsJumping && !Ship.IsLoaded(index) && FindAnyObjectByType<RoundShot>(),
                $"Space at occupied gun fired projectile pitch={pitch:F1} yaw={yaw:F1}");
            yield return Frames(33);
            Require(Ship.HitsLanded==1 && navy.Flooding.ActiveLeaks>0 && navy.Flooding.ActiveDecals>=2,
                $"shot damaged navy hull on both faces: hits={Ship.HitsLanded} leaks={navy.Flooding.ActiveLeaks} decals={navy.Flooding.ActiveDecals}");
            Leave();
        }

        IEnumerator SailAndAnchor()
        {
            var helm = Ship.Rig.Helm;
            Require(CrewStationFit.TryGetPose(Ship, CrewPlayer.Role.Helm, -1, helm, Crew.transform.position,
                out var helmFoot, out _), "authored helm stance for independent sailing case");
            Fixture(Ship.transform.InverseTransformPoint(helmFoot));
            yield return Frames(3);
            Enter("舵轮");
            var canvas = Ship.Rig.transform.Find("SailJib");
            Require(Ship.Anchored && Ship.SailStep == 0 && canvas && canvas.localScale.y < .15f,
                "sailing case begins anchored with furled canvas");
            yield return UseKey(Key.Space);
            Require(Ship.Anchored && Ship.SailStep == 0 && !Crew.IsJumping && Crew.StationName == "舵轮",
                "Space at helm leaves a moored ship at anchor");
            var before = Ship.transform.position;
            yield return UseKey(Key.W);
            yield return Frames(19);
            Require(!Ship.Anchored && Ship.SailStep == 1 && canvas.localScale.y > .45f && canvas.localScale.y < .7f,
                "first W at helm weighs anchor and opens half canvas");
            yield return UseKey(Key.W);
            yield return Frames(19);
            Require(!Ship.Anchored && Ship.SailStep == 2 && canvas.localScale.y > .98f,
                "second W at helm opens full canvas");
            yield return UseKey(Key.Space);
            yield return Frames(10);
            Require(!Ship.Anchored && Ship.SailStep == 2 && !Crew.IsJumping && Crew.StationName == "舵轮" &&
                Ship.Speed > .4f && Vector3.Distance(before, Ship.transform.position) > .6f,
                "Space at helm leaves full sail and forward motion unchanged");
            yield return UseKey(Key.S);
            yield return Frames(19);
            Require(!Ship.Anchored && Ship.SailStep == 1 && canvas.localScale.y > .45f && canvas.localScale.y < .7f,
                "first S at helm reefs to half canvas while underway");
            yield return UseKey(Key.S);
            yield return Frames(45);
            Require(Ship.Anchored && Ship.SailStep == 0 && canvas.localScale.y < .15f && Ship.Speed < .05f,
                "second S at helm furls canvas, drops anchor and stops");
            Leave();
        }

        IEnumerator DamageControl()
        {
            var water=Ship.Flooding;
            yield return KeyTap(Key.F6);
            yield return Frames(8);
            Require(water.ActiveLeaks==2 && water.ActiveDecals==4 && water.WaterHeight>water.DryHeight && water.WaterVisible,
                "training breaches flash on timber and raise water");
            foreach (int index in new[] {0,Ship.Rig.LeakInside.Length-1})
            {
                yield return ApproachRepairSite(index);
                Enter("船壳破口");
                yield return Frames(HullRepairFrames);
                Require(!Crew.IsSwimming && !water.IsLeaking(index) && Crew.StationName.Length==0,"repaired source-facing breach " + index);
            }
            Require(water.ActiveDecals==0 && water.ActiveLeaks==0 && water.WaterHeight>water.DryHeight,"repairs stop ingress with residual water");
            ApproachPump();
            yield return Frames(2);
            Enter("舱底泵");
            float original=water.WaterHeight;
            yield return Frames(4);
            var handle=Ship.Rig.transform.Find("PumpLever0");
            Require(handle && Quaternion.Angle(handle.localRotation, Quaternion.identity)>3f && water.WaterHeight<original,
                "pump lever animates while removing residual water");
            for (int i=0;i<600 && water.WaterHeight>water.DryHeight;i++) yield return Frames(1);
            Require(!Crew.IsSwimming && water.WaterLevel01==0 && !water.WaterVisible && Crew.StationName.Length==0, "bilge pumped dry");
        }

        void ApproachPump() => Fixture(Small ? new Vector3(0, 2.86f, 11f) : new Vector3(.77f, Ship.Definition.Data.lowerDeck, 9.13f));

        IEnumerator Defeat()
        {
            var water=Ship.Flooding;
            int low = GunIndex(Small ? "P2" : "下层右舷炮饰 04");
            var gun = Ship.Rig.Guns[low];
            ApproachGun(gun);
            yield return new WaitForEndOfFrame();
            Vector3 orbit = Ship.transform.InverseTransformDirection(game.MainCamera.transform.forward);
            Ship.Load(low);
            yield return UseKey();
            Require(Ship.IsLoaded(low), "low gun primed for submergence test (independent setup)");
            CheckGunView(gun, 0f, 4f);
            game.StartDamageDrill();
            Time.timeScale=12;
            for(int i=0;i<2200 && !Ship.IsGunSubmerged(low) && !water.IsSunk;i++)
            {
                Tick();
                yield return null;
            }
            Require(Crew.IsAiming && Ship.IsGunSubmerged(low) && !water.IsSunk && !Ship.Fire(low,0,0),"occupied gun submerged before defeat and cannot fire");
            Tick();
            Require(Crew.Prompt.Contains("已被淹没"), "flooding automatically releases the gun " + gun.name);
            yield return new WaitForEndOfFrame();
            CheckOrbit(orbit);
            for(int i=0;i<1600 && !water.IsSunk;i++) yield return null;
            Require(water.IsSunk && game.IsGameOver && game.GetComponent<VoyageHud>().IsDefeatVisible &&
                    Mathf.Abs(water.WaterHeight-water.SinkingHeight)<.002f,
                $"sank at measured deck crown + 0.10m; level={water.WaterHeight:F2}");
        }
    }
}
