using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Role = Galleon.CrewPlayer.Role;

namespace Galleon
{
    public sealed partial class FleetCheck
    {
        public static void BeginCompany(NavalGame session, string directory)
        {
            var check = session.gameObject.AddComponent<FleetCheck>();
            bool helmOnly = Array.IndexOf(Environment.GetCommandLineArgs(), "-company-helm-only") >= 0;
            check.StartCheck(session, directory, helmOnly ? "company-helm-check" : "company-check", "Ship company",
                check.RunCompany(helmOnly));
        }

        IEnumerator RunCompany(bool helmOnly)
        {
            bool fireOnly = Array.IndexOf(Environment.GetCommandLineArgs(), "-company-fire-only") >= 0;
            Note("Real Input System keyboard and mouse events operate the built voyage. Controlled scenarios below identify the held target, test damage, and independent station positions.");
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return null;
                Require(game.IsInMenu && !Ship && !Crew, "ordinary startup shows fleet selection");
                foreach (var choice in new[] { ShipType.Sloop, ShipType.Brigantine })
                {
                    type = choice;
                    Click(game.Menu.GetShipButton(type));
                    Require(game.IsInMenu && game.SelectedType == type, "ship selected in ordinary fleet menu");
                    if (helmOnly)
                    {
                        Click(game.Menu.StartButton);
                        CheckVoyagePair();
                        yield return CompanyPlayerHelm(false);
                        yield return CompanyBackToMenu();
                        continue;
                    }
                    if (!fireOnly)
                    {
                        yield return Photograph("company-menu-" + type);
                        Click(game.Menu.StartButton);
                        yield return CompanyUnderWay();
                        yield return CompanyBackToMenu();
                    }

                    Click(game.Menu.StartButton);
                    CheckVoyagePair();
                    using (new AcceptanceCompanyFixture(game.Navy))
                    {
                        CheckNewVoyageGuns();
                        Note("FIXTURE the enemy remains at its ordinary spawn anchor while the pirate crew staffs and fires its full starboard battery.");
                        var target = game.Navy.Flooding;
                        target.enabled = false; // Continue to receive real impacts without ending the measured cases.
                        try
                        {
                            yield return CompanyBroadside(1, !fireOnly);
                            yield return CompanyMirrorBroadside(!fireOnly);
                            yield return CompanyIndependentGuns();
                            yield return CompanyGunnerRelief();
                            if (!fireOnly) yield return CompanyFormerRigging();
                            yield return CompanyPlayerGun(mouse);
                            if (!fireOnly)
                            {
                                yield return CompanyDamageControl();
                                yield return CompanyPlayerHelm(true);
                                yield return CompanyBoardEnemy();
                            }
                        }
                        finally
                        {
                            if (target) target.enabled = true;
                        }
                    }
                    yield return CompanyBackToMenu();

                    if (!fireOnly)
                    {
                        Click(game.Menu.StartButton);
                        using (new AcceptanceCompanyFixture(Ship))
                        {
                            Note("FIXTURE the pirate ship is an underway target with its battery silent and flooding progression paused during the navy handovers.");
                            var target = Ship.Flooding;
                            target.enabled = false;
                            Ship.SetSailStep(2);
                            try
                            {
                                yield return CompanySuccession(game.Navy);
                            }
                            finally
                            {
                                if (target) target.enabled = true;
                            }
                        }
                        yield return CompanyBackToMenu();
                    }

                    Click(game.Menu.StartButton);
                    yield return CompanyLastSailor(Small ? Ship : game.Navy, Small ? game.Navy : Ship);
                    yield return CompanyBackToMenu();
                }
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
            }
        }

        IEnumerator CompanyGunnerRelief()
        {
            var company = Ship.Company;
            var available = company.Mates.Where(m => m.Duty == CrewDuty.DamageControl && m.Health > 0).ToArray();
            Require(available.Length > 0, "full staffed battery has a damage-control reserve");
            var gunner = company.Mates.Where(m => m.Duty == CrewDuty.Gunner && m.AtPost && m.Job == Role.Gun)
                .OrderBy(m => available.Min(sailor => Vector3.Distance(sailor.transform.position, m.transform.position)))
                .FirstOrDefault();
            Require(gunner, "battery has a working gunner to relieve");
            int gun = gunner.JobIndex;
            int previous = company.ShotsFiredByGun(gun);
            Note($"FIXTURE gunner {gunner.Slot} takes a controlled casualty at cannon {gun}, left loaded with a target in arc.");
            Ship.Load(gun);
            Require(Ship.IsLoaded(gun) && company.TryAim(gun, out _), $"relief gun {gun} is ready");
            gunner.TakeDamage(CrewPlayer.MaxHealth);
            CrewMate replacement = null;
            for (int frame = 0; frame < 198 && gunner.Health == 0; frame++)
            {
                replacement = company.Mates.FirstOrDefault(m => m != gunner && m.Job == Role.Gun && m.JobIndex == gun && m.AtPost);
                if (replacement && company.ShotsFiredByGun(gun) > previous) break;
                yield return null;
            }
            Require(gunner.Health == 0 && replacement && company.ShotsFiredByGun(gun) > previous &&
                Vector3.Distance(replacement.transform.position, Ship.Rig.Guns[gun].position) < 1.5f,
                $"relief sailor {replacement?.Slot} ({replacement?.Duty}) reaches the downed gunner's cannon {gun} and fires");
            for (int frame = 0; frame < 215 && gunner.Health == 0; frame++) yield return null;
            Require(gunner.Health == CrewPlayer.MaxHealth, "gunner returns after the support case before the next casualty drill");
        }

        IEnumerator CompanyBackToMenu()
        {
            if (!game.IsGameOver)
            {
                yield return KeyTap(Key.Escape);
                Require(Time.timeScale == 0f, "Escape opens voyage pause");
            }
            var hud = game.GetComponent<VoyageHud>();
            Click(game.IsGameOver ? hud.DefeatMenuButton : hud.PauseMenuButton);
            yield return null;
            Require(game.IsInMenu && !Ship && !Crew && game.SelectedType == type && Time.timeScale == 1f,
                "menu action retires the voyage and keeps the selected ship");
        }

