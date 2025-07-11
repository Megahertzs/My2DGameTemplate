using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class TMP_TextEffectEngine : MonoBehaviour
{
    [Header("General")]
    public bool animate = true;

    TMP_Text tmpText;
    TMP_MeshInfo[] cachedMeshInfo;
    string masterText;

    float effectStartTime;  // NEW: Anchor for time-based effects

    void Awake()
    {
        tmpText = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        masterText = tmpText.text;
        effectStartTime = Time.time;  // Reset the time anchor
    }

    void LateUpdate()
    {
        if (!animate) return;

        tmpText.ForceMeshUpdate();
        cachedMeshInfo = tmpText.textInfo.CopyMeshInfoVertexData();

        TMP_TextInfo textInfo = tmpText.textInfo;
        int charCount = textInfo.characterCount;
        if (charCount == 0) return;

        Bounds fullBounds = tmpText.bounds;

        for (int i = 0; i < charCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue;

            int vi = textInfo.characterInfo[i].vertexIndex;
            int mi = textInfo.characterInfo[i].materialReferenceIndex;

            Vector3[] verts = textInfo.meshInfo[mi].vertices;
            Vector3[] cached = cachedMeshInfo[mi].vertices;

            for (int j = 0; j < 4; j++) verts[vi + j] = cached[vi + j];

            Vector3 offset = Vector3.zero;

            if (useEntrance) offset += ApplyEntrance(i);
            if (useWaveY) offset += ApplyWaveY(i);
            if (useWaveX) offset += ApplyWaveX(i);
            if (useShake) offset += ApplyShake();
            if (useBounce) offset += ApplyBounce(i);
            if (useSpiral) offset += ApplySpiral(i);
            if (useFlip) ApplyFlip(verts, vi, i);
            if (useStretch) ApplyStretch(verts, vi, i);
            if (useSquish) ApplySquish(verts, vi, i);
            if (usePulse && pulseMode == PulseMode.PerLetter) ApplyPulseLetter(verts, vi, i);

            for (int j = 0; j < 4; j++) verts[vi + j] += offset;
        }

        for (int m = 0; m < textInfo.meshInfo.Length; m++)
        {
            Color32[] colors = textInfo.meshInfo[m].colors32;

            for (int i = 0; i < charCount; i++)
            {
                if (!textInfo.characterInfo[i].isVisible) continue;

                int vi = textInfo.characterInfo[i].vertexIndex;
                Color32 c = tmpText.color;

                if (useRainbow) c = ApplyRainbow(i);
                if (useColorLerp) c = ApplyColorLerp();
                if (useFade) c.a = (byte)(ApplyFade() * 255f);
                if (useFlicker) c.a = (byte)(ApplyFlicker(i) * 255f);
                if (useBlink) c.a = (byte)(ApplyBlink() * 255f);
                if (useGlowPulse) c = ApplyGlowPulse();

                if (useShineSwipe)
                {
                    float shineT = GetShineStrength(textInfo, i, fullBounds);
                    if (shineT > 0f)
                    {
                        Color shine = shineColor;
                        shine.a = shineOpacity * shineT;
                        c = Color.Lerp(c, shine, shine.a);
                    }
                }

                colors[vi + 0] = c;
                colors[vi + 1] = c;
                colors[vi + 2] = c;
                colors[vi + 3] = c;
            }
        }

        if (usePulse && pulseMode == PulseMode.WholePhrase) ApplyPulseWhole(); else tmpText.rectTransform.localScale = Vector3.one;
        if (useRotate) ApplyRotateWhole(); else tmpText.rectTransform.localRotation = Quaternion.identity;

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            var meshInfo = textInfo.meshInfo[i];
            meshInfo.mesh.vertices = meshInfo.vertices;
            meshInfo.mesh.colors32 = meshInfo.colors32;
            tmpText.UpdateGeometry(meshInfo.mesh, i);
        }
    }

    [Header("Entrance")]
    public bool useEntrance;
    public enum EntranceUnit { Phrase, Letter }
    public EntranceUnit entranceUnit = EntranceUnit.Letter;
    public float entranceDistance = 500f, entranceSpeed = 3f, entranceStagger = 0.05f;
    Vector3 ApplyEntrance(int i)
    {
        float t = (Time.time - effectStartTime) - (entranceUnit == EntranceUnit.Letter ? i * entranceStagger : 0);
        t = Mathf.Max(0f, t);
        float progress = 1f - Mathf.Exp(-t * entranceSpeed);
        float d = (1f - Mathf.Clamp01(progress)) * entranceDistance;
        return new Vector3(0, d, 0);
    }

    [Header("Flip")]
    public bool useFlip;
    public enum FlipUnit { Phrase, Letter }
    public FlipUnit flipUnit = FlipUnit.Letter;
    public float flipSpeed = 1f, flipAmount = 1f, flipDelay = 0.05f;
    void ApplyFlip(Vector3[] v, int vi, int i)
    {
        float t = (Time.time - effectStartTime) - (flipUnit == FlipUnit.Letter ? i * flipDelay : 0);
        float progress = Mathf.Sin(t * flipSpeed) * flipAmount;
        Vector3 mid = (v[vi + 0] + v[vi + 2]) * 0.5f;
        for (int j = 0; j < 4; j++)
        {
            Vector3 dir = v[vi + j] - mid;
            dir.y *= progress;
            v[vi + j] = mid + dir;
        }
    }

    [Header("Shine Swipe")]
    public bool useShineSwipe;
    public enum ShineUnit { Phrase, Letter }
    public ShineUnit shineUnit = ShineUnit.Phrase;
    public enum ShineDirection { Horizontal, Vertical }
    public ShineDirection shineDirection = ShineDirection.Horizontal;
    public Color shineColor = Color.white;
    [Range(0f, 1f)] public float shineOpacity = 0.75f;
    public float shineWidth = 50f, shineSpeed = 1f, shineDelay = 0f;
    float GetShineStrength(TMP_TextInfo info, int i, Bounds fullBounds)
    {
        float t = (Time.time - effectStartTime) - shineDelay;
        if (t < 0) return 0f;
        TMP_CharacterInfo ci = info.characterInfo[i];
        Vector3 mid = (ci.bottomLeft + ci.topRight) * 0.5f;
        Bounds b = (shineUnit == ShineUnit.Phrase) ? fullBounds : new Bounds(mid, Vector3.zero);
        float s = Mathf.Repeat(t * shineSpeed, 1f);
        float coord = shineDirection == ShineDirection.Horizontal ? mid.x : mid.y;
        float min = shineDirection == ShineDirection.Horizontal ? b.min.x : b.min.y;
        float max = shineDirection == ShineDirection.Horizontal ? b.max.x : b.max.y;
        float shinePos = Mathf.Lerp(min - shineWidth, max + shineWidth, s);
        float d = Mathf.Abs(coord - shinePos);
        float edge = Mathf.Clamp01(1f - d / shineWidth);
        return edge * edge;
    }

    [Header("Rainbow")]
    public bool useRainbow;
    public enum RainbowMode { Phrase, Letter }
    public RainbowMode rainbowMode = RainbowMode.Letter;
    public float rainbowSpeed = 1f, rainbowOffset = 0.1f;
    Color ApplyRainbow(int i)
    {
        float hue = rainbowMode == RainbowMode.Letter
            ? Mathf.Repeat((Time.time - effectStartTime) * rainbowSpeed + i * rainbowOffset, 1f)
            : Mathf.Repeat((Time.time - effectStartTime) * rainbowSpeed, 1f);
        return Color.HSVToRGB(hue, 1f, 1f);
    }

    [Header("Wave Y")] public bool useWaveY; public float waveYSpeed = 5f, waveYAmplitude = 5f; Vector3 ApplyWaveY(int i) => new Vector3(0, Mathf.Sin((Time.time - effectStartTime) * waveYSpeed + i) * waveYAmplitude, 0);
    [Header("Wave X")] public bool useWaveX; public float waveXSpeed = 5f, waveXAmplitude = 5f; Vector3 ApplyWaveX(int i) => new Vector3(Mathf.Sin((Time.time - effectStartTime) * waveXSpeed + i) * waveXAmplitude, 0, 0);
    [Header("Shake")] public bool useShake; public float shakeMagnitude = 1f; Vector3 ApplyShake() => new Vector3(Random.Range(-shakeMagnitude, shakeMagnitude), Random.Range(-shakeMagnitude, shakeMagnitude), 0);
    [Header("Bounce")] public bool useBounce; public float bounceSpeed = 5f, bounceHeight = 5f; Vector3 ApplyBounce(int i) => new Vector3(0, Mathf.Abs(Mathf.Sin((Time.time - effectStartTime) * bounceSpeed + i)) * bounceHeight, 0);
    [Header("Spiral")] public bool useSpiral; public float spiralSpeed = 2f, spiralSize = 5f; Vector3 ApplySpiral(int i) => new Vector3(Mathf.Sin((Time.time - effectStartTime) * spiralSpeed + i) * spiralSize, Mathf.Cos((Time.time - effectStartTime) * spiralSpeed + i) * spiralSize, 0);
    [Header("Stretch")] public bool useStretch; public float stretchSpeed = 2f, stretchAmount = 1.5f; void ApplyStretch(Vector3[] v, int vi, int i) { float scale = 1f + Mathf.Sin((Time.time - effectStartTime) * stretchSpeed + i) * (stretchAmount - 1f); Vector3 mid = (v[vi + 0] + v[vi + 2]) * 0.5f; for (int j = 0; j < 4; j++) v[vi + j] = mid + (v[vi + j] - mid) * scale; }
    [Header("Squish")] public bool useSquish; public float squishSpeed = 2f, squishAmount = 0.5f; void ApplySquish(Vector3[] v, int vi, int i) { float scaleX = Mathf.Sin((Time.time - effectStartTime) * squishSpeed + i) * (1f - squishAmount) + squishAmount; Vector3 mid = (v[vi + 0] + v[vi + 2]) * 0.5f; for (int j = 0; j < 4; j++) { Vector3 dir = v[vi + j] - mid; dir.x *= scaleX; v[vi + j] = mid + dir; } }
    [Header("Flicker")] public bool useFlicker; public float flickerSpeed = 10f, flickerMinAlpha = 0.4f, flickerMaxAlpha = 1f; float ApplyFlicker(int i) => Mathf.Lerp(flickerMinAlpha, flickerMaxAlpha, Mathf.PerlinNoise(i, (Time.time - effectStartTime) * flickerSpeed));
    [Header("Blink")] public bool useBlink; public float blinkSpeed = 2f, blinkDutyCycle = 0.5f; float ApplyBlink() => Mathf.Repeat((Time.time - effectStartTime) * blinkSpeed, 1f) < blinkDutyCycle ? 1f : 0f;
    [Header("Fade")] public bool useFade; public float fadeSpeed = 1f; float ApplyFade() => Mathf.PingPong((Time.time - effectStartTime) * fadeSpeed, 1f);
    [Header("Color Lerp")] public bool useColorLerp; public Color fromColor = Color.white, toColor = Color.red; public float colorLerpSpeed = 1f; Color ApplyColorLerp() => Color.Lerp(fromColor, toColor, Mathf.PingPong((Time.time - effectStartTime) * colorLerpSpeed, 1f));
    [Header("Pulse")] public bool usePulse; public enum PulseMode { WholePhrase, PerLetter }
    public PulseMode pulseMode = PulseMode.WholePhrase; public float pulseSpeed = 2f, pulseScale = 1.2f; void ApplyPulseWhole() { float s = 1f + Mathf.Sin((Time.time - effectStartTime) * pulseSpeed) * (pulseScale - 1f); tmpText.rectTransform.localScale = Vector3.one * s; }
    void ApplyPulseLetter(Vector3[] v, int vi, int i) { float s = 1f + Mathf.Sin((Time.time - effectStartTime) * pulseSpeed + i) * (pulseScale - 1f); Vector3 mid = (v[vi + 0] + v[vi + 2]) * 0.5f; for (int j = 0; j < 4; j++) v[vi + j] = mid + (v[vi + j] - mid) * s; }
    [Header("Rotate")] public bool useRotate; public float rotateSpeed = 30f, rotateAmount = 10f; void ApplyRotateWhole() => tmpText.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin((Time.time - effectStartTime) * rotateSpeed) * rotateAmount);
    [Header("Glow Pulse")] public bool useGlowPulse; public Color glowColor = Color.cyan; [Range(0f, 1f)] public float glowMax = 1f; public float glowSpeed = 2f; Color ApplyGlowPulse() { float t = (Mathf.Sin((Time.time - effectStartTime) * glowSpeed) + 1f) * 0.5f; return Color.Lerp(tmpText.color, glowColor, t * glowMax); }
}
