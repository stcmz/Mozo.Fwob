# See https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage?tabs=windows
# See https://stackoverflow.com/questions/70321465/how-to-get-code-coverage-from-unit-tests-in-visual-studio-2022-community-edition
# The coverlet.collector and coverlet.msbuild packages must be installed to test project only

# This only needs to be installed once (globally), if installed it fails silently
dotnet tool install -g dotnet-reportgenerator-globaltool 2>$null

# Save currect directory into a variable
$dir = pwd

# Delete previous test run results (there's a bunch of subfolders named with guids)
Remove-Item -Recurse -Force $dir/test/TestResults/ 2>$null

# Run the Coverlet.Collector
#dotnet test --collect:"XPlat Code Coverage" 2>&1
# Or if MSBuild is installed use the following
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Delete previous test run reports - note if you're getting wrong results do a Solution Clean and Rebuild to remove stale DLLs in the bin folder
Remove-Item -Recurse -Force $dir/CoverageReport/ 2>$null

# To keep a history of the Code Coverage we need to use the argument: -historydir:SOME_DIRECTORY 
if (!(Test-Path -path $dir/CoverageHistory)) {
    New-Item -ItemType directory -Path $dir/CoverageHistory
}

# Generate the Code Coverage HTML Report
reportgenerator -reports:"$dir/**/coverage.cobertura.xml" -targetdir:"$dir/CoverageReport" -reporttypes:Html -historydir:$dir/CoverageHistory 

# Open the Code Coverage HTML Report (if running on a WorkStation)
$osInfo = Get-CimInstance -ClassName Win32_OperatingSystem
if ($osInfo.ProductType -eq 1) {
    (& "$dir/CoverageReport/index.html")
}