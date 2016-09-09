properties {
	$configuration = "Release"
	$platform = "Any CPU"
	$folderPath = ".\"
	$cleanPackages = $false
	$oldEnvPath = ""
	$buildOutput = "..\artifacts"
	$fwkVersions = "4.5", "4.5.2", "4.6.1"
}

task default -depends CleanUpMsBuildPath

task CleanUpMsBuildPath -depends BuildPackages {
	if($oldEnvPath -ne "")
	{
		Write-Host "Reverting Path variable"
		$Env:Path = $oldEnvPath
	}
}

task BuildPackages -depends Test {
	Exec { ..\src\.nuget\nuget.exe pack -Properties Configuration=$configuration -OutputDirectory $buildOutput ..\NEventStore.Contrib.Persistence.nuspec }
#	Exec { .\.nuget\nuget.exe pack  -Properties Configuration=$configuration -OutputDirectory $buildOutput ..\NEventStore.Contrib.Persistence.symbols.nuspec -Symbols }
}

task Test -depends Compile {
	'Running Tests'
	foreach($fwk in $fwkVersions) {
		Write-Host "Building v. $fwk"
		$output = ".\$buildOutput\$fwk\$configuration"
		$firebird = Resolve-Path "$output\NEventStore.Persistence.FirebirdSql.Tests.dll"
		
		Exec { ..\src\packages\xunit.runners.1.9.1\tools\xunit.console.clr4.exe $firebird }
	}
}

task Compile -depends UpdatePackages {
	$msbuild = Resolve-Path "${Env:ProgramFiles(x86)}\MSBuild\12.0\Bin\MSBuild.exe"
	foreach($fwk in $fwkVersions) {
		$output = "..\$buildOutput\$fwk\$configuration"
		$options = ""
		Write-Host ($fwk -eq "4.5")
		if($fwk -eq "4.5") {
			$options = "/p:configuration=$configuration;DefineConstants=NET45;platform=$platform;TargetFrameworkVersion=v$fwk;OutputPath=$output"
		}
		else{
			$options = "/p:configuration=$configuration;platform=$platform;TargetFrameworkVersion=v$fwk;OutputPath=$output"
		}
		Exec { & $msbuild ..\src\NEventStore.Contrib.sln $options }
	}
	'Executed Compile!'
}

task UpdatePackages -depends Clean {
	$packageConfigs = Get-ChildItem -Path ..\ -Include "packages.config" -Recurse
	foreach($config in $packageConfigs){
		#Write-Host $config.DirectoryName
		Exec { ..\src\.nuget\nuget.exe i $config.FullName -o ..\src\packages -source https://nuget.org/api/v2/ }
	}
}

task Clean -depends CheckMsBuildPath { 
	Get-ChildItem $folderPath -include bin,obj -Recurse | foreach ($_) { remove-item $_.fullname -Force -Recurse }
	if($cleanPackages -eq $true){
		if(Test-Path "$folderPath\packages"){
			Get-ChildItem "$folderPath\packages" -Recurse | Where { $_.PSIsContainer } | foreach ($_) { Write-Host $_.fullname; remove-item $_.fullname -Force -Recurse }
		}
	}
	
	if(Test-Path "$folderPath\$buildOutput"){
		Get-ChildItem "$folderPath\$buildOutput" -Recurse | foreach ($_) { Write-Host $_.fullname; remove-item $_.fullname -Force -Recurse }
	}
}

task CheckMsBuildPath {
	$envPath = $Env:Path
	if($envPath.Contains("C:\Windows\Microsoft.NET\Framework\v4.0") -eq $false)
	{
		if(Test-Path "C:\Windows\Microsoft.NET\Framework\v4.0.30319")
		{
			$oldEnvPath = $envPath
			$Env:Path = $envPath + ";C:\Windows\Microsoft.NET\Framework\v4.0.30319"
		}
		else
		{
			throw "Could not determine path to MSBuild. Make sure you have .NET 4.0.30319 installed"
		}
	}
}

task ? -Description "Helper to display task info" {
	Write-Documentation
}
