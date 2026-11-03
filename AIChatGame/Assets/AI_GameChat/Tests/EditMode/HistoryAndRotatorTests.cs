using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIGameChat.Tests
{
    public class HistoryAndRotatorTests
    {
        [Test]
        public void History_KeepsSystemPromptAndLimitsTurns()
        {
            var history = new ChatHistory("SYSTEM", 10);
            for (int i = 0; i < 20; i++) history.Commit("q" + i, "a" + i);

            Assert.LessOrEqual(history.TurnCount, 10);

            List<ChatMessage> request = history.BuildRequest("новый вопрос");
            Assert.AreEqual("system", request[0].role);
            Assert.AreEqual("SYSTEM", request[0].content);
            Assert.AreEqual("user", request[1].role); // история всегда начинается с реплики игрока
            Assert.AreEqual("новый вопрос", request[request.Count - 1].content);
        }

        [Test]
        public void History_FailedRequestLeavesNoTrace()
        {
            var history = new ChatHistory("SYSTEM", 10);
            history.BuildRequest("сообщение, на которое не пришёл ответ");
            Assert.AreEqual(0, history.TurnCount);
        }

        [Test]
        public void Rotator_ReturnsEachModelOnce()
        {
            var go = new GameObject("rotator-test");
            try
            {
                var rotator = go.AddComponent<ModelRotator>();
                rotator.models = new List<string> { "a:free", "b:free", "c:free" };
                List<string> order = rotator.GetAttemptOrder();
                Assert.AreEqual(new List<string> { "a:free", "b:free", "c:free" }, order);
                Assert.AreEqual(3, rotator.MaxAttempts);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Rotator_StartsWithLastSuccessfulModel_StillOnePass()
        {
            var go = new GameObject("rotator-test");
            try
            {
                var rotator = go.AddComponent<ModelRotator>();
                rotator.models = new List<string> { "a:free", "b:free", "c:free" };
                rotator.MarkSuccess("b:free");
                Assert.AreEqual(new List<string> { "b:free", "c:free", "a:free" }, rotator.GetAttemptOrder());
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Rotator_ConfigOverrideReplacesListAndDropsDuplicatesAndBlanks()
        {
            var go = new GameObject("rotator-test");
            try
            {
                var rotator = go.AddComponent<ModelRotator>();
                List<string> order = rotator.GetAttemptOrder(new[] { "x:free", "", "x:free", "y:free" });
                Assert.AreEqual(new List<string> { "x:free", "y:free" }, order);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void PromptBuilder_AlwaysContainsFormatRules_EvenWithCustomPersonality()
        {
            var profile = ScriptableObject.CreateInstance<CharacterProfile>();
            profile.displayName = "Тест";
            profile.personality = "Базовый характер";
            string prompt = PromptBuilder.Build(profile, "Свой характер: ворчливый кот");
            StringAssert.Contains("ворчливый кот", prompt);
            StringAssert.DoesNotContain("Базовый характер", prompt);
            StringAssert.Contains("\"emotion\"", prompt);
            StringAssert.Contains("embarrassed", prompt);
            Object.DestroyImmediate(profile);
        }
    }
}
