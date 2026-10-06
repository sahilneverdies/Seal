

using System.Reflection;

[assembly: AssemblyProduct("Seal")]
[assembly: AssemblyCompany("Seal")]
[assembly: AssemblyCopyright("Copyright (C) 2026 Seal contributors - GPL-3.0-or-later")]
[assembly: AssemblyVersion(Seal.Meta.Version)]
[assembly: AssemblyFileVersion(Seal.Meta.Version)]
[assembly: AssemblyInformationalVersion(Seal.Meta.Version)]

namespace Seal {

    public static class Meta {
        public const string Version = "1.2.1";
    }
}
