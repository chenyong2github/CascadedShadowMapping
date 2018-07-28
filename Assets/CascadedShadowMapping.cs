using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CascadedShadowMapping : MonoBehaviour
{
    public Light dirLight;
    Camera dirLightCamera;

    public int shadowResolution = 1;
    public Shader shadowCaster = null;

    private Matrix4x4 biasMatrix = Matrix4x4.identity;

    List<Matrix4x4> world2ShadowMats = new List<Matrix4x4>(4);
    GameObject[] dirLightCameraSplits = new GameObject[4];
    RenderTexture[] depthTextures = new RenderTexture[4];

    void OnDestroy()
    {
        dirLightCamera = null;

        for (int i = 0; i < 4; i++)
        {
            if (depthTextures[i])
            {
                DestroyImmediate(depthTextures[i]);
            }
        }
    }

    void Awake()
    {
        biasMatrix.SetRow(0, new Vector4(0.5f, 0, 0, 0.5f));
        biasMatrix.SetRow(1, new Vector4(0, 0.5f, 0, 0.5f));
        biasMatrix.SetRow(2, new Vector4(0, 0, 0.5f, 0.5f));
        biasMatrix.SetRow(3, new Vector4(0, 0, 0, 1f));

        InitFrustumCorners();
    }

    // Use this for initialization
    void Start()
    {
    }

    private void CreateRenderTexture()
    {
        RenderTextureFormat rtFormat = RenderTextureFormat.Default;
        if (!SystemInfo.SupportsRenderTextureFormat(rtFormat))
            rtFormat = RenderTextureFormat.Default;

        for (int i = 0; i < 4; i++)
        {
            depthTextures[i] = new RenderTexture(1024, 1024, 24, rtFormat);
            Shader.SetGlobalTexture("_gShadowMapTexture" + i, depthTextures[i]);
        }
    }

    public Camera CreateDirLightCamera()
    {
        GameObject goLightCamera = new GameObject("Directional Light Camera");
        Camera LightCamera = goLightCamera.AddComponent<Camera>();

        LightCamera.cullingMask = 1 << LayerMask.NameToLayer("Caster");
        LightCamera.backgroundColor = Color.white;
        LightCamera.clearFlags = CameraClearFlags.SolidColor;
        LightCamera.orthographic = true;
        LightCamera.enabled = false;

        for (int i = 0; i < 4; i++)
        {
            dirLightCameraSplits[i] = new GameObject("dirLightCameraSplits" + i);
        }

        return LightCamera;
    }

    private void Update()
    {
        CalcMainCameraSplitsFrustumCorners();
        CalcLightCameraSplitsFrustum();

        if (dirLight)
        {
            if (!dirLightCamera)
            {
                dirLightCamera = CreateDirLightCamera();

                CreateRenderTexture();
            }

            Shader.SetGlobalFloat("_gShadowBias", 0.005f);
            Shader.SetGlobalFloat("_gShadowStrength", 0.5f);

            world2ShadowMats.Clear();
            for (int i = 0; i < 4; i++)
            {
                ConstructLightCameraSplits(i);

                dirLightCamera.targetTexture = depthTextures[i];
                dirLightCamera.RenderWithShader(shadowCaster, "");

                Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(dirLightCamera.projectionMatrix, false);
                world2ShadowMats.Add(projectionMatrix * dirLightCamera.worldToCameraMatrix);
            }

            Shader.SetGlobalMatrixArray("_gWorld2Shadow", world2ShadowMats);
        }
    }

    float[] _LightSplitsNear;
    float[] _LightSplitsFar;

    struct FrustumCorners
    {
        public Vector3[] nearCorners;
        public Vector3[] farCorners;
    }

    FrustumCorners[] mainCamera_Splits_fcs;
    FrustumCorners[] lightCamera_Splits_fcs;

    void InitFrustumCorners()
    {
        mainCamera_Splits_fcs = new FrustumCorners[4];
        lightCamera_Splits_fcs = new FrustumCorners[4];
        for (int i = 0; i < 4; i++)
        {
            mainCamera_Splits_fcs[i].nearCorners = new Vector3[4];
            mainCamera_Splits_fcs[i].farCorners = new Vector3[4];

            lightCamera_Splits_fcs[i].nearCorners = new Vector3[4];
            lightCamera_Splits_fcs[i].farCorners = new Vector3[4];
        }
    }

    void CalcMainCameraSplitsFrustumCorners()
    {
        float near = Camera.main.nearClipPlane;
        float far = Camera.main.farClipPlane;

        float[] nears = { near, far * 0.067f + near, far * 0.133f + far * 0.067f + near, far * 0.267f + far * 0.133f + far * 0.067f + near };
        float[] fars = { far * 0.067f + near, far * 0.133f + far * 0.067f + near, far * 0.267f + far * 0.133f + far * 0.067f + near, far };

        _LightSplitsNear = nears;
        _LightSplitsFar = fars;

        Shader.SetGlobalVector("_gLightSplitsNear", new Vector4(_LightSplitsNear[0], _LightSplitsNear[1], _LightSplitsNear[2], _LightSplitsNear[3]));
        Shader.SetGlobalVector("_gLightSplitsFar", new Vector4(_LightSplitsFar[0], _LightSplitsFar[1], _LightSplitsFar[2], _LightSplitsFar[3]));

        for (int k = 0; k < 4; k++)
        {
            Camera.main.CalculateFrustumCorners(new Rect(0, 0, 1, 1), _LightSplitsNear[k], Camera.MonoOrStereoscopicEye.Mono, mainCamera_Splits_fcs[k].nearCorners);
            for (int i = 0; i < 4; i++)
            {
                mainCamera_Splits_fcs[k].nearCorners[i] = Camera.main.transform.TransformPoint(mainCamera_Splits_fcs[k].nearCorners[i]);
            }

            Camera.main.CalculateFrustumCorners(new Rect(0, 0, 1, 1), _LightSplitsFar[k], Camera.MonoOrStereoscopicEye.Mono, mainCamera_Splits_fcs[k].farCorners);
            for (int i = 0; i < 4; i++)
            {
                mainCamera_Splits_fcs[k].farCorners[i] = Camera.main.transform.TransformPoint(mainCamera_Splits_fcs[k].farCorners[i]);
            }
        }
    }

    void CalcLightCameraSplitsFrustum()
    {
        if (dirLightCamera == null)
            return;

        for (int k = 0; k < 4; k++)
        {
            for (int i = 0; i < 4; i++)
            {
                lightCamera_Splits_fcs[k].nearCorners[i] = dirLightCameraSplits[k].transform.InverseTransformPoint(mainCamera_Splits_fcs[k].nearCorners[i]);
                lightCamera_Splits_fcs[k].farCorners[i] = dirLightCameraSplits[k].transform.InverseTransformPoint(mainCamera_Splits_fcs[k].farCorners[i]);
            }

            float[] xs = { lightCamera_Splits_fcs[k].nearCorners[0].x, lightCamera_Splits_fcs[k].nearCorners[1].x, lightCamera_Splits_fcs[k].nearCorners[2].x, lightCamera_Splits_fcs[k].nearCorners[3].x,
                       lightCamera_Splits_fcs[k].farCorners[0].x, lightCamera_Splits_fcs[k].farCorners[1].x, lightCamera_Splits_fcs[k].farCorners[2].x, lightCamera_Splits_fcs[k].farCorners[3].x };

            float[] ys = { lightCamera_Splits_fcs[k].nearCorners[0].y, lightCamera_Splits_fcs[k].nearCorners[1].y, lightCamera_Splits_fcs[k].nearCorners[2].y, lightCamera_Splits_fcs[k].nearCorners[3].y,
                       lightCamera_Splits_fcs[k].farCorners[0].y, lightCamera_Splits_fcs[k].farCorners[1].y, lightCamera_Splits_fcs[k].farCorners[2].y, lightCamera_Splits_fcs[k].farCorners[3].y };

            float[] zs = { lightCamera_Splits_fcs[k].nearCorners[0].z, lightCamera_Splits_fcs[k].nearCorners[1].z, lightCamera_Splits_fcs[k].nearCorners[2].z, lightCamera_Splits_fcs[k].nearCorners[3].z,
                       lightCamera_Splits_fcs[k].farCorners[0].z, lightCamera_Splits_fcs[k].farCorners[1].z, lightCamera_Splits_fcs[k].farCorners[2].z, lightCamera_Splits_fcs[k].farCorners[3].z };

            float minX = Mathf.Min(xs);
            float maxX = Mathf.Max(xs);

            float minY = Mathf.Min(ys);
            float maxY = Mathf.Max(ys);

            float minZ = Mathf.Min(zs);
            float maxZ = Mathf.Max(zs);

            lightCamera_Splits_fcs[k].nearCorners[0] = new Vector3(minX, minY, minZ);
            lightCamera_Splits_fcs[k].nearCorners[1] = new Vector3(maxX, minY, minZ);
            lightCamera_Splits_fcs[k].nearCorners[2] = new Vector3(maxX, maxY, minZ);
            lightCamera_Splits_fcs[k].nearCorners[3] = new Vector3(minX, maxY, minZ);

            lightCamera_Splits_fcs[k].farCorners[0] = new Vector3(minX, minY, maxZ);
            lightCamera_Splits_fcs[k].farCorners[1] = new Vector3(maxX, minY, maxZ);
            lightCamera_Splits_fcs[k].farCorners[2] = new Vector3(maxX, maxY, maxZ);
            lightCamera_Splits_fcs[k].farCorners[3] = new Vector3(minX, maxY, maxZ);

            Vector3 pos = lightCamera_Splits_fcs[k].nearCorners[0] + (lightCamera_Splits_fcs[k].nearCorners[2] - lightCamera_Splits_fcs[k].nearCorners[0]) * 0.5f;


            dirLightCameraSplits[k].transform.position = dirLightCameraSplits[k].transform.TransformPoint(pos);
            dirLightCameraSplits[k].transform.rotation = dirLight.transform.rotation;

        }
    }

    void ConstructLightCameraSplits(int k)
    {
        dirLightCamera.transform.position = dirLightCameraSplits[k].transform.position;
        dirLightCamera.transform.rotation = dirLightCameraSplits[k].transform.rotation;

        dirLightCamera.nearClipPlane = lightCamera_Splits_fcs[k].nearCorners[0].z;
        dirLightCamera.farClipPlane = lightCamera_Splits_fcs[k].farCorners[0].z;

        dirLightCamera.aspect = Vector3.Magnitude(lightCamera_Splits_fcs[k].nearCorners[0] - lightCamera_Splits_fcs[k].nearCorners[1]) / Vector3.Magnitude(lightCamera_Splits_fcs[k].nearCorners[1] - lightCamera_Splits_fcs[k].nearCorners[2]);
        dirLightCamera.orthographicSize = Vector3.Magnitude(lightCamera_Splits_fcs[k].nearCorners[1] - lightCamera_Splits_fcs[k].nearCorners[2]) * 0.5f;
    }

    void CalcWorldToShadows()
    {

    }

    void OnDrawGizmos()
    {
        if (dirLightCamera == null)
            return;

        /*
        Gizmos.color = Color.blue;
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawSphere(mainCamera_fcs.nearCorners[i], 0.1f);
            Gizmos.DrawSphere(mainCamera_fcs.farCorners[i], 0.1f);
        }

        Gizmos.color = Color.red;
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawSphere(lightCamera_fcs.nearCorners[i], 0.1f);
            Gizmos.DrawSphere(lightCamera_fcs.farCorners[i], 0.1f);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawLine(lightCamera_fcs.nearCorners[0], lightCamera_fcs.nearCorners[1]);
        Gizmos.DrawLine(lightCamera_fcs.nearCorners[1], lightCamera_fcs.nearCorners[2]);
        Gizmos.DrawLine(lightCamera_fcs.nearCorners[2], lightCamera_fcs.nearCorners[3]);
        Gizmos.DrawLine(lightCamera_fcs.nearCorners[3], lightCamera_fcs.nearCorners[0]);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(lightCamera_fcs.farCorners[0], lightCamera_fcs.farCorners[1]);
        Gizmos.DrawLine(lightCamera_fcs.farCorners[1], lightCamera_fcs.farCorners[2]);
        Gizmos.DrawLine(lightCamera_fcs.farCorners[2], lightCamera_fcs.farCorners[3]);
        Gizmos.DrawLine(lightCamera_fcs.farCorners[3], lightCamera_fcs.farCorners[0]);

        Gizmos.DrawLine(lightCamera_fcs.nearCorners[0], lightCamera_fcs.farCorners[0]);
        Gizmos.DrawLine(lightCamera_fcs.nearCorners[1], lightCamera_fcs.farCorners[1]);
        Gizmos.DrawLine(lightCamera_fcs.nearCorners[2], lightCamera_fcs.farCorners[2]);
        Gizmos.DrawLine(lightCamera_fcs.nearCorners[3], lightCamera_fcs.farCorners[3]);
        */

        FrustumCorners[] fcs = new FrustumCorners[4];
        for (int k = 0; k < 4; k++)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(mainCamera_Splits_fcs[k].nearCorners[1], mainCamera_Splits_fcs[k].nearCorners[2]);

            fcs[k].nearCorners = new Vector3[4];
            fcs[k].farCorners = new Vector3[4];

            for (int i = 0; i < 4; i++)
            {
                fcs[k].nearCorners[i] = dirLightCameraSplits[k].transform.TransformPoint(lightCamera_Splits_fcs[k].nearCorners[i]);
                fcs[k].farCorners[i] = dirLightCameraSplits[k].transform.TransformPoint(lightCamera_Splits_fcs[k].farCorners[i]);
            }

            Gizmos.color = Color.red;
            Gizmos.DrawLine(fcs[k].nearCorners[0], fcs[k].nearCorners[1]);
            Gizmos.DrawLine(fcs[k].nearCorners[1], fcs[k].nearCorners[2]);
            Gizmos.DrawLine(fcs[k].nearCorners[2], fcs[k].nearCorners[3]);
            Gizmos.DrawLine(fcs[k].nearCorners[3], fcs[k].nearCorners[0]);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(fcs[k].farCorners[0], fcs[k].farCorners[1]);
            Gizmos.DrawLine(fcs[k].farCorners[1], fcs[k].farCorners[2]);
            Gizmos.DrawLine(fcs[k].farCorners[2], fcs[k].farCorners[3]);
            Gizmos.DrawLine(fcs[k].farCorners[3], fcs[k].farCorners[0]);

            Gizmos.DrawLine(fcs[k].nearCorners[0], fcs[k].farCorners[0]);
            Gizmos.DrawLine(fcs[k].nearCorners[1], fcs[k].farCorners[1]);
            Gizmos.DrawLine(fcs[k].nearCorners[2], fcs[k].farCorners[2]);
            Gizmos.DrawLine(fcs[k].nearCorners[3], fcs[k].farCorners[3]);
        }
    }
}
