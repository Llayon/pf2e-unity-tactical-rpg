using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using PF2e.Presentation;

namespace PF2e.Tests
{
    [TestFixture]
    public class AidActionBarUiBootstrapperTests
    {
        private const BindingFlags InstancePublic = BindingFlags.Instance | BindingFlags.Public;

        [Test]
        public void ResolveOptionalReferences_MissingAidButton_DoesNotCreateAidButtonFromTemplate()
        {
            var root = new GameObject("ActionBarRoot", typeof(RectTransform));
            var ownerGo = new GameObject("Owner");
            ownerGo.transform.SetParent(root.transform, false);
            var owner = ownerGo.AddComponent<DummyOwner>();

            var escapeButton = CreateActionButton("EscapeButton", root.transform);
            var demoralizeButton = CreateActionButton("DemoralizeButton", root.transform);
            var strikeButton = CreateActionButton("StrikeButton", root.transform);

            Button aidButton = null;
            Image aidHighlight = null;
            GameObject aidBadge = null;
            object aidBadgeLabel = null;
            var bootstrapper = new AidActionBarUiBootstrapper();

            try
            {
                InvokeResolveOptionalReferences(
                    bootstrapper,
                    owner,
                    escapeButton,
                    demoralizeButton,
                    strikeButton,
                    ref aidButton,
                    ref aidHighlight,
                    ref aidBadge,
                    ref aidBadgeLabel,
                    Color.yellow,
                    Color.black);

                Assert.IsNull(aidButton, "Bootstrapper must not create AidButton at runtime.");
                Assert.IsNull(aidHighlight);
                Assert.IsNull(aidBadge);
                Assert.IsNull(aidBadgeLabel);
                Assert.IsNull(root.transform.Find("AidButton"), "AidButton should be authored/autofixed, not runtime-created.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResolveAidPreparedIndicatorReferences_MissingBadge_DoesNotCreateBadge()
        {
            var root = new GameObject("ActionBarRoot", typeof(RectTransform));
            var aidButton = CreateActionButton("AidButton", root.transform);

            GameObject aidBadge = null;
            object aidBadgeLabel = null;
            var bootstrapper = new AidActionBarUiBootstrapper();

            try
            {
                InvokeResolveAidPreparedIndicatorReferences(
                    bootstrapper,
                    aidButton,
                    ref aidBadge,
                    ref aidBadgeLabel,
                    Color.yellow,
                    Color.black);

                Assert.IsNull(aidBadge, "Bootstrapper must not create AidPreparedBadge at runtime.");
                Assert.IsNull(aidBadgeLabel);
                Assert.IsNull(aidButton.transform.Find("AidPreparedBadge"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Button CreateActionButton(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Button>();
        }

        private static void InvokeResolveOptionalReferences(
            AidActionBarUiBootstrapper bootstrapper,
            MonoBehaviour owner,
            Button escapeButton,
            Button demoralizeButton,
            Button strikeButton,
            ref Button aidButton,
            ref Image aidHighlight,
            ref GameObject aidBadge,
            ref object aidBadgeLabel,
            Color fill,
            Color label)
        {
            var method = typeof(AidActionBarUiBootstrapper).GetMethod("ResolveOptionalReferences", InstancePublic);
            Assert.IsNotNull(method, "ResolveOptionalReferences method not found.");

            object[] args =
            {
                owner,
                escapeButton,
                demoralizeButton,
                strikeButton,
                aidButton,
                aidHighlight,
                aidBadge,
                aidBadgeLabel,
                fill,
                label
            };
            method.Invoke(bootstrapper, args);
            aidButton = args[4] as Button;
            aidHighlight = args[5] as Image;
            aidBadge = args[6] as GameObject;
            aidBadgeLabel = args[7];
        }

        private static void InvokeResolveAidPreparedIndicatorReferences(
            AidActionBarUiBootstrapper bootstrapper,
            Button aidButton,
            ref GameObject aidBadge,
            ref object aidBadgeLabel,
            Color fill,
            Color label)
        {
            var method = typeof(AidActionBarUiBootstrapper).GetMethod("ResolveAidPreparedIndicatorReferences", InstancePublic);
            Assert.IsNotNull(method, "ResolveAidPreparedIndicatorReferences method not found.");

            object[] args =
            {
                aidButton,
                aidBadge,
                aidBadgeLabel,
                fill,
                label
            };
            method.Invoke(bootstrapper, args);
            aidBadge = args[1] as GameObject;
            aidBadgeLabel = args[2];
        }

        private sealed class DummyOwner : MonoBehaviour
        {
        }
    }
}
