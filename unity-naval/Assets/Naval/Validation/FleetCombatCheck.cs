using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;

namespace Galleon
{
    public sealed partial class FleetCheck
    {
        Mouse combatMouse;

        public static void BeginCombat(NavalGame session, string directory)
        {
            var check = session.gameObject.AddComponent<FleetCheck>();
            check.StartCheck(session, directory, "combat-check", "Deck combat", check.RunCombat());
        }

        IEnumerator RunCombat()
        {
            yield return null;
            type = ShipType.Brigantine;
            Require(game.IsInMenu && game.Menu.CombatButton.isActiveAndEnabled, "fleet menu shows the combat test button");
            yield return Photograph("combat-menu");
            Click(game.Menu.CombatButton);
            yield return HoldKeys(3);
            Require(game.IsCombatTest && !game.IsInMenu && Ship.Definition.Type == type && game.TestDummies.Count == 2,
                "combat button launched the brigantine with two targets");
            var left = game.TestDummies[0];
            var right = game.TestDummies[1];
            Require(left && right && left != right && left.Health == 100 && right.Health == 100, "two independent 100 HP targets");
            yield return new WaitForEndOfFrame();
            foreach (var target in game.TestDummies)
            {
                var skin = target.GetComponentInChildren<SkinnedMeshRenderer>();
                Vector3 viewport = game.MainCamera.WorldToViewportPoint(target.AimPoint);
                Require(skin && SailorOnCamera(skin) && viewport.z > 0f && viewport.x > 0f && viewport.x < 1f &&
                    viewport.y > 0f && viewport.y < 1f, $"visible standing target {target.name}, viewport={viewport:F2}");
            }
            CheckTargetHud(0, 100);
            CheckTargetHud(1, 100);
            CheckCombatGear(CrewWeapon.None);
            yield return Photograph("combat-deck");

            combatMouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return CheckCombatPistol(left, right);
                yield return CheckCombatCutlass(left, right);
                yield return KeyTap(Key.F8);
                Require(game.IsCombatTest && Ship.Definition.Type == type && game.TestDummies.Count == 2 && !left && !right &&
                    game.TestDummies[0].Health == 100 && game.TestDummies[1].Health == 100 && Crew.Weapon == CrewWeapon.None &&
                    !Crew.IsPistolAiming,
                    "F8 stayed in combat practice and replaced the injured targets with two 100 HP targets");
                left = game.TestDummies[0];
                right = game.TestDummies[1];
                yield return KeyTap(Key.Escape);
                Click(game.GetComponent<VoyageHud>().PauseMenuButton);
                yield return HoldKeys(2);
                Require(game.IsInMenu && !game.IsCombatTest && game.TestDummies.Count == 0 && !left && !right,
                    "returning to fleet selection destroys both training targets");

                Click(game.Menu.CombatButton);
                yield return HoldKeys(2);
                Require(game.IsCombatTest && game.TestDummies.Count == 2 && game.TestDummies[0].Health == 100 &&
                    game.TestDummies[1].Health == 100 && Crew.PistolCooldownRemaining == 0f && Crew.Weapon == CrewWeapon.None &&
                    !Crew.IsPistolAiming,
                    "reentering combat creates a fresh crew and two fresh targets");
                yield return KeyTap(Key.Escape);
                Click(game.GetComponent<VoyageHud>().PauseMenuButton);
                yield return HoldKeys(2);
                yield return CheckVoyageCombatControls();
            }
            finally
            {
                InputSystem.RemoveDevice(combatMouse);
                combatMouse = null;
            }
        }

        Animation CombatAnimation => Crew.GetComponentInChildren<Animation>(true);

        void CheckCombatGear(CrewWeapon expected, bool aiming = false)
        {
            var props = CombatAnimation.GetComponentsInChildren<MeshRenderer>(true);
            var pistol = Array.Find(props, r => r.name == "Pistol");
            var cutlass = Array.Find(props, r => r.name == "Cutlass");
            Require(Crew.Weapon == expected && pistol && cutlass &&
                SailorDrawable(pistol) == (expected == CrewWeapon.Pistol) &&
                SailorDrawable(cutlass) == (expected == CrewWeapon.Cutlass),
                $"equipped {Crew.Weapon}, expected {expected}; pistol and cutlass mesh visibility follows selection");
            var hud = game.transform.Find("航海界面/航行界面");
            var sight = hud.Find("PistolSight");
            var label = hud.Find("CrewWeapon/EquippedWeapon").GetComponent<TMP_Text>();
            string name = expected == CrewWeapon.Pistol ? "燧发手枪" : expected == CrewWeapon.Cutlass ? "砍刀" : "已收起";
            Require(Crew.IsPistolAiming == aiming && sight.gameObject.activeInHierarchy == aiming &&
                label.isActiveAndEnabled && label.GetParsedText().Contains(name),
                $"HUD displays {name}; pistol aiming and center sight both {aiming}");
        }

