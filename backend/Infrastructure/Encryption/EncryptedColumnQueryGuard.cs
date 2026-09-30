using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace KeplerTalento.Infrastructure.Encryption;

/// <summary>
/// Refuses any query that filters, orders or groups on an encrypted property (KTL-33 design
/// decision 9).
/// </summary>
/// <remarks>
/// Such a query would not fail by itself: PostgreSQL would compare or sort ciphertext, and an
/// equality would encrypt the parameter under a fresh nonce and never match. The result is a
/// silently wrong answer. This interceptor sees every LINQ query before EF compiles it and throws
/// instead, naming the property, so the mistake surfaces in the first test that runs the query.
/// Projections (<c>Select</c>) are allowed: that is how encrypted values are read.
/// </remarks>
public sealed class EncryptedColumnQueryGuard : IQueryExpressionInterceptor
{
    public static readonly EncryptedColumnQueryGuard Instance = new();

    private static readonly HashSet<string> GuardedOperators = new(StringComparer.Ordinal)
    {
        nameof(Queryable.Where), nameof(Queryable.OrderBy), nameof(Queryable.OrderByDescending),
        nameof(Queryable.ThenBy), nameof(Queryable.ThenByDescending), nameof(Queryable.GroupBy),
        nameof(Queryable.Any), nameof(Queryable.All), nameof(Queryable.Count), nameof(Queryable.LongCount),
        nameof(Queryable.First), nameof(Queryable.FirstOrDefault), nameof(Queryable.Single),
        nameof(Queryable.SingleOrDefault), nameof(Queryable.Last), nameof(Queryable.LastOrDefault),
        nameof(Queryable.SkipWhile), nameof(Queryable.TakeWhile), nameof(Queryable.Distinct),
        nameof(Queryable.Min), nameof(Queryable.Max), nameof(Queryable.Join), nameof(Queryable.GroupJoin),
        "FirstAsync", "FirstOrDefaultAsync", "SingleAsync", "SingleOrDefaultAsync",
        "AnyAsync", "AllAsync", "CountAsync", "LongCountAsync", "MinAsync", "MaxAsync",
        "LastAsync", "LastOrDefaultAsync",
    };

    public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
    {
        var model = eventData.Context?.Model;
        if (model is not null)
        {
            new Visitor(model).Visit(queryExpression);
        }
        return queryExpression;
    }

    private sealed class Visitor(IModel model) : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (GuardedOperators.Contains(node.Method.Name)
                && (node.Method.DeclaringType == typeof(Queryable)
                    || node.Method.DeclaringType == typeof(Enumerable)
                    || node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions)))
            {
                foreach (var argument in node.Arguments.Skip(1))
                {
                    new LambdaInspector(model, node.Method.Name).Visit(argument);
                }
            }
            return base.VisitMethodCall(node);
        }
    }

    /// <summary>Looks for encrypted properties inside one operator's lambdas.</summary>
    private sealed class LambdaInspector(IModel model, string operatorName) : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Member is PropertyInfo && node.Expression is not null)
            {
                Check(node.Expression.Type, node.Member.Name);
            }
            return base.VisitMember(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // EF.Property<string>(entity, "Email")
            if (node.Method.DeclaringType == typeof(EF)
                && node.Method.Name == nameof(EF.Property)
                && node.Arguments[1] is ConstantExpression { Value: string name })
            {
                Check(node.Arguments[0].Type, name);
            }
            // A projection nested inside the lambda is a read, not a comparison.
            if (node.Method.Name == nameof(Queryable.Select))
            {
                Visit(node.Arguments[0]);
                return node;
            }
            return base.VisitMethodCall(node);
        }

        private void Check(Type owner, string propertyName)
        {
            var property = model.FindEntityType(owner)?.FindProperty(propertyName);
            if (property is not null && property.FindAnnotation(FieldEncryptionModel.ContextAnnotation) is not null)
            {
                throw new EncryptedColumnQueryException(FieldEncryptionModel.ContextOf(property).Value, operatorName);
            }
        }
    }
}

/// <summary>A query tried to filter, order or group on ciphertext. Carries names only.</summary>
public sealed class EncryptedColumnQueryException(string column, string operatorName)
    : InvalidOperationException(
        $"{column} is encrypted and cannot be used in {operatorName}. Read it with Select and filter or sort in memory.")
{
    public string Column { get; } = column;
}
