using System.Text.Json.Nodes;
using DevSync.Domain.Diagrams;

namespace DevSync.UnitTests.Domain.Diagrams;

/// <summary>Spec 004 FR-027–FR-029, research R4.</summary>
public sealed class DiagramUpgraderTests
{
    [Fact]
    public void The_current_version_passes_through_unchanged()
    {
        var doc = JsonNode.Parse("""{"schemaVersion":1,"nodes":[],"edges":[],"viewport":null}""")!.AsObject();

        var result = DiagramDocumentUpgrader.Production.Upgrade(doc);

        Assert.Equal(UpgradeStatus.Upgraded, result.Status);
        Assert.Equal(1, result.Document!["schemaVersion"]!.GetValue<int>());
    }

    [Fact]
    public void Registered_steps_upgrade_an_older_version()
    {
        // A test-only v0 format: components were called "boxes".
        var upgrader = new DiagramDocumentUpgrader(new Dictionary<int, Func<JsonObject, JsonObject>>
        {
            [0] = old =>
            {
                var next = old.DeepClone().AsObject();
                next["nodes"] = old["boxes"]!.DeepClone();
                next.Remove("boxes");
                return next;
            },
        });
        var v0 = JsonNode.Parse("""{"schemaVersion":0,"boxes":[],"edges":[],"viewport":null}""")!.AsObject();

        var result = upgrader.Upgrade(v0);

        Assert.Equal(UpgradeStatus.Upgraded, result.Status);
        Assert.Equal(1, result.Document!["schemaVersion"]!.GetValue<int>());
        Assert.NotNull(result.Document["nodes"]);
        Assert.Null(result.Document["boxes"]);
    }

    [Theory]
    [InlineData("""{"schemaVersion":2,"nodes":[]}""", 2)]
    [InlineData("""{"schemaVersion":99}""", 99)]
    [InlineData("""{"schemaVersion":0,"nodes":[]}""", 0)]
    public void Newer_versions_and_versions_without_a_path_are_unsupported(string json, int version)
    {
        var result = DiagramDocumentUpgrader.Production.Upgrade(JsonNode.Parse(json)!.AsObject());

        Assert.Equal(UpgradeStatus.Unsupported, result.Status);
        Assert.Equal(version, result.Version);
        Assert.Null(result.Document);
    }

    [Theory]
    [InlineData("""{"nodes":[]}""")]
    [InlineData("""{"schemaVersion":"one"}""")]
    [InlineData("""{"schemaVersion":1.5}""")]
    public void Documents_without_a_readable_version_are_unreadable(string json)
    {
        var result = DiagramDocumentUpgrader.Production.Upgrade(JsonNode.Parse(json)!.AsObject());

        Assert.Equal(UpgradeStatus.Unreadable, result.Status);
    }
}

/// <summary>Spec 004 FR-004.</summary>
public sealed class DiagramNameTests
{
    [Fact]
    public void Names_are_trimmed_and_normalized()
    {
        var name = DiagramName.Create("  Checkout  ").Value;

        Assert.Equal("Checkout", name.Value);
        Assert.Equal("CHECKOUT", name.Normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a\u0001b")]
    public void Missing_blank_or_control_names_are_rejected(string? input)
    {
        Assert.Equal("name", Assert.Single(DiagramName.Create(input).Errors).Field);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Names_are_at_most_100_characters(int length, bool valid)
    {
        Assert.Equal(valid, DiagramName.Create(new string('d', length)).IsSuccess);
    }
}
