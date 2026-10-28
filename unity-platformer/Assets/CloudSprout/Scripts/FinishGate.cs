using UnityEngine;
namespace CloudSprout
{
    public sealed class FinishGate:MonoBehaviour
    {
        void OnTriggerEnter(Collider other){if(other.GetComponent<SproutPlayer>())GameSession.Instance.Finish();}
    }
}
