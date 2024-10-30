using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server._FishStation.Themida
{
    internal sealed class ActionRateLimiter<TKey, TValue> where TKey : notnull
    {
        // TODO: Move this to CCVars.cs
        // Time in milliseconds at which rate user is being limited
        public readonly int RateDelta;
        public readonly int Limit;
        public readonly int ExtremeLimit;

        private sealed class LimitRecord
        {
            public long PingTimestamp;
            public TValue? Data;
            public int Count = 1;

            public LimitRecord(long timestamp, TValue? data)
            {
                PingTimestamp = timestamp;
                Data = data;
            }
        }
        private readonly Dictionary<TKey, LimitRecord> _list = new();

        public ActionRateLimiter(int rate, int limit)
        {
            RateDelta = rate;
            Limit = limit;
            ExtremeLimit = Limit * 3;
        }

        public ActionRateLimiter(int rate, int limit, int extremelimit) : this(rate, limit)
        {
            ExtremeLimit = extremelimit;
        }

        public bool IsExtreme(TKey id)
        {
            return !_list.TryGetValue(id, out var value) || value.Count >= ExtremeLimit;
        }

        public bool IsBeingRateLimited(TKey id, TValue? data, bool renew = true)
        {
            long currentTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (_list.TryGetValue(id, out var value) == false)
            {
                if (renew)
                {
                    _list.Add(id, new LimitRecord(currentTimestamp + RateDelta, data));
                }
                return false;
            }

            if (value.PingTimestamp >= currentTimestamp)
            {
                value.Count++;
                return value.Count > Limit;
            }
            else
            {
                value.Count = 1;
            }

            if (renew)
            {
                value.PingTimestamp = currentTimestamp + RateDelta;
                _list[id] = value;
            }

            return false;
        }
    }
}
