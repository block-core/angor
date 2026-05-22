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
    /// Proves that UpsertAsync correctly uses the provided delegate to extract
    /// the document Id for each entity, so each entity gets its own correct wrapper Id.
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

        // Act
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
}
