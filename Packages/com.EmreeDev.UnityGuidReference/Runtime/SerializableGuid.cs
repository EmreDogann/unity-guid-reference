using System;
using UnityEngine;

/// <summary>
///     Extensions to the <see cref="System.Guid" /> type.
/// </summary>
public static class GuidExtensions
{
    /// <summary>
    ///     Decomposes a 16-byte <c>Guid</c> into two 8-byte <c>ulong</c>s.
    ///     Recompose with <see cref="GuidUtil.Compose(ulong, ulong)" />.
    /// </summary>
    /// <param name="guid">The <c>Guid</c> being extended</param>
    /// <param name="low">The lower 8 bytes of the guid.</param>
    /// <param name="high">The upper 8 bytes of the guid.</param>
    public static void Decompose(this Guid guid, out ulong low, out ulong high)
    {
        byte[] bytes = guid.ToByteArray();
        low = BitConverter.ToUInt64(bytes, 0);
        high = BitConverter.ToUInt64(bytes, 8);
    }
}

/// <summary>
///     Utility for dealing with <see cref="System.Guid" /> objects.
/// </summary>
public static class GuidUtil
{
    /// <summary>
    ///     Reconstructs a <see cref="Guid" /> from two <see cref="ulong" /> values representing the low and high bytes.
    /// </summary>
    /// <remarks>
    ///     Use <see cref="GuidExtensions.Decompose(Guid, out ulong, out ulong)" /> to separate the `Guid`
    ///     into its low and high components.
    /// </remarks>
    /// <param name="low">The low 8 bytes of the `Guid`.</param>
    /// <param name="high">The high 8 bytes of the `Guid`.</param>
    /// <returns>The `Guid` composed of <paramref name="low" /> and <paramref name="high" />.</returns>
    public static Guid Compose(ulong low, ulong high)
    {
        return new Guid(
            (uint)((low & 0x00000000ffffffff) >> 0),
            (ushort)((low & 0x0000ffff00000000) >> 32),
            (ushort)((low & 0xffff000000000000) >> 48),
            (byte)((high & 0x00000000000000ff) >> 0),
            (byte)((high & 0x000000000000ff00) >> 8),
            (byte)((high & 0x0000000000ff0000) >> 16),
            (byte)((high & 0x00000000ff000000) >> 24),
            (byte)((high & 0x000000ff00000000) >> 32),
            (byte)((high & 0x0000ff0000000000) >> 40),
            (byte)((high & 0x00ff000000000000) >> 48),
            (byte)((high & 0xff00000000000000) >> 56));
    }
}

/// <summary>
///     A <c>Guid</c> that can be serialized by Unity.
/// </summary>
/// <remarks>
///     The 128-bit <c>Guid</c>
///     is stored as two 64-bit <c>ulong</c>s. See the creation utility,
///     <see cref="SerializableGuidUtil" />, for additional information.
/// </remarks>
[Serializable]
public struct SerializableGuid : IEquatable<SerializableGuid>
{
    [SerializeField]
    private ulong m_GuidLow;

    [SerializeField]
    private ulong m_GuidHigh;

    /// <summary>
    ///     Represents <c>System.Guid.Empty</c>, a GUID whose value is all zeros.
    /// </summary>
    public static SerializableGuid Empty { get; } = new SerializableGuid(0, 0);

    /// <summary>
    ///     Reconstructs the <c>Guid</c> from the serialized data.
    /// </summary>
    public Guid Guid => GuidUtil.Compose(m_GuidLow, m_GuidHigh);

    /// <summary>
    ///     Creates a <c>SerializableGuid</c> from a <c>System.Guid</c>.
    /// </summary>
    /// <param name="guid">The <c>Guid</c> to represent as a <c>SerializableGuid</c>.</param>
    /// <returns>A serializable version of <paramref name="guid" />.</returns>
    public static SerializableGuid Create(Guid guid)
    {
        guid.Decompose(out ulong low, out ulong high);
        return new SerializableGuid(low, high);
    }

