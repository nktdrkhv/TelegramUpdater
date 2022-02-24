using System;
using TelegramUpdater;
using Xunit;

namespace TelegramUpdaterTests
{
    class IntFilter : Filter<int>
    {
        public IntFilter(Func<int, bool> filter) : base(filter)
        {
        }
    }

    public class FiltersTests
    {
        [Fact]
        public void AndFilterTest_1()
        {
            var filter = new IntFilter(x=> true) & new IntFilter(x=> false);

            Assert.IsType<AndFilter<int>> (filter);
        }

        [Fact]
        public void AndFilterTest_2()
        {
            var filter = new Filter<int>(x => true) & new Filter<int>(x => false);

            Assert.IsType<AndFilter<int>>(filter);
        }

        [Fact]
        public void AndFilterTest_3()
        {
            var filter = new Filter<int>(x => true) & new Filter<int>(x => false);

            Assert.False(filter.TheyShellPass(0));
        }

        [Fact]
        public void AndFilterTest_4()
        {
            var filter = new Filter<int>(x => true) & new Filter<int>(x => true);

            Assert.True(filter.TheyShellPass(0));
        }

        [Fact]
        public void OrFilterTest_1()
        {
            var filter = new IntFilter(x => true) | new IntFilter(x => false);

            Assert.IsType<OrFilter<int>>(filter);
        }

        [Fact]
        public void OrFilterTest_2()
        {
            var filter = new Filter<int>(x => true) | new Filter<int>(x => false);

            Assert.True(filter.TheyShellPass(0));
        }

        [Fact]
        public void OrFilterTest_3()
        {
            var filter = new Filter<int>(x => true) | new Filter<int>(x => false);

            Assert.True(filter.TheyShellPass(0));
        }

        [Fact]
        public void OrFilterTest_4()
        {
            var filter = new Filter<int>(x => false) | new Filter<int>(x => false);

            Assert.False(filter.TheyShellPass(0));
        }

        [Fact]
        public void CombinedFilterTest_1()
        {
            var filter = new Filter<int>(x => false) | new Filter<int>(x => false);
            var filter_2 = filter | new Filter<int>(x => true);

            Assert.True(filter_2.TheyShellPass(0));
        }

        [Fact]
        public void CombinedFilterTest_2()
        {
            var filter = new Filter<int>(x => false) | new Filter<int>(x => false);
            var filter_2 = filter & new Filter<int>(x => true);

            Assert.False(filter_2.TheyShellPass(0));
        }

        [Fact]
        public void IsFilterTest_1()
        {
            var filter = new Filter<int>(x => false) | new Filter<int>(x => false);

            var isFilter = filter.GetType().IsFilter();
            Assert.True(isFilter);
        }

        [Fact]
        public void IsFilterTest_2()
        {
            var filter = new Filter<int>(x => false);

            var isFilter = filter.GetType().IsFilter();
            Assert.True(isFilter);
        }

        class MyFilter : Filter<int>
        {
            public MyFilter() : base(x=> x == 10)
            {
            }
        }

        [Fact]
        public void IsFilterTest_3()
        {
            var isFilter = typeof(MyFilter).IsFilter();
            Assert.True(isFilter);
        }

        [Fact]
        public void IsFilterTest_4()
        {
            var myFilter = new MyFilter();

            var isFilter = myFilter.GetType().IsFilter();
            Assert.True(isFilter);
        }

        [Fact]
        public void IsFilterOfTypeTest_1()
        {
            var filter = new Filter<int>(x => false);

            var isFilter = filter.GetType().IsFilterOfType(typeof(int));
            Assert.True(isFilter);
        }

        [Fact]
        public void IsFilterOfTypeTest_2()
        {
            var filter = new Filter<int>(x => false) | new Filter<int>(x => false);

            var isFilter = filter.GetType().IsFilterOfType(typeof(int));
            Assert.True(isFilter);
        }

        [Fact]
        public void IsFilterOfTypeTest_3()
        {
            var isFilter = typeof(MyFilter).IsFilterOfType(typeof(int));
            Assert.True(isFilter);
        }
    }
}