        void CheckTargetHud(int index, int health)
        {
            var root = Array.Find(game.GetComponentsInChildren<RectTransform>(), r => r.name == "TestTarget" + index);
            var value = root ? root.Find("TargetHealthValue")?.GetComponent<TMP_Text>() : null;
            var fill = root ? root.GetComponentsInChildren<RectTransform>() : Array.Empty<RectTransform>();
            var bar = Array.Find(fill, r => r.name == "TargetHealthFill");
            var track = bar ? bar.parent as RectTransform : null;
            string text = value ? value.GetParsedText().Replace(" ", "") : "";
            Require(value && bar && track && track.rect.width > 0f && text.Contains(health + "/100") &&
                Mathf.Abs(bar.rect.width / track.rect.width - health * .01f) < .025f,
                $"target {index + 1} visible HUD reads {text}, expected {health}/100 with matching fill");
        }

        void CheckReloadLabel(string status, int seconds)
        {
            var label = game.transform.Find("航海界面/航行界面/CrewWeapon/PistolReload").GetComponent<TMP_Text>();
            string shown = label.GetParsedText().Replace(" ", "");
            Require(label.isActiveAndEnabled && shown.Contains(status) && shown.Contains(seconds + "秒") &&
                Mathf.CeilToInt(Crew.PistolCooldownRemaining) == seconds,
                $"reload HUD shows {shown} at {Crew.PistolCooldownRemaining:F2}s");
        }

        void CombatMouse(bool left = false, bool right = false, Vector2 look = default)
        {
            var state = new MouseState { delta = look }.WithButton(MouseButton.Left, left).WithButton(MouseButton.Right, right);
            InputSystem.QueueStateEvent(combatMouse, state);
        }

        IEnumerator RightClickAim(bool aiming)
        {
            CombatMouse(right: true);
            yield return null;
            CombatMouse();
            yield return null;
            yield return new WaitForEndOfFrame();
            CheckCombatGear(CrewWeapon.Pistol, aiming);
        }

        IEnumerator AimCombat(Func<Vector3> point)
        {
            var camera = game.MainCamera;
            bool deviceLook = Cursor.lockState == CursorLockMode.Locked;
            for (int i = 0; i < 32; i++)
            {
                yield return new WaitForEndOfFrame();
                Vector3 local = camera.transform.InverseTransformDirection(point() - camera.transform.position);
                float horizontal = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                float vertical = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
                if (Mathf.Abs(horizontal) < .30f && Mathf.Abs(vertical) < .30f) break;
                var look = new Vector2(Mathf.Clamp(horizontal, -48f, 48f) / .115f,
                    Mathf.Clamp(vertical, -24f, 24f) / .1f);
                // In an unfocused standalone the shell can release the pointer. Replay only the orbit in that case.
                if (deviceLook) CombatMouse(look: look);
                else Crew.Tick(new CrewCommand { Look = look }, .0001f);
                yield return null;
            }
            Vector3 screen = camera.WorldToViewportPoint(point());
            Require(screen.z > 0f && Mathf.Abs(screen.x - .5f) < .012f && Mathf.Abs(screen.y - .5f) < .012f,
                $"aimed the third-person camera at viewport {screen:F3}, device mouse look={deviceLook}");
        }

        IEnumerator ShootCombat(CombatDummy left, CombatDummy right, bool hit, bool capture = true)
        {
            int priorLeft = left.Health;
            int priorRight = right.Health;
            var muzzle = PistolMuzzle();
            Require(Crew.IsPistolAiming && Crew.PistolCooldownRemaining == 0f, "pistol was aimed and ready before a fresh left click");
            CombatMouse(true);
            yield return null;
            CombatMouse();
            Require(Crew.IsPistolAiming && Crew.PistolCooldownRemaining >= 5f - Step * 1.1f &&
                Crew.PistolCooldownRemaining <= 5f + .001f && MuzzleHeight(muzzle) > 1.12f,
                $"fresh left press discharged the raised pistol on that frame and started {Crew.PistolCooldownRemaining:F2}s reload");
            var plume = FindAnyObjectByType<PistolSmoke>();
            Require(plume && Vector3.Distance(muzzle.position, plume.transform.position) < .6f,
                "smoke appeared beside the raised muzzle on the discharge frame");
            Require(left.Health == priorLeft - (hit ? CrewPlayer.PistolDamage : 0) && right.Health == priorRight,
                $"pistol {(hit ? "hit the left target" : "missed both targets")}: HP {left.Health}/{right.Health}");
            yield return new WaitForEndOfFrame();
            CheckTargetHud(0, left.Health);
            CheckTargetHud(1, right.Health);
            if (hit && capture) yield return Photograph("combat-pistol-fire");
        }

        Transform PistolMuzzle() =>
            Array.Find(CombatAnimation.GetComponentsInChildren<Transform>(true), t => t.name == "PistolMuzzle");

        float MuzzleHeight(Transform muzzle) => Crew.transform.InverseTransformPoint(muzzle.position).y;

        float BarrelElevation(Transform muzzle)
        {
            var fromGrip = (muzzle.position - muzzle.parent.position).normalized;
            return Mathf.Asin(Mathf.Clamp(Vector3.Dot(fromGrip, Ship.transform.up), -1f, 1f)) * Mathf.Rad2Deg;
        }

