using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TestApp
{
    class Program
    {
        static void Main()
        {
            var root = new Root
            {
                ReferenceType = new ReferenceType
                {
                    ValueField = 10,
                    ReferenceField = "10",
                    ObjectField = ReferenceType2.Create(11),
                    StructField = ComplexValueType2.Create(12),
                    ArrayOfValueField = new[] { 13, 14, 15 },
                    ArrayOfReferenceField = new[] { "13", "14", "15" },
                    ArrayOfObjectField = Enumerable.Range(16, 4).Select(ReferenceType2.Create).ToArray(),
                    ArrayOfStructField = Enumerable.Range(16, 4).Select(ComplexValueType2.Create).ToArray(),
                    NullField = null,
                    BoxedInt = 20,
                    BoxedComplexValueType = (object)ComplexValueType2.Create(21),
                    InterfaceHiddenObject = ReferenceType2.Create(22),
                    InterfaceBoxedValueType = ComplexValueType2.Create(23),
                    ListOfObjectsField = Enumerable.Range(24, 2).Select(ReferenceType2.Create).ToList(),
                    ListOfStructsField = Enumerable.Range(26, 2).Select(ComplexValueType2.Create).ToList()
                },

                ComplexValueType = new ComplexValueType
                {
                    ValueField = 50,
                    ReferenceField = "50",
                    ObjectField = ReferenceType2.Create(51),
                    StructField = ComplexValueType2.Create(52),
                    ArrayOfValueField = new[] { 53, 54, 55 },
                    ArrayOfReferenceField = new[] { "53", "54", "55" },
                    ArrayOfObjectField = Enumerable.Range(66, 4).Select(ReferenceType2.Create).ToArray(),
                    ArrayOfStructField = Enumerable.Range(66, 4).Select(ComplexValueType2.Create).ToArray(),
                    NullField = null,
                    BoxedInt = 70,
                    BoxedComplexValueType = (object)ComplexValueType2.Create(71),
                    InterfaceHiddenObject = ReferenceType2.Create(72),
                    InterfaceBoxedValueType = ComplexValueType2.Create(73),
                    ListOfObjectsField = Enumerable.Range(74, 2).Select(ReferenceType2.Create).ToList(),
                    ListOfStructsField = Enumerable.Range(76, 2).Select(ComplexValueType2.Create).ToList()
                },

                SpecialTypes = new SpecialTypes
                {
                    DateTime = new DateTime(2019, 7, 16, 19, 33, 05, 445),
                    TimeSpan = TimeSpan.FromSeconds(42),
                    Guid = new Guid("d4af2890-5b89-4fb9-aa6f-2df144ceac3f"),
                    Enum = DayOfWeek.Friday,
                    EnumFlag = BindingFlags.Public | BindingFlags.Instance
                }
            };

            var assembly = Assembly.GetEntryAssembly();
            string fileName = Path.Combine(Path.GetDirectoryName(assembly.Location), $"{assembly.GetName().Name}.dmp");
            using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.Write))
            {
                MiniDump.Write(fs.SafeFileHandle, MiniDump.Option.WithFullMemory);
            }

            Console.WriteLine($"Written {(Environment.Is64BitProcess ? "x64" : "x86")} dump file to {fileName}.");
            GC.KeepAlive(root);
        }
    }

    
    public class Root
    {
        public ReferenceType ReferenceType;
        public ComplexValueType ComplexValueType;
        public SpecialTypes SpecialTypes;
    }
        
    public class ReferenceType
    {
        public int ValueField;
        public string ReferenceField;
        public ReferenceType2 ObjectField;
        public ComplexValueType2 StructField;
        public int[] ArrayOfValueField;
        public string[] ArrayOfReferenceField;
        public ReferenceType2[] ArrayOfObjectField;
        public ComplexValueType2[] ArrayOfStructField;
        public object NullField;
        public object BoxedInt;
        public object BoxedComplexValueType;
        public IInterface1 InterfaceHiddenObject;
        public IInterface2 InterfaceBoxedValueType;
        public List<ReferenceType2> ListOfObjectsField;
        public List<ComplexValueType2> ListOfStructsField;
    }

    public struct ComplexValueType
    {
        public int ValueField;
        public string ReferenceField;
        public ReferenceType2 ObjectField;
        public ComplexValueType2 StructField;
        public int[] ArrayOfValueField;
        public string[] ArrayOfReferenceField;
        public ReferenceType2[] ArrayOfObjectField;
        public ComplexValueType2[] ArrayOfStructField;
        public object NullField;
        public object BoxedInt;
        public object BoxedComplexValueType;
        public IInterface1 InterfaceHiddenObject;
        public IInterface2 InterfaceBoxedValueType;
        public List<ReferenceType2> ListOfObjectsField;
        public List<ComplexValueType2> ListOfStructsField;
    }

    public class SpecialTypes
    {
        public DateTime DateTime;
        public TimeSpan TimeSpan;
        public Guid Guid;
        public DayOfWeek Enum;
        public System.Reflection.BindingFlags EnumFlag;
    }

    public interface IInterface1
    {

    }

    public interface IInterface2
    {

    }

    public class ReferenceType2 : IInterface1
    {
        public int ValueField2;
        public string ReferenceField2;

        public static ReferenceType2 Create(int seed)
        {
            return new ReferenceType2
            {
                ValueField2 = seed,
                ReferenceField2 = seed.ToString()
            };
        }
    }

    public struct ComplexValueType2 : IInterface2
    {
        public int ValueField2;
        public string ReferenceField2;

        public static ComplexValueType2 Create(int seed)
        {
            return new ComplexValueType2
            {
                ValueField2 = seed,
                ReferenceField2 = seed.ToString()
            };
        }
    }
}
