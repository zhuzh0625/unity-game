using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Emberfall
{
    // Opt-in native input diagnostics; never records text or unrelated application input.
    public class NativeInputProbe : MonoBehaviour
    {
        int events, texts, lastFrame;
        float next;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-native-input-probe")>=0)new GameObject("Native input probe").AddComponent<NativeInputProbe>();
        }
        void Start(){InputSystem.onEvent+=Observe;if(Keyboard.current!=null)Keyboard.current.onTextInput+=Text;}
        void Observe(InputEventPtr ptr,InputDevice device){if(device is Keyboard)events++;}
        void Text(char value){texts++;}
        void Update()
        {
            var kb=Keyboard.current;
            if(kb!=null)foreach(var key in new[]{Key.B,Key.Escape,Key.E,Key.D,Key.F5})
                if(kb[key].wasPressedThisFrame)Debug.Log("NATIVE_PROBE pressed="+key);
            if(Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+2;
            Debug.Log($"NATIVE_PROBE frame={Time.frameCount} delta={Time.frameCount-lastFrame} dt={Time.unscaledDeltaTime:0.000} focused={Application.isFocused} vsync={QualitySettings.vSyncCount} target={Application.targetFrameRate} keyboard={kb?.name} enabled={kb?.enabled} events={events} textEvents={texts}");lastFrame=Time.frameCount;
        }
        void OnDestroy(){InputSystem.onEvent-=Observe;if(Keyboard.current!=null)Keyboard.current.onTextInput-=Text;}
    }
}
