using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
namespace Emberfall.Editor
{
    public static class ChinesePresentationSetup
    {
        public static void Prepare()
        {
            var importer=AssetImporter.GetAtPath("Assets/Emberfall/Resources/Art/EmberfallAtlas.png") as TextureImporter;
            if(importer==null)throw new Exception("Art atlas is missing.");
            importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            var hero=AssetImporter.GetAtPath("Assets/Emberfall/Resources/Art/WandererDirections.png") as TextureImporter;
            if(hero==null)throw new Exception("Directional hero atlas is missing.");
            hero.textureType=TextureImporterType.Default;hero.alphaIsTransparency=true;hero.mipmapEnabled=false;
            hero.wrapMode=TextureWrapMode.Clamp;hero.filterMode=FilterMode.Bilinear;hero.npotScale=TextureImporterNPOTScale.None;
            hero.maxTextureSize=2048;hero.textureCompression=TextureImporterCompression.Uncompressed;hero.SaveAndReimport();
            var enemies=AssetImporter.GetAtPath("Assets/Emberfall/Resources/Art/EnemyAttacks.png") as TextureImporter;
            if(enemies==null)throw new Exception("Enemy attack atlas is missing.");
            enemies.textureType=TextureImporterType.Default;enemies.alphaIsTransparency=true;enemies.mipmapEnabled=false;
            enemies.wrapMode=TextureWrapMode.Clamp;enemies.filterMode=FilterMode.Bilinear;enemies.npotScale=TextureImporterNPOTScale.None;
            enemies.maxTextureSize=2048;enemies.textureCompression=TextureImporterCompression.Uncompressed;enemies.SaveAndReimport();
            const string path="Assets/Emberfall/Resources/Fonts/EmberfallChinese.asset";
            if(File.Exists(path))AssetDatabase.DeleteAsset(path);
            var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/Emberfall/Resources/Fonts/NotoSansCJKsc-Regular.otf");
            if(source==null||TMP_Settings.instance==null)throw new Exception("Font source or TMP essentials missing.");
            var font=TMP_FontAsset.CreateFontAsset(source,48,5,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
            font.name="EmberfallChinese";
            string code=string.Join("",Directory.GetFiles("Assets/Emberfall/Scripts","*.cs").Select(File.ReadAllText));
            string characters=new string(code.Where(c=>c>=0x2e80 || c>=' '&&c<='~' || c=='·' || c=='…' || c=='—').Distinct().OrderBy(c=>c).ToArray());
            if(!font.TryAddCharacters(characters,out string missing)&&!string.IsNullOrEmpty(missing))throw new Exception("Missing glyphs: "+missing);
            font.atlasPopulationMode=AtlasPopulationMode.Static;AssetDatabase.CreateAsset(font,path);
            foreach(var tex in font.atlasTextures)if(tex&&!EditorUtility.IsPersistent(tex))AssetDatabase.AddObjectToAsset(tex,font);
            if(!EditorUtility.IsPersistent(font.material))AssetDatabase.AddObjectToAsset(font.material,font);
            font.material.mainTexture=font.atlasTexture;EditorUtility.SetDirty(font);EditorUtility.SetDirty(font.material);
            foreach(var tex in font.atlasTextures)EditorUtility.SetDirty(tex);AssetDatabase.SaveAssets();
            if(!AssetDatabase.LoadAllAssetsAtPath(path).Any(a=>a is Material))throw new Exception("Font material was not saved.");
            Directory.CreateDirectory("Verification");File.WriteAllText("Verification/chinese-font.txt",$"PASS {characters.Length} required characters baked; missing=0; static 2048 atlas; material persisted.");
            Debug.Log("EMBERFALL_CHINESE_FONT_OK "+characters.Length);
        }
    }
}