        IEnumerator NoDischarge(CombatDummy left, CombatDummy right, bool aiming, string action)
        {
            // Longer than the complete original firing clip, so a delayed or queued shot also fails.
            bool clean = true;
            for (int i = 0; i < 34; i++)
            {
                yield return null;
                clean &= Crew.IsPistolAiming == aiming && Crew.PistolCooldownRemaining == 0f &&
                    left.Health == 100 && right.Health == 100 && !FindAnyObjectByType<PistolSmoke>();
            }
            Require(clean, action + $": aim stayed {aiming} without smoke, damage or reload across 1.70s");
        }

        IEnumerator CheckAimInput(CombatDummy left, CombatDummy right)
        {
            var muzzle = PistolMuzzle();
            Require(muzzle && SailorOnCamera(muzzle.GetComponentInParent<MeshRenderer>()), "equipped pistol model visible at the sailor's hand");
            float low = MuzzleHeight(muzzle);
            yield return Photograph("combat-pistol-equipped");
            yield return AimCombat(() => left.AimPoint);
            CombatMouse(left: true);
            yield return null;
            CombatMouse();
            yield return NoDischarge(left, right, false, "left click before aiming");

            CombatMouse(left: true);
            yield return null;
            CombatMouse(left: true, right: true);
            yield return null;
            CombatMouse(left: true);
            yield return NoDischarge(left, right, true, "left held before right click and still held after releasing right");
            CombatMouse();
            yield return null;
            yield return AimCombat(() => left.AimPoint);
            CheckCombatGear(CrewWeapon.Pistol, true);
            float raised = MuzzleHeight(muzzle);
            Require(raised > low + .35f && raised > 1.12f && SailorOnCamera(muzzle.GetComponentInParent<MeshRenderer>()),
                $"right click alone visibly raised the muzzle from {low:F2}m to {raised:F2}m");
            yield return Photograph("combat-pistol-aim");

            yield return AimCombat(() => left.AimPoint + Ship.transform.up * 1.45f);
            float up = BarrelElevation(muzzle);
            yield return AimCombat(() => left.AimPoint - Ship.transform.up * .85f);
            float down = BarrelElevation(muzzle);
            Require(up > down + 14f, $"looking up then down tilted the rendered grip-to-muzzle axis from {up:F1}° to {down:F1}°");
            yield return AimCombat(() => left.AimPoint);

            Vector3 start = CrewOnShip;
            float lowest = raised;
            var skin = Crew.GetComponentInChildren<SkinnedMeshRenderer>();
            var pelvis = SailorBone(skin, "Pelvis");
            var foot = SailorBone(skin, "Foot.L");
            Vector3 initialFoot = pelvis.InverseTransformDirection(foot.position - pelvis.position);
            float footTravel = 0f;
            Keyboard(Key.A);
            for (int i = 0; i < 7; i++)
            {
                yield return null;
                lowest = Mathf.Min(lowest, MuzzleHeight(muzzle));
                Vector3 currentFoot = pelvis.InverseTransformDirection(foot.position - pelvis.position);
                footTravel = Mathf.Max(footTravel, Vector3.Distance(currentFoot, initialFoot));
            }
            Keyboard();
            float travelled = Vector3.Distance(CrewOnShip, start);
            Require(Crew.IsPistolAiming && travelled > .40f && lowest > low + .30f && left.Health == 100 &&
                right.Health == 100 && Crew.PistolCooldownRemaining == 0f && footTravel > .035f,
                $"strafed {travelled:F2}m after releasing right mouse; muzzle stayed above {lowest:F2}m as the bound foot travelled {footTravel:F2}m relative to pelvis");
            yield return Photograph("combat-pistol-aim-walk");
            yield return HoldKeys(7, Key.D);
            yield return HoldKeys(2);
            yield return RightClickAim(false);
            yield return NoDischarge(left, right, false, "second right click cancelled aim");
            float lowered = MuzzleHeight(muzzle);
            Require(lowered < raised - .35f, $"cancelled aim lowered the pistol to {lowered:F2}m");
            yield return Photograph("combat-pistol-aim-cancel");
            CombatMouse(left: true);
            yield return null;
            CombatMouse();
            yield return NoDischarge(left, right, false, "new left press after cancelling aim");

            CombatMouse(left: true, right: true);
            yield return null;
            CombatMouse();
            yield return NoDischarge(left, right, true, "simultaneous fresh left and right while unaimed");
            CombatMouse(left: true, right: true);
            yield return null;
            CombatMouse();
            yield return NoDischarge(left, right, false, "simultaneous fresh left and right while aimed");
            CombatMouse(right: true);
            yield return NoDischarge(left, right, true, "holding the right button without releasing");
            CombatMouse();
            yield return null;
            yield return KeyTap(Key.Digit2);
            CheckCombatGear(CrewWeapon.Cutlass);
            yield return KeyTap(Key.Digit1);
            CheckCombatGear(CrewWeapon.Pistol);
            yield return RightClickAim(true);
            yield return KeyTap(Key.Digit1);
            CheckCombatGear(CrewWeapon.None);
            yield return KeyTap(Key.Digit1);
            CheckCombatGear(CrewWeapon.Pistol);
        }

