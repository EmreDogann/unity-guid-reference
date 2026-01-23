using System;
using System.IO;
using UnityEngine;

// From: https://www.dropbox.com/scl/fi/uhb9l2dcvfzgxbduqkvao/SerializableType.cs?rlkey=py7n6asrlu94t8y0qt6hh4kcj&e=1
[Serializable]
public class SerializableType : ISerializationCallbackReceiver
{
    [NonSerialized]
    public Type Type;
    public byte[] data;

    public SerializableType(Type aType)
    {
        Type = aType;
    }

    public static Type Read(BinaryReader aReader)
    {
        byte paramCount = aReader.ReadByte();
        if (paramCount == 0xFF)
        {
            return null;
        }

        string typeName = aReader.ReadString();
        Type type = Type.GetType(typeName);
        if (type == null)
        {
            throw new Exception("Can't find type; '" + typeName + "'");
        }

        if (type.IsGenericTypeDefinition && paramCount > 0)
        {
            var p = new Type[paramCount];
            for (int i = 0; i < paramCount; i++)
            {
                p[i] = Read(aReader);
            }

            type = type.MakeGenericType(p);
        }

        return type;
    }

    public static void Write(BinaryWriter aWriter, Type aType)
    {
        if (aType == null)
        {
            aWriter.Write((byte)0xFF);
            return;
        }

        if (aType.IsGenericType)
        {
            Type t = aType.GetGenericTypeDefinition();
            var p = aType.GetGenericArguments();
            aWriter.Write((byte)p.Length);
            aWriter.Write(t.AssemblyQualifiedName);
            for (int i = 0; i < p.Length; i++)
            {
                Write(aWriter, p[i]);
            }

            return;
        }

        aWriter.Write((byte)0);
        aWriter.Write(aType.AssemblyQualifiedName);
    }

    public void OnBeforeSerialize()
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(stream))
        {
            Write(w, Type);
            data = stream.ToArray();
        }
    }

    public void OnAfterDeserialize()
    {
        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader r = new BinaryReader(stream))
        {
            Type = Read(r);
        }
    }
}