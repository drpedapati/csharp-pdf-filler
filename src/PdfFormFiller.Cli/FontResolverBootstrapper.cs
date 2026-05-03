using PdfSharp.Fonts;

namespace PdfFormFiller.Cli;

internal static class FontResolverBootstrapper
{
    private static int _initialized;

    public static void EnsureConfigured()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = new CrossPlatformFontResolver();
        }
    }

    private sealed class CrossPlatformFontResolver : IFontResolver
    {
        private static readonly IReadOnlyDictionary<string, string[]> FontPaths =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["arial#regular"] = [
                    "/System/Library/Fonts/Supplemental/Arial.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                    "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
                    "/usr/share/fonts/truetype/msttcorefonts/Arial.ttf",
                    @"C:\Windows\Fonts\arial.ttf",
                ],
                ["arial#bold"] = [
                    "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                    "/usr/share/fonts/truetype/liberation2/LiberationSans-Bold.ttf",
                    "/usr/share/fonts/truetype/msttcorefonts/Arial_Bold.ttf",
                    @"C:\Windows\Fonts\arialbd.ttf",
                ],
                ["arial#italic"] = [
                    "/System/Library/Fonts/Supplemental/Arial Italic.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Oblique.ttf",
                    "/usr/share/fonts/truetype/liberation2/LiberationSans-Italic.ttf",
                    "/usr/share/fonts/truetype/msttcorefonts/Arial_Italic.ttf",
                    @"C:\Windows\Fonts\ariali.ttf",
                ],
                ["arial#bolditalic"] = [
                    "/System/Library/Fonts/Supplemental/Arial Bold Italic.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans-BoldOblique.ttf",
                    "/usr/share/fonts/truetype/liberation2/LiberationSans-BoldItalic.ttf",
                    "/usr/share/fonts/truetype/msttcorefonts/Arial_Bold_Italic.ttf",
                    @"C:\Windows\Fonts\arialbi.ttf",
                ],
                ["couriernew#regular"] = [
                    "/System/Library/Fonts/Supplemental/Courier New.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf",
                    "/usr/share/fonts/truetype/liberation2/LiberationMono-Regular.ttf",
                    @"C:\Windows\Fonts\cour.ttf",
                ],
                ["couriernew#bold"] = [
                    "/System/Library/Fonts/Supplemental/Courier New Bold.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Bold.ttf",
                    "/usr/share/fonts/truetype/liberation2/LiberationMono-Bold.ttf",
                    @"C:\Windows\Fonts\courbd.ttf",
                ],
                ["couriernew#italic"] = [
                    "/System/Library/Fonts/Supplemental/Courier New Italic.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSansMono-Oblique.ttf",
                    "/usr/share/fonts/truetype/liberation2/LiberationMono-Italic.ttf",
                    @"C:\Windows\Fonts\couri.ttf",
                ],
                ["couriernew#bolditalic"] = [
                    "/System/Library/Fonts/Supplemental/Courier New Bold Italic.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSansMono-BoldOblique.ttf",
                    "/usr/share/fonts/truetype/liberation2/LiberationMono-BoldItalic.ttf",
                    @"C:\Windows\Fonts\courbi.ttf",
                ],
                ["timesnewroman#regular"] = [
                    "/System/Library/Fonts/Supplemental/Times New Roman.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf",
                    "/usr/share/fonts/truetype/liberation2/LiberationSerif-Regular.ttf",
                    @"C:\Windows\Fonts\times.ttf",
                ],
                ["timesnewroman#bold"] = [
                    "/System/Library/Fonts/Supplemental/Times New Roman Bold.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf",
                    "/usr/share/fonts/truetype/liberation2/LiberationSerif-Bold.ttf",
                    @"C:\Windows\Fonts\timesbd.ttf",
                ],
                ["timesnewroman#italic"] = [
                    "/System/Library/Fonts/Supplemental/Times New Roman Italic.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSerif-Italic.ttf",
                    "/usr/share/fonts/truetype/liberation2/LiberationSerif-Italic.ttf",
                    @"C:\Windows\Fonts\timesi.ttf",
                ],
                ["timesnewroman#bolditalic"] = [
                    "/System/Library/Fonts/Supplemental/Times New Roman Bold Italic.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSerif-BoldItalic.ttf",
                    "/usr/share/fonts/truetype/liberation2/LiberationSerif-BoldItalic.ttf",
                    @"C:\Windows\Fonts\timesbi.ttf",
                ],
                ["verdana#regular"] = [
                    "/System/Library/Fonts/Supplemental/Verdana.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                    "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
                    @"C:\Windows\Fonts\verdana.ttf",
                ],
                ["verdana#bold"] = [
                    "/System/Library/Fonts/Supplemental/Verdana Bold.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                    "/usr/share/fonts/truetype/liberation2/LiberationSans-Bold.ttf",
                    @"C:\Windows\Fonts\verdanab.ttf",
                ],
                ["verdana#italic"] = [
                    "/System/Library/Fonts/Supplemental/Verdana Italic.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Oblique.ttf",
                    "/usr/share/fonts/truetype/liberation2/LiberationSans-Italic.ttf",
                    @"C:\Windows\Fonts\verdanai.ttf",
                ],
                ["verdana#bolditalic"] = [
                    "/System/Library/Fonts/Supplemental/Verdana Bold Italic.ttf",
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans-BoldOblique.ttf",
                    "/usr/share/fonts/truetype/liberation2/LiberationSans-BoldItalic.ttf",
                    @"C:\Windows\Fonts\verdanaz.ttf",
                ],
            };

        public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic)
        {
            string familyKey = NormalizeFamilyName(familyName);
            string styleKey = bold && italic
                ? "bolditalic"
                : bold
                    ? "bold"
                    : italic
                        ? "italic"
                        : "regular";

            string faceName = $"{familyKey}#{styleKey}";
            string resolvedFaceName = ResolveExistingFaceName(faceName)
                ?? ResolveExistingFaceName($"{familyKey}#regular")
                ?? ResolveExistingFaceName("arial#regular")
                ?? ResolveFirstExistingFaceName()
                ?? throw new InvalidOperationException("No supported TrueType fonts were found. Install DejaVu, Liberation, Arial, Courier New, Times New Roman, or Verdana fonts.");

            return new FontResolverInfo(resolvedFaceName);
        }

        public byte[] GetFont(string faceName)
        {
            string path = ResolveFontPath(faceName)
                ?? throw new InvalidOperationException($"Resolved font face is unavailable: {faceName}");
            return File.ReadAllBytes(path);
        }

        private static string? ResolveExistingFaceName(string faceName) =>
            ResolveFontPath(faceName) is null ? null : faceName;

        private static string? ResolveFirstExistingFaceName()
        {
            foreach (string faceName in FontPaths.Keys)
            {
                if (ResolveFontPath(faceName) is not null)
                {
                    return faceName;
                }
            }

            return null;
        }

        private static string? ResolveFontPath(string faceName)
        {
            if (!FontPaths.TryGetValue(faceName, out string[]? candidatePaths))
            {
                return null;
            }

            return candidatePaths.FirstOrDefault(File.Exists);
        }

        private static string NormalizeFamilyName(string familyName)
        {
            string normalized = familyName
                .Trim()
                .ToLowerInvariant()
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty)
                .Replace("_", string.Empty);

            return normalized switch
            {
                "arialmt" => "arial",
                "helvetica" => "arial",
                "helveticaneue" => "arial",
                "helveticabold" => "arial",
                "helveticaoblique" => "arial",
                "courier" => "couriernew",
                "couriernewpsmt" => "couriernew",
                "timesroman" => "timesnewroman",
                "times" => "timesnewroman",
                "timesnewromanpsmt" => "timesnewroman",
                _ => normalized,
            };
        }
    }
}
