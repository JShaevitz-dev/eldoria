using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Eldoria.Tests
{
    public class BuildScenesTests
    {
        [Test]
        public void TheGameOpensOnTheMainMenuAndIncludesTheDemoMapAndTestRoom()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Assert.That(scenes, Is.EqualTo(new[]
            {
                "Assets/_Project/Scenes/MainMenu.unity",
                "Assets/_Project/Scenes/UrbanDemo.unity",
                "Assets/_Project/Scenes/CubeRoom.unity",
            }));
        }
    }
}
