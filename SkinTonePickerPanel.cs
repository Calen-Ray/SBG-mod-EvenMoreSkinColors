using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EvenMoreSkinColors
{
    internal sealed class SkinTonePickerPanel : MonoBehaviour
    {
        private static readonly Color32[] PresetColors =
        {
            new Color32(255, 224, 189, 255),
            new Color32(241, 194, 125, 255),
            new Color32(224, 172, 105, 255),
            new Color32(198, 134, 66, 255),
            new Color32(141, 85, 36, 255),
            new Color32(92, 51, 23, 255)
        };

        private const float ExpandedWidth = 500f;
        private const float ExpandedHeight = 470f;
        private const float MinimizedWidth = 150f;
        private const float MinimizedHeight = 44f;

        private PlayerCustomizationMenu _menu;
        private RectTransform _rootRect;
        private Image _backgroundImage;
        private GameObject _expandedContent;
        private GameObject _minimizedLauncher;
        private Image _launcherSwatch;
        private ToggleButton _toggleButton;
        private ToggleButton _minimizeButton;
        private Image _previewSwatch;
        private TMP_Text _hexLabel;
        private GameObject _advancedArea;
        private RectTransform _wheelHandle;
        private RectTransform _svHandle;
        private Texture2D _wheelTexture;
        private Texture2D _svTexture;

        private float _hue;
        private float _saturation;
        private float _value;
        // Cache the hue we last rendered the SV gradient for. The gradient is parameterized by
        // hue alone, so SV-square drags don't need a regen and small wheel jitters don't either.
        private float _lastRegenHue = float.NaN;
        // Reusable pixel buffer for the SV gradient. Allocating a fresh 65k-pixel array per drag
        // tick was a measurable hitch on its own, separate from the HSVToRGB cost.
        private Color32[] _svPixels;

        internal void Initialize(PlayerCustomizationMenu menu)
        {
            _menu = menu;
            BuildUi();
            LoadFromState();
            RefreshUi();
            // RefreshUi no longer calls ApplyToPreview on the expanded path (it skips the
            // duplicate call when reached via SetLocalSelection). On first build there's no
            // prior SetLocalSelection, so apply the preview material once explicitly.
            SkinToneState.ApplyToPreview(_menu);
        }

        private void OnDestroy()
        {
            if (_wheelTexture != null)
            {
                Destroy(_wheelTexture);
            }

            if (_svTexture != null)
            {
                Destroy(_svTexture);
            }
        }

        private void BuildUi()
        {
            // Opt out of any parent layout group (e.g. the skin-color GridLayoutGroup) so our
            // size is not overridden by a grid cell.
            LayoutElement ignoreLayout = gameObject.AddComponent<LayoutElement>();
            ignoreLayout.ignoreLayout = true;

            // UI GameObjects parented to a RectTransform get a RectTransform auto-added, so we
            // reuse whichever one exists rather than re-adding it.
            _rootRect = gameObject.GetComponent<RectTransform>();
            if (_rootRect == null)
            {
                _rootRect = gameObject.AddComponent<RectTransform>();
            }
            _rootRect.anchorMin = new Vector2(0f, 0f);
            _rootRect.anchorMax = new Vector2(0f, 0f);
            _rootRect.pivot = new Vector2(0f, 0f);
            _rootRect.sizeDelta = new Vector2(ExpandedWidth, ExpandedHeight);
            _rootRect.anchoredPosition = new Vector2(12f, 12f);

            _backgroundImage = gameObject.AddComponent<Image>();
            _backgroundImage.color = new Color(0.08f, 0.1f, 0.14f, 0.94f);
            _backgroundImage.raycastTarget = true;

            // Two top-level groups: the full panel chrome lives under _expandedContent and the
            // tiny "Custom Tone" launcher pill lives under _minimizedLauncher. We toggle between
            // them in RefreshUi based on SkinToneState.IsPanelMinimized so users can hide the
            // picker entirely to use the vanilla skin-color buttons that sit underneath.
            _expandedContent = new GameObject("ExpandedContent", typeof(RectTransform));
            _expandedContent.transform.SetParent(transform, false);
            RectTransform expandedRect = _expandedContent.GetComponent<RectTransform>();
            expandedRect.anchorMin = Vector2.zero;
            expandedRect.anchorMax = Vector2.one;
            expandedRect.offsetMin = Vector2.zero;
            expandedRect.offsetMax = Vector2.zero;

            BuildExpandedChrome(expandedRect);
            BuildMinimizedLauncher();
        }

        private void BuildExpandedChrome(RectTransform parent)
        {
            // Manual row layout: each row is pinned to top edge of the panel at a cumulative y
            // offset. Layout groups here collided with the menu's existing layout stack, so we
            // skip them for the outer chrome and only use HorizontalLayoutGroup within rows.
            const float padX = 16f;
            const float padTop = 12f;
            const float rowGap = 8f;
            float rowWidth = ExpandedWidth - padX * 2f;
            float yCursor = padTop;

            // Minimize button sits in the upper-right corner (above the title row) so it's a
            // consistent place to collapse the whole panel without bumping into the toolbar.
            _minimizeButton = CreateToggleButton(parent, 32f, 32f);
            SetAnchoredTopRow(_minimizeButton.GetComponent<RectTransform>(), ExpandedWidth - padX - 32f, padTop, 32f, 32f);
            _minimizeButton.SetLabel("\u2013");
            _minimizeButton.Clicked += OnMinimizeClicked;

            TMP_Text title = CreateLabelUnder(parent, Compatibility.IsMoreSkinColorsPresent ? "Custom Tone Override" : "Custom Tone");
            SetAnchoredTopRow(title.rectTransform, padX, yCursor, rowWidth - 40f, 34f);
            title.fontSize = 28f;
            title.color = Color.white;
            yCursor += 34f + rowGap;

            RectTransform toolbar = CreateHRowUnder(parent, "Toolbar", padX, yCursor, rowWidth, 42f);
            _toggleButton = CreateToggleButton(toolbar, 160f, 38f);
            _toggleButton.Clicked += OnToggleClicked;
            _previewSwatch = CreateSwatch(toolbar, 38f);
            _hexLabel = CreateToolbarLabel(toolbar, 160f, 38f);
            yCursor += 42f + rowGap;

            RectTransform actionsRow = CreateHRowUnder(parent, "Actions", padX, yCursor, rowWidth, 38f);
            CreateActionButton(actionsRow, "Hex", 88f, 34f, OpenHexEditor);
            CreateActionButton(actionsRow, "Vanilla", 104f, 34f, ResetToVanilla);
            CreateActionButton(actionsRow, "Default", 104f, 34f, ResetToDefaultPreset);
            yCursor += 38f + rowGap;

            RectTransform presetsRow = CreateHRowUnder(parent, "Presets", padX, yCursor, rowWidth, 34f);
            for (int i = 0; i < PresetColors.Length; i++)
            {
                int presetIndex = i;
                CreatePresetButton(presetsRow, PresetColors[i], 46f, 30f, () => ApplyPreset(presetIndex));
            }
            yCursor += 34f + rowGap;

            const float boxSize = 190f;
            _advancedArea = new GameObject("AdvancedArea", typeof(RectTransform));
            _advancedArea.transform.SetParent(parent, false);
            RectTransform advancedRect = _advancedArea.GetComponent<RectTransform>();
            SetAnchoredTopRow(advancedRect, padX, yCursor, rowWidth, boxSize);

            // Wheel box + SV box side-by-side inside AdvancedArea.
            float boxGap = (rowWidth - boxSize * 2f) * 0.5f;
            RectTransform wheelRect = CreateBoxAt(advancedRect, "WheelBox", Mathf.Max(0f, boxGap), 0f, boxSize, out Image wheelBox);
            wheelBox.color = new Color(0f, 0f, 0f, 0.18f);
            RawImage wheelImage = CreateRawImage(wheelRect, "WheelImage");
            _wheelTexture = GenerateHueWheelTexture(256, 0.30f, 0.48f);
            wheelImage.texture = _wheelTexture;
            _wheelHandle = CreateHandle(wheelRect, 16f);
            AddDragSurface(wheelRect.gameObject, OnWheelPointer);

            RectTransform svRect = CreateBoxAt(advancedRect, "SvBox", Mathf.Max(0f, boxGap) + boxSize + 10f, 0f, boxSize, out Image svBox);
            svBox.color = new Color(0f, 0f, 0f, 0.18f);
            RawImage svImage = CreateRawImage(svRect, "SvImage");
            _svTexture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            _svTexture.wrapMode = TextureWrapMode.Clamp;
            _svTexture.filterMode = FilterMode.Bilinear;
            svImage.texture = _svTexture;
            _svHandle = CreateHandle(svRect, 14f);
            AddDragSurface(svRect.gameObject, OnSvPointer);
        }

        // Pins `rect` to the top-left of its parent at (x, -y) with the given size. Parent is
        // assumed to be a standard UI rect (centered anchors are fine for the parent).
        private static void SetAnchoredTopRow(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        private RectTransform CreateHRowUnder(RectTransform parent, string name, float x, float y, float width, float height)
        {
            GameObject row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rect = row.GetComponent<RectTransform>();
            SetAnchoredTopRow(rect, x, y, width, height);
            HorizontalLayoutGroup group = row.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 10f;
            group.childControlWidth = false;
            group.childControlHeight = false;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            return rect;
        }

        private void BuildMinimizedLauncher()
        {
            _minimizedLauncher = new GameObject("MinimizedLauncher", typeof(RectTransform), typeof(Image), typeof(Button));
            _minimizedLauncher.transform.SetParent(transform, false);
            RectTransform rect = _minimizedLauncher.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(MinimizedWidth, MinimizedHeight);
            rect.anchoredPosition = Vector2.zero;

            Image bg = _minimizedLauncher.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.1f, 0.14f, 0.94f);
            bg.raycastTarget = true;

            Button button = _minimizedLauncher.GetComponent<Button>();
            button.onClick.AddListener(OnLauncherClicked);

            // Small swatch + label so the user can see the active tone at a glance.
            GameObject swatchObj = new GameObject("LauncherSwatch", typeof(RectTransform), typeof(Image));
            swatchObj.transform.SetParent(_minimizedLauncher.transform, false);
            RectTransform swatchRect = swatchObj.GetComponent<RectTransform>();
            SetAnchoredTopRow(swatchRect, 8f, 8f, 28f, 28f);
            _launcherSwatch = swatchObj.GetComponent<Image>();
            _launcherSwatch.raycastTarget = false;

            GameObject labelObj = new GameObject("LauncherLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObj.transform.SetParent(_minimizedLauncher.transform, false);
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            SetAnchoredTopRow(labelRect, 44f, 8f, MinimizedWidth - 52f, 28f);
            TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
            label.text = "Custom Tone";
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.fontSize = 18f;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        private TMP_Text CreateLabelUnder(RectTransform parent, string text)
        {
            TMP_Text template = _menu.unlocked;
            TMP_Text label = Instantiate(template, parent);
            label.text = text;
            label.gameObject.SetActive(true);
            label.alignment = TextAlignmentOptions.Left;
            return label;
        }

        private RectTransform CreateBoxAt(RectTransform parent, string name, float x, float y, float size, out Image image)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            SetAnchoredTopRow(rect, x, y, size, size);
            image = obj.GetComponent<Image>();
            image.raycastTarget = true;
            return rect;
        }

        private void LoadFromState()
        {
            SkinToneSelection selection = SkinToneState.LocalSelection;
            Color.RGBToHSV(selection.BaseColor, out _hue, out _saturation, out _value);
            if (_value <= 0.01f)
            {
                _value = 0.01f;
            }
        }

        private void RefreshUi()
        {
            LoadFromState();
            SkinToneSelection selection = CurrentSelection();

            bool minimized = SkinToneState.IsPanelMinimized;
            if (_rootRect != null)
            {
                _rootRect.sizeDelta = minimized
                    ? new Vector2(MinimizedWidth, MinimizedHeight)
                    : new Vector2(ExpandedWidth, ExpandedHeight);
            }
            if (_backgroundImage != null)
            {
                // The launcher pill provides its own background when minimized; hiding the panel
                // root's image keeps the collapsed state to a single 150x44 pill.
                _backgroundImage.enabled = !minimized;
            }
            if (_expandedContent != null) _expandedContent.SetActive(!minimized);
            if (_minimizedLauncher != null) _minimizedLauncher.SetActive(minimized);
            if (_launcherSwatch != null) _launcherSwatch.color = selection.BaseColor;

            if (minimized)
            {
                // Skip the rest — handles/textures aren't visible while minimized.
                SkinToneState.ApplyToPreview(_menu);
                return;
            }

            _toggleButton.SetLabel(selection.Enabled ? "Custom Tone: On" : "Custom Tone: Off");
            _toggleButton.SetState(selection.Enabled);
            if (_advancedArea != null) _advancedArea.SetActive(true);
            _previewSwatch.color = selection.BaseColor;
            _hexLabel.text = $"{SkinToneSelection.ToHtml(selection.BaseColor)}  Loadout {SkinToneState.ActiveLoadoutIndex + 1}";
            RegenerateSvTexture();
            UpdateHandles();
            // SkinToneState.SetLocalSelection already calls ApplyToPreview, so we don't repeat
            // it here on the hot path. The minimize/restore + Initialize paths reach RefreshUi
            // without going through SetLocalSelection, so they call ApplyToPreview themselves.
        }

        private void OnMinimizeClicked()
        {
            SkinToneState.IsPanelMinimized = true;
            RefreshUi();
        }

        private void OnLauncherClicked()
        {
            SkinToneState.IsPanelMinimized = false;
            RefreshUi();
        }

        private SkinToneSelection CurrentSelection()
        {
            return new SkinToneSelection
            {
                Enabled = SkinToneState.LocalSelection.Enabled,
                BaseColor = Color.HSVToRGB(_hue, _saturation, _value)
            };
        }

        private void OnToggleClicked()
        {
            SkinToneSelection selection = CurrentSelection();
            selection.Enabled = !SkinToneState.LocalSelection.Enabled;
            SkinToneState.SetLocalSelection(selection, persist: true, broadcast: true);
            RefreshUi();
        }

        private void ApplyPreset(int presetIndex)
        {
            Color.RGBToHSV(PresetColors[presetIndex], out _hue, out _saturation, out _value);
            SkinToneSelection selection = CurrentSelection();
            selection.Enabled = true;
            SkinToneState.SetLocalSelection(selection, persist: true, broadcast: true);
            RefreshUi();
        }

        private void ResetToVanilla()
        {
            SkinToneSelection selection = SkinToneState.LocalSelection;
            selection.Enabled = false;
            SkinToneState.SetLocalSelection(selection, persist: true, broadcast: true);
            RefreshUi();
        }

        private void ResetToDefaultPreset()
        {
            Color.RGBToHSV(SkinToneSelection.DefaultBaseColor, out _hue, out _saturation, out _value);
            SkinToneSelection selection = CurrentSelection();
            selection.Enabled = true;
            SkinToneState.SetLocalSelection(selection, persist: true, broadcast: true);
            RefreshUi();
        }

        private void OpenHexEditor()
        {
            FullScreenMessage.ShowTextField(
                "Enter a skin tone hex value like #D8A074.",
                "Hex color",
                SkinToneSelection.ToHtml(CurrentSelection().BaseColor),
                "Custom Skin Tone",
                false,
                7,
                new FullScreenMessage.ButtonEntry("Apply", ApplyHexFromDialog, cancel: false, submit: true),
                new FullScreenMessage.ButtonEntry("Cancel", FullScreenMessage.Hide, cancel: true));
        }

        private void ApplyHexFromDialog()
        {
            string input = FullScreenMessage.InputFieldText;
            if (TryParseHex(input, out Color color))
            {
                Color.RGBToHSV(color, out _hue, out _saturation, out _value);
                SkinToneSelection selection = CurrentSelection();
                selection.Enabled = true;
                SkinToneState.SetLocalSelection(selection, persist: true, broadcast: true);
                FullScreenMessage.Hide();
                RefreshUi();
                return;
            }

            FullScreenMessage.ShowErrorMessage("Please enter a valid #RRGGBB color.");
        }

        private void OnWheelPointer(Vector2 localPoint, RectTransform rect)
        {
            // The wheel box is pinned top-left (pivot 0,1), so rect.rect spans [0,w] x [-h,0].
            // Compute radial coords against the rect's visual center instead of assuming a
            // centered pivot, otherwise clicks along the ring always fall outside the distance
            // window and the hue never updates — which makes the wheel look "locked".
            Vector2 offset = localPoint - rect.rect.center;
            Vector2 normalized = new Vector2(offset.x / (rect.rect.width * 0.5f), offset.y / (rect.rect.height * 0.5f));
            float distance = normalized.magnitude;
            if (distance < 0.30f || distance > 0.48f)
            {
                return;
            }

            _hue = Mathf.Repeat(Mathf.Atan2(normalized.y, normalized.x) / (Mathf.PI * 2f), 1f);
            CommitSelectionLive();
        }

        private void OnSvPointer(Vector2 localPoint, RectTransform rect)
        {
            float u = Mathf.InverseLerp(rect.rect.xMin, rect.rect.xMax, localPoint.x);
            float v = Mathf.InverseLerp(rect.rect.yMin, rect.rect.yMax, localPoint.y);
            _saturation = Mathf.Clamp01(u);
            _value = Mathf.Clamp01(v);
            CommitSelectionLive();
        }

        // Live update during a drag: apply the new color to the preview material and refresh UI
        // handles, but do NOT persist to BepInEx config or broadcast over Mirror. Saving the
        // .cfg synchronously and re-emitting a network message every drag tick was the bulk of
        // the wheel stutter. The final state is committed once via OnDragFinished.
        private void CommitSelectionLive()
        {
            SkinToneSelection selection = CurrentSelection();
            if (!SkinToneState.LocalSelection.Enabled)
            {
                selection.Enabled = false;
            }

            SkinToneState.SetLocalSelection(selection, persist: false, broadcast: false);
            RefreshUi();
        }

        // Called once on pointer-up / drag-end. Now we pay the disk-save and network broadcast
        // exactly once for the whole drag, instead of per frame.
        private bool _dragFinalizing;
        private void OnDragFinished()
        {
            // OnPointerUp and OnEndDrag both fire on release; guard so we only commit once.
            if (_dragFinalizing) return;
            _dragFinalizing = true;
            try
            {
                SkinToneSelection selection = CurrentSelection();
                if (!SkinToneState.LocalSelection.Enabled)
                {
                    selection.Enabled = false;
                }
                SkinToneState.SetLocalSelection(selection, persist: true, broadcast: true);
            }
            finally
            {
                _dragFinalizing = false;
            }
        }

        private void RegenerateSvTexture()
        {
            // Skip the GPU upload if hue hasn't moved meaningfully since the last regen. The
            // gradient is purely a function of hue, so SV-square drags and sub-pixel wheel
            // jitters reuse the existing texture. Threshold corresponds to <1° of hue.
            if (!float.IsNaN(_lastRegenHue) && Mathf.Abs(_hue - _lastRegenHue) < 0.003f)
            {
                return;
            }

            int w = _svTexture.width;
            int h = _svTexture.height;
            if (_svPixels == null || _svPixels.Length != w * h)
            {
                _svPixels = new Color32[w * h];
            }

            // Bulk fill via a single SetPixels32/Apply pair — much cheaper than the per-pixel
            // SetPixel managed/native boundary cross we were paying once per drag tick.
            int idx = 0;
            for (int y = 0; y < h; y++)
            {
                float value = (float)y / (h - 1);
                for (int x = 0; x < w; x++)
                {
                    float saturation = (float)x / (w - 1);
                    Color rgb = Color.HSVToRGB(_hue, saturation, value);
                    _svPixels[idx++] = new Color32(
                        (byte)(rgb.r * 255f),
                        (byte)(rgb.g * 255f),
                        (byte)(rgb.b * 255f),
                        255);
                }
            }
            _svTexture.SetPixels32(_svPixels);
            _svTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            _lastRegenHue = _hue;
        }

        private void UpdateHandles()
        {
            // Handle radii scale with the wheel/SV box size so layout changes don't break placement.
            float boxHalf = _wheelHandle.parent is RectTransform parentRect ? parentRect.rect.width * 0.5f : 95f;
            float wheelRadius = boxHalf * 0.78f;
            float svExtent = boxHalf - 4f;
            float wheelAngle = _hue * Mathf.PI * 2f;
            _wheelHandle.anchoredPosition = new Vector2(Mathf.Cos(wheelAngle), Mathf.Sin(wheelAngle)) * wheelRadius;
            _svHandle.anchoredPosition = new Vector2(Mathf.Lerp(-svExtent, svExtent, _saturation), Mathf.Lerp(-svExtent, svExtent, _value));
        }

        private TMP_Text CreateLabel(string text)
        {
            TMP_Text template = _menu.unlocked;
            TMP_Text label = Instantiate(template, transform);
            label.text = text;
            label.gameObject.SetActive(true);
            label.alignment = TextAlignmentOptions.Left;
            return label;
        }

        private TMP_Text CreateToolbarLabel(RectTransform parent, float width, float height)
        {
            TMP_Text label = CreateLabel(string.Empty);
            label.transform.SetParent(parent, false);
            label.fontSize = 18f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Left;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            return label;
        }

        private RawImage CreateRawImage(RectTransform parent, string name)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 8f);
            rect.offsetMax = new Vector2(-8f, -8f);
            return obj.GetComponent<RawImage>();
        }

        private RectTransform CreateHandle(RectTransform parent, float size)
        {
            GameObject obj = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            return rect;
        }

        private Image CreateSwatch(RectTransform parent, float size)
        {
            GameObject obj = new GameObject("PreviewSwatch", typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(size, size);
            return obj.GetComponent<Image>();
        }

        private void AddDragSurface(GameObject target, Action<Vector2, RectTransform> callback)
        {
            ColorDragSurface surface = target.AddComponent<ColorDragSurface>();
            surface.Handler = callback;
            surface.EndHandler = OnDragFinished;
        }

        private ToggleButton CreateToggleButton(RectTransform parent, float width, float height)
        {
            GameObject obj = new GameObject("ToggleButton", typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);

            Image image = obj.GetComponent<Image>();
            image.color = new Color(0.18f, 0.23f, 0.3f, 1f);

            ToggleButton button = obj.AddComponent<ToggleButton>();

            GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObj.transform.SetParent(obj.transform, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 6f);
            textRect.offsetMax = new Vector2(-10f, -6f);
            TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 20f;
            text.color = Color.white;
            button.Initialize(obj.GetComponent<Button>(), text, image);
            return button;
        }

        private void CreateActionButton(RectTransform parent, string label, float width, float height, Action onClick)
        {
            ToggleButton button = CreateToggleButton(parent, width, height);
            button.SetLabel(label);
            button.Clicked += onClick;
        }

        private void CreatePresetButton(RectTransform parent, Color color, float width, float height, Action onClick)
        {
            GameObject obj = new GameObject("PresetButton", typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);

            Image image = obj.GetComponent<Image>();
            image.color = color;

            Button button = obj.GetComponent<Button>();
            button.onClick.AddListener(() => onClick());
        }

        private Texture2D GenerateHueWheelTexture(int size, float innerRadius, float outerRadius)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 normalized = new Vector2((x - half) / half, (y - half) / half);
                    float radius = normalized.magnitude;
                    if (radius < innerRadius || radius > outerRadius)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    float hue = Mathf.Repeat(Mathf.Atan2(normalized.y, normalized.x) / (Mathf.PI * 2f), 1f);
                    Color color = Color.HSVToRGB(hue, 1f, 1f);
                    color.a = 1f;
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return texture;
        }

        private static bool TryParseHex(string input, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string normalized = input.Trim();
            if (!normalized.StartsWith("#", StringComparison.Ordinal))
            {
                normalized = "#" + normalized;
            }

            if (!ColorUtility.TryParseHtmlString(normalized, out color))
            {
                return false;
            }

            color.a = 1f;
            return true;
        }

        private sealed class ColorDragSurface : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IEndDragHandler
        {
            internal Action<Vector2, RectTransform> Handler;
            // Fires once on pointer-up / drag-end. The picker uses this to commit the drag's
            // final color to BepInEx config and the Mirror network exactly once, instead of
            // every drag tick — which is the bulk of the wheel's stutter.
            internal Action EndHandler;

            public void OnPointerDown(PointerEventData eventData)
            {
                Forward(eventData);
            }

            public void OnDrag(PointerEventData eventData)
            {
                Forward(eventData);
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                EndHandler?.Invoke();
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                EndHandler?.Invoke();
            }

            private void Forward(PointerEventData eventData)
            {
                RectTransform rect = transform as RectTransform;
                if (rect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
                {
                    Handler?.Invoke(localPoint, rect);
                }
            }
        }

        private sealed class ToggleButton : MonoBehaviour
        {
            private Button _button;
            private TMP_Text _label;
            private Image _background;

            internal event Action Clicked;

            internal void Initialize(Button button, TMP_Text label, Image background)
            {
                _button = button;
                _label = label;
                _background = background;
                _button.onClick.AddListener(() => Clicked?.Invoke());
            }

            internal void SetLabel(string label)
            {
                _label.text = label;
            }

            internal void SetState(bool enabled)
            {
                _background.color = enabled
                    ? new Color(0.24f, 0.41f, 0.29f, 1f)
                    : new Color(0.18f, 0.23f, 0.3f, 1f);
            }
        }
    }
}
