using System;
using System.Collections.Generic;

namespace Jongreul.XrLocomotion.Skills
{
    [Serializable]
    public sealed class CubeSettings
    {
        public int MaxCount = 3;

        /// <summary>만든 뒤 사라지기까지(초).</summary>
        public float Lifetime = 6f;

        /// <summary>밟고 있으면 적어도 이만큼은 남게 늘린다(발밑이 갑자기 사라지지 않게).</summary>
        public float StandGrace = 2f;

        /// <summary>밟아서 늘려도 만든 뒤 이 시간을 넘지 않는다.</summary>
        public float MaxLifetime = 12f;
    }

    public enum CubeRemoveReason
    {
        Expired,
        /// <summary>최대 개수를 넘어 가장 오래된 것이 밀려났다.</summary>
        Replaced,
        Cleared,
    }

    /// <summary>
    /// 손 앞에 만드는 발판 큐브의 수명 관리. 최대 개수를 넘으면 가장 오래된 것부터 없애고,
    /// 수명이 다하면 사라지되 밟고 있는 동안은 잠깐씩 연장한다(상한 있음). 위치·충돌은 Unity 쪽 몫.
    /// </summary>
    public sealed class CubePlatforms
    {
        sealed class Entry
        {
            public int Id;
            public double SpawnedAt;
            public double ExpiresAt;
        }

        readonly CubeSettings _settings;
        readonly IClock _clock;
        readonly List<Entry> _cubes = new List<Entry>();
        readonly List<int> _ids = new List<int>();
        int _nextId = 1;

        public CubePlatforms(IClock clock, CubeSettings settings = null)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _settings = settings ?? new CubeSettings();
            if (_settings.MaxCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(settings), "MaxCount는 1 이상");
        }

        public CubeSettings Settings => _settings;
        public int Count => _cubes.Count;

        /// <summary>오래된 것부터.</summary>
        public IReadOnlyList<int> Ids
        {
            get
            {
                _ids.Clear();
                foreach (Entry cube in _cubes)
                    _ids.Add(cube.Id);
                return _ids;
            }
        }

        public event Action<int> Spawned;
        public event Action<int, CubeRemoveReason> Removed;

        public int Spawn()
        {
            if (_cubes.Count >= _settings.MaxCount)
                RemoveAt(0, CubeRemoveReason.Replaced);

            double now = _clock.Now;
            var cube = new Entry { Id = _nextId++, SpawnedAt = now, ExpiresAt = now + _settings.Lifetime };
            _cubes.Add(cube);
            Spawned?.Invoke(cube.Id);
            return cube.Id;
        }

        /// <summary>이 큐브를 밟고 있다. 없는 큐브면 false.</summary>
        public bool Stand(int id)
        {
            Entry cube = Find(id);
            if (cube == null)
                return false;

            double extended = Math.Max(cube.ExpiresAt, _clock.Now + _settings.StandGrace);
            cube.ExpiresAt = Math.Min(extended, cube.SpawnedAt + _settings.MaxLifetime);
            return true;
        }

        /// <summary>수명이 다한 큐브를 없앤다.</summary>
        /// <returns>없앤 개수.</returns>
        public int Update()
        {
            int removed = 0;
            double now = _clock.Now;
            for (int i = _cubes.Count - 1; i >= 0; i--)
            {
                if (now >= _cubes[i].ExpiresAt)
                {
                    RemoveAt(i, CubeRemoveReason.Expired);
                    removed++;
                }
            }

            return removed;
        }

        public double Remaining(int id)
        {
            Entry cube = Find(id);
            return cube == null ? 0 : Math.Max(0, cube.ExpiresAt - _clock.Now);
        }

        public void Clear()
        {
            while (_cubes.Count > 0)
                RemoveAt(0, CubeRemoveReason.Cleared);
        }

        Entry Find(int id)
        {
            foreach (Entry cube in _cubes)
            {
                if (cube.Id == id)
                    return cube;
            }

            return null;
        }

        void RemoveAt(int index, CubeRemoveReason reason)
        {
            int id = _cubes[index].Id;
            _cubes.RemoveAt(index);
            Removed?.Invoke(id, reason);
        }
    }
}
