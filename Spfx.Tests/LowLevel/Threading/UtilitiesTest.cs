using NUnit.Framework;
using System.Threading.Tasks;
using Spfx.Utilities.Threading;
using System.Threading;

namespace Spfx.Tests.LowLevel.Threading
{
    [TestFixture, Parallelizable]
    public class UtilitiesTest : CommonTestClass
    {
        [Test, Timeout(DefaultTestTimeout)]
        public void TaskEx_ThreadSwitchTest()
        {
            const string name = "Name12039501935";

            void CheckThread(bool shouldBeCustom)
            {
                Assert.AreEqual(Thread.CurrentThread.IsThreadPoolThread, !shouldBeCustom);
                if (shouldBeCustom)
                    Assert.AreEqual(Thread.CurrentThread.Name, name);
                else
                    Assert.AreNotEqual(Thread.CurrentThread.Name, name);
            }

            Task.Run(async () =>
            {
                CheckThread(false);
                await TaskEx.SwitchToNewThread(name);
                CheckThread(true);
                await TaskScheduler.Default;
                CheckThread(false);
            }).WaitOrRethrow();
        }
    }
}