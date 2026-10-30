using System;
using System.Collections.Generic;
using System.Linq;
using CoastRush;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Shared road cross-sections keep road furniture joined through curves and the lap seam.</summary>
public static class CoastRoadside
{
    const string MeshRoot="Assets/CoastRush/GeneratedMeshes/";
    public const float RailOffset=8.7f;
    static readonly Vector2[] KerbProfile={new Vector2(-.23f,-.08f),new Vector2(-.23f,.05f),new Vector2(.23f,.05f),new Vector2(.23f,-.08f)};
    static readonly Vector2[] BeamProfile={new Vector2(-.065f,.97f),new Vector2(-.065f,1.23f),new Vector2(-.035f,1.265f),new Vector2(.035f,1.265f),new Vector2(.065f,1.23f),new Vector2(.065f,.97f),new Vector2(.035f,.935f),new Vector2(-.035f,.935f)};
    static readonly Vector2[] HighlightProfile={new Vector2(-.006f,1.095f),new Vector2(-.006f,1.145f),new Vector2(.006f,1.145f),new Vector2(.006f,1.095f)};
    static readonly Vector2[] BarrierProfile={new Vector2(-.16f,-.12f),new Vector2(-.16f,1.6f),new Vector2(.16f,1.6f),new Vector2(.16f,-.12f)};

    public static void Build(CoastTrack track,Transform parent,Dictionary<string,Material> materials)
    {
        // An even number of colour blocks closes the red/white pattern cleanly.
        int blocks=Mathf.CeilToInt(track.length/8);if(blocks%2!=0)blocks++;
        float blockLength=track.length/blocks;
        foreach(int side in new[]{-1,1})
        {
            string sideName=side<0?"Left":"Right";
            for(int first=0;first<blocks;first+=8)
            {
                int last=Mathf.Min(first+8,blocks);
                float start=first*blockLength,end=last==blocks?track.length:last*blockLength;
                float[] samples=Samples(track,start,end,blockLength);
                string suffix=sideName+"_"+(first/8).ToString("00");
                Render("RoadsideKerb_"+suffix,Sweep(track,samples,side*7.24f,KerbProfile,blockLength),parent,new[]{materials["CurbRed"],materials["Chalk"]});
                Render("RoadsideRail_"+suffix,Sweep(track,samples,side*RailOffset,BeamProfile),parent,new[]{materials["GuardrailSilver"]});
                Render("RoadsideHighlight_"+suffix,Sweep(track,samples,side*(RailOffset-.071f),HighlightProfile),parent,new[]{materials["Chalk"]});
            }

            // One closed collision skin per side eliminates gaps and overlapping box ends.
            var wall=new GameObject("RoadsideBarrier_"+sideName,typeof(MeshCollider));wall.layer=8;wall.transform.SetParent(parent,false);
            wall.GetComponent<MeshCollider>().sharedMesh=Save(Sweep(track,Samples(track,0,track.length,blockLength),side*RailOffset,BarrierProfile),"RoadsideBarrier_"+sideName);

            int posts=Mathf.CeilToInt(track.length/3f);
            for(int i=0;i<posts;i++)
            {
                float s=i*track.length/posts;
                var post=GameObject.CreatePrimitive(PrimitiveType.Cube);post.name="Roadside post "+sideName;post.transform.SetParent(parent,false);
                post.transform.SetPositionAndRotation(track.Position(s,side*RailOffset)+Vector3.up*.66f,Quaternion.LookRotation(Vector3.ProjectOnPlane(track.Forward(s),Vector3.up)));
                post.transform.localScale=new Vector3(.12f,1.48f,.16f);post.GetComponent<Renderer>().sharedMaterial=materials["GuardrailSilver"];
                UnityEngine.Object.DestroyImmediate(post.GetComponent<Collider>());
            }
        }
    }

    static float[] Samples(CoastTrack track,float start,float end,float blockLength)
    {
        var values=new List<float>{start,end};
        foreach(float s in track.distances)if(s>start&&s<end)values.Add(s);
        for(int i=Mathf.FloorToInt(start/blockLength)+1;i*blockLength<end;i++)values.Add(i*blockLength);
        int subdivisions=Mathf.CeilToInt((end-start)/2);
        for(int i=1;i<subdivisions;i++)values.Add(Mathf.Lerp(start,end,i/(float)subdivisions));
        values.Sort();var samples=new List<float>();
        foreach(float s in values)if(samples.Count==0||s-samples[samples.Count-1]>.0001f)samples.Add(s);
        samples[samples.Count-1]=end;
        return samples.ToArray();
    }

