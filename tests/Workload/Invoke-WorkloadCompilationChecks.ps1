[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $root 'scripts/workload/Workload.Support.ps1')
$checks=0
function Assert-Compilation([bool]$Pass,[string]$Reason){if(!$Pass){throw ('Compilation identity: '+$Reason)};$script:checks++}
$source=Get-WorkloadIdentity $root
$rows=@(Get-WorkloadArchitectureCompilationInputs $root $source.inputs)
$evaluations=$script:WorkloadArchitectureEvaluations
$again=@(Get-WorkloadArchitectureCompilationInputs $root $source.inputs)
Assert-Compilation ($evaluations -eq $script:WorkloadArchitectureEvaluations -and (Get-WorkloadHash $rows) -ceq (Get-WorkloadHash $again)) 'stable exact input reuses one real evaluated graph without another MSBuild process'
[xml]$project=Get-Content -LiteralPath (Join-Path $root 'tests/JueMingR.ArchitectureTests/JueMingR.ArchitectureTests.csproj') -Raw
$links=@($project.SelectNodes('//Compile[@Include]')|ForEach-Object {
    $full=[IO.Path]::GetFullPath((Join-Path (Join-Path $root 'tests/JueMingR.ArchitectureTests') ([string]$_.Include)))
    $full.Substring($root.TrimEnd('\').Length+1).Replace('\','/')
})
Assert-Compilation ($links.Count -ge 15) 'current real linked set is present (new links cannot lower the obligation)'
foreach($link in $links){
    $original=@($rows|Where-Object {($_ -split ':',2)[0] -ceq $link})
    Assert-Compilation ($original.Count -eq 1) ('actual SDK graph contains '+$link)
    $after=@($rows|ForEach-Object {if(($_ -split ':',2)[0] -ceq $link){$link+':'+('0'*64)}else{$_}})
    Assert-Compilation ((Get-WorkloadCompileFingerprint ([pscustomobject]@{inputs=$source.inputs;compilationInputs=$rows})) -cne (Get-WorkloadCompileFingerprint ([pscustomobject]@{inputs=$source.inputs;compilationInputs=$after}))) ('each evaluated linked assertion invalidates preparation '+$link)
}
Assert-Compilation (@($rows|Where-Object {$_ -like 'compilation-evaluation/Debug/*'}).Count -ge 4 -and @($rows|Where-Object {$_ -like 'compilation-evaluation/Release/*'}).Count -ge 4) 'both configurations and actual ProjectReference graph are evaluated'
$identity=[pscustomobject]@{inputs=@($source.inputs)+@('docs/unrelated.md:A');compilationInputs=$rows}
$changed=[pscustomobject]@{inputs=@($source.inputs)+@('docs/unrelated.md:B');compilationInputs=$rows}
Assert-Compilation ((Get-WorkloadCompileFingerprint $identity) -ceq (Get-WorkloadCompileFingerprint $changed)) 'unrelated documentation does not invalidate compilation'
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('JueMingR-compilation-'+[Guid]::NewGuid().ToString('N'))
try{
    $directory=Join-Path $fixture 'tests/JueMingR.ArchitectureTests';[IO.Directory]::CreateDirectory($directory)|Out-Null
    $path=Join-Path $directory 'JueMingR.ArchitectureTests.csproj'
    foreach($body in @('<Import Project="unknown.props" />','<Target Name="Generate"><ItemGroup><Compile Include="unknown.cs" /></ItemGroup></Target>')){
        [IO.File]::WriteAllText($path,('<Project Sdk="Microsoft.NET.Sdk">'+$body+'</Project>'))
        $rejected=$false;try{Get-WorkloadArchitectureCompilationInputs $fixture @()|Out-Null}catch{$rejected=$_.Exception.Message -like '*Unreviewed Architecture compilation import or target*'}
        Assert-Compilation $rejected 'unknown import/target refuses preparation reuse rather than dropping its Compile inputs'
    }
    [IO.Directory]::CreateDirectory((Join-Path $fixture 'extras'))|Out-Null
    $extra=Join-Path $fixture 'extras/Linked.cs';[IO.File]::WriteAllText($extra,'public static class Linked { public static string Value { get { return "real property-linked source"; } } }')
    $program=Join-Path $directory 'Program.cs';[IO.File]::WriteAllText($program,'class Program { static void Main() { } }')
    [IO.File]::WriteAllText($path,'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net472</TargetFramework><PlatformTarget>x86</PlatformTarget></PropertyGroup></Project>')
    function Get-CompilationFixtureRows {return @(Get-ChildItem -LiteralPath $fixture -Recurse -File|Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]'}|Sort-Object FullName|ForEach-Object {$_.FullName.Substring($fixture.Length+1).Replace('\','/')+':'+(Get-FileHash -LiteralPath $_.FullName).Hash})}
    $priorExtra=[Environment]::GetEnvironmentVariable('DefineConstants')
    try{
        [Environment]::SetEnvironmentVariable('DefineConstants',$null)
        $plain=@(Get-WorkloadArchitectureCompilationInputs $fixture (Get-CompilationFixtureRows));$count=$script:WorkloadArchitectureEvaluations
        $null=Get-WorkloadArchitectureCompilationInputs $fixture (Get-CompilationFixtureRows)
        Assert-Compilation ($script:WorkloadArchitectureEvaluations -eq $count) 'same freshly hashed fixture rows do not repeat actual evaluation'
        [Environment]::SetEnvironmentVariable('DefineConstants','R01_EXTRA_SYMBOL')
        $conditional=@(Get-WorkloadArchitectureCompilationInputs $fixture (Get-CompilationFixtureRows))
        Assert-Compilation ($script:WorkloadArchitectureEvaluations -gt $count -and (Get-WorkloadHash $plain) -cne (Get-WorkloadHash $conditional)) 'new environment compilation property changes actual evaluated constants and retires the cached graph'
        $count=$script:WorkloadArchitectureEvaluations
        [IO.File]::WriteAllText((Join-Path $directory 'Added.cs'),'class NewlyGlobbed {}')
        $withNewFile=@(Get-WorkloadArchitectureCompilationInputs $fixture (Get-CompilationFixtureRows))
        Assert-Compilation ($script:WorkloadArchitectureEvaluations -gt $count -and @($withNewFile|Where-Object {$_ -like '*Added.cs:*'}).Count -eq 1) 'new default-glob member re-evaluates instead of reusing stale Compile set'
        $lockedBeforeIgnored=Get-CompilationFixtureRows
        $ignoredDirectory=Join-Path $directory 'logs';[IO.Directory]::CreateDirectory($ignoredDirectory)|Out-Null
        [IO.File]::WriteAllText((Join-Path $ignoredDirectory 'IgnoredButCompiled.cs'),'class IgnoredButCompiled {}')
        $rejected=$false;try{Get-WorkloadArchitectureCompilationInputs $fixture $lockedBeforeIgnored|Out-Null}catch{$rejected=$_.Exception.Message -like '*absent or changed outside the locked batch*'}
        Assert-Compilation $rejected 'new SDK-glob member omitted from locked Git rows cannot reuse the old graph'
        Remove-Item -LiteralPath (Join-Path $ignoredDirectory 'IgnoredButCompiled.cs')
        $hidden=Join-Path $directory 'HiddenDefault.cs'
        try{
            [IO.File]::WriteAllText($hidden,'class HiddenDefault {}');[IO.File]::SetAttributes($hidden,[IO.FileAttributes]::Hidden)
            $hiddenJson=@(& dotnet.exe msbuild $path -nologo -p:Configuration=Debug -p:Platform=x86 -getItem:Compile)
            if($LASTEXITCODE -ne 0){throw 'Actual SDK hidden-source premise failed.'}
            $hiddenItems=($hiddenJson -join "`n")|ConvertFrom-Json
            Assert-Compilation (@($hiddenItems.Items.Compile|Where-Object {$_.FullPath -ceq $hidden}).Count -eq 1) 'actual SDK default Compile includes a real Windows Hidden .cs'
            $rejected=$false;try{Get-WorkloadArchitectureCompilationInputs $fixture (Get-CompilationFixtureRows)|Out-Null}catch{$rejected=$_.Exception.Message -like '*absent or changed outside the locked batch*'}
            Assert-Compilation $rejected 'Hidden default source omitted from locked rows cannot bypass member inventory or reuse cached preparation'
        }finally{if([IO.File]::Exists($hidden)){[IO.File]::SetAttributes($hidden,[IO.FileAttributes]::Normal);Remove-Item -LiteralPath $hidden}}
        $plainProject=[IO.File]::ReadAllText($path)
        [IO.File]::WriteAllText($path,$plainProject.Replace('</Project>','<ItemGroup><Compile Include="$(JMR_R01_EXTRA_COMPILE)" /></ItemGroup></Project>'))
        $rejected=$false;try{Get-WorkloadArchitectureCompilationInputs $fixture (Get-CompilationFixtureRows)|Out-Null}catch{$rejected=$_.Exception.Message -like '*item expression*'}
        Assert-Compilation $rejected 'unknown item property or custom glob refuses cached preparation rather than silently dropping inputs'
        [IO.File]::WriteAllText($path,$plainProject)
        foreach($name in @('Directory.Build.props','Directory.Build.targets')){
            $import=Join-Path $fixture $name
            foreach($body in @('<ItemGroup><Compile Include="$(MSBuildThisFileDirectory)extras/*.cs" /></ItemGroup>','<ItemGroup><ExternalSources Include="../../extras/*.cs" /><Compile Include="@(ExternalSources)" /></ItemGroup>')){
                [IO.File]::WriteAllText($import,('<Project>'+$body+'</Project>'))
                $rejected=$false;try{Get-WorkloadArchitectureCompilationInputs $fixture (Get-CompilationFixtureRows)|Out-Null}catch{$rejected=$_.Exception.Message -like '*item expression*'}
                Assert-Compilation $rejected ('auto-imported '+$name+' obeys the same property/glob/item-reference refusal as csproj')
            }
            Remove-Item -LiteralPath $import
        }
        $namespaced=Join-Path $fixture 'Directory.Build.props'
        $glob=(Join-Path ([IO.Path]::GetFullPath((Join-Path $fixture 'extras'))) '*.cs').Replace('&','&amp;')
        [IO.File]::WriteAllText($namespaced,('<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003"><ItemGroup><Compile Include="'+$glob+'" /></ItemGroup></Project>'))
        $namespaceJson=@(& dotnet.exe msbuild $path -nologo -p:Configuration=Debug -p:Platform=x86 -getItem:Compile)
        if($LASTEXITCODE -ne 0){throw 'Actual locked SDK did not accept the standard namespaced imported project fixture.'}
        $namespaceItems=($namespaceJson -join "`n")|ConvertFrom-Json
        Assert-Compilation (@($namespaceItems.Items.Compile|Where-Object {$_.FullPath -ceq $extra}).Count -eq 1) 'actual SDK accepts standard XML namespace with external glob Compile'
        $rejected=$false;try{Get-WorkloadArchitectureCompilationInputs $fixture (Get-CompilationFixtureRows)|Out-Null}catch{$rejected=$_.Exception.Message -like '*item expression*'}
        Assert-Compilation $rejected 'namespaced auto-import cannot bypass the literal item gate'
        [IO.File]::WriteAllText($namespaced,('<Project><ItemGroup><compile include="'+$glob+'" /></ItemGroup></Project>'))
        $rejected=$false;try{Get-WorkloadArchitectureCompilationInputs $fixture (Get-CompilationFixtureRows)|Out-Null}catch{$rejected=$_.Exception.Message -like '*item expression*'}
        Assert-Compilation $rejected 'item and Include attribute case variants cannot bypass the same gate'
        Remove-Item -LiteralPath $namespaced
        [IO.File]::WriteAllText($path,$plainProject.Replace('</Project>','<ItemGroup><ExternalSources Include="../../extras/*.cs" /><Compile Include="@(ExternalSources)" /></ItemGroup></Project>'))
        $rejected=$false;try{Get-WorkloadArchitectureCompilationInputs $fixture (Get-CompilationFixtureRows)|Out-Null}catch{$rejected=$_.Exception.Message -like '*item expression*'}
        Assert-Compilation $rejected 'project item reference cannot hide a dynamic external Compile set'
        [IO.File]::WriteAllText($path,$plainProject)
        $custom=Join-Path $fixture 'custom.props';[IO.File]::WriteAllText($custom,'<Project />')
        foreach($key in @('DirectoryBuildPropsPath','DirectoryBuildTargetsPath','CustomBeforeMicrosoftCommonTargets','CustomAfterMicrosoftCommonTargets','CustomBeforeMicrosoftCommonCrossTargetingTargets','CustomAfterMicrosoftCommonCrossTargetingTargets')){
            $priorImport=[Environment]::GetEnvironmentVariable($key)
            try{
                [Environment]::SetEnvironmentVariable($key,$custom)
                $rejected=$false;try{Get-WorkloadArchitectureCompilationInputs $fixture (Get-CompilationFixtureRows)|Out-Null}catch{$rejected=$_.Exception.Message -like '*compilation*import path*'}
                Assert-Compilation $rejected ('non-default import override refuses unbound file content '+$key)
            }finally{[Environment]::SetEnvironmentVariable($key,$priorImport)}
        }
    }finally{[Environment]::SetEnvironmentVariable('DefineConstants',$priorExtra)}
    $childDirectory=Join-Path $fixture 'child';[IO.Directory]::CreateDirectory($childDirectory)|Out-Null
    [IO.File]::WriteAllText((Join-Path $childDirectory 'Child.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net472</TargetFramework><PlatformTarget>x86</PlatformTarget></PropertyGroup><ItemGroup><Compile Include="../extras/Linked.cs" Condition="''$(UseExtraAssertions)'' == ''true''" /></ItemGroup></Project>')
    [IO.File]::WriteAllText($path,'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net472</TargetFramework><PlatformTarget>x86</PlatformTarget><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><ProjectReference Include="../../child/Child.csproj" AdditionalProperties="UseExtraAssertions=true" /></ItemGroup></Project>')
    [IO.File]::WriteAllText($program,'class Program { static void Main() { System.Console.WriteLine(Linked.Value); } }')
    & dotnet.exe build $path --configuration Debug --nologo -p:Platform=x86|Out-Host
    if($LASTEXITCODE -ne 0){throw 'Actual property-bearing ProjectReference fixture did not compile its linked assertion source.'}
    $exe=Join-Path $directory 'bin/x86/Debug/net472/JueMingR.ArchitectureTests.exe'
    $actual=@(& $exe)
    Assert-Compilation ($LASTEXITCODE -eq 0 -and $actual -contains 'real property-linked source') 'actual MSBuild edge properties select an observable real linked source'
    $rejected=$false;try{Get-WorkloadArchitectureCompilationInputs $fixture (Get-CompilationFixtureRows)|Out-Null}catch{$rejected=$_.Exception.Message -like '*property-bearing ProjectReference metadata*'}
    Assert-Compilation $rejected 'property-bearing reference is refused instead of evaluating that child with default properties'
}finally{
    $full=[IO.Path]::GetFullPath($fixture);$temp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if(!$full.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Leaf $full) -notlike 'JueMingR-compilation-*'){throw 'Unsafe compilation fixture cleanup.'}
    if([IO.Directory]::Exists($full)){Remove-Item -LiteralPath $full -Recurse -Force}
}
Write-Output ('PASS compilation identity checks='+$checks+'; actual SDK graph, all linked assertions, configuration/reference propagation and conservative rejection.')
