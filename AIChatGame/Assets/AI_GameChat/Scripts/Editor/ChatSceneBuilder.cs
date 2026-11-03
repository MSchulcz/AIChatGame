using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace AIGameChat.EditorTools
{
    /// <summary>
    /// Tools → AI Chat → Build Character Scene: создаёт сцену CharacterChatScene с раскладкой 50/50
    /// (слева вертикальный портрет, справа чат), три готовых профиля персонажей и портреты-заглушки.
    /// Существующие профили и PNG не перезаписываются — можно спокойно подменять заглушки своими рисунками.
    /// </summary>
    public static class ChatSceneBuilder
    {
        const string Root = "Assets/AI_GameChat";

        class ProfileSpec
        {
            public string id;
            public string name;
            public string personality;
            public string style;
            public string greeting;
            public Color body;
            public Color hair;
        }

        static readonly ProfileSpec[] Specs =
        {
            new ProfileSpec
            {
                id = "mika",
                name = "Мика",
                personality = "Жизнерадостная и болтливая девушка-искательница приключений. Любит шутки и новые знакомства, быстро увлекается и сыплет вопросами. Искренне радуется успехам игрока и сочувствует его неудачам. Иногда смущается от комплиментов.",
                style = "Короткие энергичные фразы, восклицания, иногда смайлик «:)». Обращается на «ты».",
                greeting = "Привет-привет! Я Мика. Расскажешь, как твой день? Мне ужасно интересно!",
                body = new Color(0.95f, 0.55f, 0.25f),
                hair = new Color(0.35f, 0.20f, 0.10f)
            },
            new ProfileSpec
            {
                id = "rei",
                name = "Рэй",
                personality = "Колючая и саркастичная, но в глубине души заботливая. Не любит показывать мягкость и прикрывается язвительными замечаниями. Злится на грубость и глупые шутки, но тает, когда с ней по-доброму. Легко смущается и отрицает это.",
                style = "Сухие ироничные фразы, лёгкие подколки, редкое «ну… ладно». Обращается на «ты».",
                greeting = "А, это ты. Ну, говори уже, раз пришёл. Только не трать моё время попусту.",
                body = new Color(0.25f, 0.30f, 0.55f),
                hair = new Color(0.10f, 0.10f, 0.15f)
            },
            new ProfileSpec
            {
                id = "lin",
                name = "Наставник Лин",
                personality = "Спокойный и мудрый наставник. Говорит неторопливо, рассуждает притчами и задаёт вопросы, которые заставляют задуматься. Терпелив, поддерживает игрока в трудный момент, редко удивляется и никогда не повышает голос.",
                style = "Размеренная речь, образные сравнения, мягкий юмор. Обращается на «ты».",
                greeting = "Добро пожаловать, путник. Присаживайся. О чём ты хочешь поговорить сегодня?",
                body = new Color(0.30f, 0.55f, 0.45f),
                hair = new Color(0.80f, 0.80f, 0.82f)
            }
        };

        [MenuItem("Tools/AI Chat/Build Character Scene")]
        public static void Build()
        {
            if (TMP_Settings.instance == null)
            {
                EditorUtility.DisplayDialog("AI Chat",
                    "Сначала импортируйте ресурсы TextMeshPro:\nWindow → TextMeshPro → Import TMP Essential Resources.\nПотом запустите Tools → AI Chat → Build Character Scene ещё раз.",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string scenePath = BuildSceneNoDialogs();

            EditorUtility.DisplayDialog("AI Chat",
                "Сцена создана: " + scenePath + "\n\nДальше:\n1. Tools → AI Chat → Create Config File и вставьте ключ OpenRouter (или введите его в игре в «Настройках»).\n2. Нажмите Play.",
                "OK");
        }

        /// <summary>
        /// Та же сборка без модальных окон — для автоматизации (MCP, CI).
        /// Текущую сцену не сохраняет: несохранённые изменения в ней будут потеряны, проверяйте заранее.
        /// </summary>
        public static string BuildSceneNoDialogs()
        {
            if (TMP_Settings.instance == null)
            {
                throw new InvalidOperationException("Не импортированы TMP Essential Resources.");
            }

            // Сначала новая сцена, потом ассеты: NewScene выгружает неиспользуемые ассеты, и ссылки,
            // полученные до неё, становятся недействительными (в сцену записались бы пустые ссылки).
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureFolders();
            TMP_FontAsset font = CreateFont();
            List<CharacterProfile> profiles = CreateProfiles();

            CreateCamera();
            CreateEventSystem();

            ChatUI chatUi;
            CharacterView characterView;
            SettingsPanel settingsPanel;
            BuildCanvas(font, profiles, out chatUi, out characterView, out settingsPanel);
            CreateManager(chatUi, characterView, settingsPanel, profiles);

            string scenePath = Root + "/Scenes/CharacterChatScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AddToBuildSettings(scenePath);
            AssetDatabase.SaveAssets();
            return scenePath;
        }

        [MenuItem("Tools/AI Chat/Create Config File")]
        public static void CreateConfigFile()
        {
            string dir = "Assets/StreamingAssets";
            Directory.CreateDirectory(AbsPath(dir));
            string target = dir + "/openrouter_config.json";
            if (File.Exists(AbsPath(target)))
            {
                EditorUtility.DisplayDialog("AI Chat", "Файл уже существует: " + target, "OK");
            }
            else
            {
                File.WriteAllText(AbsPath(target), "{\n  \"apiKey\": \"PASTE_YOUR_KEY_HERE_LOCALLY\",\n  \"models\": []\n}\n");
                AssetDatabase.Refresh();
            }
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(target);
            if (asset != null) Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        // ---------------------------------------------------------------- ассеты

        static void EnsureFolders()
        {
            string[] folders =
            {
                Root, Root + "/Characters", Root + "/Sprites", Root + "/Scenes", Root + "/Fonts", "Assets/StreamingAssets"
            };
            foreach (string f in folders) Directory.CreateDirectory(AbsPath(f));
            AssetDatabase.Refresh();
        }

        static string AbsPath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        /// <summary>
        /// Динамический шрифт TMP на основе встроенного Liberation Sans (есть кириллица).
        /// Если не получилось — остаётся шрифт TMP по умолчанию.
        /// </summary>
        static TMP_FontAsset CreateFont()
        {
            string path = Root + "/Fonts/ChatFont SDF.asset";
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;

            // Если шрифт TMP по умолчанию (вместе со своими fallback) уже умеет кириллицу — используем его.
            TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont != null && defaultFont.HasCharacter('Ж', true, true)) return defaultFont;

            try
            {
                // Liberation Sans из TMP Essentials содержит кириллицу; встроенный шрифт — запасной вариант.
                Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
                if (source == null) source = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source);
                if (asset == null) return null;

                asset.name = "ChatFont SDF";
                AssetDatabase.CreateAsset(asset, path);
                if (asset.atlasTexture != null)
                {
                    asset.atlasTexture.name = "ChatFont Atlas";
                    AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
                }
                if (asset.material != null)
                {
                    asset.material.name = "ChatFont Material";
                    AssetDatabase.AddObjectToAsset(asset.material, asset);
                }

                // Чтобы глифы, «запечённые» во время тестов в редакторе, не попадали в сборку.
                var so = new SerializedObject(asset);
                SerializedProperty clear = so.FindProperty("m_ClearDynamicDataOnBuild");
                if (clear != null)
                {
                    clear.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                return asset;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AIChat] Не удалось создать шрифт с кириллицей, используется шрифт TMP по умолчанию: " + e.Message);
                return null;
            }
        }

        static List<CharacterProfile> CreateProfiles()
        {
            // 1. Пишем PNG-заглушки (только отсутствующие).
            foreach (ProfileSpec spec in Specs)
            {
                Directory.CreateDirectory(AbsPath(Root + "/Sprites/" + spec.id));
                foreach (Emotion emotion in Enum.GetValues(typeof(Emotion)))
                {
                    string file = AbsPath(SpritePath(spec.id, emotion));
                    if (!File.Exists(file))
                    {
                        File.WriteAllBytes(file, PlaceholderPortraits.RenderPng(spec.body, spec.hair, emotion));
                    }
                }
            }
            AssetDatabase.Refresh();

            // 2. Настраиваем импорт как Sprite и создаём/дополняем профили.
            var result = new List<CharacterProfile>();
            foreach (ProfileSpec spec in Specs)
            {
                string assetPath = Root + "/Characters/" + spec.id + ".asset";
                CharacterProfile profile = AssetDatabase.LoadAssetAtPath<CharacterProfile>(assetPath);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<CharacterProfile>();
                    profile.displayName = spec.name;
                    profile.personality = spec.personality;
                    profile.speechStyle = spec.style;
                    profile.greeting = spec.greeting;
                    AssetDatabase.CreateAsset(profile, assetPath);
                }

                if (profile.sprites == null) profile.sprites = new List<EmotionSprite>();
                if (profile.sprites.Count == 0)
                {
                    foreach (Emotion emotion in Enum.GetValues(typeof(Emotion)))
                    {
                        string p = SpritePath(spec.id, emotion);
                        ConfigureSpriteImport(p);
                        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                        profile.sprites.Add(new EmotionSprite { emotion = emotion, sprite = sprite });
                    }
                }

                EditorUtility.SetDirty(profile);
                result.Add(profile);
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        static string SpritePath(string id, Emotion emotion)
        {
            return Root + "/Sprites/" + id + "/" + id + "_" + emotion.ToString().ToLowerInvariant() + ".png";
        }

        static void ConfigureSpriteImport(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        // ---------------------------------------------------------------- сцена

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
            go.AddComponent<AudioListener>();
        }

        static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();

            // Если в проекте включена новая система ввода, нужен её UI-модуль; иначе — обычный.
            Type inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            go.AddComponent(inputSystemModule != null ? inputSystemModule : typeof(StandaloneInputModule));
        }

        static void BuildCanvas(TMP_FontAsset font, List<CharacterProfile> profiles,
            out ChatUI chatUi, out CharacterView characterView, out SettingsPanel settingsPanel)
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Transform canvas = canvasGo.transform;

            // Фон
            RectTransform background = NewRect("Background", canvas);
            Stretch(background);
            AddImage(background, new Color(0.07f, 0.07f, 0.10f), false);

            // ---------- Левая половина: персонаж (50% ширины экрана, вертикальный портрет) ----------
            RectTransform characterPanel = NewRect("CharacterPanel", canvas);
            Anchor(characterPanel, new Vector2(0f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            AddImage(characterPanel, new Color(0.11f, 0.11f, 0.15f), false);

            RectTransform portraitArea = NewRect("PortraitArea", characterPanel);
            Anchor(portraitArea, Vector2.zero, Vector2.one, new Vector2(24f, 100f), new Vector2(-24f, -24f));

            RectTransform portraitRt = NewRect("Portrait", portraitArea);
            Stretch(portraitRt);
            UnityEngine.UI.Image portrait = AddImage(portraitRt, Color.white, false);
            portrait.preserveAspect = true;
            Sprite firstSprite = profiles.Count > 0 ? profiles[0].GetSprite(Emotion.Neutral) : null;
            portrait.sprite = firstSprite;
            var fitter = portraitRt.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = firstSprite != null
                ? firstSprite.rect.width / firstSprite.rect.height
                : (float)PlaceholderPortraits.Width / PlaceholderPortraits.Height;

            RectTransform nameRt = NewRect("NameLabel", characterPanel);
            Anchor(nameRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 20f), new Vector2(-24f, 84f));
            TMP_Text nameLabel = AddText(nameRt, "Персонаж", 44, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);

            characterView = characterPanel.gameObject.AddComponent<CharacterView>();
            SetRef(characterView, "portrait", portrait);
            SetRef(characterView, "fitter", fitter);
            SetRef(characterView, "nameLabel", nameLabel);

            // ---------- Правая половина: чат ----------
            RectTransform chatPanel = NewRect("ChatPanel", canvas);
            Anchor(chatPanel, new Vector2(0.5f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            AddImage(chatPanel, new Color(0.14f, 0.14f, 0.19f), false);

            RectTransform titleRt = NewRect("Title", chatPanel);
            Anchor(titleRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -80f), new Vector2(-260f, -12f));
            AddText(titleRt, "Разговор", 38, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, Color.white);

            UnityEngine.UI.Button openSettings = MakeButton("SettingsButton", "Настройки", chatPanel, 26);
            Anchor((RectTransform)openSettings.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-236f, -76f), new Vector2(-24f, -16f));

            // Лента сообщений
            GameObject scrollGo = UnityEngine.UI.DefaultControls.CreateScrollView(MakeDefaultResources());
            scrollGo.name = "ScrollView";
            scrollGo.transform.SetParent(chatPanel, false);
            var scrollRt = (RectTransform)scrollGo.transform;
            Anchor(scrollRt, Vector2.zero, Vector2.one, new Vector2(24f, 170f), new Vector2(-24f, -92f));
            var scrollImage = scrollGo.GetComponent<UnityEngine.UI.Image>();
            if (scrollImage != null) scrollImage.color = new Color(0f, 0f, 0f, 0.25f);

            var scroll = scrollGo.GetComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            if (scroll.horizontalScrollbar != null)
            {
                UnityEngine.Object.DestroyImmediate(scroll.horizontalScrollbar.gameObject);
                scroll.horizontalScrollbar = null;
            }
            scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            if (scroll.viewport != null)
            {
                scroll.viewport.offsetMin = Vector2.zero;
                scroll.viewport.offsetMax = new Vector2(-17f, 0f);
            }

            RectTransform content = scroll.content;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var contentLayout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            contentLayout.spacing = 14f;
            contentLayout.padding = new RectOffset(8, 8, 10, 10);
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            var contentFitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            contentFitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;

            // Статус «Персонаж думает…»
            RectTransform statusRt = NewRect("Status", chatPanel);
            Anchor(statusRt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(28f, 122f), new Vector2(-28f, 164f));
            TMP_Text status = AddText(statusRt, "", 26, FontStyles.Italic, TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.7f));

            // Строка ввода и кнопка
            GameObject inputGo = TMP_DefaultControls.CreateInputField(MakeTmpResources());
            inputGo.name = "Input";
            inputGo.transform.SetParent(chatPanel, false);
            Anchor((RectTransform)inputGo.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 24f), new Vector2(-206f, 114f));
            TMP_InputField input = inputGo.GetComponent<TMP_InputField>();
            ConfigureInput(input, "Напишите сообщение…", 30);
            input.characterLimit = 500;

            UnityEngine.UI.Button send = MakeButton("SendButton", "Отправить", chatPanel, 28);
            Anchor((RectTransform)send.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-190f, 24f), new Vector2(-24f, 114f));

            chatUi = chatPanel.gameObject.AddComponent<ChatUI>();
            SetRef(chatUi, "input", input);
            SetRef(chatUi, "sendButton", send);
            SetRef(chatUi, "statusText", status);
            SetRef(chatUi, "scroll", scroll);
            SetRef(chatUi, "content", content);
            SetRef(chatUi, "font", font);

            // ---------- Окно настроек (поверх всего, скрыто) ----------
            RectTransform overlay = NewRect("SettingsOverlay", canvas);
            Stretch(overlay);
            AddImage(overlay, new Color(0f, 0f, 0f, 0.88f), true);

            RectTransform window = NewRect("Window", overlay);
            Anchor(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-480f, -430f), new Vector2(480f, 430f));
            AddImage(window, new Color(0.16f, 0.16f, 0.22f), true);
            var windowLayout = window.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            windowLayout.padding = new RectOffset(36, 36, 36, 36);
            windowLayout.spacing = 14f;
            windowLayout.childAlignment = TextAnchor.UpperCenter;
            windowLayout.childControlWidth = true;
            windowLayout.childControlHeight = true;
            windowLayout.childForceExpandWidth = true;
            windowLayout.childForceExpandHeight = false;

            AddLabel(window, "Настройки персонажа", 40, FontStyles.Bold, 56f);
            AddLabel(window, "Персонаж", 26, FontStyles.Normal, 34f);

            GameObject dropdownGo = TMP_DefaultControls.CreateDropdown(MakeTmpResources());
            dropdownGo.name = "ProfileDropdown";
            dropdownGo.transform.SetParent(window, false);
            SetPreferredHeight(dropdownGo, 60f);
            TMP_Dropdown dropdown = dropdownGo.GetComponent<TMP_Dropdown>();
            dropdown.captionText.fontSize = 28;
            dropdown.itemText.fontSize = 28;
            var itemRt = dropdown.itemText.transform.parent as RectTransform;
            if (itemRt != null) itemRt.sizeDelta = new Vector2(itemRt.sizeDelta.x, 56f);
            if (dropdown.template != null) dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, 320f);

            AddLabel(window, "Свой характер (необязательно — заменит характер выбранного персонажа):", 24, FontStyles.Normal, 64f);
            GameObject customGo = TMP_DefaultControls.CreateInputField(MakeTmpResources());
            customGo.name = "CustomPersonalityInput";
            customGo.transform.SetParent(window, false);
            SetPreferredHeight(customGo, 190f);
            TMP_InputField customInput = customGo.GetComponent<TMP_InputField>();
            ConfigureInput(customInput, "Например: Ты ворчливый кот-детектив, который не доверяет людям, но обожаёт рыбу…", 26);
            customInput.lineType = TMP_InputField.LineType.MultiLineNewline;
            customInput.textComponent.alignment = TextAlignmentOptions.TopLeft;
            if (customInput.placeholder is TMP_Text customPlaceholder) customPlaceholder.alignment = TextAlignmentOptions.TopLeft;

            AddLabel(window, "API-ключ OpenRouter", 26, FontStyles.Normal, 34f);
            GameObject keyGo = TMP_DefaultControls.CreateInputField(MakeTmpResources());
            keyGo.name = "ApiKeyInput";
            keyGo.transform.SetParent(window, false);
            SetPreferredHeight(keyGo, 60f);
            TMP_InputField keyInput = keyGo.GetComponent<TMP_InputField>();
            ConfigureInput(keyInput, "sk-or-… (оставьте пустым, чтобы не менять)", 26);
            keyInput.contentType = TMP_InputField.ContentType.Password;

            TMP_Text hint = AddLabel(window, "", 22, FontStyles.Italic, 76f);
            hint.color = new Color(1f, 1f, 1f, 0.65f);

            RectTransform buttonRow = NewRect("Buttons", window);
            SetPreferredHeight(buttonRow.gameObject, 64f);
            var rowLayout = buttonRow.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            rowLayout.spacing = 20f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = true;
            UnityEngine.UI.Button apply = MakeButton("ApplyButton", "Применить", buttonRow, 28);
            UnityEngine.UI.Button close = MakeButton("CloseButton", "Закрыть", buttonRow, 28);

            // SettingsPanel живёт на всегда активном объекте — само окно (overlay) скрывается им в Awake.
            var settingsGo = new GameObject("SettingsController");
            settingsGo.transform.SetParent(canvas, false);
            settingsPanel = settingsGo.AddComponent<SettingsPanel>();
            SetRef(settingsPanel, "panelRoot", overlay.gameObject);
            SetRef(settingsPanel, "profileDropdown", dropdown);
            SetRef(settingsPanel, "customInput", customInput);
            SetRef(settingsPanel, "apiKeyInput", keyInput);
            SetRef(settingsPanel, "openButton", openSettings);
            SetRef(settingsPanel, "closeButton", close);
            SetRef(settingsPanel, "applyButton", apply);
            SetRef(settingsPanel, "hintText", hint);
            overlay.gameObject.SetActive(false);

            // Шрифт с кириллицей — всем текстам, включая неактивные.
            if (font != null)
            {
                foreach (TMP_Text t in canvasGo.GetComponentsInChildren<TMP_Text>(true)) t.font = font;
            }
        }

        static void CreateManager(ChatUI chatUi, CharacterView characterView, SettingsPanel settingsPanel, List<CharacterProfile> profiles)
        {
            var go = new GameObject("ChatManager");
            ModelRotator rotator = go.AddComponent<ModelRotator>();
            OpenRouterClient client = go.AddComponent<OpenRouterClient>();
            ChatController controller = go.AddComponent<ChatController>();

            SetRef(client, "rotator", rotator);
            SetRef(controller, "ui", chatUi);
            SetRef(controller, "characterView", characterView);
            SetRef(controller, "client", client);
            SetRef(controller, "settings", settingsPanel);
            SetRefList(controller, "profiles", profiles);
        }

        static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------------------------------------------------------------- помощники UI

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        static void Stretch(RectTransform rt)
        {
            Anchor(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        static UnityEngine.UI.Image AddImage(RectTransform rt, Color color, bool raycastTarget)
        {
            var image = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        static TMP_Text AddText(RectTransform rt, string text, int size, FontStyles style, TextAlignmentOptions alignment, Color color)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = alignment;
            t.color = color;
            t.raycastTarget = false;
#if UNITY_6000_0_OR_NEWER
            t.textWrappingMode = TextWrappingModes.Normal;
#else
            t.enableWordWrapping = true;
#endif
            return t;
        }

        static TMP_Text AddLabel(RectTransform parent, string text, int size, FontStyles style, float height)
        {
            RectTransform rt = NewRect("Label", parent);
            SetPreferredHeight(rt.gameObject, height);
            return AddText(rt, text, size, style, TextAlignmentOptions.MidlineLeft, Color.white);
        }

        static void SetPreferredHeight(GameObject go, float height)
        {
            var element = go.GetComponent<UnityEngine.UI.LayoutElement>();
            if (element == null) element = go.AddComponent<UnityEngine.UI.LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }

        static void ConfigureInput(TMP_InputField input, string placeholder, int fontSize)
        {
            input.textComponent.fontSize = fontSize;
            if (input.placeholder is TMP_Text p)
            {
                p.text = placeholder;
                p.fontSize = fontSize;
                p.fontStyle = FontStyles.Italic;
            }
        }

        static UnityEngine.UI.Button MakeButton(string name, string label, Transform parent, int fontSize)
        {
            GameObject go = TMP_DefaultControls.CreateButton(MakeTmpResources());
            go.name = name;
            go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.text = label;
                text.fontSize = fontSize;
            }
            return go.GetComponent<UnityEngine.UI.Button>();
        }

        static Sprite Builtin(string path)
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
        }

        static TMP_DefaultControls.Resources MakeTmpResources()
        {
            return new TMP_DefaultControls.Resources
            {
                standard = Builtin("UI/Skin/UISprite.psd"),
                background = Builtin("UI/Skin/Background.psd"),
                inputField = Builtin("UI/Skin/InputFieldBackground.psd"),
                knob = Builtin("UI/Skin/Knob.psd"),
                checkmark = Builtin("UI/Skin/Checkmark.psd"),
                dropdown = Builtin("UI/Skin/DropdownArrow.psd"),
                mask = Builtin("UI/Skin/UIMask.psd")
            };
        }

        static UnityEngine.UI.DefaultControls.Resources MakeDefaultResources()
        {
            return new UnityEngine.UI.DefaultControls.Resources
            {
                standard = Builtin("UI/Skin/UISprite.psd"),
                background = Builtin("UI/Skin/Background.psd"),
                inputField = Builtin("UI/Skin/InputFieldBackground.psd"),
                knob = Builtin("UI/Skin/Knob.psd"),
                checkmark = Builtin("UI/Skin/Checkmark.psd"),
                dropdown = Builtin("UI/Skin/DropdownArrow.psd"),
                mask = Builtin("UI/Skin/UIMask.psd")
            };
        }

        // ---------------------------------------------------------------- связывание полей

        static void SetRef(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError("[AIChat] Поле '" + field + "' не найдено в " + target.GetType().Name);
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetRefList<T>(UnityEngine.Object target, string field, IList<T> values) where T : UnityEngine.Object
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError("[AIChat] Поле '" + field + "' не найдено в " + target.GetType().Name);
                return;
            }
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
