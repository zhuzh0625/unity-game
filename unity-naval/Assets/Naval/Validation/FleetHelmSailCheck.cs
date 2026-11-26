using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Galleon
{
    public sealed partial class FleetCheck
    {
        public static void BeginHelmSail(NavalGame session, string directory)
        {
            var check = session.gameObject.AddComponent<FleetCheck>();
            check.StartCheck(session, directory, "helm-sail-check", "Helm sail and anchor", check.RunHelmSail());
        }

        IEnumerator RunHelmSail()
        {
            yield return null;
            foreach (var choice in new[] { ShipType.Sloop, ShipType.Brigantine })
            {
                type = choice;
                game.Menu.GetShipButton(type).Select();
                yield return KeyTap(Key.Enter);
                Require(game.IsInMenu && game.SelectedType == type, "keyboard selected the requested ship");
                game.Menu.StartButton.Select();
                yield return KeyTap(Key.Enter);
                // Isolate helm controls from opposing fire and company orders.
                using (new AcceptanceCompanyFixture(Ship, game.Navy))
                    yield return CheckHelmSailVoyage();
                game.ReturnToMenu();
                yield return null;
            }
            yield return CheckNavySailOrders();
        }

        IEnumerator CheckNavySailOrders()
        {
            game.StartVoyage();
            var navy = game.Navy;
            Require(Ship.Anchored && navy.Anchored && navy.Company.Captain is CrewMate,
                "both ships start at anchor with an AI captain aboard the navy ship");
            using (new AcceptanceCompanyFixture(Ship))
            {
                // Keep the player at anchor and allow the navy's regular helm and gunnery orders to run.
                bool weighed = false, stopped = false;
                for (int frame = 0; frame < 1000 && !game.IsGameOver; frame++)
                {
                    yield return null;
                    weighed |= navy.SailStep > 0 && !navy.Anchored && navy.Speed > .3f;
                    if (weighed && navy.Company.ShotsFired > 0 && navy.Anchored && navy.Speed < .03f)
                    {
                        stopped = true;
                        break;
                    }
                }
                Require(weighed && stopped && navy.SailStep == 0 && Ship.Anchored,
                    $"navy captain sailed then furled and anchored on broadside: made way={weighed}, " +
                    $"AI shots={navy.Company.ShotsFired}, sail={navy.SailStep}, speed={navy.Speed:F2} m/s");
            }
        }

        IEnumerator CheckHelmSailVoyage()
        {
            yield return new WaitForEndOfFrame();
            Require(!game.IsInMenu && !game.IsCombatTest && Ship.Definition.Type == type && Crew.ReadDeviceInput,
                "normal menu voyage uses the selected ship and live device controls");
            var sails = Ship.Rig.transform.Cast<Transform>()
                .Where(part => part.name.StartsWith("Sail", StringComparison.Ordinal)).ToArray();
            Require(sails.Length == (Small ? 2 : 4) && sails.All(sail =>
                sail.TryGetComponent<MeshRenderer>(out var renderer) && renderer.enabled && renderer.gameObject.activeInHierarchy &&
                sail.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh && filter.sharedMesh.vertexCount > 3),
                $"found {sails.Length} actual imported sail meshes");
            var start = Ship.transform.position;
            CheckHelmSailState(0, "fresh voyage");
            CheckHelmCanvas(sails, .12f);
            Require(Ship.Speed < .01f, $"fresh voyage speed is {Ship.Speed:F2} m/s");
            yield return KeyTap(Key.E);
            yield return new WaitForEndOfFrame();
            Require(Crew.IsAtHelm && Crew.StationName == "舵轮", "keyboard E took the wheel from the normal spawn");
            CheckHelmSailHint(0);

            yield return KeyTap(Key.S);
            yield return KeyTap(Key.Space);
            yield return new WaitForEndOfFrame();
            CheckHelmSailState(0, "S below minimum and Space at the wheel");
            Require(Crew.IsAtHelm && !Crew.IsJumping && HorizontalTravel(start, Ship.transform.position) < .10f,
                "Space neither jumped nor moved the stopped ship");

            yield return HoldKeys(34, Key.W);
            yield return new WaitForEndOfFrame();
            CheckHelmSailState(1, "holding the first W changed exactly one step");
            CheckHelmSailHint(1);
            CheckHelmCanvas(sails, .56f);
            float halfSpeed = Ship.Speed;
            Require(halfSpeed > .6f && HorizontalTravel(start, Ship.transform.position) > .45f,
                $"half sail weighed anchor and made way at {halfSpeed:F2} m/s");
            yield return HoldKeys(2);

            yield return KeyTap(Key.W);
            yield return HoldKeys(21);
            yield return new WaitForEndOfFrame();
            CheckHelmSailState(2, "a second W raised full sail");
            CheckHelmSailHint(2);
            CheckHelmCanvas(sails, 1f);
            Require(Ship.Speed > halfSpeed + .35f, $"full sail continued to accelerate past {halfSpeed:F2} m/s to {Ship.Speed:F2} m/s");
            yield return KeyTap(Key.W);
            yield return KeyTap(Key.Space);
            yield return new WaitForEndOfFrame();
            CheckHelmSailState(2, "W at the maximum and Space underway");
            Require(Crew.IsAtHelm && !Crew.IsJumping, "Space underway did not end helm operation or jump");
            yield return PhotographGameCamera("helm-sail-full-" + type, withHud: true);

            float priorCourse = Ship.Course;
            yield return HoldKeys(24, Key.D);
            Require(Ship.Rudder > .5f, $"D moved rudder right to {Ship.Rudder:F2}");
            yield return HoldKeys(36, Key.A);
            Require(Ship.Rudder < -.5f && Mathf.Abs(Mathf.DeltaAngle(priorCourse, Ship.Course)) > .2f,
                $"A moved rudder left to {Ship.Rudder:F2} and the ship turned");
            yield return HoldKeys(2);
            yield return new WaitForEndOfFrame();
            CheckHelmSailState(2, "A/D steering preserved full sail and raised anchor");

            yield return HoldKeys(21, Key.S);
            yield return new WaitForEndOfFrame();
            CheckHelmSailState(1, "holding the first S reefed only one step without anchoring");
            CheckHelmCanvas(sails, .56f);
            yield return HoldKeys(2);
            yield return KeyTap(Key.S);
            yield return new WaitForEndOfFrame();
            CheckHelmSailState(0, "a second S stowed the sails and dropped anchor");
            float brakingSpeed = Ship.Speed;
            yield return HoldKeys(65);
            yield return new WaitForEndOfFrame();
            Require(brakingSpeed > .45f && Ship.Speed < .03f,
                $"anchored ship braked from {brakingSpeed:F2} m/s to {Ship.Speed:F2} m/s");
            CheckHelmCanvas(sails, .12f);
            var stop = Ship.transform.position;
            yield return HoldKeys(12);
            Require(HorizontalTravel(stop, Ship.transform.position) < .12f, "the stopped hull stayed at anchor");

            yield return KeyTap(Key.F1);
            yield return new WaitForEndOfFrame();
            var help = game.transform.Find("航海界面/航行界面/暂停和操作/键位").GetComponent<TMP_Text>();
            string wheel = help.GetParsedText().Split('\n').FirstOrDefault(line => line.Contains("舵轮")) ?? "";
            Require(Time.timeScale == 0f && help.isActiveAndEnabled && wheel.Contains("W") && wheel.Contains("S") &&
                wheel.Contains("起锚") && (wheel.Contains("下锚") || wheel.Contains("落锚") || wheel.Contains("抛锚")) &&
                !wheel.Contains("空格"), $"F1 explains the updated combined sail-and-anchor controls: {wheel}");
            yield return KeyTap(Key.F1);
            Require(Time.timeScale == 1f, "F1 resumed the voyage");

            yield return KeyTap(Key.W);
            yield return KeyTap(Key.E);
            yield return new WaitForEndOfFrame();
            Require(!Crew.IsAtHelm && Crew.StationName.Length == 0, "keyboard E left the helm");
            CheckHelmSailState(1, "leaving the helm kept half sail and raised anchor");
            // Take a side step to clear the wheel pedestal, then walk forward with the former sail key.
            yield return HoldKeys(5, Key.D);
            yield return HoldKeys(2);
            Vector3 foot = CrewOnShip;
            yield return HoldKeys(9, Key.W);
            yield return HoldKeys(2);
            yield return new WaitForEndOfFrame();
            Require(!Crew.IsSwimming && HorizontalTravel(foot, CrewOnShip) > .12f,
                $"off-helm W walked {HorizontalTravel(foot, CrewOnShip):F2} m on deck");
            CheckHelmSailState(1, "off-helm W left the sailing state unchanged");
        }

        void CheckHelmSailState(int step, string action)
        {
            string sail = step switch { 0 => "收帆", 1 => "半帆", _ => "满帆" };
            bool anchored = step == 0;
            string anchor = anchored ? "锚已落" : "锚已起";
            var readout = game.transform.Find("航海界面/航行界面/航行状态/船况").GetComponent<TMP_Text>();
            string text = readout.GetParsedText();
            Require(Ship.SailStep == step && Ship.Anchored == anchored && readout.isActiveAndEnabled &&
                text.Contains(sail) && text.Contains(anchor) && text.Contains("节"),
                $"{action}: sail step {Ship.SailStep}, anchored={Ship.Anchored}, HUD {text}");
        }

        void CheckHelmCanvas(Transform[] sails, float drop)
        {
            Require(sails.All(sail => Mathf.Abs(sail.localScale.y - drop) < .025f &&
                Mathf.Abs(sail.localScale.x - 1f) < .01f && Mathf.Abs(sail.localScale.z - 1f) < .01f),
                $"imported sails reached drop {drop:F2}: {string.Join(", ", sails.Select(sail => sail.name + "=" + sail.localScale.y.ToString("F2")))}");
        }

        void CheckHelmSailHint(int step)
        {
            string hint = Crew.Prompt;
            Require(hint.Contains("W") == (step < 2) && hint.Contains("S") == (step > 0) &&
                hint.Contains("帆") && !hint.Contains("空格"), $"wheel prompt at sail step {step}: {hint}");
        }
    }
}
