using DevSync.Domain.Diagrams;

namespace DevSync.UnitTests.Domain.Diagrams;

/// <summary>Spec 004 FR-033: every structural rule, at its boundaries (research R5).</summary>
public sealed class DiagramValidatorTests
{
    private static DiagramNode Node(string id, string type = "microservice", string title = "Orders") =>
        new(id, type, title, string.Empty, [], new Point(0, 0), new Size(160, 80));

    private static DiagramEdge Edge(string id, string source, string target, string label = "") => new(id, source, target, label);

    private static DiagramDocument Doc(IEnumerable<DiagramNode>? nodes = null, IEnumerable<DiagramEdge>? edges = null, Viewport? viewport = null) =>
        new(DiagramSchema.CurrentVersion, [.. nodes ?? []], [.. edges ?? []], viewport);

    private static IReadOnlyList<DiagramProblem> Validate(DiagramDocument doc) => DiagramValidator.Validate(doc);

    private static void AssertSingle(DiagramDocument doc, string code, string path)
    {
        var problem = Assert.Single(Validate(doc));
        Assert.Equal(code, problem.Code);
        Assert.Equal(path, problem.Path);
        Assert.False(string.IsNullOrWhiteSpace(problem.Message));
    }

    [Fact]
    public void An_empty_document_is_valid()
    {
        Assert.Empty(Validate(DiagramDocument.Empty));
    }

    [Fact]
    public void A_typical_document_is_valid()
    {
        var doc = Doc(
            [Node("client", "client", "Web"), Node("gw", "api_gateway", "Gateway"), Node("db", "database", "Orders DB")],
            [Edge("e1", "client", "gw", "HTTPS"), Edge("e2", "gw", "db"), Edge("e3", "db", "gw")],
            new Viewport(-120, 40, 1.25));

        Assert.Empty(Validate(doc));
    }

    [Theory]
    [InlineData("client")]
    [InlineData("api_gateway")]
    [InlineData("microservice")]
    [InlineData("database")]
    [InlineData("cache")]
    [InlineData("message_queue")]
    [InlineData("load_balancer")]
    [InlineData("external_service")]
    public void Every_component_type_is_accepted(string type)
    {
        Assert.Empty(Validate(Doc([Node("n", type)])));
    }

    [Theory]
    [InlineData("mainframe")]
    [InlineData("Database")]
    [InlineData("")]
    public void Unknown_types_are_rejected(string type)
    {
        AssertSingle(Doc([Node("n", type)]), "unknown_node_type", "nodes[0].type");
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("a/b")]
    [InlineData("é")]
    public void Ids_must_be_short_safe_strings(string id)
    {
        AssertSingle(Doc([Node(id)]), "invalid_id", "nodes[0].id");
    }

    [Fact]
    public void Ids_of_64_characters_are_allowed_but_not_65()
    {
        Assert.Empty(Validate(Doc([Node(new string('a', 64))])));
        AssertSingle(Doc([Node(new string('a', 65))]), "invalid_id", "nodes[0].id");
    }

    [Fact]
    public void Duplicate_node_ids_are_rejected()
    {
        AssertSingle(Doc([Node("n1"), Node("n2"), Node("n1")]), "duplicate_node_id", "nodes[2].id");
    }

    [Fact]
    public void Duplicate_edge_ids_and_ids_shared_with_a_node_are_rejected()
    {
        AssertSingle(Doc([Node("a"), Node("b")], [Edge("e", "a", "b"), Edge("e", "b", "a")]), "duplicate_edge_id", "edges[1].id");
        AssertSingle(Doc([Node("a"), Node("b")], [Edge("a", "a", "b")]), "duplicate_edge_id", "edges[0].id");
    }

    [Fact]
    public void Edges_must_reference_nodes_of_the_document()
    {
        var problems = Validate(Doc([Node("a")], [Edge("e7", "a", "n12")]));

        var problem = Assert.Single(problems);
        Assert.Equal("unknown_node_reference", problem.Code);
        Assert.Equal("edges[0].target", problem.Path);
        Assert.Contains("e7", problem.Message, StringComparison.Ordinal);
        Assert.Contains("n12", problem.Message, StringComparison.Ordinal);
        AssertSingle(Doc([Node("a")], [Edge("e", "ghost", "a")]), "unknown_node_reference", "edges[0].source");
    }

