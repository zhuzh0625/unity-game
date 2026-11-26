using System.Collections.Generic;
using UnityEngine;

namespace Galleon
{
    internal enum NavalSound { Cannon, HullHit, Collision, Pistol, Cutlass, Medicine, Repair, Splash }
    internal enum SailorLine { Attack, Hit, Kill, Miss, Death }

    internal sealed class NavalAudio : MonoBehaviour
    {
        const string ResourceFolder = "NavalAudio/";
        const int VoiceCount = CrewDamageTarget.VoiceCount;
        const int VoiceChanceDenominator = 3;
        const int InitialChannelCount = 24;
        enum Use { Effect, Action, Voice }

        readonly struct EffectSettings
        {
            internal readonly string File;
            internal readonly float Gain, Near, Far;
            internal readonly int Priority;

            internal EffectSettings(string file, float gain, float near, float far, int priority)
            {
                File = file;
                Gain = gain;
                Near = near;
                Far = far;
                Priority = priority;
            }
        }

        static readonly EffectSettings[] Effects =
        {
            new("03-cannon-fire", .65f, 12, 240, 85),
            new("04-cannonball-hull-hit", .65f, 9, 130, 80),
            new("05-ship-collision", .58f, 12, 120, 78),
            new("06-flintlock-pistol", .60f, 6, 85, 90),
            new("07-cutlass-swing", .70f, 5, 34, 110),
            new("08-drink-medicine", .66f, 5, 24, 105),
            new("09-hull-repair", .56f, 5, 43, 115),
            new("10-cannon-splash", .60f, 7, 85, 115)
        };
        static readonly string[] VoiceFiles = { "attack", "hit", "kill", "miss", "death" };

        sealed class Channel
        {
            internal AudioSource Source;
            internal CrewDamageTarget Speaker;
            internal Use Use;
            internal NavalSound Sound;
            internal SailorLine Line;
            internal float Started;
        }

        static NavalAudio current;
        readonly List<Channel> channels = new(InitialChannelCount);
        readonly AudioClip[] effects = new AudioClip[Effects.Length];
        readonly AudioClip[,] voices = new AudioClip[VoiceFiles.Length, VoiceCount];
        readonly System.Random gullTiming = new();
        System.Random voiceRolls = new();
        AudioSource sea, gulls;
        NavalShip listeningShip;
        bool paused;
        float nextGulls;

        void Awake()
        {
            current = this;
            sea = CreateSource("Sea", false);
            sea.clip = Load("01-sea-loop");
            sea.loop = true;
            sea.volume = .32f;
            sea.priority = 64;
            gulls = CreateSource("Sea birds", false);
            gulls.clip = Load("02-seagulls");
            gulls.volume = .39f;
            gulls.priority = 120;
            for (int i = 0; i < Effects.Length; i++)
                effects[i] = Load(Effects[i].File);
            for (int line = 0; line < VoiceFiles.Length; line++)
                for (int voice = 0; voice < VoiceCount; voice++)
                    voices[line, voice] = Load($"voices/{VoiceFiles[line]}-{voice + 1}");
            for (int i = 0; i < InitialChannelCount; i++) AddChannel();
        }

        Channel AddChannel()
        {
            var channel = new Channel { Source = CreateSource($"Ship sound {channels.Count + 1}", true) };
            channels.Add(channel);
            return channel;
        }

        AudioSource CreateSource(string label, bool spatial)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(transform, false);
            var source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.dopplerLevel = 0f;
            return source;
        }

        static AudioClip Load(string file)
        {
            var clip = Resources.Load<AudioClip>(ResourceFolder + file);
            if (!clip) Debug.LogError($"Missing naval audio clip: {file}");
            else if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            return clip;
        }

        internal void BeginVoyage(NavalShip homeShip)
        {
            listeningShip = homeShip;
            paused = false;
            nextGulls = 3f;
            if (sea.clip) sea.Play();
        }

        internal void EndVoyage()
        {
            listeningShip = null;
            paused = false;
            sea.Stop();
            gulls.Stop();
            foreach (var channel in channels)
            {
                channel.Source.Stop();
                channel.Source.clip = null;
                channel.Speaker = null;
            }
        }

        internal void SetPaused(bool value)
        {
            if (!listeningShip || paused == value) return;
            paused = value;
            PauseSource(sea, value);
            PauseSource(gulls, value);
            foreach (var channel in channels) PauseSource(channel.Source, value);
        }

