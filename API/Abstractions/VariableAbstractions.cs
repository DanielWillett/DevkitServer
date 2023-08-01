using System.Reflection;

namespace DevkitServer.API.Abstractions;
/// <summary>
/// Abstraction for <see cref="FieldInfo"/> and <see cref="PropertyInfo"/>.
/// </summary>
public interface IVariable
{
    /// <summary>
    /// If it is safe to get the value.
    /// </summary>
    bool CanGet { get; }
    /// <summary>
    /// If it is safe to set the value.
    /// </summary>
    bool CanSet { get; }
    /// <summary>
    /// If <see cref="Member"/> is <see langword="static"/>.
    /// </summary>
    bool IsStatic { get; }
    /// <summary>
    /// The type <see cref="Member"/> is declared in.
    /// </summary>
    Type? DeclaringType { get; }
    /// <summary>
    /// The type <see cref="Member"/> returns (field or property type).
    /// </summary>
    Type MemberType { get; }
    /// <summary>
    /// Backing member, either a <see cref="FieldInfo"/> or <see cref="PropertyInfo"/>.
    /// </summary>
    MemberInfo Member { get; }
    /// <param name="instance">The instance to get the value from. Pass <see langword="null"/> for static variables.</param>
    /// <returns>The value of the variable.</returns>
    object? GetValue(object? instance);
    /// <param name="instance">The instance to get the value from. Pass <see langword="null"/> for static variables.</param>
    /// <param name="value">Value to set.</param>
    /// <returns>The value of the variable.</returns>
    void SetValue(object? instance, object? value);
}
public static class Variables
{
    public static IVariable AsVariable(this FieldInfo field) => new FieldVariable(field);
    public static IVariable AsVariable(this PropertyInfo property) => new PropertyVariable(property);
    public static IVariable AsVariable(MemberInfo member)
    {
        return member switch
        {
            PropertyInfo property => new PropertyVariable(property),
            FieldInfo field => new FieldVariable(field),
            _ => throw new ArgumentException($"Invalid member type: {member.MemberType}.", nameof(member))
        };
    }

    /// <summary>
    /// Checks if it is safe to set this variable to a value of <paramref name="type"/>.
    /// </summary>
    public static bool IsAssignableFrom(this IVariable variable, Type type)
    {
        if (variable == null) throw new ArgumentNullException(nameof(variable));

        return variable.MemberType.IsAssignableFrom(type);
    }
    /// <summary>
    /// Checks if it is safe to get a variable and cast the result to <paramref name="type"/>.
    /// </summary>
    public static bool IsAssignableTo(this IVariable variable, Type type)
    {
        if (variable == null) throw new ArgumentNullException(nameof(variable));

        return type.IsAssignableFrom(variable.MemberType);
    }
    private sealed class FieldVariable : IVariable, IFormattable, IEquatable<IVariable>
    {
        private readonly FieldInfo _field;
        public FieldVariable(FieldInfo field)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
        }

        public bool CanGet => true;
        public bool CanSet => true;
        public Type? DeclaringType => _field.DeclaringType;
        public Type MemberType => _field.FieldType;
        public bool IsStatic => _field.IsStatic;
        public MemberInfo Member => _field;
        public object? GetValue(object? instance) => _field.GetValue(instance);
        public void SetValue(object? instance, object? value) => _field.SetValue(instance, value);
        public string ToString(string format, IFormatProvider formatProvider) => _field.Format();
        public override string ToString() => _field.ToString();
        public bool Equals(IVariable other) => _field.Equals(other.Member);
        public override bool Equals(object? obj) => obj switch
        {
            FieldInfo field => _field.Equals(field),
            IVariable variable => _field.Equals(variable.Member),
            _ => false
        };
        public override int GetHashCode() => _field.GetHashCode();
    }

    private sealed class PropertyVariable : IVariable, IFormattable, IEquatable<IVariable>
    {
        private readonly PropertyInfo _property;
        private readonly MethodInfo? _getter;
        private readonly MethodInfo? _setter;
        public PropertyVariable(PropertyInfo property)
        {
            _property = property ?? throw new ArgumentNullException(nameof(property));
            _getter = property.GetGetMethod();
            _setter = property.GetSetMethod();
            CanGet = _getter != null && _getter.GetParameters().Length == 0;
            CanSet = _setter != null && _setter.GetParameters().Length == 1;
            IsStatic = _getter == null ? _setter != null && _setter.IsStatic : _getter.IsStatic;
        }

        public bool CanGet { get; }
        public bool CanSet { get; }
        public bool IsStatic { get; }
        public MemberInfo Member => _property;
        public Type MemberType => _property.PropertyType;
        public Type? DeclaringType => _property.DeclaringType;
        public object? GetValue(object? instance)
        {
            if (!CanGet)
                throw new InvalidOperationException("Getting is not allowed for " + _property.Name + ".");

            return _getter!.Invoke(instance, Array.Empty<object>());
        }
        public void SetValue(object? instance, object? value)
        {
            if (!CanSet)
                throw new InvalidOperationException("Setting is not allowed for " + _property.Name + ".");

            _setter!.Invoke(instance, new object?[] { value });
        }

        public string ToString(string format, IFormatProvider formatProvider) => _property.Format();
        public override string ToString() => _property.ToString();
        public bool Equals(IVariable other) => _property.Equals(other.Member);
        public override bool Equals(object? obj) => obj switch
        {
            PropertyInfo property => _property.Equals(property),
            IVariable variable => _property.Equals(variable.Member),
            _ => false
        };
        public override int GetHashCode() => _property.GetHashCode();
    }
}