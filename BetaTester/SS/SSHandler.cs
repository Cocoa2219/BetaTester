using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using PluginAPI.Core;
using TMPro;
using UnityEngine;
using UserSettings.ServerSpecific;

namespace BetaTester.SS
{
    public static class SSHandler
    {
        private static SSPage _currentPage;

        public static void Initialize()
        {
            var page = new SSPage(
                new SSPageBuilder()
                    .AddGroupHeader("BetaTester Settings")
                    .AddButton("BetaTester", "Test", hint: "Test button", onInteract: player => player.SendBroadcast("Test", 5))
                );

            _currentPage = page;
        }

        public static void Dispose()
        {
            _currentPage?.Dispose();
        }

        public static void SendPage(Player player)
        {
            _currentPage.Send(player);
        }
    }

    /// <summary>
    /// Represents a full page of the Server-Specific settings system.
    /// 서버 설정 시스템의 전체 페이지를 나타냅니다.
    /// </summary>
    public class SSPage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SSPage"/> class using a builder.
        /// <br/>Builder를 사용하여 <see cref="SSPage"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        /// <param name="builder">The builder that defines the settings for the page.<br/>페이지의 설정을 정의하는 빌더입니다.</param>
        public SSPage(SSPageBuilder builder)
        {
            Entries = builder.Elements;
            ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnUserInputReceived;
        }

        /// <summary>
        /// Triggered when a user interacts with a setting.<br/>
        /// 사용자가 설정과 상호 작용할 때 호출됩니다.
        /// </summary>
        public Action<Player, SSElement> OnUserInput { get; set; }

        /// <summary>
        /// The list of entries on the page.<br/>
        /// 페이지의 항목 목록입니다.
        /// </summary>
        public Dictionary<int, SSElement> Entries { get; }

        /// <summary>
        /// Sends the page settings to a specific player.<br/>
        /// 특정 플레이어에게 페이지 설정을 전송합니다.
        /// </summary>
        /// <param name="player">The player to receive the settings.<br/>설정을 받을 플레이어입니다.</param>
        public void Send(Player player) => Send(player.ReferenceHub);

        /// <summary>
        /// Sends the page settings to a specific reference hub.<br/>
        /// 특정 ReferenceHub에 페이지 설정을 전송합니다.
        /// </summary>
        /// <param name="hub">The reference hub to receive the settings.<br/>설정을 받을 ReferenceHub입니다.</param>
        public void Send(ReferenceHub hub)
        {
            if (!NetworkServer.active) return;

            ServerSpecificSettingsSync.DefinedSettings = Entries.Select(x => x.Value.Base).ToArray();
            ServerSpecificSettingsSync.SendToPlayer(hub);
        }

        private void OnUserInputReceived(ReferenceHub player, ServerSpecificSettingBase entry)
        {
            if (entry == null)
            {
                Log.Error("Received null entry.");
                return;
            }

            if (!Entries.TryGetValue(entry.SettingId, out var element))
            {
                Log.Error($"No element found for {entry.Label} ({entry.SettingId})");
                return;
            }

            OnUserInput?.Invoke(Player.Get(player), element);

            if (element == null)
            {
                Log.Error($"No element found for {entry.Label} ({entry.SettingId})");
                return;
            }

            switch (element)
            {
                case SSButtonElement buttonElement:
                    buttonElement.OnInteract?.Invoke(Player.Get(player));
                    break;
                case SSDropdownElement dropdownElement:
                    dropdownElement.OnChanged?.Invoke(Player.Get(player));
                    break;
                case SSKeybindElement keybindElement:
                    keybindElement.OnInput?.Invoke(Player.Get(player));
                    break;
                case SSPlainTextElement plainTextElement:
                    plainTextElement.OnChanged?.Invoke(Player.Get(player));
                    break;
                case SSSliderElement sliderElement:
                    sliderElement.OnChanged?.Invoke(Player.Get(player));
                    break;
                default:
                    Log.Error($"No action defined for {element.Type}");
                    break;
            }
        }

