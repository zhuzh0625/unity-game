using UnityEngine;
using UnityEngine.Rendering;

namespace Galleon
{
    internal sealed class PistolSmoke : MonoBehaviour
    {
        const float Life = 2f;
        static readonly int Progress = Shader.PropertyToID("_Progress");
        static Material material;
        static Sprite sprite;

        Transform owner;
        Camera view;
        Vector3 origin, direction;
        SpriteRenderer plume;
        MaterialPropertyBlock properties;
        float age;

        internal static void Emit(Transform owner, Vector3 origin, Vector3 direction)
        {
            if (!sprite)
            {
                var texture = Resources.Load<Texture2D>("Crew/PistolPlume");
                var shader = Resources.Load<Shader>("Crew/PistolSmokeUnlit");
                if (!texture || !shader) throw new System.InvalidOperationException("Missing pistol smoke resources.");
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(.5f, .5f), texture.width, 0, SpriteMeshType.FullRect);
                material = new Material(shader);
                material.mainTexture = texture;
            }
            var smoke = new GameObject("手枪烟雾").AddComponent<PistolSmoke>();
            smoke.owner = owner;
            smoke.origin = origin;
            smoke.direction = direction.normalized;
            smoke.view = Camera.main;
            smoke.plume = smoke.gameObject.AddComponent<SpriteRenderer>();
            smoke.plume.sprite = sprite;
            smoke.plume.sharedMaterial = material;
            smoke.plume.shadowCastingMode = ShadowCastingMode.Off;
            smoke.plume.receiveShadows = false;
            smoke.properties = new MaterialPropertyBlock();
            smoke.Draw();
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            if (!owner || !owner.gameObject.activeInHierarchy || age >= Life)
            {
                plume.enabled = false;
                Destroy(gameObject);
                return;
            }
            Draw();
        }

        void Draw()
        {
            if (!view) return;
            float progress = age / Life;
            float late = Mathf.SmoothStep(0f, 1f, progress);
            float burst = 1f - Mathf.Exp(-8.5f * (age + .025f));
            float length = .09f + .87f * burst + .10f * late;
            float height = .052f + .39f * burst + .10f * late;
            float drift = .008f + .113f * (1f - Mathf.Exp(-2.2f * age)) + .052f * late;
            float rise = .013f * age + .35f * Mathf.Pow(progress, 1.18f);
            transform.position = origin + direction * (drift + .5f * length)
                + Vector3.up * (rise + .255f * height);

            // Keep the plume's neck at the shot end in a side view and give the
            // cloud a round cross-section when sighted directly down its axis.
            Vector3 look = (transform.position - view.transform.position).normalized;
            Vector3 across = Vector3.ProjectOnPlane(direction, look);
            float projection = across.magnitude;
            across = projection > .015f ? across / projection : view.transform.right;
            Vector3 up = Vector3.Cross(look, across).normalized;
            transform.rotation = Quaternion.LookRotation(look, up);
            float width = Mathf.Lerp(height, length, projection);
            float upright = Vector3.Dot(up, view.transform.up) >= 0f ? 1f : -1f;
            transform.localScale = new Vector3(width, upright * height / sprite.bounds.size.y, 1f);
            properties.SetFloat(Progress, progress);
            plume.SetPropertyBlock(properties);
        }
    }
}