        void CompanyManifest(NavalShip ship, int guns, int damage, CrewDamageTarget captain)
        {
            ShipCompany company = ship.Company;
            Require(company && captain && company.Captain == captain && ship.GetComponentsInChildren<CrewDamageTarget>().Length == company.Capacity &&
                company.Capacity == 1 + guns + damage && company.GunnerCount == guns && company.DamageControlCount == damage &&
                company.Mates.Count == guns + damage + (captain is CrewMate ? 1 : 0),
                $"{ship.name} roster has 1 captain, {guns} gunners and {damage} damage control");
            var bodies = new HashSet<Vector3>();
            foreach (var mate in company.Mates)
            {
                Require(mate && mate.Health == CrewPlayer.MaxHealth && mate.transform.parent == ship.transform &&
                    bodies.Add(mate.transform.localPosition) &&
                    mate.GetComponentInChildren<SkinnedMeshRenderer>() &&
                    company.CanSteer(mate) == (mate == captain),
                    $"{ship.name} slot {mate.Slot} duty={mate.Duty} physical crew at {mate.transform.localPosition:F2} with helm={company.CanSteer(mate)}");
            }
            Require(company.Mates.Count(m => m.Duty == CrewDuty.Gunner) == guns &&
                company.Mates.Count(m => m.Duty == CrewDuty.DamageControl) == damage &&
                company.ActingCaptain == null && company.CanSteer(captain),
                $"{ship.name} crew duties and captain-only helm rights match the manifest");
        }

        IEnumerator CompanyUnderWay()
        {
            CheckVoyagePair();
            var enemy = game.Navy;
            var navyCaptain = enemy.Company.Captain as CrewMate;
            int gunners = Small ? 4 : 9;
            int damageControl = Small ? 1 : 2;
            CompanyManifest(Ship, gunners, damageControl, Crew);
            CompanyManifest(enemy, gunners, damageControl, navyCaptain);
            Vector3 enemyStart = enemy.transform.position;
            var playerStart = Ship.transform.position;
            float heading = Ship.Course;
            yield return null;
            yield return KeyTap(Key.E);
            Require(Crew.StationName == "舵轮" && Ship.Anchored && Ship.SailStep == 0,
                "player takes the wheel with anchor down and sail furled");
            yield return KeyTap(Key.Space);
            Require(Crew.StationName == "舵轮" && Ship.Anchored && Ship.SailStep == 0 && !Crew.IsJumping,
                "Space leaves the anchored ship and occupied wheel unchanged");
            yield return KeyTap(Key.W);
            Require(!Ship.Anchored && Ship.SailStep == 1, "W weighs anchor and sets half sail");
            yield return KeyTap(Key.W);
            Require(!Ship.Anchored && Ship.SailStep == 2, "a second W sets full sail");
            yield return HoldKeys(24, Key.D);
            float right = Ship.Rudder;
            yield return HoldKeys(24, Key.A);
            float left = Ship.Rudder;
            yield return HoldKeys(1);
            Require(right > .4f && left < -.3f &&
                HorizontalTravel(playerStart, Ship.transform.position) > .7f && Ship.Speed > .5f &&
                Mathf.Abs(Mathf.DeltaAngle(heading, Ship.Course)) > .5f,
                $"normal helm moves the ship, rudder right={right:F2} left={left:F2} course={Ship.Course:F1} speed={Ship.Speed:F2}");
            yield return Photograph("company-helm-" + type);
            yield return KeyTap(Key.S);
            Require(!Ship.Anchored && Ship.SailStep == 1, "S reefs to half sail and leaves the ship under way");
            yield return KeyTap(Key.S);
            Require(Ship.Anchored && Ship.SailStep == 0, "a second S furls sail and lowers anchor");
            yield return KeyTap(Key.E);
            yield return HoldKeys(48);
            var helm = enemy.Rig.Helm;
            Require(Crew.StationName.Length == 0 && Ship.Company.ActingCaptain == null &&
                !Ship.Company.Mates.Any(mate => mate.Job == Role.Helm) && Ship.Anchored &&
                Ship.SailStep == 0 && Mathf.Abs(Ship.Rudder) < .03f,
                $"living pirate captain steps away and leaves the helm vacant at sail step {Ship.SailStep}");
            Require(navyCaptain && navyCaptain.AtPost && navyCaptain.Job == Role.Helm &&
                Vector3.Distance(navyCaptain.transform.position, helm.position) < 2f &&
                HorizontalTravel(enemyStart, enemy.transform.position) > 1f,
                $"original navy captain steers at the physical wheel, anchor={enemy.Anchored} sail={enemy.SailStep} course={enemy.Course:F1} displacement={HorizontalTravel(enemyStart, enemy.transform.position):F2}m");
            for (int frame = 0; frame < 560 && enemy.Company.ShotsFired == 0 && !game.IsGameOver; frame++)
                yield return null;
            Require(enemy.Company.ShotsFired > 0,
                $"moving navy crew engages from a working battery, shots={enemy.Company.ShotsFired}");
            yield return Photograph("company-sea-battle-" + type);
        }

        static float HorizontalTravel(Vector3 before, Vector3 after) =>
            Vector3.ProjectOnPlane(after - before, Vector3.up).magnitude;

        int CompanyMannedGuns(int side)
        {
            var ship = Ship;
            return ship.Company.Mates.Where(mate => mate.Job == Role.Gun && mate.AtPost && mate.Health > 0)
                .Where(mate =>
                {
                    var gun = ship.Rig.Guns[mate.JobIndex];
                    return Vector3.Dot(gun.forward, ship.transform.right) * side > .5f &&
                        Vector3.Distance(mate.transform.position, gun.position) < 1.5f;
                })
                .Select(mate => mate.JobIndex).Distinct().Count();
        }

