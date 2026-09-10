[CmdletBinding()]
param(
    [string] $TerrariaExePath,
    [string] $XnaGameAssemblyPath,
    [string] $XnaFrameworkAssemblyPath,
    [string] $XnaGraphicsAssemblyPath,
    [string] $HarmonyPackagePath,
    [switch] $VerifyPhase0TBiomePackage,
    [switch] $VerifyPhase0UF5UIPackage,
    [switch] $VerifyPhase0VSettingsPackage
)

# Retired by the accepted single-workspace policy. Keep the old command name
# as an explicit stop, not an implicit worktree creator. Previous behavior is
# available in Git history; this entry performs no source or output mutations.
throw 'This worktree-based verifier is retired. Use sequential clean builds and package comparisons in the current checkout as documented in docs/设计/可重复构建与项目骨架.md. No worktree was created.'
