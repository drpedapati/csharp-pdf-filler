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
            GlobalFontSettings.FontResolver = new MacSystemFontResolver();
        }
    }

    private sealed class MacSystemFontResolver : IFontResolver
    {
        private static readonly IReadOnlyDictionary<string, string> FontPaths =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["arial#regular"] = "/System/Library/Fonts/Supplemental/Arial.ttf",
                ["arial#bold"] = "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
                ["arial#italic"] = "/System/Library/Fonts/Supplemental/Arial Italic.ttf",
                ["arial#bolditalic"] = "/System/Library/Fonts/Supplemental/Arial Bold Italic.ttf",
                ["couriernew#regular"] = "/System/Library/Fonts/Supplemental/Courier New.ttf",
                ["couriernew#bold"] = "/System/Library/Fonts/Supplemental/Courier New Bold.ttf",
                ["couriernew#italic"] = "/System/Library/Fonts/Supplemental/Courier New Italic.ttf",
                ["couriernew#bolditalic"] = "/System/Library/Fonts/Supplemental/Courier New Bold Italic.ttf",
                ["timesnewroman#regular"] = "/System/Library/Fonts/Supplemental/Times New Roman.ttf",
                ["timesnewroman#bold"] = "/System/Library/Fonts/Supplemental/Times New Roman Bold.ttf",
                ["timesnewroman#italic"] = "/System/Library/Fonts/Supplemental/Times New Roman Italic.ttf",
                ["timesnewroman#bolditalic"] = "/System/Library/Fonts/Supplemental/Times New Roman Bold Italic.ttf",
                ["verdana#regular"] = "/System/Library/Fonts/Supplemental/Verdana.ttf",
                ["verdana#bold"] = "/System/Library/Fonts/Supplemental/Verdana Bold.ttf",
                ["verdana#italic"] = "/System/Library/Fonts/Supplemental/Verdana Italic.ttf",
                ["verdana#bolditalic"] = "/System/Library/Fonts/Supplemental/Verdana Bold Italic.ttf",
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
            if (!FontPaths.ContainsKey(faceName))
            {
                faceName = $"{familyKey}#regular";
            }

            if (!FontPaths.ContainsKey(faceName))
            {
                faceName = "arial#regular";
            }

            return new FontResolverInfo(faceName);
        }

        public byte[] GetFont(string faceName) =>
            File.ReadAllBytes(FontPaths[faceName]);

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
