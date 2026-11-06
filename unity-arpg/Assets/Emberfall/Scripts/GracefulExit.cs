using System.Collections;
using UnityEngine;

namespace Emberfall
{
    // Complete the active player-loop frame before accepting a native window-close request.
    public class GracefulExit : MonoBehaviour
    {
        bool approved, pending;
        int exitCode;
        public static void Request(int code=0)
        {
            var controller=FindFirstObjectByType<GracefulExit>();
            if(!controller){Application.Quit(code);return;}
            controller.exitCode=code;controller.WantsToQuit();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){new GameObject("Graceful exit").AddComponent<GracefulExit>();}
        void OnEnable(){Application.wantsToQuit+=WantsToQuit;}
        void OnDisable(){Application.wantsToQuit-=WantsToQuit;}
        bool WantsToQuit()
        {
            if(approved)return true;
            if(!pending){pending=true;StartCoroutine(AfterFrame());}
            return false;
        }
        IEnumerator AfterFrame()
        {
            yield return null;
            if(Adventure.Instance)Adventure.Instance.Save(false);
            approved=true;Application.Quit(exitCode);
        }
    }
}
