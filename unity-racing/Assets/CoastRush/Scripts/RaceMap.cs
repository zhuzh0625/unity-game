using UnityEngine;
using UnityEngine.UI;
namespace CoastRush
{
    public sealed class RaceMap : MaskableGraphic
    {
        public CoastTrack track;
        Vector2 min,max;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(!track||track.points==null)return;
            min=new Vector2(float.MaxValue,float.MaxValue);max=new Vector2(float.MinValue,float.MinValue);
            foreach(var p in track.points){min=Vector2.Min(min,new Vector2(p.x,p.z));max=Vector2.Max(max,new Vector2(p.x,p.z));}
            for(int i=0;i<track.points.Length-1;i++)Line(vh,Point(track.points[i]),Point(track.points[i+1]),3,new Color(.55f,.73f,.73f,.75f));
            var race=RaceDirector.Instance;
            if(race)for(int i=race.racers.Length-1;i>=0;i--)
            {
                var car=race.racers[i];if(!car.gameObject.activeSelf)continue;
                Vector2 p=Point(car.transform.position);
                Square(vh,p,i==0?5:3.5f,i==0?new Color(1,.37f,.12f):Color.white);
            }
            Vector2 start=Point(track.Position(0));Line(vh,start+Vector2.left*6,start+Vector2.right*6,2,new Color(1,.7f,.2f));
        }
        Vector2 Point(Vector3 p)
        {
            Vector2 size=max-min;float scale=Mathf.Min((rectTransform.rect.width-30)/size.x,(rectTransform.rect.height-30)/size.y);
            return new Vector2(p.x-(min.x+max.x)*.5f,p.z-(min.y+max.y)*.5f)*scale;
        }
        void Update(){SetVerticesDirty();}
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c)
        {
            Vector2 d=(b-a).normalized;Vector2 n=new Vector2(-d.y,d.x)*width*.5f;
            int i=vh.currentVertCount;vh.AddVert(a-n,c,Vector2.zero);vh.AddVert(a+n,c,Vector2.zero);vh.AddVert(b+n,c,Vector2.zero);vh.AddVert(b-n,c,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
        static void Square(VertexHelper vh,Vector2 p,float r,Color c)
        {
            int i=vh.currentVertCount;vh.AddVert(p+new Vector2(-r,-r),c,Vector2.zero);vh.AddVert(p+new Vector2(-r,r),c,Vector2.zero);vh.AddVert(p+new Vector2(r,r),c,Vector2.zero);vh.AddVert(p+new Vector2(r,-r),c,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
    }
}
