#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CloudSprout
{
    // Observe real Update motion; do not duplicate its formula or alter game state.
    public sealed class CoinLayoutVerification : MonoBehaviour
    {
        public string Status = "Ready";
        public List<string> results = new List<string>();
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
            File.WriteAllText("Verification/coin-motion.txt", string.Join("\n", results));
        }

        void Finish()
        {
            Status = results.Any(result => result.StartsWith("FAIL ")) ? "Failed" : "Complete";
            running = false;
            Save();
        }

        IEnumerator Checks()
        {
            Status = "Running";
            results.Clear();
            var game = GameSession.Instance;
            Check(game && game.State == GameState.Menu, "Verification starts in the menu");
            if (!game || game.State != GameState.Menu)
            {
                Finish();
                yield break;
            }

            // Let every Collectible finish Start and one Update before taking the baseline.
            yield return null;
            var coins = FindObjectsByType<Collectible>(FindObjectsSortMode.None)
                .Where(coin => !coin.seed).OrderBy(coin => coin.transform.position.x).ToArray();
            Check(coins.Length > 1, "Ordinary coins are present (count=" + coins.Length + ")");
            if (coins.Length < 2)
            {
                Finish();
                yield break;
            }

            var initial = coins.Select(coin => coin.transform.position).ToArray();
            var positions = new Vector3[coins.Length];
            float start = Time.unscaledTime;
            float maxPhaseError = 0, maxLateralMotion = 0;
            float minimumSpacing = float.PositiveInfinity;
            float minimumHeight = initial[0].y, maximumHeight = initial[0].y;
            int frames = 0;
            bool intact = true, stayedInMenu = true;

            do
            {
                stayedInMenu &= game && game.State == GameState.Menu;
                if (coins.Any(coin => !coin || !coin.isActiveAndEnabled))
                {
                    intact = false;
                    break;
                }

                for (int i = 0; i < coins.Length; i++) positions[i] = coins[i].transform.position;
                float referenceOffset = positions[0].y - initial[0].y;
                minimumHeight = Mathf.Min(minimumHeight, positions[0].y);
                maximumHeight = Mathf.Max(maximumHeight, positions[0].y);
                for (int i = 0; i < coins.Length; i++)
                {
                    maxPhaseError = Mathf.Max(maxPhaseError,
                        Mathf.Abs((positions[i].y - initial[i].y) - referenceOffset));
                    maxLateralMotion = Mathf.Max(maxLateralMotion,
                        Mathf.Abs(positions[i].x - initial[i].x),
                        Mathf.Abs(positions[i].z - initial[i].z));
                    for (int j = i + 1; j < coins.Length; j++)
                    {
                        float distance = Vector2.Distance(
                            new Vector2(positions[i].x, positions[i].y),
                            new Vector2(positions[j].x, positions[j].y));
                        minimumSpacing = Mathf.Min(minimumSpacing, distance);
                    }
                }
                frames++;
                yield return null;
            }
            while (Time.unscaledTime - start < 3f);

            float span = maximumHeight - minimumHeight;
            Check(stayedInMenu && intact, "Menu and all ordinary coins remain active throughout observation");
            Check(frames >= 2 && Time.unscaledTime - start >= 3f,
                "Observed actual coin updates for at least three seconds (frames=" + frames + ")");
            Check(maxPhaseError <= .0001f,
                "All coins share the same vertical displacement (maximum difference=" + maxPhaseError.ToString("F6") + ")");
            Check(maxLateralMotion <= .0001f,
                "Coins keep their original X/Z coordinates (maximum drift=" + maxLateralMotion.ToString("F6") + ")");
            Check(minimumSpacing >= 1.5f,
                "Every sampled XY center distance is at least 1.5 m (minimum=" + minimumSpacing.ToString("F4") + ")");
            Check(span >= .18f && span <= .22f,
                "Observed vertical peak-to-trough span is approximately 0.2 m (span=" + span.ToString("F4") + ")");
            Finish();
        }
    }
}
#endif
