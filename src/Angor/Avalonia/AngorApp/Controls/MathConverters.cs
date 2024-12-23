using System.Globalization;
using System.Linq;
using Avalonia;

namespace AngorApp.Controls
{
    public class MathConverters
    {
        public static Zafiro.Avalonia.Converters.FuncMultiValueConverter<object, double, string> Evaluate { get; } = new((inputs, expression) =>
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            var inputList = inputs.ToList();
            
            if (inputList.Any(o => o is UnsetValueType))
            {
                return double.NaN;
            }
            
            var numbers = inputList.Select(Convert.ToDouble).ToList();
            
            // Reemplazar los marcadores en la expresión con los valores reales
            for (int i = 0; i < numbers.Count; i++)
            {
                expression = expression.Replace($"{{{i}}}", numbers[i].ToString(CultureInfo.InvariantCulture));
            }

            // Evaluar la expresión matemática
            return EvaluateExpression(expression);
        });
      
        private static double EvaluateExpression(string expression)
        {
            // Aquí puedes implementar tu propio evaluador de expresiones
            // o usar una biblioteca como NCalc, DataTable.Compute, etc.
            // Este es un ejemplo simple que solo maneja operaciones básicas
            
            var dt = new System.Data.DataTable();
            return Convert.ToDouble(dt.Compute(expression, ""));
        }
    }
}