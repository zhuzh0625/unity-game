using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Galleon
{
    public sealed partial class FleetCheck
    {
        double lastEnemyImpact;

        public static void BeginLife(NavalGame session, string directory)
        {
            var check = session.gameObject.AddComponent<FleetCheck>();
            check.StartCheck(session, directory, "life-check", "Crew life", check.RunLife());
        }

        IEnumerator RunLife()
        {
            yield return null;
            foreach (var choice in new[] { ShipType.Sloop, ShipType.Brigantine })
            {
                type = choice;
                Click(game.Menu.GetShipButton(type));
                Click(game.Menu.StartButton);
                Note("FIXTURE use a calibrated outer-ring impact for partial injury and a direct enemy shot for the fatal hit.");
                using var companyFixture = new AcceptanceCompanyFixture(Ship, game.Navy);
                yield return null;
                Require(Ship.Definition.Type == type && Crew.Health == 100 && !Crew.IsDead, "new voyage starts at full health");
                yield return new WaitForEndOfFrame();
                CheckLifeHud(false, false);
                ApplyOuterRingImpact();
                Require(Crew.Health == 50 && !Crew.IsDead, "outer ring deals 50 damage to the sailor on deck");
                yield return new WaitForEndOfFrame();
                CheckLifeHud(false, false);
                yield return Photograph("injured-" + type);

                Crew.ReadDeviceInput = false;
                int gun = ApproachWeatherGun();
                Ship.Load(gun);
                yield return UseKey();
                Require(Crew.IsAiming && Ship.IsLoaded(gun), "sailor operated a loaded gun before the fatal hit");
                Crew.ReadDeviceInput = true;
                yield return ShotAtCrew();
                Require(Crew.Health == 0 && Crew.IsDead && !Crew.IsAiming && Crew.StationName.Length == 0,
                    "direct enemy round kills sailor and releases gun");
                yield return WaitForLife(false, lastEnemyImpact, gun);
                yield return WalkOutOfHold(true);
                yield return SwimLife();
                yield return WalkOutOfHold(false);
                yield return SampleRespawnArea();
                yield return HardLanding();
                yield return SinkWhileDead();
                game.ReturnToMenu();
                yield return null;
            }
        }

        CapsuleCollider PlayerHurtbox()
        {
            Physics.SyncTransforms();
            var capsule = Array.Find(Crew.GetComponentsInChildren<CapsuleCollider>(), c => c.isTrigger && c.enabled);
            Require(capsule, "standing or swimming R06 has an enabled projectile hit volume");
            return capsule;
        }

        void ApplyOuterRingImpact()
        {
            var capsule = PlayerHurtbox();
            Vector3 above = capsule.bounds.center + Vector3.up * 4f;
            Vector3 surface = capsule.ClosestPoint(above);
            float outerBandMidpoint = (RoundShot.InnerRadius + RoundShot.OuterRadius) * .5f;
            RoundShot.DamageCrewAt(surface + (above - surface).normalized * outerBandMidpoint);
        }

        // Start the enemy round immediately above the body and let its normal sweep resolve the direct hit.
        IEnumerator ShotAtCrew()
        {
            var capsule = PlayerHurtbox();
            var projectile = new GameObject("Acceptance enemy round").AddComponent<RoundShot>();
            projectile.transform.position = capsule.bounds.center + Vector3.up * (capsule.bounds.extents.y + .45f);
            projectile.Launch(game.Navy, Vector3.down * 42f);
            int priorHealth = Crew.Health;
            lastEnemyImpact = double.NaN;
            for (int i = 0; projectile && i < 10; i++)
            {
                yield return null;
                if (double.IsNaN(lastEnemyImpact) && Crew.Health != priorHealth)
                    lastEnemyImpact = Time.timeAsDouble;
            }
            Require(!projectile && !double.IsNaN(lastEnemyImpact), "enemy projectile was consumed by impact");
        }

        Animation OnlyDeckhand()
        {
            Animation only = null;
            int count = 0;
            foreach (var a in Crew.GetComponentsInChildren<Animation>())
            {
                if (a["Prone"] == null) continue;
                only = a;
                count++;
            }
            Require(count == 1, $"one active R06 model under the player, found {count}");
            return only;
        }

        IEnumerator PauseLife(bool dead)
        {
            float prior = dead ? Crew.RespawnSecondsRemaining : Crew.SwimSecondsRemaining;
            var swim = dead ? null : OnlyDeckhand()["Swim"];
            float phaseBefore = swim == null ? 0f : swim.normalizedTime;
            game.SetPaused(true);
            yield return HoldKeys(12);
            float paused = dead ? Crew.RespawnSecondsRemaining : Crew.SwimSecondsRemaining;
            Require(Time.timeScale == 0 && Mathf.Abs(prior - paused) < .002f && !LifeHud.gameObject.activeInHierarchy,
                $"pause held {(dead ? "respawn" : "swimming")} at {paused:F3}s");
            if (!dead)
                Require(swim != null && Mathf.Abs(swim.normalizedTime - phaseBefore) < .001f,
                    $"pause held the swim stroke at phase {phaseBefore:F3}");
            game.SetPaused(false);
        }

        Transform LifeHud => game.transform.Find("航海界面/航行界面/CrewHealth");

        void CheckLifeHud(bool dead, bool swimming)
        {
            var hud = LifeHud;
            Require(hud && hud.gameObject.activeInHierarchy, "crew health panel visible");
            var value = hud.Find("CrewHealthValue").GetComponent<TMP_Text>();
            var track = (RectTransform)hud.Find("CrewHealthTrack");
            var fill = (RectTransform)track.Find("CrewHealthFill");
            var respawn = hud.Find("CrewRespawnCountdown");
            var swim = hud.Find("CrewSwimCountdown");
            string healthText = value.GetParsedText();
            Require(healthText.Replace(" ", "") == $"{Crew.Health}/{CrewPlayer.MaxHealth}" &&
                Mathf.Abs(fill.rect.width / track.rect.width - Crew.Health / (float)CrewPlayer.MaxHealth) < .015f,
                $"HUD shows health {healthText} and bar {fill.rect.width:F1}/{track.rect.width:F1}");
            Require(respawn.gameObject.activeInHierarchy == dead && swim.gameObject.activeInHierarchy == swimming,
                $"HUD timers respawn={dead} swimming={swimming}");
            if (dead || swimming)
            {
                float remaining = dead ? Crew.RespawnSecondsRemaining : Crew.SwimSecondsRemaining;
                var label = (dead ? respawn : swim).Find("CountdownText").GetComponent<TMP_Text>();
                string countdown = label.GetParsedText();
                Require(countdown.Contains(Mathf.CeilToInt(remaining).ToString()), "visible countdown " + countdown);
            }
        }

        IEnumerator WaitForLife(bool atSea, double started, int gun = -1)
        {
            yield return new WaitForEndOfFrame();
            CheckLifeHud(true, false);
            var pose = OnlyDeckhand();
            var skin = Array.Find(pose.GetComponentsInChildren<SkinnedMeshRenderer>(),
                r => r.sharedMesh && r.sharedMesh.name == "Body");
            Require(skin && IsPose(pose, "Prone") && SailorOnCamera(skin), "imported Prone body renders in the game camera");
            var head = SailorBone(skin, "Head");
            var feet = SailorBone(skin, "Foot.L");
            Vector3 axis = head.position - feet.position;
            Require(Mathf.Abs(axis.normalized.y) < .5f, $"rendered head-to-foot axis is horizontal {axis:F2}");
            Vector3 initial = pose.transform.position;
            Vector3 local = Ship.transform.InverseTransformPoint(initial);
            Vector3 vesselStart = Ship.transform.position;
            var sea = game.GetComponent<NavalWorld>();
            float initialWave = sea.SurfaceHeight(initial);
            float waveRange = 0f, error = 0f;
            Ship.SetSailStep(2);
            yield return Photograph((atSea ? "sea-prone-" : "deck-prone-") + type);
            yield return PauseLife(true);
            yield return HoldKeys(1, Key.E, Key.Space, Key.W);
            yield return HoldKeys(1);
            Require(Crew.IsDead && !Crew.IsAiming && Crew.StationName.Length == 0 &&
                (atSea || Ship.IsLoaded(gun)), "downed sailor cannot enter a station, walk or fire the loaded gun");
            double lastAliveBody = Time.timeAsDouble;
            // Measure game time independently of the countdown and verify the corpse every frame.
            int frames = 0;
            while (Crew.IsDead && frames++ < 260)
            {
                var current = pose.transform.position;
                float wave = sea.SurfaceHeight(current);
                waveRange = Mathf.Max(waveRange, Mathf.Abs(wave - initialWave));
                float drift = atSea
                    ? Mathf.Max(Vector3.ProjectOnPlane(current - initial, Vector3.up).magnitude,
                        Mathf.Abs(current.y - initial.y - (wave - initialWave)))
                    : Vector3.Distance(Ship.transform.InverseTransformPoint(current), local);
                error = Mathf.Max(error, drift);
                if (!IsPose(pose, "Prone") || !SailorOnCamera(skin))
                    throw new InvalidOperationException("Prone body disappeared before respawn");
                lastAliveBody = Time.timeAsDouble;
                yield return null;
            }
            double duration = Time.timeAsDouble - started;
            Require(!Crew.IsDead && duration >= 10 - .002 && duration <= 10 + Step * 1.1 &&
                lastAliveBody - started >= 10 - Step * 1.1, $"Prone held until {lastAliveBody-started:F2}s, respawned at {duration:F2}s");
            Require(error < .09f && (Ship.transform.position - vesselStart).magnitude > 1f && (!atSea || waveRange > .04f),
                $"corpse followed {(atSea ? "sea-surface" : "moving hull")} frame, max error={error:F3}m, wave delta={waveRange:F3}m");
            Ship.SetSailStep(0);
            yield return new WaitForEndOfFrame();
            Require(!pose && !skin && Crew.Health == CrewPlayer.MaxHealth && !game.IsGameOver &&
                OnlyDeckhand().transform.IsChildOf(Crew.transform), "old corpse destroyed and exactly one full-health sailor returned to own ship");
            CheckLifeHud(false, false);
        }

        IEnumerator SwimLife()
        {
            var ladder = Ship.Definition.Data.boardingLadders[0];
            for (int i = 0; i < 85 && Ship.Speed > .01f; i++) yield return null;
            Require(Ship.Anchored && Ship.Speed <= .01f, "anchored ship stopped before ladder approach");
            float side = Mathf.Sign(ladder.bottom.x);
            Crew.ReadDeviceInput = false;
            Fixture(Takeoff(ladder.landing, side, out _));
            yield return Frames(4);
            Crew.ReadDeviceInput = true;
            Key outward = TowardKey(Ship.transform.right * side);
            yield return HoldKeys(1, Key.Space, outward);
            for (int i = 0; i < 65 && !Crew.IsSwimming; i++) yield return null;
            Require(Crew.IsSwimming && Crew.Health == 100, "normal jump over the side entered the sea unharmed");
            var waitingPoint = ladder.bottom + Vector3.right * (side * 1.35f);
            for (int i = 0; i < 120 && Crew.IsSwimming && HorizontalTo(waitingPoint).magnitude > .14f; i++)
                yield return HoldKeys(1, TowardKey(HorizontalTo(waitingPoint)));
            yield return HoldKeys(1);
            Require(Crew.IsSwimming && HorizontalTo(waitingPoint).magnitude <= .14f,
                $"swimmer waited outside the reach of {ladder.name} at {CrewOnShip:F2}");
            for (int i = 0; Crew.SwimSecondsRemaining > 2f && i < 400; i++) yield return null;
            Require(Crew.IsSwimming && !Crew.IsDead && Crew.SwimSecondsRemaining > 1.7f &&
                Crew.SwimSecondsRemaining <= 2f, $"still afloat near ladder with {Crew.SwimSecondsRemaining:F2}s");
            yield return new WaitForEndOfFrame();
            CheckLifeHud(false, true);
            yield return PauseLife(false);
            yield return SwimIntoGrip(ladder);
            yield return HoldKeys(50);
            Require(Crew.IsClimbing && !Crew.IsDead && Crew.Health == 100,
                "held the ladder past the original drowning deadline without losing health");
            yield return new WaitForEndOfFrame();
            CheckLifeHud(false, false);
            Keyboard(Key.S);
            float lastGripHeight = CrewOnShip.y;
            for (int i = 0; i < 90 && Crew.IsClimbing; i++)
            {
                lastGripHeight = CrewOnShip.y;
                yield return null;
            }
            Require(!Crew.IsClimbing && Mathf.Abs(lastGripHeight - ladder.bottom.y) < .15f,
                $"S left the lowest rung, last grip y={lastGripHeight:F2}, bottom={ladder.bottom.y:F2}");
            Keyboard();
            for (int i = 0; i < 65 && !Crew.IsSwimming; i++) yield return null;
            double entered = Time.timeAsDouble;
            Require(Crew.IsSwimming && Crew.SwimSecondsRemaining > 19.8f, $"new sea entry restored {Crew.SwimSecondsRemaining:F2}s");
            yield return HoldKeys(22, TowardKey(Ship.transform.right * side));
            yield return HoldKeys(1);
            ApplyOuterRingImpact();
            Require(Crew.IsSwimming && Crew.Health == 50 && !Crew.IsDead, "outer ring deals 50 damage to the swimmer at sea");
            Vector3 idle = Crew.transform.position;
            Ship.SetSailStep(2);
            double lastLiving = Time.timeAsDouble;
            for (int i = 0; !Crew.IsDead && i < 450; i++)
            {
                lastLiving = Time.timeAsDouble;
                if (!Crew.IsSwimming || Crew.Health != 50)
                    throw new InvalidOperationException($"swimming ended before drowning at {lastLiving-entered:F2}s");
                yield return null;
            }
            double drowned = Time.timeAsDouble;
            float drift = Vector3.ProjectOnPlane(Crew.transform.position - idle, Vector3.up).magnitude;
            Require(Crew.IsDead && !game.IsGameOver && drowned-entered >= 20 - Step*1.1 &&
                drowned-entered <= 20 + Step*2.1 && lastLiving-entered >= 20 - Step*2.1 && drift < .18f,
                $"drowned after {drowned-entered:F2}s; last living at {lastLiving-entered:F2}s, idle drift={drift:F3}m");
            yield return WaitForLife(true, drowned);
        }

        IEnumerator WalkOutOfHold(bool capture)
        {
            Crew.ReadDeviceInput = false;
            var foot = CrewOnShip;
            CheckLowerFoot(foot);
            yield return new WaitForEndOfFrame();
            if (Small) CheckCabinView();
            if (capture) yield return Photograph("respawn-" + type);
            var route = Small ? new[] {
                new Vector3(1,1.04f,9.02f), new Vector3(1,1.04f,7.15f),
                new Vector3(0,1.04f,7.15f), new Vector3(0,2.84f,9.12f)
            } : new[] {
                new Vector3(foot.x,1.65f,11.85f), new Vector3(0,1.65f,12.23f),
                new Vector3(0,2.9f,14.35f), new Vector3(0,4.15f,16.50f)
            };
            foreach (var target in route)
                yield return HoldRouteCheck.WalkTo(game, target, (ok, label, detail) => Require(ok, label + " " + detail));
            Require(!Crew.IsSwimming && !Crew.IsDead && CrewOnShip.y > foot.y + 1.45f,
                $"walked unassisted from lower-deck respawn {foot:F2} to upper deck {CrewOnShip:F2}");
            Crew.ReadDeviceInput = true;
        }

        void CheckCabinView()
        {
            var skin = Array.Find(Crew.GetComponentsInChildren<SkinnedMeshRenderer>(),
                r => r.sharedMesh && r.sharedMesh.name == "Body");
            var camera = game.MainCamera;
            int visible = 0;
            float bottom = 1f, top = 0f;
            foreach (var bone in skin.bones)
            {
                Vector3 point = camera.WorldToViewportPoint(bone.position);
                if (point.z <= camera.nearClipPlane || point.x <= .04f || point.x >= .96f ||
                    point.y <= .04f || point.y >= .96f) continue;
                Vector3 ray = bone.position - camera.transform.position;
                if (Physics.Raycast(camera.transform.position, ray.normalized, ray.magnitude - .03f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                visible++;
                top = Mathf.Max(top, point.y);
                bottom = Mathf.Min(bottom, point.y);
            }
            Require(SailorOnCamera(skin) && visible >= 3 && top - bottom >= .12f,
                $"initial cabin view from {CrewOnShip:F2}: visible body bones={visible}, viewport height={top - bottom:F2}, drawable={SailorDrawable(skin)}");
        }

        void CheckLowerFoot(Vector3 local)
        {
            var hull = Ship.transform;
            var foot = hull.TransformPoint(local);
            var up = hull.up;
            bool floor = Physics.Raycast(foot + up * .10f, -up, out var hit, .21f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.transform.IsChildOf(hull) && Vector3.Dot(hit.normal, up) > .8f;
            bool clear = !Physics.CheckCapsule(foot + up * .235f, foot + up * 1.355f, .205f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            bool central = Small ? Mathf.Abs(local.x) < 1.2f && local.z > 8.5f && local.z < 10.6f
                : Mathf.Abs(local.x) < 1.4f && local.z > 10.2f && local.z < 12.2f;
            Require(central && Mathf.Abs(local.y - (Small ? 1.04f : 1.65f)) < .18f && floor && clear,
                $"central lower deck {local:F2}, foot on {(hit.collider ? hit.collider.name : "nothing")}, capsule clear={clear}");
        }

        IEnumerator SampleRespawnArea()
        {
            Require(CrewRespawnArea.TryPickLocalFoot(Ship, out var first), "found a supported lower deck spawn");
            float spread = 0f;
            CheckLowerFoot(first);
            for (int i = 0; i < 12; i++)
            {
                Require(CrewRespawnArea.TryPickLocalFoot(Ship, out var local), $"lower-deck sample {i + 1} found");
                CheckLowerFoot(local);
                spread = Mathf.Max(spread, Vector3.Distance(first, local));
            }
            Require(spread > .28f, $"random lower-deck selection covered {spread:F2} metres");
            if (Small)
            {
                // On either side of the mast the preferred angle can put the camera against the post.
                Crew.ReadDeviceInput = false;
                foreach (float x in new[] { -.43f, .43f })
                {
                    Fixture(new Vector3(x, 1.076f, 9.85f));
                    Crew.ResetRespawnView();
                    yield return new WaitForEndOfFrame();
                    CheckCabinView();
                }
            }
            yield return null;
        }

        IEnumerator HardLanding()
        {
            Crew.ReadDeviceInput = false;
            var floor = Small ? new Vector3(1f, 2.85f, 7.25f) : new Vector3(1.23f, 4.15f, 16.5f);
            Fixture(floor + Vector3.up * 5.5f);
            Crew.ReadDeviceInput = true;
            for (int i = 0; i < 80 && Crew.Health == 100; i++) yield return null;
            Require(Crew.Health < 100 && !Crew.IsSwimming,
                $"high drop onto deck {CrewOnShip:F2} caused landing damage, health={Crew.Health}");
            for (int i = 0; i < 220 && Crew.IsDead; i++) yield return null;
            Require(!Crew.IsDead, "sailor available before independent sinking case");
        }

        IEnumerator SinkWhileDead()
        {
            game.StartDamageDrill();
            var flooding = Ship.Flooding;
            Time.timeScale = 12;
            for (int i = 0; i < 2600 && flooding.SinkingHeight - flooding.WaterHeight > .20f; i++) yield return null;
            Time.timeScale = 1;
            Require(!game.IsGameOver && flooding.SinkingHeight - flooding.WaterHeight <= .20f,
                "unattended damage drill brought hull just below sinking");
            Crew.TakeDamage(CrewPlayer.MaxHealth);
            var old = Crew;
            Require(old.IsDead && !game.IsGameOver, "individual died during ongoing flooding");
            for (int i = 0; i < 180 && !game.IsGameOver; i++) yield return null;
            Require(game.IsGameOver && flooding.IsSunk && Time.timeScale == 0 && game.GetComponent<VoyageHud>().IsDefeatVisible,
                "ship sank before individual respawn and showed defeat");
            yield return HoldKeys(20);
            Require(game.IsGameOver && game.GetComponent<VoyageHud>().IsDefeatVisible, "defeat remains on screen");
            yield return KeyTap(Key.F8);
            using var companyFixture = new AcceptanceCompanyFixture(Ship, game.Navy);
            yield return null;
            Require(!old && Crew && Crew.Health == 100 && !Crew.IsDead && !game.IsGameOver && Time.timeScale == 1,
                "F8 started fresh voyage with full health");
            var fresh = OnlyDeckhand();
            for (int i = 0; i < 215; i++) yield return null;
            Require(fresh && Crew.Health == 100 && !Crew.IsDead && OnlyDeckhand() == fresh,
                "new voyage has no delayed corpse or respawn from former voyage");
        }
    }
}
