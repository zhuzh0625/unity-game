using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CloudSprout.Editor
{
    public static class SpikePitBuilder
    {
        const string Root="Assets/CloudSprout/";
        static readonly Vector3[] Sites={new Vector3(35,0,2),new Vector3(88,0,2),new Vector3(159,.4f,2),new Vector3(204,.5f,2),new Vector3(245,1.2f,2),new Vector3(269,1,1)};
        static readonly Vector2[] Tiles={new Vector2(35,0),new Vector2(87,0),new Vector2(158,.4f),new Vector2(204,.5f),new Vector2(244,1.2f),new Vector2(267,1)};

        public static string ReplaceSurfaceSpikes()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
            var existing=UnityEngine.Object.FindObjectsByType<Hazard>(FindObjectsSortMode.None);
            if(existing.Count(h=>h.name=="Spike Pit")==Sites.Length)return AlignToWholeTiles();
            if(existing.Length!=11||existing.Any(h=>h.name!="Spike"))throw new InvalidOperationException("Unexpected hazard layout; inspect before replacing.");
            foreach(var site in Sites)for(int i=0;i<(int)site.z;i++)
                if(!existing.Any(h=>Vector3.Distance(h.transform.position,new Vector3(site.x+i,site.y+.04f,0))<.01f))throw new InvalidOperationException("An original spike moved; inspect before replacing.");
            var terrain=GameObject.Find("01 Terrain").transform;
            var gameplay=GameObject.Find("03 Gameplay").transform;
            foreach(var tile in Tiles)Create(tile.x,tile.y,terrain,gameplay);
            foreach(var old in existing)Undo.DestroyObjectImmediate(old.gameObject);
            PolishBanks(false);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            return "Replaced eleven surface spike models with six recessed spike pits.";
        }

        public static string AlignToWholeTiles()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
            var pits=UnityEngine.Object.FindObjectsByType<Hazard>(FindObjectsSortMode.None);
            if(pits.Length!=Tiles.Length||pits.Any(p=>p.name!="Spike Pit"))throw new InvalidOperationException("Unexpected trap layout.");
            var terrain=GameObject.Find("01 Terrain").transform;
            var gameplay=GameObject.Find("03 Gameplay").transform;
            int moved=0;
            foreach(var tile in Tiles)
            {
                var pit=pits.Single(p=>p&&Mathf.Abs(p.transform.position.y-tile.y)<.01f&&Mathf.Abs(p.transform.position.x-(tile.x+1))<2);
                float oldWidth=pit.GetComponent<BoxCollider>().size.x+.12f;
                if(Mathf.Abs(pit.transform.position.x-(tile.x+1))<.001f&&Mathf.Abs(oldWidth-2)<.001f)continue;
                RestoreOldOpening(pit,terrain);
                Undo.DestroyObjectImmediate(pit.gameObject);
                Create(tile.x,tile.y,terrain,gameplay);moved++;
            }
            string banks=PolishBanks(false);
            if(moved>0||banks!="Bank adjustments=0"){EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();}
            return "Whole-tile trap adjustments="+moved+"; "+banks;
        }

        public static void Create(float tileStart,float surfaceY,Transform terrain,Transform gameplay)
        {
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Models/SpikePit.fbx"))throw new InvalidOperationException("Create the Blender pit asset first.");
            const float width=2;
            float center=tileStart+1;
            ReplaceWholeGroundTile(tileStart,tileStart+width,surfaceY,terrain);
            var pit=new GameObject("Spike Pit");Undo.RegisterCreatedObjectUndo(pit,"Create recessed trap");pit.transform.SetParent(gameplay);pit.transform.position=new Vector3(center,surfaceY,0);
            var frame=Model("SpikePit",pit.transform,Vector3.zero);frame.transform.localScale=new Vector3(width,1,1);PrefabUtility.RecordPrefabInstancePropertyModifications(frame.transform);
            for(int i=0;i<2;i++)
            {
                var spikes=Model("Spike",pit.transform,new Vector3((i-.5f)*.84f,-1.08f,0));
                spikes.transform.localScale=new Vector3(.84f,1,1);PrefabUtility.RecordPrefabInstancePropertyModifications(spikes.transform);
            }
            pit.AddComponent<Hazard>();
            var trigger=pit.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=new Vector3(0,-.70f,0);trigger.size=new Vector3(width-.12f,.76f,1.5f);
            var floor=new GameObject("Pit Floor");floor.transform.SetParent(pit.transform,false);var support=floor.AddComponent<BoxCollider>();support.center=new Vector3(0,-1.20f,0);support.size=new Vector3(width,.24f,2.6f);
        }

        public static string PolishBanks(bool saveScene=true)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
            int changed=0;
            var landscape=GameObject.Find("02 Landscape");
            var enemies=UnityEngine.Object.FindObjectsByType<PatrolEnemy>(FindObjectsSortMode.None);
            foreach(var pit in UnityEngine.Object.FindObjectsByType<Hazard>(FindObjectsSortMode.None).Where(h=>h.name=="Spike Pit"))
            {
                float center=pit.transform.position.x,y=pit.transform.position.y,width=pit.GetComponent<BoxCollider>().size.x+.12f,left=center-width*.5f,right=center+width*.5f;
                // Run after decoration generation too, so a fresh level matches the upgraded scene.
                if(landscape)foreach(Transform decoration in landscape.transform)
                    if(decoration.name=="Flower"&&decoration.position.x>left-.2f&&decoration.position.x<right+.2f&&Mathf.Abs(decoration.position.y-y)<.2f)
                    {Undo.RecordObject(decoration,"Move flower off trap opening");var p=decoration.position;p.x=p.x<center?left-.55f:right+.55f;decoration.position=p;PrefabUtility.RecordPrefabInstancePropertyModifications(decoration);changed++;}
                foreach(var enemy in enemies)
                {
                    if(Mathf.Abs(enemy.transform.position.y-y-.08f)>.1f||enemy.right<left-.65f||enemy.left>right+.65f)continue;
                    float l=enemy.left,r=enemy.right;
                    if(enemy.transform.position.x<left)r=Mathf.Min(r,left-.65f);
                    else if(enemy.transform.position.x>right)l=Mathf.Max(l,right+.65f);
                    else throw new InvalidOperationException("An enemy starts inside a pit; inspect its spawn before clipping patrol bounds.");
                    if(l>r)throw new InvalidOperationException("A pit leaves no safe patrol interval.");
                    if(Mathf.Abs(l-enemy.left)>.001f||Mathf.Abs(r-enemy.right)>.001f)
                    {Undo.RecordObject(enemy,"Keep patrol on the bank");enemy.left=l;enemy.right=r;EditorUtility.SetDirty(enemy);changed++;}
                }
            }
            if(saveScene&&changed>0){EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();}
            return "Bank adjustments="+changed;
        }

        static GameObject Model(string name,Transform parent,Vector3 position)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Models/"+name+".fbx");
            var model=(GameObject)PrefabUtility.InstantiatePrefab(source);model.name=name;model.transform.SetParent(parent,false);model.transform.localPosition=position;model.transform.localScale=Vector3.one;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>()){renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/"+m.name.Replace("CS_","").Split('.')[0]+".mat")??m).ToArray();PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
            return model;
        }

        static void ReplaceWholeGroundTile(float left,float right,float y,Transform terrain)
        {
            var tiles=terrain.Cast<Transform>().Where(t=>t.name=="GrassTile"&&Mathf.Abs(t.position.y-y)<.01f&&t.position.x+t.localScale.x*.5f>left+.001f&&t.position.x-t.localScale.x*.5f<right-.001f).ToArray();
            if(tiles.Length!=1||Mathf.Abs(tiles[0].position.x-(left+right)*.5f)>.001f||Mathf.Abs(tiles[0].localScale.x-(right-left))>.001f)
                throw new InvalidOperationException("A spike pit must replace one complete ground tile; move the pit to a tile boundary.");
            foreach(var collider in terrain.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("Ground ")).ToArray())
            {
                if(Mathf.Abs(collider.bounds.max.y-y)>.01f)continue;
                float start=collider.bounds.min.x,end=collider.bounds.max.x;
                if(end<=left||start>=right)continue;
                var owner=collider.gameObject;var originalCenter=collider.center;var originalSize=collider.size;
                Undo.DestroyObjectImmediate(collider);
                AddSupport(owner,start,Mathf.Min(left,end),originalCenter,originalSize);
                AddSupport(owner,Mathf.Max(right,start),end,originalCenter,originalSize);
            }
            Undo.DestroyObjectImmediate(tiles[0].gameObject);
        }

        static void RestoreOldOpening(Hazard pit,Transform terrain)
        {
            float y=pit.transform.position.y,x=pit.transform.position.x,half=(pit.GetComponent<BoxCollider>().size.x+.12f)*.5f;
            var ground=terrain.Cast<Transform>().Where(t=>t.name.StartsWith("Ground ")&&Mathf.Abs(t.position.y-y)<.01f)
                .Single(t=>{var c=t.GetComponents<BoxCollider>();return c.Length>0&&c.Min(v=>v.bounds.min.x)<x&&c.Max(v=>v.bounds.max.x)>x;});
            if(Quaternion.Angle(ground.rotation,Quaternion.identity)>.01f||Vector3.Distance(ground.lossyScale,Vector3.one)>.001f)throw new InvalidOperationException("Unexpected ground transform.");
            var supports=ground.GetComponents<BoxCollider>();
            float start=supports.Min(c=>c.bounds.min.x),end=supports.Max(c=>c.bounds.max.x);
            Vector3 oldCenter=supports[0].center,oldSize=supports[0].size;
            // Restore only cells touched by the previous opening, preserving every other tile.
            float restoreStart=start+2*Mathf.Floor((x-half-start)/2),restoreEnd=Mathf.Min(end,start+2*Mathf.Ceil((x+half-start)/2));
            foreach(var fragment in terrain.Cast<Transform>().Where(t=>t.name=="GrassTile"&&Mathf.Abs(t.position.y-y)<.01f&&t.position.x+t.localScale.x*.5f>restoreStart+.001f&&t.position.x-t.localScale.x*.5f<restoreEnd-.001f).ToArray())
                Undo.DestroyObjectImmediate(fragment.gameObject);
            for(float left=restoreStart;left<restoreEnd-.001f;left+=2)
            {
                float width=Mathf.Min(2,end-left);var tile=Model("GrassTile",terrain,new Vector3(left+width*.5f,y,0));
                tile.transform.localScale=new Vector3(width,1.8f,1);PrefabUtility.RecordPrefabInstancePropertyModifications(tile.transform);
                Undo.RegisterCreatedObjectUndo(tile,"Restore whole ground tile");
            }
            foreach(var support in supports)Undo.DestroyObjectImmediate(support);
            AddSupport(ground.gameObject,start,end,oldCenter,oldSize);
        }

        static void AddSupport(GameObject owner,float start,float end,Vector3 center,Vector3 size)
        {
            if(end-start<.001f)return;
            var collider=Undo.AddComponent<BoxCollider>(owner);center.x=(start+end)*.5f-owner.transform.position.x;size.x=end-start;collider.center=center;collider.size=size;
        }
    }
}