        IEnumerator CheckCombatPistol(CombatDummy left, CombatDummy right)
        {
            yield return KeyTap(Key.Digit1);
            CheckCombatGear(CrewWeapon.Pistol);
            yield return KeyTap(Key.Digit1);
            CheckCombatGear(CrewWeapon.None);
            yield return KeyTap(Key.Digit2);
            CheckCombatGear(CrewWeapon.Cutlass);
            yield return KeyTap(Key.Digit2);
            CheckCombatGear(CrewWeapon.None);
            yield return KeyTap(Key.Digit1);
            yield return CheckAimInput(left, right);
            yield return RightClickAim(true);
            yield return AimCombat(() => left.AimPoint);
            yield return ShootCombat(left, right, true);
            yield return HoldKeys(14);
            float prior = Crew.PistolCooldownRemaining;
            CombatMouse(true);
            yield return null;
            CombatMouse();
            yield return HoldKeys(5);
            Require(!Crew.IsAttacking && Crew.PistolCooldownRemaining < prior && left.Health == 100 - CrewPlayer.PistolDamage,
                "clicking during reload neither starts another shot nor resets the timer");
            float beforeCancel = Crew.PistolCooldownRemaining;
            yield return RightClickAim(false);
            yield return HoldKeys(3);
            Require(Crew.PistolCooldownRemaining > 0f && Crew.PistolCooldownRemaining < beforeCancel && Crew.Weapon == CrewWeapon.Pistol,
                "right click cancelled the sight during reload; the equipped pistol continued loading");
            yield return KeyTap(Key.Escape);
            float paused = Crew.PistolCooldownRemaining;
            yield return HoldKeys(16);
            Require(Time.timeScale == 0f && paused > 0f && Crew.PistolCooldownRemaining == paused,
                $"pause held the equipped pistol at {paused:F2}s across 16 rendered frames");
            yield return KeyTap(Key.Escape);
            Require(Time.timeScale == 1f && Crew.PistolCooldownRemaining < paused, "resuming the voyage resumed the equipped reload");
            // Keep all four state changes within the same displayed second to catch stale text caches.
            for (int i = 0; i < 20 && Mathf.Repeat(Crew.PistolCooldownRemaining, 1f) < .55f; i++) yield return null;
            yield return new WaitForEndOfFrame();
            int displayedSeconds = Mathf.CeilToInt(Crew.PistolCooldownRemaining);
            CheckReloadLabel("装填中", displayedSeconds);
            yield return KeyTap(Key.Digit1);
            CheckCombatGear(CrewWeapon.None);
            CheckReloadLabel("装填暂停", displayedSeconds);
            float frozen = Crew.PistolCooldownRemaining;
            yield return HoldKeys(24);
            Require(frozen > 0f && Mathf.Abs(Crew.PistolCooldownRemaining - frozen) < .001f,
                $"holstering held reload at {frozen:F2}s for 1.20 simulated seconds");
            yield return KeyTap(Key.Digit2);
            yield return HoldKeys(24);
            Require(Crew.Weapon == CrewWeapon.Cutlass && Mathf.Abs(Crew.PistolCooldownRemaining - frozen) < .001f,
                "equipped cutlass held the same reload time");
            CheckReloadLabel("装填暂停", displayedSeconds);
            yield return KeyTap(Key.Digit1);
            CheckCombatGear(CrewWeapon.Pistol);
            CheckReloadLabel("装填中", displayedSeconds);
            double resumed = Time.timeAsDouble;
            float remaining = Crew.PistolCooldownRemaining;
            Require(remaining > 0f && remaining <= frozen && frozen - remaining <= 4 * Step,
                $"drawing pistol resumed from the saved {frozen:F2}s, now {remaining:F2}s");
            for (int i = 0; i < 110 && Crew.PistolCooldownRemaining > 0f; i++) yield return null;
            double elapsed = Time.timeAsDouble - resumed;
            Require(Crew.PistolCooldownRemaining == 0f && Math.Abs(elapsed - remaining) <= Step * 1.1f,
                $"remaining reload {remaining:F2}s completed after {elapsed:F2}s equipped");
            Vector3 miss = Ship.transform.InverseTransformPoint(Crew.transform.position) + new Vector3(7f, 3f, 8f);
            yield return RightClickAim(true);
            yield return AimCombat(() => Ship.transform.TransformPoint(miss));
            yield return RightClickAim(false);
            CombatMouse(right: true);
            yield return null;
            Require(Crew.IsPistolAiming && Crew.PistolCooldownRemaining == 0f, "right press entered aim without firing");
            // Release right and press left on the very next input frame.
            Vector3 shotStart = CrewOnShip;
            Keyboard(Key.A);
            yield return ShootCombat(left, right, false);
            Keyboard();
            float shotTravel = Vector3.Distance(shotStart, CrewOnShip);
            Require(shotTravel > .025f && Crew.IsPistolAiming, $"new left press fired during a {shotTravel:F2}m keyboard strafe on the same frame");
            yield return CheckCombatSmoke();
            for (int i = 0; i < 110 && Crew.PistolCooldownRemaining > 0f; i++) yield return null;
            yield return AimCombat(() => left.AimPoint);
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                // Independent physical fixture between the muzzle and a previously hit target.
                cover.name = "Combat check cover";
                cover.transform.SetParent(Ship.transform);
                Vector3 toCrew = (Crew.transform.position + Ship.transform.up * 1.12f - left.AimPoint).normalized;
                cover.transform.SetPositionAndRotation(left.AimPoint + toCrew * .75f,
                    Quaternion.LookRotation(toCrew, Ship.transform.up));
                cover.transform.localScale = new Vector3(.95f, 1.45f, .16f);
                Physics.SyncTransforms();
                var sight = game.MainCamera.ViewportPointToRay(new Vector3(.5f, .5f));
                Require(Physics.Raycast(sight, out var contact, 12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                    contact.collider.gameObject == cover, "the physical test cover is the first solid object on the aimed sightline");
                yield return ShootCombat(left, right, false);
                Require(left.Health == 100 - CrewPlayer.PistolDamage && right.Health == 100,
                    "firing into solid test cover left the previously hittable target unharmed");
                yield return Photograph("combat-shot-blocked");
            }
            finally
            {
                cover.SetActive(false);
                Destroy(cover);
            }
        }