    /// <summary>
    ///     Constructs a <see cref="SerializableGuid" /> from two 64-bit <c>ulong</c> values.
    /// </summary>
    /// <param name="guidLow">The low 8 bytes of the <c>Guid</c>.</param>
    /// <param name="guidHigh">The high 8 bytes of the <c>Guid</c>.</param>
    public SerializableGuid(ulong guidLow, ulong guidHigh)
    {
        m_GuidLow = guidLow;
        m_GuidHigh = guidHigh;
    }

    /// <summary>
    ///     Gets the hash code for this SerializableGuid.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = m_GuidLow.GetHashCode();
            return hash * 486187739 + m_GuidHigh.GetHashCode();
        }
    }

    /// <summary>
    ///     Checks if this SerializableGuid is equal to an object.
    /// </summary>
    /// <param name="obj">The object to check.</param>
    /// <returns>True if <paramref name="obj" /> is a SerializableGuid with the same field values.</returns>
    public override bool Equals(object obj)
    {
        if (!(obj is SerializableGuid serializableGuid))
        {
            return false;
        }

        return Equals(serializableGuid);
    }

    /// <summary>
    ///     Generates a string representation of the <c>Guid</c>. Same as <see cref="Guid.ToString()" />.
    ///     See <a href="https://docs.microsoft.com/en-us/dotnet/api/system.guid.tostring?view=netframework-4.7.2#System_Guid_ToString">Microsoft's documentation</a>
    ///     for more details.
    /// </summary>
    /// <returns>A string representation of the <c>Guid</c>.</returns>
    public override string ToString()
    {
        return Guid.ToString();
    }

    /// <summary>
    ///     Generates a string representation of the <c>Guid</c>. Same as <see cref="Guid.ToString(string)" />.
    /// </summary>
    /// <param name="format">
    ///     A single format specifier that indicates how to format the value of the <c>Guid</c>.
    ///     See
    ///     <a href="https://docs.microsoft.com/en-us/dotnet/api/system.guid.tostring?view=netframework-4.7.2#System_Guid_ToString_System_String_">
    ///         Microsoft's
    ///         documentation
    ///     </a>
    ///     for more details.
    /// </param>
    /// <returns>A string representation of the <c>Guid</c>.</returns>
    public string ToString(string format)
    {
        return Guid.ToString(format);
    }

    /// <summary>
    ///     Generates a string representation of the <c>Guid</c>. Same as <see cref="Guid.ToString(string, IFormatProvider)" />.
    /// </summary>
    /// <param name="format">
    ///     A single format specifier that indicates how to format the value of the <c>Guid</c>.
    ///     See
    ///     <a
    ///         href="https://docs.microsoft.com/en-us/dotnet/api/system.guid.tostring?view=netframework-4.7.2#System_Guid_ToString_System_String_System_IFormatProvider_">
    ///         Microsoft's
    ///         documentation
    ///     </a>
    ///     for more details.
    /// </param>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <returns>A string representation of the <c>Guid</c>.</returns>
    public string ToString(string format, IFormatProvider provider)
    {
        return Guid.ToString(format, provider);
    }

    /// <summary>
    ///     Check if this SerializableGuid is equal to another SerializableGuid.
    /// </summary>
    /// <param name="other">The other SerializableGuid</param>
    /// <returns>True if this SerializableGuid has the same field values as the other one.</returns>
    public bool Equals(SerializableGuid other)
    {
        return m_GuidLow == other.m_GuidLow &&
               m_GuidHigh == other.m_GuidHigh;
    }

    /// <summary>
    ///     Perform an equality operation on two SerializableGuids.
    /// </summary>
    /// <param name="lhs">The left-hand SerializableGuid.</param>
    /// <param name="rhs">The right-hand SerializableGuid.</param>
    /// <returns>True if the SerializableGuids are equal to each other.</returns>
    public static bool operator ==(SerializableGuid lhs, SerializableGuid rhs)
    {
        return lhs.Equals(rhs);
    }

    /// <summary>
    ///     Perform an inequality operation on two SerializableGuids.
    /// </summary>
    /// <param name="lhs">The left-hand SerializableGuid.</param>
    /// <param name="rhs">The right-hand SerializableGuid.</param>
    /// <returns>True if the SerializableGuids are not equal to each other.</returns>
    public static bool operator !=(SerializableGuid lhs, SerializableGuid rhs)
    {
        return !lhs.Equals(rhs);
    }
}