using System.Runtime.InteropServices;

namespace IRIS.UI.Helpers
{
    public sealed class NaturalSortComparer : IComparer<string?>
    {
        public static readonly NaturalSortComparer Instance = new();

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        private static extern int StrCmpLogicalW(string psz1, string psz2);

        public int Compare(string? x, string? y) => (x, y) switch
        {
            (null, null) => 0,
            (null, _) => -1,
            (_, null) => 1,
            _ => StrCmpLogicalW(x, y)
        };
    }
}
