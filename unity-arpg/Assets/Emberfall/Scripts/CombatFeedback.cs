using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Emberfall
{
    public enum CombatSound { Swing, Hit, Hurt, Cast, Heal, Loot, Dodge, Warning, Boss }
    public class CombatFeedback : MonoBehaviour
    {
        sealed class Effect { public SpriteRenderer renderer; public Color color; public float life,duration,expansion; public Vector2 velocity,size; }
        sealed class NumberLabel { public TMP_Text text; public float life; public Color color; }
        readonly Stack<Effect> freeEffects = new Stack<Effect>(96);
        readonly Dictionary<GameObject, Effect> effects = new Dictionary<GameObject, Effect>(96);
        readonly List<NumberLabel> numbers = new List<NumberLabel>(40);
        readonly Stack<NumberLabel> freeNumbers = new Stack<NumberLabel>(40);
        readonly List<ProjectileVisual> projectiles=new List<ProjectileVisual>(24);
        readonly Stack<ProjectileVisual> freeProjectiles=new Stack<ProjectileVisual>(24);
        public IReadOnlyList<ProjectileVisual> Projectiles => projectiles;
        readonly AudioClip[] sounds = new AudioClip[9];
        TMP_FontAsset font;
        AudioSource source;
        Transform root;
        public bool SoundsReady => sounds[0] && sounds[8] && source;
        public void Initialize(bool muted)
        {
            root = new GameObject("Reusable combat feedback").transform; root.SetParent(transform, false);
            font = Resources.Load<TMP_FontAsset>("Fonts/EmberfallChinese");
            source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0; source.volume = .38f;
            for (int i = 0; i < sounds.Length; i++) sounds[i] = Synthesize((CombatSound)i);
            for (int i = 0; i < 64; i++) freeEffects.Push(CreateEffect());
            for (int i = 0; i < 32; i++) freeNumbers.Push(CreateNumber());
            for(int kind=0;kind<4;kind++)for(int frame=0;frame<4;frame++)ProjectileArt.Frame((ProjectileKind)kind,frame);
            for(int i=0;i<12;i++)freeProjectiles.Push(CreateProjectile());
            SetMuted(muted);
        }
        ProjectileVisual CreateProjectile()
        {
            var go=new GameObject("Pooled projectile");go.transform.SetParent(root,false);
            var value=go.AddComponent<ProjectileVisual>();value.Initialize();go.SetActive(false);projectiles.Add(value);return value;
        }
        public ProjectileVisual TakeProjectile(ProjectileKind kind,Vector2 position,Vector2 velocity)
        {
            var value=freeProjectiles.Count>0?freeProjectiles.Pop():CreateProjectile();value.Begin(kind,position,velocity);return value;
        }
        public void ReleaseProjectile(ProjectileVisual value)
        {
            if(!value||!value.gameObject.activeSelf)return;
            value.gameObject.SetActive(false);freeProjectiles.Push(value);
        }
        Effect CreateEffect()
        {
            var go = new GameObject("Pooled combat sprite"); go.transform.SetParent(root, false);
            var effect = new Effect { renderer = go.AddComponent<SpriteRenderer>() };
            effects.Add(go, effect); go.SetActive(false); return effect;
        }
        NumberLabel CreateNumber()
        {
            var go = new GameObject("Pooled damage number"); go.transform.SetParent(root, false);
            var text = go.AddComponent<TextMeshPro>(); text.font = font; text.fontSize = 3.6f;
            text.alignment = TextAlignmentOptions.Center; text.text = "0"; text.GetComponent<MeshRenderer>().sortingOrder = 700;
            var value = new NumberLabel { text = text }; numbers.Add(value); go.SetActive(false); return value;
        }
        public GameObject Take(Sprite sprite, Vector2 position, Vector2 size, Color color, int order)
        {
            var effect = freeEffects.Count > 0 ? freeEffects.Pop() : CreateEffect();
            var sr = effect.renderer; sr.sprite = sprite; sr.sortingOrder = order; sr.color = effect.color = color;
            effect.life=0;
            sr.transform.SetPositionAndRotation(position, Quaternion.identity); sr.transform.localScale = new Vector3(size.x, size.y, 1);
            sr.gameObject.SetActive(true); return sr.gameObject;
        }
        public void Release(GameObject go)
        {
            if (!go || !go.activeSelf || !effects.TryGetValue(go, out var effect)) return;
            effect.life=0;go.SetActive(false); freeEffects.Push(effect);
        }
        GameObject Timed(Sprite sprite,Vector2 point,Vector2 size,Color color,float duration,Vector2 velocity,float expansion=0)
        {
            var go=Take(sprite,point,size,color,450);var effect=effects[go];
            effect.life=effect.duration=duration;effect.velocity=velocity;effect.size=size;effect.expansion=expansion;return go;
        }
        public void Muzzle(Vector2 point,ProjectileKind kind,Vector2 direction)
        {
            if(kind==ProjectileKind.Arrow)return;
            var color=ProjectileArt.Tint(kind);color.a=.7f;
            Timed(WorldArt.Glow,point+ProjectileVisual.HeightOffset,Vector2.one*.8f,color,.13f,direction*.4f,1);
        }
        public void Impact(Vector2 point,ProjectileKind kind,Vector2 direction)
        {
            point+=ProjectileVisual.HeightOffset;
            bool arrow=kind==ProjectileKind.Arrow;var color=ProjectileArt.Tint(kind);
            if(!arrow)
            {
                Timed(WorldArt.Glow,point,Vector2.one*1.1f,new Color(color.r,color.g,color.b,.7f),.18f,Vector2.zero,1.8f);
                Timed(WorldArt.Ring,point,Vector2.one*.35f,color,.2f,Vector2.zero,3.5f);
            }
            for(int i=0;i<(arrow?4:7);i++)
            {
                float a=i*2.39996f;var velocity=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(arrow?1.1f:1.8f)-direction*.5f;
                var go=Timed(arrow?WorldArt.Square:WorldArt.Glow,point,arrow?new Vector2(.16f,.035f):Vector2.one*.19f,
                    i%2==0?new Color(1,.9f,.64f):color,.16f+i*.018f,velocity);
                go.transform.rotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg);
            }
        }
        public void EnemySwing(Vector2 point,Vector2 direction)
        {
            var go=Timed(WorldArt.Slash,point+ProjectileVisual.HeightOffset+direction*.35f,Vector2.one*1.8f,new Color(1,.48f,.24f,.72f),.16f,direction*.3f);
            go.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
        }
        public void Fade(GameObject go, float alpha)
        {
            if (!go || !effects.TryGetValue(go, out var effect)) return;
            var color = effect.color; color.a *= Mathf.Clamp01(alpha); effect.renderer.color = color;
        }
        public void Number(Vector2 position, float value, Color color)
        {
            var number = freeNumbers.Count > 0 ? freeNumbers.Pop() : CreateNumber();
            number.life = .8f; number.color = color; number.text.color = color;
            number.text.SetText("{0:0}", Mathf.Ceil(value)); number.text.transform.position = position + Vector2.up * .9f;
            number.text.gameObject.SetActive(true);
        }
        public void Clear()
        {
            freeEffects.Clear(); foreach (var effect in effects.Values) { effect.life=0;effect.renderer.gameObject.SetActive(false); freeEffects.Push(effect); }
            freeNumbers.Clear(); foreach (var number in numbers) { number.life = 0; number.text.gameObject.SetActive(false); freeNumbers.Push(number); }
            freeProjectiles.Clear();foreach(var value in projectiles){value.gameObject.SetActive(false);freeProjectiles.Push(value);}
        }
        void Update()
        {
            if (Adventure.Instance && Adventure.Instance.SimulationPaused) return;
            float dt = Mathf.Min(Time.deltaTime, .05f);
            foreach(var effect in effects.Values)
            {
                if(effect.life<=0)continue;
                effect.life-=dt;if(effect.life<=0){Release(effect.renderer.gameObject);continue;}
                effect.renderer.transform.position+=(Vector3)(effect.velocity*dt);
                effect.renderer.transform.localScale=(Vector3)(effect.size*(1+(1-effect.life/effect.duration)*effect.expansion))+Vector3.forward;
                var tint=effect.color;tint.a*=Mathf.Clamp01(effect.life/effect.duration);effect.renderer.color=tint;
            }
            foreach (var number in numbers)
            {
                if (number.life <= 0) continue;
                number.life -= dt;
                if (number.life <= 0) { number.text.gameObject.SetActive(false); freeNumbers.Push(number); continue; }
                number.text.transform.position += Vector3.up * dt * .8f;
                var color = number.color; color.a = Mathf.Min(1, number.life * 3); number.text.color = color;
            }
        }
        public void SetMuted(bool muted) { if (source) source.mute = muted; }
        public void Play(CombatSound sound, float volume = 1) { if (source && !source.mute) source.PlayOneShot(sounds[(int)sound], volume); }
        static AudioClip Synthesize(CombatSound kind)
        {
            const int rate = 22050;
            float duration = kind == CombatSound.Boss ? .4f : kind == CombatSound.Heal ? .35f : .16f;
            var samples = new float[Mathf.CeilToInt(rate * duration)];
            var random = new System.Random(117 + (int)kind); float filtered = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate, progress = t / duration;
                filtered = filtered * .72f + ((float)random.NextDouble() * 2 - 1) * .28f;
                float envelope = Mathf.Sin(Mathf.PI * progress) * Mathf.Pow(1 - progress, .6f);
                float wave;
                if (kind == CombatSound.Swing || kind == CombatSound.Dodge) wave = filtered * 1.5f;
                else if (kind == CombatSound.Hit || kind == CombatSound.Hurt || kind == CombatSound.Boss)
                    wave = filtered * .8f + Mathf.Sin(2 * Mathf.PI * (kind == CombatSound.Boss ? 65 : 130) * t) * .4f;
                else
                {
                    float frequency = kind == CombatSound.Warning ? 260 : kind == CombatSound.Loot ? 760 : kind == CombatSound.Heal ? 520 : 400;
                    wave = Mathf.Sin(2 * Mathf.PI * frequency * (t + .5f * t * t)) * .45f;
                }
                samples[i] = wave * envelope * .45f;
            }
            var clip = AudioClip.Create("Emberfall " + kind, samples.Length, 1, rate, false); clip.SetData(samples, 0); return clip;
        }
        void OnDestroy() { foreach (var clip in sounds) if (clip) Destroy(clip); }
    }
}
