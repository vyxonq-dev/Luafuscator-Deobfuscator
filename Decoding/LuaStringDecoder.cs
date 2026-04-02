using System.Collections.Generic;
using System.Text;

namespace LuafuscatorDeobf
{
    
    public class LuaStringDecoder
    {
        private static readonly char[] CharTable;

        static LuaStringDecoder()
        {
            CharTable = new char[256];
            for (int i = 0; i < 256; i++)
                CharTable[i] = (char)i;
        }

        public LuaStringDecoder(LuaConstants _) { }

        private static int BitwiseAnd(int a, int b)
        {
            int y = 0, bit = 1;
            while (bit <= 128)
            {
                if (a % (bit + bit) >= bit && b % (bit + bit) >= bit) y += bit;
                bit += bit;
            }
            return y;
        }

        private static int Xor(int a, int b) => (a + b) - 2 * BitwiseAnd(a, b);

        public string Decode(List<int> data, Dictionary<int, int> lookup, int seed, int extra)
        {
            if (data == null) return null;
            if (data.Count == 0) return "";

            var sb    = new StringBuilder(data.Count);
            int state = ((seed % 256) + 256) % 256;
            int xkey  = ((extra % 256) + 256) % 256;

            for (int idx = 0; idx < data.Count; idx++)
            {
                int O = idx + 1;
                int s = data[idx] & 0xFF;

                int tKey = s + 1;
                int tVal = lookup.TryGetValue(tKey, out int lv) ? lv : 0;
                int L    = Xor(tVal, xkey);

                int posKey = (((O - 1) * 13 + 7) % 256 + 256) % 256;
                int Z      = Xor(Xor(L, state), posKey) & 0xFF;

                sb.Append(CharTable[Z]);

                state = (((s * 3 + (O - 1) * 5) + state) % 256 + 256) % 256;
            }
            return sb.ToString();
        }

        public static bool IsPrintable(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            foreach (char c in s)
                if (c != '\n' && c != '\r' && c != '\t' && (c < 0x20 || c > 0x7E))
                    return false;
            return true;
        }
    }
}
