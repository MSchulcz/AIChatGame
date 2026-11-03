using NUnit.Framework;

namespace AIGameChat.Tests
{
    public class EmotionParserTests
    {
        [Test]
        public void CleanJson_IsParsed()
        {
            ParsedReply r = EmotionParser.Parse("{\"emotion\":\"happy\",\"reply\":\"Привет!\"}", "привет");
            Assert.AreEqual(Emotion.Happy, r.emotion);
            Assert.AreEqual("Привет!", r.text);
            Assert.IsTrue(r.emotionFromModel);
            Assert.AreEqual("json", r.method);
        }

        [Test]
        public void JsonInsideCodeFence_IsParsed()
        {
            string raw = "```json\n{\"emotion\":\"sad\",\"reply\":\"Мне жаль.\"}\n```";
            ParsedReply r = EmotionParser.Parse(raw, "");
            Assert.AreEqual(Emotion.Sad, r.emotion);
            Assert.AreEqual("Мне жаль.", r.text);
        }

        [Test]
        public void JsonWithExtraText_IsParsed()
        {
            string raw = "Вот ответ: {\"emotion\":\"angry\",\"reply\":\"Прекрати!\"} Надеюсь, помог.";
            ParsedReply r = EmotionParser.Parse(raw, "");
            Assert.AreEqual(Emotion.Angry, r.emotion);
            Assert.AreEqual("Прекрати!", r.text);
        }

        [Test]
        public void TruncatedJson_FallsBackToRegex()
        {
            string raw = "{\"emotion\":\"surprised\",\"reply\":\"Ого, вот это новость";
            ParsedReply r = EmotionParser.Parse(raw, "");
            Assert.AreEqual(Emotion.Surprised, r.emotion);
            Assert.IsTrue(r.emotionFromModel);
            StringAssert.StartsWith("Ого, вот это новость", r.text);
        }

        [Test]
        public void EscapedQuotesAndNewline_AreUnescapedByRegexFallback()
        {
            string raw = "{\"emotion\":\"happy\",\"reply\":\"Она сказала: \\\"да\\\"\\nи ушла";
            ParsedReply r = EmotionParser.Parse(raw, "");
            StringAssert.Contains("\"да\"", r.text);
            StringAssert.Contains("\n", r.text);
        }

        [Test]
        public void EmotionTag_IsExtractedAndRemoved()
        {
            ParsedReply r = EmotionParser.Parse("[emotion: scared] Тут что-то шуршит!", "");
            Assert.AreEqual(Emotion.Scared, r.emotion);
            Assert.AreEqual("tag", r.method);
            Assert.AreEqual("Тут что-то шуршит!", r.text);
        }

        [Test]
        public void PlainText_UsesKeywordFallbackFromPlayerText()
        {
            ParsedReply r = EmotionParser.Parse("Хорошо, расскажи подробнее.", "Мне очень грустно сегодня");
            Assert.AreEqual(Emotion.Sad, r.emotion);
            Assert.IsFalse(r.emotionFromModel);
            Assert.AreEqual("Хорошо, расскажи подробнее.", r.text);
        }

        [Test]
        public void UnknownEmotion_BecomesNeutralWhenPlayerTextIsNeutral()
        {
            ParsedReply r = EmotionParser.Parse("{\"emotion\":\"banana\",\"reply\":\"Ладно.\"}", "который час");
            Assert.AreEqual(Emotion.Neutral, r.emotion);
            Assert.IsFalse(r.emotionFromModel);
        }

        [Test]
        public void RussianEmotionNames_AreUnderstood()
        {
            ParsedReply r = EmotionParser.Parse("{\"emotion\":\"радость\",\"reply\":\"Ура!\"}", "");
            Assert.AreEqual(Emotion.Happy, r.emotion);
        }

        [Test]
        public void EmptyInput_DoesNotThrow()
        {
            ParsedReply r = EmotionParser.Parse(null, null);
            Assert.AreEqual(Emotion.Neutral, r.emotion);
            Assert.AreEqual("", r.text);
        }

        [TestCase("Ты очень красивая", Emotion.Embarrassed)]
        [TestCase("Ты идиот", Emotion.Angry)]
        [TestCase("Спасибо, это здорово!", Emotion.Happy)]
        [TestCase("Мне страшно", Emotion.Scared)]
        [TestCase("Вау, неужели?", Emotion.Surprised)]
        [TestCase("Сколько времени?", Emotion.Neutral)]
        public void KeywordClassifier_MapsPlayerMood(string text, Emotion expected)
        {
            Assert.AreEqual(expected, EmotionParser.ClassifyPlayerText(text));
        }

        [Test]
        public void ToJson_RoundTrips()
        {
            var reply = new ParsedReply { emotion = Emotion.Sad, text = "Эх..." };
            ParsedReply back = EmotionParser.Parse(EmotionParser.ToJson(reply), "");
            Assert.AreEqual(Emotion.Sad, back.emotion);
            Assert.AreEqual("Эх...", back.text);
        }
    }
}
