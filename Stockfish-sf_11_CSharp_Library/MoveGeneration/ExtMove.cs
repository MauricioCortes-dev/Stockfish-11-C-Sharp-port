using Stockfish_sf_11_CSharp_Library.Types;
namespace Stockfish_sf_11_CSharp_Library.MoveGeneration
{
    /// <summary>Move and ordering score; assigning a move preserves the score.</summary>
    public struct ExtMove
    {
        public Move move;
        public int value;
        public static implicit operator Move(ExtMove item) => item.move;
        public void Assign(Move next) { move=next; }
        public static bool operator <(ExtMove a,ExtMove b) => a.value<b.value;
        public static bool operator >(ExtMove a,ExtMove b) => a.value>b.value;
    }
}
