using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Layouter.Utility
{
    public static class Env
    {
        public static string AppName { get; set; } = "Layouter";

        public static string UpdateUrl { get; set; } = "https://raw.githubusercontent.com/VrezenStrijder/Layouter/refs/heads/main/update.json";

        public static string ReleasePageUrl { get; set; } = "https://github.com/VrezenStrijder/Layouter/releases";

        public static string FrameworkDependencyVersionFolderName { get; set; } = "Layouter_frameworkdependency";
        public static string IndependentVersionFolderName { get; set; } = "Layouter_independent";

        public static string HiddenFolderName { get; set; } = ".layouterhidden";
    }
}
