using UnityEngine;
namespace CloudSprout
{
    public sealed class SideCamera:MonoBehaviour
    {
        public Transform target;
        Vector3 velocity;
        public float end=282;
        void LateUpdate(){if(!target)return;float y=Mathf.Clamp(target.position.y+3.0f,3.6f,6.0f);var p=new Vector3(Mathf.Clamp(target.position.x+3.2f,10,end),y,-25);if(Vector3.Distance(transform.position,p)>25)transform.position=p;else transform.position=Vector3.SmoothDamp(transform.position,p,ref velocity,.20f);}
    }
}