        /// <summary>
        /// Disposes of the page and unsubscribes from event handlers.<br/>
        /// 페이지를 삭제하고 이벤트 핸들러를 구독 취소합니다.
        /// </summary>
        public void Dispose()
        {
            ServerSpecificSettingsSync.ServerOnSettingValueReceived -= OnUserInputReceived;
        }
    }

    /// <summary>
    /// Builder class for creating a page of server-specific settings.<br/>
    /// 서버 설정 페이지를 생성하기 위한 빌더 클래스입니다.
    /// </summary>
    public class SSPageBuilder
    {
        /// <summary>
        /// The list of entries to be added to the settings page.<br/>
        /// 설정 페이지에 추가될 항목 목록입니다.
        /// </summary>
        public Dictionary<int, SSElement> Elements { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SSPageBuilder"/> class.<br/>
        /// <see cref="SSPageBuilder"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        public SSPageBuilder()
        {
            Elements = new Dictionary<int, SSElement>();
            _id = 0;
        }

        private int _id;

        /// <summary>
        /// Adds a button setting to the page.<br/>
        /// 페이지에 버튼 설정을 추가합니다.
        /// </summary>
        /// <returns>The builder instance.<br/>빌더 인스턴스를 반환합니다.</returns>
        public SSPageBuilder AddButton(string label, string buttonText, float? holdTime = null, string hint = null,
            Action<Player> onInteract = null)
        {
            var button = new SSButtonElement
                { Base = new SSButton(++_id, label, buttonText, holdTime, hint), OnInteract = onInteract };
            Elements.Add(_id, button);
            return this;
        }

        /// <summary>
        /// Adds a dropdown setting to the page.<br/>
        /// 페이지에 드롭다운 설정을 추가합니다.
        /// </summary>
        /// <param name="label">The label of the dropdown.<br/>드롭다운의 레이블입니다.</param>
        /// <param name="options">The list of options for the dropdown.<br/>드롭다운 옵션 목록입니다.</param>
        /// <param name="defaultIndex">The default selected index.<br/>기본 선택된 인덱스입니다.</param>
        /// <param name="entryType">The type of the dropdown entry.<br/>드롭다운 항목의 타입입니다.</param>
        /// <param name="hint">The hint of the dropdown.<br/>드롭다운의 힌트입니다.</param>
        /// <param name="onChanged">The action to execute when the dropdown value changes.<br/>드롭다운 값이 변경될 때 실행할 작업입니다.</param>
        /// <returns>The builder instance.<br/>빌더 인스턴스를 반환합니다.</returns>
        public SSPageBuilder AddDropdown(string label, string[] options, int defaultIndex = 0,
            SSDropdownSetting.DropdownEntryType entryType = SSDropdownSetting.DropdownEntryType.Regular,
            string hint = null, Action<Player> onChanged = null)
        {
            var dropdown = new SSDropdownElement
            {
                Base = new SSDropdownSetting(++_id, label, options, defaultIndex, entryType, hint),
                OnChanged = onChanged
            };
            Elements.Add(_id, dropdown);
            return this;
        }

        /// <summary>
        /// Adds a group header to the page.<br/>
        /// 페이지에 그룹 헤더를 추가합니다.
        /// </summary>
        /// <param name="label">The label of the group header.<br/>그룹 헤더의 레이블입니다.</param>
        /// <param name="reducedPadding">Whether the group header should have reduced padding.<br/>그룹 헤더에 줄어든 패딩이 있는지 여부입니다.</param>
        /// <param name="hint">The hint of the group header.<br/>그룹 헤더의 힌트입니다.</param>
        /// <returns>The builder instance.<br/>빌더 인스턴스를 반환합니다.</returns>
        public SSPageBuilder AddGroupHeader(string label, bool reducedPadding = false, string hint = null)
        {
            var header = new SSGroupHeaderElement { Base = new SSGroupHeader(label, reducedPadding, hint) };
            Elements.Add(_id++, header);
            return this;
        }

        /// <summary>
        /// Adds a keybind setting to the page.<br/>
        /// 페이지에 키바인드 설정을 추가합니다.
        /// </summary>
        /// <param name="label">키바인드의 레이블입니다.</param>
        /// <param name="suggestedKey">키바인드의 제안된 키입니다.</param>
        /// <param name="preventInteractionOnGUI">GUI에서 상호작용을 방지할지 여부입니다.</param>
        /// <param name="hint">키바인드의 힌트입니다.</param>
        /// <param name="onInput">키바인드 입력 시 실행할 작업입니다.</param>
        /// <returns></returns>
        public SSPageBuilder AddKeybind(string label, KeyCode suggestedKey = KeyCode.None,
            bool preventInteractionOnGUI = true, string hint = null, Action<Player> onInput = null)
        {
            var keybind = new SSKeybindElement
            {
                Base = new SSKeybindSetting(++_id, label, suggestedKey, preventInteractionOnGUI, hint),
                OnInput = onInput
            };
            Elements.Add(_id, keybind);
            return this;
        }

        public SSPageBuilder AddPlainText(string label, string placeholder = "...", int characterLimit = 64, TMP_InputField.ContentType contentType = TMP_InputField.ContentType.Standard, string hint = null, Action<Player> onChanged = null)
        {
            var plainText = new SSPlainTextElement
            {
                Base = new SSPlaintextSetting(++_id, label, placeholder, characterLimit, contentType, hint),
                OnChanged = onChanged
            };
            Elements.Add(_id, plainText);
            return this;
        }

        /// <summary>
        /// Adds a slider setting to the page.<br/>
        /// 페이지에 슬라이더 설정을 추가합니다.
        /// </summary>
        /// <param name="label">The label of the slider.<br/>슬라이더의 레이블입니다.</param>
        /// <param name="minValue">The minimum value of the slider.<br/>슬라이더의 최소값입니다.</param>
        /// <param name="maxValue">The maximum value of the slider.<br/>슬라이더의 최대값입니다.</param>
        /// <param name="defaultValue">The default value of the slider.<br/>슬라이더의 기본값입니다.</param>
        /// <param name="integer">Whether the slider should only accept integer values.<br/>슬라이더가 정수 값만 허용해야 하는지 여부입니다.</param>
        /// <param name="valueToStringFormat">The format of the slider value when converted to a string.<br/>슬라이더 값이 문자열로 변환될 때의 형식입니다.</param>
        /// <param name="finalDisplayFormat">The final display format of the slider value.<br/>슬라이더 값의 최종 표시 형식입니다.</param>
        /// <param name="hint">The hint of the slider.<br/>슬라이더의 힌트입니다.</param>
        /// <param name="onChanged">The action to execute when the slider value changes.<br/>슬라이더 값이 변경될 때 실행할 작업입니다.</param>
        /// <returns>The builder instance.<br/>빌더 인스턴스를 반환합니다.</returns>
        public SSPageBuilder AddSlider(string label, float minValue, float maxValue, float defaultValue = 0f,
            bool integer = false, string valueToStringFormat = "0.##", string finalDisplayFormat = "{0}", string hint = null,
            Action<Player> onChanged = null)
        {
            var slider = new SSSliderElement
            {
                Base = new SSSliderSetting(++_id, label, minValue, maxValue, defaultValue, integer, valueToStringFormat,
                    finalDisplayFormat, hint),
                OnChanged = onChanged
            };
            Elements.Add(_id, slider);
            return this;
        }

        /// <summary>
        /// Adds a text area setting to the page.<br/>
        /// 페이지에 텍스트 영역 설정을 추가합니다.
        /// </summary>
        /// <param name="content">The content of the text area.<br/>텍스트 영역의 내용입니다.</param>
        /// <param name="foldoutMode">The foldout mode of the text area.<br/>텍스트 영역의 접힘 모드입니다.</param>
        /// <param name="collpasedText">The text to display when the text area is collapsed.<br/>텍스트 영역이 접혔을 때 표시할 텍스트입니다.</param>
        /// <param name="textAlignment">The text alignment of the text area.<br/>텍스트 영역의 텍스트 정렬입니다.</param>
        /// <returns>The builder instance.<br/>빌더 인스턴스를 반환합니다.</returns>
        public SSPageBuilder AddTextArea(string content, SSTextArea.FoldoutMode foldoutMode = SSTextArea.FoldoutMode.NotCollapsable, string collpasedText = null, TextAlignmentOptions textAlignment = TextAlignmentOptions.Center)
        {
            var textArea = new SSTextAreaElement
            {
                Base = new SSTextArea(++_id, content, foldoutMode, collpasedText, textAlignment)
            };
            Elements.Add(_id, textArea);
            return this;
        }

        /// <summary>
        /// Adds a two-button setting to the page.<br/>
        /// 페이지에 두 개의 버튼 설정을 추가합니다.
        /// </summary>
        /// <param name="label">The label of the two buttons.<br/>두 개의 버튼의 레이블입니다.</param>
        /// <param name="optionA">The text of the first button.<br/>첫 번째 버튼의 텍스트입니다.</param>
        /// <param name="optionB">The text of the second button.<br/>두 번째 버튼의 텍스트입니다.</param>
        /// <param name="defaultIsB">Whether the default selected button is the second one.<br/>기본 선택된 버튼이 두 번째 버튼인지 여부입니다.</param>
        /// <param name="hint">The hint of the two buttons.<br/>두 개의 버튼의 힌트입니다.</param>
        /// <returns>The builder instance.<br/>빌더 인스턴스를 반환합니다.</returns>
        public SSPageBuilder AddTwoButtons(string label, string optionA, string optionB, bool defaultIsB = false, string hint = null)
        {
            var twoButtons = new SSTwoButtonElement
            {
                Base = new SSTwoButtonsSetting(++_id, label, optionA, optionB, defaultIsB, hint),
            };
            Elements.Add(_id, twoButtons);
            return this;
        }
    }

    /// <summary>
    /// Represents a single entry of the Server-Specific settings system.<br/>
    /// 서버 설정 시스템의 단일 항목을 나타냅니다.
    /// </summary>
    public abstract class SSElement
    {
        /// <summary>
        /// The base of the entry.<br/>
        /// 항목의 베이스 클래스입니다.
        /// </summary>
        public ServerSpecificSettingBase Base { get; set; }

        /// <summary>
        /// The type of the entry.<br/>
        /// 항목의 타입입니다.
        /// </summary>
        public abstract SSElementType Type { get; set; }

        /// <summary>
        /// The label of the entry.<br/>
        /// 항목의 이름입니다.
        /// </summary>
        public string Label
        {
            get => Base.Label;
            set => Base.Label = value;
        }

        /// <summary>
        /// The hint of the entry.<br/>
        /// 항목의 힌트입니다.
        /// </summary>
        public string Hint
        {
            get => Base.HintDescription;
            set => Base.HintDescription = value;
        }
    }

    public class SSButtonElement : SSElement
    {
        /// <inheritdoc />
        public override SSElementType Type { get; set; } = SSElementType.Button;

        /// <summary>
        /// Triggered when the button is interacted with.<br/>
        /// 버튼이 상호작용 (클릭 및 홀드) 되었을 때 호출됩니다.
        /// </summary>
        public Action<Player> OnInteract { get; set; }

        /// <summary>
        /// The text of the button.<br/>
        /// 버튼의 텍스트입니다.
        /// </summary>
        public string ButtonText
        {
            get => ((SSButton)Base).ButtonText;
            set => ((SSButton)Base).ButtonText = value;
        }

        /// <summary>
        /// The hold time of the button.<br/>
        /// 버튼의 홀드 시간입니다.
        /// </summary>
        public float HoldTime
        {
            get => ((SSButton)Base).HoldTimeSeconds;
            set => ((SSButton)Base).HoldTimeSeconds = value;
        }
    }

    public class SSDropdownElement : SSElement
    {
        /// <inheritdoc />
        public override SSElementType Type { get; set; } = SSElementType.Dropdown;

        /// <summary>
        /// Triggered when the dropdown value is changed.<br/>
        /// 드롭다운 값이 변경되었을 때 호출됩니다.
        /// </summary>
        public Action<Player> OnChanged { get; set; }

        /// <summary>
        /// The default index of the dropdown.<br/>
        /// 드롭다운의 기본 인덱스입니다.
        /// </summary>
        public int DefaultIndex
        {
            get => ((SSDropdownSetting)Base).DefaultOptionIndex;
            set => ((SSDropdownSetting)Base).DefaultOptionIndex = value;
        }

        /// <summary>
        /// The type of the dropdown entry.<br/>
        /// 드롭다운 항목의 타입입니다.
        /// </summary>
        public SSDropdownSetting.DropdownEntryType EntryType
        {
            get => ((SSDropdownSetting)Base).EntryType;
            set => ((SSDropdownSetting)Base).EntryType = value;
        }

        /// <summary>
        /// The list of options for the dropdown.<br/>
        /// 드롭다운의 옵션 목록입니다.
        /// </summary>
        public string[] Options
        {
            get => ((SSDropdownSetting)Base).Options;
            set => ((SSDropdownSetting)Base).Options = value;
        }
    }

    public class SSGroupHeaderElement : SSElement
    {
        /// <inheritdoc />
        public override SSElementType Type { get; set; } = SSElementType.GroupHeader;

        /// <summary>
        /// Whether the group header should have reduced padding.<br/>
        /// 그룹 헤더에 줄어든 패딩이 있는지 여부입니다.
        /// </summary>
        public bool ReducedPadding
        {
            get => ((SSGroupHeader)Base).ReducedPadding;
            set => ((SSGroupHeader)Base).ReducedPadding = value;
        }
    }

    public class SSKeybindElement : SSElement
    {
        /// <inheritdoc />
        public override SSElementType Type { get; set; } = SSElementType.Keybind;

        /// <summary>
        /// Triggered when the keybind is interacted with.<br/>
        /// 키바인드가 상호작용되었을 때 호출됩니다.
        /// </summary>
        public Action<Player> OnInput { get; set; }

        /// <summary>
        /// The suggested key of the keybind.<br/>
        /// 키바인드의 추천 키입니다.
        /// </summary>
        public KeyCode SuggestedKey
        {
            get => ((SSKeybindSetting)Base).SuggestedKey;
            set => ((SSKeybindSetting)Base).SuggestedKey = value;
        }

        /// <summary>
        /// Whether the keybind should prevent interaction on GUI.<br/>
        /// 키바인드가 GUI에서 상호작용을 방지해야 하는지 여부입니다.
        /// </summary>
        public bool PreventInteractionOnGUI
        {
            get => ((SSKeybindSetting)Base).PreventInteractionOnGUI;
            set => ((SSKeybindSetting)Base).PreventInteractionOnGUI = value;
        }
    }

    public class SSPlainTextElement : SSElement
    {
        /// <inheritdoc />
        public override SSElementType Type { get; set; } = SSElementType.PlainText;

        /// <summary>
        /// Triggered when the text of the plain text input is changed.<br/>
        /// 평문 입력의 텍스트가 변경되었을 때 호출됩니다.
        /// </summary>
        public Action<Player> OnChanged { get; set; }

        /// <summary>
        /// The text of the plain text input.<br/>
        /// 평문 입력의 텍스트입니다.
        /// </summary>
        public string Text
        {
            get => ((SSPlaintextSetting)Base).SyncInputText;
            set => ((SSPlaintextSetting)Base).SyncInputText = value;
        }

        /// <summary>
        /// The placeholder of the plain text input.<br/>
        /// 평문 입력의 플레이스홀더입니다.
        /// </summary>
        public string Placeholder
        {
            get => ((SSPlaintextSetting)Base).Placeholder;
            set => ((SSPlaintextSetting)Base).Placeholder = value;
        }

        /// <summary>
        /// The content type of the plaintext input (e.g., standard, alphanumeric, email, etc.).<br/>
        /// 플레인 텍스트 입력의 콘텐츠 유형입니다 (예: 표준, 영숫자, 이메일 등).
        /// </summary>
        public TMP_InputField.ContentType ContentType
        {
            get => ((SSPlaintextSetting)Base).ContentType;
            set => ((SSPlaintextSetting)Base).ContentType = value;
        }

        /// <summary>
        /// The maximum number of characters allowed in the plaintext input field.<br/>
        /// 플레인 텍스트 입력 필드에서 허용되는 최대 문자 수입니다.
        /// </summary>
        public int CharacterLimit
        {
            get => ((SSPlaintextSetting)Base).CharacterLimit;
            set => ((SSPlaintextSetting)Base).CharacterLimit = value;
        }
    }

    public class SSSliderElement : SSElement
    {
        /// <inheritdoc />
        public override SSElementType Type { get; set; } = SSElementType.Slider;

        /// <summary>
        /// Triggered when the slider value changes.<br/>
        /// 슬라이더 값이 변경될 때 호출됩니다.
        /// </summary>
        public Action<Player> OnChanged { get; set; }

        /// <summary>
        /// The minimum value of the slider.<br/>
        /// 슬라이더의 최소값입니다.
        /// </summary>
        public float MinValue
        {
            get => ((SSSliderSetting)Base).MinValue;
            set => ((SSSliderSetting)Base).MinValue = value;
        }

        /// <summary>
        /// The maximum value of the slider.<br/>
        /// 슬라이더의 최대값입니다.
        /// </summary>
        public float MaxValue
        {
            get => ((SSSliderSetting)Base).MaxValue;
            set => ((SSSliderSetting)Base).MaxValue = value;
        }

        /// <summary>
        /// The default value of the slider.<br/>
        /// 슬라이더의 기본값입니다.
        /// </summary>
        public float DefaultValue
        {
            get => ((SSSliderSetting)Base).DefaultValue;
            set => ((SSSliderSetting)Base).DefaultValue = value;
        }

        /// <summary>
        /// Indicates whether the slider is being dragged.<br/>
        /// 슬라이더가 드래그 중인지 여부를 나타냅니다.
        /// </summary>
        public bool IsDragging
        {
            get => ((SSSliderSetting)Base).SyncDragging;
            set => ((SSSliderSetting)Base).SyncDragging = value;
        }

        /// <summary>
        /// Indicates whether the slider only accepts integer values.<br/>
        /// 슬라이더가 정수 값만 허용하는지 여부를 나타냅니다.
        /// </summary>
        public bool IsInteger
        {
            get => ((SSSliderSetting)Base).Integer;
            set => ((SSSliderSetting)Base).Integer = value;
        }

        /// <summary>
        /// The current value of the slider.<br/>
        /// 슬라이더의 현재 값입니다.
        /// </summary>
        public float Value
        {
            get => ((SSSliderSetting)Base).SyncFloatValue;
            set => ((SSSliderSetting)Base).SyncFloatValue = value;
        }

        /// <summary>
        /// The format used to display the slider value as a string.<br/>
        /// 슬라이더 값을 문자열로 표시하는 데 사용되는 형식입니다.
        /// </summary>
        public string ValueToStringFormat
        {
            get => ((SSSliderSetting)Base).ValueToStringFormat;
            set => ((SSSliderSetting)Base).ValueToStringFormat = value;
        }

        /// <summary>
        /// The final display format of the slider value.<br/>
        /// 슬라이더 값의 최종 표시 형식입니다.
        /// </summary>
        public string FinalDisplayFormat
        {
            get => ((SSSliderSetting)Base).FinalDisplayFormat;
            set => ((SSSliderSetting)Base).FinalDisplayFormat = value;
        }
    }

    public class SSTextAreaElement : SSElement
    {
        /// <inheritdoc />
        public override SSElementType Type { get; set; } = SSElementType.TextArea;

        /// <summary>
        /// The foldout mode of the text area (e.g., collapsible or not).<br/>
        /// 텍스트 영역의 펼침 모드 (예: 접기 가능 또는 비접기 가능).
        /// </summary>
        public SSTextArea.FoldoutMode FoldoutMode
        {
            get => ((SSTextArea)Base).Foldout;
            set => ((SSTextArea)Base).Foldout = value;
        }

        /// <summary>
        /// The alignment of the text within the text area.<br/>
        /// 텍스트 영역 내 텍스트의 정렬입니다.
        /// </summary>
        public TextAlignmentOptions TextAlignment
        {
            get => ((SSTextArea)Base).AlignmentOptions;
            set => ((SSTextArea)Base).AlignmentOptions = value;
        }
    }

    public class SSTwoButtonElement : SSElement
    {
        /// <inheritdoc />
        public override SSElementType Type { get; set; } = SSElementType.TwoButtons;

        /// <summary>
        /// Indicates whether the second button (option B) is selected.<br/>
        /// 두 번째 버튼(option B)이 선택되었는지 여부를 나타냅니다.
        /// </summary>
        public bool IsB
        {
            get => ((SSTwoButtonsSetting)Base).SyncIsB;
            set => ((SSTwoButtonsSetting)Base).SyncIsB = value;
        }

        /// <summary>
        /// The label for the first button (option A).<br/>
        /// 첫 번째 버튼(option A)의 레이블입니다.
        /// </summary>
        public string OptionA
        {
            get => ((SSTwoButtonsSetting)Base).OptionA;
            set => ((SSTwoButtonsSetting)Base).OptionA = value;
        }

        /// <summary>
        /// The label for the second button (option B).<br/>
        /// 두 번째 버튼(option B)의 레이블입니다.
        /// </summary>
        public string OptionB
        {
            get => ((SSTwoButtonsSetting)Base).OptionB;
            set => ((SSTwoButtonsSetting)Base).OptionB = value;
        }

        /// <summary>
        /// Indicates whether the second button (option B) is the default selection.<br/>
        /// 두 번째 버튼(option B)이 기본 선택인지 여부를 나타냅니다.
        /// </summary>
        public bool DefaultIsB
        {
            get => ((SSTwoButtonsSetting)Base).DefaultIsB;
            set => ((SSTwoButtonsSetting)Base).DefaultIsB = value;
        }
    }

    /// <summary>
    /// Represents the type of the Server-Specific settings entry.<br/>
    /// 서버 설정 항목의 타입을 나타냅니다.
    /// </summary>
    public enum SSElementType
    {
        Button,
        Dropdown,
        GroupHeader,
        Keybind,
        PlainText,
        Slider,
        TextArea,
        TwoButtons,
    }
}