        IEnumerator CompanyBroadside(int side, bool photograph)
        {
            ShipCompany company = Ship.Company;
            var guns = CompanySideGuns(Ship, side);
            Require(guns.Length == (Small ? 4 : 9), $"fixture has {guns.Length} guns on side {side}");
            string battery = side > 0 ? "starboard" : "port";
            var previous = guns.ToDictionary(gun => gun, company.ShotsFiredByGun);
            int previousHits = Ship.HitsLanded;
            var staffed = new HashSet<int>();
            bool liveShot = false;
            for (int frame = 0; frame < 540; frame++)
            {
                yield return null;
                foreach (var mate in company.Mates)
                    if (mate.Job == Role.Gun && mate.AtPost && guns.Contains(mate.JobIndex) &&
                        Vector3.Distance(mate.transform.position, Ship.Rig.Guns[mate.JobIndex].position) < 1.5f)
                        staffed.Add(mate.JobIndex);
                if (!liveShot && company.LastShotSide == side)
                    liveShot = FindObjectsByType<RoundShot>(FindObjectsSortMode.None)
                        .Any(shot => Ship.transform.InverseTransformPoint(shot.transform.position).x * side > 0f);
                if (staffed.Count == guns.Length && guns.All(gun => company.ShotsFiredByGun(gun) > previous[gun]))
                    break;
            }
            var fired = guns.Select(gun => company.ShotsFiredByGun(gun) - previous[gun]).ToArray();
            Require(staffed.Count == guns.Length && fired.All(count => count > 0) && liveShot && company.LastShotSide == side,
                $"{battery} battery physically staffs {staffed.Count}/{guns.Length} guns, launches per gun={string.Join(",", fired)}, live round={liveShot}");
            if (photograph)
                yield return Photograph((side > 0 ? "company-full-battery-" : "company-port-battery-") + type);
            for (int frame = 0; frame < 130 && (Ship.HitsLanded <= previousHits ||
                     !guns.Any(gun => company.ShotsFiredByGun(gun) >= previous[gun] + 2)); frame++)
                yield return null;
            Require(Ship.HitsLanded > previousHits && guns.Any(gun => company.ShotsFiredByGun(gun) >= previous[gun] + 2),
                $"{battery} registers real hits={Ship.HitsLanded - previousHits} and a crewed gun reloads and fires again");
        }

        static int[] CompanySideGuns(NavalShip vessel, int side) =>
            Enumerable.Range(0, vessel.Rig.Guns.Length)
                .Where(i => Vector3.Dot(vessel.Rig.Guns[i].forward, vessel.transform.right) * side > .5f).ToArray();

        IEnumerator CompanyIndependentGuns()
        {
            var company = Ship.Company;
            int[] readyPosts = Array.Empty<int>();
            for (int frame = 0; frame < 60 && readyPosts.Length < 2; frame++)
            {
                yield return null;
                readyPosts = company.Mates.Where(m => m.Job == Role.Gun && m.AtPost && !Ship.IsLoaded(m.JobIndex) &&
                    company.TryAim(m.JobIndex, out _)).Select(m => m.JobIndex).Take(2).ToArray();
            }
            Require(readyPosts.Length == 2, "two empty guns have independent operators in place and a target in arc");
            Note("FIXTURE completes two loads one frame apart while the remaining gun crews run normally.");
            foreach (int gun in readyPosts)
            {
                int previous = company.ShotsFiredByGun(gun);
                Ship.Load(gun);
                yield return null;
                Require(company.ShotsFiredByGun(gun) == previous + 1,
                    $"staffed gun {gun} fires on its first update after becoming ready");
            }
        }

        IEnumerator CompanyLastSailor(NavalShip vessel, NavalShip target)
        {
            var company = vessel.Company;
            var survivor = vessel.IsPirate ? company.Mates.Single(m => m.Duty == CrewDuty.DamageControl) :
                (CrewMate)company.Captain;
            var casualties = company.Mates.Where(m => m != survivor).Cast<CrewDamageTarget>().ToList();
            if (vessel.IsPirate) casualties.Add(Crew);
            using var silentTarget = new AcceptanceCompanyFixture(target);
            Vector3 targetPosition = target.transform.position;
            target.enabled = false;
            target.Flooding.enabled = false;
            try
            {
                Note($"FIXTURE {vessel.name} keeps only {survivor.Duty} slot {survivor.Slot} alive. Fallen crew updates are paused to hold respawns during the drill.");
                foreach (var casualty in casualties)
                {
                    casualty.TakeDamage(CrewPlayer.MaxHealth);
                    casualty.enabled = false;
                }
                Require(casualties.All(m => m.Health == 0) && survivor.Health > 0 && company.CanSteer(survivor),
                    $"{vessel.name} sole survivor holds {(vessel.IsPirate ? "deputy" : "captain")} helm authority");
                int side = Vector3.Dot(target.transform.position - vessel.transform.position, vessel.transform.right) > 0f ? 1 : -1;
                var guns = CompanySideGuns(vessel, side);
                foreach (int gun in guns) vessel.Load(gun);
                Require(guns.Length == company.GunnerCount && guns.All(gun => vessel.IsLoaded(gun) && company.TryAim(gun, out _)),
                    $"{vessel.name} starts with all {guns.Length} guns loaded and in arc on the engaged side");

                var previous = guns.ToDictionary(gun => gun, company.ShotsFiredByGun);
                var fired = new HashSet<int>();
                double deadline = Time.timeAsDouble + 100;
                while (fired.Count < guns.Length && Time.timeAsDouble < deadline)
                {
                    int previousPost = survivor.Job == Role.Gun && survivor.AtPost ? survivor.JobIndex : -1;
                    yield return null;
                    foreach (int gun in guns)
                    {
                        int count = company.ShotsFiredByGun(gun) - previous[gun];
                        if (count == 0 || fired.Contains(gun) && count == 1) continue;
                        bool atGun = previousPost == gun || survivor.Job == Role.Gun && survivor.JobIndex == gun && survivor.AtPost;
                        Require(count == 1 && fired.Add(gun) && atGun &&
                            Vector3.Distance(survivor.transform.position, vessel.Rig.Guns[gun].position) < 1.5f,
                            $"{vessel.name} slot {survivor.Slot} at gun {gun}, launches={count}, distinct guns={fired.Count}/{guns.Length}");
                    }
                    if (fired.Count < guns.Length && fired.Any(vessel.IsLoaded))
                        Require(false, $"{vessel.name} survivor reloaded a spent gun while preloaded guns remained");
                }
                Require(fired.Count == guns.Length,
                    $"{vessel.name} single sailor walked and fired {fired.Count}/{guns.Length} distinct preloaded guns");
                target.transform.position += vessel.transform.right * (side * (NavalShip.MaxShotRange + 100f));
                Physics.SyncTransforms();
                Require(guns.All(gun => !company.TryAim(gun, out _)), $"{vessel.name} target moves out of range of every gun");
                for (int frame = 0; frame < 700 && !(survivor.Job == Role.Helm && survivor.AtPost); frame++) yield return null;
                Require(company.CanSteer(survivor) && survivor.Job == Role.Helm && survivor.AtPost &&
                    Vector3.Distance(survivor.transform.position, vessel.Rig.Helm.position) < 2f,
                    $"{vessel.name} sole sailor returns to the wheel when guns lose their target");
            }
            finally
            {
                foreach (var casualty in casualties) if (casualty) casualty.enabled = true;
                target.transform.position = targetPosition;
                target.enabled = true;
                target.Flooding.enabled = true;
                Physics.SyncTransforms();
            }
        }

