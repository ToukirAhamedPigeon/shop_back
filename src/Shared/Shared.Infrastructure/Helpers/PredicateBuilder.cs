using System.Linq.Expressions;

namespace shop_back.src.Shared.Infrastructure.Helpers
{
    public static class PredicateBuilder
    {
        public static Expression<Func<T, bool>> True<T>() { return f => true; }
        public static Expression<Func<T, bool>> False<T>() { return f => false; }

        public static Expression<Func<T, bool>> Or<T>(
            this Expression<Func<T, bool>> expr1,
            Expression<Func<T, bool>> expr2)
        {
            ArgumentNullException.ThrowIfNull(expr1);
            ArgumentNullException.ThrowIfNull(expr2);

            var parameter = Expression.Parameter(typeof(T));
            
            var leftVisitor = new ReplaceExpressionVisitor(expr1.Parameters[0], parameter);
            var left = leftVisitor.Visit(expr1.Body);
            
            var rightVisitor = new ReplaceExpressionVisitor(expr2.Parameters[0], parameter);
            var right = rightVisitor.Visit(expr2.Body);

            if (left == null || right == null)
            {
                throw new InvalidOperationException("Failed to visit expression bodies");
            }

            return Expression.Lambda<Func<T, bool>>(
                Expression.OrElse(left, right), parameter);
        }

        public static Expression<Func<T, bool>> And<T>(
            this Expression<Func<T, bool>> expr1,
            Expression<Func<T, bool>> expr2)
        {
            ArgumentNullException.ThrowIfNull(expr1);
            ArgumentNullException.ThrowIfNull(expr2);

            var parameter = Expression.Parameter(typeof(T));
            
            var leftVisitor = new ReplaceExpressionVisitor(expr1.Parameters[0], parameter);
            var left = leftVisitor.Visit(expr1.Body);
            
            var rightVisitor = new ReplaceExpressionVisitor(expr2.Parameters[0], parameter);
            var right = rightVisitor.Visit(expr2.Body);

            if (left == null || right == null)
            {
                throw new InvalidOperationException("Failed to visit expression bodies");
            }

            return Expression.Lambda<Func<T, bool>>(
                Expression.AndAlso(left, right), parameter);
        }

        private class ReplaceExpressionVisitor : ExpressionVisitor
        {
            private readonly Expression _oldValue;
            private readonly Expression _newValue;

            public ReplaceExpressionVisitor(Expression oldValue, Expression newValue)
            {
                _oldValue = oldValue ?? throw new ArgumentNullException(nameof(oldValue));
                _newValue = newValue ?? throw new ArgumentNullException(nameof(newValue));
            }

            public override Expression? Visit(Expression? node)
            {
                if (node == null) return null;
                return node == _oldValue ? _newValue : base.Visit(node);
            }
        }
    }
}