        IEnumerator CheckCombatSmoke()
        {
            var smoke = FindAnyObjectByType<PistolSmoke>();
            var plume = smoke.GetComponent<SpriteRenderer>();
            var block = new MaterialPropertyBlock();
            plume.GetPropertyBlock(block);
            float initialProgress = block.GetFloat("_Progress");
            double emitted = Time.timeAsDouble - initialProgress * 2d;
            Vector3 cloud = smoke.transform.position;
            Vector3 foot = Crew.transform.position;
            Require(!smoke.transform.parent && SailorOnCamera(plume) && plume.sprite && plume.sprite.texture &&
                plume.sprite.texture.format == TextureFormat.RGBA32 && initialProgress >= 0f && initialProgress < .1f,
                "a new unparented RGBA32 smoke sprite was visible at discharge");
            yield return HoldKeys(11, Key.A);
            yield return HoldKeys(1);
            yield return new WaitForEndOfFrame();
            Vector3 movement = Vector3.ProjectOnPlane(Crew.transform.position - foot, Vector3.up);
            float following = Mathf.Abs(Vector3.Dot(smoke.transform.position - cloud, movement.normalized));
            plume.GetPropertyBlock(block);
            float mid = block.GetFloat("_Progress");
            Require(movement.magnitude > .50f && following < .35f && mid > .24f && mid < .45f &&
                smoke.transform.position.y > cloud.y && SailorOnCamera(plume),
                $"player strafed {movement.magnitude:F2}m while the rising cloud moved only {following:F2}m along that direction, fade {mid:F2}");
            yield return Photograph("combat-smoke-strafe");
            yield return HoldKeys(11, Key.D);
            yield return HoldKeys(1);
            double lastVisible = Time.timeAsDouble;
            for (int i = 0; i < 25 && smoke && plume.enabled; i++)
            {
                lastVisible = Time.timeAsDouble;
                yield return null;
                yield return new WaitForEndOfFrame();
            }
            double lifetime = Time.timeAsDouble - emitted;
            Require((!smoke || !plume.enabled) && lifetime >= 2d - Step * 1.1d && lifetime <= 2d + Step * 1.1d &&
                lastVisible - emitted >= 2d - Step * 2.1d, $"plume disappeared after {lifetime:F2}s");
        }

