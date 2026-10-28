#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

namespace CloudSprout
{
    // Editor-only regression: drive the actual controller, never call RewardBlock.Hit directly.
    public sealed class RewardBlockVerification : MonoBehaviour
    {
        public string Status = "Ready";
        public bool PauseOnRewardPreview;
        public List<string> results = new List<string>();
        GameSession game;
        SproutPlayer player;
        RewardBlock block;
        RewardBlockContactProbe probe;
        bool running;

        public void StartChecks()
        {
            if (running) return;
            running = true;
            StartCoroutine(Checks());
        }

        void Check(bool passed, string description)
        {
            results.Add((passed ? "PASS " : "FAIL ") + description);
            Debug.Log(results[results.Count - 1]);
            Save();
        }

        void Save()
        {
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/reward-block.txt", string.Join("\n", results));
        }

        void Place(Vector3 position)
        {
            player.autoMove = 0;
            player.autoJump = false;
            player.autoHold = false;
            player.autoRun = false;
            player.Respawn(position);
            probe.Clear();
        }

        IEnumerator SettleBelow()
        {
            Place(new Vector3(block.transform.position.x, .2f, 0));
            yield return new WaitForSeconds(.55f);
        }

        IEnumerator Checks()
        {
            Status = "Running";
            results.Clear();
            game = GameSession.Instance;
            player = game.player;
            block = FindObjectsByType<RewardBlock>(FindObjectsSortMode.None)
                .OrderBy(candidate => candidate.transform.position.x).First();
            probe = player.gameObject.AddComponent<RewardBlockContactProbe>();
            probe.Target = block.GetComponent<Collider>();
            game.Begin();
            player.automation = true;
            Check(!block.Used, "Test starts with an unused reward block");
            if (block.Used)
            {
                Finish();
                yield break;
            }

            int originalBlocks = game.BlocksHit;
            int originalCoins = game.Coins;
            Place(block.transform.position + new Vector3(-.95f, -.9f, 0));
            player.autoMove = 1;
            float deadline = Time.time + .4f;
            while (!probe.Side && Time.time < deadline) yield return null;
            player.autoMove = 0;
            Check(probe.Side && !block.Used && game.BlocksHit == originalBlocks && game.Coins == originalCoins,
                "Actual side contact does not award coins");

            var bounds = probe.Target.bounds;
            Place(new Vector3(block.transform.position.x, bounds.max.y + .4f, 0));
            deadline = Time.time + 1;
            while (!probe.Top && Time.time < deadline) yield return null;
            Check(probe.Top && !block.Used && game.BlocksHit == originalBlocks && game.Coins == originalCoins,
                "Actual landing on top does not award coins");

            // Settle first so the ordinary trail coin below the block is collected before counting the reward.
            yield return SettleBelow();
            Check(player.Grounded, "Player is grounded beneath the reward block");
            int beforeCoins = game.Coins;
            int beforeBlocks = game.BlocksHit;
            probe.Clear();
            player.autoJump = true;
            player.autoHold = true;
            deadline = Time.time + 1.5f;
            while (!block.Used && Time.time < deadline) yield return null;
            Check(probe.Underside && block.Used && game.BlocksHit == beforeBlocks + 1 && game.Coins == beforeCoins + 5,
                "First real head hit awards exactly five coins and records one block");

            yield return new WaitForSeconds(.3f);
            var reward = GameObject.Find("Reward Coins");
            bool visibleCoin = reward && reward.GetComponentsInChildren<Renderer>(true)
                .Any(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy);
            Check(visibleCoin, "Reward has active visible coin renderers after the head hit");
            var toast = game.hud.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(label => label.name == "ToastText");
            Check(toast && toast.isActiveAndEnabled && toast.text.Contains("+5 COINS"),
                "HUD displays the +5 COINS reward message");
            var counter = game.hud.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(label => label.name == "Coins");
            Check(counter && counter.text == "COINS  " + (beforeCoins + 5).ToString("000"),
                "HUD coin total matches the five-coin reward");

            if (PauseOnRewardPreview)
            {
                // Freeze the reward mid-animation without opening the pause modal or expiring its toast.
                Status = "Reward preview";
                game.enabled = false;
                game.hud.ShowToast("+5 COINS");
                Time.timeScale = 0;
                while (Time.timeScale == 0) yield return null;
                game.enabled = true;
                Status = "Running";
            }

            player.autoHold = false;
            yield return SettleBelow();
            beforeCoins = game.Coins;
            beforeBlocks = game.BlocksHit;
            probe.Clear();
            player.autoJump = true;
            player.autoHold = true;
            deadline = Time.time + 1.5f;
            while (!probe.Underside && Time.time < deadline) yield return null;
            yield return new WaitForSeconds(.1f);
            Check(probe.Underside && game.Coins == beforeCoins && game.BlocksHit == beforeBlocks,
                "Repeated real head hit cannot award again");
            Finish();
        }

        void Finish()
        {
            player.autoMove = 0;
            player.autoJump = false;
            player.autoHold = false;
            player.automation = false;
            if (game.State == GameState.Playing) game.TogglePause();
            if (probe) Destroy(probe);
            Status = results.Any(result => result.StartsWith("FAIL ")) ? "Failed" : "Complete";
            running = false;
            Save();
        }
    }

    // Observe collision normals on the same GameObject that owns CharacterController.
    public sealed class RewardBlockContactProbe : MonoBehaviour
    {
        public Collider Target;
        public bool Side, Top, Underside;
        public void Clear() { Side = Top = Underside = false; }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.collider != Target) return;
            if (Mathf.Abs(hit.normal.y) < .5f) Side = true;
            if (hit.normal.y > .5f) Top = true;
            if (hit.normal.y < -.5f) Underside = true;
        }
    }
}
#endif
