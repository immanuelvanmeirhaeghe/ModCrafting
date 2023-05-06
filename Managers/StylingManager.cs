using System;
using UnityEngine;

namespace ModCrafting.Managers
{
    /// <summary>
    /// Manager for styles used from ModAPI skin.
    /// </summary>
    public class StylingManager : MonoBehaviour
    {
        private static StylingManager Instance;
        private static readonly string ModuleName = nameof(StylingManager);

        public bool IsModEnabled { get; set; } = true;

        public StylingManager()
        {
            useGUILayout = true;
            Instance = this;
        }

        public static StylingManager Get() => Instance;

        protected virtual void Awake()
        {
            Instance = this;
        }

        protected virtual void OnDestroy()
        {
            Instance = null;
        }

        protected virtual void Start()
        {
            InitData();
        }

        protected virtual void Update()
        {
            if (IsModEnabled)
            {
                InitData();
            }
        }

        protected virtual void InitData()
        { }

        protected virtual void HandleException(Exception exc, string methodName)
        {
            string info = $"[{ModuleName}:{methodName}] throws exception -  {exc.TargetSite?.Name}:\n{exc.Message}\n{exc.InnerException}\n{exc.Source}\n{exc.StackTrace}";
            ModAPI.Log.Write(info);
            Debug.Log(info);
        }

        public int DefaultFontSize { get; set; } = 12;
        public Color DefaultEnabledColor
            => Color.green;
        public Color DefaultHeaderColor
            => Color.yellow;
        public Color DefaultAttentionColor
            => Color.yellow;
        public Color DefaultColor
            => GUI.color;
        public Color DefaultHighlightColor
           => Color.cyan;
        public Color DefaultContentColor
            => GUI.contentColor;
        public Color DefaultBackGroundColor
            => GUI.backgroundColor;
        public Color ClearBackgroundColor
            => Color.clear;

