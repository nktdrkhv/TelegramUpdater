﻿namespace TelegramUpdater
{
    /// <summary>
    /// Base interface for filters.
    /// </summary>
    /// <typeparam name="T">A type that filter will apply to.</typeparam>
    public interface IFilter<T>
    {
        /// <summary>
        /// Indicates if an input of type <typeparamref name="T"/> can pass this filter
        /// </summary>
        /// <param name="input">The input value to check</param>
        /// <returns></returns>
        public bool TheyShellPass(T input);

        /// <summary>
        /// A dicionary of extra data produced by this filter.
        /// </summary>
        public IReadOnlyDictionary<string, object>? ExtraData { get; }
    }

    /// <summary>
    /// A simple basic filter
    /// </summary>
    /// <typeparam name="T">Object type that filter is gonna apply to</typeparam>
    public class Filter<T> : IFilter<T>
    {
        private readonly Func<T, bool>? _filter;
        private Dictionary<string, object>? _extraData;

        /// <inheritdoc/>
        public virtual IReadOnlyDictionary<string, object>? ExtraData => _extraData;

        /// <summary>
        /// Creates a simple basic filter
        /// </summary>
        /// <param name="filter">A function to check the input and return a boolean</param>
        public Filter(Func<T, bool>? filter = default) 
        { 
            _filter = filter;
        }

        internal void AddOrUpdateData(string key, object value)
        {
            if (_extraData is null)
            {
                _extraData = new Dictionary<string, object>();
            }

            if (_extraData.ContainsKey(key))
                _extraData[key] = value;
            else
                _extraData.Add(key, value);
        }

        /// <summary>
        /// Indicates if an input of type <typeparamref name="T"/> can pass this filter
        /// </summary>
        /// <param name="input">The input value to check</param>
        /// <returns></returns>
        public virtual bool TheyShellPass(T input)
            => input != null && (_filter is null || _filter(input));

        /// <summary>
        /// Converts a filter to a fucntion.
        /// </summary>
        /// <param name="filter"></param>
        public static implicit operator Func<T, bool>(Filter<T> filter)
            => filter.TheyShellPass;

        /// <summary>
        /// Returns an <see cref="AndFilter{T}"/> version.
        /// <para>
        /// This and <paramref name="simpleFilter"/>
        /// </para>
        /// </summary>
        public IFilter<T> And(IFilter<T> simpleFilter)
            => new AndFilter<T>(this, simpleFilter);

        /// <summary>
        /// Returns an <see cref="OrFilter{T}"/> version.
        /// <para>
        /// This Or <paramref name="simpleFilter"/>
        /// </para>
        /// </summary>
        public IFilter<T> Or(IFilter<T> simpleFilter)
            => new OrFilter<T>(this, simpleFilter);

        /// <summary>
        /// Returns a reversed version of this <see cref="Filter{T}"/>
        /// </summary>
        /// <returns></returns>
        public IFilter<T> Reverse() => new ReverseFilter<T>(this);

        /// <summary>
        /// Converts a <paramref name="filter"/> to <see cref="Filter{T}"/>
        /// </summary>
        /// <param name="filter"></param>

        public static implicit operator Filter<T>(Func<T, bool> filter) => new(filter);

        /// <summary>
        /// Creates an <see cref="AndFilter{T}"/>
        /// </summary>
        public static Filter<T> operator &(Filter<T> a, Filter<T> b)
            => new AndFilter<T>(a, b);

        /// <summary>
        /// Creates an <see cref="OrFilter{T}"/>
        /// </summary>
        public static Filter<T> operator |(Filter<T> a, Filter<T> b)
            => new OrFilter<T>(a, b);

        /// <summary>
        /// Creates a reversed version of this <see cref="Filter{T}"/>
        /// </summary>
        public static Filter<T> operator ~(Filter<T> a)
            => new ReverseFilter<T>(a);
    }

    /// <summary>
    /// Creates a simple reverse filter
    /// </summary>
    public class ReverseFilter<T> : Filter<T>
    {
        /// <summary>
        /// Creates a reverse filter ( Like not filter ), use not operator
        /// </summary>
        public ReverseFilter(IFilter<T> filter1)
            : base(x => !filter1.TheyShellPass(x))
        { }
    }

    /// <summary>
    /// Used when two filters are going to join other.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class JoinedFilter<T> : Filter<T>
    {
        private Dictionary<string, object>? _extraData;

        /// <summary>
        /// Create a joined filter using two other filters.
        /// </summary>
        public JoinedFilter(params IFilter<T>[] filters)
        {
            Filters = filters;
        }

        /// <summary>
        /// An array of sub filters.
        /// </summary>
        public IFilter<T>[] Filters { get; }

        /// <summary>
        /// Check if the filters are passed here.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        protected abstract bool InnerTheyShellPass(T input);

        /// <inheritdoc/>
        public override bool TheyShellPass(T input)
        {
            var shellPass = InnerTheyShellPass(input);
            _extraData = Filters.Where(x=> x.ExtraData is not null)
                .SelectMany(x=> x.ExtraData!) // extra data not null here.
                .DistinctBy(x=> x.Key) // Is it required ?
                .ToDictionary(x=> x.Key, x => x.Value);
            return shellPass;
        }

        /// <inheritdoc/>
        public override IReadOnlyDictionary<string, object>? ExtraData => _extraData;
    }

    /// <summary>
    /// Creates a simple and filter
    /// </summary>
    public class AndFilter<T> : JoinedFilter<T>
    {
        /// <summary>
        /// Creates a and filter ( Like filter1 and filter2 ), use and operator
        /// </summary>
        public AndFilter(IFilter<T> filter1, IFilter<T> filter2)
            : base(filter1, filter2)
        { }

        /// <inheritdoc/>
        protected override bool InnerTheyShellPass(T input)
        {
            return Filters[0].TheyShellPass(input) && Filters[1].TheyShellPass(input);
        }
    }

    /// <summary>
    /// Creates a simple or filter
    /// </summary>
    public class OrFilter<T> : JoinedFilter<T>
    {
        /// <summary>
        /// Creates an or filter ( Like filter1 or filter2 ), use or operator
        /// </summary>
        public OrFilter(IFilter<T> filter1, IFilter<T> filter2)
            : base(filter1, filter2)
        { }

        /// <inheritdoc/>
        protected override bool InnerTheyShellPass(T input)
        {
            return Filters[0].TheyShellPass(input) || Filters[1].TheyShellPass(input);
        }
    }

    /// <summary>
    /// Extension methods for filters
    /// </summary>
    public static class FiltersExtensions
    {
        /// <summary>
        /// Checks if <paramref name="type"/> is an filter.
        /// </summary>
        /// <param name="type">Type of your filter.</param>
        /// <returns></returns>
        public static bool IsFilter(this Type type)
        {
            return type.GetInterfaces().Any(x =>
              x.IsGenericType &&
              x.GetGenericTypeDefinition() == typeof(IFilter<>));
        }

        /// <summary>
        /// Checks if <paramref name="filterType"/> is an filter for <paramref name="genericType"/>.
        /// </summary>
        /// <param name="filterType">Type of your filter.</param>
        /// <param name="genericType">The type which filter applied on.</param>
        public static bool IsFilterOfType(this Type filterType, Type genericType)
        {
            return filterType.GetInterfaces().Any(x =>
            {
                if (x.IsGenericType)
                {
                    var f = x.GetGenericTypeDefinition();
                    if (f == typeof(IFilter<>))
                    {
                        return x.GetGenericArguments()[0] == genericType;
                    }
                }

                return false;
            });
        }

        /// <summary>
        /// Checks if <paramref name="filterType"/> is an filter for <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type which filter applied on.</typeparam>
        /// <param name="filterType">Type of your filter.</param>
        public static bool IsFilterOfType<T>(this Type filterType)
        {
            return filterType.GetInterfaces().Any(x =>
            {
                if (x.IsGenericType)
                {
                    var f = x.GetGenericTypeDefinition();
                    if (f == typeof(IFilter<>))
                    {
                        return x.GetGenericArguments()[0] == typeof(T);
                    }
                }

                return false;
            });
        }
    }
}