        IEnumerator CheckCombatCutlass(CombatDummy left, CombatDummy right)
        {
            yield return KeyTap(Key.Digit2);
            CheckCombatGear(CrewWeapon.Cutlass);
            var animation = CombatAnimation;
            var mesh = ReadCutlassMesh(animation);
            yield return AimCombat(() => right.AimPoint);
            yield return CutlassCameraPitch(3f);
            float far = CombatDistance(right);
            int leftBefore = left.Health;
            var single = new CutlassSweep();
            CombatMouse(true);
            yield return null;
            CombatMouse();
            int activeFrames = 0;
            bool capturedLevel = false;
            for (int i = 0; i < 48; i++)
            {
                if (Crew.IsAttacking)
                {
                    activeFrames++;
                    string clip = ActiveSlash(animation);
                    if (clip != null)
                    {
                        float phase = animation[clip].normalizedTime;
                        single.Sample(mesh, phase);
                        if (!capturedLevel && phase >= .66f)
                        {
                            capturedLevel = true;
                            yield return Photograph("cutlass-v3-level");
                        }
                    }
                }
                yield return null;
            }
            Require(far > 2.5f && activeFrames >= 35 && activeFrames <= 39 && !Crew.IsAttacking &&
                right.Health == 100 && left.Health == leftBefore, $"single click ran one stroke for {activeFrames * Step:F2}s; neither target took damage at {far:F2}m");
            CheckCutlassSweep(single, "CutlassSlash", mesh);

            Vector3 start = CrewOnShip;
            for (int i = 0; i < 70 && CombatDistance(right) > 1.15f; i++)
            {
                var direction = right.transform.position - Crew.transform.position;
                var forward = Vector3.ProjectOnPlane(game.MainCamera.transform.forward, Vector3.up).normalized;
                float z = Vector3.Dot(direction, forward);
                float x = Vector3.Dot(direction, Vector3.Cross(Vector3.up, forward));
                Key longitudinal = z >= 0f ? Key.W : Key.S;
                Key lateral = x >= 0f ? Key.D : Key.A;
                if (Mathf.Abs(x) > .42f * Mathf.Abs(z) && Mathf.Abs(z) > .42f * Mathf.Abs(x))
                    yield return HoldKeys(1, longitudinal, lateral);
                else yield return HoldKeys(1, TowardKey(direction));
            }
            yield return HoldKeys(2);
            Require(CombatDistance(right) < 1.30f && Vector3.Distance(start, CrewOnShip) > 1.4f && !Crew.IsSwimming,
                $"walked on deck from {start:F2} to {CrewOnShip:F2}; right target now {CombatDistance(right):F2}m away");
            yield return AimCombat(() => right.AimPoint);
            yield return CutlassCameraPitch(48f);
            int prior = right.Health;
            string previousStrike = "";
            double priorStrike = 0d;
            var firstRight = new CutlassSweep();
            var firstLeft = new CutlassSweep();
            int strikes = 0;
            bool capturedRight = false, capturedLeft = false;
            CombatMouse(true);
            for (int frame = 0; frame < 200 && right.Health > 0; frame++)
            {
                yield return null;
                string clip = ActiveSlash(animation);
                float phase = clip == null ? 0f : animation[clip].normalizedTime;
                if (Crew.IsAttacking && clip != null)
                {
                    bool toRight = clip == "CutlassSlashRight";
                    if (strikes == 0 || (strikes == 1 && toRight)) firstRight.Sample(mesh, phase);
                    if ((strikes == 1 && !toRight) || (strikes == 2 && !toRight)) firstLeft.Sample(mesh, phase);
                    if (phase >= .66f && phase < .76f && (toRight ? !capturedRight : !capturedLeft))
                    {
                        if (toRight) capturedRight = true;
                        else capturedLeft = true;
                        yield return Photograph(toRight ? "cutlass-v3-over-right" : "cutlass-v3-over-left");
                    }
                }
                if (right.Health == prior) continue;
                double now = Time.timeAsDouble;
                Require(clip != null && clip != previousStrike && right.Health == Mathf.Max(0, prior - CrewPlayer.CutlassDamage)
                    && left.Health == leftBefore && Mathf.Abs(phase - 20.5f / 45f) <= Step / 1.875f + .002f &&
                    (priorStrike == 0d || Math.Abs(now - priorStrike - 1.875d) <= Step * 1.1d),
                    $"held left button: {clip} dealt one hit ({prior} -> {right.Health}) at {now:F2}s, clip phase {phase:F3}; other target {left.Health}");
                strikes++;
                if (prior == 100 || prior == 100 - CrewPlayer.CutlassDamage)
                    yield return Photograph("combat-" + clip);
                previousStrike = clip;
                priorStrike = now;
                prior = right.Health;
            }
            CombatMouse();
            CheckCutlassSweep(firstRight, "CutlassSlashRight", mesh);
            CheckCutlassSweep(firstLeft, "CutlassSlash", mesh);
            Require(right.Health == 0 && previousStrike.Length != 0 && left.Health == leftBefore,
                "held cutlass swings downed only the right target");
            var body = right.GetComponentInChildren<Animation>(true);
            var skin = right.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Vector3 deathSite = Ship.transform.InverseTransformPoint(right.transform.position);
            yield return new WaitForEndOfFrame();
            CheckTargetHud(0, leftBefore);
            CheckTargetHud(1, 0);
            Require(IsPose(body, "Prone") && SailorOnCamera(skin), "downed target is prone in the player camera");
            yield return Photograph("combat-target-down");
            double lastDead = Time.timeAsDouble;
            for (int i = 0; i < 210 && right.Health == 0; i++)
            {
                lastDead = Time.timeAsDouble;
                yield return null;
            }
            double respawn = Time.timeAsDouble - priorStrike;
            Require(right.Health == 100 && left.Health == leftBefore && respawn >= 10d - Step * .1d &&
                respawn <= 10d + Step * 1.1d && lastDead - priorStrike >= 10d - Step * 1.1d &&
                Vector3.Distance(deathSite, Ship.transform.InverseTransformPoint(right.transform.position)) < .01f,
                $"right target revived in place after {respawn:F2}s; other target still has {left.Health} HP");
            yield return HoldKeys(4);
            yield return new WaitForEndOfFrame();
            CheckTargetHud(0, leftBefore);
            CheckTargetHud(1, 100);
            Require(!IsPose(body, "Prone") && SailorOnCamera(skin), "revived target is standing and visible");
            yield return Photograph("combat-target-revived");
        }

        float CombatDistance(CombatDummy target) => Vector3.ProjectOnPlane(
            target.transform.position - Crew.transform.position, Ship.transform.up).magnitude;

        sealed class CutlassMesh
        {
            public Transform Mount, Stance;
            public Vector3 Tip, Silver, Spine;
            public Vector3[] Blade;
            public Vector3 At(Vector3 local) => Stance.InverseTransformPoint(Mount.TransformPoint(local));
        }

