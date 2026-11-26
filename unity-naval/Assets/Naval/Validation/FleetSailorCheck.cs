using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Galleon
{
    public sealed partial class FleetCheck
    {
        IEnumerator RunSailor()
        {
            yield return null;
            Require(game.IsInMenu, "starts at fleet selection");
            foreach (var choice in new[] { ShipType.Sloop, ShipType.Brigantine })
            {
                type = choice;
                Click(game.Menu.GetShipButton(type));
                Click(game.Menu.StartButton);
                Note("FIXTURE isolated player animation and repair cases preserve the authored motion and damage sites.");
                using var companyFixture = new AcceptanceCompanyFixture(Ship, game.Navy);
                yield return null;
                Require(Ship && Ship.Definition.Type == type && Crew, "started selected voyage");
                Crew.ReadDeviceInput = false;
                yield return CheckSailor();
                game.ReturnToMenu();
                yield return null;
                Require(game.IsInMenu && !Ship && !Crew, "returned to fleet selection");
            }
        }

        IEnumerator CheckSailor()
        {
            var animation = Array.Find(Crew.GetComponentsInChildren<Animation>(true), a => a.name == "Deckhand_R06");
            var model = animation ? animation.transform : null;
            var source = Resources.Load<GameObject>("Crew/Deckhand_R06");
            Require(model && source, "Deckhand_R06 source model instantiated on the player");
            var body = Array.Find(model.GetComponentsInChildren<SkinnedMeshRenderer>(true),
                r => r.sharedMesh && r.sharedMesh.name == "Body");
            var hammer = Array.Find(model.GetComponentsInChildren<MeshRenderer>(true), r => r.name == "RepairHammer");
            var sourceBody = source.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Require(body && sourceBody && body.sharedMesh && body.sharedMesh == sourceBody.sharedMesh,
                "player uses the imported R06 body mesh");
            Require(body.bones.Length == 15 && body.sharedMesh.bindposeCount == body.bones.Length &&
                Array.Exists(body.bones, bone => bone && bone.name == "Pelvis"),
                $"R06 has a bound skinned mesh with {body.bones.Length} bones and {body.sharedMesh.vertexCount} vertices");
            Require(hammer && hammer.TryGetComponent<MeshFilter>(out var toolMesh) && toolMesh.sharedMesh,
                "R06 repair hammer has its imported mesh");
            CheckCostumeColor(body, "vest", "#405C5D");
            CheckCostumeColor(body, "scarf", "#934D3B");

            var hand = SailorBone(body, "Hand.R");
            var leftFoot = SailorBone(body, "Foot.L");
            var rightFoot = SailorBone(body, "Foot.R");
            var baked = new Mesh();
            try
            {
                yield return Frames(8);
                yield return new WaitForEndOfFrame();
                CheckSailorStanding(model, body, baked);
                Require(SailorOnCamera(body) && !SailorDrawable(hammer), "third person shows the deckhand with the hammer hidden");
                yield return Photograph("sailor-third-person-" + type);

                Enter("舵轮");
                var helmBefore = SailorVertices(body, baked);
                var handBefore = model.InverseTransformPoint(hand.position);
                yield return Frames(10, new CrewCommand { Move = Vector2.right });
                var helmDelta = SailorVertexTravel(body, baked, helmBefore);
                var handDelta = Vector3.Distance(handBefore, model.InverseTransformPoint(hand.position));
                Require(Ship.Rudder > .2f && helmDelta > .012f && handDelta > .012f && !SailorDrawable(hammer),
                    $"helm drives the rudder and moves the R06 hand {handDelta:F3} m, skin {helmDelta:F3} m");
                Leave();
                yield return Frames(12);

                var idle = SailorVertices(body, baked);
                var stand = Crew.transform.localPosition;
                // These directions follow the first clear section of each ship's route from the helm.
                var walk = new CrewCommand { Move = Small ? new Vector2(.55f, .83f) : Vector2.right };
                float walkDelta = 0f;
                for (int frame = 0; frame < 7; frame++)
                {
                    yield return Frames(1, walk);
                    walkDelta = Mathf.Max(walkDelta, SailorVertexTravel(body, baked, idle));
                }
                float travelled = Vector3.ProjectOnPlane(Crew.transform.localPosition - stand, Vector3.up).magnitude;
                Require(travelled > .28f && walkDelta > .025f && SailorOnCamera(body),
                    $"walking moves the player {travelled:F3} m and deforms the rendered R06 mesh {walkDelta:F3} m");
                yield return Frames(18);
                stand = Crew.transform.localPosition;
                idle = SailorVertices(body, baked);
                var leftRest = model.InverseTransformPoint(leftFoot.position);
                var rightRest = model.InverseTransformPoint(rightFoot.position);
                yield return Frames(5);
                float stoppedDelta = SailorVertexTravel(body, baked, idle);
                Require(stoppedDelta < .006f && Vector3.Distance(stand, Crew.transform.localPosition) < .02f &&
                    Vector3.Distance(leftRest, model.InverseTransformPoint(leftFoot.position)) < .006f &&
                    Vector3.Distance(rightRest, model.InverseTransformPoint(rightFoot.position)) < .006f &&
                    !SailorDrawable(hammer), $"stopped player stays planted with a steady idle pose, skin drift={stoppedDelta:F3} m");

                int gunIndex = ApproachWeatherGun();
                yield return Frames(3);
                Vector3 orbit = Ship.transform.InverseTransformDirection(game.MainCamera.transform.forward);
                yield return UseKey();
                CheckGunView(Ship.Rig.Guns[gunIndex], 0f, 4f);
                Require(!SailorDrawable(body) && !SailorDrawable(hammer), "gun first person hides the R06 body and tool");
                yield return UseKey();
                CheckOrbit(orbit);
                Require(SailorOnCamera(body) && !SailorDrawable(hammer), "exiting the gun restores the same R06 body in third person");

                var water = Ship.Flooding;
                yield return KeyTap(UnityEngine.InputSystem.Key.F6);
                yield return Frames(8);
                Require(water.ActiveLeaks == 2, "damage drill produces two live repair sites");
                yield return ApproachRepairSite(0);
                var repairBefore = SailorVertices(body, baked);
                Enter("船壳破口");
                yield return Frames(6);
                float repairDelta = SailorVertexTravel(body, baked, repairBefore);
                Require(Crew.HoldProgress > .1f && water.IsLeaking(0) && repairDelta > .018f &&
                    SailorOnCamera(body) && SailorOnCamera(hammer),
                    $"repair animates the R06 skin {repairDelta:F3} m with its hammer on camera");
                float gripDistance = Vector3.Distance(hammer.transform.position, hand.position);
                Vector3 hammerAt = model.InverseTransformPoint(hammer.transform.position);
                Quaternion hammerFacing = Quaternion.Inverse(model.rotation) * hammer.transform.rotation;
                yield return Frames(5);
                float hammerTravel = Vector3.Distance(hammerAt, model.InverseTransformPoint(hammer.transform.position));
                float hammerAngle = Quaternion.Angle(hammerFacing, Quaternion.Inverse(model.rotation) * hammer.transform.rotation);
                gripDistance = Mathf.Max(gripDistance, Vector3.Distance(hammer.transform.position, hand.position));
                Require(gripDistance < .14f && hammerTravel > .018f && hammerAngle > 3f,
                    $"repair hammer travels {hammerTravel:F3} m and rotates {hammerAngle:F1}°, grip={gripDistance:F3} m from right hand");
                // Orbit to the working hand and capture the extended stroke inside the sail.
                Tick(new CrewCommand { Look = new Vector2(-55f / .115f, 140f), Zoom = 4f });
                yield return Frames(9);
                yield return PhotographRepair(hammer);
                yield return UseKey();
                Require(water.IsLeaking(0) && !SailorDrawable(hammer), "cancelled repair leaves the breach open and hides the hammer");
                foreach (int site in new[] { 0, Ship.Rig.LeakInside.Length - 1 })
                {
                    yield return ApproachRepairSite(site);
                    Enter("船壳破口");
                    yield return Frames(HullRepairFrames);
                    Require(!water.IsLeaking(site) && Crew.StationName.Length == 0 && !SailorDrawable(hammer),
                        $"completed repair {site} closes the breach and hides the hammer");
                }

                ApproachPump();
                yield return Frames(2);
                var pumpBefore = SailorVertices(body, baked);
                float level = water.WaterHeight;
                Enter("舱底泵");
                yield return Frames(5);
                float pumpDelta = SailorVertexTravel(body, baked, pumpBefore);
                Require(Crew.StationName == "舱底泵" && water.WaterHeight < level && pumpDelta > .012f &&
                    !SailorDrawable(hammer), $"pump lowers the water and animates the R06 skin {pumpDelta:F3} m with no hammer");
            }
            finally
            {
                Destroy(baked);
            }
        }

        Transform SailorBone(SkinnedMeshRenderer body, string label)
        {
            var bone = Array.Find(body.bones, b => b && b.name == label);
            Require(bone, "R06 bound bone " + label);
            return bone;
        }

        void CheckSailorStanding(Transform model, SkinnedMeshRenderer body, Mesh baked)
        {
            var skin = SailorVertices(body, baked);
            var skinToCrew = Crew.transform.worldToLocalMatrix * body.transform.localToWorldMatrix;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            foreach (var vertex in skin)
            {
                float y = skinToCrew.MultiplyPoint3x4(vertex).y;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
            float height = maxY - minY;
            float forward = Vector3.Dot(model.forward, Ship.Rig.Spawn.forward);
            Require(height > 1.68f && height < 1.92f && Mathf.Abs(minY) < .10f && forward > .98f,
                $"standing R06 is {height:F3} m tall, soles offset {minY:F3} m, facing spawn forward ({forward:F3})");
        }

        void CheckCostumeColor(SkinnedMeshRenderer body, string slot, string html)
        {
            var material = Array.Find(body.sharedMaterials, m => m && m.name == slot);
            Require(material && material.shader && material.shader.isSupported && material.HasProperty("_BaseColor"),
                "R06 costume slot binds a supported material " + slot);
            ColorUtility.TryParseHtmlString(html, out Color expected);
            Color actual = material.GetColor("_BaseColor");
            Vector3 rgb = new Vector3(actual.r, actual.g, actual.b);
            Vector3 source = new Vector3(expected.r, expected.g, expected.b);
            Color linear = expected.linear;
            Vector3 sourceLinear = new Vector3(linear.r, linear.g, linear.b);
            Require(Mathf.Min(Vector3.Distance(rgb, source), Vector3.Distance(rgb, sourceLinear)) < .045f &&
                actual.a > .95f, $"R06 {slot} uses source color {html}, actual={actual}");
        }

        bool SailorDrawable(Renderer renderer) =>
            renderer.gameObject.activeInHierarchy && renderer.enabled && !renderer.forceRenderingOff &&
            (game.MainCamera.cullingMask & (1 << renderer.gameObject.layer)) != 0 &&
            renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly;

        bool SailorOnCamera(Renderer renderer) =>
            SailorDrawable(renderer) &&
            GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(game.MainCamera), renderer.bounds);

        IEnumerator PhotographRepair(Renderer hammer)
        {
            float timeScale = Time.timeScale;
            bool crewEnabled = Crew.enabled;
            Texture2D withTool = null, withoutTool = null;
            try
            {
                Time.timeScale = 0f;
                Crew.enabled = false;
                yield return new WaitForEndOfFrame();
                withTool = ScreenCapture.CaptureScreenshotAsTexture();
                Vector3 center = game.MainCamera.WorldToScreenPoint(hammer.bounds.center);
                hammer.forceRenderingOff = true;
                yield return null;
                yield return new WaitForEndOfFrame();
                withoutTool = ScreenCapture.CaptureScreenshotAsTexture();
                var drawn = withTool.GetPixels32();
                var hidden = withoutTool.GetPixels32();
                int visible = 0;
                for (int y = Mathf.Max(0, (int)center.y - 60); y < Mathf.Min(withTool.height, (int)center.y + 60); y++)
                for (int x = Mathf.Max(0, (int)center.x - 60); x < Mathf.Min(withTool.width, (int)center.x + 60); x++)
                {
                    int i = y * withTool.width + x;
                    if (Math.Abs(drawn[i].r-hidden[i].r) + Math.Abs(drawn[i].g-hidden[i].g) + Math.Abs(drawn[i].b-hidden[i].b) > 30)
                        visible++;
                }
                File.WriteAllBytes(Path.Combine(output, "sailor-repair-" + type + ".png"), withTool.EncodeToPNG());
                Require(center.z > 0f && visible >= 80, $"repair hammer visible on screen ({visible} pixels)");
            }
            finally
            {
                hammer.forceRenderingOff = false;
                Crew.enabled = crewEnabled;
                Time.timeScale = timeScale;
                if (withTool) Destroy(withTool);
                if (withoutTool) Destroy(withoutTool);
            }
        }

        static Vector3[] SailorVertices(SkinnedMeshRenderer body, Mesh baked)
        {
            body.BakeMesh(baked);
            return baked.vertices;
        }

        static float SailorVertexTravel(SkinnedMeshRenderer body, Mesh baked, Vector3[] previous)
        {
            var current = SailorVertices(body, baked);
            float largest = 0f;
            for (int i = 0; i < current.Length; i++)
                largest = Mathf.Max(largest, Vector3.Distance(current[i], previous[i]));
            return largest;
        }
    }
}
