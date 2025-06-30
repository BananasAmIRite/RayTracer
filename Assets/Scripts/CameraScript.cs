using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

struct MaterialTriangle
{
    public Vector3 a;
    public Vector3 b;
    public Vector3 c;
    public float4 emissionColor;
    public float4 materialColor;
    public float emission;
    public float roughness;
}

struct Ray
{
    public Vector3 position;
    public Vector3 direction; 
}

[RequireComponent(typeof(Camera))]
public class CameraScript : MonoBehaviour
{
    private int kernel;
    public ComputeShader shader;
    public int width;
    public int height;
    public int iterations;
    public int samplesPerPixel = 1;
    public RawImage displayImage;

    public new Camera camera;
    Scene scene;
    MaterialTriangle[] triangles;

    RenderTexture output;
    ComputeBuffer rayBuf;
    ComputeBuffer triangleBuf;
    Vector3[] rays;

    private int frames = 0; 

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        scene = gameObject.scene;

        GameObject[] objs = scene.GetRootGameObjects();

        int triangleCount = 0;

        // count # of triangles ( primitive array :( )
        for (int i = 0; i < objs.Count(); i++)
        {
            MeshFilter meshFilter = objs[i].GetComponent<MeshFilter>();

            ObjectSettings settings = objs[i].GetComponent<ObjectSettings>();
            if (settings == null) continue;
            if (meshFilter == null)
            {
                Debug.LogError("No MeshFilter found on this GameObject!");
                continue;
            }
            Mesh mesh = meshFilter.mesh;

            // Use triangles array which contains indices into the vertices array
            triangleCount += mesh.triangles.Length / 3;
        }


        triangles = new MaterialTriangle[triangleCount];
        int count = 0;

        for (int i = 0; i < objs.Count(); i++)
        {


            ObjectSettings settings = objs[i].GetComponent<ObjectSettings>();

            if (settings == null) continue;
            
            MeshFilter meshFilter = objs[i].GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                Debug.LogError("No MeshFilter found on this GameObject!");
                continue;
            }
            Mesh mesh = meshFilter.mesh;

            Debug.Log("Mesh found!");
            
            // add all triangles from mesh using material from ObjectSettings
            for (int j = 0; j < mesh.triangles.Length; j += 3)
            {
                int index1 = mesh.triangles[j];
                int index2 = mesh.triangles[j + 1];
                int index3 = mesh.triangles[j + 2];

                Vector3 v1 = objs[i].transform.TransformPoint(mesh.vertices[index1]);
                Vector3 v2 = objs[i].transform.TransformPoint(mesh.vertices[index2]);
                Vector3 v3 = objs[i].transform.TransformPoint(mesh.vertices[index3]);

                MaterialTriangle t = new MaterialTriangle() { a = v1, b = v2, c = v3 };

                t.emission = settings.emission;
                t.emissionColor = new float4(settings.emissionColor.r, settings.emissionColor.g, settings.emissionColor.b, 1);
                t.materialColor = new float4(settings.materialColor.r, settings.materialColor.g, settings.materialColor.b, 1);
                t.roughness = settings.roughness;

                triangles[count] = t;
                count++;
            }

        }

        Debug.Log($"Total triangles loaded: {triangles.Length}");
        for (int i = 0; i < Mathf.Min(3, triangles.Length); i++)
        {
            Debug.Log($"Triangle {i}: a={triangles[i].a}, b={triangles[i].b}, c={triangles[i].c}");
            Debug.Log($"Triangle {i}: emission={triangles[i].emission}, emissionColor={triangles[i].emissionColor}, materialColor={triangles[i].materialColor}");
        }

        rays = new Vector3[width * height]; 

        InitShader();
    }

    void InitShader()
    {
        kernel = shader.FindKernel("CSMain");

        output = new RenderTexture(width, height, 0);
        output.enableRandomWrite = true;
        output.Create();

        triangleBuf = new ComputeBuffer(triangles.Count(), sizeof(float) * (3 + 3 + 3 + 4 + 4 + 1 + 1));
        shader.SetBuffer(kernel, "triangles", triangleBuf);
        triangleBuf.SetData(triangles);

        rayBuf = new ComputeBuffer(rays.Count(), sizeof(float) * 3);
        shader.SetBuffer(kernel, "rtLightTotal", rayBuf);
        rayBuf.SetData(rays);

        shader.SetInt("width", width);
        shader.SetInt("height", height);
        shader.SetInt("iterations", iterations);
        shader.SetInt("samplesPerPixel", samplesPerPixel);

        shader.SetInt("trianglesSize", triangles.Count());

        // camera settings
        shader.SetVector("cameraPosition", camera.transform.position);
        shader.SetVector("cameraUp", camera.transform.up);
        shader.SetVector("cameraRight", camera.transform.right);
        shader.SetVector("cameraForward", camera.transform.forward);
        shader.SetFloat("nearClip", camera.nearClipPlane);
        shader.SetFloat("fovDegrees", camera.fieldOfView);
        shader.SetFloat("aspectRatio", camera.aspect);

        Debug.Log("Camera Position");
        Debug.Log(camera.transform.position);

        Debug.Log("Camera Up");
        Debug.Log(camera.transform.up);

        Debug.Log("Camera Right");
        Debug.Log(camera.transform.right);

        Debug.Log("Camera Forward");
        Debug.Log(camera.transform.forward);

        Debug.Log("Camera Near Clip");
        Debug.Log(camera.nearClipPlane);

        Debug.Log("Camera FOV");
        Debug.Log(camera.fieldOfView);

        Debug.Log("Camera Aspect Ratio");
        Debug.Log(camera.aspect);

        shader.SetTexture(kernel, "Result", output);

        displayImage.texture = output;

        Debug.Log($"Dispatching compute shader with: width={width}, height={height}, triangles={triangles.Length}");
        Debug.Log($"Dispatch groups: {width / 32} x {height / 32}");




        // Retrieve data from GPU after compute shader execution
        // rayBuf.GetData(rays);

        Time.fixedDeltaTime = 1 / 2f;
        
        
    }

    void FixedUpdate()
    {
        // shader.SetVector("cameraPosition", camera.transform.position);
        // shader.SetVector("cameraUp", camera.transform.up);
        // shader.SetVector("cameraRight", camera.transform.right);
        // shader.SetVector("cameraForward", camera.transform.forward);
        // shader.SetFloat("nearClip", camera.nearClipPlane);
        // shader.SetFloat("fovDegrees", camera.fieldOfView);
        // shader.SetFloat("aspectRatio", camera.aspect);
        frames++;
        shader.SetInt("frameCount", frames);

        shader.Dispatch(kernel, width / 32, height / 32, 1);

        Debug.Log("Dispatched frame: " + frames);
    }

    void OnDestroy()
    {
        if (rayBuf != null)
        {
            rayBuf.Release();
        }

        if (triangleBuf != null)
        {
            triangleBuf.Release();
        }
    }

}
