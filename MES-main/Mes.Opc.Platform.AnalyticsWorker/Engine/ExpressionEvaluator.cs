using NCalc;

namespace Mes.Opc.Platform.AnalyticsWorker.Engine;

public static class ExpressionEvaluator
{
    public static double? TryEvaluate(string expression, IDictionary<string, object?> parameters, out string? error)
    {
        try
        {
            var e = new Expression(expression, EvaluateOptions.IgnoreCase);

            // Provide variables
            foreach (var kv in parameters)
            {
                e.Parameters[kv.Key] = kv.Value;
            }

            // Provide safe helper functions
            e.EvaluateFunction += (name, args) =>
            {
                if (name.Equals("min", StringComparison.OrdinalIgnoreCase))
                {
                    args.Result = args.Parameters.Select(p => Convert.ToDouble(p.Evaluate())).Min();
                }
                else if (name.Equals("max", StringComparison.OrdinalIgnoreCase))
                {
                    args.Result = args.Parameters.Select(p => Convert.ToDouble(p.Evaluate())).Max();
                }
            };

            var result = e.Evaluate();
            error = null;

            if (result is null) return null;
            if (result is bool b) return b ? 1.0 : 0.0;
            return Convert.ToDouble(result);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }
}
