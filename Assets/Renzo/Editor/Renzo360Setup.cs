using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Video;

// Menu "Renzo > Maak 360 video-sphere": zet in de scene Renzo een sphere waarop de 360-video speelt,
// met de camera in het midden. Opnieuw uitvoeren is veilig (bestaande objecten worden hergebruikt).
public static class Renzo360Setup
{
    const string Dir = "Assets/Renzo";
    const string VideoPath = "Assets/video/stopmotion_360.mp4";
    const float Radius = 10f;

    [MenuItem("Renzo/Maak 360 video-sphere")]
    static void Maak()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Renzo")
        {
            EditorUtility.DisplayDialog("Renzo 360", "Open eerst de scene 'Renzo' (Assets/Scenes/Renzo.unity).", "OK");
            return;
        }

        var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(VideoPath);
        if (clip == null)
        {
            EditorUtility.DisplayDialog("Renzo 360", "Video niet gevonden: " + VideoPath, "OK");
            return;
        }

        // 1. Sphere-mesh met equirectangular UV's (midden van de video = vooruit, +Z)
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Dir + "/Sphere360.asset");
        if (mesh == null)
        {
            mesh = MaakSphere(128, 64);
            AssetDatabase.CreateAsset(mesh, Dir + "/Sphere360.asset");
        }

        // 2. Render Texture (zelfde verhouding als de video)
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(Dir + "/Video360RT.renderTexture");
        if (rt == null)
        {
            rt = new RenderTexture((int)clip.width, (int)clip.height, GraphicsFormat.R8G8B8A8_SRGB, GraphicsFormat.None)
            {
                name = "Video360RT",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
            };
            AssetDatabase.CreateAsset(rt, Dir + "/Video360RT.renderTexture");
        }

        // 3. Material met de Inside360-shader
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/Video360Mat.mat");
        if (mat == null)
        {
            mat = new Material(Shader.Find("Renzo/Inside360"));
            AssetDatabase.CreateAsset(mat, Dir + "/Video360Mat.mat");
        }
        mat.SetTexture("_MainTex", rt);
        EditorUtility.SetDirty(mat);

        // 4. Sphere in de scene met Video Player
        var go = GameObject.Find("Video360Sphere");
        if (go == null)
        {
            go = new GameObject("Video360Sphere");
            Undo.RegisterCreatedObjectUndo(go, "Maak 360 video-sphere");
        }
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one * Radius;

        var mf = Krijg<MeshFilter>(go);
        mf.sharedMesh = mesh;
        var mr = Krijg<MeshRenderer>(go);
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var vp = Krijg<VideoPlayer>(go);
        vp.source = VideoSource.VideoClip;
        vp.clip = clip;
        vp.playOnAwake = true;
        vp.isLooping = true;
        vp.skipOnDrop = true;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.targetTexture = rt;
        vp.audioOutputMode = VideoAudioOutputMode.None;

        // 5. Camera in het midden: enkel rondkijken (rotatie), geen positie-tracking
        var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            Undo.RegisterCreatedObjectUndo(camGo, "Maak camera");
            cam = camGo.AddComponent<Camera>();
        }
        Undo.RecordObject(cam.transform, "Camera naar midden");
        cam.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = Radius * 10f;

        var tpd = Krijg<TrackedPoseDriver>(cam.gameObject);
        tpd.trackingType = TrackedPoseDriver.TrackingType.RotationOnly;
        tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
        tpd.ignoreTrackingState = true;
        tpd.positionInput = new InputActionProperty(new InputAction("Position", InputActionType.Value,
            "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
        tpd.rotationInput = new InputActionProperty(new InputAction("Rotation", InputActionType.Value,
            "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
        EditorUtility.SetDirty(tpd);
        Krijg<MuisRondkijken>(cam.gameObject);   // testen zonder bril
        EditorUtility.SetDirty(cam);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = go;
        Debug.Log("[Renzo360] Klaar: Video360Sphere + camera ingesteld in scene Renzo.");
    }

    static T Krijg<T>(GameObject g) where T : Component
    {
        var comp = g.GetComponent<T>();
        return comp != null ? comp : Undo.AddComponent<T>(g);
    }

    // Lat-long sphere, driehoeken naar binnen gericht, u = 0.5 vooraan (+Z), +X = rechts in beeld
    static Mesh MaakSphere(int lonSeg, int latSeg)
    {
        var verts = new Vector3[(lonSeg + 1) * (latSeg + 1)];
        var uvs = new Vector2[verts.Length];
        int k = 0;
        for (int y = 0; y <= latSeg; y++)
        {
            float v = (float)y / latSeg;
            float lat = (v - 0.5f) * Mathf.PI;
            for (int x = 0; x <= lonSeg; x++)
            {
                float u = (float)x / lonSeg;
                float lon = (u - 0.5f) * 2f * Mathf.PI;
                verts[k] = new Vector3(Mathf.Sin(lon) * Mathf.Cos(lat), Mathf.Sin(lat), Mathf.Cos(lon) * Mathf.Cos(lat));
                uvs[k] = new Vector2(u, v);
                k++;
            }
        }
        var tris = new int[lonSeg * latSeg * 6];
        int t = 0;
        for (int y = 0; y < latSeg; y++)
        for (int x = 0; x < lonSeg; x++)
        {
            int a = y * (lonSeg + 1) + x, b = a + 1, c = a + lonSeg + 1, d = c + 1;
            tris[t++] = a; tris[t++] = c; tris[t++] = b;   // naar binnen gericht
            tris[t++] = b; tris[t++] = c; tris[t++] = d;
        }
        var mesh = new Mesh { name = "Sphere360" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
