using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Galleon
{
    public sealed partial class FleetCheck
    {
        public static void BeginMedicine(NavalGame session, string directory)
        {
            var check = session.gameObject.AddComponent<FleetCheck>();
            check.StartCheck(session, directory, "medicine-check", "Crew medicine", check.RunMedicine());
        }

        IEnumerator RunMedicine()
        {
            yield return null;
            type = ShipType.Brigantine;
            Require(game.IsInMenu && game.Menu.CombatButton.isActiveAndEnabled, "combat test is available from the fleet menu");
            Click(game.Menu.CombatButton);
            yield return HoldKeys(3);
            Require(game.IsCombatTest && !game.IsInMenu && Ship.Definition.Type == type && Crew.ReadDeviceInput,
                "started the brigantine combat test with live device input");

            combatMouse = InputSystem.AddDevice<Mouse>();
            try
            {
                CheckMedicineGear(CrewWeapon.None);
                Crew.TakeDamage(75);
                Require(Crew.Health == 25, "controlled starting injury reduced crew health to 25");
                yield return KeyTap(Key.Digit3);
                var bottle = CheckMedicineGear(CrewWeapon.Medicine);
                yield return PhotographMedicineBottle(bottle);

                CombatMouse(right: true);
                yield return null;
                CombatMouse();
                yield return null;
                Require(!Crew.IsDrinkingMedicine && !Crew.IsPistolAiming && Crew.Health == 25,
                    "right click with the bottle did not drink or raise the pistol");

                Vector3 start = CrewOnShip;
                Keyboard(Key.A);
                CombatMouse(left: true);
                yield return null;
                double startedAt = Time.timeAsDouble;
                Require(Crew.IsDrinkingMedicine && Crew.Health == 25, "left-click started drinking without immediate healing");
                yield return PhotographGameCamera("medicine-drinking", withHud: true);
                var label = game.transform.Find("航海界面/航行界面/CrewWeapon/EquippedWeapon").GetComponent<TMP_Text>();
                var health = game.transform.Find("航海界面/航行界面/CrewHealth/CrewHealthValue").GetComponent<TMP_Text>();
                Require(label.isActiveAndEnabled && label.GetParsedText().Contains("正在喝药") &&
                    health.isActiveAndEnabled && health.GetParsedText().Replace(" ", "") == "25/100",
                    "HUD shows drinking in progress and health at 25/100");
                yield return HoldKeys(5, Key.A);
                Require(Crew.IsDrinkingMedicine && Crew.Health == 25 && Vector3.Distance(start, CrewOnShip) > .20f &&
                    SailorDrawable(bottle), $"the crew moved {Vector3.Distance(start, CrewOnShip):F2} m while drinking and holding the bottle");
                yield return HoldKeys(14);
                Require(Crew.IsDrinkingMedicine && Crew.Health == 25 && Time.timeAsDouble - startedAt < .99d,
                    $"health stayed at 25 before one second ({Time.timeAsDouble - startedAt:F2}s)");
                yield return null;
                Require(!Crew.IsDrinkingMedicine && Crew.Health == 75 && Time.timeAsDouble - startedAt >= .99d,
                    $"healed exactly 50 on the one-second frame ({Time.timeAsDouble - startedAt:F2}s)");
                yield return HoldKeys(21);
                Require(!Crew.IsDrinkingMedicine && Crew.Health == 75, "holding the left button did not queue another drink");

                CombatMouse();
                yield return null;
                yield return PressMedicine();
                yield return HoldKeys(20);
                Require(!Crew.IsDrinkingMedicine && Crew.Health == 100, "a second drink capped crew health at 100");

                Crew.TakeDamage(24);
                yield return PressMedicine();
                yield return KeyTap(Key.Digit3);
                CheckMedicineGear(CrewWeapon.None);
                yield return HoldKeys(20);
                Require(!Crew.IsDrinkingMedicine && Crew.Health == 76, "pressing 3 again interrupted the drink and stowed the bottle");

                yield return KeyTap(Key.Digit3);
                yield return PressMedicine();
                yield return KeyTap(Key.Digit2);
                CheckMedicineGear(CrewWeapon.Cutlass);
                yield return HoldKeys(20);
                Require(!Crew.IsDrinkingMedicine && Crew.Health == 76 && !Crew.IsAttacking,
                    "switching to the cutlass canceled the drink without triggering an attack");
                yield return KeyTap(Key.Digit1);
                CheckMedicineGear(CrewWeapon.Pistol);
                Require(game.TestDummies[0].Health == 100 && game.TestDummies[1].Health == 100 && Crew.PistolCooldownRemaining == 0f,
                    "medicine and selection did not damage the targets or fire the pistol");
            }
            finally
            {
                InputSystem.RemoveDevice(combatMouse);
                combatMouse = null;
            }
        }

        IEnumerator PressMedicine()
        {
            CombatMouse(left: true);
            yield return null;
            Require(Crew.IsDrinkingMedicine, "fresh left click started a drink with the bottle selected");
            CombatMouse();
            yield return null;
        }

        MeshRenderer CheckMedicineGear(CrewWeapon expected)
        {
            var props = CombatAnimation.GetComponentsInChildren<MeshRenderer>(true);
            var bottle = Array.Find(props, renderer => renderer.name == "MedicineBottle");
            var pistol = Array.Find(props, renderer => renderer.name == "Pistol");
            var cutlass = Array.Find(props, renderer => renderer.name == "Cutlass");
            Require(Crew.Weapon == expected && bottle && pistol && cutlass &&
                SailorDrawable(bottle) == (expected == CrewWeapon.Medicine) &&
                SailorDrawable(pistol) == (expected == CrewWeapon.Pistol) &&
                SailorDrawable(cutlass) == (expected == CrewWeapon.Cutlass),
                $"{expected} selection made only its matching held prop drawable");
            return bottle;
        }

        IEnumerator PhotographMedicineBottle(MeshRenderer bottle)
        {
            var camera = game.MainCamera;
            var view = camera.transform;
            Vector3 originalPosition = view.position;
            Quaternion originalRotation = view.rotation;
            float originalFieldOfView = camera.fieldOfView;
            var stance = CombatAnimation.transform.parent;
            Vector3 centre = bottle.bounds.center + stance.up * .08f;
            Vector3 eye = centre + stance.right * .85f + stance.forward * .76f + stance.up * .17f;
            Crew.enabled = false;
            try
            {
                camera.fieldOfView = 48f;
                view.SetPositionAndRotation(eye, Quaternion.LookRotation(centre - eye, stance.up));
                Require(SailorOnCamera(bottle), "held bottle lies in the close-up camera view");
                yield return PhotographGameCamera("medicine-bottle", bottle: bottle);
            }
            finally
            {
                view.SetPositionAndRotation(originalPosition, originalRotation);
                camera.fieldOfView = originalFieldOfView;
                Crew.enabled = true;
            }
        }

        IEnumerator PhotographGameCamera(string label, bool withHud = false, Renderer bottle = null)
        {
            var camera = game.MainCamera;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var canvas = withHud ? game.transform.Find("航海界面").GetComponent<Canvas>() : null;
            RenderMode previousMode = canvas ? canvas.renderMode : default;
            Camera previousCamera = canvas ? canvas.worldCamera : null;
            float previousDistance = canvas ? canvas.planeDistance : 0f;
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            Texture2D readback = null;
            try
            {
                Require(target.Create(), "created the offscreen camera target for " + label);
                camera.targetTexture = target;
                if (canvas)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = .5f;
                    Canvas.ForceUpdateCanvases();
                }
                yield return new WaitForEndOfFrame();
                // A hidden Windows player can skip presenting its frame. Render this camera explicitly.
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                readback = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                readback.Apply(false);
                var colors = new HashSet<Color32>();
                var pixels = readback.GetPixels32();
                for (int i = 0; i < pixels.Length; i += 283) colors.Add(pixels[i]);
                File.WriteAllBytes(Path.Combine(output, label + ".png"), readback.EncodeToPNG());
                Require(colors.Count > 30, $"rendered {label} offscreen at {target.width}x{target.height}, sampled colors={colors.Count}");
                if (bottle) CheckBottleLabel(camera, bottle.bounds, pixels, readback.width, readback.height);
            }
            finally
            {
                if (canvas)
                {
                    canvas.renderMode = previousMode;
                    canvas.worldCamera = previousCamera;
                    canvas.planeDistance = previousDistance;
                }
                RenderTexture.active = previousActive;
                camera.targetTexture = previousTarget;
                if (readback) Destroy(readback);
                target.Release();
                Destroy(target);
            }
        }

        void CheckBottleLabel(Camera camera, Bounds bounds, Color32[] pixels, int width, int height)
        {
            Vector2 min = Vector2.one, max = Vector2.zero;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                Vector2 view = camera.WorldToViewportPoint(corner);
                min = Vector2.Min(min, view);
                max = Vector2.Max(max, view);
            }
            int red = 0;
            for (int y = Mathf.Clamp((int)(min.y * height), 0, height); y < Mathf.Min(max.y * height, height); y++)
            for (int x = Mathf.Clamp((int)(min.x * width), 0, width); x < Mathf.Min(max.x * width, width); x++)
            {
                Color32 c = pixels[y * width + x];
                if (c.r > 45 && c.r > 3 * c.g && c.r > 3 * c.b) red++;
            }
            Require(red >= 12, $"visible red cross on the held bottle ({red} red pixels within its projected bounds)");
        }
    }
}