        static void PauseSource(AudioSource source, bool value)
        {
            if (value) source.Pause();
            else source.UnPause();
        }

        void LateUpdate()
        {
            if (!listeningShip || paused) return;
            nextGulls -= Time.deltaTime;
            if (nextGulls <= 0)
            {
                if (gulls.clip) gulls.Play();
                nextGulls = 18f + (float)gullTiming.NextDouble() * 14f;
            }
            foreach (var channel in channels)
                if (channel.Source.isPlaying && channel.Speaker && channel.Speaker.gameObject.activeInHierarchy)
                    channel.Source.transform.position = channel.Speaker.transform.position;
        }

        static bool CanPlay => current && current.listeningShip && !current.paused;

        internal static void Effect(NavalSound sound, Vector3 at, NavalShip emitter, NavalShip other = null)
        {
            if (CanPlay && (current.listeningShip == emitter || current.listeningShip == other))
                current.PlayEffect(sound, at, null);
        }

        void PlayEffect(NavalSound sound, Vector3 at, CrewDamageTarget actor)
        {
            var settings = Effects[(int)sound];
            var clip = effects[(int)sound];
            if (!clip) return;
            // A broadside can overlap incoming hull impacts. Let each ordinary effect finish.
            var channel = IdleChannel() ?? AddChannel();
            channel.Use = actor ? Use.Action : Use.Effect;
            channel.Sound = sound;
            channel.Speaker = actor;
            Play(channel, clip, at, settings.Gain, settings.Near, settings.Far, settings.Priority);
        }

        internal static void SetAction(CrewDamageTarget actor, NavalSound sound, bool active)
        {
            if (!current || !actor || (active && (!CanPlay || actor.AllegianceShip != current.listeningShip))) return;
            foreach (var channel in current.channels)
            {
                if (channel.Use != Use.Action || channel.Speaker != actor || channel.Sound != sound) continue;
                if (active && channel.Source.isPlaying) return;
                if (!active)
                {
                    channel.Source.Stop();
                    channel.Speaker = null;
                }
            }
            if (active) current.PlayEffect(sound, actor.transform.position, actor);
        }

        internal static void Voice(CrewDamageTarget speaker, SailorLine line)
        {
            if (!CanPlay || !speaker || speaker.AllegianceShip != current.listeningShip ||
                (uint)speaker.VoiceIndex >= VoiceCount ||
                (line != SailorLine.Death && speaker.Health <= 0)) return;
            current.Speak(speaker, line);
        }

        void Speak(CrewDamageTarget speaker, SailorLine line)
        {
            var clip = voices[(int)line, speaker.VoiceIndex];
            if (!clip) return;
            int priority = line switch
            {
                SailorLine.Death => 45,
                SailorLine.Kill => 52,
                SailorLine.Attack => 72,
                _ => 62
            };
            bool player = speaker is CrewPlayer;
            if (player) priority -= 8;
            Channel own = null;
            foreach (var channel in channels)
            {
                if (channel.Use != Use.Voice || !channel.Source.isPlaying) continue;
                if (channel.Speaker == speaker)
                {
                    if (channel.Line == line || channel.Source.priority < priority) return;
                    own = channel;
                }
                // A battery can fire several guns in one frame. Keep nearby identical calls intelligible.
                else if (!player && line == SailorLine.Attack && channel.Line == line &&
                    Time.time - channel.Started < .55f &&
                    (channel.Source.transform.position - speaker.transform.position).sqrMagnitude < 144f)
                    return;
            }
            if (voiceRolls.Next(VoiceChanceDenominator) != 0)
            {
                if (line == SailorLine.Death && own != null) own.Source.Stop();
                return;
            }
            var selected = own ?? IdleChannel() ?? AddChannel();
            selected.Use = Use.Voice;
            selected.Line = line;
            selected.Speaker = speaker;
            Play(selected, clip, speaker.transform.position, .86f, 7, 78, priority);
        }

        internal void SetVoiceSeed(int seed) => voiceRolls = new System.Random(seed);

        Channel IdleChannel()
        {
            foreach (var channel in channels)
                if (!channel.Source.isPlaying) return channel;
            return null;
        }

        static void Play(Channel channel, AudioClip clip, Vector3 at, float gain, float near, float far, int priority)
        {
            var source = channel.Source;
            source.Stop();
            source.transform.position = at;
            source.minDistance = near;
            source.maxDistance = far;
            source.volume = gain;
            source.priority = priority;
            source.clip = clip;
            channel.Started = Time.time;
            source.Play();
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }
    }
}
