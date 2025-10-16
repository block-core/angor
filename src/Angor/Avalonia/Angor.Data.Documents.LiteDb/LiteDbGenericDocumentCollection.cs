using System.Linq.Expressions;
using Angor.Data.Documents.Interfaces;
using Angor.Data.Documents.Models;
using CSharpFunctionalExtensions;

namespace Angor.Data.Documents.LiteDb;

public class LiteDbGenericDocumentCollection<T> : IGenericDocumentCollection<T> 
    where T : class, IDocumentEntity
{
    private readonly IDocumentCollection<Document<T>> _documentCollection;

    public LiteDbGenericDocumentCollection(IAngorDocumentDatabase database)
    {
        _documentCollection = database.GetCollection<Document<T>>(typeof(T).Name);
    }

    public async Task<Result<T?>> FindByIdAsync(string id)
    {
        var result = await _documentCollection.FindByIdAsync(id);
        return result.Map(doc => doc?.Data);
    }

    public async Task<Result<IEnumerable<T>>> FindAllAsync()
    {
        var result = await _documentCollection.FindAllAsync();
        return result.Map(docs => docs.Select(d => d.Data));
    }

    public async Task<Result<IEnumerable<T>>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        // Convert T predicate to Document<T> predicate
        var parameter = Expression.Parameter(typeof(Document<T>), "doc");
        var dataProperty = Expression.Property(parameter, nameof(Document<T>.Data));
        var body = new PredicateRewriter(predicate.Parameters[0], dataProperty).Visit(predicate.Body);
        var documentPredicate = Expression.Lambda<Func<Document<T>, bool>>(body, parameter);

        var result = await _documentCollection.FindAsync(documentPredicate);
        return result.Map(docs => docs.Select(d => d.Data));
    }

    public async Task<Result<bool>> ExistsAsync(string id)
    {
        return await _documentCollection.ExistsAsync(id);
    }

    public async Task<Result<int>> InsertAsync(params T[] entities)
    {
        var documents = entities.Select(entity => 
            new Document<T>(entity, entity.GetDocumentId())).ToArray();
        
        return await _documentCollection.InsertAsync(documents);
    }

    public async Task<Result<bool>> UpdateAsync(T entity)
    {
        var document = new Document<T>(entity, entity.GetDocumentId());
        return await _documentCollection.UpdateAsync(document);
    }

    public async Task<Result<bool>> UpsertAsync(T entity)
    {
        var document = new Document<T>(entity, entity.GetDocumentId());
        return await _documentCollection.UpsertAsync(document);
    }

    public async Task<Result<bool>> DeleteAsync(string id)
    {
        return await _documentCollection.DeleteAsync(id);
    }

    public async Task<Result<int>> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        if (predicate == null)
        {
            return await _documentCollection.CountAsync();
        }

        // Convert predicate as above
        var parameter = Expression.Parameter(typeof(Document<T>), "doc");
        var dataProperty = Expression.Property(parameter, nameof(Document<T>.Data));
        var body = new PredicateRewriter(predicate.Parameters[0], dataProperty).Visit(predicate.Body);
        var documentPredicate = Expression.Lambda<Func<Document<T>, bool>>(body, parameter);

        return await _documentCollection.CountAsync(documentPredicate);
    }
    
    // Helper class for expression rewriting
    private class PredicateRewriter : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly Expression _newExpression;

        public PredicateRewriter(ParameterExpression oldParameter, Expression newExpression)
        {
            _oldParameter = oldParameter;
            _newExpression = newExpression;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _oldParameter ? _newExpression : node;
        }
    }
}