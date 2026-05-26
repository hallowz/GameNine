using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Voidborne.Tests.PlayMode
{
    [TestFixture]
    public class QuestSystemTests
    {
        private GameObject _managerGO;
        private QuestManager _manager;

        private QuestDefinition _simpleQuest;
        private QuestDefinition _prerequisiteQuest;
        private QuestDefinition _repeatableQuest;

        [SetUp]
        public void SetUp()
        {
            // Destroy any leftover singleton from a previous test.
            if (QuestManager.Instance != null)
                Object.Destroy(QuestManager.Instance.gameObject);

            _managerGO = new GameObject("TestQuestManager");
            _manager = _managerGO.AddComponent<QuestManager>();
            // Awake runs immediately on AddComponent, setting Instance.

            // --- Simple quest with no prerequisites ---
            _simpleQuest = ScriptableObject.CreateInstance<QuestDefinition>();
            _simpleQuest.questId = "quest_simple";
            _simpleQuest.questName = "Simple Quest";
            _simpleQuest.description = "A test quest with no prerequisites.";
            _simpleQuest.prerequisiteQuestIds = new List<string>();
            _simpleQuest.objectives = new List<QuestObjective>
            {
                new QuestObjective
                {
                    type = ObjectiveType.ReachLocation,
                    description = "Reach the test location",
                    targetCount = 1,
                    locationId = "test_loc"
                }
            };
            _simpleQuest.rewards = new List<ItemStack>();
            _simpleQuest.isMainStory = false;
            _simpleQuest.isRepeatable = false;

            // --- Quest that requires simpleQuest to be completed ---
            _prerequisiteQuest = ScriptableObject.CreateInstance<QuestDefinition>();
            _prerequisiteQuest.questId = "quest_locked";
            _prerequisiteQuest.questName = "Locked Quest";
            _prerequisiteQuest.description = "Requires quest_simple.";
            _prerequisiteQuest.prerequisiteQuestIds = new List<string> { "quest_simple" };
            _prerequisiteQuest.objectives = new List<QuestObjective>
            {
                new QuestObjective
                {
                    type = ObjectiveType.ReachLocation,
                    description = "Reach another location",
                    targetCount = 1,
                    locationId = "test_loc_2"
                }
            };
            _prerequisiteQuest.rewards = new List<ItemStack>();
            _prerequisiteQuest.isMainStory = false;
            _prerequisiteQuest.isRepeatable = false;

            // --- Repeatable quest ---
            _repeatableQuest = ScriptableObject.CreateInstance<QuestDefinition>();
            _repeatableQuest.questId = "quest_repeatable";
            _repeatableQuest.questName = "Repeatable Quest";
            _repeatableQuest.description = "Can be accepted again after completion.";
            _repeatableQuest.prerequisiteQuestIds = new List<string>();
            _repeatableQuest.objectives = new List<QuestObjective>
            {
                new QuestObjective
                {
                    type = ObjectiveType.KillEnemy,
                    description = "Defeat any enemy",
                    targetCount = 1
                }
            };
            _repeatableQuest.rewards = new List<ItemStack>();
            _repeatableQuest.isMainStory = false;
            _repeatableQuest.isRepeatable = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (_managerGO != null)
                Object.Destroy(_managerGO);

            if (_simpleQuest != null)
                Object.Destroy(_simpleQuest);
            if (_prerequisiteQuest != null)
                Object.Destroy(_prerequisiteQuest);
            if (_repeatableQuest != null)
                Object.Destroy(_repeatableQuest);

            // Clear static event subscribers to avoid leaking between tests.
            ClearStaticEvent(typeof(QuestManager), "OnQuestAccepted");
            ClearStaticEvent(typeof(QuestManager), "OnObjectiveUpdated");
            ClearStaticEvent(typeof(QuestManager), "OnQuestCompleted");
        }

        private static void ClearStaticEvent(System.Type type, string eventName)
        {
            var field = type.GetField(eventName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) field.SetValue(null, null);
        }

        // ---------------------------------------------------------------
        //  1. Singleton initializes
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator Singleton_Initializes()
        {
            yield return null;

            Assert.IsNotNull(QuestManager.Instance,
                "QuestManager.Instance should not be null after Awake.");
            Assert.AreEqual(_manager, QuestManager.Instance,
                "Instance should reference the manager we created.");
        }

        // ---------------------------------------------------------------
        //  2. CanAccept returns true — no prerequisites
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator CanAccept_ReturnsTrue_WhenNoPrerequisites()
        {
            yield return null;

            Assert.IsTrue(_manager.CanAccept(_simpleQuest),
                "CanAccept should return true for a quest with no prerequisites.");
        }

        // ---------------------------------------------------------------
        //  3. CanAccept returns false — unmet prerequisites
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator CanAccept_ReturnsFalse_WhenPrerequisitesNotMet()
        {
            yield return null;

            Assert.IsFalse(_manager.CanAccept(_prerequisiteQuest),
                "CanAccept should return false when prerequisite quests are not completed.");
        }

        // ---------------------------------------------------------------
        //  4. AcceptQuest adds to ActiveQuests
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator AcceptQuest_AddsToActiveQuests()
        {
            yield return null;

            bool accepted = _manager.AcceptQuest(_simpleQuest);

            Assert.IsTrue(accepted, "AcceptQuest should return true.");
            Assert.IsTrue(_manager.IsActive(_simpleQuest.questId),
                "Quest should be in ActiveQuests after acceptance.");
            Assert.IsNotNull(_manager.GetActiveInstance(_simpleQuest.questId),
                "GetActiveInstance should return a non-null QuestInstance.");
        }

        // ---------------------------------------------------------------
        //  5. AcceptQuest fires OnQuestAccepted
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator AcceptQuest_FiresOnQuestAccepted()
        {
            yield return null;

            QuestDefinition receivedDef = null;
            QuestManager.OnQuestAccepted += def => receivedDef = def;

            _manager.AcceptQuest(_simpleQuest);

            Assert.AreEqual(_simpleQuest, receivedDef,
                "OnQuestAccepted should fire with the accepted QuestDefinition.");
        }

        // ---------------------------------------------------------------
        //  6. CompleteQuest moves quest from active to completed
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator CompleteQuest_MovesFromActiveToCompleted()
        {
            yield return null;

            _manager.AcceptQuest(_simpleQuest);
            Assert.IsTrue(_manager.IsActive(_simpleQuest.questId));

            _manager.CompleteQuest(_simpleQuest.questId);

            Assert.IsFalse(_manager.IsActive(_simpleQuest.questId),
                "Quest should no longer be active after completion.");
            Assert.IsTrue(_manager.IsCompleted(_simpleQuest.questId),
                "Quest should be in CompletedIds after completion.");
        }

        [UnityTest]
        public IEnumerator CompleteQuest_FiresOnQuestCompleted()
        {
            yield return null;

            QuestDefinition completedDef = null;
            QuestManager.OnQuestCompleted += def => completedDef = def;

            _manager.AcceptQuest(_simpleQuest);
            _manager.CompleteQuest(_simpleQuest.questId);

            Assert.AreEqual(_simpleQuest, completedDef,
                "OnQuestCompleted should fire with the completed QuestDefinition.");
        }

        // ---------------------------------------------------------------
        //  7. IsCompleted / IsActive return correct values
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator IsCompleted_And_IsActive_ReturnCorrectValues()
        {
            yield return null;

            // Before accepting.
            Assert.IsFalse(_manager.IsActive(_simpleQuest.questId));
            Assert.IsFalse(_manager.IsCompleted(_simpleQuest.questId));

            // After accepting.
            _manager.AcceptQuest(_simpleQuest);
            Assert.IsTrue(_manager.IsActive(_simpleQuest.questId));
            Assert.IsFalse(_manager.IsCompleted(_simpleQuest.questId));

            // After completing.
            _manager.CompleteQuest(_simpleQuest.questId);
            Assert.IsFalse(_manager.IsActive(_simpleQuest.questId));
            Assert.IsTrue(_manager.IsCompleted(_simpleQuest.questId));
        }

        // ---------------------------------------------------------------
        //  8. Repeatable quests can be accepted again after completion
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator RepeatableQuest_CanBeAcceptedAgainAfterCompletion()
        {
            yield return null;

            // First pass.
            Assert.IsTrue(_manager.AcceptQuest(_repeatableQuest),
                "First acceptance should succeed.");
            _manager.CompleteQuest(_repeatableQuest.questId);
            Assert.IsFalse(_manager.IsActive(_repeatableQuest.questId));

            // Repeatable quest should NOT appear in CompletedIds (see QuestManager line 143).
            Assert.IsFalse(_manager.IsCompleted(_repeatableQuest.questId),
                "Repeatable quests should not be added to CompletedIds.");

            // Second pass — should be acceptable again.
            Assert.IsTrue(_manager.CanAccept(_repeatableQuest),
                "CanAccept should return true for a repeatable quest after completion.");
            Assert.IsTrue(_manager.AcceptQuest(_repeatableQuest),
                "Repeatable quest should be accepted a second time.");
            Assert.IsTrue(_manager.IsActive(_repeatableQuest.questId));
        }

        // ---------------------------------------------------------------
        //  Bonus: CanAccept returns true once prerequisites are met
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator CanAccept_ReturnsTrue_AfterPrerequisitesCompleted()
        {
            yield return null;

            // Prerequisite quest should be blocked initially.
            Assert.IsFalse(_manager.CanAccept(_prerequisiteQuest));

            // Complete the prerequisite.
            _manager.AcceptQuest(_simpleQuest);
            _manager.CompleteQuest(_simpleQuest.questId);

            Assert.IsTrue(_manager.CanAccept(_prerequisiteQuest),
                "CanAccept should return true after all prerequisite quests are completed.");
        }

        // ---------------------------------------------------------------
        //  Bonus: AcceptQuest rejects already-active quest
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator AcceptQuest_ReturnsFalse_WhenAlreadyActive()
        {
            yield return null;

            _manager.AcceptQuest(_simpleQuest);
            bool second = _manager.AcceptQuest(_simpleQuest);

            Assert.IsFalse(second,
                "AcceptQuest should return false when the quest is already active.");
        }
    }
}