    [Fact]
    public void Self_connections_are_rejected()
    {
        AssertSingle(Doc([Node("a")], [Edge("e", "a", "a")]), "self_connection", "edges[0]");
    }

    [Fact]
    public void Two_edges_in_the_same_direction_are_rejected_but_both_directions_are_fine()
    {
        AssertSingle(Doc([Node("a"), Node("b")], [Edge("e1", "a", "b"), Edge("e2", "a", "b")]), "duplicate_edge", "edges[1]");
        Assert.Empty(Validate(Doc([Node("a"), Node("b")], [Edge("e1", "a", "b"), Edge("e2", "b", "a")])));
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void At_most_500_nodes(int count, bool valid)
    {
        var doc = Doc(Enumerable.Range(0, count).Select(i => Node($"n{i}")));

        if (valid)
        {
            Assert.Empty(Validate(doc));
        }
        else
        {
            AssertSingle(doc, "too_many_nodes", "nodes");
        }
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void At_most_1000_edges(int count, bool valid)
    {
        // 50 nodes give 2,450 possible directed pairs.
        var nodes = Enumerable.Range(0, 50).Select(i => Node($"n{i}")).ToList();
        var pairs = from s in nodes from t in nodes where s.Id != t.Id select (s.Id, t.Id);
        var doc = Doc(nodes, pairs.Take(count).Select((p, i) => Edge($"e{i}", p.Item1, p.Item2)));

        if (valid)
        {
            Assert.Empty(Validate(doc));
        }
        else
        {
            AssertSingle(doc, "too_many_edges", "edges");
        }
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Titles_are_1_to_100_characters(int length, bool valid)
    {
        var doc = Doc([Node("n", title: new string('t', length))]);
        Assert.Equal(valid, Validate(doc).Count == 0);
        if (!valid)
        {
            AssertSingle(doc, "text_too_long", "nodes[0].title");
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Titles_are_required(string title)
    {
        AssertSingle(Doc([Node("n", title: title)]), "text_required", "nodes[0].title");
    }

    [Fact]
    public void Lengths_are_counted_in_code_points()
    {
        Assert.Empty(Validate(Doc([Node("n", title: string.Concat(Enumerable.Repeat("😀", 100)))])));
    }

    [Fact]
    public void Description_label_and_metadata_have_their_own_limits()
    {
        var longDescription = Node("n") with { Description = new string('d', 2001) };
        AssertSingle(Doc([longDescription]), "text_too_long", "nodes[0].description");

        Assert.Empty(Validate(Doc([Node("n") with { Description = new string('d', 2000) }])));
        AssertSingle(Doc([Node("a"), Node("b")], [Edge("e", "a", "b", new string('l', 201))]), "text_too_long", "edges[0].label");

        var meta = Node("n") with { Metadata = [new MetadataEntry(new string('k', 51), "v")] };
        AssertSingle(Doc([meta]), "text_too_long", "nodes[0].metadata[0].key");
        var value = Node("n") with { Metadata = [new MetadataEntry("k", new string('v', 501))] };
        AssertSingle(Doc([value]), "text_too_long", "nodes[0].metadata[0].value");
        var emptyKey = Node("n") with { Metadata = [new MetadataEntry("", "v")] };
        AssertSingle(Doc([emptyKey]), "text_required", "nodes[0].metadata[0].key");
    }

    [Fact]
    public void Metadata_holds_at_most_20_entries_with_unique_keys_ignoring_case()
    {
        var twenty = Node("n") with { Metadata = [.. Enumerable.Range(0, 20).Select(i => new MetadataEntry($"k{i}", "v"))] };
        Assert.Empty(Validate(Doc([twenty])));

        var many = Node("n") with { Metadata = [.. Enumerable.Range(0, 21).Select(i => new MetadataEntry($"k{i}", "v"))] };
        AssertSingle(Doc([many]), "too_many_metadata", "nodes[0].metadata");

        var duplicate = Node("n") with { Metadata = [new MetadataEntry("Owner", "a"), new MetadataEntry("OWNER", "b")] };
        AssertSingle(Doc([duplicate]), "duplicate_metadata_key", "nodes[0].metadata[1].key");
    }

    [Fact]
    public void Metadata_that_looks_like_code_is_just_text()
    {
        var node = Node("n") with
        {
            Metadata = [new MetadataEntry("__proto__", "<script>alert(1)</script>"), new MetadataEntry("constructor", "() => 1")],
        };

        Assert.Empty(Validate(Doc([node])));
    }

    [Theory]
    [InlineData("a\u0000b")]
    [InlineData("a\u0007b")]
    [InlineData("a\u007Fb")]
    [InlineData("a\nb")]
    public void Control_characters_are_rejected_in_titles(string title)
    {
        AssertSingle(Doc([Node("n", title: title)]), "invalid_text", "nodes[0].title");
    }

    [Fact]
    public void Lone_surrogates_are_rejected()
    {
        // Built in code: xUnit would re-encode a lone surrogate in [InlineData] as U+FFFD.
        var title = "a" + (char)0xD800 + "b";

        AssertSingle(Doc([Node("n", title: title)]), "invalid_text", "nodes[0].title");
    }

    [Fact]
    public void Descriptions_may_contain_line_breaks_and_tabs()
    {
        Assert.Empty(Validate(Doc([Node("n") with { Description = "line1\nline2\r\n\tindented" }])));
    }

    [Theory]
    [InlineData(39, 80, "nodes[0].size.width")]
    [InlineData(2001, 80, "nodes[0].size.width")]
    [InlineData(80, 39, "nodes[0].size.height")]
    [InlineData(80, 2001, "nodes[0].size.height")]
    public void Sizes_are_40_to_2000(double width, double height, string path)
    {
        AssertSingle(Doc([Node("n") with { Size = new Size(width, height) }]), "out_of_range", path);
    }

    [Fact]
    public void Sizes_at_the_bounds_are_fine()
    {
        Assert.Empty(Validate(Doc([Node("a") with { Size = new Size(40, 2000) }])));
    }

    [Theory]
    [InlineData(100_000.5, 0, "nodes[0].position.x")]
    [InlineData(0, -100_001, "nodes[0].position.y")]
    [InlineData(double.NaN, 0, "nodes[0].position.x")]
    [InlineData(double.PositiveInfinity, 0, "nodes[0].position.x")]
    public void Positions_stay_within_the_canvas(double x, double y, string path)
    {
        AssertSingle(Doc([Node("n") with { Position = new Point(x, y) }]), "out_of_range", path);
    }

    [Theory]
    [InlineData(0, 0, 0.09, "viewport.zoom")]
    [InlineData(0, 0, 4.01, "viewport.zoom")]
    [InlineData(10_000_001, 0, 1, "viewport.x")]
    [InlineData(0, double.NegativeInfinity, 1, "viewport.y")]
    public void The_viewport_is_limited(double x, double y, double zoom, string path)
    {
        AssertSingle(Doc(viewport: new Viewport(x, y, zoom)), "out_of_range", path);
    }

    [Fact]
    public void Several_problems_are_reported_together()
    {
        var doc = Doc([Node("a", "mainframe"), Node("a")], [Edge("e", "a", "missing")]);

        var codes = Validate(doc).Select(p => p.Code).ToList();

        Assert.Contains("unknown_node_type", codes);
        Assert.Contains("duplicate_node_id", codes);
        Assert.Contains("unknown_node_reference", codes);
    }

    [Fact]
    public void At_most_50_problems_are_reported()
    {
        var doc = Doc(Enumerable.Range(0, 200).Select(i => Node($"n{i}", "mainframe")));

        Assert.Equal(DiagramLimits.MaxProblems, Validate(doc).Count);
    }
}
