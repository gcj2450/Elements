using System.IO;
using Elements.Flow;
using Elements.Geometry;
using Elements.Fittings;
using Elements.Serialization.glTF;

namespace Elements.MEP.Tests
{

    public class TestUtils
    {
        public static string GetTestPath(string directoryName = null)
        {
            var path = "../../../TestResults/";
            if (directoryName != null)
            {
                path = Path.Combine(path, directoryName);
            }
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        public static void SaveToGltf(string testName, Element[] elements, string directory = "./", bool saveDot = false)
        {
            var dir = TestUtils.GetTestPath(directory);
            var path = Path.Join(dir, $"{testName}.gltf");
            var model = new Model();
            var i = 0;
            foreach (var element in elements)
            {
                model.AddElement(element);
                if (saveDot && element is FittingTree net)
                {
                    var p = Path.Join(dir, $"{testName}_{i}.dot");
                    File.WriteAllText(p, net.ToDot());
                }
            }
            model.ToGlTF(path, false);
        }
        public static void SaveToGltf(string testName, Element element, string directory = "./", bool saveDot = false)
        {
            SaveToGltf(testName, new[] { element }, directory, saveDot);
        }
    }
}