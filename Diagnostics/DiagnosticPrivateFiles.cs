using System.Security.AccessControl;
using System.Security.Principal;

namespace maps4.Diagnostics;

internal static class DiagnosticPrivateFiles
{
    internal static bool IsOutside(string path, string root)
    {
        string p = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        string r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        return !p.Equals(r, StringComparison.OrdinalIgnoreCase) &&
            !p.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    // Never changes ACLs. An operator must provision a private directory first.
    internal static void Validate(string directory, string contentRoot, string webRoot)
    {
        if (!OperatingSystem.IsWindows() || !Directory.Exists(directory) ||
            !IsOutside(directory, contentRoot) || !IsOutside(directory, webRoot))
            throw new InvalidOperationException();
        for (DirectoryInfo? d = new(directory); d is not null; d = d.Parent)
            if ((d.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException();
        CheckAcl(new DirectoryInfo(directory).GetAccessControl());
    }

    internal static void ValidateFile(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException();
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException();
        CheckAcl(new FileInfo(path).GetAccessControl());
    }

    private static void CheckAcl(FileSystemSecurity acl)
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException();
        string user = WindowsIdentity.GetCurrent().User!.Value;
        string[] allowed = [user, "S-1-5-18", "S-1-5-32-544"];
        if (acl.GetOwner(typeof(SecurityIdentifier)) is not SecurityIdentifier owner || !allowed.Contains(owner.Value))
            throw new InvalidOperationException();
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && !allowed.Contains(rule.IdentityReference.Value))
                throw new InvalidOperationException();
    }
}