    // Clockwise cross-section, viewed down the course. Duplicate each profile edge for crisp bevels.
    static Mesh Sweep(CoastTrack track,float[] samples,float lane,Vector2[] profile,float colourLength=0)
    {
        int rings=samples.Length,faces=profile.Length;
        var vertices=new Vector3[rings*faces*2];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];
        int materialCount=colourLength>0?2:1;
        var triangles=Enumerable.Range(0,materialCount).Select(_=>new List<int>()).ToArray();
        for(int face=0;face<faces;face++)
        {
            Vector2 a=profile[face],b=profile[(face+1)%faces];
            for(int ring=0;ring<rings;ring++)
            {
                float s=samples[ring];int index=(face*rings+ring)*2;
                Vector3 p=track.Position(s,lane+a.x)+Vector3.up*a.y,q=track.Position(s,lane+b.x)+Vector3.up*b.y;
                Vector3 tangent=track.Position(s+.05f,lane+(a.x+b.x)*.5f)-track.Position(s-.05f,lane+(a.x+b.x)*.5f);
                Vector3 normal=Vector3.Cross(tangent,q-p).normalized;
                vertices[index]=p;vertices[index+1]=q;normals[index]=normals[index+1]=normal;
                uv[index]=new Vector2(s/4,0);uv[index+1]=new Vector2(s/4,1);
                if(ring==rings-1)continue;
                int material=colourLength>0?Mathf.FloorToInt((s+samples[ring+1])*.5f/colourLength)%2:0;
                triangles[material].AddRange(new[]{index,index+2,index+1,index+1,index+2,index+3});
            }
        }
        var mesh=new Mesh{indexFormat=IndexFormat.UInt32,vertices=vertices,normals=normals,uv=uv,subMeshCount=materialCount};
        for(int i=0;i<materialCount;i++)mesh.SetTriangles(triangles[i],i);
        mesh.RecalculateBounds();return mesh;
    }

    static Mesh Save(Mesh mesh,string name)
    {
        mesh.name=name;string path=MeshRoot+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing){ReplaceMesh(existing,mesh);UnityEngine.Object.DestroyImmediate(mesh);return existing;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }

    public static void ReplaceMesh(Mesh target,Mesh source)
    {
        // Mesh setters invalidate the GPU buffers; CopySerialized alone can leave old scene-view geometry.
        target.Clear();target.indexFormat=source.indexFormat;target.vertices=source.vertices;target.normals=source.normals;
        target.tangents=source.tangents;target.uv=source.uv;target.uv2=source.uv2;target.colors32=source.colors32;
        target.subMeshCount=source.subMeshCount;
        for(int i=0;i<source.subMeshCount;i++)target.SetTriangles(source.GetTriangles(i),i,false);
        target.bounds=source.bounds;target.UploadMeshData(false);EditorUtility.SetDirty(target);
    }
    static void Render(string name,Mesh mesh,Transform parent,Material[] materials)
    {
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh=Save(mesh,name);go.GetComponent<MeshRenderer>().sharedMaterials=materials;
    }

    public static string Verify()
    {
        var track=UnityEngine.Object.FindFirstObjectByType<CoastTrack>();if(!track)throw new Exception("Missing coast track.");
        Physics.SyncTransforms();float maxJump=0;int rays=0;float maxSeam=0;
        foreach(int side in new[]{-1,1})
        {
            string name=side<0?"Left":"Right";
            var collider=GameObject.Find("RoadsideBarrier_"+name)?.GetComponent<MeshCollider>();
            if(!collider)throw new Exception("Missing continuous barrier "+name);
            foreach(float s in track.distances)
                maxJump=Mathf.Max(maxJump,Vector3.Distance(track.Position(s-.001f,side*RailOffset),track.Position(s+.001f,side*RailOffset)));
            for(float s=0;s<track.length;s+=.5f)
            {
                var origin=track.Position(s,side*6.9f)+Vector3.up*.8f;
                var direction=(track.Position(s,side*10)-track.Position(s,side*6.9f)).normalized;
                if(!collider.Raycast(new Ray(origin,direction),out var hit,4))throw new Exception($"Barrier hole: {name} at {s:F2}m");
                rays++;
            }
            foreach(string kind in new[]{"RoadsideKerb_","RoadsideRail_","RoadsideHighlight_"})
            {
                var strips=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .Where(f=>f.name.StartsWith(kind+name+"_",StringComparison.Ordinal)).OrderBy(f=>f.name,StringComparer.Ordinal).ToArray();
                int faces=kind=="RoadsideRail_"?BeamProfile.Length:4;
                if(strips.Length<2)throw new Exception("Missing roadside strips.");
                for(int i=0;i<strips.Length;i++)
                {
                    var a=strips[i].sharedMesh.vertices;var b=strips[(i+1)%strips.Length].sharedMesh.vertices;
                    int ar=a.Length/(faces*2),br=b.Length/(faces*2);
                    for(int face=0;face<faces;face++)for(int edge=0;edge<2;edge++)
                        maxSeam=Mathf.Max(maxSeam,Vector3.Distance(a[(face*ar+ar-1)*2+edge],b[face*br*2+edge]));
                }
            }
        }
        if(maxJump>.01f||maxSeam>.001f)throw new Exception($"Discontinuous roadside: sample jump={maxJump:F6}m, chunk seam={maxSeam:F6}m");
        string result=$"PASS: {rays} barrier raycasts; max offset step over 2mm={maxJump:F6}m; max kerb/rail/highlight chunk seam={maxSeam:F6}m including loop closure.";
        System.IO.Directory.CreateDirectory("doc");System.IO.File.WriteAllText("doc/roadside-verification.txt",DateTime.Now.ToString("s")+"\n"+result+"\n");return result;
    }
}
