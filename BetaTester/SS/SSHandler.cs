using System;
using System.Collections.Generic;
using System.Linq;
using PluginAPI.Core;
using TMPro;
using UnityEngine;
using UserSettings.ServerSpecific;

namespace BetaTester.SS
{
    public static class SSHandler
    {
        private static SSPageManager _pageManager;

        public static void Initialize()
        {
            _pageManager = new SSPageManager(new SSPageBuilder()
                .AddGroupHeader("User Reporting / 플레이어 신고")
                .AddPlainText("플레이어 검색", "플레이어 이름을 입력하세요.", 256, TMP_InputField.ContentType.Standard,
                    "신고할 플레이어의 이름을 입력하세요.")
                .AddTextArea("의사소통", SSTextArea.FoldoutMode.NotCollapsable, null, TextAlignmentOptions.Left)
                .AddTwoButtons("💬 부적절한 텍스트 채팅", "해당됩니다.", "해당되지 않습니다.", true, "플레이어가 부적절한 텍스트 채팅을 했는지 여부를 선택하세요.")
                .AddTwoButtons("🔊 부적절한 음성 채팅", "해당됩니다.", "해당되지 않습니다.", true, "플레이어가 부적절한 음성 채팅을 했는지 여부를 선택하세요.")
                .AddTwoButtons("😠 공격적인 이름", "해당됩니다.", "해당되지 않습니다.", true, "플레이어가 부적절한 이름을 사용했는지 여부를 선택하세요.")
                .AddTwoButtons("💥 위협", "해당됩니다.", "해당되지 않습니다.", true, "플레이어가 다른 플레이어를 위협했는지 여부를 선택하세요.")
                .AddTwoButtons("💀 비매너 행위", "해당됩니다.", "해당되지 않습니다.", true, "플레이어가 비매너 행위를 했는지 여부를 선택하세요.")
                .AddTextArea("게임플레이 방해", SSTextArea.FoldoutMode.NotCollapsable, null, TextAlignmentOptions.Left)
                .AddTwoButtons("🔧 부정행위", "해당됩니다.", "해당되지 않습니다.", true, "플레이어가 부정행위를 했는지 여부를 선택하세요.")
                .AddTwoButtons("🐞 버그 사용", "해당됩니다.", "해당되지 않습니다.", true, "플레이어가 버그를 사용했는지 여부를 선택하세요.")
                .AddTwoButtons("👥 팀원 방해", "해당됩니다.", "해당되지 않습니다.", true, "플레이어가 팀원을 방해했는지 여부를 선택하세요.")
                .AddTextArea("의사소통", SSTextArea.FoldoutMode.NotCollapsable, null, TextAlignmentOptions.Left)
                .AddTwoButtons("🚪 게임에서 나감 / 자리 비움", "해당됩니다.", "해당되지 않습니다.", true,
                    "플레이어가 게임에서 나갔거나 자리를 비웠는지 여부를 선택하세요.")
                .AddTwoButtons("👾 매크로", "해당됩니다.", "해당되지 않습니다.", true, "플레이어가 매크로를 사용했는지 여부를 선택하세요.")
                .AddPlainText("자세한 상황", "상황을 더 자세히 설명해주세요.", 512, TMP_InputField.ContentType.Custom,
                    "플레이어에 대한 추가 정보를 입력하세요.")
                .AddButton("신고", "신고하겠습니다.", 2f, hint: "선택한 옵션으로 플레이어를 신고합니다."));
        }

        public static void OnJoin(ReferenceHub hub)
        {
            _pageManager.Send(hub);
        }

        public static void Dispose()
        {
            _pageManager.Dispose();
        }
    }

    public class SSPageManager
    {
        public SSPageManager()
        {
            Pages = new Dictionary<ReferenceHub, SSPage>();
            _defaultElements = new List<SSElement>();
            ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnUserInputReceived;
        }

        public SSPageManager(List<SSElement> defaultElements)
        {
            Pages = new Dictionary<ReferenceHub, SSPage>();
            _defaultElements = defaultElements;
            ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnUserInputReceived;
        }

        public SSPageManager(SSPageBuilder builder)
        {
            Pages = new Dictionary<ReferenceHub, SSPage>();
            _defaultElements = builder.Elements;
            ServerSpecificSettingsSync.ServerOnSettingValueReceived += OnUserInputReceived;
        }

