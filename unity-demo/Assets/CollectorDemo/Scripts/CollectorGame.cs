using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CollectorDemo
{
    /// <summary>A small keyboard-controlled collection game; scene references are serialized.</summary>
    public sealed class CollectorGame : MonoBehaviour
    {
        public Transform player;
        public Transform ball;
        public Transform[] crystals;
        public TMP_Text scoreText;
        public TMP_Text timeText;
        public TMP_Text statusText;
        public GameObject victoryPanel;
        public TMP_Text victoryText;
        public UnityEngine.UI.Button restartButton;
        public UnityEngine.UI.Image progressFill;
        public float speed = 5.5f;
        public Vector2 arenaLimit = new Vector2(7.7f, 5.7f);
        public int Score { get; private set; }
        public float Elapsed { get; private set; }
        public bool Won => Score == crystals.Length;

        Vector3[] crystalOrigins;
        Vector3 spawn;
        float animationTime;

        void Awake()
        {
            spawn = player.position;
            crystalOrigins = new Vector3[crystals.Length];
            for (int i = 0; i < crystals.Length; i++) crystalOrigins[i] = crystals[i].position;
            restartButton.onClick.AddListener(Restart);
            Restart();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            animationTime += dt;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Restart();

            if (!Won)
            {
                Elapsed += dt;
                Vector2 direction = Vector2.zero;
                if (keyboard != null)
                {
                    direction.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                    direction.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                                - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
                }
                direction = Vector2.ClampMagnitude(direction, 1f);
                Vector3 before = player.position;
                Vector3 next = before + new Vector3(direction.x, 0, direction.y) * speed * dt;
                next.x = Mathf.Clamp(next.x, -arenaLimit.x, arenaLimit.x);
                next.z = Mathf.Clamp(next.z, -arenaLimit.y, arenaLimit.y);
                player.position = next;
                Vector3 delta = next - before;
                if (delta.sqrMagnitude > 0)
                    ball.Rotate(new Vector3(delta.z, 0, -delta.x).normalized, delta.magnitude * 110f, Space.World);

                for (int i = 0; i < crystals.Length; i++)
                {
                    if (!crystals[i].gameObject.activeSelf) continue;
                    Vector3 offset = player.position - crystalOrigins[i];
                    offset.y = 0;
                    if (offset.sqrMagnitude < 0.9f * 0.9f)
                    {
                        crystals[i].gameObject.SetActive(false);
                        Score++;
                        statusText.text = Won ? "ALL ENERGY RECOVERED" : "NICE! KEEP COLLECTING";
                        RefreshScore();
                    }
                }
                if (Won)
                {
                    victoryPanel.SetActive(true);
                    victoryText.text = $"All {crystals.Length} crystals collected in {Elapsed:0.0}s";
                }
                timeText.text = $"{Elapsed:000.0}<size=16> s</size>";
            }

            for (int i = 0; i < crystals.Length; i++)
            {
                if (!crystals[i].gameObject.activeSelf) continue;
                crystals[i].position = crystalOrigins[i] + Vector3.up * (Mathf.Sin(animationTime * 2.8f + i) * 0.13f);
                crystals[i].Rotate(Vector3.up, 60f * dt, Space.World);
            }
        }

        public void Restart()
        {
            Score = 0;
            Elapsed = 0;
            player.position = spawn;
            ball.localRotation = Quaternion.identity;
            foreach (var crystal in crystals) crystal.gameObject.SetActive(true);
            victoryPanel.SetActive(false);
            statusText.text = "COLLECT THE 8 ENERGY CRYSTALS";
            timeText.text = "000.0<size=16> s</size>";
            RefreshScore();
        }

        void RefreshScore()
        {
            scoreText.text = $"{Score:00}<color=#648491> / {crystals.Length:00}</color>";
            progressFill.fillAmount = (float)Score / crystals.Length;
        }

        void OnDestroy()
        {
            if (restartButton != null) restartButton.onClick.RemoveListener(Restart);
        }
    }
}
