using System.IO;

namespace LuoTianyiPet.App;

internal static class RuntimeAssetLocator
{
    private const string AssetsDirectoryName = "assets";

    internal static string AssetsRoot => Path.Combine(AppContext.BaseDirectory, AssetsDirectoryName);
    internal static string AnimationCatalogPath => Path.Combine(AssetsRoot, "manifests", "animations.json");

    internal static string Audio(string fileName) => Path.Combine(AssetsRoot, "audio", fileName);
    internal static string BodyHitMap(string fileName) => Path.Combine(AssetsRoot, "body-hit-maps", fileName);
    internal static string Cursor(string fileName) => Path.Combine(AssetsRoot, "cursors", fileName);
    internal static string Object(string fileName) => Path.Combine(AssetsRoot, "objects", fileName);

    internal static Uri PackUri(string relativeAssetPath) =>
        new($"pack://application:,,,/{AssetsDirectoryName}/{relativeAssetPath}", UriKind.Absolute);
}
