using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UniGLTF;
using UnityEditor;
using UnityEngine;


namespace VRM
{
    public static class VRMAOTMenu
    {
        /// <summary>
        /// AOT向けにダミーのGenerics呼び出しを作成する
        /// </summary>
#if VRM_DEVELOP
        [MenuItem(VRMVersion.MENU + "/GenerateAOTCall")]
#endif
        static void GenerateAOTCall()
        {
            var path = UnityPath.FromUnityPath("Assets/VRM/UniVRM/Scripts/AOTCall.g.cs");
            var encoding = new UTF8Encoding(false);
            using (var s = new MemoryStream())
            {
                using (var w = new StreamWriter(s, encoding))
                {
                    w.WriteLine(@"
using System;
using UniJSON;
using UniGLTF;
using System.Collections.Generic;


namespace VRM {
    public static partial class VRMAOTCall {
        static void glTF()
        {       
            var f = new JsonFormatter();
");

                    TraverseType(w, typeof(glTF), new List<Type>
                    {
                        typeof(object),
                        typeof(string),
                        typeof(bool),

                        typeof(byte),
                        typeof(ushort),
                        typeof(uint),
                        typeof(ulong),

                        typeof(sbyte),
                        typeof(short),
                        typeof(int),
                        typeof(long),

                        typeof(float),
                        typeof(double),

                        typeof(Vector3),
                    });

                    w.WriteLine(@"
        }
    }
}
");
                }

                var text = encoding.GetString(s.ToArray());
                File.WriteAllText(path.FullPath, text.Replace("\r\n", "\n"), encoding);
            }

            path.ImportAsset();
        }

        static Type GetGenericListValueType(Type t)
        {
            if (t.IsGenericType
                && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                return t.GetGenericArguments()[0];
            }
            else
            {
                return null;
            }
        }

        static Type GetGenericDictionaryValueType(Type t)
        {
            if (t.IsGenericType
                && t.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                && t.GetGenericArguments()[0] == typeof(string))
            {
                return t.GetGenericArguments()[1];
            }
            else
            {
                return null;
            }
        }

        static void TraverseType(TextWriter w, Type t, List<Type> excludes)
        {
            if (excludes.Contains(t))
            {
                return;
            }

            w.WriteLine();
            w.WriteLine("// $0".Replace("$0", t.Name));
            excludes.Add(t);

            {
                // list
                var valueType = GetGenericListValueType(t);
                if (valueType != null)
                {
                    w.WriteLine("f.Serialize(default(List<$0>));".Replace("$0", valueType.Name));
                    w.WriteLine(@"{
var value = default(List<$0>);
default(ListTreeNode<JsonValue>).Deserialize(ref value);
}".Replace("$0", valueType.Name));

                    TraverseType(w, valueType, excludes);
                    return;
                }
            }

            {
                // dict
                var valueType = GetGenericDictionaryValueType(t);
                if (valueType != null)
                {
                    w.WriteLine("f.Serialize(default(Dictionary<string, $0>));".Replace("$0", valueType.Name));
                    w.WriteLine(@"{
var value = default(Dictionary<string, $0>);
default(ListTreeNode<JsonValue>).Deserialize(ref value);
}".Replace("$0", valueType.Name));

                    TraverseType(w, valueType, excludes);
                    return;
                }
            }

            w.WriteLine("f.Serialize(default($0));".Replace("$0", t.Name));
            w.WriteLine(@"{
var value = default($0);
default(ListTreeNode<JsonValue>).Deserialize(ref value);
}".Replace("$0", t.Name));

            // object
            if (t.IsClass)
            {
                foreach (var fi in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    TraverseType(w, fi.FieldType, excludes);
                }
            }
        }
    }
}
