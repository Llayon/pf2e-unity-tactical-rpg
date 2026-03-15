using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PF2e.Core;
using PF2e.Data;
using PF2e.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PF2e.Tests
{
    [TestFixture]
    public class InitiativePortraitTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;
        private const string SlotPrefabPath = "Assets/Prefabs/InitiativeSlot.prefab";

        [Test]
        public void EncounterActorPortraitLibrary_Resolve_UsesExactEncounterActorId()
        {
            var library = ScriptableObject.CreateInstance<EncounterActorPortraitLibrary>();
            try
            {
                var fighterPortrait = CreateSprite(24, 40, "fighter");
                var goblinPortrait = CreateSprite(32, 32, "goblin");
                var entries = new List<EncounterActorPortraitEntry>
                {
                    new EncounterActorPortraitEntry { actorId = "fighter", portraitSprite = fighterPortrait },
                    new EncounterActorPortraitEntry { actorId = "goblin_1", portraitSprite = goblinPortrait },
                };
                SetPrivateField(library, "entries", entries);

                Assert.AreSame(fighterPortrait, library.Resolve(" fighter "));
                Assert.AreSame(goblinPortrait, library.Resolve("goblin_1"));
                Assert.IsNull(library.Resolve("goblin_2"));
            }
            finally
            {
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void DuplicateGrouping_GroupsTrailingOrdinalSuffixes_AndAssignsOrdinalsInVisibleOrder()
        {
            var buildMethod = typeof(InitiativeBarController)
                .GetMethods(StaticNonPublic)
                .FirstOrDefault(method =>
                    method.Name == "BuildDuplicateOrdinals" &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType.IsGenericType &&
                    method.GetParameters()[0].ParameterType.GetGenericArguments()[0] == typeof(EntityData));
            Assert.IsNotNull(buildMethod, "Expected BuildDuplicateOrdinals helper.");

            var fighter = new EntityData { Handle = new EntityHandle(1), Name = "Fighter", EncounterActorId = "fighter" };
            var goblin1 = new EntityData { Handle = new EntityHandle(2), Name = "Goblin_1", EncounterActorId = "goblin_1" };
            var goblin2 = new EntityData { Handle = new EntityHandle(3), Name = "Goblin_2", EncounterActorId = "goblin_2" };
            var goblin10 = new EntityData { Handle = new EntityHandle(4), Name = "Goblin_10", EncounterActorId = "goblin_10" };

            var result = buildMethod.Invoke(null, new object[] { new List<EntityData> { fighter, goblin1, goblin2, goblin10 } })
                as Dictionary<EntityHandle, int>;

            Assert.IsNotNull(result);
            Assert.IsFalse(result.ContainsKey(fighter.Handle));
            Assert.AreEqual(1, result[goblin1.Handle]);
            Assert.AreEqual(2, result[goblin2.Handle]);
            Assert.AreEqual(3, result[goblin10.Handle]);
        }

        [Test]
        public void SetupStatic_WithPortrait_EnablesPortraitMode_AndUsesPreparedArtLayout()
        {
            var slot = InstantiateSlotPrefab();
            try
            {
                var portrait = CreateSprite(808, 996, "portrait");
                var frame = CreateSprite(808, 996, "frame");

                slot.SetupStatic(new EntityHandle(1), "Goblin_1", Team.Enemy, portrait, frame, "1");

                var portraitImage = GetPrivateField<Image>(slot, "portraitImage");
                var portraitAspectFitter = GetPrivateField<AspectRatioFitter>(slot, "portraitAspectFitter");
                var portraitMaskRect = GetPrivateField<RectTransform>(slot, "portraitMaskRect");
                var nameText = GetPrivateField<TMPro.TMP_Text>(slot, "nameText");
                var hpBarFill = GetPrivateField<Image>(slot, "hpBarFill");
                var background = GetPrivateField<Image>(slot, "background");
                var duplicateBadgeRoot = GetPrivateField<GameObject>(slot, "duplicateBadgeRoot");
                var frameImage = GetPrivateField<Image>(slot, "frameImage");

                Assert.IsTrue(portraitImage.gameObject.activeSelf);
                Assert.AreEqual(AspectRatioFitter.AspectMode.None, portraitAspectFitter.aspectMode);
                Assert.IsFalse(portraitImage.preserveAspect);
                Assert.AreEqual(Vector2.zero, portraitMaskRect.offsetMin);
                Assert.AreEqual(Vector2.zero, portraitMaskRect.offsetMax);
                Assert.Greater(frameImage.transform.GetSiblingIndex(), portraitMaskRect.transform.GetSiblingIndex());
                Assert.IsFalse(nameText.gameObject.activeSelf);
                Assert.IsTrue(hpBarFill.transform.parent.gameObject.activeSelf);
                Assert.AreEqual(0f, background.color.a, 0.001f);
                Assert.IsTrue(duplicateBadgeRoot.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(slot.gameObject);
            }
        }

        [Test]
        public void SetHighlight_WithPortrait_UsesFrameTint_AndTopAlignedVisualRootScale()
        {
            var slot = InstantiateSlotPrefab();
            try
            {
                var portrait = CreateSprite(40, 60, "portrait");
                var frame = CreateSprite(60, 90, "frame");

                slot.SetupStatic(new EntityHandle(1), "Wizard", Team.Player, portrait, frame, "2");
                slot.SetHighlight(true);

                var frameImage = GetPrivateField<Image>(slot, "frameImage");
                var activeHighlight = GetPrivateField<GameObject>(slot, "activeHighlight");
                var activeFrameColor = GetColorField(slot, "activeFrameColor");
                var activeScaleFactor = GetFloatField(slot, "activeScaleFactor");
                var slotRect = slot.GetComponent<RectTransform>();
                var visualRoot = GetPrivateField<RectTransform>(slot, "visualRoot");

                Assert.AreEqual(Vector3.one, slot.transform.localScale);
                Assert.AreEqual(Vector3.one * activeScaleFactor, visualRoot.localScale);
                Assert.AreEqual(activeFrameColor.r, frameImage.color.r, 0.001f);
                Assert.AreEqual(activeFrameColor.g, frameImage.color.g, 0.001f);
                Assert.AreEqual(activeFrameColor.b, frameImage.color.b, 0.001f);
                Assert.IsTrue(activeHighlight.activeSelf);
                Assert.AreEqual(new Vector2(0.5f, 1f), slotRect.pivot);
                Assert.AreEqual(new Vector2(0.5f, 1f), visualRoot.pivot);

                slot.SetHighlight(false);

                Assert.AreEqual(Vector3.one, slot.transform.localScale);
                Assert.AreEqual(Vector3.one, visualRoot.localScale);
                Assert.IsFalse(activeHighlight.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(slot.gameObject);
            }
        }

        [Test]
        public void SetActedThisRound_WithPortrait_DimsPortraitAndFrame()
        {
            var slot = InstantiateSlotPrefab();
            try
            {
                var portrait = CreateSprite(40, 60, "portrait");
                var frame = CreateSprite(60, 90, "frame");

                slot.SetupStatic(new EntityHandle(1), "Goblin_1", Team.Enemy, portrait, frame, "1");
                slot.SetActedThisRound(true);

                var frameImage = GetPrivateField<Image>(slot, "frameImage");
                var portraitImage = GetPrivateField<Image>(slot, "portraitImage");
                var actedFrameTint = GetColorField(slot, "actedFrameTint");
                var actedPortraitTint = GetColorField(slot, "actedPortraitTint");

                Assert.AreEqual(actedFrameTint.r, frameImage.color.r, 0.001f);
                Assert.AreEqual(actedFrameTint.g, frameImage.color.g, 0.001f);
                Assert.AreEqual(actedFrameTint.b, frameImage.color.b, 0.001f);
                Assert.AreEqual(actedPortraitTint.r, portraitImage.color.r, 0.001f);
                Assert.AreEqual(actedPortraitTint.g, portraitImage.color.g, 0.001f);
                Assert.AreEqual(actedPortraitTint.b, portraitImage.color.b, 0.001f);
                Assert.Less(frameImage.color.a, 1f);
                Assert.Less(portraitImage.color.a, 1f);
            }
            finally
            {
                Object.DestroyImmediate(slot.gameObject);
            }
        }

        [Test]
        public void SetDefeated_OverridesActiveFrameTint()
        {
            var slot = InstantiateSlotPrefab();
            try
            {
                var portrait = CreateSprite(40, 60, "portrait");
                var frame = CreateSprite(60, 90, "frame");

                slot.SetupStatic(new EntityHandle(1), "Goblin_2", Team.Enemy, portrait, frame, "2");
                slot.SetHighlight(true);
                slot.SetDefeated(true);

                var frameImage = GetPrivateField<Image>(slot, "frameImage");
                var portraitImage = GetPrivateField<Image>(slot, "portraitImage");
                var defeatedFrameTint = GetColorField(slot, "defeatedFrameTint");
                var defeatedPortraitTint = GetColorField(slot, "defeatedPortraitTint");

                Assert.AreEqual(defeatedFrameTint.r, frameImage.color.r, 0.001f);
                Assert.AreEqual(defeatedFrameTint.g, frameImage.color.g, 0.001f);
                Assert.AreEqual(defeatedFrameTint.b, frameImage.color.b, 0.001f);
                Assert.AreEqual(defeatedPortraitTint.r, portraitImage.color.r, 0.001f);
                Assert.AreEqual(defeatedPortraitTint.g, portraitImage.color.g, 0.001f);
                Assert.AreEqual(defeatedPortraitTint.b, portraitImage.color.b, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(slot.gameObject);
            }
        }

        [Test]
        public void RefreshHP_WithPortrait_UpdatesThinHpStrip_AndDamageOverlay()
        {
            var slot = InstantiateSlotPrefab();
            try
            {
                var portrait = CreateSprite(40, 60, "portrait");
                var frame = CreateSprite(60, 90, "frame");

                slot.SetupStatic(new EntityHandle(1), "Wizard", Team.Player, portrait, frame, null);
                slot.RefreshHP(5, 20, true);

                var hpBarFill = GetPrivateField<Image>(slot, "hpBarFill");
                var damageOverlay = GetPrivateField<Image>(slot, "damageOverlay");
                var lowHpColor = GetColorField(slot, "hpStripLowColor");
                var damageOverlayBaseColor = GetColorField(slot, "damageOverlayColor");

                Assert.IsTrue(hpBarFill.transform.parent.gameObject.activeSelf);
                Assert.AreEqual(0.25f, hpBarFill.rectTransform.anchorMax.x, 0.001f);
                Assert.AreEqual(lowHpColor.r, hpBarFill.color.r, 0.001f);
                Assert.IsTrue(damageOverlay.gameObject.activeSelf);
                Assert.AreEqual(0.75f, damageOverlay.rectTransform.anchorMax.y, 0.001f);
                Assert.AreEqual(damageOverlayBaseColor.a * 0.75f, damageOverlay.color.a, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(slot.gameObject);
            }
        }

        [Test]
        public void SetupStatic_WithoutPortrait_KeepsLegacyNameAndHpVisible()
        {
            var slot = InstantiateSlotPrefab();
            try
            {
                slot.SetupStatic(new EntityHandle(1), "Fighter", Team.Player, null, null, null);

                var nameText = GetPrivateField<TMPro.TMP_Text>(slot, "nameText");
                var hpBarFill = GetPrivateField<Image>(slot, "hpBarFill");
                var duplicateBadgeRoot = GetPrivateField<GameObject>(slot, "duplicateBadgeRoot");

                Assert.IsTrue(nameText.gameObject.activeSelf);
                Assert.IsTrue(hpBarFill.transform.parent.gameObject.activeSelf);
                Assert.IsFalse(duplicateBadgeRoot.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(slot.gameObject);
            }
        }

        [Test]
        public void SlotPrefab_UsesPreparedArtDimensions()
        {
            var slot = InstantiateSlotPrefab();
            try
            {
                var layoutElement = slot.GetComponent<LayoutElement>();
                Assert.IsNotNull(layoutElement);
                Assert.AreEqual(64f, layoutElement.preferredWidth, 0.001f);
                Assert.AreEqual(86f, layoutElement.preferredHeight, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(slot.gameObject);
            }
        }

        [Test]
        public void GetVisualOffsetX_UsesRightPushLayout_ForActiveSlot()
        {
            const float slotWidth = 64f;
            const float activeScale = 1.3f;

            Assert.AreEqual(0f, InitiativeBarController.GetVisualOffsetX(0, 1, slotWidth, activeScale), 0.001f);
            Assert.AreEqual(9.6f, InitiativeBarController.GetVisualOffsetX(1, 1, slotWidth, activeScale), 0.001f);
            Assert.AreEqual(19.2f, InitiativeBarController.GetVisualOffsetX(2, 1, slotWidth, activeScale), 0.001f);
            Assert.AreEqual(19.2f, InitiativeBarController.GetVisualOffsetX(3, 1, slotWidth, activeScale), 0.001f);
        }

        private static InitiativeSlot InstantiateSlotPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
            Assert.IsNotNull(prefab, $"Failed to load prefab at {SlotPrefabPath}.");
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Assert.IsNotNull(instance, "Failed to instantiate initiative slot prefab.");
            return instance.GetComponent<InitiativeSlot>();
        }

        private static Sprite CreateSprite(int width, int height, string name)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = name;
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName} on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName) where T : class
        {
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName} on {target.GetType().Name}.");
            return field.GetValue(target) as T;
        }

        private static Color GetColorField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName} on {target.GetType().Name}.");
            return (Color)field.GetValue(target);
        }

        private static float GetFloatField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName} on {target.GetType().Name}.");
            return (float)field.GetValue(target);
        }
    }
}
