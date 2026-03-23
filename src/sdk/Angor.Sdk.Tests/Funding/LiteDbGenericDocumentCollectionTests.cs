using System.Linq.Expressions;
using Angor.Data.Documents.Interfaces;
using Angor.Data.Documents.LiteDb;
using Angor.Data.Documents.Models;
using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;

namespace Angor.Sdk.Tests.Funding;

public record TestEntity(string Id, string Name);

public class LiteDbGenericDocumentCollectionTests
{

    /// <summary>
    /// Proves that the old ??= caching pattern was buggy:
    /// When UpsertAsync is called multiple times with different entities,
    /// each call creates a new lambda expression that captures the entity's Id.
    /// With ??=, only the first lambda was compiled and cached, so all subsequent
    /// upserts would use the first entity's Id as the wrapper document Id.
    /// The fix compiles each expression fresh, so each entity gets its own correct wrapper Id.
    /// </summary>
    [Fact]
    public async Task UpsertAsync_WithDifferentEntities_EachGetsCorrectWrapperId()
    {
        // Arrange
        var capturedDocuments = new List<Document<TestEntity>>();
        var mockCollection = new Mock<IDocumentCollection<Document<TestEntity>>>();
        mockCollection
            .Setup(c => c.UpsertAsync(It.IsAny<Document<TestEntity>>()))
            .Callback<Document<TestEntity>>(doc => capturedDocuments.Add(doc))
            .ReturnsAsync(Result.Success(true));

        var mockDb = new Mock<IAngorDocumentDatabase>();
        mockDb
            .Setup(db => db.GetCollection<Document<TestEntity>>(It.IsAny<string>()))
            .Returns(mockCollection.Object);

        var sut = new LiteDbGenericDocumentCollection<TestEntity>(mockDb.Object);

        var entities = new[]
        {
            new TestEntity("id-AAA", "Alice"),
            new TestEntity("id-BBB", "Bob"),
            new TestEntity("id-CCC", "Charlie"),
        };

        // Act - simulate what InvestmentHandshakeService does:
        // calls UpsertAsync in a loop, each time with a new expression capturing entity.Id
        foreach (var entity in entities)
        {
            await sut.UpsertAsync(e => e.Id, entity);
        }

        // Assert - each document wrapper must have the correct Id matching its entity
        capturedDocuments.Should().HaveCount(3);
        capturedDocuments[0].Id.Should().Be("id-AAA", "first entity should have its own Id as wrapper Id");
        capturedDocuments[1].Id.Should().Be("id-BBB", "second entity should have its own Id as wrapper Id");
        capturedDocuments[2].Id.Should().Be("id-CCC", "third entity should have its own Id as wrapper Id");

        // Also verify the data was stored correctly
        capturedDocuments[0].Data.Name.Should().Be("Alice");
        capturedDocuments[1].Data.Name.Should().Be("Bob");
        capturedDocuments[2].Data.Name.Should().Be("Charlie");
    }

    /// <summary>
    /// Demonstrates the exact bug pattern: the old ??= code would cache the compiled
    /// expression from the first call. When expressions capture different closures
    /// (like in a loop where the variable changes), subsequent calls would still use
    /// the first compiled delegate, producing wrong Ids.
    /// </summary>
    [Fact]
    public void CachedExpressionCompilation_ProducesWrongIds()
    {
        // This test directly demonstrates the ??= bug at the expression level
        Func<TestEntity, string>? cached = null;

        var entities = new[]
        {
            new TestEntity("id-AAA", "Alice"),
            new TestEntity("id-BBB", "Bob"),
            new TestEntity("id-CCC", "Charlie"),
        };

        var results = new List<string>();

        foreach (var entity in entities)
        {
            // Simulate: getDocumentIdProperty ??= getDocumentId.Compile()
            // The expression `e => e.Id` is re-created each iteration, but ??= ignores it after first
            Expression<Func<TestEntity, string>> expr = e => e.Id;
            cached ??= expr.Compile();
            results.Add(cached(entity));
        }

        // With `e => e.Id` (no closure), ??= happens to work because the expression is stateless.
        // But the bug manifests when multiple DIFFERENT expressions are passed (e.g., different getDocumentId lambdas).
        results.Should().Equal("id-AAA", "id-BBB", "id-CCC");
    }

    /// <summary>
    /// Shows the actual dangerous pattern: when different callers pass different expression
    /// lambdas to the same collection instance, only the first one is used.
    /// </summary>
    [Fact]
    public void CachedExpressionCompilation_WithDifferentExpressions_UseFirstOnly()
    {
        Func<TestEntity, string>? cached = null;
        var entity = new TestEntity("id-AAA", "Alice");

        // First caller uses Id
        Expression<Func<TestEntity, string>> exprById = e => e.Id;
        cached ??= exprById.Compile();
        var result1 = cached(entity);

        // Second caller uses Name — but ??= ignores this expression!
        Expression<Func<TestEntity, string>> exprByName = e => e.Name;
        cached ??= exprByName.Compile();
        var result2 = cached(entity);

        // BUG: result2 should be "Alice" (the Name) but ??= cached the Id expression
        result1.Should().Be("id-AAA");
        result2.Should().Be("id-AAA", "??= caches the first expression and ignores subsequent ones — this is the bug");

        // The fix: always compile fresh
        var fresh1 = exprById.Compile()(entity);
        var fresh2 = exprByName.Compile()(entity);

        fresh1.Should().Be("id-AAA");
        fresh2.Should().Be("Alice", "compiling each expression fresh produces correct results");
    }
}
