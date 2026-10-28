using UnityEngine;
namespace CloudSprout
{
    public sealed class Collectible : MonoBehaviour
    {
        public bool seed;
        Vector3 initial; bool collected;
        void Start(){initial=transform.position;}
        void Update(){float phase=seed?initial.x:0;transform.position=initial+Vector3.up*(Mathf.Sin(Time.time*2.5f+phase)*.10f);transform.Rotate(0,75*Time.deltaTime,0,Space.World);}
        void OnTriggerEnter(Collider other){if(collected||!other.GetComponent<SproutPlayer>()||GameSession.Instance.State!=GameState.Playing)return;collected=true;GameSession.Instance.Collect(seed,transform.position);gameObject.SetActive(false);}
    }
}