        CutlassMesh ReadCutlassMesh(Animation animation)
        {
            var renderer = Array.Find(animation.GetComponentsInChildren<MeshRenderer>(), r => r.name == "Cutlass");
            Require(renderer && renderer.TryGetComponent<MeshFilter>(out _) && SailorOnCamera(renderer),
                "the imported cutlass is drawn in the player camera");
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var materials = renderer.sharedMaterials;
            int steel = Array.FindIndex(materials, m => m.name == "Satin slate steel");
            int silver = Array.FindIndex(materials, m => m.name == "Silver edge marking");
            int spine = Array.FindIndex(materials, m => m.name == "Steel spine");
            Require(steel >= 0 && silver >= 0 && spine >= 0 && mesh.subMeshCount > Mathf.Max(steel, silver, spine),
                "rendered steel faces, silver cutting edge and dark spine use distinct submeshes");
            Require(materials[silver].color.grayscale > materials[steel].color.grayscale + .08f,
                "silver edge material is visibly lighter than the broad steel face");

            // The shipped FBX is read/write-disabled. Read the same indexed positions submitted to the GPU.
            int stream = mesh.GetVertexAttributeStream(VertexAttribute.Position);
            int stride = mesh.GetVertexBufferStride(stream);
            int offset = mesh.GetVertexAttributeOffset(VertexAttribute.Position);
            Require(mesh.GetVertexAttributeFormat(VertexAttribute.Position) == VertexAttributeFormat.Float32 &&
                mesh.GetVertexAttributeDimension(VertexAttribute.Position) == 3, "cutlass mesh has three-component position data");
            using var vertices = mesh.GetVertexBuffer(stream);
            using var indices = mesh.GetIndexBuffer();
            var vertexRead = AsyncGPUReadback.Request(vertices);
            var indexRead = AsyncGPUReadback.Request(indices);
            vertexRead.WaitForCompletion();
            indexRead.WaitForCompletion();
            Require(!vertexRead.hasError && !indexRead.hasError, "read vertex and index buffers from the actual player cutlass");
            byte[] vertexBytes = vertexRead.GetData<byte>().ToArray();
            byte[] indexBytes = indexRead.GetData<byte>().ToArray();
            bool large = mesh.indexFormat == IndexFormat.UInt32;
            Vector3[] Points(int material)
            {
                var part = mesh.GetSubMesh(material);
                var points = new HashSet<Vector3>();
                for (int i = part.indexStart; i < part.indexStart + part.indexCount; i++)
                {
                    int index = large ? (int)BitConverter.ToUInt32(indexBytes, i * 4) : BitConverter.ToUInt16(indexBytes, i * 2);
                    int start = (part.baseVertex + index) * stride + offset;
                    points.Add(new Vector3(BitConverter.ToSingle(vertexBytes, start), BitConverter.ToSingle(vertexBytes, start + 4),
                        BitConverter.ToSingle(vertexBytes, start + 8)));
                }
                return points.ToArray();
            }
            Vector3[] edgePoints = Points(silver), backPoints = Points(spine);
            var blade = new CutlassMesh
            {
                Mount = renderer.transform, Stance = animation.transform.parent,
                Blade = Points(steel).Concat(edgePoints).Concat(backPoints).Distinct().ToArray()
            };
            blade.Tip = blade.Blade.OrderByDescending(p => blade.Mount.TransformVector(p).sqrMagnitude).First();
            float length = blade.Mount.TransformVector(blade.Tip).magnitude;
            Require(length > .70f && length < .77f, $"v3 grip to the actual curved tip is {length:F3}m");
            // Compare cutting edge and spine at the same section, about 46 cm from the grip.
            Vector3 MiddleSection(Vector3[] points)
            {
                var slice = points.Where(p => Mathf.Abs(blade.Mount.TransformVector(p).magnitude - .46f) < .035f).ToArray();
                if (slice.Length == 0) throw new InvalidOperationException("No vertices at the blade's .46m section");
                return slice.Aggregate(Vector3.zero, (sum, p) => sum + p) / slice.Length;
            }
            blade.Silver = MiddleSection(edgePoints);
            blade.Spine = MiddleSection(backPoints);
            Require(blade.Mount.TransformVector(blade.Silver - blade.Spine).magnitude > .035f,
                "the measured bright edge and spine lie on opposite sides of the actual blade section");
            return blade;
        }

        sealed class CutlassSweep
        {
            public int Samples;
            public Vector3 First, Last;
            public float MinHeight = float.PositiveInfinity, MaxHeight = float.NegativeInfinity;
            public float MaxThickness, MinLeading = 1f;

            public void Sample(CutlassMesh mesh, float phase)
            {
                if (phase < 6f / 45f || phase > 35f / 45f) return;
                Vector3 tip = mesh.At(mesh.Tip);
                if (Samples == 0) First = tip;
                else
                {
                    Vector3 motion = (tip - Last).normalized;
                    Vector3 sharpSide = (mesh.At(mesh.Silver) - mesh.At(mesh.Spine)).normalized;
                    MinLeading = Mathf.Min(MinLeading, Vector3.Dot(motion, sharpSide));
                }
                Last = tip;
                Samples++;
                MinHeight = Mathf.Min(MinHeight, tip.y);
                MaxHeight = Mathf.Max(MaxHeight, tip.y);
                float low = float.PositiveInfinity, high = float.NegativeInfinity;
                foreach (Vector3 vertex in mesh.Blade)
                {
                    float height = mesh.At(vertex).y;
                    low = Mathf.Min(low, height);
                    high = Mathf.Max(high, height);
                }
                MaxThickness = Mathf.Max(MaxThickness, high - low);
            }
        }

