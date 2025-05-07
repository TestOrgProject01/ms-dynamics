# Path to AssemblyInfo.cs
$assemblyInfoPath = "..\Properties\AssemblyInfo.cs"

# Read the AssemblyInfo.cs content
$assemblyInfoContent = Get-Content $assemblyInfoPath

# Extract the current AssemblyVersion
$assemblyVersionPattern = 'AssemblyVersion\("(\d+\.\d+\.\d+\.\d+)"\)'
$assemblyVersionMatch = $assemblyInfoContent -match $assemblyVersionPattern
$currentAssemblyVersion = $matches[1]

# Update the LastBuiltAssembly field
$updatedContent = $assemblyInfoContent -replace 'LastBuiltAssembly\(".*"\)', "LastBuiltAssembly(`"$currentAssemblyVersion`")"

# Update the AssemblyVersion to match LastBuiltAssembly but with wildcard minor version
$versionParts = $currentAssemblyVersion.Split('.')
$wildcardAssemblyVersion = "$($versionParts[0]).$($versionParts[1]).*.$($versionParts[3])"
$updatedContent = $updatedContent -replace $assemblyVersionPattern, "AssemblyVersion(`"$wildcardAssemblyVersion`")"

# Save the updated content back to AssemblyInfo.cs
$updatedContent | Set-Content $assemblyInfoPath
```