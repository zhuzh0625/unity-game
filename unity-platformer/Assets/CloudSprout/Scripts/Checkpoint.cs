using UnityEngine;
namespace CloudSprout
{
    public sealed class Checkpoint : MonoBehaviour
    {
        bool active;
        void OnTriggerEnter(Collider other){if(active||!other.GetComponent<SproutPlayer>()||GameSession.Instance.State!=GameState.Playing)return;active=true;GameSession.Instance.SetCheckpoint(transform.position+new Vector3(.8f,.2f,0));}
    }
}
