using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

public class AnimationBaker : MonoBehaviour
{

    public ComputeShader infoTexGen;
    public Shader playShader;


    public class ShaderKeywords
    {
        public string MainTextName = "_MainTex";
        public string VertexDataPositions = "_PosTex";
        public string VertexDataNormals = "_NmlTex";
        public string VertexDataTangents = "_TanTex";
    }

    public ShaderKeywords shaderKeywords = new ShaderKeywords();

    public AnimationClip[] clips;

    public struct VertInfo
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector3 tangent;
    }

    private void Scan()
    {
        var animation = GetComponent<Animation>();
        var animator = GetComponent<Animator>();

        if (animation != null)
        {
            clips = new AnimationClip[animation.GetClipCount()];
            var i = 0;
            foreach (AnimationState state in animation)
                clips[i++] = state.clip;
        }
        else if (animator != null)
            clips = animator.runtimeAnimatorController.animationClips;
    }

    // public enum BakeFrameResolution { F6 = 6, F12 = 12, F20 = 20, F24 = 24, F40 = 40, F60 = 60, F120 = 120 }
    // public BakeFrameResolution FrameResolution = BakeFrameResolution.F20;

    [Range(2,12)] public int FrameResolution = 4;

    public string saveToFolder = "AnimationBakerOutput";

    public bool createPrefabs = false;

    public bool createMeshAsset = false;
    public bool optimizeMeshOnSave = false;
    [Tooltip("Make it a bit harder to reverse engenieer this model")]
    public bool collpaseMesh = false;

    [Tooltip("Combine multiple clip textures into one with a duplicate first row at every clip end")]
    public bool combineTextures = false;

    [Header("Transform")]
    public Vector3 rotate = Vector3.zero;
    public float boundsScale = 1;

    int GetFrameCount( AnimationClip clip )
    {
        return Mathf.NextPowerOfTwo( ( int ) ( clip.length * ( ( int ) FrameResolution ) ) );
    }

    // Convert render texture to texture 2d 
    public static Texture2D ConvertRT( RenderTexture rt )
    {
        TextureFormat format;

        switch (rt.format)
        {
            case RenderTextureFormat.ARGBFloat:
                format = TextureFormat.RGBAFloat;
                break;
            case RenderTextureFormat.ARGBHalf:
                format = TextureFormat.RGBAHalf;
                break;
            case RenderTextureFormat.ARGBInt:
                format = TextureFormat.RGBA32;
                break;
            case RenderTextureFormat.ARGB32:
                format = TextureFormat.ARGB32;
                break;
            default:
                format = TextureFormat.ARGB32;
                Debug.LogWarning("Unsuported RenderTextureFormat.");
                break;
        }

        return Convert(rt, format);
    }

    static Texture2D Convert(RenderTexture rt, TextureFormat format)
    {
        var tex2d = new Texture2D(rt.width, rt.height, format, false);
        var rect = Rect.MinMaxRect(0f, 0f, tex2d.width, tex2d.height);
        RenderTexture.active = rt;
        tex2d.ReadPixels(rect, 0, 0);
        RenderTexture.active = null;
        return tex2d;
    }



