using System.Linq.Expressions;

namespace eQuantic.Validation.Internal;

internal static class PropertyPath
{
    public static string FromExpression<T, TProperty>(Expression<Func<T, TProperty>> expression)
    {
        if (expression is null)
        {
            throw new ArgumentNullException(nameof(expression));
        }

        var members = new Stack<string>();
        Expression? current = Unwrap(expression.Body);

        while (current is MemberExpression member)
        {
            members.Push(member.Member.Name);
            current = Unwrap(member.Expression);
        }

        if (current is not ParameterExpression)
        {
            throw new ArgumentException(
                "RuleFor accepts a direct property expression such as 'model => model.Email', 'model => model.Address.PostalCode' or 'model => model'.",
                nameof(expression));
        }

        return string.Join(".", members);
    }

    private static Expression? Unwrap(Expression? expression)
    {
        while (expression is UnaryExpression unary &&
               (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
        {
            expression = unary.Operand;
        }

        return expression;
    }
}
