$ErrorActionPreference = "Stop"

$assemblyName = 'Webvoto.VotingSystem.Auditing'
$isPublic = $true
$forceRepo = 'Azure'

$optionYes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes", "Yes"
$optionCancel = New-Object System.Management.Automation.Host.ChoiceDescription "&Cancel", "Cancel"
$optionsYesCancel = [System.Management.Automation.Host.ChoiceDescription[]]($optionYes, $optionCancel)

function Write-Log($s) {
	Write-Host ""
	Write-Host -ForegroundColor Green (">>> {0}" -f $s)
	Write-Host ""
}

function Check-ExitCode($errorMessage) {
	if ($LASTEXITCODE -ne 0) {
		throw $errorMessage
	}
}

function Get-ApiKey($name) {
	if ($name -ieq 'Azure') {
		return 'DummyApiKey'
	}
	$envVarName = ($name + '_API_KEY').ToUpper()
	$apiKey = $null
	if (Test-Path "env:$envVarName") {
		$apiKey = (Get-Item "env:$envVarName").Value
	}
	if ([string]::IsNullOrWhiteSpace($apiKey)) {
		$apiKey = Read-Host "Enter the $name API Key (hint -- set the environment variable $envVarName to skip this step)"
	}
	if ([string]::IsNullOrWhiteSpace($apiKey)) {
		return $null
	}
	return $apiKey.Trim()
}

try {

	#
	# Find package
	#
	Write-Log "Locating package ..."
	$packages = (Get-ChildItem . | ? { $_.Name.StartsWith($assemblyName) -and $_.Name.EndsWith('.nupkg') -and (-not $_.Name.Contains('symbols')) })
	if ($packages.Count -eq 0) {
		Write-Warning "Could not find a suitable package to upload"
		exit 1
	}
	if ($packages.Count -gt 1) {
		Write-Warning ('Found multiple ({0}) packages, aborting' -f $packages.Count)
		exit 2
	}
	$package = $packages[0]
	$isStable = (-not ($package.Name -match '-\w+\.nupkg'))
	Write-Log ('Package located: {0} (stable: {1})' -f $package.Name, $isStable)

	#
	# Decide package repository
	#
	$packageRepo = 'Azure'
	if ($forceRepo) {
		$packageRepo = $forceRepo
	} elseif ($isStable -and $isPublic) {
		$packageRepo = 'Nuget'
	}
	Write-Log "Package repository: $packageRepo"
	
	#
	# Get API key
	#
	$apiKey = Get-ApiKey $packageRepo
	if ($apiKey -eq $null) {
		Write-Warning "API key not given, aborting"
		exit 4
	}
	$apiKey = $apiKey.Trim()
	Write-Log "$packageRepo API key: $apiKey"
	
	#
	# Confirm
	#
	$choice = $host.ui.PromptForChoice('Confirmation', "Ready to upload to $packageRepo, continue?", $optionsYesCancel, 0)
	
	if ($choice -ne 0) {
		Write-Warning "User cancelled the operation"
		exit 5
	}
	
	#
	# Upload
	#
	Write-Log "Uploading to $packageRepo ..."
	if ($packageRepo -ieq 'Azure') {
	
		#
		# Azure (without symbols)
		#
		& nuget push $package.Name $apiKey -Source (Get-Item "env:AZURE_PACKAGE_SOURCE").Value
	
	} elseif ($packageRepo -ieq 'Nuget') {
	
		#
		# Nuget (without symbols)
		#
		& nuget push $package.Name $apiKey -Source https://api.nuget.org/v3/index.json
	
	} else {
		throw "Unsupported package repository: $packageRepo"
	}
	Check-ExitCode "An error occurred while uploading the package"
	
	#
	# End
	#
	Write-Log "Done."

} catch {

	Write-Host -ForegroundColor Red ("FATAL: An unhandled exception has occurred`n{0}`n{1}" -f $_.Exception, $_.ScriptStackTrace)
	
}
