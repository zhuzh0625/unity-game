using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CollectorDemo;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class VerifyCollectorDemo
{
    // Uses a temporary virtual keyboard so the actual Input System and Update loop are exercised.
    // It never teleports the player or writes the score.
    public static async Task<string> Run()
    {
        var game = UnityEngine.Object.FindFirstObjectByType<CollectorGame>();
        if (!Application.isPlaying || game == null) throw new Exception("Run CollectorDemo first.");
        var keyboard = InputSystem.AddDevice<Keyboard>("DemoVerificationKeyboard");
        try
        {
            game.Restart();
            int firstFrame = Time.frameCount;
            var origin = game.player.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            await Task.Delay(220);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Task.Delay(70);
            Require(game.player.position.z > origin.z + .5f, "WASD input did not move the player.");
            Require(Time.frameCount > firstFrame, "Frames did not advance.");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
            await Task.Delay(1800);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Task.Delay(70);
            Require(Mathf.Abs(game.player.position.x - game.arenaLimit.x) < .02f, "Arena boundary or arrow key failed.");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            await Task.Delay(70);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Task.Delay(70);
            Require(Vector3.Distance(game.player.position, origin) < .02f && game.Score == 0, "R restart failed.");

            for (int i = 0; i < game.crystals.Length; i++)
            {
                var crystal = game.crystals[i];
                float deadline = Time.realtimeSinceStartup + 8f;
                while (crystal.gameObject.activeSelf)
                {
                    if (Time.realtimeSinceStartup > deadline) throw new Exception("Timed out collecting crystal " + i);
                    var delta = crystal.position - game.player.position;
                    var keys = new List<Key>();
                    if (Mathf.Abs(delta.x) > .18f) keys.Add(delta.x > 0 ? Key.D : Key.A);
                    if (Mathf.Abs(delta.z) > .18f) keys.Add(delta.z > 0 ? Key.W : Key.S);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.ToArray()));
                    await Task.Delay(40);
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                await Task.Delay(50);
            }
            Require(game.Score == 8 && game.Won, "All-crystal score or victory failed.");
            Require(game.victoryPanel.activeSelf, "Victory UI is not visible.");
            Require(game.progressFill.fillAmount == 1f, "Progress bar is inconsistent with score.");
            Require(game.scoreText.text.StartsWith("08"), "HUD score is not updated.");
            float finishTime = game.Elapsed;
            await Task.Delay(150);
            Require(Mathf.Approximately(finishTime, game.Elapsed), "Timer did not stop after victory.");
            return $"PASS: live frames, WASD, arrow keys, arena bounds, R restart, 8/8 pickups, score HUD, progress, victory, timer freeze. Finished in {finishTime:0.0}s. Victory left visible for screenshot/button check.";
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
