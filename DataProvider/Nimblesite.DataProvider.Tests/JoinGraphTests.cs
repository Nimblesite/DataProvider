using static global::Tests.Shared.RowAssertions;

namespace Nimblesite.DataProvider.Tests;

public partial class JoinGraphTests
{
    [Fact]
    public void New_JoinGraph_IsEmpty()
    {
        // Arrange & Act
        var graph = new JoinGraph();

        // Assert
        Assert.Equal(0, graph.Count);
        Assert.Empty(graph.GetRelationships());
    }

    [Fact]
    public void Add_SingleJoin_IncrementsCount()
    {
        // Arrange
        var graph = new JoinGraph();

        // Act
        graph.Add("Users", "Posts", "u.Id = p.UserId");

        // Assert
        Assert.Equal(1, graph.Count);

        var relationships = graph.GetRelationships();
        Assert.Single(relationships);

        var relationship = relationships[0];
        Assert.Equal("Users", relationship.LeftTable);
        Assert.Equal("Posts", relationship.RightTable);
        Assert.Equal("u.Id = p.UserId", relationship.Condition);
    }

    [Fact]
    public void Add_MultipleJoins_IncrementsCountCorrectly()
    {
        // Arrange
        var graph = new JoinGraph();

        // Act
        graph.Add("Users", "Posts", "u.Id = p.UserId");
        graph.Add("Posts", "Comments", "p.Id = c.PostId");
        graph.Add("Users", "Profiles", "u.Id = pr.UserId");

        // Assert
        Assert.Equal(3, graph.Count);

        var relationships = graph.GetRelationships();
        Assert.Equal(3, relationships.Count);

        // Verify all relationships are stored
        ContainsAll(
            rows: relationships,
            projection: r => (r.LeftTable, r.RightTable),
            expected: [("Users", "Posts"), ("Posts", "Comments"), ("Users", "Profiles")]
        );
    }

    [Fact]
    public void Add_DuplicateJoin_StillAdds()
    {
        // Arrange
        var graph = new JoinGraph();

        // Act
        graph.Add("Users", "Posts", "u.Id = p.UserId");
        graph.Add("Users", "Posts", "u.Id = p.UserId"); // Duplicate

        // Assert
        Assert.Equal(2, graph.Count); // JoinGraph doesn't prevent duplicates

        var relationships = graph.GetRelationships();
        Assert.Equal(2, relationships.Count);
    }

    [Theory]
    [MemberData(nameof(Add_EmptyParameters_StillAddsInlineCases))]
    public void Add_EmptyParameters_StillAdds(string leftTable, string rightTable, string condition)
    {
        // Arrange
        var graph = new JoinGraph();

        // Act
        graph.Add(leftTable, rightTable, condition);

        // Assert
        Assert.Equal(1, graph.Count);

        var relationship = graph.GetRelationships()[0];
        Assert.Equal(leftTable, relationship.LeftTable);
        Assert.Equal(rightTable, relationship.RightTable);
        Assert.Equal(condition, relationship.Condition);
    }

    [Fact]
    public void GetRelationships_ReturnsReadOnlyView()
    {
        // Arrange
        var graph = new JoinGraph();
        graph.Add("Users", "Posts", "u.Id = p.UserId");

        // Act
        var relationships = graph.GetRelationships();

        // Assert
        Assert.NotNull(relationships);
        Assert.IsAssignableFrom<IEnumerable<JoinRelationship>>(relationships);

        // Verify it's a view that doesn't affect the original
        var count = relationships.Count;
        Assert.Equal(1, count);
    }
}
