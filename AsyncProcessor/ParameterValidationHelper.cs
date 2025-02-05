namespace AsyncProcessor
{
    internal static class ParameterValidationHelper
    {
        internal static void ThrowIfNull(params object[] parameters)
        {
            foreach (var param in parameters)
                ArgumentNullException.ThrowIfNull(param);
        }
    }
}