#if UNITY_EDITOR

    [CustomEditor(typeof(AnimationBaker))] class Inspector : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            GUILayout.Space(10);

            var script = (AnimationBaker) target;

            var s = "";

            if( script.clips == null || script.clips.Length < 1 )
            {
                if( GUILayout.Button("Scan") )
                {
                    script.Scan();
                }

                return;
            }

            foreach (var clip in script.clips)
            {
                var frames = script.GetFrameCount( clip );

                s += $"{clip.name}: {frames}\n";
            }

            s = "Frame counts per clip: \n" + s;

            EditorGUILayout.HelpBox( new GUIContent( s ) );

            if( GUILayout.Button("Bake Textures") )
            {
                script.Bake();
            }
        }
    }

    string FixPath( string s )
    {
        //return s.Replace('|', '_').Replace('/', '_').Replace('\\', '|').Replace(' ', '_')
        //    .Replace(':', '-').Replace('*', '-').Replace('?', '-')
        //    .Replace('<', '[').Replace('>', ']');

        //string validPath = Regex.Replace( s , @"[<>:""|?*]", "_");
        //validPath = Regex.Replace(validPath, @"\s+", " ");

        if ( string.IsNullOrEmpty( s ) ) throw new System.Exception("EMPTY");

        // Replace invalid characters with an underscore
        string invalidChars = new string(Path.GetInvalidPathChars());
        string validPath = Regex.Replace( s , $"[{Regex.Escape(invalidChars)}]", "_" );
        // Combine multiple path separators into a single one
        string separator = Path.DirectorySeparatorChar.ToString();
        string doubleSeparator = separator + separator;
        while (validPath.Contains(doubleSeparator))
        {
            validPath = validPath.Replace(doubleSeparator, separator);
        }
        
        return validPath.Trim();
    }

    Texture2D[] bakedTexturesPos;
    Texture2D[] bakedTexturesNorm;
    Texture2D[] bakedTexturesTan;

    [ContextMenu("bake texture")]
    void Bake()
    {
        bool bake_combined = combineTextures && clips.Length > 1;

        var skin = GetComponentInChildren<SkinnedMeshRenderer>();
        var defaultMesh = skin.sharedMesh;
        var vCount = skin.sharedMesh.vertexCount;
        var texWidth = Mathf.NextPowerOfTwo(vCount);
        var mesh = new Mesh();

        var folderName = FixPath(saveToFolder);
        var folderPath = Path.Combine("Assets", folderName);
        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder("Assets", folderName);

        var subFolder = name;
        var subFolderPath = Path.Combine(folderPath, subFolder);
        if (!AssetDatabase.IsValidFolder(subFolderPath))
            AssetDatabase.CreateFolder(folderPath, subFolder);

        if (createMeshAsset)
        {
            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            var meshAssetPath = Path.Combine(subFolderPath, $"{FixPath(name)}.mesh" + ".asset");
            defaultMesh = Instantiate(defaultMesh);
            if (!collpaseMesh && optimizeMeshOnSave) MeshUtility.Optimize(defaultMesh);
            if(collpaseMesh && defaultMesh.vertexCount > 2)
            {
                var newVerts = new Vector3[ defaultMesh.vertexCount ];

                defaultMesh.vertices.ToList().ForEach( v => { 
                    min.x = Mathf.Min(v.x, min.x);
                    min.y = Mathf.Min(v.y, min.y);
                    min.z = Mathf.Min(v.z, min.z);
                    max.x = Mathf.Max(v.x, max.x);
                    max.y = Mathf.Max(v.y, max.y);
                    max.z = Mathf.Max(v.z, max.z);
                } );
                newVerts[0] = new Vector3( min.x, min.y, min.z );
                newVerts[1] = new Vector3( max.x, max.y, max.z );
                
                defaultMesh.SetVertices( newVerts );
                defaultMesh.RecalculateBounds();
            }
                
            defaultMesh.bounds = new Bounds( defaultMesh.bounds.center, defaultMesh.bounds.size * boundsScale );

            AssetDatabase.CreateAsset( defaultMesh, meshAssetPath );
            AssetDatabase.SaveAssets();
            defaultMesh = AssetDatabase.LoadAssetAtPath<Mesh>( meshAssetPath );
        }

        int clip_index = -1;

        if( bake_combined )
        {
            bakedTexturesPos = new Texture2D[ clips.Length ];
            bakedTexturesNorm = new Texture2D[ clips.Length ];
            bakedTexturesTan = new Texture2D[ clips.Length ];
        }

        foreach (var clip in clips)
        {
            clip_index ++ ;

            var frames = GetFrameCount( clip );

            var dt = clip.length / frames;
            var infoList = new List<VertInfo>();

            // var frame_name = GetFrameResName();

            var pRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf);
            pRt.name = $"{name}.tex2D.Pos.{clip.name}.{frames}F";
            var nRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf);
            nRt.name = $"{name}.tex2D.Norm.{clip.name}.{frames}F";
            var tRt = new RenderTexture(texWidth, frames, 0, RenderTextureFormat.ARGBHalf);
            tRt.name = $"{name}.tex2D.Tan.{clip.name}.{frames}F";
            foreach (var rt in new[] { pRt, nRt, tRt })
            {
                rt.enableRandomWrite = true;
                rt.Create();
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
            }

            for (var i = 0; i < frames; i++)
            {
                clip.SampleAnimation(gameObject, dt * i);
                skin.BakeMesh(mesh);

                var verexArry = mesh.vertices;
                var normalArry = mesh.normals;
                var tangentArray = mesh.tangents;
                infoList.AddRange(Enumerable.Range(0, vCount)
                    .Select(idx => new VertInfo()
                    {
                        position = verexArry[idx],
                        normal = normalArry[idx],
                        tangent = tangentArray[idx],
                    })
                );
            }
            var buffer = new ComputeBuffer(infoList.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(VertInfo)));
            buffer.SetData(infoList.ToArray());

            var kernel = infoTexGen.FindKernel("CSMain");

            infoTexGen.GetKernelThreadGroupSizes(kernel, out uint x, out uint y, out uint z);

            infoTexGen.SetInt("VertCount", vCount);
            infoTexGen.SetBuffer(kernel, "Info", buffer);
            infoTexGen.SetTexture(kernel, "OutPosition", pRt);
            infoTexGen.SetTexture(kernel, "OutNormal", nRt);
            infoTexGen.SetTexture(kernel, "OutTangent", tRt);
            infoTexGen.SetVector("RotateEuler", rotate);
            infoTexGen.Dispatch(kernel, vCount / (int)x + 1, frames / (int)y + 1, 1);

            buffer.Release();

            var posTex = ConvertRT(pRt);
            var normTex = ConvertRT(nRt);
            var tanTex = ConvertRT(tRt);

            Graphics.CopyTexture(pRt, posTex);
            Graphics.CopyTexture(nRt, normTex);
            Graphics.CopyTexture(tRt, tanTex);

            if( bake_combined )
            {
                bakedTexturesPos[ clip_index ] = posTex;
                bakedTexturesNorm[ clip_index ] = normTex;
                bakedTexturesTan[ clip_index ] = tanTex;
            }

            else
            {
                var mat = new Material( playShader );

                mat.SetTexture( shaderKeywords.MainTextName ,           skin.sharedMaterial.mainTexture );
                mat.SetTexture( shaderKeywords.VertexDataPositions ,    posTex);
                mat.SetTexture( shaderKeywords.VertexDataNormals ,      normTex);
                mat.SetTexture( shaderKeywords.VertexDataTangents ,     tanTex);

                //mat.SetFloat("_Length", clip.length);
                //
                //if (clip.wrapMode == WrapMode.Loop)
                //{
                //    mat.SetFloat("_Loop", 1f);
                //    mat.EnableKeyword("ANIM_LOOP");
                //}

                var pRt_path = Path.Combine(subFolderPath,FixPath(pRt.name) + ".asset");
                var nRt_path = Path.Combine(subFolderPath,FixPath(nRt.name) + ".asset");
                var tRt_path = Path.Combine(subFolderPath,FixPath(tRt.name) + ".asset");

                AssetDatabase.CreateAsset(posTex,   pRt_path);
                AssetDatabase.CreateAsset(normTex,  nRt_path);
                AssetDatabase.CreateAsset(tanTex,   tRt_path);

                var mat_path = Path.Combine( subFolderPath, $"{FixPath(name)}.mat.{FixPath(clip.name)}.{frames}F.asset");

                AssetDatabase.CreateAsset(mat, mat_path );

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if ( createPrefabs )
                {
                    var go = new GameObject(name + "." + clip.name);

                    go.transform.position = transform.position + Vector3.left * ( clip_index + 1 );
                
                    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                    go.AddComponent<MeshFilter>().sharedMesh = defaultMesh;
            
                    PrefabUtility.SaveAsPrefabAssetAndConnect( go, Path.Combine( folderPath, FixPath( go.name ) + ".prefab") , InteractionMode.AutomatedAction );
                }

                Selection.activeObject = mat;
            }

        }
        
        if ( bake_combined )
        {
            var combined_tex_P = CombineTextures( bakedTexturesPos  , "Pos" );
            var combined_tex_N = CombineTextures( bakedTexturesNorm , "Nor" );
            var combined_tex_T = CombineTextures( bakedTexturesTan  , "Tan" );

            Texture2D CombineTextures( Texture2D[] textures , string n )
            {
                int w = textures[ 0 ].width;
                var f = textures[ 0 ].format;

                int total_height = 0;

                for( var i = 0; i < textures.Length; ++i )
                    total_height += textures[ i ].height + 1; // extra pixel space for duplicate first row for each clip 

                var output = new Texture2D( w , total_height, f , false );

                int y = 0;

                for ( var i = 0; i < textures.Length; ++i )
                {
                    // ...

                    var t = textures[ i ];

                    int h = t.height;
                    
                    Graphics.CopyTexture( t, 0, 0, 0, 0, w, h, output, 0, 0, 0, y );

                    y += h;

                    // duplicate first row 

                    Graphics.CopyTexture( t, 0, 0, 0, 0, w, 1, output, 0, 0, 0, y );

                    y += 1;
                }

                var path = Path.Combine( subFolderPath, $"{FixPath(name)}.tex2D.Combined_{n}.asset" );

                AssetDatabase.CreateAsset( output , path );

                return output;
            }

            // .. 

            AnimationCombinedFrames frame_data = null;

            {
                frame_data = ScriptableObject.CreateInstance<AnimationCombinedFrames>();

                frame_data.data = new AnimationCombinedFrames.FrameTimings[ bakedTexturesPos.Length ];

                int frame_offset = 0;

                for ( var i = 0; i < bakedTexturesPos.Length; ++i )
                {
                    var clip = clips[i];

                    int frame_count = GetFrameCount(clip);

                    frame_data.data[ i ] = new AnimationCombinedFrames.FrameTimings
                    {
                        name = FixPath(clip.name),
                        duration = 1,
                        frames = frame_count,
                        offset = frame_offset
                    };

                    frame_offset += frame_count + 1; // increment by previous frame count and +1 for extra row 
                }

                var path = Path.Combine(subFolderPath, $"{FixPath(name)}.framedata.asset");

                AssetDatabase.CreateAsset( frame_data , path );

                Selection.activeObject = frame_data;
            }

            var mat = new Material( playShader );

            mat.SetTexture( shaderKeywords.MainTextName ,           skin.sharedMaterial.mainTexture );
            mat.SetTexture( shaderKeywords.VertexDataPositions ,    combined_tex_P );
            mat.SetTexture( shaderKeywords.VertexDataNormals ,      combined_tex_N );
            mat.SetTexture( shaderKeywords.VertexDataTangents ,     combined_tex_T );

            var mat_path = Path.Combine(subFolderPath, $"{FixPath(name)}.combined.mat.asset");

            AssetDatabase.CreateAsset( mat, mat_path );

            if ( createPrefabs )
            {
                var go = new GameObject( name + ".combined" );

                go.transform.position = transform.position + Vector3.left * ( 1.0f );

                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                go.AddComponent<MeshFilter>().sharedMesh = defaultMesh;
                go.AddComponent<AnimationFramePlayer>().frameData = frame_data;

                PrefabUtility.SaveAsPrefabAssetAndConnect(go, Path.Combine(folderPath, FixPath(go.name) + ".prefab"), InteractionMode.AutomatedAction);

                Selection.activeObject = go;
            }

            else Selection.activeObject = mat;
        }

        bakedTexturesPos = null;
        bakedTexturesNorm = null;
        bakedTexturesTan = null;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

    }

    // Convert asset files to png 

    [MenuItem("Tools/Convert/Selected texture .asset To .PNG")]
    public static void SaveSelection()
    {
        var tex = (Texture2D) Selection.activeObject;

        if (tex == null)
        {
            Debug.LogError("Selected object is Null or not a Texture2D type");
            return;
        }

        var path = Directory.GetParent(Application.dataPath).ToString(); // project path folder without the 'Assets' folder 

        path = Path.Combine( path , AssetDatabase.GetAssetPath( tex ) ); // combine with selected asset relative path ( which allready includes 'Assets' folder )

        path = Path.ChangeExtension(path, "png"); // .asset to .png

        System.IO.File.WriteAllBytes( path , tex.EncodeToPNG() );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

#endif
}
