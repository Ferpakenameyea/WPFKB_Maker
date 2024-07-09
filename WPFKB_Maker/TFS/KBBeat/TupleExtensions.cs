namespace WPFKB_Maker.TFS.KBBeat
{
    public static class TupleExtensions
    {
        public static (int, int) Add(this (int, int) tuple, (int, int) other)
        {
            return (tuple.Item1 + other.Item1, tuple.Item2 + other.Item2);
        }

        public static (int, int) Minus(this (int, int) tuple, (int, int) other)
        {
            return (tuple.Item1 - other.Item1, tuple.Item2 - other.Item2);
        }
    }
}
