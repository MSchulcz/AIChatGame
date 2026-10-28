using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Собирает сцену ChatScene с интерфейсом чата. Меню: Tools → AI Chat → Build Chat Scene.</summary>
public static class ChatSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/ChatScene.unity";

    private static readonly Color Background = new Color(0.11f, 0.12f, 0.14f);
    private static readonly Color Panel = new Color(0.16f, 0.17f, 0.20f);
    private static readonly Color Accent = new Color(0.25f, 0.47f, 0.85f);
    private static readonly Color TextColor = new Color(0.93f, 0.94f, 0.96f);
    private static readonly Color MutedText = new Color(0.65f, 0.68f, 0.73f);

    [MenuItem("Tools/AI Chat/Build Chat Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraGo.tag = "MainCamera";
        var camera = cameraGo.GetComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Background;
        cameraGo.transform.position = new Vector3(0, 0, -10);

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var resources = new TMP_DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
        };
        var uiResources = new DefaultControls.Resources
        {
            standard = resources.standard,
            background = resources.background,
            inputField = resources.inputField,
            knob = resources.knob,
            checkmark = resources.checkmark,
            dropdown = resources.dropdown,
            mask = resources.mask
        };

        // Canvas
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = LayerMask.NameToLayer("UI");
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Корневая панель с вертикальной раскладкой
        var root = CreateUI("ChatPanel", canvasGo.transform);
        Stretch(root);
        root.gameObject.AddComponent<Image>().color = Background;
        var rootLayout = root.gameObject.AddComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(32, 32, 24, 24);
        rootLayout.spacing = 12;
        rootLayout.childControlWidth = rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        // Заголовок + кнопка очистки
        var header = CreateRow("Header", root, 56, 16);
        var title = CreateText("Title", header, "ИИ-чат · OpenRouter", 32, TextColor, FontStyles.Bold);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var clearButton = CreateButton("ClearButton", header, resources, "Очистить", Panel, 200);

        // Область сообщений
        var scrollGo = DefaultControls.CreateScrollView(uiResources);
        scrollGo.name = "MessagesScrollView";
        scrollGo.transform.SetParent(root, false);
        scrollGo.GetComponent<Image>().color = Panel;
        scrollGo.AddComponent<LayoutElement>().flexibleHeight = 1;
        var scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30;
        Object.DestroyImmediate(scrollRect.horizontalScrollbar.gameObject);
        scrollRect.horizontalScrollbar = null;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

        var content = scrollRect.content;
        content.name = "MessagesContainer";
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = Vector2.zero;
        var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(16, 16, 16, 16);
        contentLayout.spacing = 10;
        contentLayout.childControlWidth = contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var template = CreateMessageTemplate(content, resources.standard);

        // Статус
        var status = CreateText("StatusText", root, "Готово.", 20, MutedText, FontStyles.Italic);
        status.alignment = TextAlignmentOptions.MidlineLeft;
        var statusElement = status.gameObject.AddComponent<LayoutElement>();
        statusElement.preferredHeight = 28;
        statusElement.flexibleHeight = 0;

        // Ввод + отправка
        var inputRow = CreateRow("InputRow", root, 64, 12);
        var inputGo = TMP_DefaultControls.CreateInputField(resources);
        inputGo.name = "MessageInput";
        inputGo.transform.SetParent(inputRow, false);
        inputGo.AddComponent<LayoutElement>().flexibleWidth = 1;
        var input = inputGo.GetComponent<TMP_InputField>();
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 2000;
        input.pointSize = 24;
        input.textComponent.color = Color.black;
        input.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
        var placeholder = (TMP_Text)input.placeholder;
        placeholder.text = "Введите сообщение и нажмите Enter…";
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        input.textViewport.offsetMin = new Vector2(16, 6);
        input.textViewport.offsetMax = new Vector2(-16, -6);
        var sendButton = CreateButton("SendButton", inputRow, resources, "Отправить", Accent, 220);

        // ChatManager
        var manager = new GameObject("ChatManager");
        var history = manager.AddComponent<ChatHistory>();
        var rotator = manager.AddComponent<ModelRotator>();
        var client = manager.AddComponent<OpenRouterClient>();
        client.rotator = rotator;
        var ui = manager.AddComponent<ChatUI>();
        ui.history = history;
        ui.client = client;
        ui.inputField = input;
        ui.sendButton = sendButton;
        ui.clearButton = clearButton;
        ui.scrollRect = scrollRect;
        ui.messagesContainer = content;
        ui.messageTemplate = template;
        ui.statusText = status;

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuild(ScenePath);
        Debug.Log("[ChatSceneBuilder] Сцена сохранена: " + ScenePath);
    }

    private static ChatMessageView CreateMessageTemplate(Transform parent, Sprite bubbleSprite)
    {
        var row = CreateUI("MessageTemplate", parent);
        var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childControlWidth = rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;

        var bubble = CreateUI("Bubble", row);
        var bubbleImage = bubble.gameObject.AddComponent<Image>();
        bubbleImage.sprite = bubbleSprite;
        bubbleImage.type = Image.Type.Sliced;
        var bubbleLayout = bubble.gameObject.AddComponent<VerticalLayoutGroup>();
        bubbleLayout.padding = new RectOffset(18, 18, 12, 14);
        bubbleLayout.spacing = 4;
        bubbleLayout.childControlWidth = bubbleLayout.childControlHeight = true;
        bubbleLayout.childForceExpandWidth = true;
        bubbleLayout.childForceExpandHeight = false;

        var header = CreateText("Header", bubble, "Ассистент", 18, MutedText, FontStyles.Bold);
        var body = CreateText("Body", bubble, "Текст сообщения", 24, TextColor, FontStyles.Normal);
        body.richText = false;
        body.textWrappingMode = TextWrappingModes.Normal;

        var view = row.gameObject.AddComponent<ChatMessageView>();
        view.row = rowLayout;
        view.bubble = bubbleImage;
        view.header = header;
        view.body = body;
        row.gameObject.SetActive(false);
        return view;
    }

    private static RectTransform CreateUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform CreateRow(string name, Transform parent, float height, float spacing)
    {
        var row = CreateUI(name, parent);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        var element = row.gameObject.AddComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = height;
        // childForceExpandHeight делает строку «гибкой»; явный 0 оставляет всё свободное место списку сообщений.
        element.flexibleHeight = 0;
        return row;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color color, FontStyles style)
    {
        var rt = CreateUI(name, parent);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.fontStyle = style;
        return tmp;
    }

    private static Button CreateButton(string name, Transform parent, TMP_DefaultControls.Resources resources, string label, Color color, float width)
    {
        var go = TMP_DefaultControls.CreateButton(resources);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        go.AddComponent<LayoutElement>().preferredWidth = width;
        var text = go.GetComponentInChildren<TMP_Text>();
        text.text = label;
        text.fontSize = 24;
        text.color = TextColor;
        return go.GetComponent<Button>();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void AddSceneToBuild(string path)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == path);
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
