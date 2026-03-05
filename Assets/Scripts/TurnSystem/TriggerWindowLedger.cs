using System;
using System.Collections.Generic;
using PF2e.Core;

namespace PF2e.TurnSystem
{
    public enum TriggerWindowType
    {
        Unspecified = 0,
        MovementEnter = 1,
        AttackStart = 2,
        PostHitDamage = 3,
        GenericIncomingDamage = 4
    }

    public readonly struct TriggerWindowToken : IEquatable<TriggerWindowToken>
    {
        private readonly int id;

        internal TriggerWindowToken(int id)
        {
            this.id = id;
        }

        public bool IsValid => id > 0;

        public bool Equals(TriggerWindowToken other)
        {
            return id == other.id;
        }

        public override bool Equals(object obj)
        {
            return obj is TriggerWindowToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            return id;
        }

        public static bool operator ==(TriggerWindowToken left, TriggerWindowToken right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TriggerWindowToken left, TriggerWindowToken right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Tracks per-trigger-window reaction consumption. Windows can be nested;
    /// each window has an independent consumed actor set.
    /// </summary>
    public sealed class TriggerWindowLedger
    {
        private readonly Dictionary<TriggerWindowToken, WindowState> windows = new();
        private readonly List<TriggerWindowToken> openStack = new();
        private int nextTokenId = 1;

        public int OpenDepth => openStack.Count;

        public TriggerWindowToken OpenWindow(
            TriggerWindowType windowType,
            EntityHandle source = default,
            EntityHandle target = default)
        {
            var token = new TriggerWindowToken(nextTokenId++);
            windows[token] = new WindowState(windowType, source, target);
            openStack.Add(token);
            return token;
        }

        public void CloseWindow(TriggerWindowToken token)
        {
            if (!token.IsValid)
                return;
            if (!windows.Remove(token))
                return;

            for (int i = openStack.Count - 1; i >= 0; i--)
            {
                if (openStack[i] != token)
                    continue;

                openStack.RemoveAt(i);
                break;
            }
        }

        public bool IsOpen(TriggerWindowToken token)
        {
            return token.IsValid && windows.ContainsKey(token);
        }

        public bool TryGetWindowType(TriggerWindowToken token, out TriggerWindowType windowType)
        {
            windowType = TriggerWindowType.Unspecified;
            if (!windows.TryGetValue(token, out var state))
                return false;

            windowType = state.WindowType;
            return true;
        }

        public bool TryGetCurrentWindow(out TriggerWindowToken token)
        {
            token = default;
            if (openStack.Count <= 0)
                return false;

            token = openStack[openStack.Count - 1];
            return IsOpen(token);
        }

        public bool CanReact(TriggerWindowToken token, EntityHandle actor)
        {
            if (!actor.IsValid)
                return false;
            if (!windows.TryGetValue(token, out var state))
                return false;

            return !state.ConsumedActors.Contains(actor);
        }

        public bool MarkReacted(TriggerWindowToken token, EntityHandle actor)
        {
            if (!CanReact(token, actor))
                return false;

            var state = windows[token];
            state.ConsumedActors.Add(actor);
            return true;
        }

        public void Clear()
        {
            windows.Clear();
            openStack.Clear();
            nextTokenId = 1;
        }

        private sealed class WindowState
        {
            public readonly TriggerWindowType WindowType;
            public readonly EntityHandle Source;
            public readonly EntityHandle Target;
            public readonly HashSet<EntityHandle> ConsumedActors = new();

            public WindowState(TriggerWindowType windowType, EntityHandle source, EntityHandle target)
            {
                WindowType = windowType;
                Source = source;
                Target = target;
            }
        }
    }
}
