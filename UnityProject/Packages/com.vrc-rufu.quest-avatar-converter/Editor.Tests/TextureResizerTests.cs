using NUnit.Framework;
using VrcRufu.QuestAvatarConverter.Textures;

namespace VrcRufu.QuestAvatarConverter.Tests
{
    // T024: EditMode tests for TextureResizer (T023) resize math.
    public class TextureResizerTests
    {
        [Test]
        public void OversizedSquare_IsDownscaledToDefaultMax_PreservingAspectRatio()
        {
            var (width, height) = TextureResizer.ComputeTargetSize(2048, 2048, TextureResizer.DefaultMaxSize);

            Assert.AreEqual(1024, width);
            Assert.AreEqual(1024, height);
        }

        [Test]
        public void OversizedNonSquare_IsDownscaledPreservingAspectRatio()
        {
            var (width, height) = TextureResizer.ComputeTargetSize(4096, 2048, TextureResizer.DefaultMaxSize);

            Assert.AreEqual(1024, width);
            Assert.AreEqual(512, height);
        }

        [Test]
        public void UndersizedTexture_IsNeverUpscaled()
        {
            var (width, height) = TextureResizer.ComputeTargetSize(256, 128, TextureResizer.DefaultMaxSize);

            Assert.AreEqual(256, width);
            Assert.AreEqual(128, height);
        }

        [Test]
        public void TextureExactlyAtMax_IsUnchanged()
        {
            var (width, height) = TextureResizer.ComputeTargetSize(1024, 1024, TextureResizer.DefaultMaxSize);

            Assert.AreEqual(1024, width);
            Assert.AreEqual(1024, height);
        }

        [Test]
        public void CustomMaxSize_IsRespected()
        {
            var (width, height) = TextureResizer.ComputeTargetSize(2048, 1024, 512);

            Assert.AreEqual(512, width);
            Assert.AreEqual(256, height);
        }

        [Test]
        public void CustomMaxSize_DoesNotUpscaleSmallerTexture()
        {
            var (width, height) = TextureResizer.ComputeTargetSize(128, 64, 512);

            Assert.AreEqual(128, width);
            Assert.AreEqual(64, height);
        }
    }
}
