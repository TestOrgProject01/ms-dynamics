#Path to AssemblyInfo.cs
$assemblyInfoPath = "..\Properties\AssemblyInfo.cs"

# Read the AssemblyInfo.cs content
$assemblyInfoContent = Get-Content $assemblyInfoPath

# Extract the LastBuiltAssembly version
$lastBuiltAssemblyPattern = 'LastBuiltAssembly\("(\d+\.\d+\.\d+\.\d+)"\)'
$lastBuiltAssemblyMatch = $assemblyInfoContent -match $lastBuiltAssemblyPattern
$lastBuiltAssemblyVersion = $matches[1]

# Increment the minor version
$versionParts = $lastBuiltAssemblyVersion.Split('.')
$minorVersion = [int]$versionParts[3] + 1
# $newLastBuiltAssemblyVersion = "$($versionParts[0]).$($versionParts[1]).$minorVersion"

# Extract the current AssemblyVersion
$assemblyVersionPattern = 'AssemblyVersion\("(\d+\.\d+\.\d+\.\d+)"\)'
$assemblyVersionMatch = $assemblyInfoContent -match $assemblyVersionPattern
$currentAssemblyVersion = $matches[1]

# Replace the minor version in the current AssemblyVersion
$currentVersionParts = $currentAssemblyVersion.Split('.')
$newAssemblyVersion = "$($currentVersionParts[0]).$($currentVersionParts[1]).$($currentVersionParts[2].$minorVersion)"

# Update the AssemblyInfo.cs content
# $updatedContent = $assemblyInfoContent -replace $lastBuiltAssemblyPattern, "LastBuiltAssembly(`"$newLastBuiltAssemblyVersion`")"
$updatedContent = $updatedContent -replace $assemblyVersionPattern, "AssemblyVersion(`"$newAssemblyVersion`")"

# Save the updated content back to AssemblyInfo.cs
$updatedContent | Set-Content $assemblyInfoPath