        public GUIStyle NormalButton => new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            font = GUI.skin.button.font,
            fontStyle = FontStyle.Normal,
            fontSize = GUI.skin.button.fontSize,
            border = GUI.skin.button.border,
            clipping = GUI.skin.button.clipping,
            margin = GUI.skin.button.margin,
            padding = GUI.skin.button.padding,
            contentOffset = GUI.skin.button.contentOffset,
            normal = GUI.skin.button.normal,
            active = GUI.skin.button.active,
            hover = GUI.skin.button.hover,
            stretchWidth = true
        };
        public GUIStyle ToggleButton => new GUIStyle(NormalButton)
        {
            normal = new GUIStyleState
            {
                background = GUI.skin.button.normal.background,
                textColor = DefaultColor
            },
            active = new GUIStyleState
            {
                background = GUI.skin.button.active.background,
                textColor = DefaultEnabledColor
            },
            hover = new GUIStyleState
            {
                background = GUI.skin.button.hover.background,
                textColor = DefaultHighlightColor
            },
            focused = new GUIStyleState
            {
                background = GUI.skin.button.normal.background,
                textColor = DefaultColor
            },
            onNormal = new GUIStyleState
            {
                background = GUI.skin.button.normal.background,
                textColor = DefaultColor
            },
            onActive = new GUIStyleState
            {
                background = GUI.skin.button.active.background,
                textColor = DefaultEnabledColor
            },
            onHover = new GUIStyleState
            {
                background = GUI.skin.button.hover.background,
                textColor = DefaultHighlightColor
            },
            onFocused = new GUIStyleState
            {
                background = GUI.skin.button.normal.background,
                textColor = DefaultColor
            }
        };
        public GUIStyle SelectedGridButton => new GUIStyle(NormalButton)
        {
            normal = new GUIStyleState
            {
                background = GUI.skin.button.normal.background,
                textColor = DefaultColor
            },
            active = new GUIStyleState
            {
                background = GUI.skin.button.active.background,
                textColor = DefaultEnabledColor
            },
            hover = new GUIStyleState
            {
                background = GUI.skin.button.hover.background,
                textColor = DefaultHighlightColor
            },
            focused = new GUIStyleState
            {
                background = GUI.skin.button.normal.background,
                textColor = DefaultColor
            },
            onNormal = new GUIStyleState
            {
                background = GUI.skin.button.normal.background,
                textColor = DefaultColor
            },
            onActive = new GUIStyleState
            {
                background = GUI.skin.button.active.background,
                textColor = DefaultEnabledColor
            },
            onHover = new GUIStyleState
            {
                background = GUI.skin.button.hover.background,
                textColor = DefaultHighlightColor
            },
            onFocused = new GUIStyleState
            {
                background = GUI.skin.button.normal.background,
                textColor = DefaultColor
            }
        };
        public GUIStyle NormalBox => new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            font = GUI.skin.box.font,
            fontStyle = FontStyle.Normal,
            fontSize = GUI.skin.box.fontSize,
            border = GUI.skin.box.border,
            clipping = GUI.skin.box.clipping,
            margin = GUI.skin.box.margin,
            padding = GUI.skin.box.padding,
            contentOffset = GUI.skin.box.contentOffset,
            normal = GUI.skin.box.normal,
            stretchWidth = true,
            stretchHeight = true,
            wordWrap = true
        };
        public GUIStyle NormalLabel => new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            font = GUI.skin.label.font,
            fontStyle = FontStyle.Normal,
            fontSize = DefaultFontSize,
            stretchWidth = true,
            wordWrap = true
        };
        public GUIStyle TimeLabel => new GUIStyle(NormalLabel)
        {
            fontStyle = FontStyle.Bold
        };
        public GUIStyle HeaderLabel => new GUIStyle(NormalLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = NormalLabel.fontSize + 4
        };
        public GUIStyle SubHeaderLabel => new GUIStyle(HeaderLabel)
        {
            fontSize = HeaderLabel.fontSize - 2
        };
        public GUIStyle FormFieldNameLabel => new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = DefaultFontSize,
            stretchWidth = true,
            wordWrap = true
        };
        public GUIStyle FormFieldValueLabel => new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleRight,
            fontSize = DefaultFontSize,
            stretchWidth = true,
            wordWrap = true
        };
        public GUIStyle FormInputTextField => new GUIStyle(GUI.skin.textField)
        {
            alignment = TextAnchor.MiddleRight,
            fontSize = DefaultFontSize,           
            wordWrap = true
        };
        public GUIStyle CommentLabel => new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Italic,
            fontSize = DefaultFontSize,
            stretchWidth = true,
            wordWrap = true
        };
        public GUIStyle TextLabel => new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = DefaultFontSize,
            stretchWidth = true,
            wordWrap = true
        };

        public GUIStyle ColoredSelectedGridButton(bool isSelectedIndex)
        {
            GUIStyle style = SelectedGridButton;
            style.normal.textColor = isSelectedIndex ? DefaultHighlightColor : DefaultColor;
            style.onNormal.textColor = isSelectedIndex ? DefaultHighlightColor : DefaultColor;
            style.active.textColor = isSelectedIndex ? DefaultHighlightColor : DefaultColor;
            style.onActive.textColor = isSelectedIndex ? DefaultHighlightColor : DefaultColor;
            style.hover.textColor = isSelectedIndex ? DefaultHighlightColor : DefaultColor;
            style.onHover.textColor = isSelectedIndex ? DefaultHighlightColor : DefaultColor;
            style.focused.textColor = isSelectedIndex ? DefaultHighlightColor : DefaultColor;
            style.onFocused.textColor = isSelectedIndex ? DefaultHighlightColor : DefaultColor;
            return style;
        }
        public GUIStyle ColoredToggleValueTextLabel(bool enabled, Color enabledColor, Color disabledColor)
        {
            GUIStyle style = TextLabel;
            style.normal.textColor = enabled ? enabledColor : disabledColor;
            style.onNormal.textColor = enabled ? enabledColor : disabledColor;
            style.active.textColor = enabled ? enabledColor : disabledColor;
            style.onActive.textColor = enabled ? enabledColor : disabledColor;
            style.hover.textColor = enabled ? enabledColor : disabledColor;
            style.onHover.textColor = enabled ? enabledColor : disabledColor;
            style.focused.textColor = enabled ? enabledColor : disabledColor;
            style.onFocused.textColor = enabled ? enabledColor : disabledColor;
            return style;
        }
        public GUIStyle ColoredToggleButton(bool activated)
        {
            GUIStyle style = ToggleButton;
            style.normal.textColor = activated ? DefaultHighlightColor : DefaultColor;
            style.onNormal.textColor = activated ? DefaultHighlightColor : DefaultColor;
            style.active.textColor = activated ? DefaultHighlightColor : DefaultColor;
            style.onActive.textColor = activated ? DefaultHighlightColor : DefaultColor;
            style.hover.textColor = activated ? DefaultHighlightColor : DefaultColor;
            style.onHover.textColor = activated ? DefaultHighlightColor : DefaultColor;
            style.focused.textColor = activated ? DefaultHighlightColor : DefaultColor;
            style.onFocused.textColor = activated ? DefaultHighlightColor : DefaultColor;
            return style;
        }
        public GUIStyle ColoredToggleButton(bool activated, Color enabledColor, Color disabledColor)
        {
            GUIStyle style = ToggleButton;
            style.normal.textColor = activated ? enabledColor : disabledColor;
            style.onNormal.textColor = activated ? enabledColor : disabledColor;
            style.active.textColor = activated ? enabledColor : disabledColor;
            style.onActive.textColor = activated ? enabledColor : disabledColor;
            style.hover.textColor = activated ? enabledColor : disabledColor;
            style.onHover.textColor = activated ? enabledColor : disabledColor;
            style.focused.textColor = activated ? enabledColor : disabledColor;
            style.onFocused.textColor = activated ? enabledColor : disabledColor;
            return style;
        }
        public GUIStyle ColoredCommentLabel(Color color)
        {
            GUIStyle style = CommentLabel;
            style.normal.textColor = color;
            return style;
        }
        public GUIStyle ColoredFieldNameLabel(Color color)
        {
            GUIStyle style = FormFieldNameLabel;
            style.normal.textColor = color;
            return style;
        }
        public GUIStyle ColoredFieldValueLabel(Color color)
        {
            GUIStyle style = FormFieldValueLabel;
            style.normal.textColor = color;
            return style;
        }
        public GUIStyle ColoredToggleFieldValueLabel(bool enabled, Color enabledColor, Color disabledColor)
        {
            GUIStyle style = FormFieldValueLabel;
            style.normal.textColor = enabled ? enabledColor : disabledColor;
            style.onNormal.textColor = enabled ? enabledColor : disabledColor;
            style.active.textColor = enabled ? enabledColor : disabledColor;
            style.onActive.textColor = enabled ? enabledColor : disabledColor;
            style.hover.textColor = enabled ? enabledColor : disabledColor;
            style.onHover.textColor = enabled ? enabledColor : disabledColor;
            style.focused.textColor = enabled ? enabledColor : disabledColor;
            style.onFocused.textColor = enabled ? enabledColor : disabledColor;
            return style;
        }
        public GUIStyle ColoredTimeLabel(Color color)
        {
            GUIStyle style = TimeLabel;
            style.normal.textColor = color;
            return style;
        }
        public GUIStyle ColoredHeaderLabel(Color color)
        {
            GUIStyle style = HeaderLabel;
            style.normal.textColor = color;
            return style;
        }
        public GUIStyle ColoredSubHeaderLabel(Color color)
        {
            GUIStyle style = SubHeaderLabel;
            style.normal.textColor = color;
            return style;
        }

    }

}