        public Dictionary<ReferenceHub, SSPage> Pages { get; }

        public SSPage Get(ReferenceHub hub)
        {
            if (Pages.TryGetValue(hub, out var page))
            {
                return page;
            }

            var clonedElements = _defaultElements.Select(x => x.Clone()).ToList();

            var newPage = new SSPage(hub, clonedElements);
            Pages.Add(hub, newPage);
            return newPage;
        }

        private readonly List<SSElement> _defaultElements;

        private void OnUserInputReceived(ReferenceHub hub, ServerSpecificSettingBase entry)
        {
            var page = Get(hub);

            if (page == null)
            {
                Log.Error($"Received null page for player: {hub.nicknameSync.MyNick} ({hub.netId}) at {DateTime.UtcNow}.");
                return;
            }

            page.OnUserInput?.Invoke(page.GetElement(entry.SettingId));
            page.OnUserInputReceived(entry);
        }

        public void SendAll()
        {
            foreach (var page in Pages.Values)
            {
                page.Send();
            }
        }

        public void Send(ReferenceHub hub)
        {
            Get(hub).Send();
        }

        public void Dispose()
        {
            ServerSpecificSettingsSync.ServerOnSettingValueReceived -= OnUserInputReceived;
            Pages.Clear();
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
        /// <param name="owner">The reference hub that owns the page.<br/>페이지를 소유한 참조 허브입니다.</param>
        /// <param name="builder">The builder that defines the settings for the page.<br/>페이지의 설정을 정의하는 빌더입니다.</param>
        public SSPage(ReferenceHub owner, SSPageBuilder builder)
        {
            Owner = owner;
            Entries = builder.Elements;
        }

        public SSPage(ReferenceHub owner, List<SSElement> elements)
        {
            Owner = owner;
            Entries = elements;
        }

        public ReferenceHub Owner { get; set; }

        /// <summary>
        /// Triggered when a user interacts with a setting.<br/>
        /// 사용자가 설정과 상호 작용할 때 호출됩니다.
        /// </summary>
        public Action<SSElement> OnUserInput { get; set; }

        /// <summary>
        /// The list of entries on the page.<br/>
        /// 페이지의 항목 목록입니다.
        /// </summary>
        public List<SSElement> Entries { get; }

        /// <summary>
        /// Sends the page settings to a specific reference hub.<br/>
        /// 특정 ReferenceHub에 페이지 설정을 전송합니다.
        /// </summary>
        public void Send()
        {
            var elements = Entries.Select(x => x.Base).ToArray();
            Owner.connectionToClient.Send(new SSSEntriesPack(elements, ServerSpecificSettingsSync.Version));
        }

        public SSElement GetElement(int id) => Entries.FirstOrDefault(x => x.Base.SettingId == id);
        public bool TryGetElement(int id, out SSElement element) => (element = GetElement(id)) != null;

        internal void OnUserInputReceived(ServerSpecificSettingBase entry)
        {
            if (entry == null)
            {
                Log.Error($"Received null entry from player: {Owner.nicknameSync.MyNick} ({Owner.netId}) at {DateTime.UtcNow}.");
                return;
            }

            var element = GetElement(entry.SettingId);

            if (element == null)
            {
                Log.Error($"Element not found for setting '{entry.Label}' with ID {entry.SettingId}. Player: {Owner.nicknameSync.MyNick} ({Owner.netId}).");
                return;
            }

            OnUserInput?.Invoke(element);

            switch (element)
            {
                case SSButtonElement buttonElement:
                    buttonElement.OnInteract?.Invoke(buttonElement);
                    break;
                case SSDropdownElement dropdownElement:
                    dropdownElement.OnChanged?.Invoke(dropdownElement);
                    break;
                case SSKeybindElement keybindElement:
                    keybindElement.OnInput?.Invoke(keybindElement);
                    break;
                case SSPlainTextElement plainTextElement:
                    plainTextElement.OnChanged?.Invoke(plainTextElement);
                    break;
                case SSSliderElement sliderElement:
                    sliderElement.OnChanged?.Invoke(sliderElement);
                    break;
                case SSTwoButtonElement twoButtonElement:
                    twoButtonElement.OnChanged?.Invoke(twoButtonElement);
                    break;
                default:
                    Log.Error($"Unhandled action for element type '{element.Type}' in page '{this}'. Player: {Owner.nicknameSync.MyNick} ({Owner.netId}). Element Label: {element.Label}.");
                    break;
            }
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
        public List<SSElement> Elements { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SSPageBuilder"/> class.<br/>
        /// <see cref="SSPageBuilder"/> 클래스의 새 인스턴스를 초기화합니다.
        /// </summary>
        public SSPageBuilder()
        {
            Elements = new List<SSElement>();
            _id = 0;
        }

        private int _id;

        /// <summary>
        /// Adds a button setting to the page.<br/>
        /// 페이지에 버튼 설정을 추가합니다.
        /// </summary>
        /// <returns>The builder instance.<br/>빌더 인스턴스를 반환합니다.</returns>
        public SSPageBuilder AddButton(string label, string buttonText, float? holdTime = null, string hint = null,
            Action<SSButtonElement> onInteract = null)
        {
            var button = new SSButtonElement
                { Base = new SSButton(++_id, label, buttonText, holdTime, hint), OnInteract = onInteract };
            Elements.Add(button);
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
            string hint = null, Action<SSDropdownElement> onChanged = null)
        {
            var dropdown = new SSDropdownElement
            {
                Base = new SSDropdownSetting(++_id, label, options, defaultIndex, entryType, hint),
                OnChanged = onChanged
            };
            Elements.Add(dropdown);
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
            Elements.Add(header);
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
            bool preventInteractionOnGUI = true, string hint = null, Action<SSKeybindElement> onInput = null)
        {
            var keybind = new SSKeybindElement
            {
                Base = new SSKeybindSetting(++_id, label, suggestedKey, preventInteractionOnGUI, hint),
                OnInput = onInput
            };
            Elements.Add(keybind);
            return this;
        }

        /// <summary>
        /// Adds a plain text setting to the page.<br/>
        /// 페이지에 평문 설정을 추가합니다.
        /// </summary>
        /// <param name="label">The label of the plain text input.<br/>평문 입력의 레이블입니다.</param>
        /// <param name="placeholder">The placeholder of the plain text input.<br/>평문 입력의 플레이스홀더입니다.</param>
        /// <param name="characterLimit">The maximum number of characters allowed in the plain text input field.<br/>평문 입력 필드에서 허용되는 최대 문자 수입니다.</param>
        /// <param name="contentType">The content type of the plaintext input (e.g., standard, alphanumeric, email, etc.).<br/>플레인 텍스트 입력의 콘텐츠 유형입니다 (예: 표준, 영숫자, 이메일 등).</param>
        /// <param name="hint">The hint of the plain text input.<br/>평문 입력의 힌트입니다.</param>
        /// <param name="onChanged">The action to execute when the text of the plain text input is changed.<br/>평문 입력의 텍스트가 변경될 때 실행할 작업입니다.</param>
        /// <returns>The builder instance.<br/>빌더 인스턴스를 반환합니다.</returns>
        public SSPageBuilder AddPlainText(string label, string placeholder = "...", int characterLimit = 64, TMP_InputField.ContentType contentType = TMP_InputField.ContentType.Standard, string hint = null, Action<SSPlainTextElement> onChanged = null)
        {
            var plainText = new SSPlainTextElement
            {
                Base = new SSPlaintextSetting(++_id, label, placeholder, characterLimit, contentType, hint),
                OnChanged = onChanged
            };
            Elements.Add(plainText);
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
            Action<SSSliderElement> onChanged = null)
        {
            var slider = new SSSliderElement
            {
                Base = new SSSliderSetting(++_id, label, minValue, maxValue, defaultValue, integer, valueToStringFormat,
                    finalDisplayFormat, hint),
                OnChanged = onChanged
            };
            Elements.Add(slider);
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
            Elements.Add(textArea);
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
        /// <param name="onChanged">The action to execute when the button is interacted with.<br/>버튼이 상호작용되었을 때 실행할 작업입니다.</param>
        /// <returns>The builder instance.<br/>빌더 인스턴스를 반환합니다.</returns>
        public SSPageBuilder AddTwoButtons(string label, string optionA, string optionB, bool defaultIsB = false, string hint = null, Action<SSTwoButtonElement> onChanged = null)
        {
            var twoButtons = new SSTwoButtonElement
            {
                Base = new SSTwoButtonsSetting(++_id, label, optionA, optionB, defaultIsB, hint),
                OnChanged = onChanged
            };
            Elements.Add(twoButtons);
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

        public abstract SSElement Clone();

        public int SettingId => Base.SettingId;

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

        /// <inheritdoc />
        public override SSElement Clone()
        {
            return new SSButtonElement
            {
                Base = new SSButton(SettingId, Label, ((SSButton)Base).ButtonText, ((SSButton)Base).HoldTimeSeconds, Hint),
                OnInteract = OnInteract
            };
        }

        /// <summary>
        /// Triggered when the button is interacted with.<br/>
        /// 버튼이 상호작용 (클릭 및 홀드) 되었을 때 호출됩니다.
        /// </summary>
        public Action<SSButtonElement> OnInteract { get; set; }

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
        public Action<SSDropdownElement> OnChanged { get; set; }

        public override SSElement Clone()
        {
            return new SSDropdownElement
            {
                Base = new SSDropdownSetting(
                    Base.SettingId,
                    Base.Label,
                    Options.ToArray(),
                    DefaultIndex,
                    EntryType,
                    Base.HintDescription),
                OnChanged = OnChanged
            };
        }

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

        public override SSElement Clone()
        {
            return new SSGroupHeaderElement
            {
                Base = new SSGroupHeader(
                    Base.Label,
                    ReducedPadding,
                    Base.HintDescription)
            };
        }

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
        public Action<SSKeybindElement> OnInput { get; set; }

        public override SSElement Clone()
        {
            return new SSKeybindElement
            {
                Base = new SSKeybindSetting(
                    Base.SettingId,
                    Base.Label,
                    SuggestedKey,
                    PreventInteractionOnGUI,
                    Base.HintDescription),
                OnInput = OnInput
            };
        }

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
        public Action<SSPlainTextElement> OnChanged { get; set; }

        public override SSElement Clone()
        {
            return new SSPlainTextElement
            {
                Base = new SSPlaintextSetting(
                    Base.SettingId,
                    Base.Label,
                    Placeholder,
                    CharacterLimit,
                    ContentType,
                    Base.HintDescription),
                OnChanged = OnChanged
            };
        }

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

        public void ClearText(ReferenceHub hub) => ((SSPlaintextSetting)Base).SendClearRequest(x => x == hub);
        public void ClearText(Player player) => ClearText(player.ReferenceHub);
        public void ClearText(Func<ReferenceHub, bool> predicate) => ((SSPlaintextSetting)Base).SendClearRequest(predicate);
    }

    public class SSSliderElement : SSElement
    {
        /// <inheritdoc />
        public override SSElementType Type { get; set; } = SSElementType.Slider;

        /// <summary>
        /// Triggered when the slider value changes.<br/>
        /// 슬라이더 값이 변경될 때 호출됩니다.
        /// </summary>
        public Action<SSSliderElement> OnChanged { get; set; }

        public override SSElement Clone()
        {
            return new SSSliderElement
            {
                Base = new SSSliderSetting(
                    Base.SettingId,
                    Base.Label,
                    MinValue,
                    MaxValue,
                    DefaultValue,
                    IsInteger,
                    ValueToStringFormat,
                    FinalDisplayFormat,
                    Base.HintDescription),
                OnChanged = OnChanged
            };
        }

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

        public override SSElement Clone()
        {
            return new SSTextAreaElement
            {
                Base = new SSTextArea(
                    Base.SettingId,
                    Label,
                    FoldoutMode,
                    Hint,
                    TextAlignment),
            };
        }

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
        /// Triggered when the value of the two buttons is changed.<br/>
        /// 두 개의 버튼의 값이 변경되었을 때 호출됩니다.
        /// </summary>
        public Action<SSTwoButtonElement> OnChanged { get; set; }

        public override SSElement Clone()
        {
            return new SSTwoButtonElement
            {
                Base = new SSTwoButtonsSetting(
                    Base.SettingId,
                    Base.Label,
                    OptionA,
                    OptionB,
                    DefaultIsB,
                    Base.HintDescription),
                OnChanged = OnChanged
            };
        }

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