        void CheckCutlassSweep(CutlassSweep sweep, string clip, CutlassMesh mesh)
        {
            float span = sweep.Last.x - sweep.First.x;
            bool rightward = clip == "CutlassSlashRight";
            Require(sweep.Samples >= 20 && (rightward ? span > 1.55f : span < -1.55f) &&
                sweep.MinHeight >= 1.19f && sweep.MaxHeight <= 1.28f && sweep.MaxHeight - sweep.MinHeight < .045f,
                $"{clip} actual tip crossed {span:F3}m laterally at {sweep.MinHeight:F3}–{sweep.MaxHeight:F3}m in {sweep.Samples} sweep samples");
            Require(sweep.MaxThickness < .027f && sweep.MinLeading > .88f,
                $"{clip} blade occupied at most {sweep.MaxThickness:F3}m vertical thickness and its bright edge led the tip motion (min dot {sweep.MinLeading:F3}, {mesh.Blade.Length} distinct vertices)");
        }

        IEnumerator CutlassCameraPitch(float degrees)
        {
            var camera = game.MainCamera.transform;
            float Pitch() => -Mathf.Asin(Mathf.Clamp(Vector3.Dot(camera.forward, Ship.transform.up), -1f, 1f)) * Mathf.Rad2Deg;
            var look = new Vector2(0, (Pitch() - degrees) / .1f);
            if (Cursor.lockState == CursorLockMode.Locked) CombatMouse(look: look);
            else Crew.Tick(new CrewCommand { Look = look }, .0001f);
            yield return null;
            yield return new WaitForEndOfFrame();
            Require(Mathf.Abs(Pitch() - degrees) < 1f, $"normal orbit positioned the player camera at {Pitch():F1}° for the blade view");
        }

        static string ActiveSlash(Animation animation)
        {
            if (IsPose(animation, "CutlassSlash")) return "CutlassSlash";
            if (IsPose(animation, "CutlassSlashRight")) return "CutlassSlashRight";
            return null;
        }

        IEnumerator CheckVoyageCombatControls()
        {
            Click(game.Menu.GetShipButton(type));
            Click(game.Menu.StartButton);
            Note("FIXTURE independent handheld-versus-cannon control case isolates the player's station and projectile.");
            using var companyFixture = new AcceptanceCompanyFixture(Ship, game.Navy);
            yield return HoldKeys(3);
            Require(!game.IsInMenu && !game.IsCombatTest && game.TestDummies.Count == 0 &&
                FindObjectsByType<CombatDummy>(FindObjectsSortMode.None).Length == 0 && game.Navy && Crew,
                "normal voyage has its navy vessel and no training targets");
            yield return KeyTap(Key.Digit1);
            CheckCombatGear(CrewWeapon.Pistol);
            yield return HoldKeys(1, Key.Space);
            Require(Crew.IsJumping && !Crew.IsAttacking && Crew.PistolCooldownRemaining == 0f,
                "Space with a pistol equipped jumps on deck without firing");
            yield return HoldKeys(40);
            Require(!Crew.IsJumping && Crew.PistolCooldownRemaining == 0f, "jump landed with pistol still ready");
            Crew.ReadDeviceInput = false;
            int gun = ApproachWeatherGun();
            Ship.Load(gun);
            Crew.ReadDeviceInput = true;
            yield return RightClickAim(true);
            yield return KeyTap(Key.E);
            var pistolSight = game.transform.Find("航海界面/航行界面/PistolSight");
            Require(Crew.IsAiming && Ship.IsLoaded(gun) && !Crew.IsPistolAiming && !pistolSight.gameObject.activeInHierarchy,
                "E entered a loaded broadside gun and cleared the handheld aiming state and sight");
            CombatMouse(right: true);
            yield return null;
            CombatMouse();
            yield return null;
            Require(Crew.IsAiming && Ship.IsLoaded(gun) && !Crew.IsPistolAiming && Crew.PistolCooldownRemaining == 0f,
                "right mouse at the gun station neither raised nor fired the handheld weapon");
            yield return HoldKeys(1, Key.Space);
            Require(Crew.IsAiming && !Crew.IsJumping && !Crew.IsAttacking && !Ship.IsLoaded(gun) &&
                FindAnyObjectByType<RoundShot>() && Crew.PistolCooldownRemaining == 0f,
                "Space at the broadside gun fired a cannonball, with neither a jump nor a pistol shot");
            yield return HoldKeys(2);
            yield return KeyTap(Key.Escape);
            Click(game.GetComponent<VoyageHud>().PauseMenuButton);
            yield return HoldKeys(2);
            Require(game.IsInMenu && !game.IsCombatTest, "ordinary voyage returned to fleet selection");
        }
    }
}
