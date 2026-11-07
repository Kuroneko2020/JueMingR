# The solution partition already binds production source. Its console test
# project also compiles linked assertions outside that partition. Ask the real
# locked SDK for those items, including both configurations and references;
# never treat the two standalone-fixture XML parser as an MSBuild evaluator.
$script:WorkloadArchitectureCompilationCache=$null
$script:WorkloadArchitectureEvaluations=0
function Assert-WorkloadCompilationDocument {
    param([xml]$Document,[string]$Kind)
    # This bounded graph accepts literal custom item paths plus SDK defaults.
    # Apply the same rule to project and auto-imported props/targets; an item
    # reference/transform or property function can hide a new external glob.
    if($Document.OuterXml -match '@\(|%\(|\$\(\s*\['){throw 'Unreviewed Architecture compilation item expression.'}
    # MSBuild accepts its standard XML namespace and treats item/property
    # names without case sensitivity. Exact unqualified XPath would miss an
    # accepted namespaced/lowercase Compile and reopen the same cache hole.
    foreach($item in $Document.SelectNodes('//*')){
        $name=$item.LocalName.ToLowerInvariant()
        if($name -in @('import','target')){throw ('Unreviewed Architecture '+$Kind+' import or target.')}
        if($name -in @('compile','projectreference')){
            foreach($attribute in $item.Attributes){if($attribute.LocalName -ieq 'Include' -and $attribute.Value -match '[$*@?%]'){throw 'Unreviewed Architecture compilation item expression.'}}
        }
    }
}
function Get-WorkloadCompilationMembers {
    param([string[]]$Directories)
    # Git can ignore a new .cs under logs/.local while the SDK default glob
    # still compiles it. Membership must therefore be read from the real graph
    # directories before cache reuse; hashes still come from the locked rows.
    return @($Directories|ForEach-Object {Get-ChildItem -LiteralPath $_ -Recurse -File -Filter '*.cs' -Force|ForEach-Object {$_.FullName}}|Sort-Object -Unique)
}
function Get-WorkloadCompilationEnvironmentRows {
    param([string]$Root,[string[]]$InputRows)
    $names=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach($name in @('EnableDefaultItems','EnableDefaultCompileItems','DefaultLanguageSourceExtension','DefaultItemExcludes','DefaultExcludesInProjectFolder','ImportDirectoryBuildProps','DirectoryBuildPropsPath','ImportDirectoryBuildTargets','DirectoryBuildTargetsPath','CustomBeforeMicrosoftCommonTargets','CustomAfterMicrosoftCommonTargets','CustomBeforeMicrosoftCommonCrossTargetingTargets','CustomAfterMicrosoftCommonCrossTargetingTargets')){[void]$names.Add($name)}
    # MSBuild imports environment properties. Bind properties referenced by our
    # own locked project/props/targets, including a newly added conditional
    # Compile property; an arbitrary environment change cannot reuse that graph.
    foreach($row in $InputRows){
        $relative=($row -split ':',2)[0]
        if($relative -notmatch '\.(csproj|props|targets)$'){continue}
        $path=Join-Path $Root $relative;if(![IO.File]::Exists($path)){continue}
        foreach($match in [regex]::Matches([IO.File]::ReadAllText($path),'\$\(([A-Za-z_][A-Za-z0-9_]*)\b')){[void]$names.Add($match.Groups[1].Value)}
    }
    return @(Get-ChildItem Env:|Where-Object {$names.Contains($_.Name) -or $_.Name -match '^(DOTNET_|MSBUILD|COMPlus_|JueMingR|Configuration$|Platform$|DefineConstants$|TargetFramework$)'}|Sort-Object Name|ForEach-Object {'environment:'+ $_.Name+':'+(Get-WorkloadHash @($_.Value))})
}
function Get-WorkloadArchitectureCompilationInputs {
    param([string]$Root,[string[]]$InputRows)
    $base=[IO.Path]::GetFullPath($Root).TrimEnd('\')+'\'
    $recipe=(Get-WorkloadFileHash (Join-Path $PSScriptRoot 'Workload.Compilation.ps1')).Hash
    $dotnet=(Get-Command dotnet.exe -ErrorAction Stop).Source
    $key=Get-WorkloadHash (@($base,$recipe,($dotnet+':'+(Get-WorkloadFileHash $dotnet).Hash))+@($InputRows)+@(Get-WorkloadCompilationEnvironmentRows $Root $InputRows))
    # One exact-content entry in this process only. Callers re-read the whole
    # source/dependency/environment identity before each stability boundary;
    # new files/glob members, imports, properties, SDK or recipe retire it.
    if($null -ne $script:WorkloadArchitectureCompilationCache -and $script:WorkloadArchitectureCompilationCache.key -ceq $key -and
        (Get-WorkloadHash @(Get-WorkloadCompilationMembers $script:WorkloadArchitectureCompilationCache.directories)) -ceq $script:WorkloadArchitectureCompilationCache.members -and
        @($script:WorkloadArchitectureCompilationCache.optionalSdkImports|Where-Object {[IO.File]::Exists($_)}).Count -eq 0){return $script:WorkloadArchitectureCompilationCache.rows}
    $locked=@{};foreach($row in $InputRows){$parts=$row -split ':',2;$locked[$parts[0]]=$parts[1]}
    $projects=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    $pending=New-Object 'System.Collections.Generic.Queue[string]'
    $pending.Enqueue('tests/JueMingR.ArchitectureTests/JueMingR.ArchitectureTests.csproj')
    $files=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    $evaluation=New-Object 'System.Collections.Generic.List[string]'
    $optionalSdkImports=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    while($pending.Count){
        $project=$pending.Dequeue();if(!$projects.Add($project)){continue}
        if($projects.Count -gt 32){throw 'Unreviewed compilation project graph size.'}
        $projectPath=Join-Path $Root $project
        [xml]$xml=Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8
        # Item evaluation cannot see arbitrary target-time generators. Current
        # Architecture/Platform/Features/Infrastructure have none. An added
        # import/generator needs its actual propagation reviewed before reuse.
        Assert-WorkloadCompilationDocument $xml 'compilation'
        for($directory=Split-Path -Parent $projectPath;$directory.Length -ge $base.Length-1;$directory=Split-Path -Parent $directory){
            foreach($name in @('Directory.Build.props','Directory.Build.targets')){
                $import=Join-Path $directory $name
                if([IO.File]::Exists($import)){
                    [xml]$importXml=Get-Content -LiteralPath $import -Raw -Encoding UTF8
                    Assert-WorkloadCompilationDocument $importXml 'directory'
                    [void]$files.Add($import.Substring($base.Length).Replace('\','/'))
                }
            }
            if($directory -ceq $base.TrimEnd('\')){break}
        }
        [void]$files.Add($project)
        foreach($configuration in @('Debug','Release')){
            $buildRoot=Join-Path $Root ('artifacts/build/'+$configuration+'/work')
            $script:WorkloadArchitectureEvaluations++
            $json=@(& dotnet.exe msbuild $projectPath -nologo ('-p:Configuration='+$configuration) '-p:Platform=x86' ('-p:JueMingRBuildRoot='+$buildRoot) '-getItem:Compile,ProjectReference' '-getProperty:DefineConstants,TargetFramework,PlatformTarget,DefaultLanguageSourceExtension,MSBuildToolsPath,DirectoryBuildPropsPath,DirectoryBuildTargetsPath,CustomBeforeMicrosoftCommonTargets,CustomAfterMicrosoftCommonTargets,CustomBeforeMicrosoftCommonCrossTargetingTargets,CustomAfterMicrosoftCommonCrossTargetingTargets' 2>&1)
            if($LASTEXITCODE -ne 0){throw ('Actual Architecture Compile evaluation failed: '+($json -join ' '))}
            try{$value=($json -join "`n")|ConvertFrom-Json}catch{throw 'Actual Architecture Compile evaluation did not return JSON.'}
            if($value.Properties.TargetFramework -cne 'net472' -or $value.Properties.PlatformTarget -cne 'x86' -or $value.Properties.DefaultLanguageSourceExtension -cne '.cs'){throw 'Unreviewed Architecture compilation target.'}
            foreach($name in @('DirectoryBuildPropsPath','DirectoryBuildTargetsPath')){
                $import=[string]$value.Properties.$name;if(!$import){continue};$full=[IO.Path]::GetFullPath($import)
                $expected=if($name -ceq 'DirectoryBuildPropsPath'){'Directory.Build.props'}else{'Directory.Build.targets'}
                if(!$full.StartsWith($base,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($full) -cne $expected -or
                    !$files.Contains($full.Substring($base.Length).Replace('\','/'))){throw 'Unreviewed compilation directory import path.'}
            }
            foreach($side in @('Before','After')){
                $name='Custom'+$side+'MicrosoftCommonTargets';$import=[string]$value.Properties.$name
                if($import){
                    $full=[IO.Path]::GetFullPath($import)
                    $sdkDefault=[IO.Path]::GetFullPath((Join-Path $value.Properties.MSBuildToolsPath ('vCurrent/Custom.'+$side+'.Microsoft.Common.targets')))
                    if($full -ine $sdkDefault -or [IO.File]::Exists($full)){throw 'Unreviewed custom compilation import path.'}
                    [void]$optionalSdkImports.Add($full)
                }
                if([string]$value.Properties.('Custom'+$side+'MicrosoftCommonCrossTargetingTargets')){throw 'Unreviewed custom compilation import path.'}
            }
            $items=@($value.Items.Compile)
            if(!$items.Count -or $items.Count -gt 20000){throw 'Invalid actual Compile item count.'}
            $itemPaths=@(foreach($item in $items){
                $full=[IO.Path]::GetFullPath([string]$item.FullPath)
                if(!$full.StartsWith($base,[StringComparison]::OrdinalIgnoreCase)){throw 'Actual Compile input escapes the locked repository.'}
                $relative=$full.Substring($base.Length).Replace('\','/')
                [void]$files.Add($relative);$relative
            })
            $references=@(foreach($reference in @($value.Items.ProjectReference)){
                # A project path alone is not an MSBuild graph node when the
                # edge supplies properties. Until those non-default edges are
                # supported, reject them instead of evaluating the wrong node.
                $ordinary=@('Identity','Targets','OutputItemType','ReferenceSourceTarget','FullPath','RootDir','Filename','Extension','RelativeDir','Directory','RecursiveDir','ModifiedTime','CreatedTime','AccessedTime','DefiningProjectFullPath','DefiningProjectDirectory','DefiningProjectName','DefiningProjectExtension')
                foreach($metadata in $reference.PSObject.Properties){
                    if($metadata.Name -cnotin $ordinary -and [string]$metadata.Value){throw ('Unreviewed property-bearing ProjectReference metadata: '+$metadata.Name)}
                }
                if([string]$reference.Targets -or [string]$reference.OutputItemType){throw 'Unreviewed ProjectReference target or output propagation.'}
                $full=[IO.Path]::GetFullPath([string]$reference.FullPath)
                if(!$full.StartsWith($base,[StringComparison]::OrdinalIgnoreCase)){throw 'Actual ProjectReference escapes the locked repository.'}
                $relative=$full.Substring($base.Length).Replace('\','/')
                $pending.Enqueue($relative);$relative
            })
            $evaluation.Add('compilation-evaluation/'+$configuration+'/'+$project+':'+(Get-WorkloadHash (@($value.Properties.DefineConstants)+@($itemPaths|Sort-Object)+@($references|Sort-Object))))
        }
    }
    $rows=@(foreach($relative in @($files|Sort-Object)){
        $path=Join-Path $Root $relative
        if(!$locked.ContainsKey($relative) -or ![IO.File]::Exists($path) -or
            (Get-WorkloadFileHash $path).Hash -cne $locked[$relative]){throw ('Actual Compile input absent or changed outside the locked batch: '+$relative)}
        $relative+':'+$locked[$relative]
    })
    $result=@($rows)+@($evaluation)+@('compilation-evaluator:'+$recipe)
    $directories=@($projects|ForEach-Object {Split-Path -Parent (Join-Path $Root $_)})
    $script:WorkloadArchitectureCompilationCache=@{key=$key;rows=$result;directories=$directories;members=(Get-WorkloadHash @(Get-WorkloadCompilationMembers $directories));optionalSdkImports=@($optionalSdkImports)}
    return $result
}