        IEnumerator CompanyMirrorBroadside(bool photograph)
        {
            var navy = game.Navy;
            var target = navy.transform;
            Vector3 originalPosition = target.position;
            Quaternion originalRotation = target.rotation;
            bool originalEnabled = navy.enabled;
            Vector3 right = Quaternion.Euler(0f, Ship.Course, 0f) * Vector3.right;
            float across = Vector3.Dot(originalPosition - Ship.transform.position, right);
            Require(Ship.Anchored && navy.Anchored && across > 0f && Ship.Company.LastShotSide == 1,
                $"mirrored battery calibration starts with both ships at anchor and the target {across:F2}m to starboard");

            navy.enabled = false;
            try
            {
                Vector3 mirroredPosition = originalPosition - 2f * across * right;
                target.SetPositionAndRotation(mirroredPosition, originalRotation);
                Physics.SyncTransforms();
                Note($"FIXTURE moved the anchored target across the player's centreline from {originalPosition:F2} to {mirroredPosition:F2}; hull heading retained.");
                yield return CompanyBroadside(-1, photograph);
            }
            finally
            {
                if (navy)
                {
                    target.SetPositionAndRotation(originalPosition, originalRotation);
                    navy.enabled = originalEnabled;
                    Physics.SyncTransforms();
                    Note($"FIXTURE restored the anchored target at {target.position:F2} with motion enabled={navy.enabled}.");
                }
            }
            int expected = Small ? 4 : 9;
            for (int frame = 0; frame < 540 && CompanyMannedGuns(1) < expected; frame++)
                yield return null;
            Require(CompanyMannedGuns(1) == expected &&
                Vector3.Dot(target.position - Ship.transform.position, right) > 0f,
                "crew returns to the full starboard battery at the original target before the remaining cases");
        }

