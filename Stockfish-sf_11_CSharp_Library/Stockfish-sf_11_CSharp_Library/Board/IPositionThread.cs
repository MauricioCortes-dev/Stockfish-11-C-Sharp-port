namespace Stockfish_sf_11_CSharp_Library.Board
{
    /// <summary>Worker contract used by Position until the search thread is ported.</summary>
    public interface IPositionThread
    {
        void IncrementNodes();
    }
}
