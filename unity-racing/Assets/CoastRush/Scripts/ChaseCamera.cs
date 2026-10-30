using UnityEngine;
namespace CoastRush
{
    [RequireComponent(typeof(Camera))]
    public sealed class ChaseCamera : MonoBehaviour
    {
        public VehicleController target;
        Camera view;
        bool snap=true;
        public void Snap(){snap=true;}
        void Awake(){view=GetComponent<Camera>();}
        void LateUpdate()
        {
            if(!target || !RaceDirector.Instance)return;
            bool menu=RaceDirector.Instance.State==RaceState.MainMenu;
            Vector3 desired,look;
            float speed=Mathf.Clamp01(target.SpeedKph/180);
            if(menu)
            {
                float a=.08f*Mathf.Sin(Time.unscaledTime*.16f);
                desired=target.transform.TransformPoint(new Vector3(6.6f+a,3.2f,7.8f));
                look=target.transform.TransformPoint(new Vector3(-1.8f,.65f,0));
            }
            else
            {
                var heading=target.transform.forward;
                if(target.Body.linearVelocity.magnitude>8)heading=Vector3.Slerp(heading,target.Body.linearVelocity.normalized,.12f);
                heading=Vector3.ProjectOnPlane(heading,Vector3.up).normalized;
                desired=target.transform.position-heading*(7.7f+speed*1.8f)+Vector3.up*(3.25f+speed*.3f);
                look=target.transform.position+target.transform.forward*(4+speed*3)+Vector3.up*.9f;
                var origin=target.transform.position+Vector3.up*1.4f;
                if(Physics.SphereCast(origin,.3f,(desired-origin).normalized,out var hit,Vector3.Distance(desired,origin),1<<8,QueryTriggerInteraction.Ignore))
                    desired=hit.point+hit.normal*.5f;
            }
            float blend=snap?1:1-Mathf.Exp(-Time.unscaledDeltaTime*7);
            transform.position=Vector3.Lerp(transform.position,desired,blend);
            var rotation=Quaternion.LookRotation(look-transform.position,Vector3.up);
            transform.rotation=Quaternion.Slerp(transform.rotation,rotation,blend);
            view.fieldOfView=Mathf.Lerp(view.fieldOfView,menu?47:61+speed*9,blend);
            snap=false;
        }
    }
}
