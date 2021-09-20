using NUnit.Framework;

namespace Mirror.Tests
{
    public class SyncFieldTests
    {
        SyncField<int> field;
        int dirtyCalled = 0;

        [SetUp]
        public void SetUp()
        {
            field = 42;
            dirtyCalled = 0;
            field.OnDirty = () => ++dirtyCalled;
        }

        [Test]
        public void SetValue_SetsValue()
        {
            // .Value is a property which does several things.
            // make sure it .set actually sets the value
            field.Value = 1337;
            Assert.That(field.Value, Is.EqualTo(1337));
        }

        [Test]
        public void SetValue_CallsOnDirty()
        {
            // setting SyncField<T>.Value should call dirty
            field.Value = 1337;
            Assert.That(dirtyCalled, Is.EqualTo(1));
        }

        [Test]
        public void SetValue_WithoutOnDirty()
        {
            // OnDirty needs to be optional.
            // shouldn't throw exceptions if OnDirty is null.
            field.OnDirty = null;
            field.Value = 1337;
        }

        [Test]
        public void ImplicitTo()
        {
            // T = field implicit conversion should get .Value
            int value = field;
            Assert.That(value, Is.EqualTo(42));
        }

        // TODO maybe implicit from shouldn't exist?
        //      should be readonly.
        //      or a struct and copy OnDirty hook
        [Test]
        public void ImplicitFrom_SetsValue()
        {
            // field = T implicit conversion should set .Value
            field = 1337;
            Assert.That(field.Value, Is.EqualTo(1337));
        }

        // TODO maybe implicit from shouldn't exist?
        //      should be readonly.
        //      or a struct and copy OnDirty hook
        [Test]
        public void ImplicitFrom_CallsOnDirty()
        {
            // field = T implicit conversion should call OnDirty
            field = 1337;
            Assert.That(dirtyCalled, Is.EqualTo(1));
        }

        [Test, Ignore("TODO: what should copy ctor do?")]
        public void CopyConstructor()
        {
            SyncField<int> other = new SyncField<int>(43);
            field = new SyncField<int>(other);
        }

        [Test]
        public void Hook()
        {
            int called = 0;
            void OnChanged(int oldValue, int newValue)
            {
                ++called;
                Assert.That(oldValue, Is.EqualTo(42));
                Assert.That(newValue, Is.EqualTo(1337));
            }

            SyncField<int> fieldWithHook = new SyncField<int>(42, OnChanged);
            fieldWithHook.Value = 1337;
            Assert.That(called, Is.EqualTo(1));
        }
    }
}