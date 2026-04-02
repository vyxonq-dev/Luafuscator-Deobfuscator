using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LuafuscatorDeobf
{
    public static class OutputWriter
    {
        public static void WriteFinalOutput(
            string path,
            string finalSource,
            List<DecodeResult> results,
            GCallParser parser = null,
            ConstantFolder folder = null,
            int astRenames = 0,
            int astErrors = 0)
        {
            var header = new StringBuilder();
            header.AppendLine($"-- {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            header.AppendLine($"--{results.Count} calls replaced");

            if (parser != null && (parser.FailedDecodes > 0 || parser.SkippedCalls > 0))
                header.AppendLine($"--     (failed: {parser.FailedDecodes}, skipped: {parser.SkippedCalls})");

            //if (folder != null)
            //{
            //    header.AppendLine($"--{folder.LambdasFolded} folded");
            //    header.AppendLine($"--{folder.VarConstants.Count} vars");
            //    header.AppendLine($"--folder.LfrChain.Count} entries");
            //}

            header.AppendLine($"-- [5] AST rename pass          : {astRenames} identifiers renamed");

            if (astErrors > 0)
                header.AppendLine($"--     (parser messages: {astErrors})");

            header.AppendLine();

            File.WriteAllText(path, header.ToString() + finalSource, Encoding.UTF8);
        }
    }
}
