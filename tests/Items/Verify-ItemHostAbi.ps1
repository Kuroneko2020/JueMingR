[CmdletBinding()]
param([string] $RepositoryRoot, [switch] $Inspect)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) { $RepositoryRoot = Join-Path $PSScriptRoot '..\..' }
$root = [IO.Path]::GetFullPath($RepositoryRoot)
$references = Join-Path $root 'external\TerrariaRefs'
$target = Join-Path $references 'Terraria.exe'
if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ine '960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3') { throw 'Unrecognized item host target.' }
# Reflection-only metadata/IL: no game method or static constructor is executed.
$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $name = [Reflection.AssemblyName]::new($eventArgs.Name).Name
    $path = Join-Path $references ($name + '.dll')
    if ([IO.File]::Exists($path)) { return [Reflection.Assembly]::ReflectionOnlyLoadFrom($path) }
    return [Reflection.Assembly]::ReflectionOnlyLoad($eventArgs.Name)
}
[AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve($resolver)
try {
    $assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom($target)
    $opcodes = @{}
    foreach ($field in [Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static')) {
        $opcode = $field.GetValue($null); $opcodes[[int]$opcode.Value -band 65535] = $opcode
    }
    function Read-Instructions([Reflection.MethodInfo] $method) {
        $bytes = $method.GetMethodBody().GetILAsByteArray(); $i = 0
        while ($i -lt $bytes.Length) {
            $offset = $i; $value = [int]$bytes[$i]; $i++
            if ($value -eq 254) { $value = 65024 + [int]$bytes[$i]; $i++ }
            $opcode = $opcodes[$value]; $operand = $null; $size = 0
            switch ($opcode.OperandType.ToString()) {
                'InlineNone' { }
                'ShortInlineI' { $size = 1; $operand = $bytes[$i] }
                'ShortInlineVar' { $size = 1; $operand = $bytes[$i] }
                'ShortInlineBrTarget' { $size = 1; $operand = $bytes[$i] }
                'InlineVar' { $size = 2 }
                'InlineI8' { $size = 8 }
                'InlineR' { $size = 8 }
                'InlineSwitch' { $size = 4 + 4 * [BitConverter]::ToInt32($bytes, $i) }
                default { $size = 4 }
            }
            if ($opcode.OperandType -in @('InlineField','InlineMethod','InlineType','InlineTok')) {
                $operand = $method.Module.ResolveMember([BitConverter]::ToInt32($bytes, $i))
            }
            [pscustomobject]@{ Offset = $offset; Code = $opcode.Name; Operand = $operand }
            $i += $size
        }
    }
    $flags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
    $player = $assembly.GetType('Terraria.Player', $true)
    $ammo = @($player.GetMethods($flags) | Where-Object Name -eq 'FillAmmo')
    if ($ammo.Count -ne 1) { throw 'FillAmmo signature count changed.' }
    $instructions = @(Read-Instructions $ammo[0]); $gates = @()
    for ($i = 1; $i -lt $instructions.Count - 3; $i++) {
        if ($instructions[$i].Code -ne 'ldfld' -or $instructions[$i].Operand.Name -ne 'type' -or $instructions[$i].Operand.DeclaringType.FullName -ne 'Terraria.Item') { continue }
        if ($instructions[$i - 1].Code -ne 'ldelem.ref') { throw 'Ammo type does not read an Item array element.' }
        $gates += ,@($instructions[($i - 1)..($i + 3)] | ForEach-Object Code)
        if ($Inspect) { $instructions[([Math]::Max(0,$i - 4))..($i + 5)] | Format-Table Offset,Code,Operand | Out-Host }
    }
    if ($gates.Count -ne 2) { throw 'Ammo receiver must have exactly two type gates.' }
    if (($gates[0][0..3] -join ',') -cne 'ldelem.ref,ldfld,ldc.i4.0,ble' -or
        ($gates[1][0..2] -join ',') -cne 'ldelem.ref,ldfld,brtrue.s') { throw 'Ammo predicates differ from the admitted receiver gates.' }
    $selection = @('ConsumeItem','FindPaintOrCoating','ItemCheck_CheckFishingBobber_ConsumeBait')
    foreach ($name in $selection) {
        $methods = @($player.GetMethods($flags) | Where-Object Name -eq $name)
        if ($methods.Count -ne 1) { throw "Selection method count changed: $name" }
        $body = @(Read-Instructions $methods[0]); $reads = @($body | Where-Object Code -eq 'ldelem.ref').Count
        if ($reads -eq 0) { throw "Selection read shape missing: $name" }
        Write-Output "ABI: $name Item[] read sites=$reads"
    }
    Write-Output ('ABI: FillAmmo gates=' + (($gates | ForEach-Object { $_ -join ',' }) -join ' | '))
    Write-Output 'PASS: fixed 1.4.5.8 item host read-only metadata/IL shape. This is not hook execution or game acceptance.'
}
finally { [AppDomain]::CurrentDomain.remove_ReflectionOnlyAssemblyResolve($resolver) }
