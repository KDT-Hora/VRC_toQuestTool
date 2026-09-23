using NUnit.Framework;
using UnityEngine;
using VrcRufu.QuestAvatarConverter.Pipeline;
using VrcRufu.QuestAvatarConverter.Textures;

namespace VrcRufu.QuestAvatarConverter.Tests
{
    // T022: EditMode tests for TextureAtlasGenerator (T021) placement math. Uses PCTexture
    // instances with a null Asset (pure dimension data) since this logic never dereferences the
    // underlying Texture2D — only Width/Height.
    public class TextureAtlasPlacementTests
    {
        [Test]
        public void SingleTexture_IsNoOpLayout()
        {
            var texture = new PCTexture(null, 512, 256, false);

            var layout = TextureAtlasGenerator.Generate(new[] { texture });

            Assert.AreEqual(512, layout.Width);
            Assert.AreEqual(256, layout.Height);
            Assert.AreEqual(1, layout.Placements.Count);
            Assert.AreEqual(new RectInt(0, 0, 512, 256), layout.Placements[0].DestRect);
        }

        [Test]
        public void MismatchedSizes_PreserveEachSourceAspectRatio()
        {
            var large = new PCTexture(null, 2048, 2048, false);
            var narrow = new PCTexture(null, 1024, 2048, false);

            var layout = TextureAtlasGenerator.Generate(new[] { large, narrow });

            Assert.AreEqual(2, layout.Placements.Count);

            foreach (var placement in layout.Placements)
            {
                // Aspect ratio is preserved by construction: each placement's DestRect uses the
                // source's own native width/height, never a stretched size.
                Assert.AreEqual(placement.SourceTexture.Width, placement.DestRect.width);
                Assert.AreEqual(placement.SourceTexture.Height, placement.DestRect.height);
            }

            var largePlacement = layout.Placements[0];
            var narrowPlacement = layout.Placements[1];
            Assert.AreSame(large, largePlacement.SourceTexture);
            Assert.AreSame(narrow, narrowPlacement.SourceTexture);

            // No overlap between the two placements' cells.
            Assert.IsTrue(
                largePlacement.DestRect.xMax <= narrowPlacement.DestRect.xMin ||
                narrowPlacement.DestRect.xMax <= largePlacement.DestRect.xMin ||
                largePlacement.DestRect.yMax <= narrowPlacement.DestRect.yMin ||
                narrowPlacement.DestRect.yMax <= largePlacement.DestRect.yMin);

            // The overall canvas is large enough to contain every placement.
            foreach (var placement in layout.Placements)
            {
                Assert.LessOrEqual(placement.DestRect.xMax, layout.Width);
                Assert.LessOrEqual(placement.DestRect.yMax, layout.Height);
            }
        }

        [Test]
        public void EmptyInput_ProducesEmptyLayout()
        {
            var layout = TextureAtlasGenerator.Generate(System.Array.Empty<PCTexture>());

            Assert.IsEmpty(layout.Placements);
            Assert.AreEqual(0, layout.Width);
            Assert.AreEqual(0, layout.Height);
        }
    }
}
