using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SimoSlime.Editor.Tests
{
    public class SceneIdsTests
    {
        private const string ImageGuid = "fe87c0e1cc204ed48ad3b37840f39efc";
        private const string ButtonGuid = "4e29b1a8efbd4b44bb3f3716e73f07ff";
        private const long RootsId = 9223372036854775807;

        // A Canvas with one PlayButton (Image + Button), written the way Unity saves a scene:
        // documents sorted by their random file IDs, so new IDs also mean a new order.
        private static string Scene(long canvas, long canvasRect, long button, long buttonRect, long image, long buttonScript)
        {
            var docs = new List<(long Id, string Text)>
            {
                (1, "--- !u!29 &1\nOcclusionCullingSettings:\n  m_ObjectHideFlags: 0\n"),
                (canvas, $"--- !u!1 &{canvas}\nGameObject:\n  m_Component:\n  - component: {{fileID: {canvasRect}}}\n  m_Name: Canvas\n"),
                (canvasRect, $"--- !u!224 &{canvasRect}\nRectTransform:\n  m_GameObject: {{fileID: {canvas}}}\n  m_Children:\n  - {{fileID: {buttonRect}}}\n  m_Father: {{fileID: 0}}\n"),
                (button, $"--- !u!1 &{button}\nGameObject:\n  m_Component:\n  - component: {{fileID: {buttonRect}}}\n  - component: {{fileID: {image}}}\n  - component: {{fileID: {buttonScript}}}\n  m_Name: PlayButton\n"),
                (buttonRect, $"--- !u!224 &{buttonRect}\nRectTransform:\n  m_GameObject: {{fileID: {button}}}\n  m_Children: []\n  m_Father: {{fileID: {canvasRect}}}\n"),
                (image, $"--- !u!114 &{image}\nMonoBehaviour:\n  m_GameObject: {{fileID: {button}}}\n  m_Script: {{fileID: 11500000, guid: {ImageGuid}, type: 3}}\n  m_Sprite: {{fileID: 10905, guid: 0000000000000000f000000000000000, type: 0}}\n"),
                (buttonScript, $"--- !u!114 &{buttonScript}\nMonoBehaviour:\n  m_GameObject: {{fileID: {button}}}\n  m_Script: {{fileID: 11500000, guid: {ButtonGuid}, type: 3}}\n  m_TargetGraphic: {{fileID: {image}}}\n"),
                (RootsId, $"--- !u!1660057539 &{RootsId}\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots:\n  - {{fileID: {canvasRect}}}\n"),
            };
            return "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n" + string.Concat(docs.OrderBy(d => d.Id).Select(d => d.Text));
        }

        private static string FirstSave()
        {
            return Scene(118970489, 118970490, 414048051, 414048052, 414048053, 414048054);
        }

        private static string SecondSave()
        {
            return Scene(1725134600, 1725134601, 566101600, 566101601, 566101602, 566101603);
        }

        // The anchor ID of the one document whose body contains the marker.
        private static string IdOfDocWith(string yaml, string marker)
        {
            foreach (string doc in Regex.Split(yaml, "(?m)^(?=--- )"))
            {
                if (doc.Contains(marker))
                {
                    return Regex.Match(doc, @"^--- !u!\d+ &(-?\d+)").Groups[1].Value;
                }
            }

            return null;
        }

        // Canvas > Hand > two Cards, each with a Label: twins under twins.
        private static string Hand(long labelA, long labelARect, long labelB, long labelBRect)
        {
            const long canvas = 300000001, canvasRect = 300000002, hand = 300000003, handRect = 300000004;
            const long cardA = 300000005, cardARect = 300000006, cardB = 300000007, cardBRect = 300000008;

            string GameObject(long id, long rect, string name) =>
                $"--- !u!1 &{id}\nGameObject:\n  m_Component:\n  - component: {{fileID: {rect}}}\n  m_Name: {name}\n";
            string Rect(long id, long owner, long father, params long[] children) =>
                $"--- !u!224 &{id}\nRectTransform:\n  m_GameObject: {{fileID: {owner}}}\n  m_Children:"
                + (children.Length == 0 ? " []\n" : "\n" + string.Concat(children.Select(c => $"  - {{fileID: {c}}}\n")))
                + $"  m_Father: {{fileID: {father}}}\n";

            var docs = new List<(long Id, string Text)>
            {
                (canvas, GameObject(canvas, canvasRect, "Canvas")),
                (canvasRect, Rect(canvasRect, canvas, 0, handRect)),
                (hand, GameObject(hand, handRect, "Hand")),
                (handRect, Rect(handRect, hand, canvasRect, cardARect, cardBRect)),
                (cardA, GameObject(cardA, cardARect, "Card")),
                (cardARect, Rect(cardARect, cardA, handRect, labelARect)),
                (cardB, GameObject(cardB, cardBRect, "Card")),
                (cardBRect, Rect(cardBRect, cardB, handRect, labelBRect)),
                (labelA, GameObject(labelA, labelARect, "Label")),
                (labelARect, Rect(labelARect, labelA, cardARect)),
                (labelB, GameObject(labelB, labelBRect, "Label")),
                (labelBRect, Rect(labelBRect, labelB, cardBRect)),
                (RootsId, $"--- !u!1660057539 &{RootsId}\nSceneRoots:\n  m_Roots:\n  - {{fileID: {canvasRect}}}\n"),
            };
            return string.Concat(docs.OrderBy(d => d.Id).Select(d => d.Text));
        }

        [Test]
        public void Twins_under_twins_keep_stable_ids()
        {
            // The second save mints the Labels' IDs in the opposite order.
            string first = SceneIds.Stabilise(Hand(400000001, 400000002, 500000001, 500000002));
            string second = SceneIds.Stabilise(Hand(500000001, 500000002, 400000001, 400000002));

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Same_scene_twice_gives_same_text()
        {
            Assert.That(SceneIds.Stabilise(SecondSave()), Is.EqualTo(SceneIds.Stabilise(FirstSave())));
        }

        [Test]
        public void References_follow_their_objects()
        {
            string stable = SceneIds.Stabilise(FirstSave());

            string imageId = IdOfDocWith(stable, "guid: " + ImageGuid);
            string target = Regex.Match(stable, @"m_TargetGraphic: \{fileID: (-?\d+)\}").Groups[1].Value;
            Assert.That(target, Is.EqualTo(imageId));
            Assert.That(imageId, Is.Not.EqualTo("414048053"));
        }

        [Test]
        public void External_references_untouched()
        {
            string stable = SceneIds.Stabilise(FirstSave());

            StringAssert.Contains("m_Script: {fileID: 11500000, guid: " + ImageGuid + ", type: 3}", stable);
            StringAssert.Contains("m_Sprite: {fileID: 10905, guid: 0000000000000000f000000000000000, type: 0}", stable);
        }

        [Test]
        public void Settings_and_roots_keep_fixed_ids()
        {
            string stable = SceneIds.Stabilise(FirstSave());

            StringAssert.StartsWith("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!29 &1\n", stable);
            StringAssert.Contains("--- !u!1660057539 &" + RootsId + "\nSceneRoots:", stable);
            string canvasRect = IdOfDocWith(stable, "m_Father: {fileID: 0}");
            StringAssert.EndsWith("m_Roots:\n  - {fileID: " + canvasRect + "}\n", stable);
        }
    }
}
