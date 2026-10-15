using System.Text.Json;
using Godot;

namespace Riftbound.GodotClient.Debug;

public partial class OfficialMultiTypeCatalogProof : Node
{
    public override async void _Ready()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            var fixture = args.Single(a => a.StartsWith("--catalog="))[10..];
            var output = args.Single(a => a.StartsWith("--receipt="))[10..];
            var service = new OfficialCardCatalogService();
            var catalog = await service.LoadSnapshotAsync(fixture);
            Check(catalog["VEN·058"].CardCategoryName == "单位 / 装备", "Actual multi-type source retains both native categories");
            // This proof checks metadata and preview privacy, without CDN requests.
            var noImage = catalog.ToDictionary(p => p.Key, p => p.Value with { FrontImage = "" });
            var factory = new CardViewFactory(new OfficialCardImageLoader());
            var visible = await factory.BuildAsync(new("DUAL", "VEN·058", true, false), noImage, default, false);
            Check(visible.Category == "单位 / 装备" && visible.PreviewSummary.Contains("单位 / 装备"), "Production preview receives both types");
            var hidden = await factory.BuildAsync(new("DUAL", "VEN·058", false, true), noImage, default, false);
            Check(hidden.Category.Length == 0 && !hidden.PreviewSummary.Contains("散装机械魄罗"), "Hidden preview does not expose identity or types");

            var current = await service.LoadSnapshotAsync("res://data/card-catalog.zh-CN.json");
            using var raw = JsonDocument.Parse(Godot.FileAccess.GetFileAsBytes("res://data/card-catalog.zh-CN.json"));
            var legacy = 0; var modern = 0;
            foreach (var card in raw.RootElement.GetProperty("cards").EnumerateArray())
            {
                var no = card.GetProperty("cardNo").GetString()!;
                if (card.TryGetProperty("cardCategoryNameList", out var names) && names.GetArrayLength() > 0)
                {
                    var expected = string.Join(" / ", names.EnumerateArray().Select(n => n.GetString()));
                    Check(current[no].CardCategoryName == expected, "Native metadata matches modern source: " + no); modern++;
                }
                else
                {
                    Check(current[no].CardCategoryName == card.GetProperty("cardCategoryName").GetString(), "Legacy native category preserved: " + no); legacy++;
                }
            }
            File.WriteAllText(output, JsonSerializer.Serialize(new {
                status = "ACTUAL_GODOT_MULTITYPE_CATALOG_AND_PREVIEW_METADATA_PASS", fixture,
                currentEntries = current.Count, legacy, modern, visible.Category, visible.PreviewSummary,
                hiddenIdentityProtected = true, runtimeCardPromotion = false,
                scope = "Production Godot catalog service and preview factory; no card gameplay or visible paired-client acceptance."
            }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"MULTITYPE_CATALOG_PASS: {current.Count} current entries, {legacy} legacy, {modern} array-category entries");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
