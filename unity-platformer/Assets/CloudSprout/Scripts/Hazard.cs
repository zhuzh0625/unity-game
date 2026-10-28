using UnityEngine;
namespace CloudSprout
{
    public sealed class Hazard : MonoBehaviour
    {
        void OnTriggerStay(Collider other){var p=other.GetComponent<SproutPlayer>();if(p)p.Hurt();}
    }
}
