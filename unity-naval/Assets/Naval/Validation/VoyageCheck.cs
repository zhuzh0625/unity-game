using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Galleon
{
    // Opt-in standalone acceptance run. Activated exclusively by NavalGame's -naval-check argument.
    public sealed class VoyageCheck : MonoBehaviour
    {
        private const float Step = 1f / 30f;
        private static int CannonLoadFrames => Mathf.RoundToInt(5f / Step);
        private static int HullRepairFrames => Mathf.RoundToInt(3f / Step);
        private NavalGame game;
        private NavalShip ship;
        private CrewPlayer crew;
        private string output, receipt;
        private int passed, failed;
        private RenderTexture capture;
        private Texture2D readback;
        private RenderTexture oldTarget;
        private float oldCaptureStep;
        private int oldFrameRate;
        private bool began;
        private AcceptanceCompanyFixture companyFixture;
        private HullFlooding fixedTargetFlooding;

        public void Begin(NavalGame session, string directory)
        {
            if (began) return;
            began = true;
            game = session;
            ship = game.Pirate;
            crew = game.Crew;
            output = Path.GetFullPath(directory);
            Directory.CreateDirectory(output);
            receipt = Path.Combine(output, "voyage-check.txt");
            File.WriteAllText(receipt, "Brigantine crew acceptance | " + DateTime.UtcNow.ToString("O") + Environment.NewLine);
            oldCaptureStep = Time.captureDeltaTime;
            oldFrameRate = Application.targetFrameRate;
            companyFixture = new AcceptanceCompanyFixture(game.Pirate, game.Navy);
            Note("FIXTURE manual station and flooding calibration suspends crew orders and holds the cannon target at its launch position.");
            Time.captureDeltaTime = Step;
            Application.targetFrameRate = 60;
            crew.ReadDeviceInput = false;
            StartCoroutine(Guard(Run()));
        }

        private IEnumerator Guard(IEnumerator body)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(body);
            while (stack.Count > 0)
            {
                object next = null;
                try
                {
                    IEnumerator routine = stack.Peek();
                    if (!routine.MoveNext()) { stack.Pop(); continue; }
                    next = routine.Current;
                }
                catch (Exception error)
                {
                    Check(false, "unhandled acceptance exception", error.ToString());
                    break;
                }
                if (next is IEnumerator child) stack.Push(child);
                else yield return next;
            }
            game.MainCamera.targetTexture = oldTarget;
            if (readback) Destroy(readback);
            if (capture) { capture.Release(); Destroy(capture); }
            crew.ReadDeviceInput = true;
            companyFixture.Dispose();
            if (fixedTargetFlooding) fixedTargetFlooding.enabled = true;
            Time.captureDeltaTime = oldCaptureStep;
            Application.targetFrameRate = oldFrameRate;
            Note("RESULT " + (failed == 0 ? "PASS" : "FAIL") + " passed=" + passed + " failed=" + failed);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(failed == 0 ? 0 : 3);
#endif
        }

        private IEnumerator Run()
        {
            Note("Real runtime; CrewCommand goes through CrewPlayer.Tick with native collision, proximity, station state and LateUpdate camera. The driver places the sailor between independent cases; no ship pose, water or projectile position is injected.");
            yield return null;
            var cam = game.MainCamera;
            oldTarget = cam.targetTexture;
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "00-quarterdeck-hud.png"));
            yield return Frames(3);
            capture = new RenderTexture(960,540,24,RenderTextureFormat.ARGB32) { name = "Voyage check GPU capture", antiAliasing = 1 };
            bool gpu = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null && capture.Create();
            Check(gpu, "GPU camera capture", SystemInfo.graphicsDeviceName);
            if (!gpu) yield break;
            readback = new Texture2D(capture.width,capture.height,TextureFormat.RGB24,false);
            cam.targetTexture = capture;
            yield return Frames(6);
            ReadFrame();
            SaveFrame("01-quarterdeck.png");

            Check(game.Pirate != game.Navy && ship.Definition.Type == ShipType.Brigantine && game.Navy.Definition.Type == ShipType.Brigantine && ship.Rig.Guns.Length == 18 && game.Navy.Rig.Guns.Length == 18,
                "player brigantine and independently simulated navy brigantine", "player guns=" + ship.Rig.Guns.Length + " opponent guns=" + game.Navy.Rig.Guns.Length);
            Check(Flag(ship, "FlagsPirate") && !Flag(ship, "FlagsBritish") &&
                Flag(game.Navy, "FlagsBritish") && !Flag(game.Navy, "FlagsPirate"),
                "Pirate and navy flags assigned to opposite factions", "");
            var sail = ship.Rig.transform.Find("SailJib");
            Check(ship.Anchored && ship.SailStep == 0 && sail && sail.localScale.y < .15f,
                "new voyage starts with furled sails and anchor down", "step=" + ship.SailStep);
            Enter("舵轮");
            yield return Frames(12,new CrewCommand { Move = Vector2.right });
            var wheel = ship.Rig.transform.Find("Wheel");
            Check(ship.Rudder > .2f && wheel && Quaternion.Angle(wheel.localRotation,Quaternion.identity) > 5f,
                "helm A/D moves rudder and visible wheel", "rudder=" + F(ship.Rudder));
            Leave();
            yield return HoldRouteCheck.Walk(game, Check);

            yield return Cannon();
            Check(ship.Flooding.ActiveLeaks == 0 && ship.Flooding.WaterLevel01 == 0, "drill begins with dry undamaged player hold", "");
            game.StartDamageDrill();
            yield return Frames(15);
            var flooding = ship.Flooding;
            int last = ship.Rig.LeakInside.Length - 1;
            Check(last > 0 && flooding.IsLeaking(0) && flooding.IsLeaking(last) && flooding.ActiveLeaks == 2,
                "F6 training action opens two separate leaks","leaks=" + flooding.ActiveLeaks);
            Check(flooding.ActiveDecals == 4 && Decal(0,"hold") && Decal(0,"exterior") && Decal(last,"hold") && Decal(last,"exterior"),
                "both sides of both hull zones have renderer-backed decals", "decals=" + flooding.ActiveDecals);
            Check(flooding.WaterVisible && flooding.WaterHeight > ship.Flooding.DryHeight + .015f, "visible water rises with two leaks", "height (m)=" + F(flooding.WaterHeight));
            Check(ship.GetComponentsInChildren<ParticleSystem>(true).Length == 0,
                "hull damage does not add ship particle emitters", "");
            Tick(default);
            Check(!crew.Prompt.Contains("破口"),"unseen lower-hold damage creates no remote crew prompt",crew.Prompt);
            float remoteWater = flooding.WaterLevel01;
            Tick(new CrewCommand { UsePressed = true });
            yield return Frames(65);
            Check(crew.StationName != "船壳破口" && crew.StationName != "舱底泵" && flooding.ActiveLeaks == 2 && flooding.WaterLevel01 > remoteWater,
                "E on the gun deck cannot repair or pump the remote hold", crew.StationName);
            Leave();

            yield return Repair(0,true);
            Check(flooding.ActiveLeaks == 1 && flooding.ActiveDecals == 2 && flooding.IsLeaking(last),
                "first repair leaves exactly the other leak active","leaks=" + flooding.ActiveLeaks + " decals=" + flooding.ActiveDecals);
            yield return PumpWithOpenLeak(last);
            yield return Repair(last,false);
            float residual = flooding.WaterHeight;
            yield return Frames(25);
            Check(flooding.ActiveLeaks == 0 && flooding.ActiveDecals == 0 && flooding.WaterVisible && residual > ship.Flooding.DryHeight + .015f &&
                Mathf.Abs(flooding.WaterHeight - residual) < .0001f, "repairs stop ingress and leave residual water",
                "height (m) before=" + F(residual) + " after=" + F(flooding.WaterHeight));

            yield return Pump();
            yield return Sailing();
            yield return Sinking();
        }

        private static bool Flag(NavalShip vessel, string name)
        {
            var child = vessel.Rig.transform.Find(name);
            return child != null && child.gameObject.activeInHierarchy &&
                child.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0;
        }

        private IEnumerator Cannon()
        {
            int index = Array.FindIndex(ship.Rig.Guns, gun => gun && gun.name == "上层右舷炮饰 02");
            Check(index >= 0, "starboard gun resolved from measured brigantine transform","index=" + index);
            if (index < 0) yield break;
            var gun = ship.Rig.Guns[index];
            ApproachGun(index);
            int hits = ship.HitsLanded;
            Enter("右舷炮");
            yield return Frames(10);
            Check(crew.HoldProgress > 0f && !ship.IsLoaded(index), "empty gun begins loading after one E", gun.name);
            Leave();
            yield return Frames(CannonLoadFrames + 1);
            Check(!ship.IsLoaded(index) && crew.HoldProgress == 0f && ship.HitsLanded == hits,
                "E cancels incomplete loading, gun remains empty while unattended", gun.name);
            Enter("右舷炮");
            yield return Frames(12);
            Tick(new CrewCommand { FirePressed = true });
            yield return Frames(CannonLoadFrames);
            Check(ship.IsLoaded(index) && crew.IsAiming && ship.HitsLanded == hits && !FindAnyObjectByType<RoundShot>(),
                "empty gun reloads with no held input, premature trigger does not queue a shot", gun.name);
            var navy = game.Navy;
            float z = navy.Definition.Length * .5f;
            float height = Mathf.Lerp(navy.Definition.DryDraft, navy.Definition.Data.mainDeck, .55f);
            Vector3 target = navy.transform.TransformPoint(navy.Definition.HalfBreadth(z, height), height, z);
            Vector3 offset = target - gun.position;
            float flight = Mathf.Max(0f, Vector3.ProjectOnPlane(offset, Vector3.up).magnitude - NavalShip.ShotSpawnOffset) / NavalShip.ShotSpeed;
            Vector3 aim = gun.InverseTransformDirection(offset + Vector3.up * (.5f * NavalShip.ShotDrop * flight * flight));
            float pitch = Mathf.Atan2(aim.y, new Vector2(aim.x, aim.z).magnitude) * Mathf.Rad2Deg;
            float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
            Tick(new CrewCommand { Look = new Vector2(yaw/.10f, (pitch - crew.GunPitch)/.085f) });
            Check(crew.IsAiming && Mathf.Abs(crew.GunPitch - pitch) < .1f, "mouse aims the gun at the target hull", "pitch=" + F(crew.GunPitch));
            Tick(new CrewCommand { FirePressed = true });
            Check(!ship.IsLoaded(index) && FindAnyObjectByType<RoundShot>() != null, "left click launches a game projectile",gun.name);
            yield return Frames(CannonLoadFrames - 1);
            Check(!ship.IsLoaded(index), "cannon remains empty one tick before five game seconds", gun.name);
            yield return Frames(2);
            Check(ship.IsLoaded(index) && crew.IsAiming && ship.HitsLanded == hits + 1 && !FindAnyObjectByType<RoundShot>(),
                "fired gun automatically reloads and stays ready without firing again", gun.name);
            Check(ship.HitsLanded > hits && game.Navy.Flooding.ActiveLeaks > 0 && game.Navy.Flooding.ActiveDecals >= 2,
                "round shot strikes other vessel and produces hull damage on both faces",
                "hits=" + ship.HitsLanded + " opponent leaks=" + game.Navy.Flooding.ActiveLeaks + " decals=" + game.Navy.Flooding.ActiveDecals);
            Leave();
            Enter("右舷炮");
            yield return Frames(6);
            Leave();
            Check(ship.IsLoaded(index) && ship.HitsLanded == hits + 1 && !FindAnyObjectByType<RoundShot>(),
                "E entering or leaving a loaded gun never fires it", gun.name);
        }

        private IEnumerator Repair(int index, bool photograph)
        {
            Transform target = ship.Rig.LeakInside[index];
            Vector3 intoHold = Vector3.ProjectOnPlane(target.forward,ship.transform.up).normalized;
            Vector3 localFoot = ship.transform.InverseTransformPoint(target.position + intoHold * .70f);
            localFoot.y = ship.Definition.Data.lowerDeck + .026f;
            WarpFoot(localFoot);
            yield return Frames(3);
            // A lower-deck cannon is also within reach here. Looking away hides the
            // patch, while E is still free to operate the nearby gun.
            OrbitTo(intoHold, 8f);
            yield return Frames(5);
            Check(!crew.Prompt.Contains("破口"),"nearby patch behind the camera gives no prompt",crew.Prompt);
            Tick(new CrewCommand { UsePressed = true });
            Check(crew.StationName != "船壳破口" && ship.Flooding.IsLeaking(index),
                "E cannot start a patch outside the camera view", "station=" + crew.StationName + " " + crew.Prompt);
            Leave();
            yield return Frames(3); // Restore the third-person camera before computing its bearing.
            OrbitTo(target.position - crew.transform.position, 28f);
            yield return Frames(5);
            RecordRepairSight(index, target);
            if (!crew.Prompt.Contains("破口"))
                throw new InvalidOperationException("Visible repair site did not offer repair, zone " + (index + 1) + " prompt=" + crew.Prompt);
            if (photograph) yield return RecordPulse(index);
            Enter("船壳破口");
            yield return Frames(15);
            float partial = crew.HoldProgress;
            Check(crew.StationName == "船壳破口" && partial > .12f && partial < .8f && ship.Flooding.IsLeaking(index),
                "one E starts repairing and empty input advances it, zone " + (index + 1), "partial=" + F(partial));
            Leave();
            yield return Frames(HullRepairFrames + 1);
            Check(crew.HoldProgress == 0f && ship.Flooding.IsLeaking(index) && Decal(index,"hold") && Decal(index,"exterior"),
                "cancelled repair stays cancelled with both damage marks intact, zone " + (index + 1), "");
            Enter("船壳破口");
            // Enter already advanced this repair by one tick.
            yield return Frames(HullRepairFrames - 2);
            Check(ship.Flooding.IsLeaking(index), "breach remains open one tick before three game seconds", target.name);
            yield return Frames(2);
            Check(string.IsNullOrEmpty(crew.StationName) && !ship.Flooding.IsLeaking(index) && !Decal(index,"hold") && !Decal(index,"exterior"),
                "restarted repair removes both decals and automatically releases the crew, zone " + (index + 1), "decals=" + ship.Flooding.ActiveDecals);
            if (photograph && !ship.Flooding.IsLeaking(index))
            {
                yield return Frames(3);
                ReadFrame();
                SaveFrame("03-hold-after-repair.png");
            }
        }

        private void RecordRepairSight(int index, Transform target)
        {
            Camera cam = game.MainCamera;
            Vector3 eye = crew.transform.position + ship.transform.up * 1.02f;
            Vector3 point = target.position;
            Vector3 screen = cam.WorldToViewportPoint(point);
            Note("SIGHT zone=" + (index+1) + " foot=" + crew.transform.localPosition.ToString("F2") +
                " camera=" + ship.transform.InverseTransformPoint(cam.transform.position).ToString("F2") +
                " patch=" + ship.transform.InverseTransformPoint(point).ToString("F2") + " viewport=" + screen.ToString("F2") +
                " reach=" + F(Vector3.Distance(eye,point)) + " inside=" + F(Vector3.Dot(eye-point,target.forward)) +
                " crew ray=" + RepairOccluder(eye,point) + " camera ray=" + RepairOccluder(cam.transform.position,point));
        }

        private string RepairOccluder(Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start;
            if (delta.magnitude <= .12f || !Physics.Raycast(start,delta.normalized,out RaycastHit hit,
                    delta.magnitude-.12f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) return "clear";
            return hit.collider.name + "@" + ship.transform.InverseTransformPoint(hit.point).ToString("F2");
        }

        private void GoToPump()
        {
            WarpFoot(new Vector3(.77f, ship.Definition.Data.lowerDeck + .026f, 9.13f));
        }

        private IEnumerator PumpWithOpenLeak(int index)
        {
            GoToPump();
            Enter("舱底泵");
            int limit = 0;
            while (ship.Flooding.WaterHeight > ship.Flooding.DryHeight + .0015f && limit++ < 450)
                yield return Frames(1);
            bool stayed = true;
            float maximum = 0f;
            for (int i = 0; i < 30; i++)
            {
                yield return Frames(1);
                maximum = Mathf.Max(maximum,ship.Flooding.WaterHeight - ship.Flooding.DryHeight);
                stayed &= crew.StationName == "舱底泵";
            }
            Check(ship.Flooding.IsLeaking(index) && stayed && maximum < .0015f,
                "single E keeps pumping a leaking hold even at zero between strokes", "peak residual (m)=" + F(maximum));
            Leave();
            float before = ship.Flooding.WaterHeight;
            yield return Frames(30);
            Check(string.IsNullOrEmpty(crew.StationName) && ship.Flooding.WaterHeight > before + .015f,
                "E cancels active drainage and open breach starts filling the hold again", "height (m)=" + F(ship.Flooding.WaterHeight));
        }

        private IEnumerator Pump()
        {
            GoToPump();
            Transform lever = ship.Rig.transform.Find("PumpLever0");
            Quaternion rest = lever ? lever.localRotation : Quaternion.identity;
            float oldLevel = ship.Flooding.WaterLevel01;
            Enter("舱底泵");
            yield return Frames(5);
            float angle = lever ? Quaternion.Angle(rest,lever.localRotation) : 0;
            Check(crew.StationName == "舱底泵" && ship.Flooding.WaterLevel01 < oldLevel && angle > 5f,
                "one E starts pumping, empty input moves the handle and removes water", "angle=" + F(angle) + " water=" + F(ship.Flooding.WaterLevel01));
            Leave();
            float paused = ship.Flooding.WaterHeight;
            yield return Frames(10);
            Check(lever && Quaternion.Angle(rest,lever.localRotation) < .1f && Mathf.Abs(ship.Flooding.WaterHeight-paused)<.0001f,
                "E stops drainage and returns the handle to rest", "");
            Enter("舱底泵");
            int frames = Mathf.CeilToInt((paused - ship.Flooding.DryHeight) / .14f / Step) + 10;
            yield return Frames(frames);
            Check(string.IsNullOrEmpty(crew.StationName) && ship.Flooding.WaterLevel01 == 0f && !ship.Flooding.WaterVisible &&
                lever && Quaternion.Angle(rest,lever.localRotation) < .1f,
                "empty sealed hold stops the pump, hides water and automatically releases the crew", "water=" + F(ship.Flooding.WaterLevel01));
        }

        private IEnumerator Sailing()
        {
            WarpFoot(ship.Definition.Data.spawn.position + Vector3.up * .026f);
            Enter("舵轮");
            var sail = ship.Rig.transform.Find("SailJib");
            Tick(new CrewCommand { SailChange = 1 });
            yield return Frames(24);
            Check(!ship.Anchored && ship.SailStep == 1 && sail && sail.localScale.y > .45f && sail.localScale.y < .7f,
                "first sail increase weighs anchor and opens half canvas", "step=" + ship.SailStep);
            Tick(new CrewCommand { SailChange = 1 });
            yield return Frames(36);
            Check(!ship.Anchored && ship.SailStep == 2 && sail && sail.localScale.y > .98f && ship.Speed > .2f,
                "second sail increase opens full canvas and gathers way", "speed=" + F(ship.Speed));
            Leave();
            var hullBefore = ship.transform.position;
            var stand = crew.transform.localPosition;
            yield return Frames(25);
            Check(Vector3.Distance(stand, crew.transform.localPosition) < .08f && Vector3.Distance(hullBefore, ship.transform.position) > .1f,
                "idle crew remains on the same spot on a moving deck", "local drift=" + F(Vector3.Distance(stand, crew.transform.localPosition)));
            Enter("舵轮");
            Tick(new CrewCommand { SailChange = -1 });
            yield return Frames(24);
            Check(!ship.Anchored && ship.SailStep == 1 && sail && sail.localScale.y > .45f && sail.localScale.y < .7f,
                "first sail decrease reefs canvas while underway", "step=" + ship.SailStep);
            Tick(new CrewCommand { SailChange = -1 });
            yield return Frames(100);
            Check(ship.Anchored && ship.SailStep == 0 && sail && sail.localScale.y < .15f && ship.Speed < .04f,
                "second sail decrease furls canvas, drops anchor and stops", "speed=" + F(ship.Speed));
            Leave();
        }

        private IEnumerator Sinking()
        {
            // The cannon case left a real hole in the target. Leave the player's repaired ship dry
            // until that hole alone sinks the opponent.
            var navy = game.Navy;
            float dryDraft = ship.Draft;
            yield return RiseTo(navy, navy.Flooding.SinkingHeight);
            yield return null;
            var hud = game.GetComponent<VoyageHud>();
            Check(navy.Flooding.IsSunk && game.IsGameOver && game.WonVoyage && hud.IsVictoryVisible &&
                Time.timeScale == 0 && ship.Flooding.WaterLevel01 == 0f &&
                Mathf.Abs(dryDraft - ship.Definition.DryDraft) < .01f,
                "target sinks independently and triggers victory while the repaired ship remains afloat", "own draft (m)=" + F(dryDraft));
            game.MainCamera.targetTexture = oldTarget;
            yield return new WaitForEndOfFrame();
            var victory = ScreenCapture.CaptureScreenshotAsTexture();
            if (victory)
            {
                File.WriteAllBytes(Path.Combine(output, "04-victory-hud.png"), victory.EncodeToPNG());
                Destroy(victory);
            }
            companyFixture.Dispose();
            hud.DefeatMenuButton.onClick.Invoke();
            Check(game.IsInMenu && Time.timeScale == 1 && !game.WonVoyage, "victory button returns to fleet selection", "");
            game.Menu.StartButton.onClick.Invoke();
            ship = game.Pirate;
            crew = game.Crew;
            crew.ReadDeviceInput = false;
            companyFixture = new AcceptanceCompanyFixture(ship, game.Navy);
            fixedTargetFlooding = game.Navy.Flooding;
            fixedTargetFlooding.enabled = false;
            Note("FIXTURE the second voyage isolates player hull sinking; the stationary target can absorb gunfire without progressing toward another victory.");
            game.MainCamera.targetTexture = capture;
            yield return Frames(2);
            float dryKeel = KeelHeight();

            int lower = GunIndex("下层右舷炮饰 04");
            int loadedLower = GunIndex("下层左舷炮饰 04");
            int upper = GunIndex("上层右舷炮饰 02");
            float lowHeight = ship.transform.InverseTransformPoint(ship.Rig.Guns[lower].position).y;
            var flooding = ship.Flooding;
            game.StartDamageDrill();
            yield return RiseTo(ship, lowHeight - .04f);
            float middleDraft = ship.Draft;
            float middleKeel = KeelHeight();
            Check(middleDraft > dryDraft + .3f && middleKeel < dryKeel - .3f && !ship.IsGunSubmerged(lower),
                "ongoing flooding lowers the rendered hull before the low guns flood",
                "draft (m)=" + F(middleDraft) + " keel (world y)=" + F(middleKeel));
            ship.Load(loadedLower);
            Check(ship.IsLoaded(loadedLower), "second low gun is charged before its mounting floods", "");
            ApproachGun(lower);
            Enter("右舷炮");
            Check(crew.HoldProgress > 0f && !ship.IsLoaded(lower), "sailor is loading at the rising waterline", crew.Prompt);
            for (int frame = 0; frame < 55 && !ship.IsGunSubmerged(lower); frame++)
                yield return Frames(1);
            Tick(new CrewCommand { FirePressed = true });
            Check(ship.IsGunSubmerged(lower) && ship.IsGunSubmerged(loadedLower) && !crew.IsAiming &&
                crew.HoldProgress == 0f && !ship.IsLoaded(lower), "flooding the occupied gun interrupts loading", crew.StationName);
            Tick(default);
            Tick(new CrewCommand { UsePressed = true });
            Check(!crew.IsAiming, "E cannot occupy the flooded gun", crew.Prompt);
            ship.Load(lower);
            Check(!ship.IsLoaded(lower) && !ship.Fire(loadedLower, 0f, 4f) && !FindAnyObjectByType<RoundShot>(),
                "a submerged empty gun cannot load and a previously charged gun cannot fire", "");
            ApproachGun(upper);
            Enter("右舷炮");
            yield return Frames(CannonLoadFrames + 1);
            Tick(new CrewCommand { FirePressed = true });
            Check(!ship.IsGunSubmerged(upper) && crew.IsAiming && !ship.IsLoaded(upper) && FindAnyObjectByType<RoundShot>(),
                "upper gun still fires while the lower battery is flooded", ship.Rig.Guns[upper].name);
            Leave();

            WarpFoot(ship.transform.InverseTransformPoint(ship.Rig.Spawn.position) + Vector3.up * .025f);
            OrbitTo(ship.transform.forward, 22f);
            yield return RiseTo(ship, flooding.SinkingHeight - .25f);
            yield return Frames(4);
            float highDraft = ship.Draft;
            float highKeel = KeelHeight();
            Check(!game.IsGameOver && !flooding.IsSunk && highDraft > middleDraft + .3f && highKeel < middleKeel - .3f,
                "partially flooded upper deck is still playable and the hull sits deeper",
                $"water={flooding.WaterHeight:F3} draft={highDraft:F3} keel={highKeel:F3} m");
            ReadFrame();
            SaveFrame("04-partially-flooded-upper-deck.png");
            GoToPump();
            float feet = crew.transform.localPosition.y;
            Check(flooding.WaterHeight > feet + .1f, "pump operator's deck is under water",
                $"water={flooding.WaterHeight:F3} feet={feet:F3} m");
            Enter("舱底泵");
            // Long movement frames exercise the real pump and leaks on the same clock.
            // Two holes admit .054 m/s and one pump removes .146 m/s.
            Time.timeScale = 4f;
            int strokes = 0;
            float initialHeight = flooding.WaterHeight;
            float pumpingSeconds = 0f;
            // Both holes remain open while the crew leaves the pump and reloads the low gun.
            float reloadClearance = .054f * CrewPlayer.CannonLoadSeconds + .12f;
            while (flooding.WaterHeight > lowHeight - reloadClearance && strokes++ < 900 && crew.StationName == "舱底泵")
            {
                yield return null;
                pumpingSeconds += Time.deltaTime;
                crew.Tick(default, Time.deltaTime);
            }
            float drainagePerSecond = (initialHeight - flooding.WaterHeight) / Mathf.Max(.001f, pumpingSeconds);
            Check(Mathf.Abs(drainagePerSecond - .092f) < .004f, "submerged pump retains its full drainage rate against both open holes",
                $"net drainage={drainagePerSecond:F3} m/s");
            Time.timeScale = 1f;
            yield return Frames(2);
            Check(crew.StationName == "舱底泵" && flooding.ActiveLeaks == 2 && !ship.IsGunSubmerged(lower) &&
                ship.Draft < highDraft - .3f && ship.Draft < lowHeight && KeelHeight() > highKeel + .3f,
                "one E continuously pumps the flooded deck, raises the hull and uncovers a low gun",
                $"water={flooding.WaterHeight:F3} draft={ship.Draft:F3} keel={KeelHeight():F3} m");
            Leave();
            ApproachGun(lower);
            Enter("右舷炮");
            yield return Frames(CannonLoadFrames + 1);
            Check(crew.IsAiming && ship.IsLoaded(lower) && !ship.IsGunSubmerged(lower),
                "recovered low gun stays dry for a full reload", $"water={flooding.WaterHeight:F3}, gun={lowHeight:F3} m");
            Tick(new CrewCommand { FirePressed = true });
            Check(crew.IsAiming && !ship.IsLoaded(lower) && FindAnyObjectByType<RoundShot>(),
                "recovered low gun accepts a crew, reloads and fires", ship.Rig.Guns[lower].name);
            Leave();

            WarpFoot(ship.transform.InverseTransformPoint(ship.Rig.Spawn.position) + Vector3.up * .025f);
            Enter("舵轮");
            yield return RiseTo(ship, flooding.SinkingHeight - .12f);
            Check(ship.IsGunSubmerged(lower) && !ship.IsGunSubmerged(upper) && !flooding.IsSunk && !game.IsGameOver && crew.StationName == "舵轮",
                "helm and upper guns remain usable until the deck crown floods",
                "height (m)=" + F(flooding.WaterHeight));
            yield return RiseTo(ship, ship.Flooding.SinkingHeight);
            yield return null;
            Check(flooding.IsSunk && game.IsGameOver && hud.IsDefeatVisible && !crew.IsAiming && string.IsNullOrEmpty(crew.StationName),
                "entire upper deck submerged, defeat panel shown and occupied helm released",
                "height (m)=" + F(flooding.WaterHeight));
            Vector3 still = crew.transform.localPosition;
            int leaks = flooding.ActiveLeaks;
            game.StartDamageDrill();
            yield return Frames(12, new CrewCommand { Move = Vector2.up, UsePressed = true, FirePressed = true });
            Check(game.IsGameOver && hud.IsDefeatVisible && flooding.IsSunk && leaks == flooding.ActiveLeaks &&
                string.IsNullOrEmpty(crew.StationName) && Vector3.Distance(still, crew.transform.localPosition) < .01f,
                "defeat locks walking and stations; training action cannot resume this voyage", crew.Prompt);
            game.MainCamera.targetTexture = oldTarget;
            yield return null;
            yield return new WaitForEndOfFrame();
            Texture2D screen = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, "05-sinking-defeat-hud.png"), screen.EncodeToPNG());
            Destroy(screen);
        }

        private IEnumerator RiseTo(NavalShip vessel, float height)
        {
            float scale = Time.timeScale;
            Time.timeScale = 8f;
            for (int i = 0; i < 1600 && vessel.Flooding.WaterHeight < height && !game.IsGameOver; i++)
                yield return null;
            if (!game.IsGameOver) Time.timeScale = scale;
            Check(vessel.Flooding.WaterHeight >= height - .0001f, "open hull reaches requested flood height",
                $"{vessel.name}: requested={height:F3}, measured={vessel.Flooding.WaterHeight:F3} m");
        }

        private int GunIndex(string name)
        {
            int index = Array.FindIndex(ship.Rig.Guns, gun => gun && gun.name == name);
            if (index < 0) throw new InvalidOperationException("Missing measured brigantine gun " + name);
            return index;
        }

        private float KeelHeight() => ship.transform.TransformPoint(new Vector3(0f, 0f, ship.Definition.PivotZ)).y;

        private void ApproachGun(int index)
        {
            Transform gun = ship.Rig.Guns[index];
            Vector3 local = ship.transform.InverseTransformPoint(gun.position - gun.forward * .55f);
            local.y = (local.y > ship.Definition.Data.mainDeck ? ship.Definition.Data.mainDeck : ship.Definition.Data.lowerDeck) + .026f;
            WarpFoot(local);
        }

        private IEnumerator RecordPulse(int index)
        {
            var renderer = Decal(index,"hold");
            if (!renderer) { Check(false,"GPU pulse sampling","missing damage mesh"); yield break; }
            int minimum = int.MaxValue, maximum = -1;
            int visibleSamples = 0;
            byte[] best = null;
            for (int i = 0; i < 43; i++)
            {
                Tick(default);
                yield return null;
                var region = ScreenRegion(renderer);
                if (region.width <= 0 || region.height <= 0) continue;
                visibleSamples++;
                ReadFrame();
                var pixels = readback.GetRawTextureData<byte>();
                int count = 0;
                int brightestRed = 0;
                for (int y = region.yMin; y < region.yMax; y++)
                for (int x = region.xMin; x < region.xMax; x++)
                {
                    int p = (y * readback.width + x) * 3;
                    int r = pixels[p], g = pixels[p+1], b = pixels[p+2];
                    if (r > g * 2 && r > b * 2) brightestRed = Mathf.Max(brightestRed,r);
                    if (r > 140 && r > g * 2 && r > b * 2) count++;
                }
                if (i % 10 == 0)
                {
                    Note($"PULSE frame={Time.frameCount} t={Time.time:F3} roi={region} redMax={brightestRed} count={count} shader={renderer.sharedMaterial.shader.name} lit={ship.Flooding.IsSignalLit(index)}");
                    SaveFrame($"pulse-{i:D2}.png");
                }
                if (count > maximum)
                {
                    maximum = count;
                    best = readback.EncodeToPNG();
                }
                minimum = Mathf.Min(minimum,count);
            }
            if (best != null) File.WriteAllBytes(Path.Combine(output,"02-hold-red-pulse.png"),best);
            Check(visibleSamples > 20 && maximum >= 6 && maximum - minimum >= 5,
                "main-camera pixels show localized red pulse while crew faces a real hull patch",
                "in-frame samples=" + visibleSamples + " red pixels min=" + minimum + " max=" + maximum);
        }

        private RectInt ScreenRegion(Renderer renderer)
        {
            Camera cam = game.MainCamera;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Vector2 low = new Vector2(2,2), high = new Vector2(-1,-1);
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                Vector3 point = cam.WorldToViewportPoint(filter.transform.TransformPoint(vertex));
                if (point.z < cam.nearClipPlane) continue;
                low = Vector2.Min(low,point);
                high = Vector2.Max(high,point);
            }
            int left = Mathf.Clamp(Mathf.FloorToInt(low.x*capture.width),0,capture.width);
            int right = Mathf.Clamp(Mathf.CeilToInt(high.x*capture.width),0,capture.width);
            int bottom = Mathf.Clamp(Mathf.FloorToInt(low.y*capture.height),0,capture.height);
            int top = Mathf.Clamp(Mathf.CeilToInt(high.y*capture.height),0,capture.height);
            return new RectInt(left,bottom,Mathf.Max(0,right-left),Mathf.Max(0,top-bottom));
        }

        private void ReadFrame()
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                // A hidden Windows player may skip presentation; sample a newly rendered camera frame.
                RenderPipeline.SubmitRenderRequest(game.MainCamera, new RenderPipeline.StandardRequest { destination = capture });
                RenderTexture.active = capture;
                readback.ReadPixels(new Rect(0,0,capture.width,capture.height),0,0,false);
                readback.Apply(false,false);
            }
            finally { RenderTexture.active = previous; }
        }

        private void SaveFrame(string name)
        {
            File.WriteAllBytes(Path.Combine(output,name),readback.EncodeToPNG());
            Note("CAMERA " + name + " " + readback.width + "x" + readback.height);
        }

        private void WarpFoot(Vector3 localFoot)
        {
            if (!string.IsNullOrEmpty(crew.StationName)) Leave();
            CharacterController controller = crew.GetComponent<CharacterController>();
            controller.enabled = false;
            crew.transform.position = ship.transform.TransformPoint(localFoot);
            controller.enabled = true;
            Physics.SyncTransforms();
            Tick(default);
            Note("FIXTURE crew foot local " + localFoot.ToString("F2"));
        }

        private void OrbitTo(Vector3 worldHeading, float pitch)
        {
            Vector3 current = ship.transform.InverseTransformDirection(game.MainCamera.transform.forward);
            Vector3 target = ship.transform.InverseTransformDirection(worldHeading);
            float yaw = Mathf.Atan2(current.x,current.z)*Mathf.Rad2Deg;
            float nextYaw = Mathf.Atan2(target.x,target.z)*Mathf.Rad2Deg;
            float currentPitch = -Mathf.Asin(Mathf.Clamp(current.y,-1f,1f))*Mathf.Rad2Deg;
            Tick(new CrewCommand { Look = new Vector2(Mathf.DeltaAngle(yaw,nextYaw)/.115f,(currentPitch-pitch)/.10f) });
        }

        private MeshRenderer Decal(int index,string side)
        {
            var found = ship.Rig.transform.Find("Hull damage " + (index+1) + " - " + side);
            return found && found.gameObject.activeSelf ? found.GetComponent<MeshRenderer>() : null;
        }

        private void Enter(string wanted)
        {
            string hint = crew.Prompt;
            Tick(new CrewCommand { UsePressed = true });
            Check(crew.StationName == wanted,"E enters nearby " + wanted,"hint=" + hint + " actual=" + crew.StationName);
        }

        private void Leave()
        {
            if (string.IsNullOrEmpty(crew.StationName)) return;
            Tick(new CrewCommand { UsePressed = true });
            Check(string.IsNullOrEmpty(crew.StationName),"E exits without entering another post","");
        }

        private void Tick(CrewCommand command) => crew.Tick(command,Step);
        private IEnumerator Frames(int count,CrewCommand command = default)
        {
            for (int i = 0; i < count; i++)
            {
                Tick(command);
                yield return null;
            }
        }

        private static string F(float value) => value.ToString("0.000",CultureInfo.InvariantCulture);
        private void Check(bool condition,string title,string detail)
        {
            if (condition) passed++; else failed++;
            Note((condition ? "PASS " : "FAIL ") + title + (string.IsNullOrEmpty(detail) ? "" : " | " + detail));
        }

        private void Note(string message)
        {
            File.AppendAllText(receipt,message + Environment.NewLine);
            Debug.Log("[VoyageCheck] " + message);
        }
    }
}