        IEnumerator CompanyPlayerGun(Mouse mouse)
        {
            var company = Ship.Company;
            Crew.ReadDeviceInput = false;
            int gun = ApproachWeatherGun();
            Crew.ReadDeviceInput = true;
            for (int frame = 0; frame < 360 && !company.Mates.Any(m => m.Job == Role.Gun && m.JobIndex == gun && m.AtPost); frame++)
                yield return null;
            Require(company.Mates.Any(m => m.Job == Role.Gun && m.JobIndex == gun && m.AtPost),
                $"AI sailor physically occupies gun {gun} before the player's E input");
            yield return KeyTap(Key.E);
            Require(Crew.IsAiming && !company.Mates.Any(m => m.Job == Role.Gun && m.JobIndex == gun),
                "living captain can take a working cannon and its sailor yields the physical post");
            int aiShots = company.ShotsFiredByGun(gun);
            for (int i = 0; i < CannonLoadFrames && !Ship.IsLoaded(gun); i++) yield return null;
            Require(Ship.IsLoaded(gun), "captain can load the cannon at the station");
            float yaw = Crew.GunYaw;
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(35f, -13f) });
            yield return null;
            Require(Mathf.Abs(Crew.GunYaw - yaw) > 1f, $"mouse moves the captain's gun sight to {Crew.GunYaw:F1} degrees");
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left, true));
            yield return null;
            bool fired = !Ship.IsLoaded(gun);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
            Require(fired && Crew.IsAiming && company.ShotsFiredByGun(gun) == aiShots,
                "left mouse fires the occupied cannon through normal input while AI leaves the player's gun alone");
            yield return KeyTap(Key.E);
        }

        IEnumerator CompanyDamageControl()
        {
            var company = Ship.Company;
            var flooding = Ship.Flooding;
            using (new AcceptanceCompanyFixture(Ship))
            {
                Note("FIXTURE damage drill preserves the two original holes while the captain demonstrates independent repair and pump controls.");
                yield return KeyTap(Key.F6);
                Require(flooding.ActiveLeaks == 2, $"F6 drills two real breaches, observed {flooding.ActiveLeaks}");
                foreach (int index in new[] { 0, Ship.Rig.LeakInside.Length - 1 })
                {
                    Crew.ReadDeviceInput = false;
                    yield return ApproachRepairSite(index);
                    Crew.ReadDeviceInput = true;
                    yield return KeyTap(Key.E);
                    Require(Crew.StationName == "船壳破口", $"captain can take repair site {index} using E");
                    if (index == 0) yield return Photograph("company-captain-repair-" + type);
                    for (int frame = 0; frame < HullRepairFrames && flooding.IsLeaking(index); frame++) yield return null;
                    Require(!flooding.IsLeaking(index), $"captain seals breach {index}");
                }
                Crew.ReadDeviceInput = false;
                ApproachPump();
                Crew.ReadDeviceInput = true;
                yield return KeyTap(Key.E);
                Require(Crew.StationName == "舱底泵" && flooding.WaterLevel01 > 0f, "captain can operate the pump with residual water");
                for (int frame = 0; frame < 180 && flooding.WaterLevel01 > 0f; frame++) yield return null;
                Require(flooding.WaterLevel01 == 0f && !flooding.WaterVisible, "captain's pump clears the hold");
            }

            Note("FIXTURE temporarily wounds the dedicated damage-control hands before F6; ordinary crew decide how to support the repair and pump.");
            foreach (var mate in company.Mates.Where(m => m.Duty == CrewDuty.DamageControl))
                mate.TakeDamage(CrewPlayer.MaxHealth);
            yield return KeyTap(Key.F6);
            Require(flooding.ActiveLeaks == 2, "support drill starts with two open repair sites");
            bool supportingRepair = false, pump = false;
            float peakWater = flooding.WaterHeight;
            for (int frame = 0; frame < 620 && (flooding.ActiveLeaks > 0 || flooding.WaterLevel01 > 0); frame++)
            {
                supportingRepair |= company.Mates.Any(m => m.Duty == CrewDuty.Gunner && m.Job == Role.Leak &&
                    m.AtPost && flooding.IsLeaking(m.JobIndex));
                pump |= company.Mates.Any(m => m.Job == Role.Pump && m.AtPost);
                peakWater = Mathf.Max(peakWater, flooding.WaterHeight);
                yield return null;
            }
            Require(supportingRepair && pump && peakWater > flooding.DryHeight && flooding.ActiveLeaks == 0 &&
                flooding.WaterLevel01 == 0f && !flooding.WaterVisible,
                $"gunners physically support repairs and a mate pumps the hold from {peakWater:F3} to {flooding.WaterHeight:F3}m");
        }

        IEnumerator CompanyFormerRigging()
        {
            var formerPosts = Small
                ? new[] { new Vector3(-1f, 2.85f, 10.8f), new Vector3(.4f, 3.21f, 15.8f) }
                : new[] { new Vector3(2.32f, 4.15f, 10.90f), new Vector3(0f, 4.15f, 24.44f) };
            Require(Ship.Anchored && Ship.SailStep == 0, "former rigging is tested at anchor with sail furled");
            foreach (var point in formerPosts)
            {
                Crew.ReadDeviceInput = false;
                Fixture(point);
                Crew.ReadDeviceInput = true;
                yield return HoldKeys(5);
                bool offeredRope = Crew.Prompt.Contains("帆索") || Crew.Prompt.Contains("锚索");
                yield return KeyTap(Key.E);
                Require(!offeredRope && Crew.StationName != "帆索" && Crew.StationName != "锚索",
                    $"E near former rigging point {point:F2} offers no independent rope station");
                yield return KeyTap(Key.W);
                Require(Ship.Anchored && Ship.SailStep == 0, $"W away from the wheel cannot weigh anchor at {point:F2}");
                yield return KeyTap(Key.S);
                yield return KeyTap(Key.Space);
                Require(Ship.Anchored && Ship.SailStep == 0, $"former rigging leaves anchor and canvas unchanged at {point:F2}");
                if (Crew.StationName.Length > 0) yield return KeyTap(Key.E);
                for (int frame = 0; frame < 38 && Crew.IsJumping; frame++) yield return null;
            }
        }

        IEnumerator CompanyPlayerHelm(bool photograph)
        {
            var target = game.Navy;
            var position = target.transform.position;
            bool moving = target.enabled;
            bool flooding = target.Flooding.enabled;
            using var silentTarget = new AcceptanceCompanyFixture(target);
            target.enabled = false;
            target.Flooding.enabled = false;
            try
            {
                var helmStatus = game.GetComponent<VoyageHud>().GetComponentsInChildren<TextMeshProUGUI>(true)
                    .Single(label => label.name == "值舵");
                yield return CompanyMatesRecovered(Ship);
                yield return CompanyPlayerRelief(helmStatus, false, photograph);
                yield return CompanyPlayerRelief(helmStatus, true, photograph);
                yield return KeyTap(Key.E);
            }
            finally
            {
                target.transform.position = position;
                target.enabled = moving;
                target.Flooding.enabled = flooding;
                Physics.SyncTransforms();
            }
        }

        IEnumerator CompanyPlayerRelief(TextMeshProUGUI helmStatus, bool replaceDeputy, bool photograph)
        {
            var vessel = Ship;
            var company = vessel.Company;
            var captain = Crew;
            string scenario = replaceDeputy ? "replacement handover" : "first handover";
            CompanyPlaceHelmTarget(NavalShip.MaxShotRange + 100f, 55f);
            yield return CompanyPlayerTakesWheel();
            for (int step = 0; step < 2 && !vessel.Anchored; step++) yield return KeyTap(Key.S);
            for (int frame = 0; frame < 70 && (vessel.Speed > .02f || Mathf.Abs(vessel.Rudder) > .02f); frame++)
                yield return null;
            Require(vessel.Anchored && vessel.Speed <= .02f && Mathf.Abs(vessel.Rudder) <= .02f &&
                captain.Health == CrewPlayer.MaxHealth && company.CanSteer(captain) && !company.ActingCaptain &&
                !game.Navy.Company.CanSteer(captain), $"living pirate captain holds uncontested helm authority at anchor before {scenario}");

            // Move only the target. The captain, deputy, wheel, and vessel run through their usual frame updates.
            CompanyPlaceHelmTarget(NavalShip.MaxShotRange + 100f, 55f);
            Vector3 startingPosition = vessel.transform.position;
            float startingCourse = vessel.Course;
            var wheel = vessel.Rig.transform.Find("Wheel");
            var canvas = vessel.Rig.transform.Find("SailJib");
            Require(wheel && canvas, "authored wheel and jib can be observed during the handover");
            Quaternion neutralWheel = wheel.localRotation;
            double casualtyAt = Time.timeAsDouble;
            double controlDeadline = casualtyAt + 10 - Step;
            Note($"FIXTURE pirate {scenario} uses normal TakeDamage and respawn clock against the stationary test opponent.");
            captain.TakeDamage(CrewPlayer.MaxHealth);
            Require(captain.IsDead && captain.Health == 0 && !captain.IsAtHelm && !company.CanSteer(captain),
                "lethal damage immediately releases the player's station and authority");
            var first = company.ActingCaptain as CrewMate;
            Require(first && company.CanSteer(first) && !game.Navy.Company.CanSteer(first) &&
                !company.CanSteer(game.Navy.Company.Captain), "an eligible pirate mate alone takes temporary helm authority");
            yield return CompanyDeputyWalksToWheel(first, helmStatus, casualtyAt, controlDeadline);

            var onWatch = first;
            if (replaceDeputy)
            {
                while (captain.IsDead && Time.timeAsDouble < controlDeadline && vessel.Rudder < .28f) yield return null;
                Require(captain.IsDead && AtWheel(first) && vessel.Rudder >= .28f && !vessel.Anchored,
                    $"first deputy is actively steering before the second casualty, rudder={vessel.Rudder:F2} sail={vessel.SailStep}");
                double deputyCasualtyAt = Time.timeAsDouble;
                first.TakeDamage(CrewPlayer.MaxHealth);
                onWatch = company.ActingCaptain as CrewMate;
                Require(first.Health == 0 && onWatch && onWatch != first && company.CanSteer(onWatch) && !company.CanSteer(first),
                    "the dead deputy immediately loses authority and a different living mate is appointed");
                yield return CompanyDeputyWalksToWheel(onWatch, helmStatus, deputyCasualtyAt, controlDeadline);
            }

            double manoeuvreDeadline = Math.Min(controlDeadline, Time.timeAsDouble + 3);
            bool FullSailTurn() => !vessel.Anchored && vessel.SailStep == 2 && canvas.localScale.y > .90f &&
                vessel.Speed > .65f && vessel.Rudder > .36f && Mathf.DeltaAngle(startingCourse, vessel.Course) > 2f &&
                Quaternion.Angle(wheel.localRotation, neutralWheel) > 20f;
            while (captain.IsDead && Time.timeAsDouble < manoeuvreDeadline && !FullSailTurn()) yield return null;
            Require(captain.IsDead && AtWheel(onWatch) && FullSailTurn(),
                $"pirate {scenario} weighs anchor, sets full canvas and steers right toward the distant target, " +
                $"anchor={vessel.Anchored} sail={vessel.SailStep} rudder={vessel.Rudder:F2} " +
                $"course={startingCourse:F1}→{vessel.Course:F1} speed={vessel.Speed:F2}m/s");
            Require(helmStatus.text.Contains("代理掌舵") && helmStatus.text.Contains("右舵"),
                $"HUD shows the deputy's actual right rudder, observed {helmStatus.text}");
            if (photograph) yield return Photograph($"company-pirate-deputy-{(replaceDeputy ? "replacement-" : "")}{type}");

            if (!replaceDeputy)
            {
                CompanyPlaceHelmTarget(52f, 12f);
                float closeCourse = vessel.Course;
                float rightmostTurn = 0f;
                bool turnedLeft = false;
                manoeuvreDeadline = Math.Min(controlDeadline, Time.timeAsDouble + 3);
                while (captain.IsDead && Time.timeAsDouble < manoeuvreDeadline && !turnedLeft)
                {
                    float change = Mathf.DeltaAngle(closeCourse, vessel.Course);
                    rightmostTurn = Mathf.Max(rightmostTurn, change);
                    turnedLeft = vessel.SailStep == 1 && canvas.localScale.y < .70f &&
                        vessel.Rudder < -.25f && rightmostTurn - change > 2f;
                    if (!turnedLeft) yield return null;
                }
                Require(captain.IsDead && AtWheel(onWatch) && turnedLeft && !vessel.Anchored && vessel.Speed > .5f,
                    $"the same deputy reefs to half canvas and reverses the turn for the closer target, " +
                    $"sail={vessel.SailStep} rudder={vessel.Rudder:F2} course={vessel.Course:F1} speed={vessel.Speed:F2}m/s");
                Require(helmStatus.text.Contains("代理掌舵") && helmStatus.text.Contains("左舵"),
                    $"HUD shows the deputy's actual left rudder, observed {helmStatus.text}");
            }

            double deadline = casualtyAt + 10 + Step * 2;
            while (captain.IsDead && Time.timeAsDouble < deadline && !game.IsGameOver) yield return null;
            double respawn = Time.timeAsDouble - casualtyAt;
            float rudderAtRespawn = Mathf.Abs(vessel.Rudder);
            Require(respawn >= 10 - .002 && respawn <= 10 + Step * 1.1 && captain.Health == CrewPlayer.MaxHealth &&
                !captain.IsAtHelm && HorizontalTravel(startingPosition, vessel.transform.position) > .8f,
                $"player respawns at {respawn:F2}s after {scenario}");
            // Authority queries run the captain-selection logic. Observe the normal update handover first.
            yield return null;
            float nextRudder = Mathf.Abs(vessel.Rudder);
            Require(onWatch.Job != Role.Helm && !company.Mates.Any(AtWheel) &&
                (rudderAtRespawn <= .05f ? nextRudder <= .05f : nextRudder < rudderAtRespawn - .005f) &&
                helmStatus.text == "你是船长 · 可接舵",
                $"physical helm clears before the next motion step, rudder={rudderAtRespawn:F2}→{nextRudder:F2}; HUD offers the wheel to the captain");
            Require(company.CanSteer(captain) && !company.ActingCaptain && !company.CanSteer(onWatch),
                "the respawned captain holds sole helm authority");
            yield return CompanyPlayerTakesWheel();
            bool reef = vessel.SailStep == 2;
            int commandedSail = vessel.SailStep + (reef ? -1 : 1);
            yield return KeyTap(reef ? Key.S : Key.W);
            yield return HoldKeys(22, Key.A);
            Require(captain.IsAtHelm && vessel.SailStep == commandedSail && vessel.Rudder < -.27f &&
                helmStatus.text == "你正在掌舵" && !company.Mates.Any(AtWheel),
                $"normal captain input controls canvas and left rudder again, sail={vessel.SailStep} rudder={vessel.Rudder:F2}");
            yield return HoldKeys(1);
        }

        IEnumerator CompanyPlayerTakesWheel()
        {
            if (!Crew.IsAtHelm)
            {
                Crew.ReadDeviceInput = false;
                Fixture(Ship.Definition.Data.spawn.position);
                Crew.ReadDeviceInput = true;
                yield return HoldKeys(4);
                yield return KeyTap(Key.E);
            }
            Require(Crew.IsAtHelm, "living pirate captain occupies the wheel through E input");
        }

        IEnumerator CompanyDeputyWalksToWheel(CrewMate deputy, TextMeshProUGUI helmStatus, double assignedAt, double deadline)
        {
            var vessel = Ship;
            Vector3 start = deputy.transform.localPosition;
            float rudder = Mathf.Abs(vessel.Rudder);
            int sail = vessel.SailStep;
            bool waitingShown = false, heldControls = true;
            while (Crew.IsDead && Time.timeAsDouble < deadline && !AtWheel(deputy) && !game.IsGameOver)
            {
                waitingShown |= helmStatus.text == "代理舵手正在就位";
                heldControls &= vessel.SailStep == sail && Mathf.Abs(vessel.Rudder) <= rudder + .02f;
                yield return null;
            }
            float walked = Vector3.Distance(start, deputy.transform.localPosition);
            Require(Crew.IsDead && AtWheel(deputy) && vessel.Company.ActingCaptain == deputy &&
                Vector3.Distance(deputy.transform.position, vessel.Rig.Helm.position) < 2f && walked > .1f,
                $"slot {deputy.Slot} walks to the physical wheel in {Time.timeAsDouble - assignedAt:F2}s, displaced {walked:F2}m");
            Require(waitingShown && heldControls && vessel.Company.Mates.All(mate => mate == deputy || !vessel.Company.CanSteer(mate)),
                "HUD shows the deputy en route and ship controls are held until the authorized mate reaches the wheel");
        }

        static bool AtWheel(CrewMate mate) => mate.Job == Role.Helm && mate.AtPost;

        void CompanyPlaceHelmTarget(float distance, float bearing)
        {
            var vessel = Ship;
            var target = game.Navy;
            Vector3 sourceCentre = vessel.transform.TransformPoint(0f, vessel.Definition.DryDraft, vessel.Definition.PivotZ);
            Vector3 targetCentre = target.transform.TransformPoint(0f, target.Definition.DryDraft, target.Definition.PivotZ);
            Vector3 destination = sourceCentre + Quaternion.Euler(0f, vessel.Course + bearing, 0f) * Vector3.forward * distance;
            target.transform.position += Vector3.ProjectOnPlane(destination - targetCentre, Vector3.up);
            Physics.SyncTransforms();
            Note($"FIXTURE stationary opponent at {distance:F0}m, bearing {bearing:F0} degrees to starboard of the current heading.");
        }

        IEnumerator CompanyBoardEnemy()
        {
            using var silence = new AcceptanceCompanyFixture(Ship);
            yield return HoldKeys(75);
            var enemy = game.Navy;
            var ladder = enemy.Definition.Data.boardingLadders.Last();
            var hull = enemy.transform;
            var sea = game.GetComponent<NavalWorld>();
            var start = hull.TransformPoint(ladder.bottom + Vector3.right * 1.45f);
            start.y = sea.SurfaceHeight(start) + .12f;
            Note("FIXTURE places the player just above the sea by the enemy's starboard ladder. Normal swimming and climbing determine the boarded ship.");
            Crew.ReadDeviceInput = false;
            Fixture(Ship.transform.InverseTransformPoint(start));
            Crew.ReadDeviceInput = true;
            for (int frame = 0; frame < 28 && !Crew.IsSwimming; frame++) yield return null;
            Require(Crew.IsSwimming && Crew.transform.parent != hull, "player enters the sea outside the enemy ship");
            for (int frame = 0; frame < 65 && Crew.IsSwimming; frame++)
                yield return HoldKeys(1, TowardKey(Vector3.ProjectOnPlane(hull.TransformPoint(ladder.bottom) - Crew.transform.position, Vector3.up)));
            Require(Crew.IsClimbing && Crew.transform.parent == hull, "swimming into the enemy ladder grips and binds to the enemy hull");
            Keyboard(Key.W);
            for (int frame = 0; frame < 175 && Crew.IsClimbing; frame++) yield return null;
            yield return HoldKeys(4);
            Require(Crew.transform.parent == hull && !Crew.IsClimbing && !Crew.IsSwimming &&
                Vector3.Distance(Crew.transform.localPosition, ladder.landing) < .62f,
                $"W climbs onto the enemy deck, foot={Crew.transform.localPosition:F2}");

            string gunName = Small ? "S2" : "上层右舷炮饰 01";
            int gunIndex = Array.FindIndex(enemy.Rig.Guns, gun => gun.name == gunName);
            Require(gunIndex >= 0, $"enemy ladder has a nearby gun {gunName}");
            Require(CrewStationFit.TryGetPose(enemy, Role.Gun, gunIndex, enemy.Rig.Guns[gunIndex],
                Crew.transform.position, out var gunFoot, out _), "enemy gun has an authored stance");
            var gunStand = hull.InverseTransformPoint(gunFoot);
            for (int frame = 0; frame < 45 && !Crew.Prompt.Contains("右舷炮"); frame++)
                yield return HoldKeys(1, TowardKey(Vector3.ProjectOnPlane(hull.TransformPoint(gunStand) - Crew.transform.position, Vector3.up)));
            yield return HoldKeys(1);
            yield return KeyTap(Key.E);
            Require(Crew.IsAiming && Crew.transform.parent == hull, "boarded pirate may work a non-helm station aboard the enemy");
            yield return KeyTap(Key.E);
            Note("FIXTURE repositions the already-boarded player to the enemy quarterdeck to isolate captain permissions.");
            var motor = Crew.GetComponent<CharacterController>();
            motor.enabled = false;
            Crew.transform.position = enemy.Rig.Spawn.position + hull.up * .04f;
            motor.enabled = true;
            Physics.SyncTransforms();
            yield return HoldKeys(4);
            Require(Crew.Prompt.Contains("舵轮仅限本船船长") && !enemy.Company.CanSteer(Crew),
                $"enemy helm offers captain-only prompt to boarder, shown={Crew.Prompt}");
            int sail = enemy.SailStep;
            bool anchor = enemy.Anchored;
            yield return KeyTap(Key.E);
            Require(Crew.StationName.Length == 0 && Crew.Prompt.Contains("舵轮仅限本船船长"),
                "enemy helm rejects E from a boarded pirate captain");
            yield return Photograph("company-enemy-helm-denied-" + type);
            yield return KeyTap(Key.W);
            Require(enemy.Anchored == anchor && enemy.SailStep == sail, "boarder cannot increase the opposing ship's sail or weigh anchor");
            yield return KeyTap(Key.S);
            yield return KeyTap(Key.Space);
            Require(Crew.StationName.Length == 0 && enemy.Anchored == anchor && enemy.SailStep == sail,
                "boarder cannot set the opposing helm's sail or anchor");
        }

        IEnumerator CompanySuccession(NavalShip vessel)
        {
            yield return CompanyMatesRecovered(vessel);
            yield return CompanySuccessionCase(vessel, false);
            yield return CompanyMatesRecovered(vessel);
            yield return CompanySuccessionCase(vessel, true);
            yield return CompanyMatesRecovered(vessel);
        }

        IEnumerator CompanyMatesRecovered(NavalShip vessel)
        {
            double deadline = Time.timeAsDouble + 10.5;
            while (vessel.Company.Mates.Any(mate => mate.Health <= 0) &&
                Time.timeAsDouble < deadline && !game.IsGameOver)
                yield return null;
            Require(vessel.Company.Mates.All(mate => mate.Health > 0),
                $"{vessel.name} all mates are alive for the next independent case");
        }

        IEnumerator CompanySuccessionCase(NavalShip vessel, bool replaceDeputy)
        {
            var company = vessel.Company;
            var captain = company.Captain;
            string scenario = replaceDeputy ? "replacement handover" : "first handover";
            for (int frame = 0; frame < 110 && (vessel.Anchored || vessel.Speed <= .3f); frame++)
                yield return null;
            Require(!vessel.Anchored && vessel.Speed > .3f,
                $"{vessel.name} sailing before {scenario}, speed={vessel.Speed:F2}");
            var initialPosition = vessel.transform.position;
            Require(captain.Health == CrewPlayer.MaxHealth && company.CanSteer(captain) && !company.ActingCaptain,
                $"{vessel.name} original captain holds uncontested helm authority before {scenario}");
            Note($"FIXTURE {vessel.name} {scenario} uses normal TakeDamage and the normal respawn clock.");
            double casualtyAt = Time.timeAsDouble;
            captain.TakeDamage(CrewPlayer.MaxHealth);
            Require(captain.Health == 0 && !company.CanSteer(captain), $"{vessel.name} dead captain immediately loses helm authority");
            var first = company.ActingCaptain as CrewMate;
            Require(first && first.Health > 0 && company.CanSteer(first), $"{vessel.name} assigns acting captain");
            CrewMate onWatch = first;
            if (replaceDeputy)
            {
                first.TakeDamage(CrewPlayer.MaxHealth);
                onWatch = company.ActingCaptain as CrewMate;
                Require(Time.timeAsDouble == casualtyAt && first.Health == 0 && onWatch && onWatch != first &&
                    company.CanSteer(onWatch) && !company.CanSteer(first),
                    $"{vessel.name} reassigns helm as soon as the first appointee falls");
            }
            double deadline = casualtyAt + 10 + Step * 2;
            while (captain.Health == 0 && !(onWatch.Job == Role.Helm && onWatch.AtPost) &&
                Time.timeAsDouble < deadline && !game.IsGameOver)
                yield return null;
            double arrival = Time.timeAsDouble - casualtyAt;
            Require(onWatch.Job == Role.Helm && onWatch.AtPost && company.ActingCaptain == onWatch && captain.Health == 0 &&
                Vector3.Distance(onWatch.transform.position, vessel.Rig.Helm.position) < 2f &&
                !vessel.Anchored && vessel.Speed > .3f,
                $"{vessel.name} {scenario} physically takes the wheel at {onWatch.transform.localPosition:F2} in {arrival:F2}s and keeps sailing at {vessel.Speed:F2}m/s");
            yield return Photograph($"company-navy-deputy-{(replaceDeputy ? "replacement-" : "")}{type}");
            while (captain.Health == 0 && Time.timeAsDouble < deadline && !game.IsGameOver)
                yield return null;
            double respawn = Time.timeAsDouble - casualtyAt;
            float travelled = HorizontalTravel(initialPosition, vessel.transform.position);
            Require(travelled > .3f && !vessel.Anchored && vessel.Speed > .3f,
                $"{vessel.name} stays under way through {scenario}, displacement={travelled:F2}m");
            Require(respawn >= 10 - .002 && respawn <= 10 + Step * 1.1 && captain.Health == CrewPlayer.MaxHealth &&
                company.CanSteer(captain) && !company.ActingCaptain && !company.CanSteer(onWatch) && onWatch.Job != Role.Helm,
                $"{vessel.name} original captain respawns at {respawn:F2}s and immediately resumes authority after {scenario}");
            if (captain is CrewMate original)
                yield return CompanyNavyCaptainReturns(original);
        }

        IEnumerator CompanyNavyCaptainReturns(CrewMate original)
        {
            float distance = original.HelmWalkDistance();
            Require(!float.IsInfinity(distance), $"resurrected navy captain has a walkable route from {original.transform.localPosition:F2} to the helm");
            float travelSeconds = distance / CrewPlayer.RunSpeed;
            double deadline = Time.timeAsDouble + travelSeconds + 3f;
            while (Time.timeAsDouble < deadline && !(original.Job == Role.Helm && original.AtPost) && !game.IsGameOver)
                yield return null;
            Require(original.Job == Role.Helm && original.AtPost,
                $"resurrected navy captain physically reoccupies helm: route={distance:F2}m travel budget={travelSeconds + 3f:F2}s foot={original.transform.localPosition:F2} job={original.Job}");
        }
    }
}
