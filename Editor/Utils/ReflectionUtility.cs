using System;
using System.Linq.Expressions;
using System.Reflection;

public class ReflectionUtility
{
    /// <summary>
    ///     Compile a lambda that will invoke the specified static field's getter and return the value.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="bindingFlags">The flags used to find the field.</param>
    /// <typeparam name="T">The instance type to access the field from.</typeparam>
    /// <typeparam name="U">The return value type.</typeparam>
    /// <returns>Lambda which can be invoked like a normal function. (e.g. lambdaName(instance))</returns>
    public static Func<U> BuildFieldGetterStatic<T, U>(string fieldName, BindingFlags bindingFlags)
    {
        FieldInfo fieldInfo = typeof(T).GetField(fieldName, bindingFlags);
        MemberExpression fieldAccess = Expression.Field(null, fieldInfo);

        var lambda = Expression.Lambda<Func<U>>(fieldAccess, null);
        return lambda.Compile();
    }

    /// <summary>
    ///     Compile a lambda that will invoke the specified field's getter and return the value.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="bindingFlags">The flags used to find the field.</param>
    /// <typeparam name="T">The instance type to access the field from.</typeparam>
    /// <typeparam name="U">The return value type.</typeparam>
    /// <returns>Lambda which can be invoked like a normal function. (e.g. lambdaName(instance))</returns>
    public static Func<T, U> BuildFieldGetter<T, U>(string fieldName, BindingFlags bindingFlags)
    {
        ParameterExpression instanceParam = Expression.Parameter(typeof(T), "t");

        FieldInfo fieldInfo = typeof(T).GetField(fieldName, bindingFlags);
        MemberExpression fieldAccess = Expression.Field(instanceParam, fieldInfo);

        var lambda = Expression.Lambda<Func<T, U>>(fieldAccess, instanceParam);
        return lambda.Compile();
    }

    /// <summary>
    ///     Compile a lambda that will invoke the specified field's setter with a supplied value.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="bindingFlags">The flags used to find the field.</param>
    /// <typeparam name="T">The instance type to access the field from.</typeparam>
    /// <typeparam name="U">The return value type.</typeparam>
    /// <returns>Lambda which can be invoked like a normal function. (e.g. lambdaName(instance))</returns>
    public static Action<T, U> BuildFieldSetter<T, U>(string fieldName, BindingFlags bindingFlags)
    {
        ParameterExpression instanceParam = Expression.Parameter(typeof(T), "instance");
        ParameterExpression valueParam = Expression.Parameter(typeof(U), "value");

        FieldInfo fieldInfo = typeof(T).GetField(fieldName, bindingFlags);
        MemberExpression fieldAccess = Expression.Field(instanceParam, fieldInfo);
        BinaryExpression assign = Expression.Assign(fieldAccess, valueParam);

        var lambda = Expression.Lambda<Action<T, U>>(assign, instanceParam, valueParam);
        return lambda.Compile();
    }

    /// <summary>
    ///     Compile a lambda that will invoke the specified field's setter with the supplied value. (Generic version)
    /// </summary>
    /// <param name="type">The type of the instance the property is defined in.</param>
    /// <param name="fieldName">The name of the property member.</param>
    /// <param name="bindingFlags">The flags used to find the property.</param>
    /// <typeparam name="T">The return value type (use 'object' if unknown at compile-time).</typeparam>
    /// <returns>Lambda which can be invoked like a normal function. (e.g. lambdaName(instance))</returns>
    public static Action<T, U> BuildFieldSetter<T, U>(Type type, string fieldName, BindingFlags bindingFlags)
    {
        ParameterExpression instanceParam = Expression.Parameter(typeof(T), "instance");
        ParameterExpression valueParam = Expression.Parameter(typeof(U), "value");

        // Cast the instance from "object" to the correct type.
        Expression instanceExpr = instanceParam;
        if (typeof(T) != type)
        {
            instanceExpr = Expression.TypeAs(instanceParam, type);
        }

        FieldInfo fieldInfo = type.GetField(fieldName, bindingFlags);
        MemberExpression fieldAccess = Expression.Field(instanceExpr, fieldInfo);
        BinaryExpression assign = Expression.Assign(fieldAccess, valueParam);

        var lambda = Expression.Lambda<Action<T, U>>(assign, instanceParam, valueParam);
        return lambda.Compile();
    }
}