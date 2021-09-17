using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrackLiveXAML
{
    public abstract class TryResult<T> : TryResult
    {
        public abstract bool HasValue { get; }

        public abstract bool HasNonNullValue { get; }

        public abstract T Value { get; }

        public class Some<TValue> : TryResult<TValue>
        {
            public override bool HasValue
            {
                get
                {
                    return true;
                }
            }

            public override bool HasNonNullValue
            {
                get
                {
                    return (object)this.Value != null;
                }
            }

            public override TValue Value { get; }

            public Some(TValue value)
            {
                this.Value = value;
            }
        }

        public class None : TryResult<T>
        {
            public override bool HasValue
            {
                get
                {
                    return false;
                }
            }

            public override bool HasNonNullValue
            {
                get
                {
                    return false;
                }
            }

            public override T Value
            {
                get
                {
                    throw new InvalidOperationException("None can't have a value");
                }
            }
        }
    }

    public abstract class TryResult
    {
        public static TryResult<TValue>.Some<TValue> FromValue<TValue>(TValue value)
        {
            return new TryResult<TValue>.Some<TValue>(value);
        }

        public static TryResult<TValue>.None None<TValue>()
        {
            return new TryResult<TValue>.None();
        }
    }

    internal static class Try
    {
        public static TryResult<T> Get<T>(Func<T> getter)
        {
            try
            {
                return (TryResult<T>)TryResult.FromValue<T>(getter());
            }
            catch (Exception ex)
            {
                return (TryResult<T>)TryResult.None<T>();
            }
        }
    }
}
