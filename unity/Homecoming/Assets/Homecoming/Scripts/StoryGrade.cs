using UnityEngine;

namespace Homecoming
{
    // The film camera's finishing pass: bloom, exposure, filmic tone mapping, grade,
    // vignette and grain. StoryLighting sets the values per shot. The IMGUI interface is
    // drawn after this pass, so captions and menus are never graded.
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class StoryGrade : MonoBehaviour
    {
        [Header("Exposure and tone")]
        public float exposure = 1.25f;
        [Range(0, 2)] public float saturation = 1;
        [Range(.5f, 1.5f)] public float contrast = 1.04f;
        public Color tint = Color.white;
        [Tooltip("Colour pushed into the shadows; alpha is the strength.")]
        public Color shadowTint = new Color(.82f, .92f, 1.05f, .35f);
        [Header("Lens")]
        [Range(0, 1)] public float vignette = .35f;
        [Range(0, .2f)] public float grain = .025f;
        [Header("Bloom")]
        public float bloomThreshold = .95f;
        [Range(0, 3)] public float bloomIntensity = .55f;
        public bool animateGrain = true;
        [Header("Selective focus")]
        public float focusDistance = 5;
        public float focusRange = 2;
        [Range(0, 1)] public float focusStrength;
        public float shotTime;

        const int Iterations = 5;
        static readonly int ThresholdId = Shader.PropertyToID("_Threshold"), KneeId = Shader.PropertyToID("_Knee"),
            BloomTexId = Shader.PropertyToID("_BloomTex"), BloomIntensityId = Shader.PropertyToID("_BloomIntensity"),
            ExposureId = Shader.PropertyToID("_Exposure"), SaturationId = Shader.PropertyToID("_Saturation"),
            ContrastId = Shader.PropertyToID("_Contrast"), VignetteId = Shader.PropertyToID("_Vignette"),
            GrainId = Shader.PropertyToID("_Grain"), SeedId = Shader.PropertyToID("_Seed"),
            TintId = Shader.PropertyToID("_Tint"), ShadowTintId = Shader.PropertyToID("_ShadowTint");
        static readonly int FocusTexId = Shader.PropertyToID("_FocusTex"), FocusId = Shader.PropertyToID("_Focus");
        readonly RenderTexture[] chain = new RenderTexture[Iterations];
        Material material;
        void OnEnable() { GetComponent<Camera>().depthTextureMode |= DepthTextureMode.Depth; }

        Material Material
        {
            get
            {
                if (material != null) return material;
                var shader = Resources.Load<Shader>("Shaders/FilmGrade");
                if (shader == null || !shader.isSupported) return null;
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                return material;
            }
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var mat = Material;
            if (mat == null) { Graphics.Blit(source, destination); return; }

            mat.SetFloat(ThresholdId, bloomThreshold);
            mat.SetFloat(KneeId, bloomThreshold * .5f);
            var format = source.format;
            RenderTexture focusSmall = null, focusBlur = null;
            if (focusStrength > .001f)
            {
                focusSmall = RenderTexture.GetTemporary(Mathf.Max(1, source.width / 2), Mathf.Max(1, source.height / 2), 0, format);
                focusBlur = RenderTexture.GetTemporary(focusSmall.width, focusSmall.height, 0, format);
                focusSmall.filterMode = focusBlur.filterMode = FilterMode.Bilinear;
                Graphics.Blit(source, focusSmall, mat, 1);
                Graphics.Blit(focusSmall, focusBlur, mat, 4);
            }
            int width = source.width / 2, height = source.height / 2, levels = 0;
            RenderTexture current = null;
            for (int i = 0; i < Iterations && width >= 8 && height >= 8; i++, width /= 2, height /= 2)
            {
                chain[i] = RenderTexture.GetTemporary(width, height, 0, format);
                chain[i].filterMode = FilterMode.Bilinear;
                Graphics.Blit(i == 0 ? source : current, chain[i], mat, i == 0 ? 0 : 1);
                current = chain[i];
                levels++;
            }
            for (int i = levels - 2; i >= 0; i--)
            {
                Graphics.Blit(current, chain[i], mat, 2);
                current = chain[i];
            }

            mat.SetTexture(BloomTexId, current);
            mat.SetFloat(BloomIntensityId, levels > 0 ? bloomIntensity : 0);
            mat.SetFloat(ExposureId, exposure);
            mat.SetFloat(SaturationId, saturation);
            mat.SetFloat(ContrastId, contrast);
            mat.SetFloat(VignetteId, vignette);
            mat.SetFloat(GrainId, grain);
            mat.SetFloat(SeedId, animateGrain ? shotTime % 97 : 0);
            mat.SetTexture(FocusTexId, focusBlur != null ? focusBlur : source);
            mat.SetVector(FocusId, new Vector4(focusDistance, focusRange, focusStrength, 0));
            mat.SetColor(TintId, tint);
            mat.SetColor(ShadowTintId, shadowTint);
            Graphics.Blit(source, destination, mat, 3);
            if (focusSmall != null) RenderTexture.ReleaseTemporary(focusSmall);
            if (focusBlur != null) RenderTexture.ReleaseTemporary(focusBlur);

            for (int i = 0; i < levels; i++)
            {
                RenderTexture.ReleaseTemporary(chain[i]);
                chain[i] = null;
            }
        }

        void OnDisable()
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
            material = null;
        }
    }
}
