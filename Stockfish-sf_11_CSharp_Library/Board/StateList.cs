using System.Collections.Generic;

namespace Stockfish_sf_11_CSharp_Library.Board
{
    /// <summary>Owned state history; adding entries never changes their object identity.</summary>
    public sealed class StateList
    {
        private readonly List<StateInfo> states = new List<StateInfo>();
        public int Count => states.Count;
        public StateInfo this[int index] => states[index];
        public StateInfo AddNew()
        {
            var state = new StateInfo();
            states.Add(state);
            return state;
        }
        public void Clear() => states.Clear();
    }
}
