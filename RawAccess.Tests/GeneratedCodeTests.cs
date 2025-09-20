using RawAccess.Generated;
using RawAccess.Generated.Module1.ClassModule;
using RawAccess.Generated.Module2.StructModule;
using RawAccess.Generated.Module3.OtherModule;

namespace RawAccess.Tests
{
    [TestClass]
    public class GeneratedCodeTests
    {
        [TestMethod]
        public void MyClassTest()
        {
            var myClass = MyClassRawAccess.GetMyClass<MyList<string>, List<string>, string>("some_str", ["mde"], ["hmm"], "meh");

            for (int i = 0, limit = myClass.A.Count; i < limit; ++i)
            {
                Assert.AreEqual(myClass.A[i], MyClassRawAccess.GetA(myClass)[i]);
            }

            for (int i = 0, limit = myClass.B.Count; i < limit; ++i)
            {
                Assert.AreEqual(myClass.B[i], MyClassRawAccess.GetB(myClass)[i]);
            }

            List<string> newB = ["lol"];
            var myClassHatch1 = MyClassRawAccess.WithB(myClass, newB);

            for (int i = 0, limit = myClassHatch1.B.Count; i < limit; ++i)
            {
                Assert.AreEqual(newB[i], MyClassRawAccess.GetB(myClass)[i]);
            }

            MyClassRawAccess.WithC(myClass, "1337");
        }

        [TestMethod]
        public void MyStructTest()
        {
            var myStruct = MyStructRawAccess.GetMyStruct("str");
            Assert.AreEqual(myStruct.Str, MyStructRawAccess.GetStr(myStruct));
            const string newStr = "newStr";
            var myStructHatch1 = MyStructRawAccess.WithStr(myStruct, newStr);
            Assert.AreEqual(newStr, MyStructRawAccess.GetStr(myStructHatch1));
        }

        [TestMethod]
        public void MyRecordTest()
        {
            var myRecord = MyRecordRawAccess.GetMyRecord(1337);
            Assert.AreEqual(myRecord.Number, MyRecordRawAccess.GetNumber(myRecord));
            const int newNumber = 228;
            var myRecordHatch1 = MyRecordRawAccess.WithNumber(myRecord, newNumber);
            Assert.AreEqual(newNumber, MyRecordRawAccess.GetNumber(myRecordHatch1));
        }
    }
}

namespace Module1.ClassModule
{
    [GenerateRawAccess]
    public class MyClass<TA, TB, TC> where TA : TB where TB : IEnumerable<TC> where TC : class
    {
        public TA A { get; } = default!;

        public TB B { get; set; } = default!;

        // ReSharper disable once ValueParameterNotUsed
        public TC C { set { } }

        public MyClass(string myString, TA a, TB b, TC c)
        {
            A = a;
            B = b; 
            C = c;
        }

        public MyClass(bool meh)
        {

        }
    }
}

namespace Module2.StructModule
{
    [GenerateRawAccess]
    public struct MyStruct(string str)
    {
        public string Str { get; set; } = str;
    }
}

namespace Module3.OtherModule
{
    [GenerateRawAccess]
    public record MyRecord(int Number);
}

#pragma warning disable CA1050
public class MyList<T> : List<T>;
#pragma warning restore CA1050
