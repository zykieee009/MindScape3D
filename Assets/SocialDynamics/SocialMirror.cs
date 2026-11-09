using UnityEngine;
using UnityEngine.Rendering;

namespace Mindscape.SocialDynamics
{
    // A small dedicated camera gives the mirror a live view of the player. For the ending a second camera can join it:
    // the same view, but it also sees the YoungerLayer (younger Joey standing beside you, invisible to the room's own
    // camera), and the mirror cross-fades between the two pictures (Blend).
    public sealed class SocialMirror : MonoBehaviour
    {
        public const int YoungerLayer = 30;
        public Transform player;
        Renderer[] body;
        ShadowCastingMode[] bodyModes;
        public float planeX;
        public float side = -1;        // which way the room is from the mirror, along X (-1: the mirror is on an east wall)
        public float eyeHeight = 2.6f; // the reflection camera's height (the player's eyes)
        Camera reflection, withYounger;
        RenderTexture texture, youngerTexture;
        Material material;
        bool blendShader;
        float blend, glow = 1;

        /// <summary>0 shows the plain reflection, 1 the reflection with younger Joey (after ShowYounger).</summary>
        public float Blend
        {
            get => blend;
            set { blend = Mathf.Clamp01(value); if (material != null && blendShader) material.SetFloat("_Blend", blend); }
        }

        /// <summary>Brightens the mirror a little (1 = as it is).</summary>
        public float Glow
        {
            get => glow;
            set { glow = value; if (material != null && blendShader) material.SetFloat("_Glow", glow); }
        }

        void Start()
        {
            gameObject.layer = 2;
            texture = new RenderTexture(256, 512, 16);
            var shader = Resources.Load<Shader>("Shaders/MirrorBlend");
            blendShader = shader != null && shader.isSupported;
            if (blendShader)
            {
                material = new Material(shader);
                material.SetTexture("_MainTex", texture);
                material.SetTexture("_SecondTex", texture);
                material.SetFloat("_Blend", blend); material.SetFloat("_Glow", glow);
            }
            else
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                material.mainTexture = texture;
                material.mainTextureScale = new Vector2(-1, 1); material.mainTextureOffset = new Vector2(1, 0);
            }
            GetComponent<Renderer>().material = material;
            reflection = MakeCamera("Mirror reflection camera", texture, ~((1 << 2) | (1 << YoungerLayer)));
        }

        Camera MakeCamera(string name, RenderTexture target, int mask)
        {
            var cam = new GameObject(name).AddComponent<Camera>();
            cam.targetTexture = target; cam.cullingMask = mask;
            cam.nearClipPlane = .05f; cam.farClipPlane = 25; cam.fieldOfView = 55;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.18f, .23f, .27f);
            return cam;
        }

        /// <summary>Starts the second reflection, which also sees everything on YoungerLayer. Blend fades it in.</summary>
        public void ShowYounger()
        {
            if (withYounger != null || !blendShader) return;
            youngerTexture = new RenderTexture(256, 512, 16);
            withYounger = MakeCamera("Mirror reflection camera (younger)", youngerTexture, ~(1 << 2));
            material.SetTexture("_SecondTex", youngerTexture);
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
        }
        bool Mine(Camera camera) => camera != null && (camera == reflection || camera == withYounger);
        // ShotFocus hides the player's body (shadow only) while a story shot looks at the mirror, so the reflection
        // cameras alone put the body back for their own render.
        void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!Mine(camera) || player == null) return;
            if (body == null) body = player.GetComponentsInChildren<Renderer>(true);
            if (bodyModes == null || bodyModes.Length != body.Length) bodyModes = new ShadowCastingMode[body.Length];
            for (int i = 0; i < body.Length; i++)
            {
                if (body[i] == null) continue;
                bodyModes[i] = body[i].shadowCastingMode;
                if (bodyModes[i] == ShadowCastingMode.ShadowsOnly) body[i].shadowCastingMode = ShadowCastingMode.On;
            }
        }
        void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!Mine(camera) || body == null || bodyModes == null) return;
            for (int i = 0; i < body.Length; i++) if (body[i] != null) body[i].shadowCastingMode = bodyModes[i];
        }
        void LateUpdate()
        {
            if (reflection == null) return;
            reflection.transform.position = new Vector3(planeX + side * .25f, eyeHeight, transform.position.z);
            reflection.transform.LookAt(player.position + Vector3.up * (eyeHeight - .8f));
            if (withYounger != null) withYounger.transform.SetPositionAndRotation(reflection.transform.position, reflection.transform.rotation);
        }
        void OnDestroy()
        {
            if (reflection != null) Destroy(reflection.gameObject);
            if (withYounger != null) Destroy(withYounger.gameObject);
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (youngerTexture != null) { youngerTexture.Release(); Destroy(youngerTexture); }
            if (material != null) Destroy(material);
        }
    }
}
